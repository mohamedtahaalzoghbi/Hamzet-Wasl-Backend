using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[AllowAnonymous]
[Route("api/v1/files")]
public sealed class FilesController : ApiControllerBase
{
    private readonly IFileStorage _storage;

    public FilesController(IFileStorage storage) => _storage = storage;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string key, [FromQuery] long expires, [FromQuery] string signature,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || !_storage.ValidateSignedUrl(key, expires, signature))
        {
            throw new AppException(401, ErrorCodes.Unauthorized, "رابط الملف غير صالح أو منتهي.");
        }

        var stream = await _storage.OpenReadAsync(key, ct);
        if (stream is null)
        {
            throw AppException.NotFound("الملف غير موجود.");
        }

        var contentType = Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".pdf" => "application/pdf",
            ".mp3" => "audio/mpeg",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".webm" => "video/webm",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };

        return File(stream, contentType, enableRangeProcessing: true);
    }
}
