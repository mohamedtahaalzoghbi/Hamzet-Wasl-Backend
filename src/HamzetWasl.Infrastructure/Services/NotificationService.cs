using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Stores the notification row and pushes the contracted payload to the recipient.
/// </summary>
public sealed class NotificationService(
    HamzetWaslDbContext db,
    INotificationPublisher publisher,
    IClock clock) : INotificationService
{
    public async Task<Guid> NotifyAsync(
        Guid recipientId,
        string notificationType,
        string bodyAr,
        string? targetType = null,
        Guid? targetId = null,
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var notification = new Notification
        {
            NotificationId = Guid.NewGuid(),
            RecipientId = recipientId,
            NotificationType = notificationType,
            Channel = NotificationChannels.InApp,
            TextBody = bodyAr,
            CreatedAt = now
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        var payload = new NotificationPayload(
            notification.NotificationId,
            notification.NotificationType,
            NotificationTitles.For(notification.NotificationType),
            bodyAr,
            now,
            NotificationPayloadFactory.BuildPayloadJson(notification.Channel, targetType, targetId));

        await publisher.PublishAsync(recipientId, payload, ct);
        return notification.NotificationId;
    }
}
