namespace HamzetWasl.Application.Contracts.Help;

public sealed record CreateHelpRequestRequest(
    string HelpMode,
    string Category,
    string? Description,
    string ApproximateArea,
    string? Urgency,
    DateTime? ScheduledAt);

public sealed record CreateHelpOfferRequest(string? Message);

public sealed record AcceptHelpOfferRequest(Guid OfferId);

public sealed record SendHelpMessageRequest(string Text);

public sealed record HelpFeedbackRequest(int Rating, string? Comment);

public sealed record HelpSafetyReportRequest(string Description);

public sealed record HelpOfferDto(
    Guid OfferId,
    Guid VolunteerId,
    string VolunteerName,
    string? Message,
    string Status,
    double? AverageRating,
    int RatingsCount,
    DateTime CreatedAt);

public sealed record HelpRequestDto(
    Guid RequestId,
    Guid RequesterId,
    string RequesterName,
    string HelpMode,
    string Category,
    string? Description,
    string ApproximateArea,
    string Urgency,
    DateTime? ScheduledAt,
    string Status,
    int OffersCount,
    Guid? AcceptedOfferId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    bool HasOffered);

public sealed record HelpRequestDetailDto(
    HelpRequestDto Request,
    IReadOnlyList<HelpOfferDto> Offers,
    Guid? AssignedVolunteerId,
    string? AssignedVolunteerName,
    bool ChatOpen);

public sealed record HelpMessageDto(Guid MessageId, Guid SenderId, string SenderName, string Text, DateTime SentAt);
