namespace HamzetWasl.Application.Contracts.Notifications;

/// <summary>
/// One item of the notification centre. The fields mirror the live SignalR payload
/// (type/titleAr/bodyAr/createdAt) so the clients can render history and live events
/// with the same model.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    string TitleAr,
    string BodyAr,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ReadAt);
