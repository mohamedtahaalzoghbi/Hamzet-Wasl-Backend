using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Infrastructure.Configuration;
using HamzetWasl.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>
/// Access tokens and refresh tokens must be distinguishable: only access tokens may
/// authenticate a protected endpoint.
/// </summary>
public class TokenServiceTests
{
    private const string SigningKey = "unit-test-signing-key-that-is-long-enough-0123456789";

    private static TokenService CreateService() =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "HamzetWasl",
            Audience = "HamzetWasl.Clients",
            SigningKey = SigningKey
        }), new FixedClock());

    private sealed class FixedClock : Application.Interfaces.IClock
    {
        // The real clock: the token lifetime is validated against the wall clock.
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [Fact]
    public void An_access_token_carries_the_access_purpose()
    {
        var service = CreateService();

        var token = service.CreateAccessToken(new UserSessionDto(
            Guid.NewGuid(), "volunteer", "active", "user@example.com", true, "+201012345678", true, "Ø£Ø­Ù…Ø¯"));

        var principal = ReadClaims(token.Token);
        Assert.Equal(TokenPurposes.Access, principal.FirstOrDefault(c => c.Type == TokenPurposes.Claim)?.Value);
    }

    [Fact]
    public void A_refresh_token_carries_the_refresh_purpose()
    {
        var service = CreateService();

        var token = service.CreateRefreshToken(Guid.NewGuid());

        var principal = ReadClaims(token.Token);
        Assert.Equal(TokenPurposes.Refresh, principal.FirstOrDefault(c => c.Type == TokenPurposes.Claim)?.Value);
    }

    [Fact]
    public void The_two_purposes_are_never_equal()
    {
        Assert.NotEqual(TokenPurposes.Access, TokenPurposes.Refresh);
    }

    [Fact]
    public void A_refresh_token_is_not_validated_as_an_access_token()
    {
        var service = CreateService();

        var refresh = service.CreateRefreshToken(Guid.NewGuid());

        // The service level validation of a refresh token still works...
        Assert.NotNull(service.ValidateRefreshToken(refresh.Token));
        // ...but it does not carry the access purpose, so the bearer pipeline rejects it.
        var principal = ReadClaims(refresh.Token);
        Assert.NotEqual(TokenPurposes.Access, principal.FirstOrDefault(c => c.Type == TokenPurposes.Claim)?.Value);
    }

    private static IReadOnlyList<Claim> ReadClaims(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
}
