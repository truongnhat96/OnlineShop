using Infrastructure.SqlServer.DataContext;
using Infrastructure.SqlServer.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public class RecommendationSeedTests
{
    [Fact]
    public async Task ManifestSeedsOnceWithoutOverwritingUserChangesOrCreatingReviews()
    {
        var manifest = await RecommendationSeed.ReadAsync(Path.Combine(AppContext.BaseDirectory, "recommendations.json"));
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ShopContext(new DbContextOptionsBuilder<ShopContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        foreach (var category in manifest.Products.Select(p => p.Category).Distinct()) db.Categories.Add(new Category { Name = category });
        await db.SaveChangesAsync();
        Assert.Equal(40, await RecommendationSeed.ApplyAsync(db, manifest));
        Assert.Equal(40, await db.Products.CountAsync());
        Assert.Equal(4, await db.Products.CountAsync(p => p.Quantity == 0));
        Assert.True(await db.ProductFitments.CountAsync() >= 12);
        Assert.Equal(0, await db.Reviews.CountAsync());
        var edited = await db.Products.OrderBy(p => p.Id).FirstAsync();
        edited.Price = 123456;
        await db.SaveChangesAsync();
        Assert.Equal(0, await RecommendationSeed.ApplyAsync(db, manifest));
        Assert.Equal(123456, (await db.Products.FindAsync(edited.Id))!.Price);
        Assert.Equal(40, await db.Products.CountAsync());
    }
}
