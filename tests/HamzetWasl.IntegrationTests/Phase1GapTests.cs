using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Notifications;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Regression tests for the Phase 1 gaps closed after the UI/ERD review:
/// the account-deletion request (Q21) and the notification centre (Q22).
/// </summary>
[Collection("Api")]
public class Phase1GapTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;

    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private int _registrationCounter;

    private string Email(string prefix) => $"{prefix}{Suffix}{_registrationCounter++}@example.com";

    private static string Phone() => "010" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<AuthResponse> RegisterAsync(string role, string name)
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/register", new
        {
            role,
            name,
            email = Email(role),
            phone = Phone(),
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
            termsAccepted = true,
            policyVersion = "1.0"
        });

        Assert.Equal(201, status);
        Assert.Null(error);
        return data!;
    }

    private async Task<AuthResponse> AdminLoginAsync()
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = "admin@hamzetwasl.local",
            password = "Admin@12345"
        });

        Assert.Equal(200, status);
        Assert.Null(error);
        return data!;
    }

    /// <summary>
    /// Inserts a notification row directly: the list endpoint is what is under test here,
    /// not the producer (the producer is covered by the P0 SignalR tests).
    /// </summary>
    private async Task<Guid> AddNotificationAsync(
        Guid recipientId,
        Guid notificationId,
        string notificationType,
        string bodyAr,
        DateTime createdAt)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();

        db.Notifications.Add(new Notification
        {
            NotificationId = notificationId,
            RecipientId = recipientId,
            NotificationType = notificationType,
            Channel = NotificationChannels.InApp,
            TextBody = bodyAr,
            CreatedAt = createdAt
        });

        await db.SaveChangesAsync();
        return notificationId;
    }

    // ---------------------------------------------------------------- Q21

    [Fact]
    public async Task A_deletion_request_is_created_once_and_reaches_the_admin_queue()
    {
        var user = await RegisterAsync("volunteer", "Deletion Requester");
        var client = Authed(user.AccessToken);

        var (firstStatus, created, firstError) =
            await client.PostAsync<DeletionRequestResponse>("/api/v1/me/deletion-request");

        Assert.Equal(201, firstStatus);
        Assert.Null(firstError);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.DeletionRequestId);
        Assert.Equal(DeletionRequestStatuses.Pending, created.Status);

        // The contract serialises UTC ISO-8601, so the round-tripped value must be Utc.
        Assert.Equal(DateTimeKind.Utc, created.RequestedAt.Kind);

        // A second call must not create a duplicate pending row.
        var (secondStatus, _, secondError) =
            await client.PostAsync<DeletionRequestResponse>("/api/v1/me/deletion-request");

        Assert.Equal(409, secondStatus);
        Assert.NotNull(secondError);
        Assert.Equal(ErrorCodes.DeletionRequestAlreadyPending, secondError!.Code);

        // The admin queue is the other half of the flow: the row must be visible there.
        var admin = await AdminLoginAsync();
        var adminClient = Authed(admin.AccessToken);

        var (queueStatus, queue, queueError) = await adminClient.GetAsync<PagedResult<DeletionRequestDto>>(
            "/api/v1/admin/deletion-requests?status=pending&page=1&pageSize=100");

        Assert.Equal(200, queueStatus);
        Assert.Null(queueError);
        Assert.NotNull(queue);

        var row = Assert.Single(queue!.Items, item => item.DeletionRequestId == created.DeletionRequestId);
        Assert.Equal(user.UserId, row.UserId);
        Assert.Equal(DeletionRequestStatuses.Pending, row.Status);
        Assert.Null(row.ResolvedAt);
        Assert.Equal(DateTimeKind.Utc, row.RequestedAt.Kind);
    }

    // ---------------------------------------------------------------- Q22

    [Fact]
    public async Task Notifications_are_returned_newest_first_and_paged()
    {
        var user = await RegisterAsync("volunteer", "Notification Reader");
        var client = Authed(user.AccessToken);

        var (emptyStatus, empty, emptyError) =
            await client.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications");

        Assert.Equal(200, emptyStatus);
        Assert.Null(emptyError);
        Assert.NotNull(empty);
        Assert.Empty(empty!.Items);
        Assert.Equal(0, empty.TotalCount);

        var now = DateTime.UtcNow;

        // The ids are deliberately the inverse of the timestamps: ordering by the GUID
        // would return the wrong order, so this pins the created_at ordering.
        var oldestId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var middleId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var newestId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        await AddNotificationAsync(user.UserId, oldestId, NotificationTypes.VerificationRejected, "الأقدم", now.AddMinutes(-3));
        await AddNotificationAsync(user.UserId, middleId, NotificationTypes.InterviewBooked, "الأوسط", now.AddMinutes(-2));
        await AddNotificationAsync(user.UserId, newestId, NotificationTypes.VerificationApproved, "الأحدث", now.AddMinutes(-1));

        var (firstStatus, firstPage, firstError) =
            await client.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications?page=1&pageSize=2");

        Assert.Equal(200, firstStatus);
        Assert.Null(firstError);
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage!.Items.Count);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);

        Assert.Equal(newestId, firstPage.Items[0].Id);
        Assert.Equal(middleId, firstPage.Items[1].Id);
        Assert.False(firstPage.Items[0].IsRead);
        Assert.Null(firstPage.Items[0].ReadAt);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.Items[0].TitleAr));
        Assert.Equal("الأحدث", firstPage.Items[0].BodyAr);
        Assert.Equal(DateTimeKind.Utc, firstPage.Items[0].CreatedAt.Kind);

        var (secondStatus, secondPage, secondError) =
            await client.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications?page=2&pageSize=2");

        Assert.Equal(200, secondStatus);
        Assert.Null(secondError);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage!.Items);
        Assert.Equal(oldestId, secondPage.Items[0].Id);
    }

    [Fact]
    public async Task Another_user_cannot_mark_someone_elses_notification_as_read()
    {
        var owner = await RegisterAsync("volunteer", "Notification Owner");
        var notificationId = Guid.NewGuid();
        await AddNotificationAsync(owner.UserId, notificationId, NotificationTypes.OrganizationApproved,
            "إشعار المالك", DateTime.UtcNow.AddMinutes(-1));

        var other = await RegisterAsync("volunteer", "Notification Intruder");
        var otherClient = Authed(other.AccessToken);

        // 404 (not 403): the API must not confirm that another user's notification exists.
        var (status, _, error) =
            await otherClient.PutAsync<object>($"/api/v1/me/notifications/{notificationId}/read");

        Assert.Equal(404, status);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.NotFound, error!.Code);

        // The owner's notification stays unread, and the intruder's list stays empty.
        var ownerClient = Authed(owner.AccessToken);
        var (_, ownerPage, _) =
            await ownerClient.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications");
        Assert.False(Assert.Single(ownerPage!.Items).IsRead);

        var (_, otherPage, _) =
            await otherClient.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications");
        Assert.Empty(otherPage!.Items);
    }

    [Fact]
    public async Task Marking_a_notification_read_is_idempotent()
    {
        var user = await RegisterAsync("volunteer", "Notification Idempotent");
        var client = Authed(user.AccessToken);
        var notificationId = Guid.NewGuid();
        await AddNotificationAsync(user.UserId, notificationId, NotificationTypes.AccountReactivated,
            "تم تفعيل الحساب", DateTime.UtcNow.AddMinutes(-1));

        var (firstStatus, _, firstError) =
            await client.PutAsync<object>($"/api/v1/me/notifications/{notificationId}/read");

        Assert.Equal(204, firstStatus);
        Assert.Null(firstError);

        var (_, firstPage, _) = await client.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications");
        var firstReadAt = Assert.Single(firstPage!.Items).ReadAt;
        Assert.NotNull(firstReadAt);

        // A second PUT must succeed without moving the first read timestamp.
        var (secondStatus, _, secondError) =
            await client.PutAsync<object>($"/api/v1/me/notifications/{notificationId}/read");

        Assert.Equal(204, secondStatus);
        Assert.Null(secondError);

        var (_, secondPage, _) = await client.GetAsync<PagedResult<NotificationDto>>("/api/v1/me/notifications");
        var item = Assert.Single(secondPage!.Items);
        Assert.True(item.IsRead);
        Assert.Equal(firstReadAt, item.ReadAt);
    }
}
