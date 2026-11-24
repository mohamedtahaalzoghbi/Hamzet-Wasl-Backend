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

    /// <summary>[Bearer Token — Volunteer] Returns the volunteer profile and current verification status.</summary>
    [HttpGet("volunteer-profile")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VolunteerProfileResponse>>> GetProfile(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetProfileAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Volunteer] Saves volunteer bio, helpMode (online_only / online_and_field), skillLevel, weeklyHours, experienceNotes, and skillIds.</summary>
    [HttpPut("volunteer-profile")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerProfileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VolunteerProfileResponse>>> UpdateProfile(
        [FromBody] UpdateVolunteerProfileRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.UpdateProfileAsync(CurrentUserId, request, ct)));

    /// <summary>[Bearer Token — Volunteer] Returns the volunteer's governorate, serviceArea, map coordinates, and serviceRadiusKm.</summary>
    [HttpGet("volunteer-location")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerLocationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VolunteerLocationResponse>>> GetLocation(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetLocationAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Volunteer] Saves volunteer governorate, serviceArea, latitude/longitude, and serviceRadiusKm (1–100 km).</summary>
    [HttpPut("volunteer-location")]
    [ProducesResponseType(typeof(ApiResponse<VolunteerLocationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VolunteerLocationResponse>>> UpdateLocation(
        [FromBody] VolunteerLocationRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.UpdateLocationAsync(CurrentUserId, request, ct)));

    /// <summary>[Bearer Token — Volunteer] Returns the full verification checklist (steps, currentStep, canBookInterview, uploadedEvidenceTypes, socialLinks).</summary>
    [HttpGet("verification")]
    [ProducesResponseType(typeof(ApiResponse<VerificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> GetVerification(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetVerificationAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Volunteer] Uploads identity verification evidence (national_id_front, national_id_back, or face_photo). Requires email and phone verified first.</summary>
    [HttpPost("verification/evidence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadEvidence(
        [FromForm] string evidenceType, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        await _volunteers.UploadEvidenceAsync(CurrentUserId, evidenceType, upload, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token — Volunteer] Saves social media profile URLs (facebook, linkedin, instagram — full https:// URLs).</summary>
    [HttpPut("verification/social-links")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetSocialLinks(
        [FromBody] SocialLinksRequest request, CancellationToken ct)
    {
        await _volunteers.SetSocialLinksAsync(CurrentUserId, request.Links, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token — Volunteer] Lists upcoming available interview slots (optional ?date=YYYY-MM-DD filter).</summary>
    [HttpGet("/api/v1/interview-slots")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<InterviewSlotDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InterviewSlotDto>>>> GetInterviewSlots(
        [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetInterviewSlotsAsync(date, ct)));

    /// <summary>[Bearer Token — Volunteer] Books an interview slot once all prior verification steps are completed.</summary>
    [HttpPost("verification/interview")]
    [ProducesResponseType(typeof(ApiResponse<VerificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> BookInterview(
        [FromBody] BookInterviewRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.BookInterviewAsync(CurrentUserId, request.InterviewSlotId, ct)));

    [HttpPut("verification/interview")]
    [ProducesResponseType(typeof(ApiResponse<VerificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> ChangeInterview(
        [FromBody] BookInterviewRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.ChangeInterviewAsync(CurrentUserId, request.InterviewSlotId, ct)));

    [HttpDelete("verification/interview")]
    [ProducesResponseType(typeof(ApiResponse<VerificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<VerificationResponse>>> CancelInterview(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.CancelInterviewAsync(CurrentUserId, ct)));

    [HttpGet("availability")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AvailabilitySlotDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AvailabilitySlotDto>>>> GetAvailability(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetAvailabilityAsync(CurrentUserId, ct)));

    [HttpPut("availability")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AvailabilitySlotDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AvailabilitySlotDto>>>> SetAvailability(
        [FromBody] SetAvailabilityRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.SetAvailabilityAsync(CurrentUserId, request.Slots, ct)));

    [HttpGet("training")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TrainingModuleDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TrainingModuleDto>>>> GetTraining(CancellationToken ct) =>
        Ok(Envelope(await _volunteers.GetTrainingAsync(CurrentUserId, ct)));

    [HttpPost("training/{moduleId:guid}/attempts")]
    [ProducesResponseType(typeof(ApiResponse<TrainingAttemptDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TrainingAttemptDto>>> SubmitTrainingAttempt(
        Guid moduleId, [FromBody] SubmitTrainingAttemptRequest request, CancellationToken ct) =>
        Ok(Envelope(await _volunteers.SubmitTrainingAttemptAsync(CurrentUserId, moduleId, request.Score, ct)));
}
