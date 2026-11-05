using Infrastructure.SqlServer.Seed;
using UseCase.Recommendations;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.Tests;

// Relevance sets are specified by product code before running or tuning the ranker.
public sealed class RecommendationAcceptanceTests(ITestOutputHelper output)
{
    private sealed class Catalog(RecommendationManifest manifest) : IRecommendationCatalog
    {
        public List<string> Categories { get; } = manifest.Products.Select(p => p.Category).Distinct().ToList();
        public Task<List<VehicleOption>> VehiclesAsync(CancellationToken token = default) => Task.FromResult(manifest.Vehicles.Select((v, i) => new VehicleOption { Id = i + 1, Brand = v.Brand, Model = v.Model, Variant = v.Variant, Market = v.Market, Aliases = v.Aliases }).ToList());
        public Task<List<RecommendationProduct>> ProductsAsync(CancellationToken token = default) => Task.FromResult(manifest.Products.Select((p, i) => new RecommendationProduct {
            Id = i + 1, Name = p.Name, Brand = p.Brand, CategoryId = Categories.IndexOf(p.Category) + 1, Category = p.Category, Price = 150000 + i * 25000, Quantity = p.Stock,
            Tags = p.Tags, UseCases = p.Use, Fitment = (FitmentKind)p.Kind, Fitments = p.Vehicle is null ? [] : [new FitmentRecord {
                VehicleModelId = manifest.Vehicles.FindIndex(v => v.Key == p.Vehicle) + 1, YearFrom = p.From!.Value, YearTo = p.To!.Value, EvidenceUrl = manifest.Sources[p.Source] }]
        }).ToList());
    }
    private sealed record Case(string Name, string Query, string[] Relevant, string? Vehicle = null, int? Year = null, double? Max = null);
    private static readonly string[] Helmets = ["M35.1 SCUDO II", "V33", "HX36F VERTIGO", "D-VISOR M30.3 LCR", "STRADA 2 V20.2", "D-VISOR M30.3", "M10.2 VISTA"];
    private static readonly string[] Rain = ["ARN04", "ARN03", "ARS4T", "RRS08"];
    private static readonly string[] Chain = ["C2", "C3"];
    private static readonly string[] HelmetCleaning = ["M1", "M2"];

    [Fact]
    public async Task ThirtyPredeterminedCasesEnforceConstraintsAndMeasureTopThree()
    {
        var manifest = await RecommendationSeed.ReadAsync(Path.Combine(AppContext.BaseDirectory, "recommendations.json"));
        var catalog = new Catalog(manifest); var engine = new ProductRecommendationService(catalog);
        var positive = new[] {
            new Case("mũ có dấu", "mũ bảo hiểm", Helmets), new Case("mũ không dấu", "mu bao hiem", Helmets),
            new Case("nón không dấu", "bao hiem", Helmets), new Case("áo mưa", "áo mưa", Rain),
            new Case("áo mưa không dấu", "ao mua", Rain), new Case("bộ đồ mưa", "quần áo mưa", Rain),
            new Case("dưỡng sên", "dưỡng sên", Chain), new Case("dưỡng sên không dấu", "duong sen", Chain),
            new Case("mũ vệ sinh", "vệ sinh mũ", HelmetCleaning), new Case("mũ vệ sinh không dấu", "ve sinh mu", HelmetCleaning),
            new Case("Forza kính", "kính chắn gió", ["D1166ST"], "forza", 2019),
            new Case("Forza tay lái", "chắn gió tay lái", ["DF1166"], "forza", 2020),
            new Case("CB500X kính", "kính chắn gió", ["D1171ST", "D1171S"], "cb500x", 2020),
            new Case("CB500X khung", "khung bảo vệ động cơ", ["TN1171", "TNH1171"], "cb500x", 2022),
            new Case("CB500X két", "bảo vệ két nước", ["PR1121", "PR1171"], "cb500x", 2020),
            new Case("CB500X két đời cuối", "bảo vệ két nước", ["PR1171"], "cb500x", 2023),
            new Case("CB650R két", "bảo vệ két nước", ["PR1208"], "cb650r", 2025),
            new Case("Tracer kính", "kính chắn gió", ["D2130S"], "tracer", 2018),
            new Case("Tracer khung", "khung bảo vệ động cơ", ["TN2130"], "tracer", 2018),
            new Case("PCX kính", "kinh chan gio", ["D1190ST"], "pcx", 2022)
        };
        double precision = 0;
        foreach (var test in positive)
        {
            int? vehicle = test.Vehicle is null ? null : manifest.Vehicles.FindIndex(v => v.Key == test.Vehicle) + 1;
            var result = await engine.RecommendAsync(new RecommendationQuery { Query = test.Query, VehicleModelId = vehicle, Year = test.Year, MaxPrice = test.Max, Limit = 3 });
            Assert.True(result.Items.Count > 0, test.Name + ": no items");
            var correct = result.Items.Count(x => test.Relevant.Contains(manifest.Products[x.Id - 1].Code));
            output.WriteLine($"{test.Name}: {string.Join(", ", result.Items.Select(x => manifest.Products[x.Id - 1].Code))}; {correct}/{result.Items.Count}");
            precision += (double)correct / result.Items.Count;
            foreach (var item in result.Items)
            {
                var p = manifest.Products[item.Id - 1];
                Assert.True(p.Stock > 0 && (!test.Max.HasValue || item.Price <= test.Max), test.Name + ": stock/price");
                Assert.True(p.Kind == 1 || (p.Kind == 2 && p.Vehicle == test.Vehicle && p.From <= test.Year && p.To >= test.Year), test.Name + ": fitment");
            }
        }
        Assert.True(precision / positive.Length >= .8, $"Precision@3 = {precision / positive.Length:P1}");
        var negatives = new (string Name, RecommendationQuery Query, string Status)[] {
            ("thiếu đời", new() { Query="kính chắn gió", VehicleModelId=1 }, "NeedMoreInfo"),
            ("thiếu xe", new() { Query="kính chắn gió" }, "NeedMoreInfo"),
            ("năm không có xe", new() { Year=2022 }, "NeedMoreInfo"),
            ("xe không tồn tại", new() { VehicleModelId=999, Year=2022 }, "NeedMoreInfo"),
            ("giá âm", new() { MaxPrice=-1 }, "NeedMoreInfo"),
            ("giá đảo ngược", new() { MinPrice=500000, MaxPrice=100000 }, "NeedMoreInfo"),
            ("giá không đủ", new() { Query="mũ bảo hiểm", MaxPrice=1 }, "NoMatches"),
            ("ngoài đời", new() { Query="kính chắn gió", VehicleModelId=1, Year=2018 }, "NoMatches"),
            ("không có từ khóa", new() { Query="xyzkhongtonthai" }, "NoMatches"),
            ("giới hạn không hợp lệ", new() { Limit=0 }, "NeedMoreInfo")
        };
        foreach (var test in negatives)
        {
            var result = await engine.RecommendAsync(test.Query);
            Assert.True(result.Status == test.Status && result.Items.Count == 0, $"{test.Name}: {result.Status}");
        }
    }
}
