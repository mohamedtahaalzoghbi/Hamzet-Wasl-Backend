using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace HamzetWasl.Infrastructure.Services;

/// <summary>The registration data captured at step 1, waiting for the phone OTP.</summary>
public sealed record PendingRegistration(
    string Provider,
    string Role,
    string DisplayName,
    string Email,
    string Phone,
    string? PasswordHash,
    string? GoogleSubject,
    string PolicyVersion);

public sealed record PendingRegistrationRecord(
    PendingRegistration Pending, string CodeHash, DateTime ExpiresAt, DateTime FirstIssuedAt);

public enum PendingConsumeStatus
{
    Success,
    TicketUnknown,
    CodeExpired,
    TooManyAttempts,
    Invalid
}

public sealed record PendingConsumeResult(
    PendingConsumeStatus Status, PendingRegistrationRecord? Record, int AttemptsLeft);

/// <summary>
/// Holds the short-lived pending registrations. The read-check-write behind
/// "verify the code and consume the ticket" is NOT atomic on a bare IDistributedCache,
/// so both backends take a per-ticket lock around the whole decision: a Redis lock when
/// Redis is configured (atomic across every API instance), an in-process semaphore
/// otherwise (atomic on the single instance that owns the ticket).
/// </summary>
public interface IPendingRegistrationStore
{
    Task SaveAsync(Guid pendingId, PendingRegistrationRecord record, TimeSpan ttl, CancellationToken ct = default);
    Task<PendingRegistrationRecord?> GetAsync(Guid pendingId, CancellationToken ct = default);
    Task<PendingConsumeResult> ConsumeAsync(
        Guid pendingId, string presentedCodeHash, int maxAttempts, DateTime now, CancellationToken ct = default);
}

public sealed class MemoryPendingRegistrationStore(IDistributedCache cache) : IPendingRegistrationStore
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public Task SaveAsync(Guid pendingId, PendingRegistrationRecord record, TimeSpan ttl, CancellationToken ct = default) =>
        cache.SetStringAsync(Key(pendingId), JsonSerializer.Serialize(record),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);

    public async Task<PendingRegistrationRecord?> GetAsync(Guid pendingId, CancellationToken ct = default)
    {
        var json = await cache.GetStringAsync(Key(pendingId), ct);
        return json is null ? null : JsonSerializer.Deserialize<PendingRegistrationRecord>(json);
    }

    public async Task<PendingConsumeResult> ConsumeAsync(
        Guid pendingId, string presentedCodeHash, int maxAttempts, DateTime now, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(pendingId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var record = await GetAsync(pendingId, ct);
            if (record is null)
            {
                return new PendingConsumeResult(PendingConsumeStatus.TicketUnknown, null, 0);
            }

            if (record.ExpiresAt < now)
            {
                return new PendingConsumeResult(PendingConsumeStatus.CodeExpired, null, 0);
            }

            var attempts = await GetAttemptsAsync(pendingId, ct);
            if (attempts >= maxAttempts)
            {
                return new PendingConsumeResult(PendingConsumeStatus.TooManyAttempts, null, 0);
            }

            if (!FixedTimeEquals(record.CodeHash, presentedCodeHash))
            {
                attempts++;
                await SetAttemptsAsync(pendingId, attempts, ct);
                return new PendingConsumeResult(PendingConsumeStatus.Invalid, null, Math.Max(0, maxAttempts - attempts));
            }

            await cache.RemoveAsync(Key(pendingId), ct);
            await cache.RemoveAsync(AttemptsKey(pendingId), ct);
            _locks.TryRemove(pendingId, out _);
            return new PendingConsumeResult(PendingConsumeStatus.Success, record, 0);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<int> GetAttemptsAsync(Guid pendingId, CancellationToken ct)
    {
        var value = await cache.GetStringAsync(AttemptsKey(pendingId), ct);
        return int.TryParse(value, out var attempts) ? attempts : 0;
    }

    private Task SetAttemptsAsync(Guid pendingId, int attempts, CancellationToken ct) =>
        cache.SetStringAsync(AttemptsKey(pendingId), attempts.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) }, ct);

    internal static string Key(Guid pendingId) => $"reg:pending:{pendingId:N}";
    internal static string AttemptsKey(Guid pendingId) => $"reg:attempts:{pendingId:N}";

    internal static bool FixedTimeEquals(string a, string b)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(a);
        var right = System.Text.Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }
}

