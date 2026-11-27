namespace HamzetWasl.Application.Contracts.Jobs;

public sealed record CreateJobRequest(
    string Title, string Description, string EmploymentType, string WorkArrangement, string Location,
    string AccessibilityInformation, int[]? AccommodationIds, DateTime? ExpiresAt);

public sealed record ApplyRequest(string ApplicationText, string? VoluntarilySharedNeeds);
public sealed record ChangeApplicationStatusRequest(string Status);

public sealed record JobDto(
    Guid JobId, Guid OrganizationId, string OrganizationName, string Title, string Description, string EmploymentType,
    string WorkArrangement, string Location, string AccessibilityInformation, IReadOnlyList<string> Accommodations,
    string Status, DateTime PublishedAt, DateTime? ExpiresAt, int ApplicationsCount, bool Applied);

public sealed record ApplicationDto(
    Guid ApplicationId, Guid JobId, string JobTitle, Guid ApplicantId, string ApplicantName, string ApplicationText,
    string? VoluntarilySharedNeeds, string Status, DateTime SubmittedAt);
