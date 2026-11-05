using UseCase.Recommendations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PersonalizationRankingTests
{
    private sealed class Catalog : IRecommendationCatalog
    {
        public List<RecommendationProduct> Data { get; } = [
            new() { Id=1, Name="Mũ bảo hiểm đô thị", CategoryId=1, Category="Mũ bảo hiểm", Brand="A", Tags="mũ bảo hộ", Price=300, Quantity=5, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=2, Name="Mũ bảo hiểm du lịch", CategoryId=1, Category="Mũ bảo hiểm", Brand="A", Tags="mũ bảo hộ", Price=500, Quantity=5, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=3, Name="Nón bảo hiểm ba phần tư", CategoryId=1, Category="Mũ bảo hiểm", Brand="B", Tags="mũ bảo hộ", Price=400, Quantity=5, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=4, Name="Dung dịch vệ sinh sên", CategoryId=2, Category="Chăm sóc bảo dưỡng", Brand="C", Tags="vệ sinh sên", Price=100, Quantity=5, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=5, Name="Dung dịch dưỡng sên", CategoryId=2, Category="Chăm sóc bảo dưỡng", Brand="C", Tags="dưỡng sên", Price=150, Quantity=5, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=6, Name="Dung dịch rửa xe", CategoryId=2, Category="Chăm sóc bảo dưỡng", Brand="C", Tags="vệ sinh xe", Price=200, Quantity=5, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=7, Name="Kính chắn gió xe", CategoryId=3, Category="Kính chắn gió", Brand="A", Price=300, Quantity=3, Fitment=FitmentKind.Verified, Fitments=[new() {VehicleModelId=1, YearFrom=2020, YearTo=2022, EvidenceUrl="https://example.test/source"}] },
            new() { Id=8, Name="Mũ bảo hiểm hết hàng", CategoryId=1, Category="Mũ bảo hiểm", Brand="A", Price=100, Quantity=0, Fitment=FitmentKind.VehicleIndependent },
            new() { Id=9, Name="Kính chưa xác minh", CategoryId=3, Category="Kính chắn gió", Brand="A", Price=100, Quantity=3, Fitment=FitmentKind.Unknown }
        ];
        public Task<List<RecommendationProduct>> ProductsAsync(CancellationToken token = default) => Task.FromResult(Data);
        public Task<List<VehicleOption>> VehiclesAsync(CancellationToken token = default) => Task.FromResult(new List<VehicleOption> { new() {Id=1, Brand="Honda", Model="SH", Market="VN"} });
    }
    private sealed class Profile(RecommendationProfile value) : IRecommendationProfileProvider
    { public Task<RecommendationProfile> GetAsync(CancellationToken token = default) => Task.FromResult(value); }
    private static RecommendationProfile History(int id, string kind, int days) => new() {
        Enabled=true, Events=[new() { ProductId=id, Kind=kind, At=DateTimeOffset.UtcNow.AddDays(-days) }]
    };

    // The expected categories below are fixed independently of the scoring implementation.
    [Theory]
    [InlineData(1,"view",0,1)] [InlineData(1,"view",7,1)] [InlineData(1,"view",14,1)] [InlineData(1,"view",89,1)]
    [InlineData(1,"cart",0,1)] [InlineData(1,"cart",14,1)] [InlineData(2,"view",0,1)] [InlineData(2,"cart",14,1)]
    [InlineData(3,"view",1,1)] [InlineData(3,"cart",60,1)] [InlineData(4,"view",0,2)] [InlineData(4,"view",7,2)]
    [InlineData(4,"view",14,2)] [InlineData(4,"view",89,2)] [InlineData(4,"cart",0,2)] [InlineData(4,"cart",14,2)]
    [InlineData(5,"view",0,2)] [InlineData(5,"cart",14,2)] [InlineData(6,"view",1,2)] [InlineData(6,"cart",60,2)]
    public async Task HistoryProducesRelevantTopThree(int productId, string kind, int age, int expectedCategory)
    {
        var catalog = new Catalog();
        var result = await new ProductRecommendationService(catalog, new Profile(History(productId, kind, age))).RecommendAsync(new() {Limit=3});
        Assert.Equal("Personalized", result.Mode);
        Assert.Equal(3, result.Items.Count);
        Assert.True(result.Items.Count(item => catalog.Data.Single(p => p.Id == item.Id).CategoryId == expectedCategory) >= 3);
    }
    [Fact] public async Task DifferentUsersReceiveDifferentRankings()
    {
        var catalog = new Catalog();
        var one = await new ProductRecommendationService(catalog, new Profile(History(1,"view",0))).RecommendAsync(new());
        var two = await new ProductRecommendationService(catalog, new Profile(History(4,"view",0))).RecommendAsync(new());
        Assert.NotEqual(one.Items[0].Id, two.Items[0].Id);
    }
    [Fact] public async Task CurrentNeedCanChangeDirection()
    {
        var result = await new ProductRecommendationService(new Catalog(), new Profile(History(1,"view",0))).RecommendAsync(new() {Query="dung dịch dưỡng sên"});
        Assert.Equal(5, result.Items[0].Id);
    }
    [Fact] public async Task NoConsentIsExploreEvenWithEvents()
    {
        var profile = History(1,"view",0); profile.Enabled=false;
        var result = await new ProductRecommendationService(new Catalog(), new Profile(profile)).RecommendAsync(new());
        Assert.Equal("Explore", result.Mode);
    }
    [Fact] public async Task SearchOnlyHistoryCanPersonalize()
    {
        var profile = new RecommendationProfile { Enabled=true, Events=[new() {Kind="search", Terms="bao hiem", At=DateTimeOffset.UtcNow}] };
        var catalog = new Catalog(); var result = await new ProductRecommendationService(catalog, new Profile(profile)).RecommendAsync(new());
        Assert.Equal("Personalized", result.Mode); Assert.Equal(1, catalog.Data.Single(p=>p.Id==result.Items[0].Id).CategoryId);
    }
    [Fact] public async Task ExpiredHistoryIsExplore()
    {
        var result = await new ProductRecommendationService(new Catalog(), new Profile(History(1,"view",91))).RecommendAsync(new());
        Assert.Equal("Explore", result.Mode);
    }
    [Fact] public async Task CartProductsAreExcluded()
    {
        var profile = History(1,"cart",0); profile.CartProductIds.Add(1);
        var result = await new ProductRecommendationService(new Catalog(), new Profile(profile)).RecommendAsync(new());
        Assert.DoesNotContain(result.Items, p=>p.Id==1);
    }
    [Theory]
    [InlineData(2019, false)] [InlineData(2020, true)] [InlineData(2021, true)] [InlineData(2022, true)] [InlineData(2023, false)]
    public async Task FitmentMustMatchExactYear(int year, bool expected)
    {
        var profile = History(7,"view",0); profile.VehicleModelId=1; profile.Year=year;
        var result = await new ProductRecommendationService(new Catalog(), new Profile(profile)).RecommendAsync(new() {Limit=20});
        Assert.Equal(expected, result.Items.Any(p=>p.Id==7));
        Assert.DoesNotContain(result.Items,p=>p.Id==9);
    }
    [Theory] [InlineData(99)] [InlineData(200)] [InlineData(300)] [InlineData(400)] [InlineData(500)]
    public async Task PriceAndStockAreAlwaysRespected(int maxPrice)
    {
        var result = await new ProductRecommendationService(new Catalog(), new Profile(History(1,"view",0))).RecommendAsync(new() {MaxPrice=maxPrice,Limit=20});
        Assert.All(result.Items, p => { Assert.True(p.Price <= maxPrice); Assert.True(p.Quantity > 0); });
        Assert.DoesNotContain(result.Items,p=>p.Id==8);
    }
}
