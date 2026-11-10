using System.Security.Cryptography;
using System.Text;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace HamzetWasl.Infrastructure.Services;

public sealed class OtpService
{
    private readonly OtpOptions _options;
    private readonly IDistributedCache _cache;
    private readonly IClock _clock;
    private readonly string _hashKey;

    public OtpService(IOptions<OtpOptions> options, IDistributedCache cache, IClock clock)
    {
        _options = options.Value;
        _cache = cache;
        _clock = clock;

        if (string.IsNullOrWhiteSpace(_options.HashKey) || _options.HashKey.Length < 16)
        {
            throw new InvalidOperationException(
                "Otp:HashKey is required and must be at least 16 characters. " +
                "Set it through the environment variable Otp__HashKey.");
        }

        _hashKey = _options.HashKey;
    }

    public string GenerateCode()
    {
        var max = (int)Math.Pow(10, _options.Length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', _options.Length));
    }

    public string Hash(Guid userId, string purpose, string code)
    {
        var bytes = Encoding.UTF8.GetBytes($"{userId:N}:{purpose}:{code}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hashKey));
        return Convert.ToBase64String(hmac.ComputeHash(bytes));
    }

    public bool Verify(Guid userId, string purpose, string code, string storedHash)
    {
        var computed = Encoding.UTF8.GetBytes(Hash(userId, purpose, code));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return computed.Length == stored.Length && CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    public async Task EnsureCanSendAsync(Guid userId, string purpose, CancellationToken ct)
    {
        var key = $"otp:cooldown:{purpose}:{userId:N}";
        var existing = await _cache.GetStringAsync(key, ct);
        if (existing is not null)
        {
            throw new AppException(429, ErrorCodes.OtpResendTooSoon);
        }
    }

    public async Task MarkSentAsync(Guid userId, string purpose, CancellationToken ct)
    {
        var key = $"otp:cooldown:{purpose}:{userId:N}";
        await _cache.SetStringAsync(key, _clock.UtcNow.ToString("O"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.ResendCooldownSeconds)
        }, ct);
    }

    public async Task<int> GetAttemptsAsync(Guid userId, string purpose, CancellationToken ct)
    {
        var value = await _cache.GetStringAsync($"otp:attempts:{purpose}:{userId:N}", ct);
        return int.TryParse(value, out var attempts) ? attempts : 0;
    }

    public async Task<int> IncrementAttemptsAsync(Guid userId, string purpose, CancellationToken ct)
    {
        var attempts = await GetAttemptsAsync(userId, purpose, ct) + 1;
        await _cache.SetStringAsync($"otp:attempts:{purpose}:{userId:N}", attempts.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.ExpiryMinutes + 5) }, ct);
        return attempts;
    }

    public async Task ResetAttemptsAsync(Guid userId, string purpose, CancellationToken ct)
    {
        await _cache.RemoveAsync($"otp:attempts:{purpose}:{userId:N}", ct);
    }

    public int ExpiryMinutes => _options.ExpiryMinutes;
    public int MaxAttempts => _options.MaxAttempts;
}
