using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/directory")]
public sealed class DirectoryController(IDirectoryService directory) : ApiControllerBase
{
    /// <summary>[Public] Published services, programs and events of approved associations. Filters: type, q.</summary>
    [HttpGet("items")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<DirectoryItemDto>>>> Search([FromQuery] string? type, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await directory.SearchAsync(type, q, page, pageSize, ct)));

    [HttpGet("items/{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DirectoryItemDto>>> Get(Guid id, CancellationToken ct) => Ok(Envelope(await directory.GetAsync(id, ct)));

    /// <summary>[Bearer Token — Association] Own items including drafts.</summary>
    [HttpGet("items/mine")]
    [Authorize(Policy = "Association")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DirectoryItemDto>>>> Mine(CancellationToken ct) => Ok(Envelope(await directory.MineAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Association, approved] Creates a service, program or event.</summary>
    [HttpPost("items")]
    [Authorize(Policy = "Association")]
    public async Task<ActionResult<ApiResponse<DirectoryItemDto>>> Create([FromBody] DirectoryItemRequest request, CancellationToken ct) =>
        CreatedEnvelope(await directory.CreateAsync(CurrentUserId, request, ct));

    [HttpPut("items/{id:guid}/status")]
    [Authorize(Policy = "Association")]
    public async Task<ActionResult<ApiResponse<DirectoryItemDto>>> SetStatus(Guid id, [FromBody] DirectoryStatusRequest request, CancellationToken ct) =>
        Ok(Envelope(await directory.SetStatusAsync(CurrentUserId, id, request.Status, ct)));

    /// <summary>[Bearer Token] Starts an inquiry on a published item.</summary>
    [HttpPost("items/{id:guid}/inquiries")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<InquiryDto>>> Start(Guid id, [FromBody] InquiryRequest request, CancellationToken ct) =>
        CreatedEnvelope(await directory.StartInquiryAsync(CurrentUserId, id, request, ct));

    /// <summary>[Bearer Token] My sent inquiries (received=false) or inquiries on my items (received=true).</summary>
    [HttpGet("inquiries")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InquiryDto>>>> List([FromQuery] bool received = false, CancellationToken ct = default) =>
        Ok(Envelope(await directory.InquiriesAsync(CurrentUserId, received, ct)));

    [HttpGet("inquiries/{id:guid}")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<InquiryDto>>> GetInquiry(Guid id, CancellationToken ct) => Ok(Envelope(await directory.GetInquiryAsync(CurrentUserId, id, ct)));

    [HttpPost("inquiries/{id:guid}/messages")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<InquiryDto>>> Reply(Guid id, [FromBody] InquiryMessageRequest request, CancellationToken ct) =>
        Ok(Envelope(await directory.ReplyAsync(CurrentUserId, id, request.Body, ct)));
}
