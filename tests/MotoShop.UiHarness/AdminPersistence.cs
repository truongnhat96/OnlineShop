using AutoMapper;
using Infrastructure.SqlServer;
using Infrastructure.SqlServer.AutoMapper;
using Infrastructure.SqlServer.DataContext;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UseCase;
using UseCase.Business_Logic;
using UseCase.Caching;
using UseCase.Repository;
using UseCase.UnitOfWork;

// TEST ONLY. Real application services/repositories over isolated SQLite, never SQL Server.
internal static class AdminPersistence
{
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton(_ => { var connection = new SqliteConnection("Data Source=:memory:"); connection.Open(); return connection; });
        services.AddScoped<ShopContext>(provider => new TestContext(new DbContextOptionsBuilder<ShopContext>().UseSqlite(provider.GetRequiredService<SqliteConnection>()).Options));
        services.AddSingleton<IMapper>(new MapperConfiguration(config => config.AddProfile<EntityMapper>()).CreateMapper());
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IProductUnitOfWork, ProductUnitOfWork>();
        services.AddScoped<IProductManage, ProductManage>();
        services.AddScoped<IPostManage>(provider => new CachablePostManage(provider.GetRequiredService<IPostRepository>(), provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(), new(), provider.GetRequiredService<ILogger<CachablePostManage>>()));
        services.AddScoped<ICategoryManage>(provider => new CachableCategoryManage(provider.GetRequiredService<IProductUnitOfWork>(), provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(), new(), provider.GetRequiredService<ILogger<CachableCategoryManage>>()));
    }

    public static async Task Seed(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        await db.Database.EnsureCreatedAsync();
        db.Roles.Add(new Role { Id = 1, Name = "Admin" });
        db.Users.Add(new User { Id = 1, RoleId = 1, Username = "admin-test", DisplayName = "Admin test", Email = "admin@example.invalid", Password = "TEST-ONLY" });
        db.Categories.AddRange(new Category { Id = 1, Name = "Test products" }, new Category { Id = 10, Name = "Test articles" });
        for (var i = 1; i <= 26; i++)
        {
            db.Products.Add(new Product { Id = i, Name = $"Test product {i:00}", Brand = "Test", CategoryId = 1, Description = "Original description", ImageUrl = "", Price = 100000 + i, Quantity = i == 2 ? 0 : i, Sold = i, Date_Import = DateTime.Today });
            db.Posts.Add(new Post { Id = Guid.NewGuid(), Title = $"Test article {i:00}", Content = "<p>Original article</p>", ImageUrl = "existing-cover.png", UserId = 1, CategoryId = 10, CreatedAt = DateTime.Today.AddDays(-i) });
        }
        await db.SaveChangesAsync();
    }

    public static void MapState(WebApplication app) => app.MapGet("/_fixture/admin-state", async (ShopContext db) => Results.Json(new {
        products = await db.Products.AsNoTracking().ToListAsync(),
        posts = await db.Posts.AsNoTracking().ToListAsync(),
        discounts = await db.Discounts.AsNoTracking().ToListAsync(),
        credentials = await db.ItemInfors.AsNoTracking().ToListAsync()
    })).RequireAuthorization(policy => policy.RequireRole("Admin"));

    private sealed class TestContext(DbContextOptions<ShopContext> options) : ShopContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder builder) { }
    }
}
