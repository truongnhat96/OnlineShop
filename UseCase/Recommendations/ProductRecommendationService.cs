using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace UseCase.Recommendations;

public sealed class ProductRecommendationService(IRecommendationCatalog catalog) : IProductRecommendationService
{
    private static readonly HashSet<string> StopWords = ["va", "cho", "xe", "toi", "co", "mot", "cua", "voi", "can", "muon", "nen", "mua", "goi", "y", "tu", "van", "san", "pham", "phu", "kien"];
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static string Clean(string text) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(text ?? "", @"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>", " ", RegexOptions.IgnoreCase), "<[^>]*>", " "));
    private static List<string> Terms(string? value)
    {
        var text = Normalize(Clean(value ?? ""));
        // Nón bảo hiểm and mũ bảo hiểm are common Vietnamese synonyms; xích and sên
        // are both used for motorcycle chains. Keep all other product words intact.
        if (text.Contains("bao hiem")) text = Regex.Replace(text, @"\bnon\b", "mu");
        text = Regex.Replace(text, @"\bxich\b", "sen");
        var words = Regex.Matches(text, @"[\p{L}\p{N}]+")
            .Select(m => m.Value).Where(w => w.Length > 1 && !StopWords.Contains(w)).ToList();
        var result = new List<string>(words);
        for (var i = 1; i < words.Count; i++) result.Add(words[i - 1] + "_" + words[i]);
        return result;
    }

    public async Task<RecommendationResult> RecommendAsync(RecommendationQuery request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new RecommendationResult { EffectiveCriteria = request };
        if ((request.Query?.Length ?? 0) > 1000 || request.Limit is < 1 or > 20 || request.MinPrice is < 0 || request.MaxPrice is < 0 ||
            (request.MinPrice.HasValue && !double.IsFinite(request.MinPrice.Value)) || (request.MaxPrice.HasValue && !double.IsFinite(request.MaxPrice.Value)) ||
            request.MinPrice > request.MaxPrice || request.Year is < 1900 or > 2100 || (request.Year.HasValue && !request.VehicleModelId.HasValue))
        { result.Status = "NeedMoreInfo"; result.Clarification = "Vui lòng kiểm tra lại xe, đời xe và khoảng giá."; return result; }
        var products = await catalog.ProductsAsync(cancellationToken);
        if (request.VehicleModelId.HasValue && !(await catalog.VehiclesAsync(cancellationToken)).Any(v => v.Id == request.VehicleModelId))
        { result.Status = "NeedMoreInfo"; result.Clarification = "Chưa có dữ liệu cho dòng xe này. Vui lòng chọn lại dòng xe."; return result; }

        var candidates = products.Where(p => p.Quantity > 0 && double.IsFinite(p.Price) && p.Price > 0 &&
            (!request.CategoryId.HasValue || p.CategoryId == request.CategoryId) &&
            (string.IsNullOrWhiteSpace(request.Brand) || Normalize(p.Brand) == Normalize(request.Brand)) &&
            (!request.MinPrice.HasValue || p.Price >= request.MinPrice) && (!request.MaxPrice.HasValue || p.Price <= request.MaxPrice)).ToList();
        var queryTerms = Terms(request.Query);
        // Ask for a year only when the requested category or text actually points at
        // a vehicle-dependent item; unrelated protective gear must not hide this question.
        var queryPhrases = queryTerms.Where(t => t.Contains('_')).ToArray();
        var asksForDependentItem = candidates.Any(p => p.Fitment == FitmentKind.Verified &&
            (request.CategoryId.HasValue || queryTerms.Count > 0) &&
            (queryTerms.Count == 0 || Terms(string.Join(' ', p.Name, p.Category, p.Tags, p.UseCases, p.Description))
                .Intersect(queryPhrases.Length > 0 ? queryPhrases : queryTerms).Any()));
        if (asksForDependentItem && (!request.VehicleModelId.HasValue || !request.Year.HasValue))
        {
            result.Status = "NeedMoreInfo";
            result.Clarification = "Vui lòng cho biết dòng và đời xe để kiểm tra tương thích.";
            return result;
        }
        if (request.VehicleModelId.HasValue)
            candidates = candidates.Where(p => p.Fitment == FitmentKind.VehicleIndependent || (p.Fitment == FitmentKind.Verified && request.Year.HasValue &&
                p.Fitments.Any(f => f.VehicleModelId == request.VehicleModelId && Uri.TryCreate(f.EvidenceUrl, UriKind.Absolute, out var evidence) && evidence.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(evidence.UserInfo) && string.IsNullOrWhiteSpace(f.Notes) && f.YearFrom <= request.Year && f.YearTo >= request.Year))).ToList();
        else
            candidates = candidates.Where(p => p.Fitment == FitmentKind.VehicleIndependent).ToList();

        var documents = candidates.ToDictionary(p => p.Id, p => Terms(string.Join(' ', p.Name, p.Name, p.Name, p.Category, p.Category, p.Brand, p.Tags, p.UseCases, p.Description)));
        var requiredWords = queryTerms.Where(t => !t.Contains('_')).Distinct().ToArray();
        var df = documents.Values.SelectMany(terms => terms.Distinct()).GroupBy(term => term).ToDictionary(g => g.Key, g => g.Count());
        double Idf(string term) => Math.Log((documents.Count + 1d) / (df.GetValueOrDefault(term) + 1d)) + 1;
        Dictionary<string, double> Vector(IEnumerable<string> terms) => terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count() * Idf(g.Key));
        var qv = Vector(queryTerms);
        var qnorm = Math.Sqrt(qv.Values.Sum(x => x * x));
        double Cosine(List<string> terms)
        {
            var dv = Vector(terms); var norm = Math.Sqrt(dv.Values.Sum(x => x * x));
            return qnorm == 0 || norm == 0 ? 0 : qv.Sum(pair => pair.Value * dv.GetValueOrDefault(pair.Key)) / (qnorm * norm);
        }
        var min = candidates.Count > 0 ? candidates.Min(p => p.Price) : 0;
        var max = candidates.Count > 0 ? candidates.Max(p => p.Price) : 0;
        var ranked = candidates.Select(p => {
            var similarity = Cosine(documents[p.Id]);
            var useMatch = !string.IsNullOrWhiteSpace(request.UseCase) && Normalize(p.UseCases).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(Normalize(request.UseCase));
            var useActive = !string.IsNullOrWhiteSpace(request.UseCase);
            var sum = (qnorm > 0 ? .65 : 0) + (useActive ? .25 : 0) + (request.PreferLowPrice ? .10 : 0);
            var cheap = max > min ? (max - p.Price) / (max - min) : 1;
            var score = sum > 0 ? (.65 * similarity + (useActive && useMatch ? .25 : 0) + (request.PreferLowPrice ? .10 * cheap : 0)) / sum : 0;
            return (Product: p, Similarity: similarity, UseMatch: useMatch, Score: score);
        }).Where(x => qnorm == 0 || (x.Similarity > 0 && requiredWords.All(documents[x.Product.Id].Contains)) || x.UseMatch)
          .OrderByDescending(x => x.Score).ThenBy(x => x.Product.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Product.Id).ToList();

        if (qnorm == 0 && string.IsNullOrWhiteSpace(request.UseCase) && !request.PreferLowPrice && !request.CategoryId.HasValue && string.IsNullOrWhiteSpace(request.Brand))
            ranked = ranked.GroupBy(x => x.Product.CategoryId).OrderBy(g => g.Key).SelectMany(g => g.Select((item, index) => (item, index)))
                .OrderBy(x => x.index).ThenBy(x => x.item.Product.CategoryId).Select(x => x.item).ToList();
        result.Items = ranked.Take(request.Limit).Select(x => new RecommendationItem {
            Id = x.Product.Id, Name = x.Product.Name, Brand = x.Product.Brand, Price = x.Product.Price, Image = x.Product.Image,
            IsSample = x.Product.IsSample, Quantity = x.Product.Quantity, Score = x.Score,
            Compatibility = x.Product.Fitment == FitmentKind.VehicleIndependent ? "Không phụ thuộc dòng xe" : "Tương thích theo dữ liệu đã xác minh",
            Reasons = new List<string> { x.UseMatch ? "Phù hợp nhu cầu đã chọn" : qnorm > 0 || request.CategoryId.HasValue ? "Cùng nội dung hoặc danh mục bạn chọn" : "Khám phá sản phẩm trong danh mục" }
        }).ToList();
        if (result.Items.Count == 0)
        {
            result.Status = "NoMatches";
            result.Clarification = result.Status == "NeedMoreInfo" ? "Vui lòng cho biết dòng và đời xe để kiểm tra tương thích." : "Không tìm thấy sản phẩm phù hợp với các điều kiện đã chọn.";
        }
        return result;
    }
}
