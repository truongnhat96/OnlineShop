using Infrastructure.Models.AHP;
using UseCase.Business_Logic;

namespace Infrastructure.AIChat
{
    public interface IAHPRecommendationService
    {
        Task<string> GenerateRecommendationResponseAsync(string userQuery, string userSessionId);
        Task<List<AHPProductScore>> GetPersonalizedRecommendationsAsync(string userQuery, string userSessionId);
        Task<string> ExtractCriteriaFromQueryAsync(string userQuery);
        Task<List<AHPComparisonMatrix>> GenerateComparisonMatrixFromQueryAsync(string userQuery, string userSessionId);
    }

    public class AHPRecommendationService : IAHPRecommendationService
    {
        private readonly IAHPService _ahpService;
        private readonly IProductManage _productManage;

        public AHPRecommendationService(IAHPService ahpService, IProductManage productManage)
        {
            _ahpService = ahpService;
            _productManage = productManage;
        }

        public async Task<string> GenerateRecommendationResponseAsync(string userQuery, string userSessionId)
        {
            var extractedCriteria = await ExtractCriteriaFromQueryAsync(userQuery);
            var recommendations = await GetPersonalizedRecommendationsAsync(userQuery, userSessionId);
            if (recommendations.Any())
            {
                return BuildRecommendationResponse(recommendations, extractedCriteria);
            }

            // Fallback: trả lời dạng liệt kê sản phẩm theo từ khóa thường gặp
            var fallback = await BuildFallbackListingAsync(userQuery);
            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback
                : "Tôi không tìm thấy sản phẩm phù hợp với yêu cầu của bạn.";
        }

        public async Task<List<AHPProductScore>> GetPersonalizedRecommendationsAsync(string userQuery, string userSessionId)
        {
            var request = new AHPRecommendationRequest
            {
                UserSessionId = userSessionId,
                // Ưu tiên keyword đã biết (office/key) để tăng tỉ lệ khớp
                SearchQuery = GuessKeyword(userQuery) ?? ExtractSearchQuery(userQuery),
                CategoryId = ExtractCategoryFromQuery(userQuery)
            };
            request.Comparisons = await GenerateComparisonMatrixFromQueryAsync(userQuery, userSessionId);
            return await _ahpService.GetProductRecommendationsAsync(request);
        }

        public Task<string> ExtractCriteriaFromQueryAsync(string userQuery)
        {
            var criteria = new List<string>();
            var query = userQuery.ToLower();
            var map = new Dictionary<string, string>
            {
                { "giá", "Giá" }, { "price", "Giá" }, { "rẻ", "Giá" },
                { "đánh giá", "Đánh giá" }, { "rating", "Đánh giá" }, { "sao", "Đánh giá" },
                { "chất lượng", "Chất lượng" }, { "quality", "Chất lượng" },
                { "bán chạy", "Số lượng bán" }, { "phổ biến", "Số lượng bán" },
                { "giao hàng", "Thời gian giao hàng" }, { "delivery", "Thời gian giao hàng" }, { "nhanh", "Thời gian giao hàng" }
            };
            foreach (var kv in map) if (query.Contains(kv.Key)) criteria.Add(kv.Value);
            return Task.FromResult(string.Join(", ", criteria.Distinct()));
        }

        public Task<List<AHPComparisonMatrix>> GenerateComparisonMatrixFromQueryAsync(string userQuery, string userSessionId)
        {
            var list = new List<AHPComparisonMatrix>();
            var priorities = ExtractPrioritiesFromQuery(userQuery.ToLower());
            var criteriaMap = new Dictionary<string, int> { { "giá", 1 }, { "đánh giá", 2 }, { "chất lượng", 3 }, { "bán chạy", 4 }, { "giao hàng", 5 } };
            foreach (var pr in priorities)
            {
                if (!criteriaMap.ContainsKey(pr.Key)) continue;
                var id = criteriaMap[pr.Key];
                foreach (var other in criteriaMap.Where(k => k.Key != pr.Key))
                {
                    list.Add(new AHPComparisonMatrix
                    {
                        CriteriaId1 = id,
                        CriteriaId2 = other.Value,
                        ComparisonValue = CalculateComparisonValue(pr.Value, 5),
                        UserSessionId = userSessionId
                    });
                }
            }
            return Task.FromResult(list);
        }

