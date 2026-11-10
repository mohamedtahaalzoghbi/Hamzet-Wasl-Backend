using HamzetWasl.Application.Contracts.Auth;

namespace HamzetWasl.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<GoogleAuthResponse> GoogleAsync(GoogleAuthRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, string? refreshToken, CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task SendEmailCodeAsync(Guid userId, CancellationToken ct = default);
    Task VerifyEmailAsync(Guid userId, string code, CancellationToken ct = default);
    Task SendPhoneCodeAsync(Guid userId, string phone, CancellationToken ct = default);
    Task VerifyPhoneAsync(Guid userId, string code, CancellationToken ct = default);
    Task<UserSessionDto> GetSessionAsync(Guid userId, CancellationToken ct = default);
}
