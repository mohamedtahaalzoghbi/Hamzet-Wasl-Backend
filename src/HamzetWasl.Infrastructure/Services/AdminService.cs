using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

public sealed class AdminService : IAdminService
{
    private readonly HamzetWaslDbContext _db;
    private readonly IFileStorage _storage;
    private readonly StorageKeyProtector _protector;
    private readonly INotificationService _notifications;
    private readonly IClock _clock;
    private readonly StorageOptions _storageOptions;
    private readonly RetentionOptions _retention;

    public AdminService(
        HamzetWaslDbContext db,
        IFileStorage storage,
        StorageKeyProtector protector,
        INotificationService notifications,
        IClock clock,
        IOptions<StorageOptions> storageOptions,
        IOptions<RetentionOptions> retention)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
        _notifications = notifications;
        _clock = clock;
        _storageOptions = storageOptions.Value;
        _retention = retention.Value;
    }

    public async Task<PagedResult<VolunteerReviewListItemDto>> GetVolunteerReviewsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.VerificationReviews.AsNoTracking()
            .Include(r => r.Volunteer).ThenInclude(v => v.User).ThenInclude(u => u.UserProfile)
            .Where(r => r.Status != VerificationReviewStatuses.Draft && r.Status != VerificationReviewStatuses.Cancelled);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.InterviewAt ?? DateTime.MinValue)
            .ThenByDescending(r => r.ReviewId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new VolunteerReviewListItemDto(
                r.ReviewId,
                r.VolunteerId,
                r.Volunteer.User.UserProfile != null ? r.Volunteer.User.UserProfile.DisplayName : null,
                r.Volunteer.User.Email,
                r.Status,
                r.InterviewAt,
                r.DecidedAt))
            .ToListAsync(ct);

        return new PagedResult<VolunteerReviewListItemDto>(items, page, pageSize, total);
    }

    public async Task<VolunteerReviewDetailDto> GetVolunteerReviewAsync(Guid reviewId, CancellationToken ct = default)
    {
        var review = await _db.VerificationReviews.AsNoTracking()
            .Include(r => r.Volunteer).ThenInclude(v => v.User).ThenInclude(u => u.UserProfile)
            .Include(r => r.Volunteer).ThenInclude(v => v.VolunteerSocialLinks)
            .Include(r => r.VerificationEvidences)
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct)
            ?? throw AppException.NotFound("طلب التحقق غير موجود.");

        var evidence = review.VerificationEvidences.Select(e =>
        {
            var key = _protector.Unprotect(e.EncryptedStorageKey);
            var signed = _storage.CreateSignedUrl(key, TimeSpan.FromMinutes(_storageOptions.SignedUrlMinutes));
            return new EvidenceDto(e.EvidenceId, e.EvidenceType, signed.Url, signed.ExpiresAt);
        }).ToList();

        return new VolunteerReviewDetailDto(
            review.ReviewId,
            review.VolunteerId,
            review.Volunteer.User.UserProfile?.DisplayName,
            review.Volunteer.User.Email,
            review.Volunteer.User.Phone,
            review.Volunteer.HelpMode,
            review.Volunteer.ServiceArea,
            review.Volunteer.SkillLevel,
            review.Volunteer.WeeklyHours,
            review.Status,
            review.InterviewAt,
            review.DecidedAt,
            evidence,
            review.Volunteer.VolunteerSocialLinks.OrderBy(l => l.Platform)
                .Select(l => new SocialLinkAdminDto(l.Platform, l.Url)).ToList());
    }

    public async Task DecideVolunteerReviewAsync(Guid reviewId, Guid reviewerId, DecisionRequest request, CancellationToken ct = default)
    {
        var review = await _db.VerificationReviews
            .Include(r => r.Volunteer)
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct)
            ?? throw AppException.NotFound("طلب التحقق غير موجود.");

        if (review.Status is not (VerificationReviewStatuses.Pending or VerificationReviewStatuses.InterviewScheduled))
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اتخاذ قرار بشأن هذا الطلب بالفعل.");
        }

        var now = _clock.UtcNow;
        review.ReviewerId = reviewerId;
        review.DecidedAt = now;
        review.Status = request.Approve ? VerificationReviewStatuses.Approved : VerificationReviewStatuses.Rejected;
        review.Volunteer.VerificationStatus = request.Approve ? VerificationStatuses.Approved : VerificationStatuses.Rejected;

        var evidence = await _db.VerificationEvidences.Where(e => e.ReviewId == reviewId).ToListAsync(ct);
        foreach (var item in evidence)
        {
            item.RetentionUntil = now.AddDays(_retention.VerificationEvidenceDays);
        }

        AddAudit(reviewerId, request.Approve ? AdminActions.ApproveVolunteer : AdminActions.RejectVolunteer,
            "verification_review", reviewId, AuditOutcomes.Success, now);

        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(
            review.VolunteerId,
            request.Approve ? NotificationTypes.VerificationApproved : NotificationTypes.VerificationRejected,
            request.Approve ? "تم اعتماد حسابك كمتطوع." : "تم رفض طلب التحقق. يمكنك إعادة التقديم.",
            "verification_review",
            reviewId,
            ct);
    }

    public async Task<PagedResult<OrganizationReviewListItemDto>> GetOrganizationReviewsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.OrganizationReviews.AsNoTracking().Include(r => r.Organization).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new OrganizationReviewListItemDto(
                r.ReviewId,
                r.OrganizationId,
                r.Organization.Name,
                r.Organization.OrganizationType,
                r.Organization.ApprovalStatus,
                r.Status,
                r.RequestedAt,
                r.DecidedAt))
            .ToListAsync(ct);

        return new PagedResult<OrganizationReviewListItemDto>(items, page, pageSize, total);
    }

    public async Task<OrganizationReviewDetailDto> GetOrganizationReviewAsync(Guid reviewId, CancellationToken ct = default)
    {
        var review = await _db.OrganizationReviews.AsNoTracking()
            .Include(r => r.Organization).ThenInclude(o => o.OrganizationDocuments)
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct)
            ?? throw AppException.NotFound("طلب المؤسسة غير موجود.");

        var documents = review.Organization.OrganizationDocuments.Select(d =>
        {
            var signed = _storage.CreateSignedUrl(d.StorageKey, TimeSpan.FromMinutes(_storageOptions.SignedUrlMinutes));
            return new OrganizationDocumentAdminDto(d.DocumentType, signed.Url, signed.ExpiresAt, d.UploadedAt);
        }).ToList();

        var org = review.Organization;
        return new OrganizationReviewDetailDto(
            review.ReviewId, org.OrganizationId, org.Name, org.OrganizationType, org.Field, org.Description,
            org.Governorate, org.City, org.Website, org.ContactInformation, org.ApprovalStatus, review.Status,
            review.RequestedAt, review.DecidedAt, review.DecisionNotes, documents);
    }

    public async Task DecideOrganizationReviewAsync(Guid reviewId, Guid reviewerId, DecisionRequest request, CancellationToken ct = default)
    {
        var review = await _db.OrganizationReviews
            .Include(r => r.Organization)
            .FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct)
            ?? throw AppException.NotFound("طلب المؤسسة غير موجود.");

        if (review.Status != OrganizationReviewStatuses.Pending)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اتخاذ قرار بشأن هذا الطلب بالفعل.");
        }

        var now = _clock.UtcNow;
        review.ReviewerId = reviewerId;
        review.DecidedAt = now;
        review.DecisionNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        review.Status = request.Approve ? OrganizationReviewStatuses.Approved : OrganizationReviewStatuses.Rejected;
        review.Organization.ApprovalStatus = request.Approve
            ? OrganizationApprovalStatuses.Approved
            : OrganizationApprovalStatuses.Rejected;

        var owner = await _db.UserAccounts.FirstAsync(a => a.UserId == review.Organization.OwnerUserId, ct);
        if (request.Approve)
        {
            owner.AccountStatus = AccountStatuses.Active;
        }

        AddAudit(reviewerId, request.Approve ? AdminActions.ApproveOrganization : AdminActions.RejectOrganization,
            "organization_review", reviewId, AuditOutcomes.Success, now);

        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(
            review.Organization.OwnerUserId,
            request.Approve ? NotificationTypes.OrganizationApproved : NotificationTypes.OrganizationRejected,
            request.Approve ? "تم اعتماد مؤسستك. يمكنك الآن نشر الفرص والخدمات." : "تم رفض طلب المؤسسة. راجع الملاحظات وأعد التقديم.",
            "organization_review",
            reviewId,
            ct);
    }

    public async Task<IReadOnlyList<InterviewSlotAdminDto>> GetInterviewSlotsAsync(CancellationToken ct = default)
    {
        return await _db.InterviewSlots.AsNoTracking().OrderBy(s => s.StartsAt)
            .Select(s => new InterviewSlotAdminDto(s.InterviewSlotId, s.ReviewerId, s.StartsAt, s.DurationMinutes, s.Status))
            .ToListAsync(ct);
    }

    public async Task<InterviewSlotAdminDto> CreateInterviewSlotAsync(Guid reviewerId, CreateInterviewSlotRequest request, CancellationToken ct = default)
    {
        var startsAt = request.StartsAt.Kind == DateTimeKind.Utc
            ? request.StartsAt
            : request.StartsAt.ToUniversalTime();

        if (startsAt <= _clock.UtcNow)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["startsAt"] = ["وقت الموعد يجب أن يكون في المستقبل."] });
        }

        if (await _db.InterviewSlots.AnyAsync(s => s.ReviewerId == reviewerId && s.StartsAt == startsAt, ct))
        {
            throw AppException.Conflict(ErrorCodes.Conflict, "يوجد موعد بنفس الوقت.");
        }

        var slot = new InterviewSlot
        {
            InterviewSlotId = Guid.NewGuid(),
            ReviewerId = reviewerId,
            StartsAt = startsAt,
            DurationMinutes = request.DurationMinutes,
            Status = InterviewSlotStatuses.Available
        };
        _db.InterviewSlots.Add(slot);
        AddAudit(reviewerId, AdminActions.CreateInterviewSlot, "interview_slot", slot.InterviewSlotId,
            AuditOutcomes.Success, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        return new InterviewSlotAdminDto(slot.InterviewSlotId, slot.ReviewerId, slot.StartsAt, slot.DurationMinutes, slot.Status);
    }

    public async Task DeleteInterviewSlotAsync(Guid reviewerId, Guid slotId, CancellationToken ct = default)
    {
        var slot = await _db.InterviewSlots.FirstOrDefaultAsync(s => s.InterviewSlotId == slotId, ct)
                   ?? throw AppException.NotFound("الموعد غير موجود.");

        if (slot.Status == InterviewSlotStatuses.Booked ||
            await _db.VerificationReviews.AnyAsync(r => r.InterviewSlotId == slotId, ct))
        {
            throw new AppException(422, ErrorCodes.InvalidState, "لا يمكن حذف موعد محجوز.");
        }

        _db.InterviewSlots.Remove(slot);
        AddAudit(reviewerId, AdminActions.DeleteInterviewSlot, "interview_slot", slotId,
            AuditOutcomes.Success, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetUserSuspensionAsync(Guid adminId, Guid userId, SuspendUserRequest request, CancellationToken ct = default)
    {
        if (adminId == userId) throw AppException.Business(ErrorCodes.SelfActionNotAllowed);

        var account = await _db.UserAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct)
                      ?? throw AppException.NotFound("المستخدم غير موجود.");

        var now = _clock.UtcNow;
        if (request.Suspend)
        {
            account.AccountStatus = AccountStatuses.Suspended;
        }
        else
        {
            var organization = await _db.Organizations
                .FirstOrDefaultAsync(o => o.OwnerUserId == userId, ct);
            account.AccountStatus = organization is not null &&
                                    organization.ApprovalStatus != OrganizationApprovalStatuses.Approved
                ? AccountStatuses.PendingApproval
                : AccountStatuses.Active;
        }

        if (request.Suspend)
        {
            await _db.UserTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        }

        AddAudit(adminId, request.Suspend ? AdminActions.SuspendUser : AdminActions.ReactivateUser,
            "user_account", userId, AuditOutcomes.Success, now);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(
            userId,
            request.Suspend ? NotificationTypes.AccountSuspended : NotificationTypes.AccountReactivated,
            request.Suspend
                ? "تم إيقاف حسابك. تواصل مع الدعم إن كنت ترى أن هذا خطأ."
                : "تمت إعادة تفعيل حسابك.",
            "user_account",
            userId,
            ct);
    }

    public async Task<PagedResult<DeletionRequestDto>> GetDeletionRequestsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.DeletionRequests.AsNoTracking().Include(r => r.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new DeletionRequestDto(r.DeletionRequestId, r.UserId, r.User.Email, r.Status, r.RequestedAt, r.ResolvedAt))
            .ToListAsync(ct);

        return new PagedResult<DeletionRequestDto>(items, page, pageSize, total);
    }

    public async Task ResolveDeletionRequestAsync(Guid adminId, Guid deletionRequestId, ResolveDeletionRequestRequest request, CancellationToken ct = default)
    {
        var deletion = await _db.DeletionRequests.FirstOrDefaultAsync(r => r.DeletionRequestId == deletionRequestId, ct)
                       ?? throw AppException.NotFound("طلب الحذف غير موجود.");

        if (deletion.Status != DeletionRequestStatuses.Pending)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تمت معالجة طلب الحذف بالفعل.");
        }

        var now = _clock.UtcNow;
        if (request.Approve)
        {
            await AnonymizeUserAsync(deletion.UserId, ct);
            deletion.Status = DeletionRequestStatuses.Completed;
        }
        else
        {
            deletion.Status = DeletionRequestStatuses.Rejected;
        }

        deletion.ResolvedAt = now;
        AddAudit(adminId, AdminActions.ResolveDeletionRequest, "deletion_request", deletionRequestId,
            AuditOutcomes.Success, now);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AdminAuditDto>> GetAuditAsync(int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.AdminAudits.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.OccurredAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AdminAuditDto(a.AuditId, a.ActorUserId, a.Action, a.TargetType, a.TargetId, a.Outcome, a.OccurredAt))
            .ToListAsync(ct);
        return new PagedResult<AdminAuditDto>(items, page, pageSize, total);
    }

    private void AddAudit(Guid actorId, string action, string targetType, Guid targetId, string outcome, DateTime now)
    {
        _db.AdminAudits.Add(new AdminAudit
        {
            AuditId = Guid.NewGuid(),
            ActorUserId = actorId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Outcome = outcome,
            OccurredAt = now
        });
    }

    private async Task AnonymizeUserAsync(Guid userId, CancellationToken ct)
    {
        var account = await _db.UserAccounts
            .Include(a => a.UserProfile)
            .Include(a => a.AccessibilityProfile)
            .Include(a => a.VolunteerProfile).ThenInclude(v => v!.VerificationReviews).ThenInclude(r => r.VerificationEvidences)
            .Include(a => a.Organization).ThenInclude(o => o!.OrganizationDocuments)
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

        if (account is null) return;

        var anonymizedEmail = $"deleted-{userId:N}@deleted.local";
        var now = _clock.UtcNow;

        account.Email = anonymizedEmail;
        account.EmailVerified = false;
        account.Phone = null;
        account.PhoneVerified = false;
        account.AccountStatus = AccountStatuses.Suspended;

        if (account.UserProfile is not null)
        {
            var oldAvatar = account.UserProfile.AvatarStorageKey;
            account.UserProfile.DisplayName = "حساب محذوف";
            account.UserProfile.Bio = null;
            account.UserProfile.City = null;
            account.UserProfile.AvatarStorageKey = null;
            if (!string.IsNullOrWhiteSpace(oldAvatar)) await _storage.DeleteAsync(oldAvatar, ct);
        }

        var appUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (appUser is not null)
        {
            appUser.UserName = anonymizedEmail;
            appUser.NormalizedUserName = anonymizedEmail.ToUpperInvariant();
            appUser.Email = anonymizedEmail;
            appUser.NormalizedEmail = anonymizedEmail.ToUpperInvariant();
            appUser.EmailConfirmed = false;
            appUser.PhoneNumber = null;
            appUser.PhoneNumberConfirmed = false;
            appUser.PasswordHash = null;
            appUser.SecurityStamp = Guid.NewGuid().ToString();
            appUser.LockoutEnabled = true;
            appUser.LockoutEnd = DateTimeOffset.MaxValue;
        }

        await _db.UserTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await _db.Notifications.Where(n => n.RecipientId == userId).ExecuteDeleteAsync(ct);

        if (account.VolunteerProfile is not null)
        {
            foreach (var review in account.VolunteerProfile.VerificationReviews)
            {
                foreach (var evidence in review.VerificationEvidences)
                {
                    try
                    {
                        await _storage.DeleteAsync(_protector.Unprotect(evidence.EncryptedStorageKey), ct);
                    }
                    catch (Exception)
                    {
                        // Key corruption should not block account deletion.
                    }
                }
            }

            var reviewIds = account.VolunteerProfile.VerificationReviews.Select(r => r.ReviewId).ToList();
            var slotIds = account.VolunteerProfile.VerificationReviews
                .Where(r => r.InterviewSlotId.HasValue).Select(r => r.InterviewSlotId!.Value).ToList();

            if (slotIds.Count > 0)
            {
                await _db.InterviewSlots.Where(s => slotIds.Contains(s.InterviewSlotId) && s.Status == InterviewSlotStatuses.Booked)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, InterviewSlotStatuses.Available), ct);
            }

            if (reviewIds.Count > 0)
            {
                await _db.VerificationEvidences.Where(e => reviewIds.Contains(e.ReviewId)).ExecuteDeleteAsync(ct);
                await _db.VerificationReviews.Where(r => reviewIds.Contains(r.ReviewId)).ExecuteDeleteAsync(ct);
            }

            await _db.VolunteerSocialLinks.Where(l => l.VolunteerId == userId).ExecuteDeleteAsync(ct);
            await _db.AvailabilitySlots.Where(s => s.VolunteerId == userId).ExecuteDeleteAsync(ct);
            await _db.TrainingAttempts.Where(a => a.VolunteerId == userId).ExecuteDeleteAsync(ct);
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.VOLUNTEER_SKILL WHERE volunteer_id = {userId}", ct);
            await _db.VolunteerProfiles.Where(v => v.UserId == userId).ExecuteDeleteAsync(ct);
        }

        if (account.AccessibilityProfile is not null)
        {
            _db.AccessibilityProfiles.Remove(account.AccessibilityProfile);
        }

        if (account.Organization is not null)
        {
            foreach (var doc in account.Organization.OrganizationDocuments)
            {
                await _storage.DeleteAsync(doc.StorageKey, ct);
            }

            await _db.OrganizationDocuments.Where(d => d.OrganizationId == account.Organization.OrganizationId).ExecuteDeleteAsync(ct);
            account.Organization.Name = "مؤسسة محذوفة";
            account.Organization.Description = null;
            account.Organization.Website = null;
            account.Organization.ContactInformation = null;
            account.Organization.LogoStorageKey = null;
            account.Organization.CoverStorageKey = null;
            account.Organization.ApprovalStatus = OrganizationApprovalStatuses.Rejected;
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.USER_INTEREST WHERE user_id = {userId}", ct);
        await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.USER_SKILL WHERE user_id = {userId}", ct);
        await _db.UserFollows.Where(f => f.FollowerId == userId || f.FollowedId == userId).ExecuteDeleteAsync(ct);

        account.CreatedAt = now;
    }
}
