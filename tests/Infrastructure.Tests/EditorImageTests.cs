using System.Net;
using Infrastructure.EditorImages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Infrastructure.Tests;

public sealed class EditorImageTests
{
    private const string CloudImage = "https://res.cloudinary.com/test-cloud/image/upload/v1/motoshop/editor/example.png";
    private static EditorImageOptions Settings() => new() { CloudName = "test-cloud", ApiKey = "FAKE", ApiSecret = "FAKE" };
    private static EditorContentService Content() => new(new EditorImagePolicy(Options.Create(Settings())));

    [Theory]
    [InlineData("border-image: initial;")]
    [InlineData("border-image: none; background-image: none !important;")]
    [InlineData("mask-image: unset; list-style-image: inherit;")]
    [InlineData("border-image:revert-layer; background-image:revert")]
    [InlineData("border-image:/**/initial; text-align: justify;")]
    public void CopiedCssResetsDoNotBlockArticleSave(string css)
    {
        var result = Content().PrepareForSave($"<p style='{css}'>Copied article</p><img src='{CloudImage}'>");
        Assert.Contains("Copied article", result);
        Assert.Contains(CloudImage, result);
        Assert.DoesNotContain("border-image", result);
        Assert.DoesNotContain("background-image", result);
        Assert.DoesNotContain("mask-image", result);
    }

    [Theory]
    [InlineData("border-image: initial; background-image: url(data:image/png;base64,AAAA)")]
    [InlineData("border-image:initial url(https://example.invalid/image.png)")]
    [InlineData("border-image:initial; mask-image:u\\72l(https://example.invalid/image.png)")]
    public void CssResetDoesNotAllowActualUnresolvedImageReferences(string css) =>
        Assert.Throws<EditorImageException>(() => Content().PrepareForSave($"<p style='{css}'>Copy</p>"));

