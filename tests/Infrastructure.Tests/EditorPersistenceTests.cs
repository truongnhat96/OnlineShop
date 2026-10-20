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
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UseCase.Business_Logic;
using UseCase.UnitOfWork;
using Xunit;

namespace Infrastructure.Tests;

public sealed class EditorPersistenceTests
{
    private static EditorContentService Content() => new(new EditorImagePolicy(Options.Create(new EditorImageOptions { CloudName = "test-cloud" })));
    [Fact]
    public async Task RealRepositoriesPersistUrlsAndRejectBase64WithoutChangingExistingRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new IsolatedShopContext(new DbContextOptionsBuilder<ShopContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Roles.Add(new Role { Id = 1, Name = "Admin" });
        db.Users.Add(new User { Id = 1, Username = "fixture", DisplayName = "Fixture", Email = "fixture@example.invalid", Password = "TEST-ONLY", RoleId = 1 });
        db.Categories.Add(new Category { Id = 1, Name = "Fixture" });
        await db.SaveChangesAsync();
        var mapper = new MapperConfiguration(config => config.AddProfile<EntityMapper>()).CreateMapper();
        var posts = new PostRepository(db, mapper);
        var products = new ProductRepository(db, mapper);
        var html = "<p>Nội dung thử nghiệm</p><img src='https://res.cloudinary.com/test-cloud/image/upload/v1/sample.png'>";
        var post = new Entities.Post { Id = Guid.NewGuid(), UserId = 1, CategoryId = 1, Title = "Fixture", Content = Content().PrepareForSave(html), ImageUrl = "cover.png", CreatedAt = DateTime.UtcNow };
        await posts.AddPostAsync(post);
        var product = new Entities.Product { Name = "Fixture", Brand = "Fixture", CategoryId = 1, Description = Content().PrepareForSave(html, true), ImageUrl = "product.png", Quantity = 3, Price = 100, Date_Import = DateTime.Today };
        await products.AddProductAsync(product);
        var storedPost = await db.Posts.AsNoTracking().SingleAsync();
        var storedProduct = await db.Products.AsNoTracking().SingleAsync();
        Assert.Contains("https://res.cloudinary.com/test-cloud/", storedPost.Content);
        Assert.DoesNotContain("data:image", storedPost.Content);
        Assert.StartsWith(EditorContentService.ProductPrefix, storedProduct.Description);
        Assert.DoesNotContain("base64", storedProduct.Description);
        Assert.Equal("cover.png", storedPost.ImageUrl);
        Assert.Equal("product.png", storedProduct.ImageUrl);
        Assert.Equal(3, storedProduct.Quantity);
        var bad = "<p>Không được ghi</p><img src='data:image/png;base64,AAAA'>";
        Assert.Throws<EditorImageException>(() => Content().PrepareForSave(bad));
        Assert.Throws<EditorImageException>(() => Content().PrepareForSave(bad, true));
        Assert.Equal(storedPost.Content, (await db.Posts.AsNoTracking().SingleAsync()).Content);
        Assert.Equal(storedProduct.Description, (await db.Products.AsNoTracking().SingleAsync()).Description);
        post.Content = Content().PrepareForSave(html.Replace("thử nghiệm", "cập nhật"));
        await posts.UpdatePostAsync(post);
        var saved = await products.GetProductsAsync();
        var updated = Assert.Single(saved); updated.Description = Content().PrepareForSave("<p>Mô tả cập nhật</p>", true);
        await products.UpdateProductAsync(updated);
        Assert.Contains("cập nhật", (await db.Posts.AsNoTracking().SingleAsync()).Content);
        Assert.Contains("cập nhật", (await db.Products.AsNoTracking().SingleAsync()).Description);
    }

    [Fact]
    public async Task BothArticleAndProductGuardsRunBeforeCoverWritesOrServices()
    {
        const string bad = "<img src='data:image/png;base64,AAAA'>";
        var articles = new Mock<IPostManage>(MockBehavior.Strict);
        var products = new Mock<IProductManage>(MockBehavior.Strict);
        products.Setup(p => p.GetAllCategoriesAsync()).ReturnsAsync(new List<Entities.Category>());
        var file = new Mock<IFormFile>(MockBehavior.Strict);
        var environment = new Mock<IWebHostEnvironment>(MockBehavior.Strict);
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        var articleController = new UserController(Mock.Of<IUserManage>(), Mock.Of<IReviewerFinder>(), articles.Object, environment.Object, Content());
        var result = Assert.IsType<ViewResult>(await articleController.Blog(new PostModel { Title = "Giữ tiêu đề", Content = bad, ImageUrl = file.Object }, ""));
        Assert.Equal(bad, Assert.IsType<PostModel>(result.Model).Content);
        Assert.False(articleController.ModelState.IsValid);
        var productController = new ProductController(products.Object, NullLogger<ProductController>.Instance, Mock.Of<IReviewerFinder>(), environment.Object, Content());
        Assert.IsType<ViewResult>(await productController.Add(new ProductModel { Name = "Giữ tên", Description = bad, ImageUrl = file.Object }));
        Assert.False(productController.ModelState.IsValid);
        Assert.IsType<ViewResult>(await productController.Edit(new ProductModel { Id = 1, Description = bad, ImageUrl = file.Object }, 1));
        articles.VerifyNoOtherCalls();
        products.Verify(p => p.GetAllCategoriesAsync(), Times.Once);
        products.VerifyNoOtherCalls();
        file.VerifyNoOtherCalls(); environment.VerifyGet(e => e.EnvironmentName, Times.Once); environment.VerifyNoOtherCalls();
    }

    // Explicitly replace OnConfiguring: the production context hardcodes SQL Server.
    // This test context can only use the already-open in-memory SQLite connection.
    private sealed class IsolatedShopContext(DbContextOptions<ShopContext> options) : ShopContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }
    }
}
