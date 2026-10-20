namespace Infrastructure.EditorImages;

public sealed class EditorImageOptions
{
    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string Folder { get; set; } = "motoshop/editor";
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudName) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);
}

public sealed record EditorImageResult(string Url, string? PublicId, int? Width, int? Height);
public sealed record CloudinaryAsset(string Url, string PublicId, int Width, int Height, long Bytes, string Format, string ResourceType);
public sealed class EditorImageException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public interface ICloudinaryGateway
{
    Task<CloudinaryAsset> UploadAsync(Stream? stream, string source, string publicId, CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}

public interface IEditorImageService
{
    Task<EditorImageResult> UploadAsync(IFormFile file, CancellationToken cancellationToken);
    Task<EditorImageResult> ImportAsync(string url, Uri origin, CancellationToken cancellationToken);
}
