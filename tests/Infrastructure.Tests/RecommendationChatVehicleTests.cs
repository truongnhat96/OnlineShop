using Infrastructure.AIChat;
using Microsoft.AspNetCore.Http;
using Moq;
using UseCase.Recommendations;
using Xunit;

namespace Infrastructure.Tests;

public sealed class RecommendationChatVehicleTests
{
    [Theory]
    [InlineData(true, "Tư vấn kính chắn gió Honda CB500X châu Âu 2022", true)]
    [InlineData(false, "Tư vấn kính chắn gió Honda CB500X châu Âu 2022", false)]
    [InlineData(true, "Tư vấn kính chắn gió Honda CB500X 2022", false)]
    [InlineData(true, "Tư vấn kính chắn gió Honda CB500X châu Âu", false)]
    public async Task SaveVehicleOnlyAfterMarketYearAndOptIn(bool enabled, string input, bool shouldSave)
    {
        var catalog = new Mock<IRecommendationCatalog>();
        catalog.Setup(c => c.VehiclesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new VehicleOption { Id = 7, Brand = "Honda", Model = "CB500X", Market = "Châu Âu" }
        ]);
        var engine = new Mock<IProductRecommendationService>();
        engine.Setup(e => e.RecommendAsync(It.IsAny<RecommendationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationResult());
        var interactions = new Mock<IRecommendationInteractionService>();
        interactions.Setup(i => i.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecommendationProfile { Enabled = enabled });
        var adapter = new RecommendationChatAdapter(engine.Object, catalog.Object, new HttpContextAccessor(), interactions.Object);
        await adapter.GenerateRecommendationResponseAsync(input, "test");
        interactions.Verify(i => i.SetVehicleAsync(7, 2022, It.IsAny<CancellationToken>()), shouldSave ? Times.Once() : Times.Never());
    }
}
