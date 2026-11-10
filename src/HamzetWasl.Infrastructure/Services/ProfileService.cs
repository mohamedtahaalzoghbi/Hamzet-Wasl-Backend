using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class ProfileService : IProfileService
{
    private readonly HamzetWaslDbContext _db;
    private readonly IFileStorage _storage;

    public ProfileService(HamzetWaslDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<ProfileResponse> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? throw AppException.NotFound("لم يتم إنشاء الملف الشخصي بعد.");
        return Map(profile);
    }

    public async Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? throw AppException.NotFound("لم يتم إنشاء الملف الشخصي بعد.");

        profile.DisplayName = request.DisplayName.Trim();
        profile.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        profile.City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim();
        profile.Visibility = request.Visibility;
        await _db.SaveChangesAsync(ct);
        return Map(profile);
    }

    public async Task SetInterestsAsync(Guid userId, IReadOnlyList<int> interestIds, CancellationToken ct = default)
    {
        var ids = interestIds.Distinct().ToArray();
        var interests = await _db.Interests.Where(i => ids.Contains(i.InterestId)).ToListAsync(ct);
        if (interests.Count != ids.Length)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["interestIds"] = ["بعض الاهتمامات غير موجودة."] });
        }

        var account = await _db.UserAccounts.Include(a => a.Interests)
                          .FirstOrDefaultAsync(a => a.UserId == userId, ct)
                      ?? throw AppException.NotFound();

        account.Interests.Clear();
        foreach (var interest in interests) account.Interests.Add(interest);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSkillsAsync(Guid userId, IReadOnlyList<int> skillIds, CancellationToken ct = default)
    {
        var ids = skillIds.Distinct().ToArray();
        var skills = await _db.Skills.Where(s => ids.Contains(s.SkillId)).ToListAsync(ct);
        if (skills.Count != ids.Length)
        {
            throw AppException.Validation(new Dictionary<string, string[]> { ["skillIds"] = ["بعض المهارات غير موجودة."] });
        }

        var account = await _db.UserAccounts.Include(a => a.Skills)
                          .FirstOrDefaultAsync(a => a.UserId == userId, ct)
                      ?? throw AppException.NotFound();

        account.Skills.Clear();
        foreach (var skill in skills) account.Skills.Add(skill);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> UpdateAvatarAsync(Guid userId, FileUpload file, CancellationToken ct = default)
    {
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? throw AppException.NotFound("لم يتم إنشاء الملف الشخصي بعد.");

        var stored = await _storage.SaveAsync(file, FileCategory.PublicImage, userId, ct);
        var oldKey = profile.AvatarStorageKey;
        profile.AvatarStorageKey = stored.Key;
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(oldKey))
        {
            await _storage.DeleteAsync(oldKey, ct);
        }

        return _storage.GetPublicUrl(stored.Key);
    }

    private ProfileResponse Map(UserProfile profile) =>
        new(profile.UserId, profile.DisplayName, profile.Bio, profile.City,
            string.IsNullOrWhiteSpace(profile.AvatarStorageKey) ? null : _storage.GetPublicUrl(profile.AvatarStorageKey),
            profile.Visibility);
}

public sealed class AccessibilityService : IAccessibilityService
{
    private readonly HamzetWaslDbContext _db;
    private readonly IClock _clock;

