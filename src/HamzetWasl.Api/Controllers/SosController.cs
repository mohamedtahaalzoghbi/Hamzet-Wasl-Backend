using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/sos")]
[Authorize(Policy = "DisabledUserOrVolunteer")]
public sealed class SosController(ISosService sos) : ApiControllerBase
{
    [HttpGet("contacts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EmergencyContactDto>>>> Contacts(CancellationToken ct) => Ok(Envelope(await sos.ContactsAsync(CurrentUserId, ct)));

    [HttpPost("contacts")]
    public async Task<ActionResult<ApiResponse<EmergencyContactDto>>> AddContact([FromBody] EmergencyContactRequest request, CancellationToken ct) => CreatedEnvelope(await sos.AddContactAsync(CurrentUserId, request, ct));

    [HttpDelete("contacts/{id:guid}")]
    public async Task<IActionResult> DeleteContact(Guid id, CancellationToken ct) { await sos.DeleteContactAsync(CurrentUserId, id, ct); return NoContent(); }

    /// <summary>[Bearer Token] Sends an SOS alert with the location to the emergency contacts.</summary>
    [HttpPost("alerts")]
    public async Task<ActionResult<ApiResponse<SosAlertDto>>> Trigger([FromBody] SosRequest request, CancellationToken ct) => CreatedEnvelope(await sos.TriggerAsync(CurrentUserId, request, ct));

    [HttpGet("alerts/mine")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SosAlertDto>>>> Mine(CancellationToken ct) => Ok(Envelope(await sos.MyAlertsAsync(CurrentUserId, ct)));
}
