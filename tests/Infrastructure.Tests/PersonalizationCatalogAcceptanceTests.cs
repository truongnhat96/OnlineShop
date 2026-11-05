using Infrastructure.SqlServer.Seed;
using UseCase.Recommendations;
using Xunit;
using Xunit.Abstractions;

namespace Infrastructure.Tests;

public sealed class PersonalizationCatalogAcceptanceTests(ITestOutputHelper output)
{
    private sealed class Catalog(RecommendationManifest manifest) : IRecommendationCatalog
    {
        private readonly List<string> categories = manifest.Products.Select(p => p.Category).Distinct().ToList();
        public Task<List<VehicleOption>> VehiclesAsync(CancellationToken token = default) => Task.FromResult(manifest.Vehicles.Select((v,i) => new VehicleOption {Id=i+1,Brand=v.Brand,Model=v.Model,Variant=v.Variant,Market=v.Market}).ToList());
        public Task<List<RecommendationProduct>> ProductsAsync(CancellationToken token = default) => Task.FromResult(manifest.Products.Select((p,i) => new RecommendationProduct {
            Id=i+1, Name=p.Name, Brand=p.Brand, CategoryId=categories.IndexOf(p.Category)+1, Category=p.Category,
            Price=150000+i*25000, Quantity=p.Stock, IsSample=true, Tags=p.Tags, UseCases=p.Use,
            Description=p.Kind==1 ? "Không phụ thuộc dòng xe; kiểm tra kích cỡ và hướng dẫn sử dụng trước khi chọn." : "Chỉ tham khảo xe đúng phiên bản, thị trường và khoảng đời trong dữ liệu tương thích. Nên kiểm tra hướng dẫn lắp của hãng.",
            Fitment=(FitmentKind)p.Kind, Fitments=p.Vehicle is null ? [] : [new() {VehicleModelId=manifest.Vehicles.FindIndex(v=>v.Key==p.Vehicle)+1,YearFrom=p.From!.Value,YearTo=p.To!.Value,EvidenceUrl=manifest.Sources[p.Source]}]
        }).ToList());
    }
    private sealed class Profile(RecommendationProfile profile) : IRecommendationProfileProvider
    { public Task<RecommendationProfile> GetAsync(CancellationToken token = default) => Task.FromResult(profile); }
    // Relevance categories and cases were fixed before measuring this dataset.
    [Fact]
    public async Task TwentyBehaviorHistoriesAchievePrecisionAtThree()
    {
        var manifest = await RecommendationSeed.ReadAsync(Path.Combine(AppContext.BaseDirectory,"recommendations.json"));
        var cases = new (string code,string kind,int age,string category)[] {
            ("M35.1 SCUDO II","view",0,"Đồ bảo hộ"),("M35.1 SCUDO II","view",14,"Đồ bảo hộ"),("M35.1 SCUDO II","cart",0,"Đồ bảo hộ"),
            ("V33","view",0,"Đồ bảo hộ"),("V33","view",7,"Đồ bảo hộ"),("V33","cart",14,"Đồ bảo hộ"),
            ("HX36F VERTIGO","view",0,"Đồ bảo hộ"),("D-VISOR M30.3 LCR","view",14,"Đồ bảo hộ"),
            ("STRADA 2 V20.2","cart",0,"Đồ bảo hộ"),("M10.2 VISTA","view",30,"Đồ bảo hộ"),
            ("C2","view",0,"Chăm sóc & bảo dưỡng"),("C2","view",14,"Chăm sóc & bảo dưỡng"),("C2","cart",0,"Chăm sóc & bảo dưỡng"),
            ("C3","view",0,"Chăm sóc & bảo dưỡng"),("C3","view",7,"Chăm sóc & bảo dưỡng"),("C3","cart",14,"Chăm sóc & bảo dưỡng"),
            ("E2","view",0,"Chăm sóc & bảo dưỡng"),("E2","view",14,"Chăm sóc & bảo dưỡng"),
            ("M1","cart",0,"Chăm sóc & bảo dưỡng"),("M2","view",30,"Chăm sóc & bảo dưỡng")
        };
        var catalog = new Catalog(manifest); double total=0;
        foreach (var test in cases)
        {
            var id=manifest.Products.FindIndex(p=>p.Code==test.code)+1;
            var profile=new RecommendationProfile {Enabled=true,Events=[new() {Kind=test.kind,ProductId=id,At=DateTimeOffset.UtcNow.AddDays(-test.age)}]};
            if (test.kind=="cart") profile.CartProductIds.Add(id);
            var result=await new ProductRecommendationService(catalog,new Profile(profile)).RecommendAsync(new() {Limit=3});
            Assert.Equal("Personalized",result.Mode); Assert.NotEmpty(result.Items);
            var correct=result.Items.Count(p=>manifest.Products[p.Id-1].Category==test.category);
            total+=(double)correct/result.Items.Count;
            Assert.All(result.Items,p=> { Assert.True(p.Quantity>0); Assert.DoesNotContain(p.Id,profile.CartProductIds); });
            output.WriteLine($"{test.code} {test.kind} {test.age}d: {correct}/{result.Items.Count}");
        }
        var precision=total/cases.Length;
        output.WriteLine($"macro Precision@3 = {precision:P1}");
        Assert.True(precision>=.8,$"macro Precision@3 = {precision:P1}");
    }
}
