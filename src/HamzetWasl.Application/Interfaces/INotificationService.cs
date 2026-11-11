using System.Text.Json;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Notifications;

namespace HamzetWasl.Application.Interfaces;

/// <summary>
/// The notification payload pushed to the clients. The field names are the contract
/// (<c>docs/API_CONTRACT.md</c>, section 10) and must not change without a version bump.
/// </summary>
public sealed record NotificationPayload(
    Guid Id,
    string Type,
    string TitleAr,
    string BodyAr,
    DateTime CreatedAt,
    string PayloadJson);

public interface INotificationPublisher
{
    /// <summary>Pushes a notification to the recipient's SignalR group.</summary>
    Task PublishAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default);
}

/// <summary>
/// Creates the notification rows and pushes them to the connected clients.
/// Every notification the product sends goes through this service so the stored row and
/// the pushed payload never drift apart. It also serves the notification centre
/// (history + read tracking) so the client can recover what it missed offline.
/// </summary>
public interface INotificationService
{
    /// <summary>Stores the notification and pushes it to the recipient.</summary>
    Task<Guid> NotifyAsync(
        Guid recipientId,
        string notificationType,
        string bodyAr,
        string? targetType = null,
        Guid? targetId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns one page of the recipient's notifications, newest first.
    /// <paramref name="page"/> is 1-based and <paramref name="pageSize"/> is clamped to 1..100,
    /// matching the paging contract used by every other list endpoint.
    /// </summary>
    Task<PagedResult<NotificationDto>> GetForRecipientAsync(
        Guid recipientId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Marks one notification as read. The lookup is scoped to the recipient so another
    /// user's id is reported as 404 (no existence leak). The call is idempotent: marking
    /// an already-read notification succeeds without changing <c>readAt</c>.
    /// </summary>
    Task MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default);
}

/// <summary>Helpers shared by the notification services.</summary>
public static class NotificationPayloadFactory
{
    /// <summary>Builds the JSON document that carries the extra context of a notification.</summary>
    public static string BuildPayloadJson(string channel, string? targetType, Guid? targetId)
    {
        var payload = new Dictionary<string, object?>
        {
            ["channel"] = channel,
            ["targetType"] = targetType,
            ["targetId"] = targetId?.ToString()
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
