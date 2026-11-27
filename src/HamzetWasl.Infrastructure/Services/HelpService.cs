using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Help;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class HelpService(HamzetWaslDbContext db, INotificationService notifications, IClock clock) : IHelpService
{
    private static readonly string[] Modes = ["online", "field"];
    private static readonly string[] Categories = ["reading", "accompaniment", "explanation", "other"];
    private static readonly string[] Urgencies = ["normal", "urgent"];

    private static AppException Invalid(string field, string message) =>
        AppException.Validation(new Dictionary<string, string[]> { [field] = [message] });

    public async Task<HelpRequestDto> CreateRequestAsync(Guid userId, CreateHelpRequestRequest r, CancellationToken ct = default)
    {
        if (!Modes.Contains(r.HelpMode)) throw Invalid("helpMode", "نوع المساعدة غير صحيح (online أو field).");
        if (!Categories.Contains(r.Category)) throw Invalid("category", "تصنيف المساعدة غير صحيح.");
        var urgency = string.IsNullOrWhiteSpace(r.Urgency) ? "normal" : r.Urgency!;
        if (!Urgencies.Contains(urgency)) throw Invalid("urgency", "درجة الاستعجال غير صحيحة.");
        if (string.IsNullOrWhiteSpace(r.ApproximateArea) || r.ApproximateArea.Length > 200) throw Invalid("approximateArea", "المنطقة التقريبية مطلوبة (حتى 200 حرف).");
        if (r.Description is { Length: > 2000 }) throw Invalid("description", "الوصف طويل جدًا.");
        var open = await db.HelpRequests.CountAsync(x => x.RequesterId == userId && (x.Status == "open" || x.Status == "accepted" || x.Status == "in_progress"), ct);
        if (open >= 5) throw AppException.Business("HELP_TOO_MANY_OPEN", "عندك 5 طلبات مفتوحة بالفعل. أنهِ أو ألغِ طلبًا قبل إضافة آخر.");

        var entity = new HelpRequest
        {
            RequestId = Guid.NewGuid(),
            RequesterId = userId,
            HelpMode = r.HelpMode,
            Category = r.Category,
            Description = r.Description?.Trim(),
            ApproximateArea = r.ApproximateArea.Trim(),
            Urgency = urgency,
            ScheduledAt = r.ScheduledAt,
            Status = "open",
            CreatedAt = clock.UtcNow
        };
        db.HelpRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(entity, userId, ct);
    }

    public async Task<PagedResult<HelpRequestDto>> GetMineAsync(Guid userId, string role, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        IQueryable<HelpRequest> q = role == "volunteer"
            ? db.HelpRequests.Where(x => x.HelpOffers.Any(o => o.VolunteerId == userId))
            : db.HelpRequests.Where(x => x.RequesterId == userId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<HelpRequestDto>(await ToDtosAsync(rows, userId, ct), page, pageSize, total);
    }

    public async Task<PagedResult<HelpRequestDto>> GetOpenAsync(Guid volunteerId, string? mode, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Clamp(page, pageSize);
        var vol = await RequireApprovedVolunteerAsync(volunteerId, ct);
        var q = db.HelpRequests.Where(x => x.Status == "open");
        if (mode is "online" or "field") q = q.Where(x => x.HelpMode == mode);
        if (vol.HelpMode != "online_and_field") q = q.Where(x => x.HelpMode == "online");
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.Urgency == "urgent").ThenByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<HelpRequestDto>(await ToDtosAsync(rows, volunteerId, ct), page, pageSize, total);
    }

    public async Task<HelpRequestDetailDto> GetDetailAsync(Guid userId, string role, Guid requestId, CancellationToken ct = default)
    {
        var req = await db.HelpRequests.Include(x => x.HelpAssignment).ThenInclude(a => a!.Offer).FirstOrDefaultAsync(x => x.RequestId == requestId, ct)
                  ?? throw AppException.NotFound();
        var isRequester = req.RequesterId == userId;
        var assignedVolunteer = req.HelpAssignment?.Offer.VolunteerId;
        var myOffer = await db.HelpOffers.AnyAsync(o => o.RequestId == requestId && o.VolunteerId == userId, ct);
        if (!isRequester && role != "admin" && !myOffer && !(role == "volunteer" && req.Status == "open"))
            throw AppException.Forbidden();

        var dto = await ToDtoAsync(req, userId, ct);
        IReadOnlyList<HelpOfferDto> offers = [];
        if (isRequester || role == "admin")
        {
            var rows = await db.HelpOffers.Where(o => o.RequestId == requestId).OrderBy(o => o.CreatedAt).ToListAsync(ct);
            offers = await ToOfferDtosAsync(rows, ct);
        }
        else if (myOffer)
        {
            var rows = await db.HelpOffers.Where(o => o.RequestId == requestId && o.VolunteerId == userId).ToListAsync(ct);
            offers = await ToOfferDtosAsync(rows, ct);
        }

        string? name = null;
        if (assignedVolunteer is { } v) name = await NameAsync(v, ct);
        var chat = req.HelpAssignment != null && (isRequester || assignedVolunteer == userId || role == "admin");
        return new HelpRequestDetailDto(dto, offers, assignedVolunteer, name, chat);
    }

    public async Task<HelpOfferDto> OfferAsync(Guid volunteerId, Guid requestId, CreateHelpOfferRequest request, CancellationToken ct = default)
    {
        var vol = await RequireApprovedVolunteerAsync(volunteerId, ct);
        var req = await db.HelpRequests.FirstOrDefaultAsync(x => x.RequestId == requestId, ct) ?? throw AppException.NotFound();
        if (req.Status != "open") throw AppException.Business("HELP_REQUEST_CLOSED", "الطلب ده مبقاش مفتوح.");
        if (req.HelpMode == "field" && vol.HelpMode != "online_and_field") throw AppException.Forbidden(message: "الطلبات الميدانية للمتطوعين المعتمدين للميداني فقط.");
        if (req.RequesterId == volunteerId) throw AppException.Forbidden();
        if (request.Message is { Length: > 1000 }) throw Invalid("message", "الرسالة طويلة جدًا.");
        var exists = await db.HelpOffers.AnyAsync(o => o.RequestId == requestId && o.VolunteerId == volunteerId && o.Status != "withdrawn", ct);
        if (exists) throw AppException.Conflict(message: "قدّمت عرضًا على الطلب ده بالفعل.");

        var offer = new HelpOffer { OfferId = Guid.NewGuid(), RequestId = requestId, VolunteerId = volunteerId, Message = request.Message?.Trim(), Status = "pending", CreatedAt = clock.UtcNow };
        db.HelpOffers.Add(offer);
        await db.SaveChangesAsync(ct);
        await notifications.NotifyAsync(req.RequesterId, NotificationTypes.HelpOfferReceived, "وصلك عرض مساعدة جديد على طلبك.", "help_request", requestId, ct);
        return (await ToOfferDtosAsync([offer], ct))[0];
    }

    public async Task<HelpRequestDetailDto> AcceptOfferAsync(Guid requesterId, Guid requestId, Guid offerId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var req = await db.HelpRequests.FirstOrDefaultAsync(x => x.RequestId == requestId && x.RequesterId == requesterId, ct) ?? throw AppException.NotFound();
        if (req.Status != "open") throw AppException.Business("HELP_REQUEST_CLOSED", "الطلب ده مبقاش مفتوح.");
        var offer = await db.HelpOffers.FirstOrDefaultAsync(o => o.OfferId == offerId && o.RequestId == requestId && o.Status == "pending", ct) ?? throw AppException.NotFound("العرض غير موجود.");
        var now = clock.UtcNow;
        var others = await db.HelpOffers.Where(o => o.RequestId == requestId && o.OfferId != offerId && o.Status == "pending").ToListAsync(ct);
        foreach (var o in others) o.Status = "rejected";
        offer.Status = "accepted";
        req.Status = "accepted";
        db.HelpAssignments.Add(new HelpAssignment { RequestId = requestId, OfferId = offerId, RequesterAcceptedAt = now, VolunteerAcceptedAt = now, Status = "active" });
        db.HelpConversations.Add(new HelpConversation { ConversationId = Guid.NewGuid(), RequestId = requestId, OpenedAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await notifications.NotifyAsync(offer.VolunteerId, NotificationTypes.HelpOfferAccepted, "تم قبول عرضك. تقدر تبدأ المحادثة.", "help_request", requestId, ct);
        return await GetDetailAsync(requesterId, "disabled_user", requestId, ct);
    }

    public async Task CancelAsync(Guid requesterId, Guid requestId, CancellationToken ct = default)
    {
        var req = await db.HelpRequests.FirstOrDefaultAsync(x => x.RequestId == requestId && x.RequesterId == requesterId, ct) ?? throw AppException.NotFound();
        if (req.Status is "completed" or "cancelled") throw AppException.Business("HELP_REQUEST_CLOSED", "الطلب ده خلص بالفعل.");
        req.Status = "cancelled";
        var pending = await db.HelpOffers.Where(o => o.RequestId == requestId && o.Status == "pending").ToListAsync(ct);
        foreach (var o in pending) o.Status = "rejected";
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid requesterId, Guid requestId, CancellationToken ct = default)
    {
        var req = await db.HelpRequests.Include(x => x.HelpAssignment).ThenInclude(a => a!.Offer).FirstOrDefaultAsync(x => x.RequestId == requestId && x.RequesterId == requesterId, ct) ?? throw AppException.NotFound();
        if (req.Status is not ("accepted" or "in_progress")) throw AppException.Business("HELP_REQUEST_NOT_ACTIVE", "الطلب لازم يتقبل فيه عرض الأول.");
        req.Status = "completed";
        req.CompletedAt = clock.UtcNow;
        if (req.HelpAssignment != null) req.HelpAssignment.Status = "completed";
        await db.SaveChangesAsync(ct);
        if (req.HelpAssignment != null)
            await notifications.NotifyAsync(req.HelpAssignment.Offer.VolunteerId, NotificationTypes.HelpCompleted, "تم إنهاء المساعدة. شكرًا ليك.", "help_request", requestId, ct);
    }

    public async Task<IReadOnlyList<HelpMessageDto>> GetMessagesAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        var conv = await RequireConversationAsync(userId, requestId, ct);
        var rows = await db.HelpMessages.Where(m => m.ConversationId == conv.ConversationId).OrderBy(m => m.SentAt).Take(500).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(m => m.SenderId).Distinct().ToList(), ct);
        return rows.Select(m => new HelpMessageDto(m.MessageId, m.SenderId, names.GetValueOrDefault(m.SenderId, ""), m.TextBody ?? "", m.SentAt)).ToList();
    }

    public async Task<HelpMessageDto> SendMessageAsync(Guid userId, Guid requestId, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000) throw Invalid("text", "نص الرسالة مطلوب (حتى 2000 حرف).");
        var conv = await RequireConversationAsync(userId, requestId, ct);
        var req = await db.HelpRequests.Include(x => x.HelpAssignment).ThenInclude(a => a!.Offer).FirstAsync(x => x.RequestId == requestId, ct);
        if (req.Status is "completed" or "cancelled") throw AppException.Business("HELP_REQUEST_CLOSED", "المحادثة اتقفلت.");
        var msg = new HelpMessage { MessageId = Guid.NewGuid(), ConversationId = conv.ConversationId, SenderId = userId, TextBody = text.Trim(), SentAt = clock.UtcNow };
        db.HelpMessages.Add(msg);
        await db.SaveChangesAsync(ct);
        var other = req.RequesterId == userId ? req.HelpAssignment!.Offer.VolunteerId : req.RequesterId;
        await notifications.NotifyAsync(other, NotificationTypes.HelpMessageReceived, "وصلتك رسالة جديدة في طلب المساعدة.", "help_request", requestId, ct);
        return new HelpMessageDto(msg.MessageId, userId, await NameAsync(userId, ct), msg.TextBody!, msg.SentAt);
    }

    public async Task FeedbackAsync(Guid userId, Guid requestId, HelpFeedbackRequest request, CancellationToken ct = default)
    {
        if (request.Rating is < 1 or > 5) throw Invalid("rating", "التقييم من 1 إلى 5.");
        if (request.Comment is { Length: > 1000 }) throw Invalid("comment", "التعليق طويل جدًا.");
        var req = await db.HelpRequests.Include(x => x.HelpAssignment).ThenInclude(a => a!.Offer).FirstOrDefaultAsync(x => x.RequestId == requestId, ct) ?? throw AppException.NotFound();
        var vol = req.HelpAssignment?.Offer.VolunteerId;
        if (req.Status != "completed" || vol is null) throw AppException.Business("HELP_REQUEST_NOT_COMPLETED", "التقييم بعد إنهاء المساعدة.");
        if (userId != req.RequesterId && userId != vol) throw AppException.Forbidden();
        if (await db.HelpFeedbacks.AnyAsync(f => f.RequestId == requestId && f.AuthorId == userId, ct)) throw AppException.Conflict(message: "قيّمت الطلب ده بالفعل.");
        db.HelpFeedbacks.Add(new HelpFeedback { FeedbackId = Guid.NewGuid(), RequestId = requestId, AuthorId = userId, RatedUserId = userId == req.RequesterId ? vol.Value : req.RequesterId, Rating = request.Rating, Comment = request.Comment?.Trim(), CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReportAsync(Guid userId, Guid requestId, HelpSafetyReportRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000) throw Invalid("description", "وصف البلاغ مطلوب (حتى 2000 حرف).");
        var req = await db.HelpRequests.Include(x => x.HelpAssignment).ThenInclude(a => a!.Offer).FirstOrDefaultAsync(x => x.RequestId == requestId, ct) ?? throw AppException.NotFound();
        var vol = req.HelpAssignment?.Offer.VolunteerId;
        if (vol is null) throw AppException.Business("HELP_REQUEST_NOT_ACTIVE", "مفيش طرف تاني تبلّغ عنه لسه.");
        if (userId != req.RequesterId && userId != vol) throw AppException.Forbidden();
        db.SafetyReports.Add(new SafetyReport { ReportId = Guid.NewGuid(), ReporterId = userId, ReportedUserId = userId == req.RequesterId ? vol.Value : req.RequesterId, RequestId = requestId, Description = request.Description.Trim(), Status = "open", CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----
    private static (int, int) Clamp(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 50));

    private async Task<VolunteerProfile> RequireApprovedVolunteerAsync(Guid id, CancellationToken ct)
    {
        var vol = await db.VolunteerProfiles.FirstOrDefaultAsync(v => v.UserId == id, ct) ?? throw AppException.Forbidden();
        if (vol.VerificationStatus != VerificationStatuses.Approved) throw AppException.Forbidden(message: "لازم حسابك يتعتمد كمتطوع الأول.");
        return vol;
    }

    private async Task<HelpConversation> RequireConversationAsync(Guid userId, Guid requestId, CancellationToken ct)
    {
        var req = await db.HelpRequests.Include(x => x.HelpAssignment).ThenInclude(a => a!.Offer).Include(x => x.HelpAssignment).ThenInclude(a => a!.HelpConversation)
                      .FirstOrDefaultAsync(x => x.RequestId == requestId, ct) ?? throw AppException.NotFound();
        if (req.HelpAssignment?.HelpConversation is not { } conv) throw AppException.Business("HELP_CHAT_NOT_OPEN", "المحادثة بتفتح بعد قبول عرض.");
        if (userId != req.RequesterId && userId != req.HelpAssignment.Offer.VolunteerId) throw AppException.Forbidden();
        return conv;
    }

    private async Task<string> NameAsync(Guid id, CancellationToken ct) =>
        await db.UserProfiles.Where(p => p.UserId == id).Select(p => p.DisplayName).FirstOrDefaultAsync(ct) ?? "مستخدم";

    private async Task<Dictionary<Guid, string>> NamesAsync(List<Guid> ids, CancellationToken ct) =>
        await db.UserProfiles.Where(p => ids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);

    private async Task<HelpRequestDto> ToDtoAsync(HelpRequest r, Guid viewer, CancellationToken ct) => (await ToDtosAsync([r], viewer, ct))[0];

    private async Task<List<HelpRequestDto>> ToDtosAsync(List<HelpRequest> rows, Guid viewer, CancellationToken ct)
    {
        var ids = rows.Select(r => r.RequestId).ToList();
        var counts = await db.HelpOffers.Where(o => ids.Contains(o.RequestId) && o.Status != "withdrawn").GroupBy(o => o.RequestId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var mine = (await db.HelpOffers.Where(o => ids.Contains(o.RequestId) && o.VolunteerId == viewer).Select(o => o.RequestId).ToListAsync(ct)).ToHashSet();
        var accepted = await db.HelpAssignments.Where(a => ids.Contains(a.RequestId)).ToDictionaryAsync(a => a.RequestId, a => a.OfferId, ct);
        var names = await NamesAsync(rows.Select(r => r.RequesterId).Distinct().ToList(), ct);
        return rows.Select(r => new HelpRequestDto(r.RequestId, r.RequesterId, names.GetValueOrDefault(r.RequesterId, "مستخدم"), r.HelpMode, r.Category, r.Description, r.ApproximateArea, r.Urgency, r.ScheduledAt, r.Status,
            counts.GetValueOrDefault(r.RequestId), accepted.TryGetValue(r.RequestId, out var oid) ? oid : null, r.CreatedAt, r.CompletedAt, mine.Contains(r.RequestId))).ToList();
    }

    private async Task<IReadOnlyList<HelpOfferDto>> ToOfferDtosAsync(List<HelpOffer> rows, CancellationToken ct)
    {
        var vids = rows.Select(o => o.VolunteerId).Distinct().ToList();
        var names = await NamesAsync(vids, ct);
        var ratings = await db.HelpFeedbacks.Where(f => vids.Contains(f.RatedUserId)).GroupBy(f => f.RatedUserId).Select(g => new { g.Key, Avg = g.Average(x => (double)x.Rating), N = g.Count() }).ToDictionaryAsync(x => x.Key, ct);
        return rows.Select(o => new HelpOfferDto(o.OfferId, o.VolunteerId, names.GetValueOrDefault(o.VolunteerId, "متطوع"), o.Message, o.Status,
            ratings.TryGetValue(o.VolunteerId, out var rt) ? Math.Round(rt.Avg, 1) : null, ratings.TryGetValue(o.VolunteerId, out var rt2) ? rt2.N : 0, o.CreatedAt)).ToList();
    }
}
