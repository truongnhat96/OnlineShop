using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infrastructure.Models;
using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using UseCase.Recommendations;

namespace Infrastructure.Recommendations;

public sealed class RecommendationInteractionService(ShopContext db, IHttpContextAccessor accessor, IRecommendationCatalog catalog) : IRecommendationInteractionService
{
    public const string GuestKey = "recommendation:profile:v1";
    private sealed class GuestState
    {
        public bool Chosen { get; set; }
        public bool Enabled { get; set; }
        public int? VehicleModelId { get; set; }
        public int? Year { get; set; }
        public DateTimeOffset Updated { get; set; }
        public List<RecommendationBehavior> Events { get; set; } = [];
    }
    private HttpContext? Context => accessor.HttpContext;
    private ISession? Session => Context?.Features.Get<ISessionFeature>()?.Session;
    private int? UserId => Context?.User.Identity?.IsAuthenticated == true && int.TryParse(Context.User.FindFirstValue(ClaimTypes.Sid), out var id) && id > 0 ? id : null;
    private bool InvalidIdentity => Context?.User.Identity?.IsAuthenticated == true && !UserId.HasValue;
    private bool IsAdmin => Context?.User.IsInRole("Admin") == true;
    private GuestState Guest()
    {
        try
        {
            var json = Session?.GetString(GuestKey);
            if (json is null) return new GuestState();
            var state = json is null ? null : JsonSerializer.Deserialize<GuestState>(json);
            if (state is not null && DateTimeOffset.UtcNow - state.Updated <= TimeSpan.FromMinutes(30)) return state;
        }
        catch (JsonException) { }
        Session?.Remove(GuestKey);
        return new GuestState();
    }
    private void SaveGuest(GuestState state)
    {
        state.Updated = DateTimeOffset.UtcNow;
        Session?.SetString(GuestKey, JsonSerializer.Serialize(state));
    }
    public void ClearGuest() => Session?.Remove(GuestKey);

