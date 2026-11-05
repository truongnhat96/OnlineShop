using Infrastructure.SqlServer.DataContext;
using Infrastructure.SqlServer.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

if (args.Length != 2 || (args[0] != "--apply-main" && args[0] != "--apply-test"))
{
    Console.Error.WriteLine("Usage: RecommendationSeed --apply-main|--apply-test <manifest-path>");
    return 2;
}
var manifest = await RecommendationSeed.ReadAsync(Path.GetFullPath(args[1]));
var targetDatabase = args[0] == "--apply-main" ? "OnlineShop" : "OnlineShop_Recommendation_Test";
var options = new DbContextOptionsBuilder<ShopContext>().UseSqlServer($@"Server=.\SQLEXPRESS;Database={targetDatabase};Trusted_Connection=True;TrustServerCertificate=True;").UseLazyLoadingProxies().Options;
await using var db = new ShopContext(options);
var connection = (SqlConnection)db.Database.GetDbConnection();
await connection.OpenAsync();
if (!connection.Database.Equals(targetDatabase, StringComparison.OrdinalIgnoreCase) || !connection.DataSource.Equals(@".\SQLEXPRESS", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Sai database đích; không thực hiện seed.");
Console.WriteLine($"Target database: {connection.Database}; configured server: {connection.DataSource}");
var added = await RecommendationSeed.ApplyAsync(db, manifest);
Console.WriteLine($"Added: {added}; manifest total: {manifest.Products.Count}.");
return 0;
