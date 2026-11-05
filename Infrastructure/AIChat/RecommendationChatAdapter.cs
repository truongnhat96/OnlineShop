using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infrastructure.Models.AHP;
using UseCase.Recommendations;

namespace Infrastructure.AIChat;

// Compatibility adapter: the existing AHP routes and chat contract now share one ranker.
public sealed class RecommendationChatAdapter(IProductRecommendationService engine, IRecommendationCatalog catalog, IHttpContextAccessor accessor) : IAHPRecommendationService
{
    private const string SessionKey = "recommendation:pending:v1";
    private sealed record Pending(RecommendationQuery Query, DateTimeOffset UpdatedAt, bool MarketConfirmed = false);
    private ISession? Session => accessor.HttpContext?.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>()?.Session;
    private Pending? ReadPending()
    {
        try {
            var json = Session?.GetString(SessionKey);
            var value = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Pending>(json);
            if (value is not null && DateTimeOffset.UtcNow - value.UpdatedAt <= TimeSpan.FromMinutes(30)) return value;
            Session?.Remove(SessionKey);
        } catch (JsonException) { Session?.Remove(SessionKey); }
        return null;
    }
    public bool IsPendingFollowUp(string query) => ReadPending() is not null &&
        Regex.IsMatch(ProductRecommendationService.Normalize(query), @"\b(?:19\d{2}|20\d{2}|\d+(?:[.,]\d+)?\s*(?:k|nghin|ngan|trieu|tr)|honda|yamaha|vision|air\s*blade|sh|wave|winner|exciter|sirius|pcx|chau au|europe|eu|viet nam|vn)\b");

    private async Task<(RecommendationQuery Query, string? Error, bool MarketConfirmed)> Parse(string input)
    {
        var q = ProductRecommendationService.Normalize(input);
        var previous = IsPendingFollowUp(input) ? ReadPending() : null;
        var result = previous?.Query ?? new RecommendationQuery();
        var previousVehicle = result.VehicleModelId;
        result.Limit = 3;
        var vehicles = await catalog.VehiclesAsync();
        var matches = vehicles.Where(v => new[] { v.Model, v.Brand + " " + v.Model }.Concat(v.Aliases.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(ProductRecommendationService.Normalize).Any(alias => alias.Length >= 2 && Regex.IsMatch(q, @"(?<![\p{L}\p{N}])" + Regex.Escape(alias) + @"(?![\p{L}\p{N}])"))).ToList();
        if (matches.Count > 1) return (result, "Có nhiều phiên bản xe cùng tên. Vui lòng chọn đúng phiên bản tại trang gợi ý.", false);
        if (matches.Count == 1) result.VehicleModelId = matches[0].Id;
        var year = Regex.Match(q, @"(?<!\d)(?:19|20)\d{2}(?!\d)");
        if (year.Success) result.Year = int.Parse(year.Value, CultureInfo.InvariantCulture);
        var money = Regex.Match(q, @"(?:duoi|toi da|khong qua|ngan sach)?\s*(?<n>\d+(?:[.,]\d+)?)\s*(?<u>k|nghin|ngan|trieu|tr)\b");
        if (money.Success && double.TryParse(money.Groups["n"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
            result.MaxPrice = amount * (money.Groups["u"].Value is "trieu" or "tr" ? 1000000 : 1000);
        else if (Regex.IsMatch(q, @"\b(duoi|toi da|khong qua|ngan sach)\b") && !IsPendingFollowUp(input))
            return (result, "Vui lòng cho biết rõ ngân sách, ví dụ dưới 500 nghìn đồng.", false);
        result.PreferLowPrice |= q.Contains("gia re") || q.Contains("tiet kiem");
        var terms = new[] { "mu bao hiem", "non bao hiem", "gang tay", "ao mua", "guong", "kinh chan gio", "baga", "thung", "den", "dau nhot", "duong sen", "ve sinh mu", "ve sinh", "bao ve", "gia do dien thoai" };
        var found = terms.FirstOrDefault(q.Contains);
        if (found is not null) result.Query = found;
        else if (previous is null && !result.VehicleModelId.HasValue && !q.Contains("san pham")) result.Query = input;
        var vehicle = vehicles.SingleOrDefault(v => v.Id == result.VehicleModelId);
        var marketConfirmed = vehicle is null || (previousVehicle == result.VehicleModelId && previous?.MarketConfirmed == true);
        if (vehicle is not null)
        {
            var market = ProductRecommendationService.Normalize(vehicle.Market);
            marketConfirmed |= q.Contains(market) || (market == "chau au" && Regex.IsMatch(q, @"\b(?:eu|europe)\b")) ||
                               (market is "vn" or "viet nam" && Regex.IsMatch(q, @"\b(?:vn|viet nam)\b"));
            if (!marketConfirmed) return (result, $"Dữ liệu hiện có dành cho {vehicle.Brand} {vehicle.Model}, thị trường {vehicle.Market}. Xe của bạn có đúng phiên bản thị trường này không? Vui lòng ghi rõ thị trường hoặc chọn trên trang gợi ý.", false);
        }
        return (result, null, marketConfirmed);
    }

    public async Task<string> GenerateRecommendationResponseAsync(string userQuery, string userSessionId)
    {
        var (query, error, marketConfirmed) = await Parse(userQuery);
        if (error is not null) { Session?.SetString(SessionKey, JsonSerializer.Serialize(new Pending(query, DateTimeOffset.UtcNow, marketConfirmed))); return WebUtility.HtmlEncode(error); }
        var response = await engine.RecommendAsync(query);
        Session?.SetString(SessionKey, JsonSerializer.Serialize(new Pending(query, DateTimeOffset.UtcNow, marketConfirmed)));
        if (response.Status != "Ready") return WebUtility.HtmlEncode(response.Clarification ?? "Chưa tìm thấy sản phẩm phù hợp.");
        return string.Join("<br>", response.Items.Select((item, index) => $"{index + 1}. <a href=\"/Product/{item.Id}\">{WebUtility.HtmlEncode(item.Name)}</a> — {item.Price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} ₫ — Còn hàng ({item.Quantity}) — {WebUtility.HtmlEncode(item.Compatibility)}{(item.IsSample ? " — Giá và tồn kho kiểm thử" : "")}"));
    }
    public async Task<List<AHPProductScore>> GetPersonalizedRecommendationsAsync(string userQuery, string userSessionId)
    {
        var (query, error, _) = await Parse(userQuery);
        if (error is not null) return [];
        var result = await engine.RecommendAsync(query);
        return result.Items.Select((p, index) => new AHPProductScore { ProductId = p.Id, ProductName = p.Name, TotalScore = p.Score, Rank = index + 1 }).ToList();
    }
    public Task<string> ExtractCriteriaFromQueryAsync(string userQuery) => Task.FromResult("");
    public Task<List<AHPComparisonMatrix>> GenerateComparisonMatrixFromQueryAsync(string userQuery, string userSessionId) => Task.FromResult(new List<AHPComparisonMatrix>());
}
