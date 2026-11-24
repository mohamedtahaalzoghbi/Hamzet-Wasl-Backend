using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// End-to-end coverage of the staged registration flow against the real API and
/// database: pending state before OTP, attempt bounding, single-use tickets, resend,
/// uniqueness conflicts and volunteer radius bounds. Unit tests cover the same rules
/// with fakes; these prove the wiring (validators, middleware, error codes, database).
/// </summary>
[Collection("Api")]
public class RegistrationFlowTests(ApiFixture fixture)
{
    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private string Email(string prefix) => $"{prefix}{Suffix}@example.com";

    private static string Phone() => "010" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private HttpClient Anon() => fixture.AnonymousClient;

    private HttpClient Authed(string token)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private object RegisterBody(string role, string email, string phone) => new
    {
        role,
        name = "Flow Tester",
        email,
        phone,
        password = "Passw0rd!",
        confirmPassword = "Passw0rd!",
        termsAccepted = true,
        policyVersion = "1.0"
    };

    private async Task<(RegisterPendingResponse Pending, string Code)> StartAsync(string role, string email, string phone)
    {
        var (status, pending, error) = await Anon().PostAsync<RegisterPendingResponse>(
            "/api/v1/auth/register", RegisterBody(role, email, phone));

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(pending);
        var code = fixture.Messages.LastCode;
        Assert.NotNull(code);
        return (pending!, code!);
    }

