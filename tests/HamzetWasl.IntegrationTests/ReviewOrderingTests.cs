using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Batch 2 (S1) regression tests: "the latest review" must be chosen by
/// <c>VERIFICATION_REVIEW.created_at</c>, never by the review id.
///
/// The two ids are deliberately the inverse of the timestamps
/// (<c>ReviewId = ffff…</c> is the oldest, <c>000…001</c> is the newest), so any
/// regression to <c>OrderByDescending(r =&gt; r.ReviewId)</c> fails the test.
/// </summary>
[Collection("Api")]
public class ReviewOrderingTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;

    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    /// <summary>
    /// Two ids that are unique per test (the database is shared) but still the inverse of their
    /// timestamps. SQL Server compares the last six bytes of a GUID first, so sharing that group
    /// makes <c>ORDER BY review_id DESC</c> return the "ffff…" row first.
    /// </summary>
    private (Guid Older, Guid Newer) ReviewIds() => (
        Guid.Parse($"ffffffff-ffff-ffff-ffff-00{Suffix}"),
        Guid.Parse($"00000000-0000-0000-0000-00{Suffix}"));

    private int _registrationCounter;

    private string Email(string prefix) => $"{prefix}{Suffix}{_registrationCounter++}@example.com";

    private static string Phone() => "010" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
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
    /// Inserts one review row directly. The ordering of the read queries is what is under
    /// test, so the rows are written through the DbContext instead of a long UI flow.
    /// </summary>
    private async Task AddReviewAsync(Guid volunteerId, Guid reviewId, DateTime createdAt)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HamzetWaslDbContext>();

        db.VerificationReviews.Add(new VerificationReview
        {
            ReviewId = reviewId,
            VolunteerId = volunteerId,
            ReviewerId = null,
            VerificationLevel = VerificationLevels.Basic,
            Status = VerificationReviewStatuses.Pending,
            CreatedAt = createdAt
        });

        await db.SaveChangesAsync();
    }

    private static int IndexOf(IReadOnlyList<VolunteerReviewListItemDto> items, Guid reviewId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].ReviewId == reviewId)
            {
                return index;
            }
        }

        return -1;
    }

    // ------------------------------------------------- volunteer read path

    [Fact]
    public async Task The_latest_review_is_the_newest_created_at_not_the_largest_review_id()
    {
        var user = await RegisterAsync("volunteer", "Review Ordering");
        var client = Authed(user.AccessToken);
        var (olderReviewId, newerReviewId) = ReviewIds();

        await AddReviewAsync(user.UserId, olderReviewId, DateTime.UtcNow.AddMinutes(-10));
        await AddReviewAsync(user.UserId, newerReviewId, DateTime.UtcNow.AddMinutes(-1));

        var (status, data, error) = await client.GetAsync<VerificationResponse>("/api/v1/me/verification");

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(data);

        // The removed ordering (review_id DESC) returned the "ffff…" row here.
        Assert.Equal<Guid?>(newerReviewId, data!.ReviewId);
    }

    // ------------------------------------------------------- admin queue

    [Fact]
    public async Task The_admin_queue_uses_created_at_as_the_tie_breaker()
    {
        var user = await RegisterAsync("volunteer", "Admin Queue Ordering");
        var (olderReviewId, newerReviewId) = ReviewIds();

        // Both rows have interview_at = NULL, so the queue falls through to created_at.
        await AddReviewAsync(user.UserId, olderReviewId, DateTime.UtcNow.AddMinutes(-10));
        await AddReviewAsync(user.UserId, newerReviewId, DateTime.UtcNow.AddMinutes(-1));

        var admin = await AdminLoginAsync();
        var adminClient = Authed(admin.AccessToken);

        var (status, queue, error) = await adminClient.GetAsync<PagedResult<VolunteerReviewListItemDto>>(
            "/api/v1/admin/volunteer-reviews?status=pending&page=1&pageSize=100");

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(queue);

        var newerIndex = IndexOf(queue!.Items, newerReviewId);
        var olderIndex = IndexOf(queue.Items, olderReviewId);

        Assert.True(newerIndex >= 0, "The newest review is missing from the admin queue.");
        Assert.True(olderIndex >= 0, "The oldest review is missing from the admin queue.");
        Assert.True(
            newerIndex < olderIndex,
            $"Expected created_at ordering, but the newest row was at {newerIndex} and the oldest at {olderIndex}.");
    }
}
