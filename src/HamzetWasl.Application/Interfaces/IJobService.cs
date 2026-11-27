using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Jobs;

namespace HamzetWasl.Application.Interfaces;

public interface IJobService
{
    Task<PagedResult<JobDto>> SearchAsync(Guid? viewerId, string? q, string? employmentType, string? workArrangement, int page, int pageSize, CancellationToken ct = default);
    Task<JobDto> GetAsync(Guid? viewerId, Guid jobId, CancellationToken ct = default);
    Task<JobDto> CreateAsync(Guid ownerUserId, CreateJobRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<JobDto>> MineAsync(Guid ownerUserId, CancellationToken ct = default);
    Task CloseAsync(Guid userId, bool isAdmin, Guid jobId, CancellationToken ct = default);
    Task ApplyAsync(Guid userId, Guid jobId, ApplyRequest r, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationDto>> MyApplicationsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationDto>> JobApplicationsAsync(Guid ownerUserId, Guid jobId, CancellationToken ct = default);
    Task ChangeStatusAsync(Guid ownerUserId, Guid applicationId, string status, CancellationToken ct = default);
}
