using HamzetWasl.Application.Interfaces;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace HamzetWasl.UnitTests;

public sealed class OtpServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
    }

    private static OtpService CreateService(string? fixedCode)
    {
        var options = Options.Create(new OtpOptions
        {
            Length = 6,
            ExpiryMinutes = 10,
            MaxAttempts = 5,
            ResendCooldownSeconds = 60,
            HashKey = "unit-test-otp-hash-key-at-least-32-chars",
            FixedCode = fixedCode
        });
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new OtpService(options, cache, new FakeClock());
    }

    [Fact]
    public void GenerateCode_returns_fixed_code_when_configured()
    {
        var service = CreateService("123456");

        Assert.Equal("123456", service.GenerateCode());
        Assert.Equal("123456", service.GenerateCode());
    }

    [Fact]
    public void GenerateCode_returns_six_digit_code_when_fixed_code_is_null()
    {
        var service = CreateService(null);

        var code = service.GenerateCode();
        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12ab56")]
    public void Constructor_rejects_invalid_fixed_code(string invalid)
    {
        Assert.Throws<InvalidOperationException>(() => CreateService(invalid));
    }
}
