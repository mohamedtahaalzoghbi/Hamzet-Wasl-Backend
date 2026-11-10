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
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Get(CancellationToken ct) =>
        Ok(Envelope(await _profiles.GetAsync(CurrentUserId, ct)));

    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Update(
        [FromBody] UpdateProfileRequest request, CancellationToken ct) =>
        Ok(Envelope(await _profiles.UpdateAsync(CurrentUserId, request, ct)));

    [HttpPut("profile/avatar")]
    [ProducesResponseType(typeof(ApiResponse<AvatarResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<AvatarResponse>>> UpdateAvatar(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await _profiles.UpdateAvatarAsync(CurrentUserId, upload, ct);
        return Ok(Envelope(new AvatarResponse(url)));
    }

    [HttpPut("interests")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetInterests([FromBody] SetInterestsRequest request, CancellationToken ct)
    {
        await _profiles.SetInterestsAsync(CurrentUserId, request.InterestIds, ct);
        return NoContent();
    }

    [HttpPut("skills")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetSkills([FromBody] SetSkillsRequest request, CancellationToken ct)
    {
        await _profiles.SetSkillsAsync(CurrentUserId, request.SkillIds, ct);
        return NoContent();
    }
}
