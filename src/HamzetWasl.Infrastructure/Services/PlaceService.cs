using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Services;

public sealed class PlaceService(HamzetWaslDbContext db, IClock clock) : IPlaceService
{
    private static readonly string[] FacStatuses = ["available", "unavailable", "unknown"];
    private static readonly string[] ObsStatuses = ["open", "verified", "resolved", "rejected"];

    private static AppException Invalid(string f, string m) => AppException.Validation(new Dictionary<string, string[]> { [f] = [m] });

    public async Task<IReadOnlyList<string>> FacilityTypesAsync(CancellationToken ct = default) =>
        await db.FacilityTypes.OrderBy(f => f.FacilityTypeId).Select(f => f.Name).ToListAsync(ct);

    private async Task<List<PlaceDto>> ToDtosAsync(List<Place> rows, bool withReviews, CancellationToken ct)
    {
        var ids = rows.Select(p => p.PlaceId).ToList();
        var facs = await (from pf in db.PlaceFacilities join ft in db.FacilityTypes on pf.FacilityTypeId equals ft.FacilityTypeId where ids.Contains(pf.PlaceId)
                          select new { pf.PlaceId, ft.Name, pf.AvailabilityStatus, pf.Notes }).ToListAsync(ct);
        var revs = await db.PlaceReviews.Where(r => ids.Contains(r.PlaceId)).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var uids = revs.Select(r => r.UserId).Distinct().ToList();
        var names = withReviews ? await db.UserProfiles.Where(p => uids.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct) : new Dictionary<Guid, string>();
        return rows.Select(p =>
        {
            var pr = revs.Where(r => r.PlaceId == p.PlaceId).ToList();
            return new PlaceDto(p.PlaceId, p.Name, p.Address, p.Latitude, p.Longitude, p.LastVerifiedAt, p.VerificationSource,
                facs.Where(f => f.PlaceId == p.PlaceId).Select(f => new FacilityDto(f.Name, f.AvailabilityStatus, f.Notes)).ToList(),
                pr.Count == 0 ? null : Math.Round(pr.Average(r => r.Rating), 1), pr.Count,
                withReviews ? pr.Select(r => new PlaceReviewDto(r.ReviewId, names.GetValueOrDefault(r.UserId, ""), r.Rating, r.Comment, r.CreatedAt)).ToList() : null);
        }).ToList();
    }