    [Theory]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("blob:https://localhost/fake")]
    [InlineData("file:///private/image.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.com/image.png")]
    [InlineData("https://res.cloudinary.com/other-cloud/image/upload/x.png")]
    [InlineData("//res.cloudinary.com/test-cloud/image/upload/x.png")]
    [InlineData("https://res.cloudinary.com.evil.invalid/test-cloud/image/upload/x.png")]
    [InlineData("https://res.cloudinary.com/test-cloud/raw/upload/x.png")]
    [InlineData("d&#97;ta:image/png;base64,AAAA")]
    public void SaveRejectsUnresolvedImages(string source) => Assert.Throws<EditorImageException>(() => Content().PrepareForSave($"<p>Giữ nội dung</p><img src='{source}'>"));

    [Theory]
    [InlineData("<svg><image href='data:image/png;base64,AAAA'></image></svg>")]
    [InlineData("<picture><source srcset='data:image/png;base64,AAAA'></picture>")]
    [InlineData("<img srcset='data:image/png;base64,AAAA'>")]
    [InlineData("<div style='background-image:url(data:image/png;base64,AAAA)'>x</div>")]
    [InlineData("<div style='background:u\\72l(data:image/png;base64,AAAA)'>x</div>")]
    [InlineData("<div style='background:u/**/rl(data:image/png;base64,AAAA)'>x</div>")]
    [InlineData("<iframe src='https://example.com'></iframe>")]
    public void SaveRejectsUnsupportedImageMarkup(string html) => Assert.Throws<EditorImageException>(() => Content().PrepareForSave(html));

    [Fact]
    public void SaveKeepsCloudinaryImageAndFormattingButRemovesExecution()
    {
        var result = Content().PrepareForSave($"<h2>Tiêu đề</h2><p><strong>Đậm</strong><a href='javascript:alert(1)'>Liên kết</a></p><img src='{CloudImage}' onerror='alert(1)' style='width:50%;float:left' alt='Mô tả'><script>alert(1)</script>");
        Assert.Contains(CloudImage, result);
        Assert.Contains("<strong>Đậm</strong>", result);
        Assert.Contains("alt=\"Mô tả\"", result);
        Assert.Contains("float", result);
        Assert.DoesNotContain("onerror", result);
        Assert.DoesNotContain("javascript:", result);
        Assert.DoesNotContain("<script", result);
    }

    [Fact]
    public void ResponsiveFallbackIsPreservedWithoutHiddenImageSources()
    {
        var result = Content().PrepareForSave($"<picture><source srcset='data:image/png;base64,AAAA'><img src='{CloudImage}' srcset='data:image/png;base64,AAAA'></picture>");
        Assert.Contains(CloudImage, result);
        Assert.DoesNotContain("srcset", result);
        Assert.DoesNotContain("data:image", result);
    }

    [Fact]
    public void PlainLegacyDescriptionsRemainLiteralUntilExplicitSave()
    {
        const string old = "Giữ <tag> & dấu < >\r\nDòng hai";
        var html = EditorContentService.ProductEditorHtml(old);
        Assert.Contains("&lt;tag&gt;", html);
        Assert.Contains("&amp;", html);
        Assert.Contains("<br>", html);
        var stored = Content().PrepareForSave(html, true);
        Assert.StartsWith(EditorContentService.ProductPrefix, stored);
        var parser = new AngleSharp.Html.Parser.HtmlParser();
        Assert.Equal(parser.ParseDocument(html).Body!.TextContent, parser.ParseDocument(EditorContentService.ProductEditorHtml(stored)).Body!.TextContent);
        Assert.Contains("<br>", stored);
        Assert.DoesNotContain(EditorContentService.ProductPrefix, Content().ProductDisplayHtml(stored));
    }

    [Fact]
    public void PlainTextWorksWithoutCloudinaryCredentials()
    {
        var content = new EditorContentService(new EditorImagePolicy(Options.Create(new EditorImageOptions())));
        Assert.Equal("<p>Nội dung chữ</p>", content.PrepareForSave("<p>Nội dung chữ</p>"));
    }

    [Fact]
    public void HtmlSizeLimitIsUtf8AndDoesNotScanLiteralBase64ExamplesAsImages()
    {
        Assert.Contains("data:image", Content().PrepareForSave("<code>Ví dụ data:image/png;base64,AAAA</code>"));
        Assert.Throws<EditorImageException>(() => Content().PrepareForSave(new string('ạ', EditorContentService.MaxHtmlBytes / 2)));
    }

    [Theory]
    [InlineData("127.0.0.1")][InlineData("10.1.2.3")][InlineData("172.16.0.1")][InlineData("192.168.0.1")]
    [InlineData("169.254.169.254")][InlineData("100.64.0.1")][InlineData("::1")][InlineData("fc00::1")]
    [InlineData("fe80::1")][InlineData("::ffff:127.0.0.1")][InlineData("224.0.0.1")]
    public void PrivateAddressesAreRejected(string ip) => Assert.False(EditorImagePolicy.IsPublicAddress(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")][InlineData("1.1.1.1")][InlineData("2606:4700:4700::1111")]
    public void PublicAddressesAreAllowed(string ip) => Assert.True(EditorImagePolicy.IsPublicAddress(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("89504E470D0A1A0A", "png")][InlineData("FFD8FF00", "jpg")]
    [InlineData("474946383961", "gif")][InlineData("524946460000000057454250", "webp")]
    public void SupportedSignaturesAreRecognized(string hex, string format) => Assert.Equal(format, EditorImagePolicy.DetectFormat(Convert.FromHexString(hex)));

    [Fact]
    public void RenamingSvgToPngDoesNotPassSignatureCheck() => Assert.Throws<EditorImageException>(() => EditorImagePolicy.DetectFormat("<svg></svg>"u8));

    private static (EditorImageService Service, Mock<ICloudinaryGateway> Gateway) Images(EditorImageOptions? settings = null, string? root = null, long resultSize = 100)
    {
        var options = Options.Create(settings ?? Settings());
        var gateway = new Mock<ICloudinaryGateway>();
        gateway.Setup(g => g.UploadAsync(It.IsAny<Stream?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream? stream, string source, string id, CancellationToken token) => new CloudinaryAsset($"https://res.cloudinary.com/test-cloud/image/upload/v1/{id}.png", id, 10, 10, resultSize, "png", "image"));
        gateway.Setup(g => g.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var environment = new Mock<IWebHostEnvironment>(); environment.SetupGet(e => e.WebRootPath).Returns(root ?? Path.GetTempPath());
        var resolver = new Mock<IImageHostResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string host, CancellationToken token) => new[] { IPAddress.Parse(host.StartsWith("private") ? "10.0.0.1" : "8.8.8.8") });
        return (new EditorImageService(gateway.Object, options, new EditorImagePolicy(options), environment.Object, resolver.Object, NullLogger<EditorImageService>.Instance), gateway);
    }
    private static IFormFile Png(long? reportedSize = null)
    {
        var bytes = Convert.FromHexString("89504E470D0A1A0A00000000");
        return new FormFile(new MemoryStream(bytes), 0, reportedSize ?? bytes.Length, "file", "test.png");
    }

    [Fact]
    public async Task MissingCredentialsReturnUnavailableWithoutCallingCloudinary()
    {
        var (service, gateway) = Images(new EditorImageOptions());
        var error = await Assert.ThrowsAsync<EditorImageException>(() => service.UploadAsync(Png(), default));
        Assert.Equal(503, error.Status); gateway.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task UploadStreamsToCloudinaryAndUsesUniquePublicIds()
    {
        var (service, _) = Images();
        var first = await service.UploadAsync(Png(), default);
        var second = await service.UploadAsync(Png(), default);
        Assert.NotEqual(first.PublicId, second.PublicId);
        Assert.StartsWith("https://res.cloudinary.com/test-cloud/image/upload/", first.Url);
    }
    [Fact]
    public async Task OversizedInputIsRejectedBeforeProviderCall()
    {
        var (service, gateway) = Images();
        Assert.Equal(413, (await Assert.ThrowsAsync<EditorImageException>(() => service.UploadAsync(Png(5242881), default))).Status);
        gateway.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task OversizedRemoteResultDeletesOnlyTheNewAsset()
    {
        var (service, gateway) = Images(resultSize: 5242881);
        Assert.Equal(413, (await Assert.ThrowsAsync<EditorImageException>(() => service.ImportAsync("https://public.invalid/a.png", new Uri("https://localhost:7042"), default))).Status);
        gateway.Verify(g => g.DeleteAsync(It.Is<string>(id => id.StartsWith("motoshop/editor/")), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task OwnedUrlIsReusedWithoutUploadingOrDeleting()
    {
        var (service, gateway) = Images();
        Assert.Equal(CloudImage, (await service.ImportAsync(CloudImage, new Uri("https://localhost:7042"), default)).Url);
        gateway.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData("file:///private/a.png")][InlineData("https://user:password@public.invalid/a.png")]
    [InlineData("http://localhost/a.png")][InlineData("https://private.invalid/a.png")]
    [InlineData("https://public.invalid:8443/a.png")][InlineData("//public.invalid/a.png")]
    public async Task UnsafeImportUrlsNeverReachCloudinary(string url)
    {
        var (service, gateway) = Images();
        await Assert.ThrowsAsync<EditorImageException>(() => service.ImportAsync(url, new Uri("https://localhost:7042"), default));
        gateway.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task SameOriginImagesUseLocalStreamAndCannotEscapeAllowedFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "motoshop-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "images"));
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "images", "sample.png"), Convert.FromHexString("89504E470D0A1A0A00000000"));
            var (service, gateway) = Images(root: root);
            await service.ImportAsync("/images/sample.png", new Uri("https://localhost:7042"), default);
            gateway.Verify(g => g.UploadAsync(It.IsNotNull<Stream>(), "editor-image.png", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            await Assert.ThrowsAsync<EditorImageException>(() => service.ImportAsync("/images/%2e%2e/private.png", new Uri("https://localhost:7042"), default));
        }
        finally { File.Delete(Path.Combine(root, "images", "sample.png")); Directory.Delete(Path.Combine(root, "images")); Directory.Delete(root); }
    }
    [Fact]
    public async Task TimeoutIsReportedWithoutExposingProviderError()
    {
        var (service, gateway) = Images();
        gateway.Setup(g => g.UploadAsync(It.IsAny<Stream?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        Assert.Equal(504, (await Assert.ThrowsAsync<EditorImageException>(() => service.UploadAsync(Png(), default))).Status);
    }
}
