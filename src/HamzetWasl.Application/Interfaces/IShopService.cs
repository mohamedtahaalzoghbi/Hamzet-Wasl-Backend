using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Shops;

namespace HamzetWasl.Application.Interfaces;

public interface IShopService
{
    Task<PagedResult<ShopSummaryDto>> SearchAsync(string? q, string? governorate, int page, int pageSize, CancellationToken ct = default);
    Task<ShopDetailDto> GetAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> MyProductsAsync(Guid ownerUserId, CancellationToken ct = default);
    Task<ProductDto> CreateProductAsync(Guid ownerUserId, ProductRequest r, CancellationToken ct = default);
    Task<ProductDto> UpdateProductAsync(Guid ownerUserId, Guid productId, ProductRequest r, CancellationToken ct = default);
    Task DeleteProductAsync(Guid ownerUserId, Guid productId, CancellationToken ct = default);
}
