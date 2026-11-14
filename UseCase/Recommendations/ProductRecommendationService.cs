using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace UseCase.Recommendations;

public sealed class ProductRecommendationService(IRecommendationCatalog catalog, IRecommendationProfileProvider? profiles = null) : IProductRecommendationService
{
    private static readonly HashSet<string> StopWords = ["va", "cho", "xe", "toi", "co", "mot", "cua", "voi", "can", "muon", "nen", "mua", "goi", "y", "tu", "van", "san", "pham", "phu", "kien"];
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = value.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
    }

    private static string Clean(string text) => WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(text ?? "", @"<(script|style)\b[^>]*>[\s\S]*?</\1\s*>", " ", RegexOptions.IgnoreCase), "<[^>]*>", " "));
    private static string ContentDescription(RecommendationProduct product)
    {
        var clean = Clean(product.Description);
        if (product.IsSample && (clean.StartsWith("Không phụ thuộc dòng xe; kiểm tra kích cỡ", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("Chỉ tham khảo xe đúng phiên bản", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("Chưa xác minh tương thích. Cần kiểm tra", StringComparison.OrdinalIgnoreCase))) return "";
        return clean.Replace("Giá và tồn kho phục vụ kiểm thử", "", StringComparison.OrdinalIgnoreCase);
    }
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
        var profile = profiles is null ? new RecommendationProfile() : await profiles.GetAsync(cancellationToken);
        var vehicleId = request.VehicleModelId ?? (profile.Enabled ? profile.VehicleModelId : null);
        var year = request.Year ?? (request.VehicleModelId is null && profile.Enabled ? profile.Year : null);
        if ((request.Query?.Length ?? 0) > 1000 || request.Placement is not ("home" or "recommendations" or "chat") || request.Limit is < 1 or > 20 || request.MinPrice is < 0 || request.MaxPrice is < 0 ||
            (request.MinPrice.HasValue && !double.IsFinite(request.MinPrice.Value)) || (request.MaxPrice.HasValue && !double.IsFinite(request.MaxPrice.Value)) ||
            request.MinPrice > request.MaxPrice || year is < 1900 or > 2100 || (year.HasValue && !vehicleId.HasValue))
        { result.Status = "NeedMoreInfo"; result.Clarification = "Vui lòng kiểm tra lại xe, đời xe và khoảng giá."; return result; }
        var products = await catalog.ProductsAsync(cancellationToken);
        if (vehicleId.HasValue && !(await catalog.VehiclesAsync(cancellationToken)).Any(v => v.Id == vehicleId))
        { result.Status = "NeedMoreInfo"; result.Clarification = "Chưa có dữ liệu cho dòng xe này. Vui lòng chọn lại dòng xe."; return result; }

        var candidates = products.Where(p => p.Quantity > 0 && double.IsFinite(p.Price) && p.Price > 0 && !profile.CartProductIds.Contains(p.Id) &&
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
        if (asksForDependentItem && (!vehicleId.HasValue || !year.HasValue))
        {
            result.Status = "NeedMoreInfo";
            result.Clarification = "Vui lòng cho biết dòng và đời xe để kiểm tra tương thích.";
            return result;
        }
        if (vehicleId.HasValue)
            candidates = candidates.Where(p => p.Fitment == FitmentKind.VehicleIndependent ||
                (p.Fitment == FitmentKind.Unknown && !products.Any(c => c.CategoryId == p.CategoryId && c.Fitment == FitmentKind.Verified)) ||
                (p.Fitment == FitmentKind.Verified && year.HasValue &&
                p.Fitments.Any(f => f.VehicleModelId == vehicleId && Uri.TryCreate(f.EvidenceUrl, UriKind.Absolute, out var evidence) && evidence.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(evidence.UserInfo) && string.IsNullOrWhiteSpace(f.Notes) && f.YearFrom <= year && f.YearTo >= year))).ToList();
        else
            candidates = candidates.Where(p => p.Fitment is FitmentKind.VehicleIndependent or FitmentKind.Unknown || (profile.Enabled && profile.Events.Any(e => e.ProductId == p.Id))).ToList();

        var documents = products.ToDictionary(p => p.Id, p => Terms(string.Join(' ', p.Name, p.Name, p.Name, p.Category, p.Category, p.Brand, p.Tags, p.UseCases, ContentDescription(p))));
        var df = documents.Values.SelectMany(terms => terms.Distinct()).GroupBy(term => term).ToDictionary(g => g.Key, g => g.Count());
        double Idf(string term) => Math.Log((documents.Count + 1d) / (df.GetValueOrDefault(term) + 1d)) + 1;
        Dictionary<string, double> Vector(IEnumerable<string> terms) => terms.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count() * Idf(g.Key));
        var qv = Vector(queryTerms);
        var qnorm = Math.Sqrt(qv.Values.Sum(x => x * x));
        double Cosine(Dictionary<string, double> left, Dictionary<string, double> right)
        {
            var norm = Math.Sqrt(left.Values.Sum(x => x * x) * right.Values.Sum(x => x * x));
            return norm == 0 ? 0 : left.Sum(pair => pair.Value * right.GetValueOrDefault(pair.Key)) / norm;
        }
        var profileVector = new Dictionary<string, double>();
        var categoryWeights = new Dictionary<int, double>();
        if (profile.Enabled)
        foreach (var behavior in profile.Events.Where(e => e.At >= DateTimeOffset.UtcNow.AddDays(-90)).Take(500))
        {
            var age = Math.Max(0, (DateTimeOffset.UtcNow - behavior.At).TotalDays);
            var weight = (behavior.Kind == "cart" ? 5 : behavior.Kind == "search" ? 2 : behavior.Kind == "view" ? 1 : 0) * Math.Pow(2, -age / 14);
            if (weight == 0) continue;
            var relevant = behavior.ProductId.HasValue ? products.FirstOrDefault(p => p.Id == behavior.ProductId) : null;
            var terms = relevant is null ? Terms(behavior.Terms) : documents[relevant.Id];
            if (relevant is not null) categoryWeights[relevant.CategoryId] = categoryWeights.GetValueOrDefault(relevant.CategoryId) + weight;
            var vector = Vector(terms);
            var norm = Math.Sqrt(vector.Values.Sum(x => x * x));
            if (norm > 0) foreach (var pair in vector) profileVector[pair.Key] = profileVector.GetValueOrDefault(pair.Key) + pair.Value * weight / norm;
        }
        var hasHistory = profileVector.Count > 0;
        result.Mode = hasHistory ? "Personalized" : qnorm > 0 || !string.IsNullOrWhiteSpace(request.UseCase) ? "Contextual" : "Explore";
        var topCategoryWeight = categoryWeights.Values.DefaultIfEmpty(0).Max();
        var min = candidates.Count > 0 ? candidates.Min(p => p.Price) : 0;
        var max = candidates.Count > 0 ? candidates.Max(p => p.Price) : 0;
        var ranked = candidates.Select(p => {
            var itemVector = Vector(documents[p.Id]);
            var similarity = Cosine(qv, itemVector);
            var useMatch = !string.IsNullOrWhiteSpace(request.UseCase) && Normalize(p.UseCases).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(Normalize(request.UseCase));
            var personalSimilarity = Cosine(profileVector, itemVector);
            var categoryAffinity = topCategoryWeight > 0 ? categoryWeights.GetValueOrDefault(p.CategoryId) / topCategoryWeight : 0;
            var personalScore = topCategoryWeight > 0 ? .8 * personalSimilarity + .2 * categoryAffinity : personalSimilarity;
            var intent = qnorm > 0 ? similarity : useMatch ? 1 : 0;
            if (qnorm > 0 && !string.IsNullOrWhiteSpace(request.UseCase)) intent = .75 * similarity + (useMatch ? .25 : 0);
            var hasIntent = qnorm > 0 || !string.IsNullOrWhiteSpace(request.UseCase);
            var score = hasHistory && hasIntent ? .7 * intent + .3 * personalScore : hasHistory ? personalScore : intent;
            var cheap = max > min ? (max - p.Price) / (max - min) : 1;
            if (request.PreferLowPrice) score = .9 * score + .1 * cheap;
            return (Product: p, Similarity: similarity, PersonalScore: personalScore, UseMatch: useMatch, Score: score);
        }).Where(x => qnorm == 0 || x.Similarity > 0 || x.UseMatch)
          .OrderByDescending(x => x.Score).ThenBy(x => x.Product.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Product.Id).ToList();

        // Relative relevance keeps partial matches without filling the list with
        // products that share only an incidental word with the current request.
        if (qnorm > 0 && ranked.Count > 0)
        {
            var bestSimilarity = products.Select(p => Cosine(qv, Vector(documents[p.Id]))).DefaultIfEmpty(0).Max();
            if (bestSimilarity > 0) ranked = ranked.Where(x => x.Similarity >= bestSimilarity * .6 || x.UseMatch).ToList();
        }

        if (hasHistory && qnorm == 0 && string.IsNullOrWhiteSpace(request.UseCase))
        {
            var relevant = ranked.Where(x => x.Score > 0).ToList();
            if (relevant.Count > 0) ranked = relevant;
        }
        if (result.Mode == "Explore" && !request.PreferLowPrice && !request.CategoryId.HasValue && string.IsNullOrWhiteSpace(request.Brand))
            ranked = ranked.GroupBy(x => x.Product.CategoryId).OrderBy(g => g.Key).SelectMany(g => g.Select((item, index) => (item, index)))
                .OrderBy(x => x.index).ThenBy(x => x.item.Product.CategoryId).Select(x => x.item).ToList();
        result.Items = ranked.Take(request.Limit).Select(x => new RecommendationItem {
            Id = x.Product.Id, Name = x.Product.Name, Brand = x.Product.Brand, Price = x.Product.Price, Image = x.Product.Image,
            IsSample = x.Product.IsSample, Quantity = x.Product.Quantity, Score = x.Score,
            Compatibility = x.Product.Fitment == FitmentKind.VehicleIndependent ? "Không phụ thuộc dòng xe" : x.Product.Fitment == FitmentKind.Unknown ? "Kiểm tra thông số trước khi lắp" : "Tương thích theo dữ liệu đã xác minh",
            Reasons = new List<string> { x.UseMatch ? "Phù hợp nhu cầu đã chọn" : qnorm > 0 && x.Similarity > 0 ? "Liên quan đến nhu cầu bạn nhập" : x.PersonalScore > 0 && result.Mode == "Personalized" ? "Liên quan đến sở thích từ hoạt động của bạn" : result.Mode == "Personalized" ? "Gợi ý dựa trên hoạt động của bạn" : request.CategoryId.HasValue ? "Cùng danh mục bạn chọn" : "Khám phá sản phẩm trong danh mục" }
        }).ToList();
        if (result.Items.Count == 0)
        {
            result.Status = "NoMatches";
            result.Clarification = result.Status == "NeedMoreInfo" ? "Vui lòng cho biết dòng và đời xe để kiểm tra tương thích." : "Không tìm thấy sản phẩm phù hợp với các điều kiện đã chọn.";
        }
        return result;
    }
}
