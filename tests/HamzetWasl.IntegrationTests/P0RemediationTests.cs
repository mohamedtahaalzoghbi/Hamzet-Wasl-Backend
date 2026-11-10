using System.Net.Http.Headers;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Regression tests for the P0 contract and security fixes:
/// token purposes, UTC timestamps, the notification payload and the accessibility preset.
/// </summary>
[Collection("Api")]
public class P0RemediationTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private string Email(string prefix) => $"{prefix}{Suffix}@example.com";

    private string Phone() => "010" + (DateTime.UtcNow.Ticks % 100_000_000).ToString("D8");

    private async Task<AuthResponse> RegisterAsync(string role, string name)
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/register", new
        {
            role,
            name,
            email = Email(role),
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

    // ---------------------------------------------------------------- C1

    [Fact]
    public async Task A_refresh_token_is_rejected_on_protected_endpoints()
    {
        var user = await RegisterAsync("volunteer", "Token Purpose");
        var refresh = user.RefreshToken;

        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refresh);

        var (status, _, error) = await client.GetAsync<ProfileResponse>("/api/v1/me/profile");
        Assert.Equal(401, status);
        Assert.Equal("UNAUTHORIZED", error!.Code);

        var (sessionStatus, _, sessionError) = await client.GetAsync<object>("/api/v1/auth/session");
        Assert.Equal(401, sessionStatus);
        Assert.Equal("UNAUTHORIZED", sessionError!.Code);
    }

    [Fact]
    public async Task A_refresh_token_is_rejected_on_admin_endpoints()
    {
        var admin = await AdminLoginAsync();
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.RefreshToken);

        var (status, _, error) = await client.GetAsync<object>("/api/v1/admin/audit");
        Assert.Equal(401, status);
        Assert.Equal("UNAUTHORIZED", error!.Code);
    }

    [Fact]
    public async Task An_access_token_still_works_and_the_refresh_rotation_is_preserved()
    {
        var user = await RegisterAsync("volunteer", "Token Rotation");
        var client = Authed(user.AccessToken);

        var (status, _, error) = await client.GetAsync<ProfileResponse>("/api/v1/me/profile");
        Assert.Equal(200, status);
        Assert.Null(error);

        // The refresh token still rotates and returns a new pair.
        var (refreshStatus, refreshed, refreshError) = await Client.PostAsync<AuthResponse>(
            "/api/v1/auth/refresh", new { refreshToken = user.RefreshToken });
        Assert.Equal(200, refreshStatus);
        Assert.Null(refreshError);
        Assert.NotEqual(user.RefreshToken, refreshed!.RefreshToken);

        // The consumed refresh token cannot be used again.
        var (replayStatus, _, _) = await Client.PostAsync<AuthResponse>(
            "/api/v1/auth/refresh", new { refreshToken = user.RefreshToken });
        Assert.Equal(401, replayStatus);

        // And the new access token works.
        var newClient = Authed(refreshed.AccessToken);
        var (newStatus, _, _) = await newClient.GetAsync<ProfileResponse>("/api/v1/me/profile");
        Assert.Equal(200, newStatus);
    }

    [Fact]
    public async Task Admin_endpoints_still_require_the_admin_role()
    {
        var volunteer = await RegisterAsync("volunteer", "Role Check");
        var client = Authed(volunteer.AccessToken);

        var (status, _, error) = await client.GetAsync<object>("/api/v1/admin/audit");
        Assert.Equal(403, status);
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    // ---------------------------------------------------------------- C2

    [Fact]
    public async Task Timestamps_are_utc_whether_they_come_from_the_database_or_are_new()
    {
        var user = await RegisterAsync("volunteer", "Timezones");
        Assert.EndsWith("Z", user.AccessTokenExpiresAt.ToUniversalTime().ToString("O"), StringComparison.Ordinal);

        var admin = await AdminLoginAsync();
        var adminClient = Authed(admin.AccessToken);

        // A timestamp that is read back from SQL Server.
        var (createdStatus, _, createdError) = await adminClient.PostAsync<object>("/api/v1/admin/interview-slots", new
        {
            startsAt = DateTime.UtcNow.AddDays(4),
            durationMinutes = 30
        });
        Assert.Equal(201, createdStatus);
        Assert.Null(createdError);

        var raw = await adminClient.GetStringAsync("/api/v1/admin/interview-slots");
        Assert.Contains("Z", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("+00:00", raw, StringComparison.Ordinal);

        // A timestamp that is read back from SQL Server through a paged endpoint.
        var auditRaw = await adminClient.GetStringAsync("/api/v1/admin/audit?page=1&pageSize=1");
        Assert.Contains("\"occurredAt\":", auditRaw, StringComparison.Ordinal);
        Assert.Matches("\"occurredAt\":\"[^\"]+Z\"", auditRaw);
    }

    // ---------------------------------------------------------------- C4

    [Fact]
    public async Task The_notification_payload_matches_the_published_contract()
    {
        var publisher = fixture.Factory.Services.GetRequiredService<RecordingNotificationPublisher>();

        var admin = await AdminLoginAsync();
        var adminClient = Authed(admin.AccessToken);

        var (slot, _, slotError) = await adminClient.PostAsync<object>("/api/v1/admin/interview-slots", new
        {
            startsAt = DateTime.UtcNow.AddDays(5),
            durationMinutes = 30
        });
        Assert.Null(slotError);

        // The organization decision publishes a notification for the owner.
        var email = Email("notify");
        var phone = Phone();
        var (registerStatus, organization, registerError) = await Client.PostAsync<AuthResponse>(
            "/api/v1/auth/register", new
            {
                role = "company",
                organizationName = "Notify Company",
                email,
                phone,
                password = "Passw0rd!",
                confirmPassword = "Passw0rd!",
                termsAccepted = true,
                policyVersion = "1.0"
            });
        Assert.Equal(201, registerStatus);
        Assert.Null(registerError);

        var ownerClient = Authed(organization!.AccessToken);
        await ownerClient.PutAsync<object>("/api/v1/me/organization", new
        {
            name = "Notify Company",
            field = "technology",
            description = "شركة للاختبار",
            governorate = "cairo",
            city = "nasr_city",
            employeeCountRange = "11-50",
            contactInformation = "contact@example.com"
        });

        var pdf = "%PDF-1.4\n%Test document\n%%EOF"u8.ToArray();
        foreach (var documentType in new[] { "commercial_register", "tax_card", "address_proof" })
        {
            await ownerClient.UploadAsync<object>(
                $"/api/v1/me/organization/documents/{documentType}", $"{documentType}.pdf", pdf, "application/pdf");
        }

        // The logo is part of the required profile.
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        await ownerClient.UploadAsync<object>(
            "/api/v1/me/organization/logo", "logo.png", png, "image/png");

        var (submit, submitResponse, submitError) =
            await ownerClient.PostAsync<HamzetWasl.Application.Contracts.Organization.SubmitOrganizationResponse>(
                "/api/v1/me/organization/submit");
        Assert.Null(submitError);
        Assert.Equal(200, submit);
        Assert.NotNull(submitResponse);

        var reviewId = submitResponse!.ReviewId;

        await adminClient.PostAsync<object>(
            $"/api/v1/admin/organization-reviews/{reviewId}/decision", new { approve = true, notes = (string?)null });

        var payload = publisher.Last;
        Assert.NotNull(payload);
        Assert.Equal("organization_approved", payload!.Type);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.False(string.IsNullOrWhiteSpace(payload.TitleAr));
        Assert.False(string.IsNullOrWhiteSpace(payload.BodyAr));
        Assert.Equal(DateTimeKind.Utc, payload.CreatedAt.Kind);
        Assert.Contains("\"channel\"", payload.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("organization_review", payload.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_signalr_client_method_name_is_the_contracted_one()
    {
        Assert.Equal("notificationReceived", HamzetWasl.Api.Realtime.SignalRNotificationPublisher.ClientMethod);
    }

    // ---------------------------------------------------------------- C5

    [Fact]
    public async Task The_effective_preset_is_calculated_from_the_need_type()
    {
        var user = await RegisterAsync("disabled_user", "Preset User");
        var client = Authed(user.AccessToken);

        var (status, _, error) = await client.PutAsync<AccessibilityResponse>("/api/v1/me/accessibility", new
        {
            needType = "visual",
            theme = "system",
            fontScale = 1.0,
            reducedMotion = false,
            interactionMode = "touch",
            notificationMode = "in_app"
        });

        Assert.Equal(200, status);
        Assert.Null(error);

        var (getStatus, accessibility, getError) = await client.GetAsync<AccessibilityResponse>("/api/v1/me/accessibility");
        Assert.Equal(200, getStatus);
        Assert.Null(getError);

        Assert.NotNull(accessibility);
        Assert.Equal("high_contrast", accessibility!.EffectivePreset.Theme);
        Assert.Equal(1.25m, accessibility.EffectivePreset.FontScale);
        Assert.Equal("screen_reader", accessibility.EffectivePreset.InteractionMode);
        Assert.Equal("visual_alert", accessibility.EffectivePreset.NotificationMode);
        Assert.True(accessibility.EffectivePreset.HighContrast);
        Assert.Contains("visual", accessibility.EffectivePreset.NeedTypes);
    }

    [Fact]
    public async Task A_manual_override_wins_over_the_need_type_defaults()
    {
        var user = await RegisterAsync("disabled_user", "Override User");
        var client = Authed(user.AccessToken);

        var overrides = JsonDocument.Parse("""{"theme":"light","needTypes":["visual","hearing"]}""").RootElement;

        var (status, _, error) = await client.PutAsync<AccessibilityResponse>("/api/v1/me/accessibility", new
        {
            needType = "visual",
            theme = "system",
            fontScale = 1.0,
            reducedMotion = false,
            interactionMode = "touch",
            notificationMode = "in_app",
            manualOverrides = overrides
        });

        Assert.Equal(200, status);
        Assert.Null(error);

        var (_, accessibility, _) = await client.GetAsync<AccessibilityResponse>("/api/v1/me/accessibility");
        Assert.NotNull(accessibility);
        Assert.Equal("light", accessibility!.EffectivePreset.Theme);
        Assert.Contains("visual", accessibility.EffectivePreset.NeedTypes);
        Assert.Contains("hearing", accessibility.EffectivePreset.NeedTypes);
    }

    private async Task<AuthResponse> AdminLoginAsync()
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = "admin@hamzetwasl.local",
            password = "Admin@12345"
        });

        Assert.Equal(200, status);
        Assert.Null(error);
        return data!;
    }
}