    public async Task<RecommendationProfile> GetAsync(CancellationToken cancellationToken = default)
    {
        var profile = new RecommendationProfile();
        if (IsAdmin || InvalidIdentity) return profile;
        if (UserId is int id)
        {
            var preference = await db.RecommendationPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == id, cancellationToken);
            profile.Chosen = preference?.Chosen == true;
            profile.Enabled = preference?.Enabled == true;
            if (profile.Enabled)
            {
                profile.VehicleModelId = preference!.VehicleModelId;
                profile.Year = preference.Year;
                var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
                var query = db.RecommendationEvents.AsNoTracking().Where(e => e.UserId == id);
                if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                    profile.Events = (await query.ToListAsync(cancellationToken)).Where(e => e.At >= cutoff).OrderByDescending(e => e.At).Take(500)
                        .Select(e => new RecommendationBehavior { Kind = e.Kind, ProductId = e.ProductId, Terms = e.Terms, At = e.At }).ToList();
                else
                    profile.Events = await query.Where(e => e.At >= cutoff).OrderByDescending(e => e.At).Take(500)
                        .Select(e => new RecommendationBehavior { Kind = e.Kind, ProductId = e.ProductId, Terms = e.Terms, At = e.At }).ToListAsync(cancellationToken);
            }
            profile.CartProductIds = (await db.CartItems.AsNoTracking().Where(c => c.UserId == id && c.Quantity > 0).Select(c => c.ProductId).ToListAsync(cancellationToken)).ToHashSet();
        }
        else
        {
            var guest = Guest();
            profile.Chosen = guest.Chosen; profile.Enabled = guest.Enabled;
            if (guest.Enabled) { profile.Events = guest.Events.OrderByDescending(e => e.At).Take(500).ToList(); profile.VehicleModelId = guest.VehicleModelId; profile.Year = guest.Year; }
            try
            {
                var cart = Session?.GetString(Controllers.CartController.CartSessionKey);
                if (cart is not null) profile.CartProductIds = (JsonSerializer.Deserialize<List<CartModel>>(cart) ?? []).Where(c => c.Product is not null && c.Quantity > 0).Select(c => c.Product.Id).ToHashSet();
            }
            catch (JsonException) { }
        }
        return profile;
    }
    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (IsAdmin || InvalidIdentity) throw new InvalidOperationException("Tài khoản không sử dụng hồ sơ gợi ý.");
        if (UserId is int id)
        {
            var row = await db.RecommendationPreferences.FindAsync([id], cancellationToken);
            if (row is null) { row = new RecommendationPreference { UserId = id }; db.RecommendationPreferences.Add(row); }
            row.Chosen = true; row.Enabled = enabled;
            await db.SaveChangesAsync(cancellationToken);
        }
        else { var state = Guest(); state.Chosen = true; state.Enabled = enabled; SaveGuest(state); }
    }
    public async Task SetVehicleAsync(int? vehicleId, int? year, CancellationToken cancellationToken = default)
    {
        if (IsAdmin || InvalidIdentity) throw new InvalidOperationException("Tài khoản không sử dụng hồ sơ gợi ý.");
        if (vehicleId.HasValue != year.HasValue || (year.HasValue && (year < 1900 || year > 2100)) ||
            (vehicleId.HasValue && !await db.VehicleModels.AnyAsync(v => v.Id == vehicleId, cancellationToken)))
            throw new ArgumentException("Vui lòng chọn đúng dòng, phiên bản và đời xe.");
        if (UserId is int id)
        {
            var row = await db.RecommendationPreferences.FindAsync([id], cancellationToken);
            if (row?.Enabled != true) throw new InvalidOperationException("Hãy bật cá nhân hóa trước khi lưu xe.");
            row.VehicleModelId = vehicleId; row.Year = year; row.VehicleConfirmedAt = vehicleId.HasValue ? DateTimeOffset.UtcNow : null;
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var state = Guest(); if (!state.Enabled) throw new InvalidOperationException("Hãy bật cá nhân hóa trước khi lưu xe.");
            state.VehicleModelId = vehicleId; state.Year = year; SaveGuest(state);
        }
    }
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (IsAdmin || InvalidIdentity) return;
        if (UserId is int id)
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () => {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.RecommendationEvents.Where(e => e.UserId == id).ExecuteDeleteAsync(cancellationToken);
                var row = await db.RecommendationPreferences.FindAsync([id], cancellationToken);
                if (row is not null) { row.VehicleModelId = null; row.Year = null; row.VehicleConfirmedAt = null; await db.SaveChangesAsync(cancellationToken); }
                await transaction.CommitAsync(cancellationToken);
            });
        }
        else { var state = Guest(); state.Events.Clear(); state.VehicleModelId = null; state.Year = null; SaveGuest(state); }
    }
    public Task RecordViewAsync(int productId, string ticket, CancellationToken cancellationToken = default) => RecordAsync("view", productId, null, cancellationToken, ticket);
    public Task RecordCartAsync(int productId, CancellationToken cancellationToken = default) => RecordAsync("cart", productId, null, cancellationToken);
    public async Task RecordSearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 1000) return;
        var products = await catalog.ProductsAsync(cancellationToken);
        static IEnumerable<string> Words(string text) => Regex.Matches(ProductRecommendationService.Normalize(text), @"[\p{L}\p{N}]{2,}").Select(m => m.Value);
        var allowed = products.SelectMany(p => Words(string.Join(' ', p.Name, p.Category, p.Brand, p.Tags, p.UseCases))).ToHashSet();
        var terms = string.Join(' ', Words(query).Where(allowed.Contains).Distinct().Take(15));
        if (terms.Length > 300) terms = terms[..300];
        if (terms.Length == 0) return;
        await RecordAsync("search", null, terms, cancellationToken);
    }
    private async Task RecordAsync(string kind, int? productId, string? terms, CancellationToken cancellationToken, string? requestKey = null)
    {
        if (IsAdmin || InvalidIdentity || (productId.HasValue && !await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))) return;
        var profile = await GetAsync(cancellationToken);
        if (!profile.Enabled) return;
        var now = DateTimeOffset.UtcNow;
        var day = now.UtcDateTime.Date;
        var hashInput = requestKey ?? terms;
        var hash = hashInput is null ? "" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)))[..16];
        if (UserId is int id)
        {
            var query = db.RecommendationEvents.Where(e => e.UserId == id && e.Kind == kind && e.ProductId == productId && (kind != "search" || e.Terms == terms));
            var existing = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
                ? (await query.ToListAsync(cancellationToken)).Count(e => e.At >= day)
                : await query.CountAsync(e => e.At >= day, cancellationToken);
            var max = kind == "search" ? 1 : 3;
            if (existing >= max) return;
            var key = $"{day:yyyyMMdd}:{kind}:{productId}:{(kind == "search" ? hash : "")}:{existing}";
            var persistedTerms = requestKey is null ? terms : "#" + hash;
            if (requestKey is not null && await db.RecommendationEvents.AnyAsync(e => e.UserId == id && e.Kind == kind && e.Terms == persistedTerms, cancellationToken)) return;
            // Check consent in the same statement as the insert, so a concurrent
            // opt-out cannot be followed by a new account event.
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO [RecommendationEvents] ([UserId],[Kind],[ProductId],[Terms],[At],[DedupeKey])
                SELECT {id},{kind},{productId},{persistedTerms},{now},{key}
                WHERE EXISTS (SELECT 1 FROM [RecommendationPreferences] WHERE [UserId] = {id} AND [Enabled] = {true})", cancellationToken);
        }
        else
        {
            var state = Guest();
            if (requestKey is not null && state.Events.Any(e => e.Key == hash)) return;
            var existing = state.Events.Count(e => e.Kind == kind && e.ProductId == productId && e.At.UtcDateTime.Date == day && (kind != "search" || e.Terms == terms));
            if (existing >= (kind == "search" ? 1 : 3)) return;
            state.Events.Add(new RecommendationBehavior { Kind = kind, ProductId = productId, Terms = terms, At = now, Key = requestKey is null ? null : hash });
            state.Events = state.Events.TakeLast(500).ToList(); SaveGuest(state);
        }
    }
}
