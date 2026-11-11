using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Notifications;
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
    private readonly IDeletionRequestService _deletionRequests;
    private readonly INotificationService _notifications;

    public ProfileController(
        IProfileService profiles,
        IDeletionRequestService deletionRequests,
        INotificationService notifications)
    {
        _profiles = profiles;
        _deletionRequests = deletionRequests;
        _notifications = notifications;
    }

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

    /// <summary>
    /// Creates a pending account-deletion request. The admin queue
    /// (<c>GET /api/v1/admin/deletion-requests</c>) resolves it later.
    /// </summary>
    [HttpPost("deletion-request")]
    [ProducesResponseType(typeof(ApiResponse<DeletionRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<DeletionRequestResponse>>> RequestDeletion(CancellationToken ct) =>
        CreatedEnvelope(await _deletionRequests.RequestAsync(CurrentUserId, ct));

    /// <summary>Notification centre: newest first, paged like every other list endpoint.</summary>
    [HttpGet("notifications")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(Envelope(await _notifications.GetForRecipientAsync(CurrentUserId, page, pageSize, ct)));

    /// <summary>Marks one notification as read; idempotent and scoped to the caller.</summary>
    [HttpPut("notifications/{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNotificationRead(Guid notificationId, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(CurrentUserId, notificationId, ct);
        return NoContent();
    }
}
