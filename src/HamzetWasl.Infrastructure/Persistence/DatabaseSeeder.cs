using HamzetWasl.Application.Metadata;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Domain.Rules;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    private readonly HamzetWaslDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly SeedOptions _seed;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        HamzetWaslDbContext db,
        UserManager<AppUser> users,
        IOptions<SeedOptions> seed,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _users = users;
        _seed = seed.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedRolesAsync(ct);
        await SeedLookupsAsync(ct);
        await SeedTrainingAsync(ct);
        await SeedAdminAsync(ct);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var existing = await _db.AccountRoles.Select(r => r.RoleName).ToListAsync(ct);
        foreach (var role in RoleNames.All.Where(r => !existing.Contains(r, StringComparer.Ordinal)))
        {
            _db.AccountRoles.Add(new AccountRole { RoleName = role });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedLookupsAsync(CancellationToken ct)
    {
        var needTypes = await _db.NeedTypes.Select(n => n.Name).ToListAsync(ct);
        foreach (var item in MetaCatalog.NeedTypeCatalog.Where(n => !needTypes.Contains(n.Key, StringComparer.Ordinal)))
        {
            _db.NeedTypes.Add(new NeedType { Name = item.Key });
        }

        var skills = await _db.Skills.Select(s => s.Name).ToListAsync(ct);
        foreach (var item in MetaCatalog.StarterSkills.Where(s => !skills.Contains(s.NameAr, StringComparer.Ordinal)))
        {
            _db.Skills.Add(new Skill { Name = item.NameAr });
        }

        var interests = await _db.Interests.Select(i => i.Name).ToListAsync(ct);
        foreach (var item in MetaCatalog.StarterInterests.Where(i => !interests.Contains(i.NameAr, StringComparer.Ordinal)))
        {
            _db.Interests.Add(new Interest { Name = item.NameAr });
        }

        var accommodations = await _db.Accommodations.Select(a => a.Name).ToListAsync(ct);
        foreach (var item in MetaCatalog.StarterAccommodations.Where(a => !accommodations.Contains(a.NameAr, StringComparer.Ordinal)))
        {
            _db.Accommodations.Add(new Accommodation { Name = item.NameAr, Category = item.Category });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedTrainingAsync(CancellationToken ct)
    {
        if (await _db.TrainingModules.AnyAsync(ct)) return;
        _db.TrainingModules.Add(new TrainingModule
        {
            ModuleId = Guid.NewGuid(),
            Title = "التدريب التعريفي للمتطوعين",
            Version = "1.0"
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_seed.AdminEmail) || string.IsNullOrWhiteSpace(_seed.AdminPassword))
        {
            _logger.LogWarning("Admin seed skipped: Seed:AdminEmail and Seed:AdminPassword are not configured.");
            return;
        }

        var email = _seed.AdminEmail.Trim().ToLowerInvariant();
        var existing = await _db.UserAccounts.Include(a => a.Role).FirstOrDefaultAsync(a => a.Email == email, ct);
        if (existing is not null)
        {
            if (existing.Role.RoleName != RoleNames.Admin)
            {
                _logger.LogWarning("Admin seed skipped: an account with the admin address exists with role {Role}.",
                    existing.Role.RoleName);
            }

            return;
        }

        var adminRole = await _db.AccountRoles.SingleAsync(r => r.RoleName == RoleNames.Admin, ct);
        var phone = PhoneNumberNormalizer.TryNormalize(_seed.AdminPhone);
        var userId = Guid.NewGuid();

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var account = new UserAccount
        {
            UserId = userId,
            RoleId = adminRole.RoleId,
            Email = email,
            EmailVerified = true,
            Phone = phone,
            PhoneVerified = false,
            AccountStatus = AccountStatuses.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.UserAccounts.Add(account);
        _db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            DisplayName = string.IsNullOrWhiteSpace(_seed.AdminName) ? "مدير النظام" : _seed.AdminName,
            Visibility = ProfileVisibilities.Private
        });
        await _db.SaveChangesAsync(ct);

        var appUser = new AppUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            PhoneNumber = phone,
            PhoneNumberConfirmed = false,
            UserAccount = account
        };

        var result = await _users.CreateAsync(appUser, _seed.AdminPassword);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError("Admin seed failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await transaction.CommitAsync(ct);
        _logger.LogInformation("Admin account seeded.");
    }
}
