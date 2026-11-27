using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Help;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Authorize]
[Route("api/v1/help")]
public sealed class HelpController(IHelpService help) : ApiControllerBase
{
    private string Role => CurrentRole ?? "";

    /// <summary>[Bearer Token — Disabled user] Creates a help request (helpMode online|field, category reading|accompaniment|explanation|other, approximateArea, urgency normal|urgent).</summary>
    [HttpPost("requests")]
    [Authorize(Policy = "DisabledUser")]
    public async Task<ActionResult<ApiResponse<HelpRequestDto>>> Create([FromBody] CreateHelpRequestRequest request, CancellationToken ct) =>
        CreatedEnvelope(await help.CreateRequestAsync(CurrentUserId, request, ct));

    /// <summary>[Bearer Token — Disabled user or Volunteer] The caller's own requests (disabled user) or the requests the volunteer offered on.</summary>
    [HttpGet("requests/mine")]
    [Authorize(Policy = "DisabledUserOrVolunteer")]
    public async Task<ActionResult<ApiResponse<PagedResult<HelpRequestDto>>>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await help.GetMineAsync(CurrentUserId, Role, page, pageSize, ct)));

    /// <summary>[Bearer Token — Approved volunteer] Open requests a volunteer can offer on.</summary>
    [HttpGet("requests/open")]
    [Authorize(Policy = "Volunteer")]
    public async Task<ActionResult<ApiResponse<PagedResult<HelpRequestDto>>>> Open([FromQuery] string? mode, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await help.GetOpenAsync(CurrentUserId, mode, page, pageSize, ct)));

    /// <summary>[Bearer Token] Request detail with offers (requester sees all, a volunteer sees only their own).</summary>
    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult<ApiResponse<HelpRequestDetailDto>>> Detail(Guid id, CancellationToken ct) =>
        Ok(Envelope(await help.GetDetailAsync(CurrentUserId, Role, id, ct)));

    /// <summary>[Bearer Token — Approved volunteer] Sends an offer on an open request.</summary>
    [HttpPost("requests/{id:guid}/offers")]
    [Authorize(Policy = "Volunteer")]
    public async Task<ActionResult<ApiResponse<HelpOfferDto>>> Offer(Guid id, [FromBody] CreateHelpOfferRequest request, CancellationToken ct) =>
        CreatedEnvelope(await help.OfferAsync(CurrentUserId, id, request, ct));

    /// <summary>[Bearer Token — Requester] Accepts exactly one offer; the others are rejected and the chat opens.</summary>
    [HttpPost("requests/{id:guid}/accept")]
    [Authorize(Policy = "DisabledUser")]
    public async Task<ActionResult<ApiResponse<HelpRequestDetailDto>>> Accept(Guid id, [FromBody] AcceptHelpOfferRequest request, CancellationToken ct) =>
        Ok(Envelope(await help.AcceptOfferAsync(CurrentUserId, id, request.OfferId, ct)));

    /// <summary>[Bearer Token — Requester] Cancels an open or accepted request.</summary>
    [HttpPost("requests/{id:guid}/cancel")]
    [Authorize(Policy = "DisabledUser")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await help.CancelAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token — Requester] Marks an accepted request as completed.</summary>
    [HttpPost("requests/{id:guid}/complete")]
    [Authorize(Policy = "DisabledUser")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct) { await help.CompleteAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token — Requester or assigned volunteer] Chat messages of an accepted request.</summary>
    [HttpGet("requests/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HelpMessageDto>>>> Messages(Guid id, CancellationToken ct) =>
        Ok(Envelope(await help.GetMessagesAsync(CurrentUserId, id, ct)));

    /// <summary>[Bearer Token — Requester or assigned volunteer] Sends a text message.</summary>
    [HttpPost("requests/{id:guid}/messages")]
    public async Task<ActionResult<ApiResponse<HelpMessageDto>>> Send(Guid id, [FromBody] SendHelpMessageRequest request, CancellationToken ct) =>
        CreatedEnvelope(await help.SendMessageAsync(CurrentUserId, id, request.Text, ct));

    /// <summary>[Bearer Token — Requester or assigned volunteer] Rates the other party (1-5) after completion.</summary>
    [HttpPost("requests/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, [FromBody] HelpFeedbackRequest request, CancellationToken ct) { await help.FeedbackAsync(CurrentUserId, id, request, ct); return NoContent(); }

    /// <summary>[Bearer Token — Requester or assigned volunteer] Files a safety report about the other party.</summary>
    [HttpPost("requests/{id:guid}/safety-report")]
    public async Task<IActionResult> Report(Guid id, [FromBody] HelpSafetyReportRequest request, CancellationToken ct) { await help.ReportAsync(CurrentUserId, id, request, ct); return NoContent(); }
}
