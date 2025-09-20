using Infrastructure.AIChat;
using Infrastructure.Models.AHP;
using Microsoft.AspNetCore.Mvc;
using UseCase.Business_Logic;

namespace Infrastructure.Controllers
{
    [Route("api/[controller]")]
    public class AHPController : Controller
    {
        private readonly IAHPRecommendationService _rec;
        private readonly IAHPService _ahp;
        private readonly IProductManage _productManage;

        public AHPController(IAHPRecommendationService rec, IAHPService ahp, IProductManage productManage)
        {
            _rec = rec;
            _ahp = ahp;
            _productManage = productManage;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var products = await _productManage.GetProductsAsync();
            return View(new AHPViewModel { Products = products.ToList() ?? [] });
        }


        [HttpPost("recommendations")]
        public async Task<IActionResult> GetRecommendations([FromBody] AHPRecommendationRequest request)
        {
            var res = await _ahp.GetProductRecommendationsAsync(request);
            return Ok(res);
        }

        [HttpPost("chat-recommendations")]
        public async Task<IActionResult> ChatRecommendations([FromBody] ChatRecommendationRequest request)
        {
            var text = await _rec.GenerateRecommendationResponseAsync(request.Query, request.SessionId ?? Guid.NewGuid().ToString());
            return Ok(new { response = text });
        }

        [HttpPost("calculate-weights")]
        public async Task<IActionResult> CalculateWeights([FromBody] CalculateWeightsRequest request)
        {
            var w = await _ahp.CalculateCriteriaWeightsAsync(request.Comparisons);
            return Ok(w);
        }

        [HttpGet("criteria")]
        public IActionResult Criteria()
        {
            var list = new List<AHPCriteria>
            {
                new() { Id = 1, Name = "Giá", Description = "Giá cả sản phẩm", Weight = 0.3, Priority = 1, Type = CriteriaType.Cost },
                new() { Id = 2, Name = "Đánh giá", Description = "Đánh giá từ khách hàng", Weight = 0.25, Priority = 2, Type = CriteriaType.Benefit },
                new() { Id = 3, Name = "Chất lượng", Description = "Chất lượng sản phẩm", Weight = 0.2, Priority = 3, Type = CriteriaType.Benefit },
                new() { Id = 4, Name = "Số lượng bán", Description = "Mức độ phổ biến", Weight = 0.15, Priority = 4, Type = CriteriaType.Benefit },
                new() { Id = 5, Name = "Thời gian giao hàng", Description = "Tốc độ giao hàng", Weight = 0.1, Priority = 5, Type = CriteriaType.Cost }
            };
            return Ok(list);
        }
    }

    public class ChatRecommendationRequest
    {
        public string Query { get; set; } = string.Empty;
        public string? SessionId { get; set; }
    }

    public class CalculateWeightsRequest
    {
        public List<AHPComparisonMatrix> Comparisons { get; set; } = new();
    }
}











