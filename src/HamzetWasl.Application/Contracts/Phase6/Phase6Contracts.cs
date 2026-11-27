namespace HamzetWasl.Application.Contracts.Phase6;

// ---- Directory (services, programs, events) ----
public sealed record DirectoryItemRequest(string ItemType, string Title, string Description, DateTime? StartsAt, DateTime? EndsAt, bool Publish);
public sealed record DirectoryStatusRequest(string Status);
public sealed record DirectoryItemDto(Guid ItemId, Guid OrganizationId, string OrganizationName, string ItemType, string Title, string Description,
    DateTime? StartsAt, DateTime? EndsAt, string PublicationStatus, int InquiriesCount);
public sealed record InquiryRequest(string Subject, string Body);
public sealed record InquiryMessageRequest(string Body);
public sealed record InquiryMessageDto(Guid MessageId, Guid SenderId, string SenderName, string Body, DateTime SentAt);
public sealed record InquiryDto(Guid InquiryId, Guid ItemId, string ItemTitle, Guid OrganizationId, string OrganizationName, Guid SenderId, string SenderName,
    string Subject, string Status, DateTime CreatedAt, IReadOnlyList<InquiryMessageDto> Messages);

// ---- Accessible places ----
public sealed record FacilityInput(string Type, string Status, string? Notes);
public sealed record PlaceRequest(string Name, string Address, decimal Latitude, decimal Longitude, string VerificationSource, IReadOnlyList<FacilityInput>? Facilities);
public sealed record FacilityDto(string Type, string Status, string? Notes);
public sealed record PlaceReviewRequest(int Rating, string? Comment);
public sealed record PlaceReviewDto(Guid ReviewId, string UserName, int Rating, string? Comment, DateTime CreatedAt);
public sealed record PlaceDto(Guid PlaceId, string Name, string Address, decimal Latitude, decimal Longitude, DateTime? LastVerifiedAt, string VerificationSource,
    IReadOnlyList<FacilityDto> Facilities, double? AverageRating, int ReviewsCount, IReadOnlyList<PlaceReviewDto>? Reviews);
public sealed record ObstacleRequest(Guid? PlaceId, string Description, decimal Latitude, decimal Longitude);
public sealed record ObstacleStatusRequest(string Status);
public sealed record ObstacleDto(Guid ObstacleId, Guid? PlaceId, string Description, decimal Latitude, decimal Longitude, string Status, DateTime ReportedAt);

// ---- SOS ----
public sealed record EmergencyContactRequest(string DisplayName, string Destination);
public sealed record EmergencyContactDto(Guid ContactId, string DisplayName, string MaskedDestination);
public sealed record SosRequest(decimal Latitude, decimal Longitude, string? Message);
public sealed record SosDeliveryDto(string ContactName, string Status);
public sealed record SosAlertDto(Guid AlertId, decimal Latitude, decimal Longitude, string? Message, DateTime CreatedAt, IReadOnlyList<SosDeliveryDto> Deliveries);

// ---- Stats ----
public sealed record PublicStatsDto(int VerifiedVolunteers, int DisabledUsers, int ApprovedOrganizations, int PublishedJobs, int CompletedHelpRequests, int Places);
public sealed record AdminStatsDto(PublicStatsDto Totals, int PendingVolunteerReviews, int PendingOrganizationReviews, int OpenContentReports, int OpenSafetyReports,
    int OpenObstacleReports, int SosAlertsLast30Days, int NewUsersLast7Days);
