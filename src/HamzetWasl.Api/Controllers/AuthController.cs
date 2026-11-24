using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HamzetWasl.Api.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>[Public — No Token] Sign Up Step 1: Validates registration data and sends phone OTP. Does NOT create the account in DB yet; returns registrationTicket.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterPendingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RegisterPendingResponse>>> Register(
        [FromBody] RegisterRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.RegisterAsync(request, ct)));

    /// <summary>[Public — No Token] Sign Up Step 2: Verifies phone OTP using registrationTicket, creates the account in DB, and returns JWT access &amp; refresh tokens.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
    [HttpPost("register/complete")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> CompleteRegistration(
        [FromBody] CompleteRegistrationRequest request, CancellationToken ct) =>
        CreatedEnvelope(await _auth.CompleteRegistrationAsync(request, ct));

    /// <summary>[Public — No Token] Resends the Sign Up phone OTP for an active registrationTicket.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("otp")]
    [HttpPost("register/resend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendRegistrationCode(
        [FromBody] ResendRegistrationCodeRequest request, CancellationToken ct)
    {
        await _auth.ResendRegistrationCodeAsync(request, ct);
        return NoContent();
    }

    /// <summary>[Public — No Token] Logs in with email or phone + password and returns JWT access &amp; refresh tokens.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.LoginAsync(request, ct)));

    /// <summary>[Public — No Token] Sign in or start sign up using a Google ID token.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("google")]
    [ProducesResponseType(typeof(ApiResponse<GoogleAuthResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<GoogleAuthResponse>>> Google(
        [FromBody] GoogleAuthRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.GoogleAsync(request, ct)));

    /// <summary>[Public — No Token] Rotates a refresh token into a fresh access &amp; refresh token pair.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(
        [FromBody] RefreshRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.RefreshAsync(request, ct)));

    /// <summary>[Bearer Token Required] Revokes active refresh tokens and logs out the current user.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request, CancellationToken ct)
    {
        await _auth.LogoutAsync(CurrentUserId, request?.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>[Public — No Token] Sends a password reset token to the email if an account exists.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    /// <summary>[Public — No Token] Resets the password using email + reset token + new password.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token Required] Changes the password for the currently logged-in user.</summary>
    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(CurrentUserId, request, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token Required] Sends an email verification OTP for an ALREADY logged-in user (e.g. Volunteer verification screen). NOT used during Sign Up.</summary>
    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("email/send-code")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendEmailCode(CancellationToken ct)
    {
        await _auth.SendEmailCodeAsync(CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token Required] Verifies the 6-digit email OTP for an ALREADY logged-in user. NOT used during Sign Up.</summary>
    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("email/verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyCodeRequest request, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(CurrentUserId, request.Code, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token Required] Sends a phone verification OTP for an ALREADY logged-in user (e.g. Volunteer verification or phone change). For Sign Up use POST /auth/register.</summary>
    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("phone/send-code")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendPhoneCode(
        [FromBody] SendPhoneCodeRequest request, CancellationToken ct)
    {
        await _auth.SendPhoneCodeAsync(CurrentUserId, request.Phone, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token Required] Verifies the 6-digit phone OTP for an ALREADY logged-in user. For Sign Up use POST /auth/register/complete.</summary>
    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("phone/verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyCodeRequest request, CancellationToken ct)
    {
        await _auth.VerifyPhoneAsync(CurrentUserId, request.Code, ct);
        return NoContent();
    }

    /// <summary>[Bearer Token Required] Returns the current logged-in user's session, role, accountStatus, and verification flags.</summary>
    [Authorize]
    [HttpGet("session")]
    [ProducesResponseType(typeof(ApiResponse<UserSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserSessionDto>>> Session(CancellationToken ct) =>
        Ok(Envelope(await _auth.GetSessionAsync(CurrentUserId, ct)));
}
