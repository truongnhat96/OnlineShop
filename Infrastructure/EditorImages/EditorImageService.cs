using Microsoft.Extensions.Options;

namespace Infrastructure.EditorImages;

public sealed class EditorImageService(
    ICloudinaryGateway gateway, IOptions<EditorImageOptions> options, EditorImagePolicy policy,
    IWebHostEnvironment environment, IImageHostResolver resolver, ILogger<EditorImageService> logger) : IEditorImageService
{
    public Task<EditorImageResult> UploadAsync(IFormFile file, CancellationToken cancellationToken) => ExecuteAsync(async token =>
    {
        CheckConfigured();
        if (file.Length == 0) throw new EditorImageException("empty_image", "Ảnh đang trống.");
        CheckSize(file.Length);
        await using var source = file.OpenReadStream();
        return await UploadStreamAsync(source, token);
    }, cancellationToken);

    public Task<EditorImageResult> ImportAsync(string url, Uri origin, CancellationToken cancellationToken) => ExecuteAsync(async token =>
    {
        if (policy.IsOwnedUrl(url)) return new EditorImageResult(new Uri(url).AbsoluteUri, null, null, null);
        CheckConfigured();
        if (string.IsNullOrWhiteSpace(url) || url.Length > 4096 || url.StartsWith("//", StringComparison.Ordinal)
            || !Uri.TryCreate(origin, url, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0)
            throw new EditorImageException("invalid_url", "URL ảnh phải là địa chỉ HTTP/HTTPS hợp lệ.");
        if (uri.GetLeftPart(UriPartial.Authority).Equals(origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = OpenLocalImage(uri);
            CheckSize(stream.Length);
            return await UploadStreamAsync(stream, token);
        }
        if (!uri.IsDefaultPort || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            throw new EditorImageException("private_url", "Chỉ cho phép URL ảnh công khai hoặc ảnh có sẵn của cửa hàng.");
        var addresses = await resolver.ResolveAsync(uri.DnsSafeHost, token);
        if (addresses.Length == 0 || addresses.Any(address => !EditorImagePolicy.IsPublicAddress(address)))
            throw new EditorImageException("private_url", "Không cho phép URL ảnh trỏ tới mạng nội bộ.");
        return await UploadToCloudinaryAsync(null, uri.AbsoluteUri, token);
    }, cancellationToken);

    private FileStream OpenLocalImage(Uri uri)
    {
        var relative = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        if (relative.Contains('\\') || relative.Contains('\0') || relative.Split('/').Any(part => part is "." or "..") || !new[] { "uploads/products/", "uploads/posts/", "images/" }.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
            throw new EditorImageException("invalid_local_path", "Chỉ hỗ trợ ảnh trong thư mục ảnh của cửa hàng.");
        var root = Path.GetFullPath(environment.WebRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            throw new EditorImageException("image_not_found", "Không tìm thấy ảnh trong cửa hàng.");
        for (FileSystemInfo? entry = new FileInfo(fullPath); entry is not null && entry.FullName.Length >= root.TrimEnd(Path.DirectorySeparatorChar).Length; entry = entry is FileInfo file ? file.Directory : ((DirectoryInfo)entry).Parent)
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new EditorImageException("invalid_local_path", "Không hỗ trợ đường dẫn ảnh liên kết.");
        return File.OpenRead(fullPath);
    }

    private async Task<EditorImageResult> UploadStreamAsync(Stream source, CancellationToken token)
    {
        // Bound the actual bytes, not just the client's claimed length.
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, token)) > 0)
        {
            CheckSize(buffer.Length + read);
            await buffer.WriteAsync(chunk.AsMemory(0, read), token);
        }
        var format = EditorImagePolicy.DetectFormat(buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, 12)));
        buffer.Position = 0;
        return await UploadToCloudinaryAsync(buffer, "editor-image." + format, token);
    }

    private async Task<EditorImageResult> UploadToCloudinaryAsync(Stream? stream, string source, CancellationToken token)
    {
        var folder = options.Value.Folder.Trim('/');
        if (folder.Length == 0 || folder.Split('/').Any(part => part is "." or ".." || part.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))))
            throw new EditorImageException("invalid_configuration", "Thư mục Cloudinary không hợp lệ.", 503);
        var publicId = folder + "/" + Guid.NewGuid().ToString("N");
        var asset = await gateway.UploadAsync(stream, source, publicId, token);
        if (asset.PublicId != publicId || !policy.IsOwnedUrl(asset.Url) || asset.ResourceType != "image"
            || !new[] { "jpg", "jpeg", "png", "webp", "gif" }.Contains(asset.Format) || asset.Width <= 0 || asset.Height <= 0 || asset.Bytes <= 0 || asset.Bytes > options.Value.MaxImageBytes)
        {
            if (asset.PublicId == publicId)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await gateway.DeleteAsync(publicId, cleanup.Token);
                }
                catch { logger.LogWarning("Could not clean up rejected editor asset {PublicId}.", publicId); }
            }
            throw new EditorImageException("invalid_provider_image", "Ảnh không đúng định dạng cho phép hoặc vượt quá dung lượng tối đa.", asset.Bytes > options.Value.MaxImageBytes ? 413 : 400);
        }
        return new(asset.Url, asset.PublicId, asset.Width, asset.Height);
    }

    private void CheckConfigured()
    {
        if (!options.Value.IsConfigured)
        {
            logger.LogWarning("Cloudinary editor configuration incomplete. HasCloudName={HasCloudName} HasApiKey={HasApiKey} HasApiSecret={HasApiSecret}",
                !string.IsNullOrWhiteSpace(options.Value.CloudName), !string.IsNullOrWhiteSpace(options.Value.ApiKey), !string.IsNullOrWhiteSpace(options.Value.ApiSecret));
            throw new EditorImageException("not_configured", "Chưa cấu hình Cloudinary. Vui lòng bổ sung CloudName, ApiKey và ApiSecret ở máy chủ.", 503);
        }
    }
    private void CheckSize(long length)
    {
        if (length > options.Value.MaxImageBytes) throw new EditorImageException("image_too_large", $"Ảnh vượt quá giới hạn {options.Value.MaxImageBytes / 1024 / 1024} MiB.", 413);
    }
    private async Task<EditorImageResult> ExecuteAsync(Func<CancellationToken, Task<EditorImageResult>> action, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { return await action(timeout.Token); }
        catch (EditorImageException) { throw; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new EditorImageException("upload_timeout", "Upload ảnh quá thời gian chờ. Vui lòng thử lại.", 504); }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Editor image network operation failed. HttpStatus={HttpStatus} Reason={Reason}", (int?)exception.StatusCode, exception.HttpRequestError);
            throw new EditorImageException("upload_failed", "Chưa tải được ảnh. Vui lòng kiểm tra kết nối và thử lại.", 502);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Editor image operation failed ({ExceptionType}).", exception.GetType().Name);
            throw new EditorImageException("upload_failed", "Chưa tải được ảnh. Vui lòng kiểm tra URL, cấu hình hoặc thử lại.", 502);
        }
    }
}