/// <summary>
/// Redis backend: the same decision under a Redis lock (SET NX PX), so concurrent
/// completes on different API instances cannot both consume one ticket and the attempt
/// counter cannot under-count. Requires Redis:Configuration; the application falls back
/// to the in-memory backend without it.
/// </summary>
public sealed class RedisPendingRegistrationStore(IConnectionMultiplexer redis) : IPendingRegistrationStore
{
    private const string Prefix = "HamzetWasl:";

    public Task SaveAsync(Guid pendingId, PendingRegistrationRecord record, TimeSpan ttl, CancellationToken ct = default) =>
        Db.StringSetAsync(Key(pendingId), JsonSerializer.Serialize(record), ttl);

    public async Task<PendingRegistrationRecord?> GetAsync(Guid pendingId, CancellationToken ct = default)
    {
        var json = await Db.StringGetAsync(Key(pendingId));
        return json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PendingRegistrationRecord>((string)json!);
    }

    public async Task<PendingConsumeResult> ConsumeAsync(
        Guid pendingId, string presentedCodeHash, int maxAttempts, DateTime now, CancellationToken ct = default)
    {
        var db = Db;
        var lockKey = Prefix + "reg:lock:" + pendingId.ToString("N");
        var lockToken = Guid.NewGuid().ToString("N");

        // Wait briefly for a concurrent consume of the same ticket to finish.
        var acquired = false;
        for (var i = 0; i < 30 && !acquired; i++)
        {
            acquired = await db.LockTakeAsync(lockKey, lockToken, TimeSpan.FromSeconds(15));
            if (!acquired)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        if (!acquired)
        {
            // Extreme contention on one ticket: fail closed, the client retries.
            return new PendingConsumeResult(PendingConsumeStatus.TicketUnknown, null, 0);
        }

        try
        {
            var record = await GetAsync(pendingId, ct);
            if (record is null)
            {
                return new PendingConsumeResult(PendingConsumeStatus.TicketUnknown, null, 0);
            }

            if (record.ExpiresAt < now)
            {
                return new PendingConsumeResult(PendingConsumeStatus.CodeExpired, null, 0);
            }

            var attemptsValue = await db.StringGetAsync(AttemptsKey(pendingId));
            var attempts = attemptsValue.IsNullOrEmpty ? 0 : (int)attemptsValue;
            if (attempts >= maxAttempts)
            {
                return new PendingConsumeResult(PendingConsumeStatus.TooManyAttempts, null, 0);
            }

            if (!MemoryPendingRegistrationStore.FixedTimeEquals(record.CodeHash, presentedCodeHash))
            {
                attempts++;
                await db.StringSetAsync(AttemptsKey(pendingId), attempts, TimeSpan.FromHours(1));
                return new PendingConsumeResult(PendingConsumeStatus.Invalid, null, Math.Max(0, maxAttempts - attempts));
            }

            await db.KeyDeleteAsync([Key(pendingId), AttemptsKey(pendingId)]);
            return new PendingConsumeResult(PendingConsumeStatus.Success, record, 0);
        }
        finally
        {
            await db.LockReleaseAsync(lockKey, lockToken);
        }
    }

    private IDatabase Db => redis.GetDatabase();

    private static string Key(Guid pendingId) => Prefix + MemoryPendingRegistrationStore.Key(pendingId);
    private static string AttemptsKey(Guid pendingId) => Prefix + MemoryPendingRegistrationStore.AttemptsKey(pendingId);
}
