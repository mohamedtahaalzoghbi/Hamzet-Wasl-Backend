using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Jobs;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/jobs")]
public sealed class JobsController(IJobService jobs) : ApiControllerBase
{
    private Guid? ViewerId => User.Identity?.IsAuthenticated == true ? CurrentUserId : null;

    /// <summary>[Public] Published jobs of approved organizations. Filters: q, employmentType, workArrangement.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<JobDto>>>> Search(
        [FromQuery] string? q, [FromQuery] string? employmentType, [FromQuery] string? workArrangement,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await jobs.SearchAsync(ViewerId, q, employmentType, workArrangement, page, pageSize, ct)));

    /// <summary>[Public] One published job.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<JobDto>>> Get(Guid id, CancellationToken ct) =>
        Ok(Envelope(await jobs.GetAsync(ViewerId, id, ct)));

    /// <summary>[Bearer Token — Organization, approved] Publishes a job.</summary>
    [HttpPost]
    [Authorize(Policy = "JobPoster")]
    public async Task<ActionResult<ApiResponse<JobDto>>> Create([FromBody] CreateJobRequest request, CancellationToken ct) =>
        CreatedEnvelope(await jobs.CreateAsync(CurrentUserId, request, ct));

    /// <summary>[Bearer Token — Organization] The organization's own jobs with application counts.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = "JobPoster")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JobDto>>>> Mine(CancellationToken ct) =>
        Ok(Envelope(await jobs.MineAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Organization owner or Admin] Closes a job.</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "OrganizationOrAdmin")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct) { await jobs.CloseAsync(CurrentUserId, CurrentRole == "admin", id, ct); return NoContent(); }

    /// <summary>[Bearer Token — Disabled user] Applies to a job.</summary>
    [HttpPost("{id:guid}/apply")]
    [Authorize(Policy = "DisabledUser")]
    public async Task<IActionResult> Apply(Guid id, [FromBody] ApplyRequest request, CancellationToken ct) { await jobs.ApplyAsync(CurrentUserId, id, request, ct); return NoContent(); }

    /// <summary>[Bearer Token — Disabled user] The caller's applications.</summary>
    [HttpGet("applications/mine")]
    [Authorize(Policy = "DisabledUser")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApplicationDto>>>> MyApplications(CancellationToken ct) =>
        Ok(Envelope(await jobs.MyApplicationsAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Organization owner] Applications of a job.</summary>
    [HttpGet("{id:guid}/applications")]
    [Authorize(Policy = "JobPoster")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApplicationDto>>>> Applications(Guid id, CancellationToken ct) =>
        Ok(Envelope(await jobs.JobApplicationsAsync(CurrentUserId, id, ct)));

    /// <summary>[Bearer Token — Organization owner] Moves an application (reviewing|interview|accepted|rejected).</summary>
    [HttpPut("applications/{applicationId:guid}/status")]
    [Authorize(Policy = "JobPoster")]
    public async Task<IActionResult> ChangeStatus(Guid applicationId, [FromBody] ChangeApplicationStatusRequest request, CancellationToken ct) { await jobs.ChangeStatusAsync(CurrentUserId, applicationId, request.Status, ct); return NoContent(); }
}
