using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Infrastructure.EditorImages;

public sealed class EditorImagePolicy(IOptions<EditorImageOptions> options)
{
    public bool IsOwnedUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(options.Value.CloudName) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        var cloud = Uri.EscapeDataString(options.Value.CloudName);
        return uri.Scheme == "https" && uri.IdnHost.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase)
            && uri.IsDefaultPort && uri.UserInfo.Length == 0
            && uri.AbsolutePath.StartsWith($"/{cloud}/image/upload/", StringComparison.Ordinal)
            && uri.AbsolutePath.Length > $"/{cloud}/image/upload/".Length;
    }

    public static string DetectFormat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return "jpg";
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))) return "gif";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return "webp";
        throw new EditorImageException("unsupported_format", "Chỉ hỗ trợ ảnh JPEG, PNG, WebP và GIF có định dạng hợp lệ.");
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (b[0] & 0xe0) == 0x20; // Global unicast only; no local, mapped, link-local or multicast addresses.
        return b[0] != 0 && b[0] != 10 && b[0] != 127 && b[0] < 224
            && !(b[0] == 169 && b[1] == 254) && !(b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            && !(b[0] == 192 && b[1] == 168) && !(b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            && !(b[0] == 198 && b[1] is 18 or 19) && !(b[0] == 192 && b[1] == 0)
            && !(b[0] == 198 && b[1] == 51 && b[2] == 100) && !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}

public interface IImageHostResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
public sealed class ImageHostResolver : IImageHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken);
}