        private string BuildRecommendationResponse(List<AHPProductScore> recs, string extractedCriteria)
        {
            if (!recs.Any()) return "Tôi không tìm thấy sản phẩm phù hợp với yêu cầu của bạn.";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Dựa trên các tiêu chí: {extractedCriteria}, tôi gợi ý cho bạn các sản phẩm sau:\n");
            for (int i = 0; i < Math.Min(5, recs.Count); i++)
            {
                var p = recs[i];
                sb.AppendLine($"{i + 1}. {p.ProductName}");
                sb.AppendLine($"   - Điểm tổng: {p.TotalScore:F2}");
                sb.AppendLine($"   - Xếp hạng: #{p.Rank}");
                if (p.CriteriaScores.Any())
                {
                    sb.AppendLine("   - Chi tiết điểm số:");
                    foreach (var c in p.CriteriaScores) sb.AppendLine($"     • {c.Key}: {c.Value:F2}");
                }
                sb.AppendLine();
            }
            sb.Append("Bạn có muốn tôi giải thích thêm về tiêu chí đánh giá hoặc so sánh chi tiết không?");
            return sb.ToString();
        }

        private int? ExtractCategoryFromQuery(string userQuery) => null;

        private string? ExtractSearchQuery(string userQuery)
        {
            var k = new[] { "tìm", "search", "có", "bán", "mua" };
            var lower = userQuery.ToLower();
            foreach (var kw in k)
            {
                if (!lower.Contains(kw)) continue;
                var idx = lower.IndexOf(kw);
                var after = userQuery.Substring(idx + kw.Length).Trim();
                // Chuẩn hóa đơn giản: nếu có "office" trong chuỗi sau, trả về "office"
                var afterLower = after.ToLower();
                if (afterLower.Contains("office")) return "office";
                if (afterLower.Contains("key")) return "key";
                // Nếu không nhận diện được, bỏ qua để tránh truy vấn khó khớp
                if (!string.IsNullOrWhiteSpace(after)) return null;
            }
            return null;
        }

        private string? GuessKeyword(string userQuery)
        {
            var q = userQuery.ToLower();
            if (q.Contains("office")) return "office";
            if (q.Contains("key")) return "key";
            return null;
        }

        private Dictionary<string, int> ExtractPrioritiesFromQuery(string q)
        {
            var result = new Dictionary<string, int>();
            var importance = new Dictionary<string, int> { { "rất quan trọng", 9 }, { "quan trọng", 7 }, { "khá quan trọng", 5 }, { "bình thường", 3 }, { "không quan trọng", 1 } };
            foreach (var kv in importance)
            {
                if (!q.Contains(kv.Key)) continue;
                foreach (var c in new[] { "giá", "đánh giá", "chất lượng", "bán chạy", "giao hàng" }) if (q.Contains(c)) result[c] = kv.Value;
            }
            // Ưu tiên giá rẻ nếu người dùng là sinh viên/nhắc đến giá rẻ
            var studentHints = new[] { "sinh viên", "sv", "student" };
            var cheapHints = new[] { "giá rẻ", "rẻ", "tiết kiệm", "budget" };
            if (studentHints.Any(h => q.Contains(h)) || cheapHints.Any(h => q.Contains(h)))
            {
                result["giá"] = 9;
                if (!result.ContainsKey("chất lượng")) result["chất lượng"] = 5;
                if (!result.ContainsKey("đánh giá")) result["đánh giá"] = 4;
            }
            // Câu cấu trúc "giá rẻ hơn chất lượng" → giá > chất lượng
            if (q.Contains("giá rẻ hơn chất lượng") || q.Contains("giá hơn chất lượng") || q.Contains("ưu tiên giá hơn chất lượng"))
            {
                result["giá"] = 9;
                result["chất lượng"] = result.ContainsKey("chất lượng") ? Math.Min(result["chất lượng"], 5) : 5;
            }
            if (!result.Any()) { result["giá"] = 7; result["đánh giá"] = 5; result["chất lượng"] = 5; }
            return result;
        }

        private double CalculateComparisonValue(int a, int b) => a == b ? 1.0 : a > b ? (double)a / b : (double)b / a;

        private async Task<string> BuildFallbackListingAsync(string userQuery)
        {
            var keyword = ExtractSearchQuery(userQuery) ?? GuessKeyword(userQuery);
            var list = !string.IsNullOrWhiteSpace(keyword)
                ? await _productManage.GetProductsAsync(keyword)
                : await _productManage.GetProductsAsync();

            var products = list?.ToList() ?? new List<Entities.Product>();
            if (!products.Any()) return string.Empty;

            // Ưu tiên hiển thị giá rẻ nếu có gợi ý sinh viên/giá rẻ
            var q = userQuery.ToLower();
            var studentOrCheap = q.Contains("sinh viên") || q.Contains("giá rẻ") || q.Contains("rẻ") || q.Contains("student") || q.Contains("budget");
            if (studentOrCheap)
            {
                products = products.OrderBy(p => p.Price).ToList();
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Dưới đây là các sản phẩm phù hợp:");
            int i = 1;
            foreach (var p in products.Take(5))
            {
                sb.AppendLine($"{i++}. {p.Name} - Giá: {p.Price:N0}₫ - Thương hiệu: {p.Brand}");
            }
            return sb.ToString();
        }
    }
}


