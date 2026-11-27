using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HamzetWasl.Application.Common;

namespace HamzetWasl.Infrastructure.Services;

public sealed class StatsService(HamzetWaslDbContext db, IClock clock) : IStatsService
{
    public async Task<PublicStatsDto> PublicAsync(CancellationToken ct = default) => new(
        await db.VolunteerProfiles.CountAsync(v => v.VerificationStatus == VerificationStatuses.Approved, ct),
        await db.UserAccounts.CountAsync(u => u.Role.RoleName == RoleNames.DisabledUser && u.AccountStatus == AccountStatuses.Active, ct),
        await db.Organizations.CountAsync(o => o.ApprovalStatus == "approved", ct),
        await db.Jobs.CountAsync(j => j.Status == "published", ct),
        await db.HelpRequests.CountAsync(h => h.Status == "completed", ct),
        await db.Places.CountAsync(ct));

    public async Task<AdminStatsDto> AdminAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return new AdminStatsDto(await PublicAsync(ct),
            await db.VerificationReviews.CountAsync(r => r.Status == "pending", ct),
            await db.OrganizationReviews.CountAsync(r => r.Status == "pending", ct),
            await db.ContentReports.CountAsync(r => r.Status == "open", ct),
            await db.SafetyReports.CountAsync(r => r.Status == "open", ct),
            await db.ObstacleReports.CountAsync(o => o.Status == "open", ct),
            await db.SosAlerts.CountAsync(a => a.CreatedAt > now.AddDays(-30), ct),
            await db.UserAccounts.CountAsync(u => u.CreatedAt > now.AddDays(-7), ct));
    }
}
