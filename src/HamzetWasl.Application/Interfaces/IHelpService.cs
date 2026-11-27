using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Help;

namespace HamzetWasl.Application.Interfaces;

/// <summary>Help requests: a person with a disability asks, verified volunteers offer, the requester accepts one, both chat, then rate.</summary>
public interface IHelpService
{
    Task<HelpRequestDto> CreateRequestAsync(Guid userId, CreateHelpRequestRequest request, CancellationToken ct = default);
    Task<PagedResult<HelpRequestDto>> GetMineAsync(Guid userId, string role, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<HelpRequestDto>> GetOpenAsync(Guid volunteerId, string? mode, int page, int pageSize, CancellationToken ct = default);
    Task<HelpRequestDetailDto> GetDetailAsync(Guid userId, string role, Guid requestId, CancellationToken ct = default);
    Task<HelpOfferDto> OfferAsync(Guid volunteerId, Guid requestId, CreateHelpOfferRequest request, CancellationToken ct = default);
    Task<HelpRequestDetailDto> AcceptOfferAsync(Guid requesterId, Guid requestId, Guid offerId, CancellationToken ct = default);
    Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken ct = default);
    Task CompleteAsync(Guid requesterId, Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<HelpMessageDto>> GetMessagesAsync(Guid userId, Guid requestId, CancellationToken ct = default);
    Task<HelpMessageDto> SendMessageAsync(Guid userId, Guid requestId, string text, CancellationToken ct = default);
    Task FeedbackAsync(Guid userId, Guid requestId, HelpFeedbackRequest request, CancellationToken ct = default);
    Task ReportAsync(Guid userId, Guid requestId, HelpSafetyReportRequest request, CancellationToken ct = default);
}