    [Fact]
    public async Task Register_returns_a_pending_ticket_and_no_account_can_log_in_yet()
    {
        var email = Email("pending");
        var phone = Phone();

        var (status, pending, error) = await Anon().PostAsync<RegisterPendingResponse>(
            "/api/v1/auth/register", RegisterBody("disabled_user", email, phone));

        Assert.Equal(200, status);
        Assert.Null(error);
        Assert.NotNull(pending);
        Assert.True(pending!.RequiresPhoneVerification);
        Assert.False(string.IsNullOrWhiteSpace(pending.RegistrationTicket));
        Assert.True(pending.ExpiresAt > DateTime.UtcNow);
        Assert.Equal("+20" + phone.TrimStart('0'), pending.Phone);

        // No account exists yet: the credentials must not authenticate.
        var (loginStatus, _, loginError) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = email,
            password = "Passw0rd!"
        });

        Assert.Equal(401, loginStatus);
        Assert.Equal("INVALID_CREDENTIALS", loginError!.Code);
    }

    [Fact]
    public async Task Complete_with_wrong_codes_is_bounded_and_then_rejects_even_the_right_code()
    {
        var (pending, code) = await StartAsync("disabled_user", Email("attempts"), Phone());
        var wrong = code == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (status, _, error) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
            {
                registrationTicket = pending.RegistrationTicket,
                code = wrong
            });

            Assert.Equal(422, status);
            Assert.Equal(ErrorCodes.OtpInvalid, error!.Code);
        }

        var (blockedStatus, _, blockedError) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code
        });

        Assert.Equal(422, blockedStatus);
        Assert.Equal(ErrorCodes.OtpTooManyAttempts, blockedError!.Code);
    }

    [Fact]
    public async Task Complete_creates_a_phone_verified_session_and_the_ticket_cannot_be_reused()
    {
        var (pending, code) = await StartAsync("disabled_user", Email("complete"), Phone());

        var (status, auth, error) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code
        });

        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(auth);

        var (_, session, sessionError) = await Authed(auth!.AccessToken).GetAsync<object>("/api/v1/auth/session");
        Assert.Null(sessionError);
        Assert.NotNull(session);
        Assert.Contains("\"phoneVerified\":true", session!.ToString(), StringComparison.Ordinal);

        var (reuseStatus, _, reuseError) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code
        });

        Assert.Equal(422, reuseStatus);
        Assert.Equal(ErrorCodes.RegistrationExpired, reuseError!.Code);
    }

    [Fact]
    public async Task An_unknown_ticket_is_rejected_as_expired()
    {
        var (status, _, error) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = Guid.NewGuid().ToString("N"),
            code = "123456"
        });

        Assert.Equal(422, status);
        Assert.Equal(ErrorCodes.RegistrationExpired, error!.Code);
    }

    [Fact]
    public async Task Concurrent_completes_of_one_ticket_create_exactly_one_account()
    {
        var (pending, code) = await StartAsync("disabled_user", Email("race"), Phone());

        var attempts = Enumerable.Range(0, 5).Select(_ =>
            Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
            {
                registrationTicket = pending.RegistrationTicket,
                code
            })).ToArray();
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(r => r.Item1 == 201));
        Assert.All(results.Where(r => r.Item1 != 201), r =>
        {
            Assert.Equal(422, r.Item1);
            Assert.Equal(ErrorCodes.RegistrationExpired, r.Item3!.Code);
        });

        var (loginStatus, _, _) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = results.First(r => r.Item1 == 201).Item2!.Email,
            password = "Passw0rd!"
        });
        Assert.Equal(200, loginStatus);
    }

    [Fact]
    public async Task Resend_issues_a_fresh_code_that_completes_the_registration()
    {
        var (pending, _) = await StartAsync("disabled_user", Email("resend"), Phone());

        var (resendStatus, _, resendError) = await Anon().PostAsync<object>("/api/v1/auth/register/resend", new
        {
            registrationTicket = pending.RegistrationTicket
        });

        Assert.Equal(204, resendStatus);
        Assert.Null(resendError);

        var freshCode = fixture.Messages.LastCode;
        Assert.NotNull(freshCode);

        var (status, auth, error) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code = freshCode
        });

        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(auth);
    }

    [Fact]
    public async Task A_taken_email_is_rejected_at_start_with_a_conflict()
    {
        var email = Email("taken");
        var (pending, code) = await StartAsync("disabled_user", email, Phone());
        await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code
        });

        var (status, _, error) = await Anon().PostAsync<RegisterPendingResponse>(
            "/api/v1/auth/register", RegisterBody("disabled_user", email, Phone()));

        Assert.Equal(409, status);
        Assert.Equal(ErrorCodes.EmailAlreadyExists, error!.Code);
    }

    [Fact]
    public async Task A_taken_phone_is_rejected_at_start_with_a_conflict()
    {
        var phone = Phone();
        var (pending, code) = await StartAsync("disabled_user", Email("phoneowner"), phone);
        await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code
        });

        var (status, _, error) = await Anon().PostAsync<RegisterPendingResponse>(
            "/api/v1/auth/register", RegisterBody("disabled_user", Email("phoneprobe"), phone));

        Assert.Equal(409, status);
        Assert.Equal(ErrorCodes.PhoneAlreadyExists, error!.Code);
    }

    [Fact]
    public async Task Volunteer_radius_is_bounded_to_1_through_100_km()
    {
        var (pending, code) = await StartAsync("volunteer", Email("radius"), Phone());
        var (_, auth, _) = await Anon().PostAsync<AuthResponse>("/api/v1/auth/register/complete", new
        {
            registrationTicket = pending.RegistrationTicket,
            code
        });
        Assert.NotNull(auth);

        var client = Authed(auth!.AccessToken);

        foreach (var radius in new[] { 0, 101 })
        {
            var (status, _, error) = await client.PutAsync<object>("/api/v1/me/volunteer-location", new
            {
                governorate = "cairo",
                serviceArea = "nasr_city",
                serviceRadiusKm = radius
            });

            Assert.Equal(400, status);
            Assert.NotNull(error);
        }

        foreach (var radius in new[] { 1, 100 })
        {
            var (status, _, error) = await client.PutAsync<object>("/api/v1/me/volunteer-location", new
            {
                governorate = "cairo",
                serviceArea = "nasr_city",
                serviceRadiusKm = radius
            });

            Assert.Equal(200, status);
            Assert.Null(error);
        }
    }
}
