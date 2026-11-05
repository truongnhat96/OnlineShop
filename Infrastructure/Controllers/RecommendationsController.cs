using Microsoft.AspNetCore.Mvc;
using UseCase.Business_Logic;
using UseCase.Recommendations;
using Microsoft.AspNetCore.RateLimiting;

namespace Infrastructure.Controllers;

public sealed class RecommendationsController(IProductRecommendationService engine, IRecommendationCatalog catalog, ICategoryManage categories, IRecommendationInteractionService interactions) : Controller
{
    public sealed record PreferenceInput(bool Enabled);
    public sealed record VehicleInput(int? VehicleModelId, int? Year);
    public sealed record ViewInput(string Ticket);
    [HttpGet("/Recommendations")]
    public IActionResult Index() => View();

    [HttpGet("/api/recommendations/options")]
    public async Task<IActionResult> Options(CancellationToken token)
    {
        var products = await catalog.ProductsAsync(token);
        var vehicles = await catalog.VehiclesAsync(token);
        return Ok(new { categories = await categories.GetAllCategoriesAsync(), vehicles,
            brands = products.Select(p => p.Brand).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order().ToList(),
            useCases = products.SelectMany(p => p.UseCases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).Distinct().Order().ToList() });
    }

    [HttpPost("/api/recommendations")]
    public async Task<IActionResult> Recommend([FromBody] RecommendationQuery request, CancellationToken token)
    {
        if (request is null) return BadRequest();
        return Ok(await engine.RecommendAsync(request, token));
    }

    [HttpGet("/api/recommendations/personalized")]
    public async Task<IActionResult> Personalized([FromQuery] string placement = "recommendations", CancellationToken token = default) => Ok(await engine.RecommendAsync(new RecommendationQuery { Placement = placement, Limit = 6 }, token));

    [HttpGet("/api/recommendations/preferences")]
    public async Task<IActionResult> Preferences(CancellationToken token)
    {
        var profile = await interactions.GetAsync(token);
        return Ok(new { profile.Chosen, profile.Enabled, profile.VehicleModelId, profile.Year });
    }

    [HttpPost("/api/recommendations/preferences")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("SlidingWindow")]
    public async Task<IActionResult> Preferences([FromBody] PreferenceInput input, CancellationToken token)
    {
        if (input is null || User.IsInRole("Admin")) return BadRequest();
        await interactions.SetEnabledAsync(input.Enabled, token); return NoContent();
    }

    [HttpPost("/api/recommendations/vehicle")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("SlidingWindow")]
    public async Task<IActionResult> Vehicle([FromBody] VehicleInput input, CancellationToken token)
    {
        if (input is null || User.IsInRole("Admin")) return BadRequest();
        try { await interactions.SetVehicleAsync(input.VehicleModelId, input.Year, token); return NoContent(); }
        catch (ArgumentException) { return BadRequest(new { message = "Vui lòng chọn đúng dòng và đời xe." }); }
        catch (InvalidOperationException) { return BadRequest(new { message = "Hãy bật cá nhân hóa trước." }); }
    }

    [HttpPost("/api/recommendations/history/delete")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("SlidingWindow")]
    public async Task<IActionResult> ClearHistory(CancellationToken token) { await interactions.ClearAsync(token); return NoContent(); }

    [HttpPost("/api/recommendations/events")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("SlidingWindow")]
    public async Task<IActionResult> RecordView([FromBody] ViewInput input, CancellationToken token)
    {
        if (input is null) return BadRequest();
        var productId = Infrastructure.Recommendations.RecommendationViewTicket.Verify(HttpContext, input.Ticket);
        if (!productId.HasValue) return BadRequest();
        try { await interactions.RecordViewAsync(productId.Value, input.Ticket, token); }
        catch (Exception error) { HttpContext.RequestServices.GetRequiredService<ILogger<RecommendationsController>>().LogWarning("Recommendation view not recorded: {ErrorType}", error.GetType().Name); }
        return NoContent();
    }

    [HttpPost("/api/recommendations/context/reset")]
    [ValidateAntiForgeryToken]
    public IActionResult ResetContext()
    {
        HttpContext.Session.Remove("recommendation:pending:v1");
        return NoContent();
    }
}
