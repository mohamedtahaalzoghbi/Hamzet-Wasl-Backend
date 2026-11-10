using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Authorize(Policy = "Admin")]
[Route("api/v1/admin")]
public sealed class AdminController : ApiControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    [HttpGet("volunteer-reviews")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VolunteerReviewListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<VolunteerReviewListItemDto>>>> GetVolunteerReviews(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(Envelope(await _admin.GetVolunteerReviewsAsync(status, page, pageSize, ct)));

    [HttpGet("volunteer-reviews/{reviewId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerReviewDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VolunteerReviewDetailDto>>> GetVolunteerReview(
        Guid reviewId, CancellationToken ct) =>
        Ok(Envelope(await _admin.GetVolunteerReviewAsync(reviewId, ct)));

    [HttpPost("volunteer-reviews/{reviewId:guid}/decision")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DecideVolunteerReview(
        Guid reviewId, [FromBody] DecisionRequest request, CancellationToken ct)
    {
        await _admin.DecideVolunteerReviewAsync(reviewId, CurrentUserId, request, ct);
        return NoContent();
    }

    [HttpGet("organization-reviews")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OrganizationReviewListItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<OrganizationReviewListItemDto>>>> GetOrganizationReviews(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(Envelope(await _admin.GetOrganizationReviewsAsync(status, page, pageSize, ct)));

    [HttpGet("organization-reviews/{reviewId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationReviewDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizationReviewDetailDto>>> GetOrganizationReview(
        Guid reviewId, CancellationToken ct) =>
        Ok(Envelope(await _admin.GetOrganizationReviewAsync(reviewId, ct)));

    [HttpPost("organization-reviews/{reviewId:guid}/decision")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DecideOrganizationReview(
        Guid reviewId, [FromBody] DecisionRequest request, CancellationToken ct)
    {
        await _admin.DecideOrganizationReviewAsync(reviewId, CurrentUserId, request, ct);
        return NoContent();
    }

    [HttpGet("interview-slots")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<InterviewSlotAdminDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InterviewSlotAdminDto>>>> GetInterviewSlots(CancellationToken ct) =>
        Ok(Envelope(await _admin.GetInterviewSlotsAsync(ct)));

    [HttpPost("interview-slots")]
    [ProducesResponseType(typeof(ApiResponse<InterviewSlotAdminDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<InterviewSlotAdminDto>>> CreateInterviewSlot(
        [FromBody] CreateInterviewSlotRequest request, CancellationToken ct) =>
        CreatedEnvelope(await _admin.CreateInterviewSlotAsync(CurrentUserId, request, ct));

    [HttpDelete("interview-slots/{slotId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteInterviewSlot(Guid slotId, CancellationToken ct)
    {
        await _admin.DeleteInterviewSlotAsync(CurrentUserId, slotId, ct);
        return NoContent();
    }

    [HttpPut("users/{userId:guid}/suspension")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetUserSuspension(
        Guid userId, [FromBody] SuspendUserRequest request, CancellationToken ct)
    {
        await _admin.SetUserSuspensionAsync(CurrentUserId, userId, request, ct);
        return NoContent();
    }

    [HttpGet("deletion-requests")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DeletionRequestDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DeletionRequestDto>>>> GetDeletionRequests(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(Envelope(await _admin.GetDeletionRequestsAsync(status, page, pageSize, ct)));

    [HttpPost("deletion-requests/{deletionRequestId:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResolveDeletionRequest(
        Guid deletionRequestId, [FromBody] ResolveDeletionRequestRequest request, CancellationToken ct)
    {
        await _admin.ResolveDeletionRequestAsync(CurrentUserId, deletionRequestId, request, ct);
        return NoContent();
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminAuditDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminAuditDto>>>> GetAudit(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await _admin.GetAuditAsync(page, pageSize, ct)));
}
