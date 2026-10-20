using Infrastructure.Configuration;

namespace Infrastructure.EditorImages;

public static class LocalCloudinarySettings
{
    public static void Apply(EditorImageOptions settings, IConfiguration configuration, IReadOnlyDictionary<string, string> local)
    {
        string Resolve(string name, string current)
        {
            var configured = configuration["Cloudinary:" + name];
            return (!string.IsNullOrWhiteSpace(configured) ? configured :
                local.TryGetValue("Cloudinary__" + name, out var value) ? value : current).Trim();
        }
        // Non-empty ASP.NET configuration (including user-secrets/exported env) wins per field.
        settings.CloudName = Resolve("CloudName", settings.CloudName);
        settings.ApiKey = Resolve("ApiKey", settings.ApiKey);
        settings.ApiSecret = Resolve("ApiSecret", settings.ApiSecret);
    }

    public static IReadOnlyDictionary<string, string> Read(string contentRoot) => LocalEnvironmentFile.Read(contentRoot,
        "Cloudinary__CloudName", "Cloudinary__ApiKey", "Cloudinary__ApiSecret");
}
