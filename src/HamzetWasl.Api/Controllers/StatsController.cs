using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/stats")]
public sealed class StatsController(IStatsService stats) : ApiControllerBase
{
    /// <summary>[Public] Platform totals for the website.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PublicStatsDto>>> Public(CancellationToken ct) => Ok(Envelope(await stats.PublicAsync(ct)));

    /// <summary>[Bearer Token — Admin] Totals and open work queues.</summary>
    [HttpGet("admin")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApiResponse<AdminStatsDto>>> Admin(CancellationToken ct) => Ok(Envelope(await stats.AdminAsync(ct)));
}
