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
    [ProducesResponseType(typeof(ApiResponse<OrganizationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Get(CancellationToken ct) =>
        Ok(Envelope(await _organizations.GetAsync(CurrentUserId, ct)));

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<OrganizationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Update(
        [FromBody] UpdateOrganizationRequest request, CancellationToken ct) =>
        Ok(Envelope(await _organizations.UpdateAsync(CurrentUserId, request, ct)));

    [HttpPut("logo")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationLogoResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<OrganizationLogoResponse>>> UpdateLogo(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await _organizations.UpdateLogoAsync(CurrentUserId, upload, ct);
        return Ok(Envelope(new OrganizationLogoResponse(url)));
    }

    [HttpPut("cover")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationCoverResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<OrganizationCoverResponse>>> UpdateCover(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        var url = await _organizations.UpdateCoverAsync(CurrentUserId, upload, ct);
        return Ok(Envelope(new OrganizationCoverResponse(url)));
    }

    [HttpPut("documents/{documentType}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationResponse>), StatusCodes.Status200OK)]
    [EnableRateLimiting("uploads")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> UploadDocument(
        string documentType, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var upload = new FileUpload(stream, file.FileName, file.ContentType, file.Length);
        return Ok(Envelope(await _organizations.UploadDocumentAsync(CurrentUserId, documentType, upload, ct)));
    }

    [HttpPost("submit")]
    [ProducesResponseType(typeof(ApiResponse<SubmitOrganizationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<SubmitOrganizationResponse>>> Submit(CancellationToken ct) =>
        Ok(Envelope(await _organizations.SubmitAsync(CurrentUserId, ct)));

    [HttpGet("review-status")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationReviewStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<OrganizationReviewStatusResponse>>> GetReviewStatus(CancellationToken ct) =>
        Ok(Envelope(await _organizations.GetReviewStatusAsync(CurrentUserId, ct)));
}
