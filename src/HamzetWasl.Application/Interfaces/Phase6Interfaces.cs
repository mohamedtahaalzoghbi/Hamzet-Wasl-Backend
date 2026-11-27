using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;

namespace HamzetWasl.Application.Interfaces;

public interface IDirectoryService
{
    Task<PagedResult<DirectoryItemDto>> SearchAsync(string? type, string? q, int page, int pageSize, CancellationToken ct = default);
    Task<DirectoryItemDto> GetAsync(Guid itemId, CancellationToken ct = default);
    Task<IReadOnlyList<DirectoryItemDto>> MineAsync(Guid ownerUserId, CancellationToken ct = default);
    Task<DirectoryItemDto> CreateAsync(Guid ownerUserId, DirectoryItemRequest r, CancellationToken ct = default);
    Task<DirectoryItemDto> SetStatusAsync(Guid ownerUserId, Guid itemId, string status, CancellationToken ct = default);
    Task<InquiryDto> StartInquiryAsync(Guid userId, Guid itemId, InquiryRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<InquiryDto>> InquiriesAsync(Guid userId, bool received, CancellationToken ct = default);
    Task<InquiryDto> GetInquiryAsync(Guid userId, Guid inquiryId, CancellationToken ct = default);
    Task<InquiryDto> ReplyAsync(Guid userId, Guid inquiryId, string body, CancellationToken ct = default);
}

public interface IPlaceService
{
    Task<IReadOnlyList<string>> FacilityTypesAsync(CancellationToken ct = default);
    Task<PagedResult<PlaceDto>> SearchAsync(string? q, string? facility, int page, int pageSize, CancellationToken ct = default);
    Task<PlaceDto> GetAsync(Guid placeId, CancellationToken ct = default);
    Task<PlaceDto> CreateAsync(PlaceRequest r, CancellationToken ct = default);
    Task<PlaceDto> ReviewAsync(Guid userId, Guid placeId, PlaceReviewRequest r, CancellationToken ct = default);
    Task<ObstacleDto> ReportObstacleAsync(Guid userId, ObstacleRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<ObstacleDto>> ObstaclesAsync(bool all, CancellationToken ct = default);
    Task<ObstacleDto> SetObstacleStatusAsync(Guid obstacleId, string status, CancellationToken ct = default);
}

public interface ISosService
{
    Task<IReadOnlyList<EmergencyContactDto>> ContactsAsync(Guid userId, CancellationToken ct = default);
    Task<EmergencyContactDto> AddContactAsync(Guid userId, EmergencyContactRequest r, CancellationToken ct = default);
    Task DeleteContactAsync(Guid userId, Guid contactId, CancellationToken ct = default);
    Task<SosAlertDto> TriggerAsync(Guid userId, SosRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<SosAlertDto>> MyAlertsAsync(Guid userId, CancellationToken ct = default);
}

public interface IStatsService
{
    Task<PublicStatsDto> PublicAsync(CancellationToken ct = default);
    Task<AdminStatsDto> AdminAsync(CancellationToken ct = default);
}
