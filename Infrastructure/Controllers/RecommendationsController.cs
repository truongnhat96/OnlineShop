using Microsoft.AspNetCore.Mvc;
using UseCase.Business_Logic;
using UseCase.Recommendations;

namespace Infrastructure.Controllers;

public sealed class RecommendationsController(IProductRecommendationService engine, IRecommendationCatalog catalog, ICategoryManage categories) : Controller
{
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

    [HttpPost("/api/recommendations/context/reset")]
    [ValidateAntiForgeryToken]
    public IActionResult ResetContext()
    {
        HttpContext.Session.Remove("recommendation:pending:v1");
        return NoContent();
    }
}
