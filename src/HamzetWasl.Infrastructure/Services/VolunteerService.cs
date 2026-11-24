using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Domain.Rules;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

public sealed class VolunteerService : IVolunteerService
{
    private readonly HamzetWaslDbContext _db;
    private readonly IFileStorage _storage;
    private readonly StorageKeyProtector _protector;
    private readonly INotificationService _notifications;
    private readonly IGeoCatalog _geo;
    private readonly IClock _clock;
    private readonly RetentionOptions _retention;

    public VolunteerService(
        HamzetWaslDbContext db,
        IFileStorage storage,
        StorageKeyProtector protector,
        INotificationService notifications,
        IGeoCatalog geo,
        IClock clock,
        IOptions<RetentionOptions> retention)
    {
        _db = db;
        _storage = storage;
        _protector = protector;
        _notifications = notifications;
        _geo = geo;
        _clock = clock;
        _retention = retention.Value;
    }

    public async Task<VolunteerProfileResponse> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        return Map(profile);
    }

    public async Task<VolunteerProfileResponse> UpdateProfileAsync(Guid userId, UpdateVolunteerProfileRequest request, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);

        var ids = request.SkillIds.Distinct().ToArray();
        var skills = await _db.Skills.Where(s => ids.Contains(s.SkillId)).ToListAsync(ct);
        if (skills.Count != ids.Length)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["skillIds"] = ["بعض المهارات غير موجودة."] });
        }

        profile.Introduction = string.IsNullOrWhiteSpace(request.Introduction) ? null : request.Introduction.Trim();
        profile.HelpMode = request.HelpMode;
        profile.SkillLevel = request.SkillLevel;
        profile.WeeklyHours = request.WeeklyHours;
        profile.ExperienceNotes = string.IsNullOrWhiteSpace(request.ExperienceNotes) ? null : request.ExperienceNotes.Trim();
        profile.Skills.Clear();
        foreach (var skill in skills) profile.Skills.Add(skill);

        await _db.SaveChangesAsync(ct);
        return Map(profile);
    }

    public async Task<VolunteerLocationResponse> GetLocationAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        return new VolunteerLocationResponse(profile.Governorate ?? string.Empty, profile.ServiceArea,
            profile.Latitude, profile.Longitude, profile.ServiceRadiusKm);
    }

    public async Task<VolunteerLocationResponse> UpdateLocationAsync(Guid userId, VolunteerLocationRequest request, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        // التحقق من الكتالوج الديناميكي (قاعدة البيانات) بدلاً من القائمة الثابتة.
        var governorate = await _geo.FindGovernorateAsync(request.Governorate, ct)
                          ?? throw AppException.Validation(new Dictionary<string, string[]> { ["governorate"] = ["المحافظة غير صحيحة."] });
        var area = await _geo.FindAreaAsync(governorate, request.ServiceArea, ct)
                   ?? throw AppException.Validation(new Dictionary<string, string[]> { ["serviceArea"] = ["المنطقة غير صحيحة."] });

        profile.Governorate = governorate.Key;
        profile.ServiceArea = area.Key;
        profile.Latitude = request.Latitude;
        profile.Longitude = request.Longitude;
        profile.ServiceRadiusKm = request.ServiceRadiusKm;
        await _db.SaveChangesAsync(ct);

        return new VolunteerLocationResponse(profile.Governorate, profile.ServiceArea,
            profile.Latitude, profile.Longitude, profile.ServiceRadiusKm);
    }

    public async Task<VerificationResponse> GetVerificationAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        var review = await GetLatestReviewAsync(userId, ct);
        return BuildVerification(profile, review);
    }

    public async Task UploadEvidenceAsync(Guid userId, string evidenceType, FileUpload file, CancellationToken ct = default)
    {
        if (!EvidenceTypes.All.Contains(evidenceType))
        {
            throw new AppException(400, ErrorCodes.ValidationError, "نوع المستند غير صحيح.");
        }

        var profile = await GetVolunteerAsync(userId, ct);
        if (!profile.User.EmailVerified)
        {
            throw new AppException(422, ErrorCodes.EmailNotVerified);
        }

        if (!profile.User.PhoneVerified)
        {
            throw new AppException(422, ErrorCodes.PhoneNotVerified);
        }

        var review = await GetOrCreateReviewAsync(profile, ct);
        if (review.Status != VerificationReviewStatuses.Draft)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "لا يمكن تعديل مستندات التحقق بعد حجز المقابلة.");
        }

        // Keep the replaced storage keys so the old files can be removed after
        // the database row is gone; otherwise replaced files stay on disk forever.
        var replacedKeys = await _db.VerificationEvidences
            .Where(e => e.ReviewId == review.ReviewId && e.EvidenceType == evidenceType)
            .Select(e => e.EncryptedStorageKey)
            .ToListAsync(ct);

        var stored = await _storage.SaveAsync(file, FileCategory.VerificationEvidence, userId, ct);

        // The row swap is one transaction: a failure in the middle can never lose the
        // old evidence record while the new one is missing. If the swap fails, the
        // just-stored file is removed again so it does not stay on disk unreferenced.
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await _db.VerificationEvidences
                .Where(e => e.ReviewId == review.ReviewId && e.EvidenceType == evidenceType)
                .ExecuteDeleteAsync(ct);

            _db.VerificationEvidences.Add(new VerificationEvidence
            {
                EvidenceId = Guid.NewGuid(),
                ReviewId = review.ReviewId,
                EvidenceType = evidenceType,
                EncryptedStorageKey = _protector.Protect(stored.Key),
                RetentionUntil = _clock.UtcNow.AddDays(_retention.VerificationEvidenceDays)
            });
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            try
            {
                await _storage.DeleteAsync(stored.Key, CancellationToken.None);
            }
            catch (Exception)
            {
                // Best-effort compensation; bounded to this failure.
            }

            throw;
        }

        foreach (var replacedKey in replacedKeys)
        {
            try
            {
                await _storage.DeleteAsync(_protector.Unprotect(replacedKey), ct);
            }
            catch (Exception)
            {
                // Key corruption or a storage failure must not fail the upload; the
                // replaced file is already dereferenced and stays bounded to this failure.
            }
        }
    }

    public async Task SetSocialLinksAsync(Guid userId, IReadOnlyList<SocialLinkInput> links, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        var duplicate = links.GroupBy(l => l.Platform).Any(g => g.Count() > 1);
        if (duplicate)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["links"] = ["لا يمكن إضافة أكثر من رابط لنفس المنصة."] });
        }

        foreach (var old in profile.VolunteerSocialLinks.ToList())
        {
            _db.VolunteerSocialLinks.Remove(old);
        }

        profile.VolunteerSocialLinks.Clear();
        foreach (var link in links)
        {
            var socialLink = new VolunteerSocialLink
            {
                VolunteerId = userId,
                Platform = link.Platform,
                Url = link.Url.Trim()
            };
            _db.VolunteerSocialLinks.Add(socialLink);
            profile.VolunteerSocialLinks.Add(socialLink);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InterviewSlotDto>> GetInterviewSlotsAsync(DateOnly? date, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var query = _db.InterviewSlots.AsNoTracking().Where(s => s.StartsAt > now);
        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(s => s.StartsAt >= start && s.StartsAt < end);
        }

        return await query.OrderBy(s => s.StartsAt)
            .Select(s => new InterviewSlotDto(s.InterviewSlotId, s.StartsAt, s.DurationMinutes, s.Status))
            .ToListAsync(ct);
    }

    public async Task<VerificationResponse> BookInterviewAsync(Guid userId, Guid slotId, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        if (profile.VerificationStatus == VerificationStatuses.Approved)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اعتماد حسابك بالفعل.");
        }

        var review = await GetOrCreateReviewAsync(profile, ct);
        if (review.Status == VerificationReviewStatuses.InterviewScheduled || review.InterviewSlotId is not null)
        {
            throw new AppException(409, ErrorCodes.InterviewAlreadyBooked);
        }

        var latest = await GetLatestReviewAsync(userId, ct);
        if (!ArePreInterviewStepsComplete(profile, latest))
        {
            throw new AppException(422, ErrorCodes.VerificationIncomplete);
        }

        var now = _clock.UtcNow;
        var verificationLevel = profile.HelpMode == HelpModes.OnlineAndField ? VerificationLevels.Field : VerificationLevels.Basic;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Atomic slot claim: exactly one volunteer can flip an available slot to booked.
        var slotClaimed = await _db.InterviewSlots
            .Where(s => s.InterviewSlotId == slotId && s.Status == InterviewSlotStatuses.Available && s.StartsAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, InterviewSlotStatuses.Booked), ct);

        if (slotClaimed == 0)
        {
            throw new AppException(409, ErrorCodes.InterviewSlotUnavailable);
        }

        var slot = await _db.InterviewSlots.AsNoTracking().FirstAsync(s => s.InterviewSlotId == slotId, ct);

        // Atomic review claim: only a still-open review can take the slot, so two
        // concurrent bookings can never both win and orphan the first one's slot.
        var reviewClaimed = await _db.VerificationReviews
            .Where(r => r.ReviewId == review.ReviewId && r.InterviewSlotId == null
                && (r.Status == VerificationReviewStatuses.Draft || r.Status == VerificationReviewStatuses.Pending))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReviewerId, slot.ReviewerId)
                .SetProperty(r => r.InterviewSlotId, slot.InterviewSlotId)
                .SetProperty(r => r.InterviewAt, slot.StartsAt)
                .SetProperty(r => r.Status, VerificationReviewStatuses.InterviewScheduled)
                .SetProperty(r => r.VerificationLevel, verificationLevel)
                .SetProperty(r => r.DecidedAt, (DateTime?)null), ct);

        if (reviewClaimed == 0)
        {
            // Rolls the slot claim back with the transaction.
            throw new AppException(409, ErrorCodes.InterviewAlreadyBooked);
        }

        profile.VerificationStatus = VerificationStatuses.InterviewScheduled;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await _notifications.NotifyAsync(
            userId,
            NotificationTypes.InterviewBooked,
            "تم حجز موعد المقابلة بنجاح.",
            "interview_slot",
            slotId,
            ct);

        await _notifications.NotifyAsync(
            slot.ReviewerId,
            NotificationTypes.InterviewBooked,
            "حجز متطوع موعد مقابلة من مواعيدك.",
            "interview_slot",
            slotId,
            ct);

        _db.Entry(review).State = EntityState.Detached;
        return BuildVerification(profile, await GetLatestReviewAsync(userId, ct));
    }

    public async Task<VerificationResponse> ChangeInterviewAsync(Guid userId, Guid slotId, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        var review = await GetLatestReviewAsync(userId, ct)
                     ?? throw new AppException(422, ErrorCodes.VerificationIncomplete);
        if (review.Status == VerificationReviewStatuses.Approved)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اعتماد حسابك بالفعل.");
        }

        if (review.InterviewSlotId is null)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "لا يوجد موعد مقابلة لتغييره.");
        }

        var now = _clock.UtcNow;
        var oldSlotId = review.InterviewSlotId.Value;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // Atomic claim of the new slot: exactly one volunteer can flip it to booked.
        var slotClaimed = await _db.InterviewSlots
            .Where(s => s.InterviewSlotId == slotId && s.Status == InterviewSlotStatuses.Available && s.StartsAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, InterviewSlotStatuses.Booked), ct);
        if (slotClaimed == 0) throw new AppException(409, ErrorCodes.InterviewSlotUnavailable);

        var slot = await _db.InterviewSlots.AsNoTracking().FirstAsync(s => s.InterviewSlotId == slotId, ct);
        var oldReviewerId = review.ReviewerId;

        // Atomic guard: the review must still hold the old slot, so a concurrent
        // cancel or change cannot make this request free a slot it no longer owns.
        var moved = await _db.VerificationReviews
            .Where(r => r.ReviewId == review.ReviewId
                && r.Status == VerificationReviewStatuses.InterviewScheduled
                && r.InterviewSlotId == oldSlotId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReviewerId, slot.ReviewerId)
                .SetProperty(r => r.InterviewSlotId, slot.InterviewSlotId)
                .SetProperty(r => r.InterviewAt, slot.StartsAt), ct);
        if (moved == 0)
        {
            // Rolls the new slot claim back with the transaction.
            throw new AppException(409, ErrorCodes.Conflict, "تغيّرت حالة الحجز. أعد المحاولة.");
        }

        if (oldSlotId != slotId)
        {
            await _db.InterviewSlots
                .Where(s => s.InterviewSlotId == oldSlotId && s.Status == InterviewSlotStatuses.Booked)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, InterviewSlotStatuses.Available), ct);
        }

        profile.VerificationStatus = VerificationStatuses.InterviewScheduled;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await _notifications.NotifyAsync(
            userId,
            NotificationTypes.InterviewChanged,
            "تم تغيير موعد المقابلة.",
            "interview_slot",
            slotId,
            ct);

        await _notifications.NotifyAsync(
            slot.ReviewerId,
            NotificationTypes.InterviewChanged,
            "نُقلت مقابلة متطوع إلى موعد من مواعيدك.",
            "interview_slot",
            slotId,
            ct);

        if (oldReviewerId.HasValue && oldReviewerId.Value != slot.ReviewerId)
        {
            await _notifications.NotifyAsync(
                oldReviewerId.Value,
                NotificationTypes.InterviewCancelled,
                "نُقلت مقابلة متطوع من موعدك إلى موعد آخر.",
                "interview_slot",
                oldSlotId,
                ct);
        }

        _db.Entry(review).State = EntityState.Detached;
        return BuildVerification(profile, await GetLatestReviewAsync(userId, ct));
    }

    public async Task<VerificationResponse> CancelInterviewAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await GetVolunteerAsync(userId, ct);
        var review = await GetLatestReviewAsync(userId, ct)
                     ?? throw new AppException(422, ErrorCodes.VerificationIncomplete);

        if (review.Status == VerificationReviewStatuses.Approved)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اعتماد حسابك بالفعل.");
        }

        var slotId = review.InterviewSlotId;
        var reviewerId = review.ReviewerId;

        // One transaction: the slot release and the review reset commit or fail together.
        // The state transition is the original one (the round returns to draft); only the
        // atomicity is new, so a concurrent cancel or decision cannot split the two writes.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var cleared = await _db.VerificationReviews
            .Where(r => r.ReviewId == review.ReviewId && r.InterviewSlotId == slotId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.InterviewSlotId, (Guid?)null)
                .SetProperty(r => r.InterviewAt, (DateTime?)null)
                .SetProperty(r => r.ReviewerId, (Guid?)null)
                .SetProperty(r => r.Status, VerificationReviewStatuses.Draft)
                .SetProperty(r => r.DecidedAt, (DateTime?)null), ct);

        if (cleared == 0)
        {
            throw new AppException(409, ErrorCodes.Conflict, "تغيّرت حالة المراجعة. أعد المحاولة.");
        }

        if (slotId is not null)
        {
            await _db.InterviewSlots
                .Where(s => s.InterviewSlotId == slotId && s.Status == InterviewSlotStatuses.Booked)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, InterviewSlotStatuses.Available), ct);
        }

        profile.VerificationStatus = VerificationStatuses.Unverified;
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await _notifications.NotifyAsync(
            userId,
            NotificationTypes.InterviewCancelled,
            "تم إلغاء موعد المقابلة.",
            "interview_slot",
            null,
            ct);

        if (reviewerId.HasValue)
        {
            await _notifications.NotifyAsync(
                reviewerId.Value,
                NotificationTypes.InterviewCancelled,
                "ألغى متطوع موعد المقابلة الذي كان محجوزًا معك.",
                "interview_slot",
                slotId,
                ct);
        }

        _db.Entry(review).State = EntityState.Detached;
        return BuildVerification(profile, await GetLatestReviewAsync(userId, ct));
    }

    public async Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailabilityAsync(Guid userId, CancellationToken ct = default)
    {
        await GetVolunteerAsync(userId, ct);
        return await _db.AvailabilitySlots.AsNoTracking()
            .Where(s => s.VolunteerId == userId)
            .OrderBy(s => s.StartsAt)
            .Select(s => new AvailabilitySlotDto(s.SlotId, s.StartsAt, s.EndsAt, s.Timezone))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AvailabilitySlotDto>> SetAvailabilityAsync(Guid userId, IReadOnlyList<AvailabilityInput> slots, CancellationToken ct = default)
    {
        await GetVolunteerAsync(userId, ct);
        var now = _clock.UtcNow;
        var ordered = slots.OrderBy(s => s.StartsAt).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].StartsAt <= now || ordered[i].EndsAt <= ordered[i].StartsAt)
            {
                throw AppException.Validation(new Dictionary<string, string[]> { ["slots"] = ["الفترات يجب أن تكون مستقبلية وصحيحة."] });
            }

            if (i > 0 && ordered[i].StartsAt < ordered[i - 1].EndsAt)
            {
                throw AppException.Validation(new Dictionary<string, string[]> { ["slots"] = ["الفترات لا يجب أن تتداخل."] });
            }
        }

        await _db.AvailabilitySlots.Where(s => s.VolunteerId == userId && s.StartsAt >= now).ExecuteDeleteAsync(ct);
        foreach (var slot in ordered)
        {
            _db.AvailabilitySlots.Add(new AvailabilitySlot
            {
                SlotId = Guid.NewGuid(),
                VolunteerId = userId,
                StartsAt = slot.StartsAt,
                EndsAt = slot.EndsAt,
                Timezone = slot.Timezone
            });
        }

        await _db.SaveChangesAsync(ct);
        return await GetAvailabilityAsync(userId, ct);
    }

    public async Task<IReadOnlyList<TrainingModuleDto>> GetTrainingAsync(Guid userId, CancellationToken ct = default)
    {
        await GetVolunteerAsync(userId, ct);
        var modules = await _db.TrainingModules.AsNoTracking().OrderBy(m => m.Title).ToListAsync(ct);
        var attempts = await _db.TrainingAttempts.AsNoTracking()
            .Where(a => a.VolunteerId == userId)
            .OrderBy(a => a.CompletedAt)
            .ToListAsync(ct);

        return modules.Select(m => new TrainingModuleDto(
            m.ModuleId, m.Title, m.Version,
            attempts.Where(a => a.ModuleId == m.ModuleId)
                .Select(a => new TrainingAttemptDto(a.AttemptId, a.Score, a.Result, a.CompletedAt))
                .ToList())).ToList();
    }

    public async Task<TrainingAttemptDto> SubmitTrainingAttemptAsync(Guid userId, Guid moduleId, decimal score, CancellationToken ct = default)
    {
        await GetVolunteerAsync(userId, ct);
        if (!await _db.TrainingModules.AnyAsync(m => m.ModuleId == moduleId, ct))
        {
            throw AppException.NotFound("وحدة التدريب غير موجودة.");
        }

        var attempt = new TrainingAttempt
        {
            AttemptId = Guid.NewGuid(),
            VolunteerId = userId,
            ModuleId = moduleId,
            Score = score,
            Result = score >= 70 ? TrainingResults.Passed : TrainingResults.Failed,
            CompletedAt = _clock.UtcNow
        };
        _db.TrainingAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
        return new TrainingAttemptDto(attempt.AttemptId, attempt.Score, attempt.Result, attempt.CompletedAt);
    }

    private async Task<VolunteerProfile> GetVolunteerAsync(Guid userId, CancellationToken ct)
    {
        var profile = await _db.VolunteerProfiles
            .Include(p => p.User).ThenInclude(u => u.Role)
            .Include(p => p.Skills)
            .Include(p => p.VolunteerSocialLinks)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is null)
        {
            if (await _db.UserAccounts.AnyAsync(a => a.UserId == userId, ct))
            {
                throw AppException.Forbidden(ErrorCodes.Forbidden, "هذا الحساب ليس حساب متطوع.");
            }

            throw AppException.NotFound();
        }

        if (profile.User.Role.RoleName != RoleNames.Volunteer)
        {
            throw AppException.Forbidden(ErrorCodes.Forbidden, "هذا الحساب ليس حساب متطوع.");
        }

        return profile;
    }

    private async Task<VerificationReview?> GetLatestReviewAsync(Guid userId, CancellationToken ct) =>
        await _db.VerificationReviews
            .Include(r => r.VerificationEvidences)
            .Where(r => r.VolunteerId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.ReviewId)
            .FirstOrDefaultAsync(ct);

    private async Task<VerificationReview> GetOrCreateReviewAsync(VolunteerProfile profile, CancellationToken ct)
    {
        var review = await _db.VerificationReviews
            .Where(r => r.VolunteerId == profile.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.ReviewId)
            .FirstOrDefaultAsync(ct);

        if (review is null)
        {
            review = new VerificationReview
            {
                ReviewId = Guid.NewGuid(),
                VolunteerId = profile.UserId,
                VerificationLevel = profile.HelpMode == HelpModes.OnlineAndField ? VerificationLevels.Field : VerificationLevels.Basic,
                Status = VerificationReviewStatuses.Draft,
                // العمود الوحيد الذي يصلح للترتيب الزمني؛ ReviewId عشوائي هنا.
                CreatedAt = _clock.UtcNow
            };
            _db.VerificationReviews.Add(review);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // A concurrent first upload created the round first: the filtered unique
                // index UX_VERIFICATION_REVIEW_volunteer_open allows one open review per
                // volunteer, so the loser detaches its insert and re-reads the winner.
                _db.Entry(review).State = EntityState.Detached;
                review = await _db.VerificationReviews
                    .Where(r => r.VolunteerId == profile.UserId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenByDescending(r => r.ReviewId)
                    .FirstAsync(ct);
            }
            return review;
        }

        if (review.Status == VerificationReviewStatuses.Approved)
        {
            throw new AppException(422, ErrorCodes.InvalidState, "تم اعتماد حسابك بالفعل.");
        }

        if (review.Status == VerificationReviewStatuses.Rejected)
        {
            if (review.InterviewSlotId is not null)
            {
                var oldSlotId = review.InterviewSlotId.Value;
                await _db.InterviewSlots
                    .Where(s => s.InterviewSlotId == oldSlotId && s.Status == InterviewSlotStatuses.Booked)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, InterviewSlotStatuses.Available), ct);
            }

            review.Status = VerificationReviewStatuses.Draft;
            review.ReviewerId = null;
            review.InterviewSlotId = null;
            review.InterviewAt = null;
            review.DecidedAt = null;
            profile.VerificationStatus = VerificationStatuses.Unverified;
            await _db.SaveChangesAsync(ct);
        }

        return review;
    }

    private static bool ArePreInterviewStepsComplete(VolunteerProfile profile, VerificationReview? review)
    {
        var types = review?.VerificationEvidences.Select(e => e.EvidenceType).ToHashSet(StringComparer.Ordinal)
                    ?? new HashSet<string>(StringComparer.Ordinal);
        return profile.User.EmailVerified &&
               profile.User.PhoneVerified &&
               types.Contains(EvidenceTypes.NationalIdFront) &&
               types.Contains(EvidenceTypes.NationalIdBack) &&
               types.Contains(EvidenceTypes.FacePhoto) &&
               profile.VolunteerSocialLinks.Count > 0;
    }

    private VerificationResponse BuildVerification(VolunteerProfile profile, VerificationReview? review)
    {
        var evidence = review?.VerificationEvidences.Select(e => e.EvidenceType).ToHashSet(StringComparer.Ordinal)
                       ?? new HashSet<string>(StringComparer.Ordinal);
        var hasInterview = review?.InterviewSlotId is not null && review.InterviewAt is not null;
        var status = profile.VerificationStatus;

        var steps = new List<VerificationStepDto>
        {
            new("email", "التحقق من البريد الإلكتروني", profile.User.EmailVerified, true),
            new("phone", "التحقق من رقم الهاتف", profile.User.PhoneVerified, true),
            new("national_id_front", "صورة البطاقة من الأمام", evidence.Contains(EvidenceTypes.NationalIdFront), true),
            new("national_id_back", "صورة البطاقة من الخلف", evidence.Contains(EvidenceTypes.NationalIdBack), true),
            new("social_links", "حسابات التواصل الاجتماعي", profile.VolunteerSocialLinks.Count > 0, true),
            new("face_photo", "صورة الوجه", evidence.Contains(EvidenceTypes.FacePhoto), true),
            new("interview", "حجز المقابلة", hasInterview, true),
            new("decision", "مراجعة الإدارة", status is VerificationStatuses.Approved or VerificationStatuses.Rejected, false)
        };

        var current = steps.FirstOrDefault(s => s.Required && !s.Completed)?.Key ?? "admin_review";
        if (status == VerificationStatuses.Approved) current = "completed";
        if (status == VerificationStatuses.Rejected) current = "reapply";

        var preComplete = ArePreInterviewStepsComplete(profile, review);
        return new VerificationResponse(
            status,
            current,
            review?.ReviewId,
            review?.InterviewAt,
            preComplete && status != VerificationStatuses.Approved,
            preComplete && !hasInterview && status != VerificationStatuses.Approved,
            steps,
            evidence.OrderBy(e => e).ToList(),
            profile.VolunteerSocialLinks.OrderBy(l => l.Platform)
                .Select(l => new SocialLinkDto(l.Platform, l.Url)).ToList());
    }

    private static VolunteerProfileResponse Map(VolunteerProfile profile) =>
        new(profile.UserId, profile.Introduction, profile.HelpMode, profile.ServiceArea, profile.Governorate,
            profile.Latitude, profile.Longitude, profile.ServiceRadiusKm, profile.SkillLevel,
            profile.ExperienceNotes, profile.WeeklyHours, profile.VerificationStatus,
            profile.Skills.OrderBy(s => s.Name).Select(s => new LookupValueDto(s.SkillId, s.Name)).ToList());
}
