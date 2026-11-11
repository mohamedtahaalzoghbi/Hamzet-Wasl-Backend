using System.Net.Http.Headers;
using HamzetWasl.Application.Common;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Profile;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// Batch 4: <c>PUT /me/accessibility</c> accepts one to three need types, keeps the primary
/// type in <c>need_type_id</c>, stores the extras in <c>manual_overrides.needTypes</c>, and
/// rejects a fourth type with a field-level validation error.
/// </summary>
[Collection("Api")]
public class AccessibilityNeedTypesTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;

    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private int _registrationCounter;

    private string Email(string prefix) => $"{prefix}{Suffix}{_registrationCounter++}@example.com";

    private static string Phone() => "010" + Random.Shared.Next(0, 100_000_000).ToString("D8");

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<AuthResponse> RegisterAsync()
    {
        var (status, data, error) = await Client.PostAsync<AuthResponse>("/api/v1/auth/register", new
        {
            role = "disabled_user",
            name = "Multiple Needs",
            email = Email("accessibility"),
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

    private static bool HasField(ApiError error, string field) =>
        error.Fields is not null &&
        error.Fields.Keys.Any(key => string.Equals(key, field, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task One_and_two_need_types_are_accepted_and_four_are_rejected()
    {
        var user = await RegisterAsync();
        var client = Authed(user.AccessToken);

        // فئة واحدة: تبقى في need_type_id ولا تُكتب في manual_overrides.
        var (oneStatus, one, oneError) = await client.PutAsync<AccessibilityResponse>(
            "/api/v1/me/accessibility",
            new
            {
                needType = "visual",
                needTypes = new[] { "visual" },
                theme = "system",
                fontScale = 1.0m,
                reducedMotion = false,
                interactionMode = "touch",
                notificationMode = "in_app"
            });

        Assert.Equal(200, oneStatus);
        Assert.Null(oneError);
        Assert.Equal("visual", one!.NeedType);
        Assert.Equal(new[] { "visual" }, one.NeedTypes);
        Assert.Equal(new[] { "visual" }, one.EffectivePreset.NeedTypes);

        // فئتان: الأساسية أولاً ثم الإضافية، وتظهران في الاستجابة والـ preset.
        var (twoStatus, two, twoError) = await client.PutAsync<AccessibilityResponse>(
            "/api/v1/me/accessibility",
            new
            {
                needType = "visual",
                needTypes = new[] { "visual", "hearing" },
                theme = "system",
                fontScale = 1.0m,
                reducedMotion = false,
                interactionMode = "touch",
                notificationMode = "in_app"
            });

        Assert.Equal(200, twoStatus);
        Assert.Null(twoError);
        Assert.Equal("visual", two!.NeedType);
        Assert.Equal(new[] { "visual", "hearing" }, two.NeedTypes);
        Assert.Contains("visual", two.EffectivePreset.NeedTypes);
        Assert.Contains("hearing", two.EffectivePreset.NeedTypes);

        // الفئتان محفوظتان فعلاً في قاعدة البيانات.
        var (getStatus, stored, getError) = await client.GetAsync<AccessibilityResponse>(
            "/api/v1/me/accessibility");
        Assert.Equal(200, getStatus);
        Assert.Null(getError);
        Assert.Equal(new[] { "visual", "hearing" }, stored!.NeedTypes);

        // أربع فئات مرفوضة بخطأ تحقق مرتبط بالحقل needTypes، ولا تغيّر المحفوظ.
        var (fourStatus, _, fourError) = await client.PutAsync<AccessibilityResponse>(
            "/api/v1/me/accessibility",
            new
            {
                needType = "visual",
                needTypes = new[] { "visual", "hearing", "motor", "visual" },
                theme = "system",
                fontScale = 1.0m,
                reducedMotion = false,
                interactionMode = "touch",
                notificationMode = "in_app"
            });

        Assert.Equal(400, fourStatus);
        Assert.NotNull(fourError);
        Assert.True(HasField(fourError!, "needTypes"));

        var (unchangedStatus, unchanged, _) = await client.GetAsync<AccessibilityResponse>(
            "/api/v1/me/accessibility");
        Assert.Equal(200, unchangedStatus);
        Assert.Equal(new[] { "visual", "hearing" }, unchanged!.NeedTypes);
    }
}
