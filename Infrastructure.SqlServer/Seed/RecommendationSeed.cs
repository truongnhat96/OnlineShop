using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infrastructure.SqlServer.DataContext;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.SqlServer.Seed;

public sealed class RecommendationManifest
{
    public Dictionary<string, string> Sources { get; set; } = [];
    public List<SeedVehicle> Vehicles { get; set; } = [];
    public List<SeedProduct> Products { get; set; } = [];
}
public sealed class SeedVehicle
{
    public string Key { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Market { get; set; } = "";
    public string Aliases { get; set; } = "";
}
public sealed class SeedProduct
{
    public string Category { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public int Kind { get; set; }
    public string? Vehicle { get; set; }
    public int? From { get; set; }
    public int? To { get; set; }
    public string Tags { get; set; } = "";
    public string Use { get; set; } = "";
    public int Stock { get; set; } = 8;
}

public static class RecommendationSeed
{
    private static readonly Dictionary<string, int> RequiredCategories = new() {
        ["Phụ kiện tiện ích"] = 8, ["Tay lái & Gương"] = 6, ["Bảo vệ xe"] = 6,
        ["Điện & chiếu sáng"] = 6, ["Chăm sóc & bảo dưỡng"] = 6, ["Đồ bảo hộ"] = 8
    };
    public static async Task<RecommendationManifest> ReadAsync(string path, CancellationToken token = default)
    {
        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<RecommendationManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, token)
            ?? throw new InvalidDataException("Manifest rỗng.");
        Validate(manifest);
        return manifest;
    }
    public static string SeedKey(SeedProduct p) => "motoshop-v1-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Brand.ToUpperInvariant() + "|" + p.Code.ToUpperInvariant())))[..24];
    public static void Validate(RecommendationManifest m)
    {
        if (m.Products.Count != 40 || RequiredCategories.Any(c => m.Products.Count(p => p.Category == c.Key) != c.Value) ||
            m.Products.Count(p => p.Stock == 0) != 4 || m.Products.Any(p => p.Stock < 0) ||
            m.Products.Select(SeedKey).Distinct().Count() != 40 || m.Vehicles.Select(v => v.Key).Distinct().Count() != m.Vehicles.Count)
            throw new InvalidDataException("Phân bố sản phẩm, tồn kho hoặc seed key không hợp lệ.");
        foreach (var p in m.Products)
        {
            if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Brand) || string.IsNullOrWhiteSpace(p.Code) || p.Kind is < 0 or > 2 ||
                !m.Sources.TryGetValue(p.Source, out var source) || !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length > 0)
                throw new InvalidDataException($"Nguồn hoặc metadata không hợp lệ: {p.Code}");
            if (p.Kind == 2 && (!m.Vehicles.Any(v => v.Key == p.Vehicle) || p.From is < 1900 or null || p.To is > 2100 or null || p.From > p.To))
                throw new InvalidDataException($"Tương thích không đầy đủ: {p.Code}");
        }
        if (m.Products.Count(p => p.Kind == 2) < 12 || m.Products.Where(p => p.Kind == 2).Select(p => p.Vehicle).Distinct().Count() < 4)
            throw new InvalidDataException("Chưa đủ sản phẩm và cấu hình xe được xác minh.");
    }

    public static async Task<int> ApplyAsync(ShopContext db, RecommendationManifest m, CancellationToken token = default)
    {
        Validate(m);
        var categoryRows = await db.Categories.Where(c => RequiredCategories.Keys.Contains(c.Name)).ToListAsync(token);
        if (RequiredCategories.Keys.Any(name => categoryRows.Count(c => c.Name == name) != 1))
            throw new InvalidOperationException("Cần đúng một danh mục cho mỗi tên trong manifest.");
        var existingKeys = (await db.Products.AsNoTracking().Where(p => p.RecommendationSeedKey != null).Select(p => p.RecommendationSeedKey!).ToListAsync(token)).ToHashSet();
        await using var tx = await db.Database.BeginTransactionAsync(token);
        var models = new Dictionary<string, VehicleModel>();
        foreach (var v in m.Vehicles)
        {
            var matches = await db.VehicleModels.Where(x => x.Brand == v.Brand && x.Model == v.Model && x.Variant == v.Variant && x.Market == v.Market).ToListAsync(token);
            if (matches.Count > 1) throw new InvalidOperationException($"Dòng xe trùng: {v.Brand} {v.Model} {v.Market}");
            var model = matches.SingleOrDefault() ?? new VehicleModel { Brand = v.Brand, Model = v.Model, Variant = v.Variant, Market = v.Market, Aliases = v.Aliases };
            if (matches.Count == 0) db.VehicleModels.Add(model);
            models.Add(v.Key, model);
        }
        int added = 0;
        foreach (var (p, index) in m.Products.Select((value, index) => (value, index)))
        {
            var key = SeedKey(p);
            if (existingKeys.Contains(key)) continue;
            var description = p.Kind switch {
                0 => "Chưa xác minh tương thích. Cần kiểm tra đui và hệ thống điện trước khi lắp.",
                1 => "Không phụ thuộc dòng xe; kiểm tra kích cỡ và hướng dẫn sử dụng trước khi chọn.",
                _ => "Chỉ tham khảo xe đúng phiên bản, thị trường và khoảng đời trong dữ liệu tương thích. Nên kiểm tra hướng dẫn lắp của hãng."
            };
            var item = new Product { Name = p.Name, Brand = p.Brand, CategoryId = categoryRows.Single(c => c.Name == p.Category).Id,
                ManufacturerCode = p.Code, ReferenceUrl = m.Sources[p.Source], RecommendationTags = p.Tags, RecommendationUseCases = p.Use,
                RecommendationSeedKey = key, IsSampleData = true, FitmentKind = p.Kind, Description = description,
                ImageUrl = "motoshop-seed-illustration.svg", Price = 150000 + index * 25000, Quantity = p.Stock, Sold = 0, Date_Import = DateTime.UtcNow };
            if (p.Kind == 2) item.Fitments.Add(new ProductFitment { VehicleModel = models[p.Vehicle!], YearFrom = p.From!.Value, YearTo = p.To!.Value, EvidenceUrl = m.Sources[p.Source] });
            db.Products.Add(item); added++;
        }
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return added;
    }
}
