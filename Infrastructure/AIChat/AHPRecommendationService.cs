using Infrastructure.Models.AHP;
using UseCase.Business_Logic;
<<<<<<< HEAD
=======
using System.Reflection;
using System.Text;
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6

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
<<<<<<< HEAD
            if (recommendations.Any())
            {
                return BuildRecommendationResponse(recommendations, extractedCriteria);
            }

            // Fallback: trả lời dạng liệt kê sản phẩm theo từ khóa thường gặp
            var fallback = await BuildFallbackListingAsync(userQuery);
            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback
                : "Tôi không tìm thấy sản phẩm phù hợp với yêu cầu của bạn.";
=======

            if (recommendations != null && recommendations.Any())
            {
                return await BuildRecommendationResponseAsync(recommendations, extractedCriteria);
            }

            var fallback = await BuildFallbackListingAsync(userQuery);
            return !string.IsNullOrWhiteSpace(fallback)
                ? fallback
                : "❌ Tôi không tìm thấy sản phẩm phù hợp với yêu cầu của bạn.";
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
        }

        public async Task<List<AHPProductScore>> GetPersonalizedRecommendationsAsync(string userQuery, string userSessionId)
        {
            var request = new AHPRecommendationRequest
            {
                UserSessionId = userSessionId,
<<<<<<< HEAD
                // Ưu tiên keyword đã biết (office/key) để tăng tỉ lệ khớp
                SearchQuery = GuessKeyword(userQuery) ?? ExtractSearchQuery(userQuery),
                CategoryId = ExtractCategoryFromQuery(userQuery)
            };
            request.Comparisons = await GenerateComparisonMatrixFromQueryAsync(userQuery, userSessionId);
            return await _ahpService.GetProductRecommendationsAsync(request);
=======
                SearchQuery = GuessKeyword(userQuery) ?? ExtractSearchQuery(userQuery),
                CategoryId = ExtractCategoryFromQuery(userQuery)
            };

            request.Comparisons = await GenerateComparisonMatrixFromQueryAsync(userQuery, userSessionId);
            var scores = await _ahpService.GetProductRecommendationsAsync(request);
            return (scores ?? new List<AHPProductScore>()).OrderByDescending(s => s.TotalScore).ToList();
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
        }

        public Task<string> ExtractCriteriaFromQueryAsync(string userQuery)
        {
            var criteria = new List<string>();
<<<<<<< HEAD
            var query = userQuery.ToLower();
=======
            var query = (userQuery ?? string.Empty).ToLowerInvariant();
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            var map = new Dictionary<string, string>
            {
                { "giá", "Giá" }, { "price", "Giá" }, { "rẻ", "Giá" },
                { "đánh giá", "Đánh giá" }, { "rating", "Đánh giá" }, { "sao", "Đánh giá" },
                { "chất lượng", "Chất lượng" }, { "quality", "Chất lượng" },
                { "bán chạy", "Số lượng bán" }, { "phổ biến", "Số lượng bán" },
                { "giao hàng", "Thời gian giao hàng" }, { "delivery", "Thời gian giao hàng" }, { "nhanh", "Thời gian giao hàng" }
            };
<<<<<<< HEAD
            foreach (var kv in map) if (query.Contains(kv.Key)) criteria.Add(kv.Value);
=======

            foreach (var kv in map)
                if (query.Contains(kv.Key)) criteria.Add(kv.Value);

>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            return Task.FromResult(string.Join(", ", criteria.Distinct()));
        }

        public Task<List<AHPComparisonMatrix>> GenerateComparisonMatrixFromQueryAsync(string userQuery, string userSessionId)
        {
            var list = new List<AHPComparisonMatrix>();
<<<<<<< HEAD
            var priorities = ExtractPrioritiesFromQuery(userQuery.ToLower());
            var criteriaMap = new Dictionary<string, int> { { "giá", 1 }, { "đánh giá", 2 }, { "chất lượng", 3 }, { "bán chạy", 4 }, { "giao hàng", 5 } };
=======
            var priorities = ExtractPrioritiesFromQuery((userQuery ?? string.Empty).ToLowerInvariant());
            var criteriaMap = new Dictionary<string, int> { { "giá", 1 }, { "đánh giá", 2 }, { "chất lượng", 3 }, { "bán chạy", 4 }, { "giao hàng", 5 } };

>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
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

<<<<<<< HEAD
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

=======
        /// <summary>
        /// Build response text: CHỈ show Top 3 product (Tên, Giá, Mô tả/ngắn, Ưu điểm tags). Không show điểm AHP.
        /// </summary>
        private async Task<string> BuildRecommendationResponseAsync(List<AHPProductScore> recs, string extractedCriteria)
        {
            if (recs == null || !recs.Any()) return "❌ Tôi không tìm thấy sản phẩm phù hợp.";

            var top3 = recs.OrderByDescending(r => r.TotalScore).Take(3).ToList();
            var allRaw = await _productManage.GetProductsAsync();
            var all = (allRaw == null) ? new List<object>() : allRaw.Cast<object>().ToList();

            var sb = new StringBuilder();
            var criteriaPart = string.IsNullOrWhiteSpace(extractedCriteria) ? "tiêu chí phù hợp" : $"các tiêu chí: {extractedCriteria}";
            sb.AppendLine($"✨ Dựa trên {criteriaPart}, Trường Shop gợi ý Top 3 sản phẩm phù hợp:<br>");

            int idx = 1;
            foreach (var s in top3)
            {
                var product = all.FirstOrDefault(p => GetIntProperty(p, "Id") == s.ProductId);

                var name = product != null ? GetStringProperty(product, "Name") : s.ProductName ?? "Sản phẩm";
                var price = product != null ? GetDoubleProperty(product, "Price") : 0.0;
                var desc = product != null ? GetStringProperty(product, "Description") : string.Empty;
                var brand = product != null ? GetStringProperty(product, "Brand") : string.Empty;

                sb.AppendLine($"{idx}. {name} – Giá: {(price > 0 ? $"{price:N0}₫" : $"Liên hệ")}");
                if (!string.IsNullOrWhiteSpace(desc))sb.AppendLine($"   • <b>{Truncate(desc, 300)}</b><br>");
                //if (!string.IsNullOrWhiteSpace(brand)) sb.AppendLine($"   • Thương hiệu: {brand}");

                var advTags = GenerateAdvantageTags(product, all);
                //if (advTags.Any()) sb.AppendLine($"   • Ưu điểm: {string.Join(" • ", advTags)} <br>");

                sb.AppendLine("<br>");
                idx++;
            }

            sb.Append("👉 Bạn muốn mình tư vấn chi tiết hơn về sản phẩm nào trong 3 cái trên không?");
            return sb.ToString();
        }

        /// <summary>
        /// Fallback: nếu AHP không trả về kết quả thì liệt kê Top 3 sản phẩm theo từ khóa hoặc theo giá cho 'sinh viên'.
        /// </summary>
        private async Task<string> BuildFallbackListingAsync(string userQuery)
        {
            var keyword = ExtractSearchQuery(userQuery) ?? GuessKeyword(userQuery);
            var listRaw = !string.IsNullOrWhiteSpace(keyword)
                ? await _productManage.GetProductsAsync(keyword)
                : await _productManage.GetProductsAsync();

            var products = (listRaw == null) ? new List<object>() : listRaw.Cast<object>().ToList();
            if (!products.Any()) return string.Empty;

            var q = (userQuery ?? string.Empty).ToLowerInvariant();
            var studentOrCheap = q.Contains("sinh viên") || q.Contains("giá rẻ") || q.Contains("rẻ") || q.Contains("student") || q.Contains("budget");

            if (studentOrCheap)
            {
                products = products.OrderBy(p => GetDoubleProperty(p, "Price")).ToList();
            }

            var sb = new StringBuilder();
            sb.AppendLine("Dưới đây là Top 3 sản phẩm gợi ý cho bạn:<br>");

            int i = 1;
            foreach (var p in products.Take(3))
            {
                var name = GetStringProperty(p, "Name");
                var price = GetDoubleProperty(p, "Price");
                var brand = GetStringProperty(p, "Brand");
                sb.AppendLine($"{i++}. {name} – Giá: {(price > 0 ? $"{price:N0}₫" : "Liên hệ")}{(string.IsNullOrWhiteSpace(brand) ? "" : $" – {brand}")}<br>");
            }

            return sb.ToString();
        }

        #region Helpers (reflection + utils)

        private static object? GetPropertyValue(object? obj, string propName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return prop?.GetValue(obj);
        }

        private static int GetIntProperty(object? obj, string propName)
        {
            var v = GetPropertyValue(obj, propName);
            if (v == null) return 0;
            try { return Convert.ToInt32(v); } catch { return 0; }
        }

        private static double GetDoubleProperty(object? obj, string propName)
        {
            var v = GetPropertyValue(obj, propName);
            if (v == null) return 0.0;
            try { return Convert.ToDouble(v); } catch { return 0.0; }
        }

        private static string GetStringProperty(object? obj, string propName)
        {
            var v = GetPropertyValue(obj, propName);
            return v?.ToString() ?? string.Empty;
        }

        private List<string> GenerateAdvantageTags(object? product, List<object> allProducts)
        {
            var tags = new List<string>();
            if (product == null || allProducts == null || !allProducts.Any()) return tags;

            try
            {
                var maxPrice = allProducts.Max(p => GetDoubleProperty(p, "Price"));
                var maxSold = allProducts.Max(p => GetIntProperty(p, "Sold"));

                var price = GetDoubleProperty(product, "Price");
                var sold = GetIntProperty(product, "Sold");

                if (maxPrice > 0 && price <= maxPrice * 0.5) tags.Add("Giá rẻ");
                if (maxSold > 0 && sold >= maxSold * 0.7) tags.Add("Bán chạy");
                if (maxPrice > 0 && price >= maxPrice * 0.85) tags.Add("Chất lượng cao");

                var brand = GetStringProperty(product, "Brand");
                if (!string.IsNullOrWhiteSpace(brand)) tags.Add($"Thương hiệu: {brand}");

                if (!tags.Any()) tags.Add("Phù hợp nhu cầu");
            }
            catch
            {
                tags.Add("Phù hợp nhu cầu");
            }

            return tags.Distinct().ToList();
        }

        private static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen).TrimEnd() + "...";
        }

        #endregion

        #region Query parsing helpers

