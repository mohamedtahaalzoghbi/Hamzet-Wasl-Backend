using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Admin;

namespace HamzetWasl.Application.Interfaces;

public interface IAdminService
{
    Task<PagedResult<VolunteerReviewListItemDto>> GetVolunteerReviewsAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<VolunteerReviewDetailDto> GetVolunteerReviewAsync(Guid reviewId, CancellationToken ct = default);
    Task DecideVolunteerReviewAsync(Guid reviewId, Guid reviewerId, DecisionRequest request, CancellationToken ct = default);
    Task<PagedResult<OrganizationReviewListItemDto>> GetOrganizationReviewsAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<OrganizationReviewDetailDto> GetOrganizationReviewAsync(Guid reviewId, CancellationToken ct = default);
    Task DecideOrganizationReviewAsync(Guid reviewId, Guid reviewerId, DecisionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<InterviewSlotAdminDto>> GetInterviewSlotsAsync(CancellationToken ct = default);
    Task<InterviewSlotAdminDto> CreateInterviewSlotAsync(Guid reviewerId, CreateInterviewSlotRequest request, CancellationToken ct = default);
    Task DeleteInterviewSlotAsync(Guid reviewerId, Guid slotId, CancellationToken ct = default);
    Task SetUserSuspensionAsync(Guid adminId, Guid userId, SuspendUserRequest request, CancellationToken ct = default);
    Task<PagedResult<DeletionRequestDto>> GetDeletionRequestsAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task ResolveDeletionRequestAsync(Guid adminId, Guid deletionRequestId, ResolveDeletionRequestRequest request, CancellationToken ct = default);
    Task<PagedResult<AdminAuditDto>> GetAuditAsync(int page, int pageSize, CancellationToken ct = default);
}
