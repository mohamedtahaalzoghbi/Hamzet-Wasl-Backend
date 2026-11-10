namespace HamzetWasl.Application.Contracts.Organization;

public sealed class UpdateOrganizationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? Description { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Website { get; set; }
    public string? EmployeeCountRange { get; set; }
    public string? ContactInformation { get; set; }
}

public sealed record OrganizationDocumentDto(
    string DocumentType,
    DateTime UploadedAt);

public sealed record OrganizationResponse(
    Guid OrganizationId,
    string OrganizationType,
    string Name,
    string? Field,
    string? Description,
    string? Governorate,
    string? City,
    string? Website,
    string? LogoUrl,
    string? CoverUrl,
    string? EmployeeCountRange,
    string? ContactInformation,
    string ApprovalStatus,
    IReadOnlyList<OrganizationDocumentDto> Documents,
    IReadOnlyList<string> MissingDocuments,
    bool CanSubmit,
    int ProfileCompletionPercent);

public sealed record OrganizationReviewStatusResponse(
    string ApprovalStatus,
    Guid? ReviewId,
    string? ReviewStatus,
    DateTime? RequestedAt,
    DateTime? DecidedAt,
    string? DecisionNotes,
    IReadOnlyList<string> MissingDocuments,
    bool CanSubmit);

public sealed record SubmitOrganizationResponse(
    Guid ReviewId,
    string ApprovalStatus,
    string ReviewStatus,
    DateTime RequestedAt);
