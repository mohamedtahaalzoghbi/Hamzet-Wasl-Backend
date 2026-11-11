using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Application.Metadata;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class MetaService : IMetaService
{
    private readonly HamzetWaslDbContext _db;
    private readonly IGeoCatalog _geo;

    public MetaService(HamzetWaslDbContext db, IGeoCatalog geo)
    {
        _db = db;
        _geo = geo;
    }

    public IReadOnlyList<AccountTypeDto> GetAccountTypes() => MetaCatalog.AccountTypes;

    public async Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default) =>
        await _geo.GetGovernoratesAsync(ct);

    public async Task<IReadOnlyList<AreaDto>> GetAreasAsync(string governorateKey, CancellationToken ct = default)
    {
        var governorate = await _geo.FindGovernorateAsync(governorateKey, ct)
                          ?? throw AppException.NotFound("المحافظة غير موجودة.");
        return governorate.Areas;
    }

    public IReadOnlyList<BusinessFieldDto> GetBusinessFields() => MetaCatalog.BusinessFields;
    public IReadOnlyList<EmployeeRangeDto> GetEmployeeRanges() => MetaCatalog.EmployeeRanges;

    public async Task<IReadOnlyList<LookupDto>> GetSkillsAsync(CancellationToken ct = default) =>
        await _db.Skills.OrderBy(s => s.Name)
            .Select(s => new LookupDto(s.SkillId, s.Name, s.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<LookupDto>> GetInterestsAsync(CancellationToken ct = default) =>
        await _db.Interests.OrderBy(i => i.Name)
            .Select(i => new LookupDto(i.InterestId, i.Name, i.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NeedTypeDto>> GetNeedTypesAsync(CancellationToken ct = default)
    {
        var rows = await _db.NeedTypes.OrderBy(n => n.NeedTypeId).ToListAsync(ct);
        return rows.Select(n =>
        {
            var meta = MetaCatalog.NeedTypeCatalog.FirstOrDefault(m => m.Key == n.Name);
            return new NeedTypeDto(n.NeedTypeId, n.Name, meta.NameAr ?? n.Name, meta.DescriptionAr ?? string.Empty);
        }).ToList();
    }

    public async Task<IReadOnlyList<AccommodationDto>> GetAccommodationsAsync(CancellationToken ct = default) =>
        await _db.Accommodations.OrderBy(a => a.Name)
            .Select(a => new AccommodationDto(a.AccommodationId, a.Name, a.Name, a.Category))
            .ToListAsync(ct);
}
