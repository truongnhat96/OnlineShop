using Infrastructure.Models.AHP;
using UseCase.Business_Logic;

namespace Infrastructure.AIChat
{
    public interface IAHPService
    {
        Task<List<AHPProductScore>> GetProductRecommendationsAsync(AHPRecommendationRequest request);
        Task<Dictionary<int, double>> CalculateCriteriaWeightsAsync(List<AHPComparisonMatrix> comparisons);
        Task<List<AHPProductScore>> CalculateProductScoresAsync(List<int> productIds, Dictionary<int, double> criteriaWeights);
    }

    public class AHPService : IAHPService
    {
        private readonly IProductManage _productManage;
        private readonly List<AHPCriteria> _defaultCriteria;

        public AHPService(IProductManage productManage)
        {
            _productManage = productManage;
            _defaultCriteria = InitializeDefaultCriteria();
        }

        public async Task<List<AHPProductScore>> GetProductRecommendationsAsync(AHPRecommendationRequest request)
        {
            var criteriaWeights = await CalculateCriteriaWeightsAsync(request.Comparisons);
            var products = await GetProductsForRecommendation(request);
            var productIds = products.Select(p => (int)p.Id).ToList();
            var scores = await CalculateProductScoresAsync(productIds, criteriaWeights);
            return scores.OrderByDescending(s => s.TotalScore).ToList();
        }

        public Task<Dictionary<int, double>> CalculateCriteriaWeightsAsync(List<AHPComparisonMatrix> comparisons)
        {
            if (!comparisons.Any())
            {
                return Task.FromResult(_defaultCriteria.ToDictionary(c => c.Id, c => c.Weight));
            }

            var criteriaIds = comparisons.SelectMany(c => new[] { c.CriteriaId1, c.CriteriaId2 }).Distinct().ToList();
            var n = criteriaIds.Count;
            var matrix = new double[n, n];
            for (int i = 0; i < n; i++) matrix[i, i] = 1.0;

            foreach (var c in comparisons)
            {
                var i = criteriaIds.IndexOf(c.CriteriaId1);
                var j = criteriaIds.IndexOf(c.CriteriaId2);
                if (i >= 0 && j >= 0)
                {
                    matrix[i, j] = c.ComparisonValue;
                    matrix[j, i] = 1.0 / c.ComparisonValue;
                }
            }

            var weights = CalculateEigenvector(matrix);
            var sum = weights.Sum();
            for (int i = 0; i < weights.Length; i++) weights[i] /= sum;

            var result = new Dictionary<int, double>();
            for (int i = 0; i < n; i++) result[criteriaIds[i]] = weights[i];
            return Task.FromResult(result);
        }

        public async Task<List<AHPProductScore>> CalculateProductScoresAsync(List<int> productIds, Dictionary<int, double> criteriaWeights)
        {
            var all = await _productManage.GetProductsAsync();
            var products = all.Where(p => productIds.Contains(p.Id));
            var results = new List<AHPProductScore>();

            // Chuẩn hóa theo tập sản phẩm đang so sánh
            var maxPrice = products.Any() ? products.Max(p => p.Price) : 1.0;
            var maxSold = products.Any() ? products.Max(p => p.Sold) : 1;

            foreach (var p in products)
            {
                var score = new AHPProductScore
                {
                    ProductId = p.Id,
                    ProductName = p.Name
                };

                double total = 0;
                foreach (var c in _defaultCriteria)
                {
                    if (!criteriaWeights.TryGetValue(c.Id, out var w)) continue;
                    var cs = CalculateCriteriaScore(p, c, maxPrice, maxSold);
                    score.CriteriaScores[c.Name] = cs;
                    total += cs * w;
                }
                score.TotalScore = total;
                results.Add(score);
            }

            if (results.Count > 0)
            {
                var max = results.Max(r => r.TotalScore);
                var min = results.Min(r => r.TotalScore);
                var range = max - min;
                foreach (var r in results)
                {
                    r.NormalizedScore = range > 0 ? (r.TotalScore - min) / range : 1.0;
                }
                var ordered = results.OrderByDescending(r => r.TotalScore).ToList();
                for (int i = 0; i < ordered.Count; i++) ordered[i].Rank = i + 1;
                results = ordered;
            }

            return results;
        }

