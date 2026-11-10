using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Interfaces;
using HamzetWasl.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HamzetWasl.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SymmetricSecurityKey _key;
    private readonly JwtSecurityTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
    }

    public AccessTokenResult CreateAccessToken(UserSessionDto session)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, session.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new("role", session.Role),
            new("accountStatus", session.AccountStatus),
            new("emailVerified", session.EmailVerified.ToString().ToLowerInvariant()),
            new("phoneVerified", session.PhoneVerified.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Email, session.Email),
            new("name", session.DisplayName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TokenPurposes.Claim, TokenPurposes.Access)
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(_handler.WriteToken(token), expires);
    }

    public RefreshTokenResult CreateRefreshToken(Guid userId)
    {
        var now = _clock.UtcNow;
        var expires = now.AddDays(_options.RefreshTokenDays);
        var tokenId = Guid.NewGuid();
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, tokenId.ToString()),
                new Claim(TokenPurposes.Claim, TokenPurposes.Refresh)
            ],
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new RefreshTokenResult(_handler.WriteToken(token), expires, tokenId.ToString());
    }

    public RefreshTokenPayload? ValidateRefreshToken(string token)
    {
        var principal = Validate(token, TokenPurposes.Refresh);
        if (principal is null) return null;

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!Guid.TryParse(sub, out var userId) || !Guid.TryParse(jti, out var tokenId)) return null;

        var exp = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var expires = exp is not null && long.TryParse(exp, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : _clock.UtcNow.AddDays(_options.RefreshTokenDays);

        return new RefreshTokenPayload(userId, tokenId, expires);
    }

    public string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string CreateGoogleRegistrationTicket(string email, string? name, string googleSubject)
    {
        var now = _clock.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("name", name ?? string.Empty),
                new Claim("google_sub", googleSubject),
                new Claim(TokenPurposes.Claim, TokenPurposes.GoogleRegistration)
            ],
            notBefore: now,
            expires: now.AddMinutes(_options.GoogleTicketMinutes),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return _handler.WriteToken(token);
    }

    public GoogleRegistrationTicketPayload? ValidateGoogleRegistrationTicket(string ticket)
    {
        var principal = Validate(ticket, TokenPurposes.GoogleRegistration);
        if (principal is null) return null;

        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
        var subject = principal.FindFirstValue("google_sub");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(subject)) return null;

        var exp = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
        var expires = exp is not null && long.TryParse(exp, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : _clock.UtcNow;

        return new GoogleRegistrationTicketPayload(email, principal.FindFirstValue("name"), subject, expires);
    }

    private ClaimsPrincipal? Validate(string token, string purpose)
    {
        try
        {
            var principal = _handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = JwtRegisteredClaimNames.Sub,
                RoleClaimType = "role"
            }, out _);

            return principal.FindFirstValue(TokenPurposes.Claim) == purpose ? principal : null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
