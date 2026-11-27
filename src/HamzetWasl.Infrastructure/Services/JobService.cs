using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Jobs;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class JobService(HamzetWaslDbContext db, INotificationService notifications, IClock clock) : IJobService
{
    private static readonly string[] Types = ["full_time", "part_time", "internship"];
    private static readonly string[] Arrangements = ["remote", "onsite", "hybrid"];
    private static readonly string[] Statuses = ["submitted", "reviewing", "interview", "accepted", "rejected"];

    private static AppException Invalid(string field, string message) =>
        AppException.Validation(new Dictionary<string, string[]> { [field] = [message] });

    private async Task<Organization> ApprovedOrgAsync(Guid ownerUserId, CancellationToken ct)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId, ct) ?? throw AppException.Forbidden();
        if (org.ApprovalStatus != "approved") throw AppException.Forbidden(message: "لازم الجهة تتعتمد الأول عشان تنشر وظائف.");
        return org;
    }

    private IQueryable<Job> Live() => db.Jobs.Where(j => j.Status == "published" && (j.ExpiresAt == null || j.ExpiresAt > clock.UtcNow) && j.Organization.ApprovalStatus == "approved");

    public async Task<PagedResult<JobDto>> SearchAsync(Guid? viewerId, string? q, string? employmentType, string? workArrangement, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = Live();
        if (!string.IsNullOrWhiteSpace(q)) { var t = q.Trim(); query = query.Where(j => j.Title.Contains(t) || j.Description.Contains(t) || j.Location.Contains(t)); }
        if (employmentType is not null && Types.Contains(employmentType)) query = query.Where(j => j.EmploymentType == employmentType);
        if (workArrangement is not null && Arrangements.Contains(workArrangement)) query = query.Where(j => j.WorkArrangement == workArrangement);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(j => j.PublishedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<JobDto>(await ToDtosAsync(rows, viewerId, false, ct), page, pageSize, total);
    }

    public async Task<JobDto> GetAsync(Guid? viewerId, Guid jobId, CancellationToken ct = default)
    {
        var j = await Live().FirstOrDefaultAsync(x => x.JobId == jobId, ct) ?? throw AppException.NotFound();
        return (await ToDtosAsync([j], viewerId, false, ct))[0];
    }

    public async Task<JobDto> CreateAsync(Guid ownerUserId, CreateJobRequest r, CancellationToken ct = default)
    {
        var org = await ApprovedOrgAsync(ownerUserId, ct);
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 200) throw Invalid("title", "عنوان الوظيفة مطلوب (حتى 200 حرف).");
        if (string.IsNullOrWhiteSpace(r.Description)) throw Invalid("description", "وصف الوظيفة مطلوب.");
        if (!Types.Contains(r.EmploymentType)) throw Invalid("employmentType", "نوع التوظيف غير صحيح.");
        if (!Arrangements.Contains(r.WorkArrangement)) throw Invalid("workArrangement", "ترتيب العمل غير صحيح.");
        if (string.IsNullOrWhiteSpace(r.Location)) throw Invalid("location", "المكان مطلوب.");
        if (string.IsNullOrWhiteSpace(r.AccessibilityInformation)) throw Invalid("accessibilityInformation", "اكتب معلومات الإتاحة في بيئة العمل.");
        if (r.ExpiresAt is { } ex && ex <= clock.UtcNow) throw Invalid("expiresAt", "تاريخ الانتهاء لازم يكون في المستقبل.");
        var job = new Job
        {
            JobId = Guid.NewGuid(), OrganizationId = org.OrganizationId, Title = r.Title.Trim(), Description = r.Description.Trim(),
            EmploymentType = r.EmploymentType, WorkArrangement = r.WorkArrangement, Location = r.Location.Trim(),
            AccessibilityInformation = r.AccessibilityInformation.Trim(), Status = "published", PublishedAt = clock.UtcNow, ExpiresAt = r.ExpiresAt
        };
        db.Jobs.Add(job);
        var ids = (r.AccommodationIds ?? []).Distinct().ToList();
        if (ids.Count > 0)
        {
            var valid = await db.Accommodations.Where(a => ids.Contains(a.AccommodationId)).Select(a => a.AccommodationId).ToListAsync(ct);
            foreach (var id in valid) db.JobAccommodations.Add(new JobAccommodation { JobId = job.JobId, AccommodationId = id, Active = true });
        }
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([job], ownerUserId, true, ct))[0];
    }

    public async Task<IReadOnlyList<JobDto>> MineAsync(Guid ownerUserId, CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.OwnerUserId == ownerUserId, ct) ?? throw AppException.Forbidden();
        var rows = await db.Jobs.Where(j => j.OrganizationId == org.OrganizationId).OrderByDescending(j => j.PublishedAt).Take(100).ToListAsync(ct);
        return await ToDtosAsync(rows, ownerUserId, true, ct);
    }

    public async Task CloseAsync(Guid userId, bool isAdmin, Guid jobId, CancellationToken ct = default)
    {
        var j = await db.Jobs.Include(x => x.Organization).FirstOrDefaultAsync(x => x.JobId == jobId, ct) ?? throw AppException.NotFound();
        if (!isAdmin && j.Organization.OwnerUserId != userId) throw AppException.Forbidden();
        j.Status = "closed";
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(Guid userId, Guid jobId, ApplyRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.ApplicationText)) throw Invalid("applicationText", "اكتب رسالة التقديم.");
        if (r.ApplicationText.Length > 4000) throw Invalid("applicationText", "الرسالة طويلة.");
        var job = await Live().Include(j => j.Organization).FirstOrDefaultAsync(j => j.JobId == jobId, ct) ?? throw AppException.NotFound();
        if (await db.JobApplications.AnyAsync(a => a.JobId == jobId && a.ApplicantId == userId, ct)) throw AppException.Conflict(message: "قدمت على الوظيفة دي قبل كده.");
        var app = new JobApplication
        {
            ApplicationId = Guid.NewGuid(), JobId = jobId, ApplicantId = userId, ApplicationText = r.ApplicationText.Trim(),
            VoluntarilySharedNeeds = string.IsNullOrWhiteSpace(r.VoluntarilySharedNeeds) ? null : r.VoluntarilySharedNeeds.Trim(),
            Status = "submitted", SubmittedAt = clock.UtcNow
        };
        db.JobApplications.Add(app);
        await db.SaveChangesAsync(ct);
        await notifications.NotifyAsync(job.Organization.OwnerUserId, NotificationTypes.JobApplicationReceived, "وصل طلب تقديم جديد على وظيفة.", "job", jobId, ct);
    }

    public async Task<IReadOnlyList<ApplicationDto>> MyApplicationsAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.JobApplications.Include(a => a.Job).Where(a => a.ApplicantId == userId).OrderByDescending(a => a.SubmittedAt).Take(100).ToListAsync(ct);
        return rows.Select(a => new ApplicationDto(a.ApplicationId, a.JobId, a.Job.Title, userId, "", a.ApplicationText, a.VoluntarilySharedNeeds, a.Status, a.SubmittedAt)).ToList();
    }

    public async Task<IReadOnlyList<ApplicationDto>> JobApplicationsAsync(Guid ownerUserId, Guid jobId, CancellationToken ct = default)
    {
        var job = await db.Jobs.Include(j => j.Organization).FirstOrDefaultAsync(j => j.JobId == jobId, ct) ?? throw AppException.NotFound();
        if (job.Organization.OwnerUserId != ownerUserId) throw AppException.Forbidden();
        var rows = await db.JobApplications.Where(a => a.JobId == jobId).OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);
        var ids = rows.Select(a => a.ApplicantId).ToList();
        var names = await db.UserProfiles.Where(p => ids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        return rows.Select(a => new ApplicationDto(a.ApplicationId, jobId, job.Title, a.ApplicantId, names.GetValueOrDefault(a.ApplicantId, "متقدم"), a.ApplicationText, a.VoluntarilySharedNeeds, a.Status, a.SubmittedAt)).ToList();
    }

    public async Task ChangeStatusAsync(Guid ownerUserId, Guid applicationId, string status, CancellationToken ct = default)
    {
        if (!Statuses.Contains(status) || status == "submitted") throw Invalid("status", "الحالة غير صحيحة (reviewing, interview, accepted, rejected).");
        var a = await db.JobApplications.Include(x => x.Job).ThenInclude(j => j.Organization).FirstOrDefaultAsync(x => x.ApplicationId == applicationId, ct) ?? throw AppException.NotFound();
        if (a.Job.Organization.OwnerUserId != ownerUserId) throw AppException.Forbidden();
        if (a.Status == status) return;
        db.ApplicationStatusChanges.Add(new ApplicationStatusChange { ChangeId = Guid.NewGuid(), ApplicationId = a.ApplicationId, ActorUserId = ownerUserId, PreviousStatus = a.Status, NewStatus = status, ChangedAt = clock.UtcNow });
        a.Status = status;
        await db.SaveChangesAsync(ct);
        await notifications.NotifyAsync(a.ApplicantId, NotificationTypes.JobApplicationStatus, "تم تحديث حالة طلب تقديمك على وظيفة.", "job", a.JobId, ct);
    }

    private async Task<List<JobDto>> ToDtosAsync(List<Job> rows, Guid? viewer, bool withCounts, CancellationToken ct)
    {
        var ids = rows.Select(j => j.JobId).ToList();
        var oids = rows.Select(j => j.OrganizationId).Distinct().ToList();
        var orgs = await db.Organizations.Where(o => oids.Contains(o.OrganizationId)).ToDictionaryAsync(o => o.OrganizationId, o => o.Name, ct);
        var acc = await db.JobAccommodations.Where(x => ids.Contains(x.JobId) && x.Active).Join(db.Accommodations, x => x.AccommodationId, a => a.AccommodationId, (x, a) => new { x.JobId, a.Name }).ToListAsync(ct);
        var accBy = acc.GroupBy(x => x.JobId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name).ToList());
        var counts = withCounts ? await db.JobApplications.Where(a => ids.Contains(a.JobId)).GroupBy(a => a.JobId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct) : new();
        var applied = viewer is { } v ? (await db.JobApplications.Where(a => ids.Contains(a.JobId) && a.ApplicantId == v).Select(a => a.JobId).ToListAsync(ct)).ToHashSet() : new HashSet<Guid>();
        return rows.Select(j => new JobDto(j.JobId, j.OrganizationId, orgs.GetValueOrDefault(j.OrganizationId, ""), j.Title, j.Description, j.EmploymentType, j.WorkArrangement, j.Location,
            j.AccessibilityInformation, accBy.TryGetValue(j.JobId, out var l) ? l : [], j.Status, j.PublishedAt, j.ExpiresAt, counts.GetValueOrDefault(j.JobId), applied.Contains(j.JobId))).ToList();
    }
}
