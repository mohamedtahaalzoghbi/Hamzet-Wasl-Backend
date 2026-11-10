using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HamzetWasl.Api.Controllers;

[Authorize(Policy = "Volunteer")]
[Route("api/v1/me")]
public sealed class VolunteerController : ApiControllerBase
{
    private readonly IVolunteerService _volunteers;

    public VolunteerController(IVolunteerService volunteers) => _volunteers = volunteers;

    [HttpGet("volunteer-profile")]
    public async Task<ActionResult<ApiResponse<VolunteerProfileResponse>>> GetProfile(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetProfileAsync(CurrentUserId, ct)));

    [HttpPut("volunteer-profile")]
    public async Task<ActionResult<ApiResponse<VolunteerProfileResponse>>> UpdateProfile(
        [FromBody] UpdateVolunteerProfileRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.UpdateProfileAsync(CurrentUserId, request, ct)));

    [HttpGet("volunteer-location")]
    public async Task<ActionResult<ApiResponse<VolunteerLocationResponse>>> GetLocation(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetLocationAsync(CurrentUserId, ct)));

    [HttpPut("volunteer-location")]
    public async Task<ActionResult<ApiResponse<VolunteerLocationResponse>>> UpdateLocation(
        [FromBody] VolunteerLocationRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.UpdateLocationAsync(CurrentUserId, request, ct)));

    [HttpGet("verification")]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> GetVerification(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetVerificationAsync(CurrentUserId, ct)));

    [HttpPost("verification/evidence")]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidence(
        [FromForm] string evidenceType, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        await _volunteers.UploadEvidenceAsync(CurrentUserId, evidenceType, upload, ct);
        return NoContent();
    }

    [HttpPut("verification/social-links")]
    public async Task<IActionResult> SetSocialLinks(
        [FromBody] SocialLinksRequest request, CancellationToken ct)
    {
        await _volunteers.SetSocialLinksAsync(CurrentUserId, request.Links, ct);
        return NoContent();
    }

    [HttpGet("/api/v1/interview-slots")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InterviewSlotDto>>>> GetInterviewSlots(
        [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetInterviewSlotsAsync(date, ct)));

    [HttpPost("verification/interview")]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> BookInterview(
        [FromBody] BookInterviewRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.BookInterviewAsync(CurrentUserId, request.InterviewSlotId, ct)));

    [HttpPut("verification/interview")]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> ChangeInterview(
        [FromBody] BookInterviewRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.ChangeInterviewAsync(CurrentUserId, request.InterviewSlotId, ct)));

    [HttpDelete("verification/interview")]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> CancelInterview(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.CancelInterviewAsync(CurrentUserId, ct)));

    [HttpGet("availability")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AvailabilitySlotDto>>>> GetAvailability(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetAvailabilityAsync(CurrentUserId, ct)));

    [HttpPut("availability")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AvailabilitySlotDto>>>> SetAvailability(
        [FromBody] SetAvailabilityRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.SetAvailabilityAsync(CurrentUserId, request.Slots, ct)));

    [HttpGet("training")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrainingModuleDto>>>> GetTraining(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetTrainingAsync(CurrentUserId, ct)));

    [HttpPost("training/{moduleId:guid}/attempts")]
    public async Task<ActionResult<ApiResponse<TrainingAttemptDto>>> SubmitTrainingAttempt(
        Guid moduleId, [FromBody] SubmitTrainingAttemptRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.SubmitTrainingAttemptAsync(CurrentUserId, moduleId, request.Score, ct)));
}
