namespace HamzetWasl.Application.Contracts.Volunteer;

public sealed class UpdateVolunteerProfileRequest
{
    public string? Introduction { get; set; }
    public string HelpMode { get; set; } = "online_only";
    public string SkillLevel { get; set; } = "beginner";
    public int WeeklyHours { get; set; }
    public string? ExperienceNotes { get; set; }
    public IReadOnlyList<int> SkillIds { get; set; } = Array.Empty<int>();
}

public sealed record VolunteerProfileResponse(
    Guid UserId,
    string? Introduction,
    string HelpMode,
    string ServiceArea,
    string? Governorate,
    decimal? Latitude,
    decimal? Longitude,
    int? ServiceRadiusKm,
    string SkillLevel,
    string? ExperienceNotes,
    int WeeklyHours,
    string VerificationStatus,
    IReadOnlyList<LookupValueDto> Skills);

public sealed class VolunteerLocationRequest
{
    public string Governorate { get; set; } = string.Empty;
    public string ServiceArea { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? ServiceRadiusKm { get; set; }
}

public sealed record VolunteerLocationResponse(
    string Governorate,
    string ServiceArea,
    decimal? Latitude,
    decimal? Longitude,
    int? ServiceRadiusKm);

public sealed record LookupValueDto(int Id, string Name);

public sealed record VerificationStepDto(
    string Key,
    string TitleAr,
    bool Completed,
    bool Required);

public sealed record VerificationResponse(
    string Status,
    string CurrentStep,
    Guid? ReviewId,
    DateTime? InterviewAt,
    bool CanSubmit,
    bool CanBookInterview,
    IReadOnlyList<VerificationStepDto> Steps,
    IReadOnlyList<string> UploadedEvidenceTypes,
    IReadOnlyList<SocialLinkDto> SocialLinks);

public sealed class SocialLinksRequest
{
    public IReadOnlyList<SocialLinkInput> Links { get; set; } = Array.Empty<SocialLinkInput>();
}

public sealed class SocialLinkInput
{
    public string Platform { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed record SocialLinkDto(string Platform, string Url);

public sealed class BookInterviewRequest
{
    public Guid InterviewSlotId { get; set; }
}

public sealed record InterviewSlotDto(
    Guid InterviewSlotId,
    DateTime StartsAt,
    int DurationMinutes,
    string Status);

public sealed record AvailabilitySlotDto(
    Guid SlotId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Timezone);

public sealed class SetAvailabilityRequest
{
    public IReadOnlyList<AvailabilityInput> Slots { get; set; } = Array.Empty<AvailabilityInput>();
}

public sealed class AvailabilityInput
{
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Timezone { get; set; } = "Africa/Cairo";
}

public sealed record TrainingModuleDto(
    Guid ModuleId,
    string Title,
    string Version,
    IReadOnlyList<TrainingAttemptDto> Attempts);

public sealed record TrainingAttemptDto(
    Guid AttemptId,
    decimal? Score,
    string? Result,
    DateTime? CompletedAt);

public sealed class SubmitTrainingAttemptRequest
{
    public decimal Score { get; set; }
}
