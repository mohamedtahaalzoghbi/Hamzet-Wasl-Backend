using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>
/// Emergency contacts and SOS alerts. There is no SMS gateway: a contact that is also a registered user
/// (matched by email or phone) gets an in-app notification and the delivery is marked delivered; any other
/// contact stays pending. Alerts are kept for 30 days.
/// </summary>
public sealed class SosService(HamzetWaslDbContext db, INotificationService notifications, IClock clock) : ISosService
{
    private const int MaxContacts = 5;
    private static AppException Invalid(string f, string m) => AppException.Validation(new Dictionary<string, string[]> { [f] = [m] });

    private static string Mask(string d)
    {
        d = d.Trim();
        if (d.Contains('@')) { var i = d.IndexOf('@'); return (i <= 2 ? "***" : d[..2] + "***") + d[i..]; }
        return d.Length <= 4 ? "****" : new string('*', d.Length - 4) + d[^4..];
    }

    public async Task<IReadOnlyList<EmergencyContactDto>> ContactsAsync(Guid userId, CancellationToken ct = default) =>
        (await db.EmergencyContacts.Where(c => c.OwnerUserId == userId).OrderBy(c => c.DisplayName).ToListAsync(ct))
        .Select(c => new EmergencyContactDto(c.ContactId, c.DisplayName, Mask(c.ProtectedContactDestination))).ToList();

    public async Task<EmergencyContactDto> AddContactAsync(Guid userId, EmergencyContactRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.DisplayName) || r.DisplayName.Trim().Length > 200) throw Invalid("displayName", "الاسم مطلوب.");
        var dest = (r.Destination ?? "").Trim();
        if (dest.Length < 5 || dest.Length > 300 || !(dest.Contains('@') || dest.Count(char.IsDigit) >= 8)) throw Invalid("destination", "اكتب رقم موبايل أو بريد إلكتروني صحيح.");
        if (await db.EmergencyContacts.CountAsync(c => c.OwnerUserId == userId, ct) >= MaxContacts) throw AppException.Conflict(message: "أقصى عدد لجهات الطوارئ " + MaxContacts + ".");
        var c = new EmergencyContact { ContactId = Guid.NewGuid(), OwnerUserId = userId, DisplayName = r.DisplayName.Trim(), ProtectedContactDestination = dest };
        db.EmergencyContacts.Add(c);
        await db.SaveChangesAsync(ct);
        return new EmergencyContactDto(c.ContactId, c.DisplayName, Mask(dest));
    }

    public async Task DeleteContactAsync(Guid userId, Guid contactId, CancellationToken ct = default)
    {
        var c = await db.EmergencyContacts.FirstOrDefaultAsync(x => x.ContactId == contactId && x.OwnerUserId == userId, ct) ?? throw AppException.NotFound();
        var deliveries = await db.SosDeliveries.Where(d => d.ContactId == contactId).ToListAsync(ct);
        db.SosDeliveries.RemoveRange(deliveries);
        db.EmergencyContacts.Remove(c);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SosAlertDto> TriggerAsync(Guid userId, SosRequest r, CancellationToken ct = default)
    {
        if (r.Latitude is < -90 or > 90 || r.Longitude is < -180 or > 180) throw Invalid("latitude", "الموقع غير صحيح.");
        var contacts = await db.EmergencyContacts.Where(c => c.OwnerUserId == userId).ToListAsync(ct);
        if (contacts.Count == 0) throw AppException.Business("NO_EMERGENCY_CONTACTS", "ضيف جهة طوارئ واحدة على الأقل الأول.");
        var now = clock.UtcNow;
        var alert = new SosAlert { AlertId = Guid.NewGuid(), UserId = userId, Latitude = r.Latitude, Longitude = r.Longitude, Message = string.IsNullOrWhiteSpace(r.Message) ? null : r.Message.Trim(), CreatedAt = now, RetentionUntil = now.AddDays(30) };
        db.SosAlerts.Add(alert);
        var name = await db.UserProfiles.Where(p => p.UserId == userId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct) ?? "مستخدم";
        var outcome = new List<SosDeliveryDto>();
        foreach (var c in contacts)
        {
            var dest = c.ProtectedContactDestination.Trim();
            var digits = new string(dest.Where(char.IsDigit).ToArray());
            Guid? target = null;
            if (dest.Contains('@')) target = await db.UserAccounts.Where(u => u.Email == dest).Select(u => (Guid?)u.UserId).FirstOrDefaultAsync(ct);
            else if (digits.Length >= 8) { var tail = digits[^8..]; target = await db.UserAccounts.Where(u => u.Phone != null && u.Phone.EndsWith(tail)).Select(u => (Guid?)u.UserId).FirstOrDefaultAsync(ct); }
            var status = target is null ? "pending" : "delivered";
            db.SosDeliveries.Add(new SosDelivery { DeliveryId = Guid.NewGuid(), AlertId = alert.AlertId, ContactId = c.ContactId, DeliveryStatus = status, AttemptedAt = now });
            outcome.Add(new SosDeliveryDto(c.DisplayName, status));
            if (target is { } t) await notifications.NotifyAsync(t, NotificationTypes.SosAlert, $"{name} بعت تنبيه طوارئ. الموقع: {r.Latitude},{r.Longitude}" + (alert.Message is null ? "" : " - " + alert.Message), "sos", alert.AlertId, ct);
        }
        await db.SaveChangesAsync(ct);
        return new SosAlertDto(alert.AlertId, alert.Latitude, alert.Longitude, alert.Message, alert.CreatedAt, outcome);
    }

    public async Task<IReadOnlyList<SosAlertDto>> MyAlertsAsync(Guid userId, CancellationToken ct = default)
    {
        var alerts = await db.SosAlerts.Where(a => a.UserId == userId && a.RetentionUntil > clock.UtcNow).OrderByDescending(a => a.CreatedAt).Take(50).ToListAsync(ct);
        var ids = alerts.Select(a => a.AlertId).ToList();
        var ds = await (from d in db.SosDeliveries join c in db.EmergencyContacts on d.ContactId equals c.ContactId where ids.Contains(d.AlertId) select new { d.AlertId, c.DisplayName, d.DeliveryStatus }).ToListAsync(ct);
        return alerts.Select(a => new SosAlertDto(a.AlertId, a.Latitude, a.Longitude, a.Message, a.CreatedAt, ds.Where(d => d.AlertId == a.AlertId).Select(d => new SosDeliveryDto(d.DisplayName, d.DeliveryStatus)).ToList())).ToList();
    }
}
