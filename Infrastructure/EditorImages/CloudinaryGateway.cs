using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace Infrastructure.EditorImages;

public sealed class CloudinaryGateway(IOptions<EditorImageOptions> options, ILogger<CloudinaryGateway> logger) : ICloudinaryGateway
{
    private Cloudinary CreateClient()
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new EditorImageException("not_configured", "Chưa cấu hình Cloudinary. Vui lòng bổ sung CloudName, ApiKey và ApiSecret ở máy chủ.", 503);
        var client = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        client.Api.Secure = true;
        return client;
    }

    public async Task<CloudinaryAsset> UploadAsync(Stream? stream, string source, string publicId, CancellationToken cancellationToken)
    {
        var result = await CreateClient().UploadAsync(new ImageUploadParams
        {
            File = stream is null ? new FileDescription(source) : new FileDescription(source, stream),
            PublicId = publicId,
            Overwrite = false,
            AllowedFormats = ["jpg", "jpeg", "png", "webp", "gif"]
        }, cancellationToken);
        if (result.Error is not null || (int)result.StatusCode is < 200 or >= 300 || result.SecureUrl is null)
        {
            logger.LogWarning("Cloudinary upload rejected. HttpStatus={HttpStatus} Reason={Reason}", (int)result.StatusCode, Classify(result.Error?.Message));
            throw new EditorImageException("provider_error", "Cloudinary chưa xử lý được ảnh. Vui lòng kiểm tra cấu hình hoặc thử lại.", 502);
        }
        return new(result.SecureUrl.AbsoluteUri, result.PublicId, result.Width, result.Height, result.Bytes, result.Format, result.ResourceType);
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        var result = await CreateClient().DestroyAsync(new DeletionParams(publicId)).WaitAsync(cancellationToken);
        if (result.Error is not null)
        {
            logger.LogWarning("Cloudinary cleanup rejected. HttpStatus={HttpStatus} Reason={Reason}", (int)result.StatusCode, Classify(result.Error.Message));
            throw new EditorImageException("cleanup_error", "Không xóa được bản upload không hợp lệ.", 502);
        }
    }

    private static string Classify(string? message)
    {
        if (message?.Contains("signature", StringComparison.OrdinalIgnoreCase) == true) return "invalid_signature";
        if (message?.Contains("api key", StringComparison.OrdinalIgnoreCase) == true) return "invalid_api_key";
        if (message?.Contains("cloud name", StringComparison.OrdinalIgnoreCase) == true) return "invalid_cloud_name";
        if (message?.Contains("quota", StringComparison.OrdinalIgnoreCase) == true || message?.Contains("limit", StringComparison.OrdinalIgnoreCase) == true) return "provider_limit";
        return "provider_rejected";
    }
}
