using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;

namespace Infrastructure.Recommendations;

public static class RecommendationViewTicket
{
    private const string Purpose = "MotoShop.RecommendationView.v1";
    private static string Subject(HttpContext context) => context.User.Identity?.IsAuthenticated == true
        ? "u:" + context.User.FindFirstValue(ClaimTypes.Sid)
        : "s:" + context.Session.Id;
    public static string Create(HttpContext context, int productId)
    {
        var provider = context.RequestServices.GetRequiredService<IDataProtectionProvider>();
        return provider.CreateProtector(Purpose).Protect($"{productId}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{Subject(context)}");
    }
    public static int? Verify(HttpContext context, string? ticket)
    {
        if (string.IsNullOrEmpty(ticket) || ticket.Length > 2048) return null;
        try
        {
            var provider = context.RequestServices.GetRequiredService<IDataProtectionProvider>();
            var parts = provider.CreateProtector(Purpose).Unprotect(ticket).Split('|');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var id) || id <= 0 || !long.TryParse(parts[1], out var issued) || parts[2] != Subject(context)) return null;
            var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issued;
            return age is >= 8 and <= 1200 ? id : null;
        }
        catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or FormatException or InvalidOperationException) { return null; }
    }
}
