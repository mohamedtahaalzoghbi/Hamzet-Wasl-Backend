using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Shops;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/shops")]
public sealed class ShopsController(IShopService shops) : ApiControllerBase
{
    /// <summary>[Public] Approved shops. Filters: q, governorate.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResult<ShopSummaryDto>>>> Search(
        [FromQuery] string? q, [FromQuery] string? governorate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(Envelope(await shops.SearchAsync(q, governorate, page, pageSize, ct)));

    /// <summary>[Public] One approved shop with its available products and contact details.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ShopDetailDto>>> Get(Guid id, CancellationToken ct) =>
        Ok(Envelope(await shops.GetAsync(id, ct)));

    /// <summary>[Bearer Token — Shop] The shop's own products, including unavailable ones.</summary>
    [HttpGet("mine/products")]
    [Authorize(Policy = "Shop")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProductDto>>>> Mine(CancellationToken ct) =>
        Ok(Envelope(await shops.MyProductsAsync(CurrentUserId, ct)));

    /// <summary>[Bearer Token — Shop, approved] Adds a product.</summary>
    [HttpPost("products")]
    [Authorize(Policy = "Shop")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create([FromBody] ProductRequest request, CancellationToken ct) =>
        CreatedEnvelope(await shops.CreateProductAsync(CurrentUserId, request, ct));

    /// <summary>[Bearer Token — Shop, approved] Edits a product.</summary>
    [HttpPut("products/{id:guid}")]
    [Authorize(Policy = "Shop")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Update(Guid id, [FromBody] ProductRequest request, CancellationToken ct) =>
        Ok(Envelope(await shops.UpdateProductAsync(CurrentUserId, id, request, ct)));

    /// <summary>[Bearer Token — Shop, approved] Deletes a product.</summary>
    [HttpDelete("products/{id:guid}")]
    [Authorize(Policy = "Shop")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await shops.DeleteProductAsync(CurrentUserId, id, ct); return NoContent(); }
}
