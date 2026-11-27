namespace HamzetWasl.Application.Contracts.Shops;

public sealed record ProductRequest(string Name, string? Description, decimal? Price, bool Available);

public sealed record ProductDto(Guid ProductId, string Name, string? Description, decimal? Price, bool Available, DateTime CreatedAt);

public sealed record ShopSummaryDto(
    Guid OrganizationId, string Name, string? Field, string? Description, string? Governorate, string? City, int ProductsCount);

public sealed record ShopDetailDto(
    Guid OrganizationId, string Name, string? Field, string? Description, string? Governorate, string? City,
    string? Website, string? ContactInformation, IReadOnlyList<ProductDto> Products);
