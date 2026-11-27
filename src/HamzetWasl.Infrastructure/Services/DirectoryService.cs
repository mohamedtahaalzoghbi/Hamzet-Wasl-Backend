using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class DirectoryService(HamzetWaslDbContext db, INotificationService notifications, IClock clock) : IDirectoryService
{
    private static readonly string[] Types = ["service", "program", "event"];
    private static readonly string[] Statuses = ["draft", "published", "archived"];

    private static AppException Invalid(string f, string m) => AppException.Validation(new Dictionary<string, string[]> { [f] = [m] });

    private async Task<Organization> ApprovedAssociationAsync(Guid ownerUserId, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId && o.OrganizationType == OrganizationTypes.Association, ct) ?? throw AppException.Forbidden();
        if (org.ApprovalStatus != "approved") throw AppException.Forbidden(message: "لازم الجمعية تتعتمد الأول عشان تنشر خدمات.");
        return org;
    }

    private async Task<List<DirectoryItemDto>> ToDtosAsync(List<DirectoryItem> rows, CancellationToken ct)
    {
        var oids = rows.Select(r => r.OrganizationId).Distinct().ToList();
        var names = await db.Organizations.Where(o => oids.Contains(o.OrganizationId)).ToDictionaryAsync(o => o.OrganizationId, o => o.Name, ct);
        var ids = rows.Select(r => r.ItemId).ToList();
        var counts = await db.ServiceInquiries.Where(i => ids.Contains(i.ItemId)).GroupBy(i => i.ItemId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return rows.Select(r => new DirectoryItemDto(r.ItemId, r.OrganizationId, names.GetValueOrDefault(r.OrganizationId, ""), r.ItemType, r.Title, r.Description,
            r.StartsAt, r.EndsAt, r.PublicationStatus, counts.GetValueOrDefault(r.ItemId))).ToList();
    }

    private IQueryable<DirectoryItem> Live() => db.DirectoryItems.Where(i => i.PublicationStatus == "published" && i.Organization.ApprovalStatus == "approved");

    public async Task<PagedResult<DirectoryItemDto>> SearchAsync(string? type, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Live();
        if (type is not null && Types.Contains(type)) query = query.Where(i => i.ItemType == type);
        if (!string.IsNullOrWhiteSpace(q)) { var t = q.Trim(); query = query.Where(i => i.Title.Contains(t) || i.Description.Contains(t)); }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.StartsAt ?? DateTime.MaxValue).ThenBy(i => i.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<DirectoryItemDto>(await ToDtosAsync(rows, ct), page, pageSize, total);
    }

    public async Task<DirectoryItemDto> GetAsync(Guid itemId, CancellationToken ct = default)
    {
        var r = await Live().FirstOrDefaultAsync(i => i.ItemId == itemId, ct) ?? throw AppException.NotFound();
        return (await ToDtosAsync([r], ct))[0];
    }

    public async Task<IReadOnlyList<DirectoryItemDto>> MineAsync(Guid ownerUserId, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId && o.OrganizationType == OrganizationTypes.Association, ct) ?? throw AppException.Forbidden();
        var rows = await db.DirectoryItems.Where(i => i.OrganizationId == org.OrganizationId).OrderByDescending(i => i.StartsAt).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<DirectoryItemDto> CreateAsync(Guid ownerUserId, DirectoryItemRequest r, CancellationToken ct = default)
    {
        var org = await ApprovedAssociationAsync(ownerUserId, ct);
        if (!Types.Contains(r.ItemType)) throw Invalid("itemType", "النوع لازم يكون خدمة أو برنامج أو فعالية.");
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 200) throw Invalid("title", "العنوان مطلوب (حتى 200 حرف).");
        if (string.IsNullOrWhiteSpace(r.Description)) throw Invalid("description", "الوصف مطلوب.");
        if (r.StartsAt is { } s && r.EndsAt is { } e && e < s) throw Invalid("endsAt", "تاريخ النهاية قبل البداية.");
        var item = new DirectoryItem
        {
            ItemId = Guid.NewGuid(), OrganizationId = org.OrganizationId, ItemType = r.ItemType, Title = r.Title.Trim(), Description = r.Description.Trim(),
            StartsAt = r.StartsAt, EndsAt = r.EndsAt, PublicationStatus = r.Publish ? "published" : "draft"
        };
        db.DirectoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([item], ct))[0];
    }

    public async Task<DirectoryItemDto> SetStatusAsync(Guid ownerUserId, Guid itemId, string status, CancellationToken ct = default)
    {
        var org = await ApprovedAssociationAsync(ownerUserId, ct);
        if (!Statuses.Contains(status)) throw Invalid("status", "الحالة غير صحيحة (draft, published, archived).");
        var item = await db.DirectoryItems.FirstOrDefaultAsync(i => i.ItemId == itemId && i.OrganizationId == org.OrganizationId, ct) ?? throw AppException.NotFound();
        item.PublicationStatus = status;
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([item], ct))[0];
    }

    public async Task<InquiryDto> StartInquiryAsync(Guid userId, Guid itemId, InquiryRequest r, CancellationToken ct = default)
    {
        var item = await Live().Include(i => i.Organization).FirstOrDefaultAsync(i => i.ItemId == itemId, ct) ?? throw AppException.NotFound();
        if (item.Organization.OwnerUserId == userId) throw AppException.Forbidden(message: "مينفعش تبعت استفسار لنفسك.");
        if (string.IsNullOrWhiteSpace(r.Subject) || r.Subject.Trim().Length > 200) throw Invalid("subject", "الموضوع مطلوب (حتى 200 حرف).");
        if (string.IsNullOrWhiteSpace(r.Body)) throw Invalid("body", "اكتب رسالتك.");
        var now = clock.UtcNow;
        var inq = new ServiceInquiry { InquiryId = Guid.NewGuid(), OrganizationId = item.OrganizationId, ItemId = itemId, SenderId = userId, Subject = r.Subject.Trim(), Status = "open", CreatedAt = now };
        db.ServiceInquiries.Add(inq);
        db.InquiryMessages.Add(new InquiryMessage { MessageId = Guid.NewGuid(), InquiryId = inq.InquiryId, SenderId = userId, Body = r.Body.Trim(), SentAt = now });
        await db.SaveChangesAsync(ct);
        await notifications.NotifyAsync(item.Organization.OwnerUserId, NotificationTypes.InquiryReceived, "وصلك استفسار جديد على أحد خدماتك.", "inquiry", inq.InquiryId, ct);
        return await GetInquiryAsync(userId, inq.InquiryId, ct);
    }

    public async Task<IReadOnlyList<InquiryDto>> InquiriesAsync(Guid userId, bool received, CancellationToken ct = default)
    {
        var q = db.ServiceInquiries.AsQueryable();
        q = received ? q.Where(i => i.Organization.OwnerUserId == userId) : q.Where(i => i.SenderId == userId);
        var ids = await q.OrderByDescending(i => i.CreatedAt).Select(i => i.InquiryId).Take(100).ToListAsync(ct);
        var list = new List<InquiryDto>();
        foreach (var id in ids) list.Add(await BuildAsync(id, ct));
        return list;
    }

    private async Task<InquiryDto> BuildAsync(Guid inquiryId, CancellationToken ct)
    {
        var i = await db.ServiceInquiries.Include(x => x.Item).Include(x => x.Organization).FirstAsync(x => x.InquiryId == inquiryId, ct);
        var msgs = await db.InquiryMessages.Where(m => m.InquiryId == inquiryId).OrderBy(m => m.SentAt).ToListAsync(ct);
        var uids = msgs.Select(m => m.SenderId).Append(i.SenderId).Distinct().ToList();
        var names = await db.UserProfiles.Where(p => uids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        return new InquiryDto(i.InquiryId, i.ItemId, i.Item.Title, i.OrganizationId, i.Organization.Name, i.SenderId, names.GetValueOrDefault(i.SenderId, ""), i.Subject, i.Status, i.CreatedAt,
            msgs.Select(m => new InquiryMessageDto(m.MessageId, m.SenderId, names.GetValueOrDefault(m.SenderId, ""), m.Body, m.SentAt)).ToList());
    }

    private async Task<ServiceInquiry> AccessibleAsync(Guid userId, Guid inquiryId, CancellationToken ct)
    {
        var i = await db.ServiceInquiries.Include(x => x.Organization).FirstOrDefaultAsync(x => x.InquiryId == inquiryId, ct) ?? throw AppException.NotFound();
        if (i.SenderId != userId && i.Organization.OwnerUserId != userId) throw AppException.NotFound();
        return i;
    }

    public async Task<InquiryDto> GetInquiryAsync(Guid userId, Guid inquiryId, CancellationToken ct = default)
    {
        await AccessibleAsync(userId, inquiryId, ct);
        return await BuildAsync(inquiryId, ct);
    }

    public async Task<InquiryDto> ReplyAsync(Guid userId, Guid inquiryId, string body, CancellationToken ct = default)
    {
        var i = await AccessibleAsync(userId, inquiryId, ct);
        if (string.IsNullOrWhiteSpace(body)) throw Invalid("body", "اكتب رسالتك.");
        if (i.Status == "closed") throw AppException.Conflict(message: "الاستفسار مقفول.");
        db.InquiryMessages.Add(new InquiryMessage { MessageId = Guid.NewGuid(), InquiryId = inquiryId, SenderId = userId, Body = body.Trim(), SentAt = clock.UtcNow });
        var fromOrg = i.Organization.OwnerUserId == userId;
        if (fromOrg) i.Status = "replied";
        await db.SaveChangesAsync(ct);
        if (fromOrg) await notifications.NotifyAsync(i.SenderId, NotificationTypes.InquiryReplied, "ردّت الجهة على استفسارك.", "inquiry", inquiryId, ct);
        else await notifications.NotifyAsync(i.Organization.OwnerUserId, NotificationTypes.InquiryReceived, "وصلتك رسالة جديدة في استفسار.", "inquiry", inquiryId, ct);
        return await BuildAsync(inquiryId, ct);
    }
}
