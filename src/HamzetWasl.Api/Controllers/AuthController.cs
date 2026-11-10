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

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        [FromBody] RegisterRequest request, CancellationToken ct) =>
        CreatedEnvelope(await _auth.RegisterAsync(request, ct));

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        [FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.LoginAsync(request, ct)));

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("google")]
    public async Task<ActionResult<ApiResponse<GoogleAuthResponse>>> Google(
        [FromBody] GoogleAuthRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.GoogleAsync(request, ct)));

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(
        [FromBody] RefreshRequest request, CancellationToken ct) =>
        Ok(Envelope(await _auth.RefreshAsync(request, ct)));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request, CancellationToken ct)
    {
        await _auth.LogoutAsync(CurrentUserId, request?.RefreshToken, ct);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        await _auth.ChangePasswordAsync(CurrentUserId, request, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("email/send-code")]
    public async Task<IActionResult> SendEmailCode(CancellationToken ct)
    {
        await _auth.SendEmailCodeAsync(CurrentUserId, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("email/verify")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyCodeRequest request, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(CurrentUserId, request.Code, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("phone/send-code")]
    public async Task<IActionResult> SendPhoneCode(
        [FromBody] SendPhoneCodeRequest request, CancellationToken ct)
    {
        await _auth.SendPhoneCodeAsync(CurrentUserId, request.Phone, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("otp")]
    [HttpPost("phone/verify")]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyCodeRequest request, CancellationToken ct)
    {
        await _auth.VerifyPhoneAsync(CurrentUserId, request.Code, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("session")]
    public async Task<ActionResult<ApiResponse<UserSessionDto>>> Session(CancellationToken ct) =>
        Ok(Envelope(await _auth.GetSessionAsync(CurrentUserId, ct)));
}
