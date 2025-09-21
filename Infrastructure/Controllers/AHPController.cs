using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.AIChat;
using Infrastructure.Models.AHP;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Infrastructure.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AHPController : ControllerBase
    {
        private readonly IAHPRecommendationService _rec;

        public AHPController(IAHPRecommendationService rec)
        {
            _rec = rec;
        }

        /// <summary>
        /// Trả về Top 3 AHPProductScore dựa trên query (gọi service cá nhân hoá)
        /// Body: { "query": "...", "sessionId": "..." }
        /// </summary>
        [HttpPost("recommendations")]
        public async Task<IActionResult> GetRecommendations([FromBody] RecommendationRequest req)
        {
            if (req == null) return BadRequest("Missing request body.");
            var sessionId = string.IsNullOrWhiteSpace(req.SessionId) ? Guid.NewGuid().ToString() : req.SessionId;

            var scores = await _rec.GetPersonalizedRecommendationsAsync(req.Query ?? string.Empty, sessionId);
            if (scores == null || !scores.Any()) return Ok(new { items = Array.Empty<object>() });

            // Trả về top 3, rút gọn thông tin (không show chi tiết điểm nếu bạn muốn)
            var top3 = scores.OrderByDescending(s => s.TotalScore).Take(3)
                .Select(s => new
                {
                    ProductId = s.ProductId,
                    ProductName = s.ProductName,
                    Score = Math.Round(s.TotalScore, 4),
                    Rank = s.Rank
                })
                .ToList();

            return Ok(new { items = top3 });
        }

        /// <summary>
        /// Chatbot: trả về câu text gợi ý (đã có format đẹp trong IAHPRecommendationService)
        /// Body: { "query": "...", "sessionId": "..." }
        /// </summary>
        [HttpPost("chat-recommendations")]
        public async Task<IActionResult> ChatRecommendations([FromBody] ChatRecommendationRequest request)
        {
            if (request == null) return BadRequest("Missing request body.");
            var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId;
            var text = await _rec.GenerateRecommendationResponseAsync(request.Query ?? string.Empty, sessionId);
            return Ok(new { response = text });
        }

        /// <summary>
        /// Tính trọng số tiêu chí từ ma trận so sánh (nếu client cần)
        /// Body: { "comparisons": [ { "CriteriaId1":1, "CriteriaId2":2, "ComparisonValue":3 }, ... ] }
        /// </summary>
        [HttpPost("calculate-weights")]
        public IActionResult CalculateWeights([FromBody] CalculateWeightsRequest request)
        {
            var weights = CalculateCriteriaWeights(request?.Comparisons ?? new List<AHPComparisonMatrix>());
            return Ok(weights);
        }

        /// <summary>
        /// Trả danh sách tiêu chí mặc định
        /// </summary>
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

        #region — Local helpers (weights calculation) —

        /// <summary>
        /// Nếu không có comparisons, trả về trọng số mặc định.
        /// Nếu có, build ma trận và tính eigenvector (power method), chuẩn hoá tổng = 1
        /// </summary>
        private Dictionary<int, double> CalculateCriteriaWeights(List<AHPComparisonMatrix> comparisons)
        {
            var defaultCriteria = new List<AHPCriteria>
            {
                new() { Id = 1, Name = "Giá", Weight = 0.3 },
                new() { Id = 2, Name = "Đánh giá", Weight = 0.25 },
                new() { Id = 3, Name = "Chất lượng", Weight = 0.2 },
                new() { Id = 4, Name = "Số lượng bán", Weight = 0.15 },
                new() { Id = 5, Name = "Thời gian giao hàng", Weight = 0.1 }
            };

            if (comparisons == null || !comparisons.Any())
            {
                return defaultCriteria.ToDictionary(c => c.Id, c => c.Weight);
            }

            var criteriaIds = comparisons.SelectMany(c => new[] { c.CriteriaId1, c.CriteriaId2 }).Distinct().ToList();
            var n = criteriaIds.Count;
            if (n == 0) return defaultCriteria.ToDictionary(c => c.Id, c => c.Weight);

            var matrix = new double[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) matrix[i, j] = (i == j) ? 1.0 : 0.0;

            foreach (var c in comparisons)
            {
                var i = criteriaIds.IndexOf(c.CriteriaId1);
                var j = criteriaIds.IndexOf(c.CriteriaId2);
                if (i >= 0 && j >= 0 && c.ComparisonValue > 0)
                {
                    matrix[i, j] = c.ComparisonValue;
                    matrix[j, i] = 1.0 / c.ComparisonValue;
                }
            }

            var vec = PowerMethodEigenvector(matrix, 100, 1e-8);
            var sum = vec.Sum();
            if (sum <= 0) sum = 1.0;
            for (int i = 0; i < vec.Length; i++) vec[i] /= sum;

            var result = new Dictionary<int, double>();
            for (int i = 0; i < n; i++) result[criteriaIds[i]] = vec[i];
            return result;
        }

        private static double[] PowerMethodEigenvector(double[,] matrix, int maxIter = 1000, double tol = 1e-9)
        {
            int n = matrix.GetLength(0);
            var v = new double[n];
            var rnd = new Random();
            for (int i = 0; i < n; i++) v[i] = rnd.NextDouble() + 0.1;

            for (int it = 0; it < maxIter; it++)
            {
                var nv = new double[n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        nv[i] += matrix[i, j] * v[j];

                var norm = Math.Sqrt(nv.Sum(x => x * x));
                if (norm > 0) for (int i = 0; i < n; i++) nv[i] /= norm;

                double diff = 0;
                for (int i = 0; i < n; i++) diff += Math.Abs(nv[i] - v[i]);
                v = nv;
                if (diff < tol) break;
            }
            return v;
        }

        #endregion
    }

    #region — Request DTOs used by this controller —

    public class RecommendationRequest
    {
        public string? Query { get; set; }
        public string? SessionId { get; set; }
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

    #endregion
}
