using AutoMapper;
using Infrastructure.Controllers;
using Infrastructure.EditorImages;
using Infrastructure.Models;
using Infrastructure.SqlServer;
using Infrastructure.SqlServer.AutoMapper;
using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using UseCase;
using UseCase.Business_Logic;
using UseCase.Caching;
using UseCase.UnitOfWork;
using Xunit;

namespace Infrastructure.Tests;

public sealed class AdminLogicTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuestCodStockProcessingPersistsQuantityAndSoldWithRealRepository(bool cached)
    {
        await using var store = await Store.Create();
        var saved = await store.Products.AddProductAsync(new Entities.Product { Name = "COD product", Brand = "Actual", CategoryId = 1, Description = "Description", ImageUrl = "", Price = 100000, Quantity = 5 });
        var mapper = new MapperConfiguration(config => config.AddProfile<EntityMapper>()).CreateMapper();
        var unit = new CartItemUnitOfWork(store.Db, mapper);
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        ICartManage carts = cached ? new CachableCartManage(unit, cache, new()) : new CartManage(unit);
        await carts.OrderProcessingGuestAsync([new Entities.CartItem { ProductId = saved.Id, Quantity = 2 }]);
        var result = await store.Db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(3, result.Quantity);
        Assert.Equal(2, result.Sold);
    }

    [Fact]
    public async Task CachedArticleEditIsVisibleInDetailAndListAndPreservesCover()
    {
        await using var store = await Store.Create();
        var articles = store.Articles;
        var added = await articles.AddPostAsync(1, "Original title", "<p>Original</p>", "original.png");
        await articles.GetPostsAsync();
        await articles.GetPostDetailAsync(added.Id);
        await articles.UpdatePostAsync(new Entities.Post { Id = added.Id, Title = "Changed title", Content = "<p>Changed</p>" });
        Assert.Equal("Changed title", (await articles.GetPostDetailAsync(added.Id)).Title);
        Assert.Equal("Changed title", Assert.Single(await articles.GetPostsAsync()).Title);
        Assert.Equal("original.png", (await store.Db.Posts.AsNoTracking().SingleAsync()).ImageUrl);
        await articles.DeletePostAsync(added.Id);
        Assert.Empty(await articles.GetPostsAsync());
    }

    [Fact]
    public async Task ProductCreateReturnsGeneratedIdAndCouponBelongsToThatProductThenDeleteWithoutImageWorks()
    {
        await using var store = await Store.Create();
        var products = store.Products;
        var saved = await products.AddProductAsync(new Entities.Product { Name = "Actual", Brand = "Actual", CategoryId = 1, Description = "Description", ImageUrl = "", Price = 250000 });
        Assert.True(saved.Id > 0);
        await products.AddOrUpdateCouponAsync(saved.Id, "TEST", 12);
        Assert.Equal(saved.Id, (await store.Db.Discounts.SingleAsync()).Id);
        await products.DeleteProductAsync(saved.Id);
        Assert.Empty(await store.Db.Products.ToListAsync());
        Assert.Empty(await store.Db.Discounts.ToListAsync());
    }

    [Fact]
    public async Task ProductEditUsesDatabasePriceInsteadOfAnotherTabsTempData()
    {
        await using var store = await Store.Create();
        var saved = await store.Products.AddProductAsync(new Entities.Product { Name = "Actual", Brand = "Actual", CategoryId = 1, Description = "Description", ImageUrl = "", Price = 250000 });
        var environment = Mock.Of<IWebHostEnvironment>(env => env.EnvironmentName == "Development");
        var controller = new ProductController(store.Products, NullLogger<ProductController>.Instance, Mock.Of<IReviewerFinder>(), environment, Content());
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()) { ["Price"] = "100", ["OldPrice"] = "999" };
        await controller.Edit(new ProductModel { Id = saved.Id, Name = "Updated", Brand = "Actual", CategoryId = 1, Description = "<p>Description</p>", Price = 200000 }, saved.Id);
        var result = await store.Db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(200000, result.Price);
        Assert.Equal(250000, result.OldPrice);
    }

    [Fact]
    public async Task ArticleControllerUpdatesRealStoreAndRedirectsInsteadOfRenderingEmptyEditForm()
    {
        await using var store = await Store.Create();
        var saved = await store.Articles.AddPostAsync(1, "Original", "Original", "cover.png");
        await store.Articles.GetPostsAsync();
        var environment = Mock.Of<IWebHostEnvironment>();
        var controller = new UserController(Mock.Of<IUserManage>(), Mock.Of<IReviewerFinder>(), store.Articles, environment, Content());
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Sid, "1")], "test")) };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());
        var response = Assert.IsType<RedirectToActionResult>(await controller.Blog(new PostModel { Title = "Updated", Content = "<p>Updated content</p>" }, saved.Id.ToString()));
        Assert.Equal(nameof(UserController.BlogManage), response.ActionName);
        Assert.Equal("Updated", (await store.Articles.GetPostDetailAsync(saved.Id)).Title);
        Assert.Equal("cover.png", (await store.Db.Posts.AsNoTracking().SingleAsync()).ImageUrl);
    }

    private static EditorContentService Content() => new(new EditorImagePolicy(Options.Create(new EditorImageOptions { CloudName = "test-cloud" })));
    private sealed class Store : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
        public ShopContext Db { get; }
        public CachablePostManage Articles { get; }
        public ProductManage Products { get; }
        private Store(SqliteConnection connection)
        {
            _connection = connection;
            Db = new TestContext(new DbContextOptionsBuilder<ShopContext>().UseSqlite(connection).Options);
            var mapper = new MapperConfiguration(config => config.AddProfile<EntityMapper>()).CreateMapper();
            Articles = new CachablePostManage(new PostRepository(Db, mapper), _cache, new(), NullLogger<CachablePostManage>.Instance);
            Products = new ProductManage(new ProductUnitOfWork(Db, mapper));
        }
        public static async Task<Store> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var store = new Store(connection);
            await store.Db.Database.EnsureCreatedAsync();
            store.Db.Roles.Add(new Role { Id = 1, Name = "Admin" });
            store.Db.Users.Add(new User { Id = 1, RoleId = 1, Username = "test", Password = "TEST-ONLY", Email = "test@example.invalid", DisplayName = "Test" });
            store.Db.Categories.AddRange(new Category { Id = 1, Name = "Products" }, new Category { Id = 10, Name = "Articles" });
            await store.Db.SaveChangesAsync();
            return store;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    private sealed class TestContext(DbContextOptions<ShopContext> options) : ShopContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder builder) { }
    }
}
