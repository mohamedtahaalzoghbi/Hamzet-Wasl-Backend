namespace HamzetWasl.Application.Contracts.Meta;

public sealed record AccountTypeDto(
    string Key,
    string NameAr,
    string DescriptionAr);

public sealed record LookupDto(
    int Id,
    string Key,
    string NameAr);

public sealed record NeedTypeDto(
    int Id,
    string Key,
    string NameAr,
    string DescriptionAr);

public sealed record BusinessFieldDto(
    string Key,
    string NameAr);

public sealed record EmployeeRangeDto(
    string Key,
    string NameAr);

public sealed record AreaDto(
    string Key,
    string NameAr);

public sealed record GovernorateDto(
    string Key,
    string NameAr,
    IReadOnlyList<AreaDto> Areas);

public sealed record AccommodationDto(
    int Id,
    string Key,
    string NameAr,
    string Category);
