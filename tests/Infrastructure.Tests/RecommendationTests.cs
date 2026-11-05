using UseCase.Recommendations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class RecommendationTests
{
    private sealed class Catalog : IRecommendationCatalog
    {
        public List<RecommendationProduct> Data { get; } = [
            new() { Id=1, Name="Gương SH", Brand="A", CategoryId=1, Category="Gương", Price=300000, Quantity=3, Fitment=FitmentKind.Verified, Fitments=[new() { VehicleModelId=1, YearFrom=2020, YearTo=2022, EvidenceUrl="https://example.test/fitment" }] },
            new() { Id=2, Name="Gương xe khác", Brand="A", CategoryId=1, Category="Gương", Price=200000, Quantity=2, Fitment=FitmentKind.Verified, Fitments=[new() { VehicleModelId=2, YearFrom=2020, YearTo=2022, EvidenceUrl="https://example.test/fitment" }] },
            new() { Id=3, Name="Gương đã hết hàng", Brand="A", CategoryId=1, Category="Gương", Price=100000, Quantity=0, Fitment=FitmentKind.Verified, Fitments=[new() { VehicleModelId=1, YearFrom=2020, YearTo=2022, EvidenceUrl="https://example.test/fitment" }] },
            new() { Id=4, Name="Mũ bảo hiểm", Brand="B", CategoryId=2, Category="Bảo hộ", Price=400000, Quantity=4, Fitment=FitmentKind.VehicleIndependent, UseCases="đi phố" },
            new() { Id=5, Name="Gương cần ngàm", Brand="A", CategoryId=1, Category="Gương", Price=200000, Quantity=2, Fitment=FitmentKind.Verified, Fitments=[new() { VehicleModelId=1, YearFrom=2020, YearTo=2022, EvidenceUrl="https://example.test/fitment", Notes="Cần ngàm" }] },
            new() { Id=6, Name="Gương chưa có nguồn", Brand="A", CategoryId=1, Category="Gương", Price=200000, Quantity=2, Fitment=FitmentKind.Verified, Fitments=[new() { VehicleModelId=1, YearFrom=2020, YearTo=2022, EvidenceUrl="" }] }
        ];
        public Task<List<RecommendationProduct>> ProductsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Data);
        public Task<List<VehicleOption>> VehiclesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<VehicleOption> { new() { Id=1, Brand="Honda", Model="SH" }, new() { Id=2, Brand="Honda", Model="PCX" } });
    }

    [Fact]
    public async Task VehicleBudgetAndStockAreHardConstraints()
    {
        var result = await new ProductRecommendationService(new Catalog()).RecommendAsync(new() { CategoryId=1, VehicleModelId=1, Year=2021, MaxPrice=350000 });
        Assert.Equal("Ready", result.Status);
        Assert.Equal(1, Assert.Single(result.Items).Id);
    }
    [Theory]
    [InlineData(2019)] [InlineData(2023)]
    public async Task YearOutsideVerifiedRangeReturnsNoMatches(int year)
    {
        var result = await new ProductRecommendationService(new Catalog()).RecommendAsync(new() { CategoryId=1, VehicleModelId=1, Year=year });
        Assert.Equal("NoMatches", result.Status); Assert.Empty(result.Items);
    }
    [Fact]
    public async Task MissingYearAsksBeforeVehicleDependentRecommendations()
    {
        var result = await new ProductRecommendationService(new Catalog()).RecommendAsync(new() { CategoryId=1, VehicleModelId=1 });
        Assert.Equal("NeedMoreInfo", result.Status); Assert.Empty(result.Items);
    }
    [Fact]
    public async Task MissingYearIsNotHiddenByIndependentProductWithSimilarName()
    {
        var catalog = new Catalog();
        catalog.Data.Add(new RecommendationProduct { Id = 7, Name = "Khăn lau gương", Category = "Chăm sóc", CategoryId = 2,
            Price = 10000, Quantity = 1, Fitment = FitmentKind.VehicleIndependent });
        var result = await new ProductRecommendationService(catalog).RecommendAsync(new() { Query = "gương", VehicleModelId = 1 });
        Assert.Equal("NeedMoreInfo", result.Status);
        Assert.Empty(result.Items);
    }
    [Fact]
    public async Task VietnameseAccentsAndUseCaseMatchOnIndependentProduct()
    {
        var result = await new ProductRecommendationService(new Catalog()).RecommendAsync(new() { Query="mu bao hiem", UseCase="di pho" });
        Assert.Equal(4, Assert.Single(result.Items).Id);
    }
    [Fact]
    public async Task VietnameseHelmetSynonymMatches()
    {
        var result = await new ProductRecommendationService(new Catalog()).RecommendAsync(new() { Query = "non bao hiem" });
        Assert.Equal(4, Assert.Single(result.Items).Id);
    }
    [Fact]
    public async Task NoFakeRecommendationWhenBudgetExcludesMatches()
    {
        var result = await new ProductRecommendationService(new Catalog()).RecommendAsync(new() { CategoryId=1, VehicleModelId=1, Year=2021, MaxPrice=100000 });
        Assert.Empty(result.Items); Assert.Equal("NoMatches", result.Status);
    }
    [Fact]
    public async Task InvalidZeroPriceIsExcluded()
    {
        var catalog = new Catalog();
        catalog.Data.Add(new RecommendationProduct { Id = 7, Name = "Mũ bảo hiểm không có giá", CategoryId = 2,
            Category = "Bảo hộ", Price = 0, Quantity = 1, Fitment = FitmentKind.VehicleIndependent });
        var result = await new ProductRecommendationService(catalog).RecommendAsync(new() { Query = "mũ bảo hiểm" });
        Assert.DoesNotContain(result.Items, item => item.Id == 7);
    }

    [Fact]
    public async Task UseCaseIsScoringSignalRatherThanHardFilter()
    {
        var catalog = new Catalog();
        catalog.Data.Add(new RecommendationProduct { Id = 7, Name = "Mũ bảo hiểm khác", Brand = "B", CategoryId = 2,
            Category = "Bảo hộ", Price = 450000, Quantity = 1, Fitment = FitmentKind.VehicleIndependent });
        var result = await new ProductRecommendationService(catalog).RecommendAsync(new() { Query = "mũ bảo hiểm", UseCase = "đi phố" });
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(4, result.Items[0].Id);
    }
}
