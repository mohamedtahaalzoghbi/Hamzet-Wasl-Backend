using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Authorize(Policy = "DisabledUser")]
[Route("api/v1/me/accessibility")]
public sealed class AccessibilityController : ApiControllerBase
{
    private readonly IAccessibilityService _accessibility;

    public AccessibilityController(IAccessibilityService accessibility) => _accessibility = accessibility;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AccessibilityResponse>>> Get(CancellationToken ct) =>
        Ok(Envelope(await _accessibility.GetAsync(CurrentUserId, ct)));

    [HttpPut]
    public async Task<ActionResult<ApiResponse<AccessibilityResponse>>> Upsert(
        [FromBody] UpsertAccessibilityRequest request, CancellationToken ct) =>
        Ok(Envelope(await _accessibility.UpsertAsync(CurrentUserId, request, ct)));

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        await _accessibility.DeleteAsync(CurrentUserId, ct);
        return NoContent();
    }
}
