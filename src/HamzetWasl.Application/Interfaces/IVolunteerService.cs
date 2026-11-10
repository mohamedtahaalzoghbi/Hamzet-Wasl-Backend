using HamzetWasl.Application.Contracts.Volunteer;

namespace HamzetWasl.Application.Interfaces;

public interface IVolunteerService
{
    Task<VolunteerProfileResponse> GetProfileAsync(Guid userId, CancellationToken ct = default);
    Task<VolunteerProfileResponse> UpdateProfileAsync(Guid userId, UpdateVolunteerProfileRequest request, CancellationToken ct = default);
    Task<VolunteerLocationResponse> GetLocationAsync(Guid userId, CancellationToken ct = default);
    Task<VolunteerLocationResponse> UpdateLocationAsync(Guid userId, VolunteerLocationRequest request, CancellationToken ct = default);
    Task<VerificationResponse> GetVerificationAsync(Guid userId, CancellationToken ct = default);
    Task UploadEvidenceAsync(Guid userId, string evidenceType, Common.FileUpload file, CancellationToken ct = default);
    Task SetSocialLinksAsync(Guid userId, IReadOnlyList<SocialLinkInput> links, CancellationToken ct = default);
    Task<IReadOnlyList<InterviewSlotDto>> GetInterviewSlotsAsync(DateOnly? date, CancellationToken ct = default);
    Task<VerificationResponse> BookInterviewAsync(Guid userId, Guid slotId, CancellationToken ct = default);
    Task<VerificationResponse> ChangeInterviewAsync(Guid userId, Guid slotId, CancellationToken ct = default);
    Task<VerificationResponse> CancelInterviewAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailabilityAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<AvailabilitySlotDto>> SetAvailabilityAsync(Guid userId, IReadOnlyList<AvailabilityInput> slots, CancellationToken ct = default);
    Task<IReadOnlyList<TrainingModuleDto>> GetTrainingAsync(Guid userId, CancellationToken ct = default);
    Task<TrainingAttemptDto> SubmitTrainingAttemptAsync(Guid userId, Guid moduleId, decimal score, CancellationToken ct = default);
}
