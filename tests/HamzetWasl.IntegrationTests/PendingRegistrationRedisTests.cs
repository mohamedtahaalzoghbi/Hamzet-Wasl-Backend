using HamzetWasl.Domain.Constants;
using HamzetWasl.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Executes the Redis backend of the pending-registration store against a real server.
/// Runs only when HW_TEST_REDIS is set (CI provides a redis:7-alpine service); without a
/// server the tests pass as no-ops, and the in-memory backend's own tests still apply.
/// </summary>
public sealed class PendingRegistrationRedisTests
{
    private static readonly string? Configuration = Environment.GetEnvironmentVariable("HW_TEST_REDIS");

    private static PendingRegistrationRecord Record(string codeHash, DateTime now) =>
        new(new PendingRegistration("password", RoleNames.Volunteer, "م", "a@b.com", "+2010", "hash", null, "1.0"),
            codeHash, now.AddMinutes(10), now);

    [Fact]
    public async Task Concurrent_consumes_of_one_ticket_yield_exactly_one_success()
    {
        if (Configuration is null) return;

        await using var redis = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var store = new RedisPendingRegistrationStore(redis);
        var pendingId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await store.SaveAsync(pendingId, Record("code-hash", now), TimeSpan.FromMinutes(10));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            store.ConsumeAsync(pendingId, "code-hash", 3, now)));

        Assert.Equal(1, outcomes.Count(o => o.Status == PendingConsumeStatus.Success));
        Assert.Equal(19, outcomes.Count(o => o.Status == PendingConsumeStatus.TicketUnknown));
    }

    [Fact]
    public async Task Concurrent_wrong_codes_cannot_exceed_the_attempt_limit()
    {
        if (Configuration is null) return;

        await using var redis = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var store = new RedisPendingRegistrationStore(redis);
        var pendingId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await store.SaveAsync(pendingId, Record("code-hash", now), TimeSpan.FromMinutes(10));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            store.ConsumeAsync(pendingId, "wrong-hash", 3, now)));

        Assert.Equal(3, outcomes.Count(o => o.Status == PendingConsumeStatus.Invalid));
        Assert.Equal(7, outcomes.Count(o => o.Status == PendingConsumeStatus.TooManyAttempts));
        Assert.NotNull(await store.GetAsync(pendingId));
    }

    [Fact]
    public async Task A_consumed_ticket_stays_consumed_after_the_lock_is_released()
    {
        if (Configuration is null) return;

        await using var redis = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var store = new RedisPendingRegistrationStore(redis);
        var pendingId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await store.SaveAsync(pendingId, Record("code-hash", now), TimeSpan.FromMinutes(10));

        var first = await store.ConsumeAsync(pendingId, "code-hash", 3, now);
        var second = await store.ConsumeAsync(pendingId, "code-hash", 3, now);

        Assert.Equal(PendingConsumeStatus.Success, first.Status);
        Assert.Equal(PendingConsumeStatus.TicketUnknown, second.Status);
    }
}
