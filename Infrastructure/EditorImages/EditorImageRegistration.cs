using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace Infrastructure.EditorImages;

public static class EditorImageRegistration
{
    public static IServiceCollection AddEditorImages(this IServiceCollection services, IConfiguration configuration, string? contentRoot = null)
    {
        var local = contentRoot == null ? new Dictionary<string, string>() : LocalCloudinarySettings.Read(contentRoot);
        services.AddOptions<EditorImageOptions>().Bind(configuration.GetSection("Cloudinary"))
            .PostConfigure(settings => LocalCloudinarySettings.Apply(settings, configuration, local))
            .Validate(settings => settings.MaxImageBytes > 0 && settings.MaxImageBytes <= 20 * 1024 * 1024, "Cloudinary MaxImageBytes must be between 1 byte and 20 MiB.");
        services.AddSingleton<EditorImagePolicy>();
        services.AddSingleton<EditorContentService>();
        services.AddSingleton<IImageHostResolver, ImageHostResolver>();
        services.AddSingleton<ICloudinaryGateway, CloudinaryGateway>();
        services.AddScoped<IEditorImageService, EditorImageService>();
        services.AddScoped<EditorImageAntiforgeryFilter>();
        services.AddRateLimiter(options => options.AddPolicy<string, EditorImageRateLimitPolicy>("EditorImageUpload"));
        return services;
    }

    public static Task WriteErrorAsync(HttpResponse response, int status, string code, string message)
    {
        response.StatusCode = status;
        return response.WriteAsJsonAsync(new { code, message });
    }

    public static Task RedirectOrError(HttpContext context, string redirect, int status)
    {
        if (context.Request.Path.StartsWithSegments("/admin/editor-images"))
            return WriteErrorAsync(context.Response, status, status == 401 ? "unauthenticated" : "forbidden",
                status == 401 ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." : "Chỉ quản trị viên được upload ảnh nội dung.");
        context.Response.Redirect(redirect);
        return Task.CompletedTask;
    }
}

public sealed class EditorImageRateLimitPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => (context, _) =>
        new(EditorImageRegistration.WriteErrorAsync(context.HttpContext.Response, 429, "rate_limited", "Đã đạt giới hạn 60 ảnh/phút. Vui lòng chờ rồi thử lại."));
    public RateLimitPartition<string> GetPartition(HttpContext context) => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.Sid) ?? context.User.Identity?.Name ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
}

public sealed class EditorImageAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new { code = "invalid_antiforgery", message = "Phiên biểu mẫu đã hết hạn. Vui lòng tải lại trang." }) { StatusCode = 400 };
        }
    }
}
