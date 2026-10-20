using System.Collections.Concurrent;
using System.Net;
using Infrastructure.EditorImages;

public sealed class FixtureCloudinaryGateway : ICloudinaryGateway
{
    public string Mode { get; set; } = "normal";
    public ConcurrentQueue<object> Uploads { get; } = new();
    public ConcurrentQueue<string> Deletes { get; } = new();
    private int active;
    private int maximum;
    public int MaximumConcurrent => maximum;
    public async Task<CloudinaryAsset> UploadAsync(Stream? stream, string source, string publicId, CancellationToken token)
    {
        var count = Interlocked.Increment(ref active);
        int previous;
        do { previous = maximum; } while (count > previous && Interlocked.CompareExchange(ref maximum, count, previous) != previous);
        try
        {
            await Task.Delay(Mode == "slow" || source.Contains("slow") ? 900 : 80, token);
            if (Mode == "fail" || source.Contains("fail")) throw new EditorImageException("provider_error", "Cloudinary fixture: lỗi upload thử nghiệm.", 502);
            if (Mode == "timeout" || source.Contains("timeout")) throw new OperationCanceledException();
            Uploads.Enqueue(new { source, publicId, isFile = stream is not null });
            return new($"https://res.cloudinary.com/motoshop-fixture/image/upload/v1/{publicId}.png", publicId, 64, 64, Mode == "oversize" ? 5242881 : 1024, "png", "image");
        }
        finally { Interlocked.Decrement(ref active); }
    }
    public Task DeleteAsync(string publicId, CancellationToken token) { Deletes.Enqueue(publicId); return Task.CompletedTask; }
}

public sealed class FixtureImageHostResolver : IImageHostResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken token) => Task.FromResult(new[] {
        IPAddress.TryParse(host, out var ip) ? ip : IPAddress.Parse(host.StartsWith("private") ? "10.0.0.1" : "8.8.8.8")
    });
}
