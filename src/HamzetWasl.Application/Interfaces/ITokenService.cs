using HamzetWasl.Application.Contracts.Auth;

namespace HamzetWasl.Application.Interfaces;

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public sealed record RefreshTokenResult(string Token, DateTime ExpiresAt, string TokenId);

public sealed record RefreshTokenPayload(Guid UserId, Guid TokenId, DateTime ExpiresAt);

public sealed record GoogleRegistrationTicketPayload(string Email, string? Name, string GoogleSubject, DateTime ExpiresAt);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(UserSessionDto session);
    RefreshTokenResult CreateRefreshToken(Guid userId);
    RefreshTokenPayload? ValidateRefreshToken(string token);
    string HashRefreshToken(string token);
    string CreateGoogleRegistrationTicket(string email, string? name, string googleSubject);
    GoogleRegistrationTicketPayload? ValidateGoogleRegistrationTicket(string ticket);
}