    public AccessibilityService(HamzetWaslDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AccessibilityResponse> GetAsync(Guid userId, CancellationToken ct = default)
    {
        await EnsureAllowedAsync(userId, ct);
        var profile = await _db.AccessibilityProfiles
                          .Include(p => p.NeedType)
                          .FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? throw AppException.NotFound("لم يتم اختيار إعدادات الإتاحة بعد.");
        return Map(profile);
    }

    public async Task<AccessibilityResponse> UpsertAsync(Guid userId, UpsertAccessibilityRequest request, CancellationToken ct = default)
    {
        await EnsureAllowedAsync(userId, ct);

        var needType = await _db.NeedTypes.FirstOrDefaultAsync(n => n.Name == request.NeedType, ct)
                       ?? throw AppException.Validation(new Dictionary<string, string[]> { ["needType"] = ["نوع الاحتياج غير صحيح."] });

        var now = _clock.UtcNow;
        var profile = await _db.AccessibilityProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var consentId = profile?.ConsentId;

        if (consentId is null)
        {
            var consent = new ConsentRecord
            {
                ConsentId = Guid.NewGuid(),
                UserId = userId,
                Purpose = Domain.Constants.ConsentPurposes.Accessibility,
                PolicyVersion = "1.0",
                GrantedAt = now
            };
            _db.ConsentRecords.Add(consent);
            consentId = consent.ConsentId;
        }

        if (profile is null)
        {
            profile = new AccessibilityProfile
            {
                UserId = userId,
                NeedTypeId = needType.NeedTypeId,
                ConsentId = consentId.Value,
                Theme = request.Theme,
                FontScale = request.FontScale,
                ReducedMotion = request.ReducedMotion,
                InteractionMode = request.InteractionMode,
                NotificationMode = request.NotificationMode,
                ManualOverrides = request.ManualOverrides?.GetRawText(),
                UpdatedAt = now
            };
            _db.AccessibilityProfiles.Add(profile);
        }
        else
        {
            profile.NeedTypeId = needType.NeedTypeId;
            profile.ConsentId = consentId.Value;
            profile.Theme = request.Theme;
            profile.FontScale = request.FontScale;
            profile.ReducedMotion = request.ReducedMotion;
            profile.InteractionMode = request.InteractionMode;
            profile.NotificationMode = request.NotificationMode;
            profile.ManualOverrides = request.ManualOverrides?.GetRawText();
            profile.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        profile.NeedType = needType;
        return Map(profile);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        await EnsureAllowedAsync(userId, ct);
        var profile = await _db.AccessibilityProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return;

        var consent = await _db.ConsentRecords.FirstOrDefaultAsync(c => c.ConsentId == profile.ConsentId, ct);
        if (consent is not null)
        {
            consent.WithdrawnAt = _clock.UtcNow;
        }

        _db.AccessibilityProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureAllowedAsync(Guid userId, CancellationToken ct)
    {
        var roleName = await _db.UserAccounts.Where(a => a.UserId == userId)
            .Select(a => a.Role.RoleName)
            .FirstOrDefaultAsync(ct);

        if (roleName is null) throw AppException.NotFound();
        if (!Domain.Rules.BusinessRules.CanHaveAccessibilityProfile(roleName))
        {
            throw AppException.Forbidden(ErrorCodes.AccessibilityNotAllowed);
        }
    }

    private static AccessibilityResponse Map(AccessibilityProfile profile)
    {
        // The effective preset is calculated, not copied: need-type defaults fill the values the
        // user has not chosen, the user's own preferences win over them and the manual
        // overrides win over everything.
        var preset = Domain.Rules.AccessibilityPresets.Calculate(
            profile.NeedType?.Name,
            profile.Theme,
            profile.FontScale,
            profile.ReducedMotion,
            profile.InteractionMode,
            profile.NotificationMode,
            profile.ManualOverrides);

        var presetDto = new AccessibilityPresetDto(
            preset.Values.Theme,
            preset.Values.FontScale,
            preset.Values.ReducedMotion,
            preset.Values.InteractionMode,
            preset.Values.NotificationMode,
            preset.Values.HighContrast,
            preset.Values.TextDirection,
            profile.NeedType?.Name,
            preset.NeedTypes);

        return new AccessibilityResponse(
            profile.UserId,
            profile.NeedType?.Name ?? string.Empty,
            profile.Theme,
            profile.FontScale,
            profile.ReducedMotion,
            profile.InteractionMode,
            profile.NotificationMode,
            string.IsNullOrWhiteSpace(profile.ManualOverrides)
                ? null
                : System.Text.Json.JsonDocument.Parse(profile.ManualOverrides).RootElement.Clone(),
            profile.UpdatedAt,
            presetDto);
    }
}