        private async Task<List<dynamic>> GetProductsForRecommendation(AHPRecommendationRequest request)
        {
            if (request.CategoryId.HasValue)
            {
                return (await _productManage.GetProductsByCategoryAsync(request.CategoryId.Value)).Cast<dynamic>().ToList();
            }
            if (!string.IsNullOrEmpty(request.SearchQuery))
            {
                return (await _productManage.GetProductsAsync(request.SearchQuery)).Cast<dynamic>().ToList();
            }
            var all = await _productManage.GetProductsAsync();
            if (request.ProductIds.Any()) return all.Where(p => request.ProductIds.Contains(p.Id)).Cast<dynamic>().ToList();
            return all.Cast<dynamic>().ToList();
        }

        private double CalculateCriteriaScore(dynamic product, AHPCriteria criteria, double maxPrice, int maxSold)
        {
            return criteria.Type switch
            {
                CriteriaType.Cost => CalculateCostScore(product, criteria, maxPrice),
                CriteriaType.Benefit => CalculateBenefitScore(product, criteria, maxPrice, maxSold),
                _ => 0.5
            };
        }

        private double CalculateCostScore(dynamic product, AHPCriteria criteria, double maxPrice)
        {
            return criteria.Name.ToLower() switch
            {
                // Giá: thấp hơn tốt hơn (chuẩn hóa theo maxPrice của tập so sánh)
                "price" or "giá" => maxPrice > 0 ? 1.0 - Math.Min((double)product.Price / maxPrice, 1.0) : 0.5,
                "delivery_time" or "thời gian giao hàng" => 0.8,
                _ => 0.5
            };
        }

        private double CalculateBenefitScore(dynamic product, AHPCriteria criteria, double maxPrice, int maxSold)
        {
            return criteria.Name.ToLower() switch
            {
                "rating" or "đánh giá" => 0.7,
                // Số lượng bán: cao hơn tốt hơn (chuẩn hóa theo maxSold của tập so sánh)
                "sales_count" or "số lượng bán" => maxSold > 0 ? Math.Min((double)product.Sold / maxSold, 1.0) : 0.5,
                // Chất lượng: giả định giá cao tương ứng chất lượng cao hơn; kết hợp với độ phổ biến
                "quality" or "chất lượng" => CalculateQualityScore(product, maxPrice, maxSold),
                _ => 0.5
            };
        }

        private double CalculateQualityScore(dynamic product, double maxPrice, int maxSold)
        {
            var sold = (double)product.Sold;
            var price = (double)product.Price;

            // Độ phổ biến: chuẩn hóa theo maxSold (tránh chia cho 0)
            double popularity = 0.5;
            if (maxSold > 0)
            {
                // Dùng log để giảm ảnh hưởng khi sold rất lớn
                var denom = Math.Log(1 + (double)maxSold);
                popularity = denom > 0 ? Math.Log(1 + sold) / denom : 0.5;
            }

            // Giá càng cao ⇒ chất lượng càng cao (chuẩn hóa theo maxPrice trong tập so sánh)
            double priceScore = 0.5;
            if (maxPrice > 0)
            {
                priceScore = Math.Min(price / maxPrice, 1.0);
            }

            // Trung bình hai yếu tố
            return (popularity + priceScore) / 2.0;
        }

        private double[] CalculateEigenvector(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            var v = new double[n];
            var rnd = new Random();
            for (int i = 0; i < n; i++) v[i] = rnd.NextDouble();

            for (int iter = 0; iter < 100; iter++)
            {
                var nv = new double[n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        nv[i] += matrix[i, j] * v[j];
                var norm = Math.Sqrt(nv.Sum(x => x * x));
                if (norm > 0)
                {
                    for (int i = 0; i < n; i++) nv[i] /= norm;
                }
                var diff = 0.0;
                for (int i = 0; i < n; i++) diff += Math.Abs(nv[i] - v[i]);
                v = nv;
                if (diff < 1e-6) break;
            }
            return v;
        }

        private List<AHPCriteria> InitializeDefaultCriteria()
        {
            return new List<AHPCriteria>
            {
                new() { Id = 1, Name = "Giá", Description = "Giá cả sản phẩm", Weight = 0.3, Priority = 1, Type = CriteriaType.Cost },
                new() { Id = 2, Name = "Đánh giá", Description = "Đánh giá từ khách hàng", Weight = 0.25, Priority = 2, Type = CriteriaType.Benefit },
                new() { Id = 3, Name = "Chất lượng", Description = "Chất lượng sản phẩm", Weight = 0.2, Priority = 3, Type = CriteriaType.Benefit },
                new() { Id = 4, Name = "Số lượng bán", Description = "Mức độ phổ biến", Weight = 0.15, Priority = 4, Type = CriteriaType.Benefit },
                new() { Id = 5, Name = "Thời gian giao hàng", Description = "Tốc độ giao hàng", Weight = 0.1, Priority = 5, Type = CriteriaType.Cost }
            };
        }
    }
}


