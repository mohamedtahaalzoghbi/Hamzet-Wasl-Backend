using System.Text.Json.Serialization;

namespace HamzetWasl.Application.Contracts.Auth;

public sealed class RegisterRequest
{
    public string Role { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? OrganizationName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public bool TermsAccepted { get; set; }
    public string PolicyVersion { get; set; } = "1.0";
}

public sealed class LoginRequest
{
    public string EmailOrPhone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class GoogleAuthRequest
{
    public string IdToken { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public bool TermsAccepted { get; set; }
    public string? RegistrationTicket { get; set; }
    public string PolicyVersion { get; set; } = "1.0";
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class VerifyCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public sealed class SendPhoneCodeRequest
{
    public string Phone { get; set; } = string.Empty;
}

public sealed record AuthResponse(
    Guid UserId,
    string Role,
    string AccountStatus,
    string Email,
    bool EmailVerified,
    string? Phone,
    bool PhoneVerified,
    string? DisplayName,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);

public sealed record UserSessionDto(
    Guid UserId,
    string Role,
    string AccountStatus,
    string Email,
    bool EmailVerified,
    string? Phone,
    bool PhoneVerified,
    string? DisplayName);

public sealed record GoogleRegistrationRequiredResponse(
    bool RequiresRegistration,
    string RegistrationTicket,
    string Email,
    string? Name,
    IReadOnlyList<string> AvailableRoles);

public sealed record GoogleAuthResponse(
    AuthResponse? Auth,
    GoogleRegistrationRequiredResponse? RegistrationRequired)
{
    [JsonIgnore]
    public bool IsRegistrationRequired => RegistrationRequired is not null;
}
