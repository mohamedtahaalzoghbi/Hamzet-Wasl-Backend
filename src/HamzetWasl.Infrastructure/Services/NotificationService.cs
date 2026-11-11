using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Notifications;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Stores the notification row and pushes the contracted payload to the recipient.
/// The same rows feed the notification centre, so the history and the live events
/// always describe the same notification.
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

    public async Task<PagedResult<NotificationDto>> GetForRecipientAsync(
        Guid recipientId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientId == recipientId);
        var total = await query.CountAsync(ct);

        var rows = await query
            // created_at is DATETIME2(0); the id breaks ties so paging stays stable.
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.NotificationId,
                n.NotificationType,
                n.TextBody,
                n.CreatedAt,
                n.ReadAt
            })
            .ToListAsync(ct);

        // The Arabic title is derived from the stored type (it is not a column),
        // so it is composed in memory exactly like the push payload does.
        var items = rows
            .Select(n => new NotificationDto(
                n.NotificationId,
                n.NotificationType,
                NotificationTitles.For(n.NotificationType),
                n.TextBody ?? string.Empty,
                n.CreatedAt,
                n.ReadAt is not null,
                n.ReadAt))
            .ToList();

        return new PagedResult<NotificationDto>(items, page, pageSize, total);
    }

    public async Task MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default)
    {
        // Scoping the lookup to the recipient turns another user's id into 404
        // instead of 403, which would confirm that the notification exists.
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.RecipientId == recipientId, ct)
            ?? throw AppException.NotFound();

        // PUT is idempotent: marking an already-read notification keeps the first read time.
        if (notification.ReadAt is not null)
        {
            return;
        }

        notification.ReadAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
