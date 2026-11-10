using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HamzetWasl.Api.Controllers;

[Authorize]
[Route("api/v1/me")]
public sealed class ProfileController : ApiControllerBase
{
    private readonly IProfileService _profiles;

    public ProfileController(IProfileService profiles) => _profiles = profiles;

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Get(CancellationToken ct) =>
        Ok(Envelope(await _profiles.GetAsync(CurrentUserId, ct)));

    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Update(
        [FromBody] UpdateProfileRequest request, CancellationToken ct) =>
        Ok(Envelope(await _profiles.UpdateAsync(CurrentUserId, request, ct)));

    [HttpPut("profile/avatar")]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateAvatar(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await _profiles.UpdateAvatarAsync(CurrentUserId, upload, ct);
        return Ok(Envelope<object>(new { avatarUrl = url }));
    }

    [HttpPut("interests")]
    public async Task<IActionResult> SetInterests([FromBody] SetInterestsRequest request, CancellationToken ct)
    {
        await _profiles.SetInterestsAsync(CurrentUserId, request.InterestIds, ct);
        return NoContent();
    }

    [HttpPut("skills")]
    public async Task<IActionResult> SetSkills([FromBody] SetSkillsRequest request, CancellationToken ct)
    {
        await _profiles.SetSkillsAsync(CurrentUserId, request.SkillIds, ct);
        return NoContent();
    }
}
