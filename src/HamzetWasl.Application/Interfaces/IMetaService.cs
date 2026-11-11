using HamzetWasl.Application.Contracts.Meta;

namespace HamzetWasl.Application.Interfaces;

public interface IMetaService
{
    IReadOnlyList<AccountTypeDto> GetAccountTypes();
    Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AreaDto>> GetAreasAsync(string governorateKey, CancellationToken ct = default);
    IReadOnlyList<BusinessFieldDto> GetBusinessFields();
    IReadOnlyList<EmployeeRangeDto> GetEmployeeRanges();
    Task<IReadOnlyList<LookupDto>> GetSkillsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LookupDto>> GetInterestsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NeedTypeDto>> GetNeedTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AccommodationDto>> GetAccommodationsAsync(CancellationToken ct = default);
}
