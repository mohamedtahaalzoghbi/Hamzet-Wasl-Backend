namespace HamzetWasl.Application.Contracts.Admin;

public sealed record VolunteerReviewListItemDto(
    Guid ReviewId,
    Guid VolunteerId,
    string? VolunteerName,
    string Email,
    string Status,
    DateTime? InterviewAt,
    DateTime? DecidedAt);

public sealed record EvidenceDto(
    Guid EvidenceId,
    string EvidenceType,
    string ShortLivedUrl,
    DateTime ExpiresAt);

public sealed record VolunteerReviewDetailDto(
    Guid ReviewId,
    Guid VolunteerId,
    string? VolunteerName,
    string Email,
    string? Phone,
    string HelpMode,
    string ServiceArea,
    string SkillLevel,
    int WeeklyHours,
    string Status,
    DateTime? InterviewAt,
    DateTime? DecidedAt,
    IReadOnlyList<EvidenceDto> Evidence,
    IReadOnlyList<SocialLinkAdminDto> SocialLinks);

public sealed record SocialLinkAdminDto(string Platform, string Url);

public sealed class DecisionRequest
{
    public bool Approve { get; set; }
    public string? Notes { get; set; }
}

public sealed record OrganizationReviewListItemDto(
    Guid ReviewId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationType,
    string ApprovalStatus,
    string Status,
    DateTime RequestedAt,
    DateTime? DecidedAt);

public sealed record OrganizationDocumentAdminDto(
    string DocumentType,
    string ShortLivedUrl,
    DateTime ExpiresAt,
    DateTime UploadedAt);

public sealed record OrganizationReviewDetailDto(
    Guid ReviewId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationType,
    string? Field,
    string? Description,
    string? Governorate,
    string? City,
    string? Website,
    string? ContactInformation,
    string ApprovalStatus,
    string Status,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    string? DecisionNotes,
    IReadOnlyList<OrganizationDocumentAdminDto> Documents);

public sealed class CreateInterviewSlotRequest
{
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
}

public sealed record InterviewSlotAdminDto(
    Guid InterviewSlotId,
    Guid ReviewerId,
    DateTime StartsAt,
    int DurationMinutes,
    string Status);

public sealed class SuspendUserRequest
{
    public bool Suspend { get; set; }
    public string? Reason { get; set; }
}

public sealed record DeletionRequestDto(
    Guid DeletionRequestId,
    Guid UserId,
    string Email,
    string Status,
    DateTime RequestedAt,
    DateTime? ResolvedAt);

public sealed class ResolveDeletionRequestRequest
{
    public bool Approve { get; set; }
    public string? Notes { get; set; }
}

public sealed record AdminAuditDto(
    Guid AuditId,
    Guid ActorUserId,
    string Action,
    string TargetType,
    Guid TargetId,
    string Outcome,
    DateTime OccurredAt);
