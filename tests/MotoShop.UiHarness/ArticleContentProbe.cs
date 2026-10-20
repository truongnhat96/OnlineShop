using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Infrastructure.EditorImages;
using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

// Explicit read-only diagnostic. No host, migrations, seeding, SaveChanges or provider uploads.
internal static class ArticleContentProbe
{
    public static async Task RunAsync()
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
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<EditorContentService>();
        var policy = scope.ServiceProvider.GetRequiredService<EditorImagePolicy>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await using var db = new ShopContext();
            db.Database.SetCommandTimeout(15);
            var articles = await db.Posts.AsNoTracking().Select(post => new { post.Id, post.Content }).ToListAsync(deadline.Token);
            var reports = articles.Select(post => {
                var document = new HtmlParser().ParseDocument(post.Content);
                var images = document.QuerySelectorAll("img");
                var ownedCount = images.Count(image => policy.IsOwnedUrl(image.GetAttribute("src")));
                string code;
                try { service.PrepareForSave(post.Content); code = "valid"; }
                catch (EditorImageException exception) { code = exception.Code; }
                // Validate an in-memory copy with owned placeholder URLs, without uploading,
                // exposing or saving the article. This separates image migration from markup.
                var cloud = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EditorImageOptions>>().Value.CloudName;
                foreach (var image in images) image.SetAttribute("src", $"https://res.cloudinary.com/{Uri.EscapeDataString(cloud)}/image/upload/diagnostic-placeholder");
                string normalizedCode;
                try { service.PrepareForSave(document.Body?.InnerHtml ?? ""); normalizedCode = "valid"; }
                catch (EditorImageException exception) { normalizedCode = exception.Code; }
                return new {
                    id = post.Id, characters = post.Content.Length, validation = code,
                    imageNormalizedValidation = normalizedCode,
                    ownedImages = ownedCount, otherImages = images.Length - ownedCount,
                    mediaTags = document.QuerySelectorAll("svg,object,embed,iframe,video,audio").Select(element => element.LocalName).Distinct().ToArray(),
                    cssImageProperties = document.All.SelectMany(element => Regex.Matches(element.GetAttribute("style") ?? "", @"(?<name>background-image|border-image|list-style-image|mask-image)\s*:\s*(?<value>[^;]*)", RegexOptions.IgnoreCase).Select(match => new { name = match.Groups["name"].Value.ToLowerInvariant(), value = Regex.IsMatch(match.Groups["value"].Value.Trim(), @"\A(?:none|initial|inherit|unset|revert(?:-layer)?)(?:\s*!important)?\z", RegexOptions.IgnoreCase) ? match.Groups["value"].Value.Trim() : "[resource-or-complex]" })).Distinct().ToArray()
                };
            }).ToArray();
            Console.WriteLine(JsonSerializer.Serialize(new { readOnly = true, success = true, reports }));
        }
        catch (Exception exception)
        {
            var rootError = exception.GetBaseException();
            Console.WriteLine(JsonSerializer.Serialize(new { readOnly = true, success = false, errorType = exception.GetType().Name, rootErrorType = rootError.GetType().Name, sqlErrorNumber = rootError is Microsoft.Data.SqlClient.SqlException sql ? (int?)sql.Number : null }));
            Environment.ExitCode = 1;
        }
    }
}
