using System.Reflection;
using FluentAssertions;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Contracts.Notifications;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Persistence;
using HamzetWasl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HamzetWasl.UnitTests;

/// <summary>
/// Regression coverage for the November 2026 race-safety and retention round.
/// These run on SQLite in-memory because the atomic conditional updates
/// (ExecuteUpdateAsync / ExecuteDeleteAsync) and the filtered unique index are
/// relational behaviors that the InMemory provider cannot exercise. SQLite is
/// still not SQL Server: these tests prove the service logic and the model's
/// intent, and the real-engine integration suite remains a separate step.
/// </summary>
public sealed class TestDbContext(DbContextOptions<HamzetWaslDbContext> options) : HamzetWaslDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite cannot evaluate the SQL Server default expressions
        // (newsequentialid(), sysutcdatetime()); the tests set every value
        // explicitly, so the defaults are stripped here only.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                property.SetDefaultValueSql(null);
            }

            foreach (var index in entityType.GetIndexes())
            {
                var filter = index.GetFilter();
                if (filter is not null)
                {
                    // SQLite has no N'' string literal prefix.
                    index.SetFilter(filter.Replace("N'", "'"));
                }
            }
        }
    }
}

public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class FakeNotifications : INotificationService
{
    public List<(Guid Recipient, string Type, string? TargetType, Guid? TargetId)> Sent { get; } = [];

    public Task<Guid> NotifyAsync(Guid recipientId, string notificationType, string bodyAr,
        string? targetType = null, Guid? targetId = null, CancellationToken ct = default)
    {
        Sent.Add((recipientId, notificationType, targetType, targetId));
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<PagedResult<NotificationDto>> GetForRecipientAsync(Guid recipientId, int page, int pageSize,
        CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<NotificationDto>([], page, pageSize, 0));

    public Task MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

public sealed class FakeStorage : IFileStorage
{
    private int _counter;
    public List<string> SavedKeys { get; } = [];
    public List<string> DeletedKeys { get; } = [];

    public Task<StoredFile> SaveAsync(FileUpload file, FileCategory category, Guid ownerId, CancellationToken ct = default)
    {
        var key = $"test-{++_counter}-{file.FileName}";
        SavedKeys.Add(key);
        return Task.FromResult(new StoredFile(key, file.FileName, file.ContentType, file.Length));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        DeletedKeys.Add(key);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
    public string GetPublicUrl(string key) => $"/media/{key}";
    public SignedFileUrl CreateSignedUrl(string key, TimeSpan lifetime) => new($"/signed/{key}", DateTime.UtcNow.Add(lifetime));
    public bool ValidateSignedUrl(string key, long expiresUnix, string signature) => false;
}

public sealed class FakeGeoCatalog : IGeoCatalog
{
    public Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GovernorateDto>>([]);

    public Task<GovernorateDto?> FindGovernorateAsync(string? keyOrName, CancellationToken ct = default) =>
        Task.FromResult<GovernorateDto?>(null);

    public Task<AreaDto?> FindAreaAsync(GovernorateDto governorate, string? keyOrName, CancellationToken ct = default) =>
        Task.FromResult<AreaDto?>(null);
}

public abstract class SqliteServiceTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Dictionary<string, AccountRole> _roles = new(StringComparer.Ordinal);

    protected TestDbContext Db { get; }
    protected FakeClock Clock { get; } = new();
    protected FakeNotifications Notifications { get; } = new();
    protected FakeStorage Storage { get; } = new();

    protected static readonly IOptions<StorageOptions> StorageOpts =
        Options.Create(new StorageOptions { SigningKey = new string('k', 32) });
    protected static readonly IOptions<RetentionOptions> Retention = Options.Create(new RetentionOptions());

    protected StorageKeyProtector Protector { get; } = new(StorageOpts);

    protected SqliteServiceTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<HamzetWaslDbContext>().UseSqlite(_connection).Options;
        Db = new TestDbContext(options);
        Db.Database.EnsureCreated();
    }

    protected TestDbContext NewSecondContext() =>
        new(new DbContextOptionsBuilder<HamzetWaslDbContext>().UseSqlite(_connection).Options);

    protected AdminService NewAdminService() =>
        new(Db, Storage, Protector, Notifications, Clock, StorageOpts, Retention);

    protected VolunteerService NewVolunteerService() =>
        new(Db, Storage, Protector, Notifications, new FakeGeoCatalog(), Clock, Retention);

    protected UserAccount SeedUser(string roleName)
    {
        if (!_roles.TryGetValue(roleName, out var role))
        {
            role = new AccountRole { RoleId = _roles.Count + 1, RoleName = roleName };
            Db.AccountRoles.Add(role);
            _roles[roleName] = role;
        }

        var user = new UserAccount
        {
            UserId = Guid.NewGuid(),
            RoleId = role.RoleId,
            Email = $"{Guid.NewGuid():N}@example.com",
            EmailVerified = true,
            PhoneVerified = true,
            AccountStatus = AccountStatuses.Active,
            CreatedAt = Clock.UtcNow.AddDays(-30)
        };
        Db.UserAccounts.Add(user);
        return user;
    }

    protected VolunteerProfile SeedVolunteer(out Guid userId)
    {
        var user = SeedUser(RoleNames.Volunteer);
        userId = user.UserId;
        var profile = new VolunteerProfile
        {
            UserId = user.UserId,
            HelpMode = HelpModes.OnlineOnly,
            ServiceArea = "القاهرة",
            SkillLevel = "متوسط",
            VerificationStatus = VerificationStatuses.Unverified,
            WeeklyHours = 4
        };
        Db.VolunteerProfiles.Add(profile);
        Db.VolunteerSocialLinks.Add(new VolunteerSocialLink
        {
            VolunteerId = user.UserId,
            Platform = "facebook",
            Url = "https://facebook.com/example"
        });
        return profile;
    }

    protected VerificationReview SeedReview(Guid volunteerId, string status, bool withAllEvidence = false,
        string? singleEvidenceType = null, string storageKey = "file-key")
    {
        var review = new VerificationReview
        {
            ReviewId = Guid.NewGuid(),
            VolunteerId = volunteerId,
            VerificationLevel = VerificationLevels.Basic,
            Status = status,
            CreatedAt = Clock.UtcNow
        };
        Db.VerificationReviews.Add(review);

        IEnumerable<string> types = withAllEvidence
            ? EvidenceTypes.All
            : singleEvidenceType is null ? [] : new[] { singleEvidenceType };

        foreach (var type in types)
        {
            Db.VerificationEvidences.Add(new VerificationEvidence
            {
                EvidenceId = Guid.NewGuid(),
                ReviewId = review.ReviewId,
                EvidenceType = type,
                EncryptedStorageKey = Protector.Protect($"{storageKey}-{type}"),
                RetentionUntil = Clock.UtcNow.AddDays(90)
            });
        }

        return review;
    }

    private int _slotCounter;

    protected InterviewSlot SeedSlot(Guid reviewerId, string status = InterviewSlotStatuses.Available)
    {
        var slot = new InterviewSlot
        {
            InterviewSlotId = Guid.NewGuid(),
            ReviewerId = reviewerId,
            // (reviewer_id, starts_at) is unique: each seeded slot gets its own start time.
            StartsAt = Clock.UtcNow.AddDays(1).AddMinutes(30 * _slotCounter++),
            DurationMinutes = 30,
            Status = status
        };
        Db.InterviewSlots.Add(slot);
        return slot;
    }

    protected void ClearTracker() => Db.ChangeTracker.Clear();

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

public sealed class AdminDecisionTests : SqliteServiceTestBase
{
    [Fact]
    public async Task Volunteer_decision_is_exactly_once()
    {
        var profile = SeedVolunteer(out var volunteerId);
        var review = SeedReview(volunteerId, VerificationReviewStatuses.Pending);
        var admin = SeedUser(RoleNames.Admin);
        await Db.SaveChangesAsync();
        var service = NewAdminService();

        await service.DecideVolunteerReviewAsync(review.ReviewId, admin.UserId, new DecisionRequest { Approve = true });

        ClearTracker();
        (await Db.VerificationReviews.SingleAsync(r => r.ReviewId == review.ReviewId))
            .Status.Should().Be(VerificationReviewStatuses.Approved);
        (await Db.VolunteerProfiles.SingleAsync(v => v.UserId == volunteerId))
            .VerificationStatus.Should().Be(VerificationStatuses.Approved);
        (await Db.VerificationEvidences.Where(e => e.ReviewId == review.ReviewId).ToListAsync())
            .Should().OnlyContain(e => e.RetentionUntil == Clock.UtcNow.AddDays(90));
        Notifications.Sent.Should().Contain(n => n.Recipient == volunteerId && n.Type == NotificationTypes.VerificationApproved);

        // A second decision on the same round (the loser of a concurrent pair) is refused.
        var act = () => service.DecideVolunteerReviewAsync(review.ReviewId, admin.UserId, new DecisionRequest { Approve = false, Notes = "x" });
        await act.Should().ThrowAsync<AppException>()
            .Where(e => e.StatusCode == 422 && e.Code == ErrorCodes.InvalidState);

        ClearTracker();
        (await Db.VerificationReviews.SingleAsync(r => r.ReviewId == review.ReviewId))
            .Status.Should().Be(VerificationReviewStatuses.Approved, "the losing decision must not overwrite the winner");
        profile.Should().NotBeNull();
    }

    [Fact]
    public async Task Volunteer_decision_eligibility_is_unchanged_by_this_round()
    {
        // Original contract (Q24 left open): a decision is accepted on pending or
        // interview_scheduled, including an interview that has not happened yet.
        SeedVolunteer(out var volunteerId);
        var review = SeedReview(volunteerId, VerificationReviewStatuses.InterviewScheduled);
        review.InterviewAt = Clock.UtcNow.AddDays(1);
        var admin = SeedUser(RoleNames.Admin);
        await Db.SaveChangesAsync();

        await NewAdminService().DecideVolunteerReviewAsync(review.ReviewId, admin.UserId, new DecisionRequest { Approve = true });

        ClearTracker();
        (await Db.VerificationReviews.SingleAsync(r => r.ReviewId == review.ReviewId))
            .Status.Should().Be(VerificationReviewStatuses.Approved);
    }

    [Fact]
    public async Task Volunteer_decision_on_draft_round_is_refused()
    {
        SeedVolunteer(out var volunteerId);
        var review = SeedReview(volunteerId, VerificationReviewStatuses.Draft);
        var admin = SeedUser(RoleNames.Admin);
        await Db.SaveChangesAsync();

        var act = () => NewAdminService().DecideVolunteerReviewAsync(review.ReviewId, admin.UserId, new DecisionRequest { Approve = true });
        await act.Should().ThrowAsync<AppException>()
            .Where(e => e.StatusCode == 422 && e.Code == ErrorCodes.InvalidState);
    }

    [Fact]
    public async Task Organization_decision_is_exactly_once_and_stores_rejection_notes()
    {
        var owner = SeedUser(RoleNames.Association);
        var organization = new Organization
        {
            OrganizationId = Guid.NewGuid(),
            OwnerUserId = owner.UserId,
            OrganizationType = "جمعية",
            Name = "مؤسسة اختبار",
            ApprovalStatus = OrganizationApprovalStatuses.Pending
        };
        Db.Organizations.Add(organization);
        var review = new OrganizationReview
        {
            ReviewId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            Status = OrganizationReviewStatuses.Pending,
            RequestedAt = Clock.UtcNow
        };
        Db.OrganizationReviews.Add(review);
        var admin = SeedUser(RoleNames.Admin);
        await Db.SaveChangesAsync();
        var service = NewAdminService();

        await service.DecideOrganizationReviewAsync(review.ReviewId, admin.UserId,
            new DecisionRequest { Approve = false, Notes = "السجل التجاري غير واضح" });

        ClearTracker();
        var decided = await Db.OrganizationReviews.SingleAsync(r => r.ReviewId == review.ReviewId);
        decided.Status.Should().Be(OrganizationReviewStatuses.Rejected);
        decided.DecisionNotes.Should().Be("السجل التجاري غير واضح", "organization notes are persisted in decision_notes");
        (await Db.Organizations.SingleAsync(o => o.OrganizationId == organization.OrganizationId))
            .ApprovalStatus.Should().Be(OrganizationApprovalStatuses.Rejected);

        var act = () => service.DecideOrganizationReviewAsync(review.ReviewId, admin.UserId, new DecisionRequest { Approve = true });
        await act.Should().ThrowAsync<AppException>()
            .Where(e => e.StatusCode == 422 && e.Code == ErrorCodes.InvalidState);
    }

    [Fact]
    public async Task Resolving_deletion_keeps_the_original_created_at()
    {
        var user = SeedUser(RoleNames.Volunteer);
        var originalCreatedAt = user.CreatedAt;
        var deletion = new DeletionRequest
        {
            DeletionRequestId = Guid.NewGuid(),
            UserId = user.UserId,
            Status = DeletionRequestStatuses.Pending,
            RequestedAt = Clock.UtcNow
        };
        Db.DeletionRequests.Add(deletion);
        var admin = SeedUser(RoleNames.Admin);
        await Db.SaveChangesAsync();

        // AnonymizeUserAsync issues two pre-existing SQL Server raw deletes against
        // dbo.USER_INTEREST / dbo.USER_SKILL; attach empty stand-ins so SQLite accepts them.
        await Db.Database.ExecuteSqlRawAsync("ATTACH DATABASE ':memory:' AS dbo");
        await Db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.USER_INTEREST (user_id TEXT NOT NULL, interest_id INTEGER NOT NULL)");
        await Db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.USER_SKILL (user_id TEXT NOT NULL, skill_id INTEGER NOT NULL)");

        await NewAdminService().ResolveDeletionRequestAsync(admin.UserId, deletion.DeletionRequestId,
            new ResolveDeletionRequestRequest { Approve = true });

        ClearTracker();
        var anonymized = await Db.UserAccounts.SingleAsync(u => u.UserId == user.UserId);
        anonymized.CreatedAt.Should().Be(originalCreatedAt, "anonymisation must not rewrite the creation timestamp");
        anonymized.Email.Should().Be($"deleted-{user.UserId:N}@deleted.local");
        anonymized.AccountStatus.Should().Be(AccountStatuses.Suspended);
    }
}

public sealed class InterviewBookingTests : SqliteServiceTestBase
{
    private async Task<(VolunteerService Service, Guid UserId, VerificationReview Review)> SeedBookableVolunteer()
    {
        SeedVolunteer(out var userId);
        var review = SeedReview(userId, VerificationReviewStatuses.Draft, withAllEvidence: true);
        await Db.SaveChangesAsync();
        return (NewVolunteerService(), userId, review);
    }

    [Fact]
    public async Task Two_volunteers_cannot_book_the_same_slot()
    {
        var (service, userA, reviewA) = await SeedBookableVolunteer();
        SeedVolunteer(out var userB);
        var reviewB = SeedReview(userB, VerificationReviewStatuses.Draft, withAllEvidence: true);
        var reviewer = SeedUser(RoleNames.Admin);
        var slot = SeedSlot(reviewer.UserId);
        await Db.SaveChangesAsync();

        await service.BookInterviewAsync(userA, slot.InterviewSlotId);

        var act = () => service.BookInterviewAsync(userB, slot.InterviewSlotId);
        await act.Should().ThrowAsync<AppException>()
            .Where(e => e.StatusCode == 409 && e.Code == ErrorCodes.InterviewSlotUnavailable);

        ClearTracker();
        (await Db.InterviewSlots.SingleAsync(s => s.InterviewSlotId == slot.InterviewSlotId))
            .Status.Should().Be(InterviewSlotStatuses.Booked, "the winner keeps the slot");
        var roundA = await Db.VerificationReviews.SingleAsync(r => r.ReviewId == reviewA.ReviewId);
        roundA.Status.Should().Be(VerificationReviewStatuses.InterviewScheduled);
        roundA.InterviewSlotId.Should().Be(slot.InterviewSlotId);
        var roundB = await Db.VerificationReviews.SingleAsync(r => r.ReviewId == reviewB.ReviewId);
        roundB.InterviewSlotId.Should().BeNull("the loser must not take the slot");
        roundB.Status.Should().Be(VerificationReviewStatuses.Draft);
        Notifications.Sent.Should().Contain(n => n.Recipient == reviewer.UserId && n.Type == NotificationTypes.InterviewBooked);
    }

    [Fact]
    public async Task The_same_volunteer_cannot_hold_two_bookings()
    {
        var (service, userId, _) = await SeedBookableVolunteer();
        var reviewer = SeedUser(RoleNames.Admin);
        var first = SeedSlot(reviewer.UserId);
        var second = SeedSlot(reviewer.UserId);
        await Db.SaveChangesAsync();

        await service.BookInterviewAsync(userId, first.InterviewSlotId);

        var act = () => service.BookInterviewAsync(userId, second.InterviewSlotId);
        await act.Should().ThrowAsync<AppException>()
            .Where(e => e.StatusCode == 409 && e.Code == ErrorCodes.InterviewAlreadyBooked);

        ClearTracker();
        (await Db.InterviewSlots.SingleAsync(s => s.InterviewSlotId == second.InterviewSlotId))
            .Status.Should().Be(InterviewSlotStatuses.Available, "the refused booking must not consume the second slot");
    }

    [Fact]
    public async Task Cancel_releases_the_slot_resets_the_round_and_notifies_the_reviewer()
    {
        var (service, userId, review) = await SeedBookableVolunteer();
        var reviewer = SeedUser(RoleNames.Admin);
        var slot = SeedSlot(reviewer.UserId);
        await Db.SaveChangesAsync();

        await service.BookInterviewAsync(userId, slot.InterviewSlotId);
        await service.CancelInterviewAsync(userId);

        ClearTracker();
        (await Db.InterviewSlots.SingleAsync(s => s.InterviewSlotId == slot.InterviewSlotId))
            .Status.Should().Be(InterviewSlotStatuses.Available);
        var round = await Db.VerificationReviews.SingleAsync(r => r.ReviewId == review.ReviewId);
        round.Status.Should().Be(VerificationReviewStatuses.Draft);
        round.InterviewSlotId.Should().BeNull();
        round.InterviewAt.Should().BeNull();
        round.ReviewerId.Should().BeNull();
        Notifications.Sent.Should().Contain(n => n.Recipient == userId && n.Type == NotificationTypes.InterviewCancelled);
        Notifications.Sent.Should().Contain(n => n.Recipient == reviewer.UserId && n.Type == NotificationTypes.InterviewCancelled);

        // The original reset semantics are preserved: the freed slot can be booked again.
        await service.BookInterviewAsync(userId, slot.InterviewSlotId);
        ClearTracker();
        (await Db.InterviewSlots.SingleAsync(s => s.InterviewSlotId == slot.InterviewSlotId))
            .Status.Should().Be(InterviewSlotStatuses.Booked);
    }

    [Fact]
    public async Task Change_moves_the_booking_to_the_new_slot()
    {
        var (service, userId, review) = await SeedBookableVolunteer();
        var reviewer = SeedUser(RoleNames.Admin);
        var oldSlot = SeedSlot(reviewer.UserId);
        var newSlot = SeedSlot(reviewer.UserId);
        await Db.SaveChangesAsync();

        await service.BookInterviewAsync(userId, oldSlot.InterviewSlotId);
        await service.ChangeInterviewAsync(userId, newSlot.InterviewSlotId);

        ClearTracker();
        (await Db.InterviewSlots.SingleAsync(s => s.InterviewSlotId == oldSlot.InterviewSlotId))
            .Status.Should().Be(InterviewSlotStatuses.Available, "the old slot is released in the same transaction");
        (await Db.InterviewSlots.SingleAsync(s => s.InterviewSlotId == newSlot.InterviewSlotId))
            .Status.Should().Be(InterviewSlotStatuses.Booked);
        (await Db.VerificationReviews.SingleAsync(r => r.ReviewId == review.ReviewId))
            .InterviewSlotId.Should().Be(newSlot.InterviewSlotId);
        Notifications.Sent.Should().Contain(n => n.Recipient == reviewer.UserId && n.Type == NotificationTypes.InterviewChanged);
    }

    [Fact]
    public async Task The_filtered_unique_index_allows_one_open_round_per_volunteer()
    {
        SeedVolunteer(out var userId);
        SeedReview(userId, VerificationReviewStatuses.Pending);
        await Db.SaveChangesAsync();

        await using var second = NewSecondContext();
        second.VerificationReviews.Add(new VerificationReview
        {
            ReviewId = Guid.NewGuid(),
            VolunteerId = userId,
            VerificationLevel = VerificationLevels.Basic,
            Status = VerificationReviewStatuses.Draft,
            CreatedAt = Clock.UtcNow
        });
        var act = () => second.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("a second open round violates UX_VERIFICATION_REVIEW_volunteer_open");

        // A closed round plus a new open one stays allowed (re-application by design).
        await using var third = NewSecondContext();
        third.VerificationReviews.Add(new VerificationReview
        {
            ReviewId = Guid.NewGuid(),
            VolunteerId = userId,
            VerificationLevel = VerificationLevels.Basic,
            Status = VerificationReviewStatuses.Rejected,
            CreatedAt = Clock.UtcNow.AddDays(-10),
            DecidedAt = Clock.UtcNow.AddDays(-10)
        });
        await third.SaveChangesAsync();
    }
}

public sealed class EvidenceTests : SqliteServiceTestBase
{
    [Fact]
    public async Task Replacing_evidence_deletes_the_replaced_file_from_storage()
    {
        SeedVolunteer(out var userId);
        SeedReview(userId, VerificationReviewStatuses.Draft, singleEvidenceType: EvidenceTypes.NationalIdFront,
            storageKey: "old-file");
        await Db.SaveChangesAsync();
        var service = NewVolunteerService();

        using var upload = new FileUpload(new MemoryStream([1, 2, 3]), "front.jpg", "image/jpeg", 3);
        await service.UploadEvidenceAsync(userId, EvidenceTypes.NationalIdFront, upload);

        ClearTracker();
        var rows = await Db.VerificationEvidences.Where(e => e.EvidenceType == EvidenceTypes.NationalIdFront).ToListAsync();
        rows.Should().ContainSingle("the replacement keeps exactly one row per evidence type");
        Storage.DeletedKeys.Should().Contain("old-file-national_id_front",
            "the replaced file is removed from storage instead of staying orphaned");
    }

    [Fact]
    public async Task Retention_cleanup_deletes_expired_evidence_of_closed_rounds_only()
    {
        SeedVolunteer(out var userId);

        var closed = SeedReview(userId, VerificationReviewStatuses.Rejected,
            singleEvidenceType: EvidenceTypes.NationalIdFront, storageKey: "expired-closed");
        closed.DecidedAt = Clock.UtcNow.AddDays(-100);

        SeedVolunteer(out var openUserId);
        var open = SeedReview(openUserId, VerificationReviewStatuses.Pending,
            singleEvidenceType: EvidenceTypes.NationalIdFront, storageKey: "expired-open");

        SeedVolunteer(out var futureUserId);
        var future = SeedReview(futureUserId, VerificationReviewStatuses.Approved,
            singleEvidenceType: EvidenceTypes.NationalIdFront, storageKey: "not-yet-expired");
        future.DecidedAt = Clock.UtcNow.AddDays(-1);
        await Db.SaveChangesAsync();

        // Expire the closed and open rounds' evidence; keep the third inside retention.
        await Db.VerificationEvidences
            .Where(e => e.ReviewId == closed.ReviewId || e.ReviewId == open.ReviewId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RetentionUntil, Clock.UtcNow.AddMinutes(-1)));

        await RunCleanupOnceAsync();

        ClearTracker();
        (await Db.VerificationEvidences.AnyAsync(e => e.ReviewId == closed.ReviewId))
            .Should().BeFalse("expired evidence of a closed round is removed");
        Storage.DeletedKeys.Should().Contain("expired-closed-national_id_front");
        (await Db.VerificationEvidences.AnyAsync(e => e.ReviewId == open.ReviewId))
            .Should().BeTrue("an open round keeps its documents even past retention_until");
        Storage.DeletedKeys.Should().NotContain("expired-open-national_id_front");
        (await Db.VerificationEvidences.AnyAsync(e => e.ReviewId == future.ReviewId))
            .Should().BeTrue("evidence inside the retention window is kept");
    }

    private async Task RunCleanupOnceAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HamzetWaslDbContext>(Db);
        services.AddSingleton<IFileStorage>(Storage);
        services.AddSingleton(Protector);
        services.AddSingleton<IClock>(Clock);
        await using var provider = services.BuildServiceProvider();

        var cleanup = new EvidenceRetentionCleanupService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Retention,
            NullLogger<EvidenceRetentionCleanupService>.Instance);

        // ExecuteAsync waits 5 minutes before the first pass, so the test invokes the
        // single-pass method directly; the hosting loop itself is trivial.
        var method = typeof(EvidenceRetentionCleanupService)
            .GetMethod("CleanupOnceAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CleanupOnceAsync not found.");
        await (Task)method.Invoke(cleanup, [CancellationToken.None])!;
    }
}
