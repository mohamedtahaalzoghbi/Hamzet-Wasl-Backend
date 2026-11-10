using System.Net;
using System.Net.Http.Headers;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Common;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Regression tests for the security hardening: account lockout, phone ownership,
/// enumeration, the rate limiter and the exposure of the OpenAPI document.
/// </summary>
[Collection("Api")]
public class SecurityHardeningTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;

    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private string Email(string prefix) => $"{prefix}{Suffix}@example.com";

    private string Phone() => "010" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("D8");

    private async Task<AuthResponse> RegisterAsync(string label)
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/register", new
        {
            role = "disabled_user",
            name = "Security Tester",
            email = Email(label),
            phone = Phone(),
            password = "Passw0rd!",
            confirmPassword = "Passw0rd!",
            termsAccepted = true,
            policyVersion = "1.0"
        });

        Assert.Equal(201, status);
        Assert.Null(error);
        return data!;
    }

    // ---------------------------------------------------------------- I2

    [Fact]
    public async Task The_account_is_locked_after_five_failed_logins()
    {
        var user = await RegisterAsync("lockout");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (status, _, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
            {
                emailOrPhone = user.Email,
                password = "WrongPassword!"
            });

            Assert.Equal(401, status);
            Assert.Equal("INVALID_CREDENTIALS", error!.Code);
        }

        // The correct password no longer works while the account is locked.
        var (lockedStatus, _, lockedError) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = user.Email,
            password = "Passw0rd!"
        });

        Assert.Equal(401, lockedStatus);
        Assert.Equal("INVALID_CREDENTIALS", lockedError!.Code);
    }

    [Fact]
    public async Task An_unknown_account_answers_the_same_error_as_a_wrong_password()
    {
        var (status, _, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = Email("unknown"),
            password = "Passw0rd!"
        });

        Assert.Equal(401, status);
        Assert.Equal("INVALID_CREDENTIALS", error!.Code);
    }

    // ---------------------------------------------------------------- I6

    [Fact]
    public async Task The_reset_password_endpoint_does_not_reveal_whether_the_address_exists()
    {
        var (unknownStatus, _, unknownError) = await Client.PostAsync<object>("/api/v1/auth/reset-password", new
        {
            email = Email("nobody"),
            token = "whatever",
            newPassword = "Passw0rd!",
            confirmPassword = "Passw0rd!"
        });

        Assert.Equal(400, unknownStatus);
        Assert.Equal("OTP_INVALID", unknownError!.Code);

        var user = await RegisterAsync("reset");
        var (wrongStatus, _, wrongError) = await Client.PostAsync<object>("/api/v1/auth/reset-password", new
        {
            email = user.Email,
            token = "wrong-token",
            newPassword = "Passw0rd!",
            confirmPassword = "Passw0rd!"
        });

        Assert.Equal(400, wrongStatus);
        Assert.Equal("OTP_INVALID", wrongError!.Code);
    }

    // ---------------------------------------------------------------- I5

    [Fact]
    public async Task The_phone_number_is_only_written_after_the_code_is_verified()
    {
        var user = await RegisterAsync("phone");
        var newPhone = "011" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("D8");

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var (sendStatus, _, sendError) = await client.PostAsync<object>("/api/v1/auth/phone/send-code", new { phone = newPhone });
        Assert.Equal(204, sendStatus);
        Assert.Null(sendError);

        // The session still shows the old number: the new one is only pending.
        var (_, session, _) = await client.GetAsync<object>("/api/v1/auth/session");
        Assert.NotNull(session);
        Assert.DoesNotContain(newPhone, session!.ToString()!, StringComparison.Ordinal);

        // The code proves the ownership and then the number is applied.
        var code = fixture.Messages.LastCode;
        Assert.NotNull(code);

        var (verifyStatus, _, verifyError) = await client.PostAsync<object>("/api/v1/auth/phone/verify", new { code });
        Assert.Equal(204, verifyStatus);
        Assert.Null(verifyError);

        var (_, verifiedSession, _) = await client.GetAsync<object>("/api/v1/auth/session");
        Assert.NotNull(verifiedSession);
        Assert.Contains(newPhone, verifiedSession!.ToString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_phone_code_endpoint_does_not_reveal_whether_a_number_is_registered()
    {
        var other = await RegisterAsync("taken");
        var user = await RegisterAsync("probe");

        using var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        // The number of the other account: the endpoint must not say that it exists.
        var (status, _, error) = await client.PostAsync<object>("/api/v1/auth/phone/send-code", new { phone = other.Phone });

        Assert.Equal(204, status);
        Assert.Null(error);
    }

    // ---------------------------------------------------------------- I14

    [Fact]
    public async Task The_rate_limiter_answers_429_with_a_retry_after_header()
    {
        using var factory = new HamzetWaslWebApplicationFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = "2",
            ["RateLimiting:AuthWindowMinutes"] = "1"
        });

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });

        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < 5 && limited is null; attempt++)
        {
            var response = await client.PostAsync("/api/v1/auth/login", new StringContent(
                """{"emailOrPhone":"nobody@example.com","password":"Passw0rd!"}""",
                System.Text.Encoding.UTF8, "application/json"));
            response.Dispose();

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
            }
        }

        Assert.NotNull(limited);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited!.StatusCode);
        Assert.True(limited.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(values!.First(), out var seconds));
        Assert.True(seconds > 0);
    }
}