>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
        private int? ExtractCategoryFromQuery(string userQuery) => null;

        private string? ExtractSearchQuery(string userQuery)
        {
<<<<<<< HEAD
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
=======
            if (string.IsNullOrWhiteSpace(userQuery)) return null;
            var keywords = new[] { "office", "key", "diệt virus", "chatgpt", "vpn", "autocad" };
            var low = userQuery.ToLowerInvariant();
            foreach (var kw in keywords) if (low.Contains(kw)) return kw;
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            return null;
        }

        private string? GuessKeyword(string userQuery)
        {
<<<<<<< HEAD
            var q = userQuery.ToLower();
=======
            if (string.IsNullOrWhiteSpace(userQuery)) return null;
            var q = userQuery.ToLowerInvariant();
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            if (q.Contains("office")) return "office";
            if (q.Contains("key")) return "key";
            return null;
        }

        private Dictionary<string, int> ExtractPrioritiesFromQuery(string q)
        {
            var result = new Dictionary<string, int>();
<<<<<<< HEAD
            var importance = new Dictionary<string, int> { { "rất quan trọng", 9 }, { "quan trọng", 7 }, { "khá quan trọng", 5 }, { "bình thường", 3 }, { "không quan trọng", 1 } };
            foreach (var kv in importance)
            {
                if (!q.Contains(kv.Key)) continue;
                foreach (var c in new[] { "giá", "đánh giá", "chất lượng", "bán chạy", "giao hàng" }) if (q.Contains(c)) result[c] = kv.Value;
            }
            // Ưu tiên giá rẻ nếu người dùng là sinh viên/nhắc đến giá rẻ
=======
            var importance = new Dictionary<string, int>
            {
                { "rất quan trọng", 9 }, { "quan trọng", 7 }, { "khá quan trọng", 5 },
                { "bình thường", 3 }, { "không quan trọng", 1 }
            };

            foreach (var kv in importance)
            {
                if (!q.Contains(kv.Key)) continue;
                foreach (var c in new[] { "giá", "đánh giá", "chất lượng", "bán chạy", "giao hàng" })
                    if (q.Contains(c)) result[c] = kv.Value;
            }

>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            var studentHints = new[] { "sinh viên", "sv", "student" };
            var cheapHints = new[] { "giá rẻ", "rẻ", "tiết kiệm", "budget" };
            if (studentHints.Any(h => q.Contains(h)) || cheapHints.Any(h => q.Contains(h)))
            {
                result["giá"] = 9;
                if (!result.ContainsKey("chất lượng")) result["chất lượng"] = 5;
                if (!result.ContainsKey("đánh giá")) result["đánh giá"] = 4;
            }
<<<<<<< HEAD
            // Câu cấu trúc "giá rẻ hơn chất lượng" → giá > chất lượng
            if (q.Contains("giá rẻ hơn chất lượng") || q.Contains("giá hơn chất lượng") || q.Contains("ưu tiên giá hơn chất lượng"))
=======

            if (q.Contains("giá rẻ hơn chất lượng") || q.Contains("ưu tiên giá hơn chất lượng"))
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            {
                result["giá"] = 9;
                result["chất lượng"] = result.ContainsKey("chất lượng") ? Math.Min(result["chất lượng"], 5) : 5;
            }
<<<<<<< HEAD
            if (!result.Any()) { result["giá"] = 7; result["đánh giá"] = 5; result["chất lượng"] = 5; }
=======

            if (!result.Any())
            {
                result["giá"] = 7;
                result["đánh giá"] = 5;
                result["chất lượng"] = 5;
            }

>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
            return result;
        }

        private double CalculateComparisonValue(int a, int b) => a == b ? 1.0 : a > b ? (double)a / b : (double)b / a;

<<<<<<< HEAD
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


=======
        #endregion
    }
}
>>>>>>> ca0995f49d8fcd9cfc33ae0a511771982a1630f6
