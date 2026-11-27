using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/places")]
public sealed class PlacesController(IPlaceService places) : ApiControllerBase
{
    [HttpGet("facility-types")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> Types(CancellationToken ct) => Ok(Envelope(await places.FacilityTypesAsync(ct)));

    /// <summary>[Public] Accessible places. Filters: q, facility (ramp, elevator, accessible_restroom, visual_alarm, sign_language).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<PlaceDto>>>> Search([FromQuery] string? q, [FromQuery] string? facility, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(Envelope(await places.SearchAsync(q, facility, page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PlaceDto>>> Get(Guid id, CancellationToken ct) => Ok(Envelope(await places.GetAsync(id, ct)));

    /// <summary>[Bearer Token — Admin] Adds a verified place.</summary>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApiResponse<PlaceDto>>> Create([FromBody] PlaceRequest request, CancellationToken ct) => CreatedEnvelope(await places.CreateAsync(request, ct));

    /// <summary>[Bearer Token] Adds or updates my review of a place.</summary>
    [HttpPut("{id:guid}/review")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<PlaceDto>>> Review(Guid id, [FromBody] PlaceReviewRequest request, CancellationToken ct) =>
        Ok(Envelope(await places.ReviewAsync(CurrentUserId, id, request, ct)));

    /// <summary>[Public] Open and verified obstacle reports.</summary>
    [HttpGet("obstacles")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ObstacleDto>>>> Obstacles(CancellationToken ct) => Ok(Envelope(await places.ObstaclesAsync(false, ct)));

    /// <summary>[Bearer Token — Admin] All obstacle reports.</summary>
    [HttpGet("obstacles/all")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ObstacleDto>>>> AllObstacles(CancellationToken ct) => Ok(Envelope(await places.ObstaclesAsync(true, ct)));

    [HttpPost("obstacles")]
    [Authorize(Policy = "NonAdmin")]
    public async Task<ActionResult<ApiResponse<ObstacleDto>>> Report([FromBody] ObstacleRequest request, CancellationToken ct) => CreatedEnvelope(await places.ReportObstacleAsync(CurrentUserId, request, ct));

    [HttpPut("obstacles/{id:guid}/status")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ApiResponse<ObstacleDto>>> SetStatus(Guid id, [FromBody] ObstacleStatusRequest request, CancellationToken ct) =>
        Ok(Envelope(await places.SetObstacleStatusAsync(id, request.Status, ct)));
}
