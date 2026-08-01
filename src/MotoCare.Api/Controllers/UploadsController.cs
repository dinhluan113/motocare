using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MotoCare.Api.Infrastructure;
using MotoCare.Api.Services;

namespace MotoCare.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
[Authorize(Roles = SecurityRoles.Operations)]
public sealed class UploadsController(ImageStorageService storage) : ControllerBase
{
    [HttpPost("images")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(
        IFormFile file,
        [FromQuery] string category = "general",
        CancellationToken cancellationToken = default)
    {
        if (file is null) throw new InvalidOperationException("Chưa chọn ảnh tải lên.");
        await using var stream = file.OpenReadStream();
        var path = await storage.SaveAsync(
            stream,
            file.FileName,
            file.ContentType,
            category,
            file.Length,
            cancellationToken);
        return Ok(ApiEnvelope.Ok(new { path }));
    }

    [HttpDelete("images")]
    public IActionResult DeleteImage(DeleteUploadedImageRequest request)
    {
        return Ok(ApiEnvelope.Ok(new { path = request.Path, deleted = storage.Delete(request.Path) }));
    }
}

public sealed record DeleteUploadedImageRequest(string Path);
