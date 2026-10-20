using Infrastructure.EditorImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Infrastructure.Controllers;

[Authorize(Roles = "Admin")]
[Route("admin/editor-images")]
[EnableRateLimiting("EditorImageUpload")]
[ServiceFilter(typeof(EditorImageAntiforgeryFilter))]
[TypeFilter(typeof(EditorImageApiFilter))]
public sealed class EditorImagesController(IEditorImageService images) : Controller
{
    [HttpPost("upload")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024)]
    public Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken) => RunAsync(async () =>
    {
        if (!ModelState.IsValid || file is null) throw new EditorImageException("missing_image", "Vui lòng chọn file ảnh hợp lệ.");
        return await images.UploadAsync(file, cancellationToken);
    });
    [HttpPost("import")]
    [RequestSizeLimit(16 * 1024)]
    public Task<IActionResult> Import([FromBody] ImportImageRequest? request, CancellationToken cancellationToken) => RunAsync(async () =>
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request?.Url)) throw new EditorImageException("invalid_url", "Vui lòng nhập URL ảnh hợp lệ.");
        return await images.ImportAsync(request.Url, new Uri($"{Request.Scheme}://{Request.Host}"), cancellationToken);
    });
    private async Task<IActionResult> RunAsync(Func<Task<EditorImageResult>> action)
    {
        try { return Ok(await action()); }
        catch (EditorImageException exception) { return StatusCode(exception.Status, new { code = exception.Code, message = exception.Message }); }
    }
    public sealed record ImportImageRequest(string Url);
}
