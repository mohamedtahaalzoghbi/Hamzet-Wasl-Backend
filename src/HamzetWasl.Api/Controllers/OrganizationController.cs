using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HamzetWasl.Api.Controllers;

[Authorize(Policy = "Organization")]
[Route("api/v1/me/organization")]
public sealed class OrganizationController : ApiControllerBase
{
    private readonly IOrganizationService _organizations;

    public OrganizationController(IOrganizationService organizations) => _organizations = organizations;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Get(CancellationToken ct) =>
        Ok(Envelope(await _organizations.GetAsync(CurrentUserId, ct)));

    [HttpPut]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Update(
        [FromBody] UpdateOrganizationRequest request, CancellationToken ct) =>
        Ok(Envelope(await _organizations.UpdateAsync(CurrentUserId, request, ct)));

    [HttpPut("logo")]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateLogo(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await _organizations.UpdateLogoAsync(CurrentUserId, upload, ct);
        return Ok(Envelope<object>(new { logoUrl = url }));
    }

    [HttpPut("cover")]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCover(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await _organizations.UpdateCoverAsync(CurrentUserId, upload, ct);
        return Ok(Envelope<object>(new { coverUrl = url }));
    }

    [HttpPut("documents/{documentType}")]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> UploadDocument(
        string documentType, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        return Ok(Envelope(await _organizations.UploadDocumentAsync(CurrentUserId, documentType, upload, ct)));
    }

    [HttpPost("submit")]
    public async Task<ActionResult<ApiResponse<SubmitOrganizationResponse>>> Submit(CancellationToken ct) =>
        Ok(Envelope(await _organizations.SubmitAsync(CurrentUserId, ct)));

    [HttpGet("review-status")]
    public async Task<ActionResult<ApiResponse<OrganizationReviewStatusResponse>>> GetReviewStatus(CancellationToken ct) =>
        Ok(Envelope(await _organizations.GetReviewStatusAsync(CurrentUserId, ct)));
}