    public async Task<PagedResult<PlaceDto>> SearchAsync(string? q, string? facility, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Places.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var t = q.Trim(); query = query.Where(p => p.Name.Contains(t) || p.Address.Contains(t)); }
        if (!string.IsNullOrWhiteSpace(facility))
            query = query.Where(p => db.PlaceFacilities.Any(pf => pf.PlaceId == p.PlaceId && pf.AvailabilityStatus == "available" && db.FacilityTypes.Any(ft => ft.FacilityTypeId == pf.FacilityTypeId && ft.Name == facility)));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<PlaceDto>(await ToDtosAsync(rows, false, ct), page, pageSize, total);
    }

    public async Task<PlaceDto> GetAsync(Guid placeId, CancellationToken ct = default)
    {
        var p = await db.Places.FirstOrDefaultAsync(x => x.PlaceId == placeId, ct) ?? throw AppException.NotFound();
        return (await ToDtosAsync([p], true, ct))[0];
    }

    public async Task<PlaceDto> CreateAsync(PlaceRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 200) throw Invalid("name", "اسم المكان مطلوب (حتى 200 حرف).");
        if (string.IsNullOrWhiteSpace(r.Address) || r.Address.Trim().Length > 400) throw Invalid("address", "العنوان مطلوب.");
        if (r.Latitude is < -90 or > 90) throw Invalid("latitude", "خط العرض غير صحيح.");
        if (r.Longitude is < -180 or > 180) throw Invalid("longitude", "خط الطول غير صحيح.");
        if (string.IsNullOrWhiteSpace(r.VerificationSource)) throw Invalid("verificationSource", "مصدر التحقق مطلوب.");
        var types = await db.FacilityTypes.ToListAsync(ct);
        var facs = (r.Facilities ?? []).GroupBy(f => f.Type).Select(g => g.First()).ToList();
        foreach (var f in facs)
        {
            if (!types.Any(t => t.Name == f.Type)) throw Invalid("facilities", "نوع تسهيل غير معروف.");
            if (!FacStatuses.Contains(f.Status)) throw Invalid("facilities", "حالة التسهيل غير صحيحة.");
        }
        var p = new Place { PlaceId = Guid.NewGuid(), Name = r.Name.Trim(), Address = r.Address.Trim(), Latitude = r.Latitude, Longitude = r.Longitude, VerificationSource = r.VerificationSource.Trim(), LastVerifiedAt = clock.UtcNow };
        db.Places.Add(p);
        foreach (var f in facs)
            db.PlaceFacilities.Add(new PlaceFacility { PlaceId = p.PlaceId, FacilityTypeId = types.First(t => t.Name == f.Type).FacilityTypeId, AvailabilityStatus = f.Status, Notes = string.IsNullOrWhiteSpace(f.Notes) ? null : f.Notes.Trim() });
        await db.SaveChangesAsync(ct);
        return await GetAsync(p.PlaceId, ct);
    }

    public async Task<PlaceDto> ReviewAsync(Guid userId, Guid placeId, PlaceReviewRequest r, CancellationToken ct = default)
    {
        if (!await db.Places.AnyAsync(p => p.PlaceId == placeId, ct)) throw AppException.NotFound();
        if (r.Rating is < 1 or > 5) throw Invalid("rating", "التقييم من 1 إلى 5.");
        var existing = await db.PlaceReviews.FirstOrDefaultAsync(x => x.PlaceId == placeId && x.UserId == userId, ct);
        if (existing is null) db.PlaceReviews.Add(new PlaceReview { ReviewId = Guid.NewGuid(), PlaceId = placeId, UserId = userId, Rating = r.Rating, Comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim(), CreatedAt = clock.UtcNow });
        else { existing.Rating = r.Rating; existing.Comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim(); }
        await db.SaveChangesAsync(ct);
        return await GetAsync(placeId, ct);
    }

    private static ObstacleDto ToDto(ObstacleReport o) => new(o.ObstacleId, o.PlaceId, o.Description, o.Latitude, o.Longitude, o.Status, o.ReportedAt);

    public async Task<ObstacleDto> ReportObstacleAsync(Guid userId, ObstacleRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Description)) throw Invalid("description", "اوصف العائق.");
        if (r.Latitude is < -90 or > 90 || r.Longitude is < -180 or > 180) throw Invalid("latitude", "الموقع غير صحيح.");
        if (r.PlaceId is { } pid && !await db.Places.AnyAsync(p => p.PlaceId == pid, ct)) throw Invalid("placeId", "المكان غير موجود.");
        var o = new ObstacleReport { ObstacleId = Guid.NewGuid(), ReporterId = userId, PlaceId = r.PlaceId, Description = r.Description.Trim(), Latitude = r.Latitude, Longitude = r.Longitude, Status = "open", ReportedAt = clock.UtcNow };
        db.ObstacleReports.Add(o);
        await db.SaveChangesAsync(ct);
        return ToDto(o);
    }

    public async Task<IReadOnlyList<ObstacleDto>> ObstaclesAsync(bool all, CancellationToken ct = default)
    {
        var q = db.ObstacleReports.AsQueryable();
        if (!all) q = q.Where(o => o.Status == "open" || o.Status == "verified");
        return (await q.OrderByDescending(o => o.ReportedAt).Take(200).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<ObstacleDto> SetObstacleStatusAsync(Guid obstacleId, string status, CancellationToken ct = default)
    {
        if (!ObsStatuses.Contains(status)) throw Invalid("status", "الحالة غير صحيحة.");
        var o = await db.ObstacleReports.FirstOrDefaultAsync(x => x.ObstacleId == obstacleId, ct) ?? throw AppException.NotFound();
        o.Status = status;
        await db.SaveChangesAsync(ct);
        return ToDto(o);
    }
}
