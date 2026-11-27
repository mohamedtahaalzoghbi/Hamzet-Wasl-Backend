using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Shops;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>Shops list products and contact details only. There is no ordering or payment.</summary>
public sealed class ShopService(HamzetWaslDbContext db, IClock clock) : IShopService
{
    private static AppException Invalid(string field, string message) =>
        AppException.Validation(new Dictionary<string, string[]> { [field] = [message] });

    private IQueryable<Organization> Live() =>
        db.Organizations.Where(o => o.OrganizationType == OrganizationTypes.Shop && o.ApprovalStatus == "approved");

    private async Task<Organization> MyApprovedShopAsync(Guid ownerUserId, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId && o.OrganizationType == OrganizationTypes.Shop, ct)
            ?? throw AppException.Forbidden();
        if (org.ApprovalStatus != "approved") throw AppException.Forbidden(message: "لازم المحل يتعتمد الأول عشان تضيف منتجات.");
        return org;
    }

    private static ProductDto ToDto(ShopProduct p) => new(p.ProductId, p.Name, p.Description, p.Price, p.Available, p.CreatedAt);

    private static void Validate(ProductRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) throw Invalid("name", "اسم المنتج مطلوب (حتى 200 حرف).");
        if (r.Price is < 0) throw Invalid("price", "السعر لازم يكون صفر أو أكتر.");
    }

    public async Task<PagedResult<ShopSummaryDto>> SearchAsync(string? q, string? governorate, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Live();
        if (!string.IsNullOrWhiteSpace(q)) { var t = q.Trim(); query = query.Where(o => o.Name.Contains(t) || (o.Description != null && o.Description.Contains(t)) || (o.Field != null && o.Field.Contains(t))); }
        if (!string.IsNullOrWhiteSpace(governorate)) query = query.Where(o => o.Governorate == governorate);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(o => o.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = rows.Select(o => o.OrganizationId).ToList();
        var counts = await db.ShopProducts.Where(p => ids.Contains(p.OrganizationId) && p.Available)
            .GroupBy(p => p.OrganizationId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var items = rows.Select(o => new ShopSummaryDto(o.OrganizationId, o.Name, o.Field, o.Description, o.Governorate, o.City, counts.GetValueOrDefault(o.OrganizationId))).ToList();
        return new PagedResult<ShopSummaryDto>(items, page, pageSize, total);
    }

    public async Task<ShopDetailDto> GetAsync(Guid organizationId, CancellationToken ct = default)
    {
        var o = await Live().FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct) ?? throw AppException.NotFound();
        var products = await db.ShopProducts.Where(p => p.OrganizationId == organizationId && p.Available).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return new ShopDetailDto(o.OrganizationId, o.Name, o.Field, o.Description, o.Governorate, o.City, o.Website, o.ContactInformation, products.Select(ToDto).ToList());
    }

    public async Task<IReadOnlyList<ProductDto>> MyProductsAsync(Guid ownerUserId, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId && o.OrganizationType == OrganizationTypes.Shop, ct) ?? throw AppException.Forbidden();
        var rows = await db.ShopProducts.Where(p => p.OrganizationId == org.OrganizationId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(Guid ownerUserId, ProductRequest r, CancellationToken ct = default)
    {
        var org = await MyApprovedShopAsync(ownerUserId, ct);
        Validate(r);
        var p = new ShopProduct
        {
            ProductId = Guid.NewGuid(), OrganizationId = org.OrganizationId, Name = r.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Price = r.Price, Available = r.Available, CreatedAt = clock.UtcNow
        };
        db.ShopProducts.Add(p);
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid ownerUserId, Guid productId, ProductRequest r, CancellationToken ct = default)
    {
        var org = await MyApprovedShopAsync(ownerUserId, ct);
        Validate(r);
        var p = await db.ShopProducts.FirstOrDefaultAsync(x => x.ProductId == productId && x.OrganizationId == org.OrganizationId, ct) ?? throw AppException.NotFound();
        p.Name = r.Name.Trim();
        p.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        p.Price = r.Price; p.Available = r.Available;
        await db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task DeleteProductAsync(Guid ownerUserId, Guid productId, CancellationToken ct = default)
    {
        var org = await MyApprovedShopAsync(ownerUserId, ct);
        var p = await db.ShopProducts.FirstOrDefaultAsync(x => x.ProductId == productId && x.OrganizationId == org.OrganizationId, ct) ?? throw AppException.NotFound();
        db.ShopProducts.Remove(p);
        await db.SaveChangesAsync(ct);
    }
}
