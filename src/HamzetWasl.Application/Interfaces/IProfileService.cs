using HamzetWasl.Application.Contracts.Profile;

namespace HamzetWasl.Application.Interfaces;

public interface IProfileService
{
    Task<ProfileResponse> GetAsync(Guid userId, CancellationToken ct = default);
    Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task SetInterestsAsync(Guid userId, IReadOnlyList<int> interestIds, CancellationToken ct = default);
    Task SetSkillsAsync(Guid userId, IReadOnlyList<int> skillIds, CancellationToken ct = default);
    Task<string> UpdateAvatarAsync(Guid userId, Common.FileUpload file, CancellationToken ct = default);
}

public interface IAccessibilityService
{
    Task<AccessibilityResponse> GetAsync(Guid userId, CancellationToken ct = default);
    Task<AccessibilityResponse> UpsertAsync(Guid userId, UpsertAccessibilityRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, CancellationToken ct = default);
}
