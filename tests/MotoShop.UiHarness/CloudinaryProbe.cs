using System.Text.Json;
using CloudinaryDotNet;
using Infrastructure.EditorImages;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

// Explicit provider diagnostic. Never starts the real app or registers a database.
public static class CloudinaryProbe
{
    public static async Task RunAsync(bool upload)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var content = Path.Combine(root, "Infrastructure");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = content, WebRootPath = Path.Combine(content, "wwwroot"), EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.Configuration.AddUserSecrets<Infrastructure.Program>(optional: true).AddEnvironmentVariables();
        builder.Services.AddAntiforgery();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddEditorImages(builder.Configuration, content);
        await using var app = builder.Build();
        var settings = app.Services.GetRequiredService<IOptions<EditorImageOptions>>().Value;
        var report = new Dictionary<string, object?> { ["configured"] = settings.IsConfigured, ["operation"] = upload ? "upload-and-cleanup" : "ping", ["databaseUsed"] = false };
        string? createdId = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            if (!settings.IsConfigured) { report["result"] = "MISSING_CONFIGURATION"; return; }
            if (!upload)
            {
                var client = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
                var result = await client.PingAsync(deadline.Token);
                report["httpStatus"] = (int)result.StatusCode;
                report["success"] = result.Error == null && (int)result.StatusCode is >= 200 and < 300;
                return;
            }
            using var scope = app.Services.CreateScope();
            var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aSioAAAAASUVORK5CYII=");
            using var stream = new MemoryStream(bytes);
            var file = new FormFile(stream, 0, bytes.Length, "file", "cloudinary-diagnostic.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
            var resultImage = await scope.ServiceProvider.GetRequiredService<IEditorImageService>().UploadAsync(file, deadline.Token);
            createdId = resultImage.PublicId;
            report["success"] = true;
            report["httpsOwnedImage"] = app.Services.GetRequiredService<EditorImagePolicy>().IsOwnedUrl(resultImage.Url);
            report["width"] = resultImage.Width; report["height"] = resultImage.Height;
        }
        catch (EditorImageException error) { report["success"] = false; report["errorCode"] = error.Code; report["status"] = error.Status; }
        catch (Exception error) { report["success"] = false; report["errorType"] = error.GetType().Name; }
        finally
        {
            if (createdId != null)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await app.Services.GetRequiredService<ICloudinaryGateway>().DeleteAsync(createdId, cleanup.Token);
                    report["cleanup"] = "SUCCESS";
                }
                catch (Exception error) { report["cleanup"] = "FAILED"; report["cleanupErrorType"] = error.GetType().Name; }
            }
            Console.WriteLine(JsonSerializer.Serialize(report));
        }
    }
}
