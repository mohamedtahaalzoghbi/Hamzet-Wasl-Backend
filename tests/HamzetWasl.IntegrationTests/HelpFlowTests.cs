using System.Net.Http.Headers;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Help;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class HelpFlowTests(ApiFixture fixture)
{
    private HttpClient Client => fixture.AnonymousClient;
    private string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    private HttpClient Authed(string accessToken)
    {
        var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost")
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private string Email(string prefix) => $"{prefix}{Suffix}@example.com";
    private string Phone()
    {
        var ticks = DateTime.UtcNow.Ticks % 100_000_000;
        return $"010{ticks:D8}";
    }

    /// <summary>Registers a user through the two-step OTP flow and asserts it succeeded.</summary>
    private async Task<AuthResponse> Register(string role, string name, string email, string phone)
    {
        return await Client.RegisterVerifiedAsync(fixture.Messages, RegisterBody(role, name, email, phone));
    }

    /// <summary>Registers a user and asserts the call failed with the given status.</summary>
    private async Task<ApiError> RegisterFailing(
        string role, string name, string email, string phone, int expectedStatus)
    {
        var (status, _, error) =
            await Client.PostAsync<AuthResponse>("/api/v1/auth/register", RegisterBody(role, name, email, phone));
        Assert.Equal(expectedStatus, status);
        Assert.NotNull(error);
        return error!;
    }

    private static object RegisterBody(string role, string name, string email, string phone) => new
    {
        role,
        name = role is "association" or "company" ? null : name,
        organizationName = role is "association" or "company" ? name : null,
        email,
        phone,
        password = "Passw0rd!",
        confirmPassword = "Passw0rd!",
        termsAccepted = true,
        policyVersion = "1.0"
    };

    private async Task<AuthResponse> AdminLogin()
    {
        var (status, data, _) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = "admin@hamzetwasl.local",
            password = "Admin@12345"
        });

        Assert.Equal(200, status);
        Assert.NotNull(data);
        return data!;
    }


    private async Task<HttpClient> ApprovedVolunteer()
    {
        var email = Email("hvol");
        var phone = Phone();
        var user = await Register("volunteer", "Help Volunteer", email, phone);
        var client = Authed(user.AccessToken);
        var adminClient = Authed((await AdminLogin()).AccessToken);

        await client.PostAsync<object?>("/api/v1/auth/email/send-code");
        await client.PostAsync<object?>("/api/v1/auth/email/verify", new { code = fixture.Factory.Messages.LastCode });
        await client.PostAsync<object?>("/api/v1/auth/phone/send-code", new { phone });
        await client.PostAsync<object?>("/api/v1/auth/phone/verify", new { code = fixture.Factory.Messages.LastCode });
        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        foreach (var t in new[] { "national_id_front", "national_id_back", "face_photo" })
            await client.UploadFormAsync<object?>("/api/v1/me/verification/evidence", $"{t}.jpg", jpg, "image/jpeg",
                new Dictionary<string, string> { ["evidenceType"] = t });
        await client.PutAsync<object?>("/api/v1/me/verification/social-links",
            new { links = new[] { new { platform = "facebook", url = "https://facebook.com/example" } } });
        var (_, slot, _) = await adminClient.PostAsync<InterviewSlotAdminDto>("/api/v1/admin/interview-slots",
            new { startsAt = DateTime.UtcNow.AddDays(3), durationMinutes = 30 });
        var (_, booked, _) = await client.PostAsync<VerificationResponse>("/api/v1/me/verification/interview",
            new { interviewSlotId = slot!.InterviewSlotId });
        var (_, _, err) = await adminClient.PostAsync<object?>(
            $"/api/v1/admin/volunteer-reviews/{booked!.ReviewId}/decision", new { approve = true, notes = (string?)null });
        Assert.Null(err);
        return client;
    }

    [Fact]
    public async Task Help_request_full_lifecycle()
    {
        var du = Authed((await Register("disabled_user", "Help Requester", Email("hdu"), Phone())).AccessToken);
        var vol = await ApprovedVolunteer();

        var (s0, _, e0) = await vol.PostAsync<HelpRequestDto>("/api/v1/help/requests",
            new { helpMode = "online", category = "reading", approximateArea = "القاهرة" });
        Assert.Equal(403, s0);

        var (s1, bad, e1) = await du.PostAsync<HelpRequestDto>("/api/v1/help/requests",
            new { helpMode = "nope", category = "reading", approximateArea = "القاهرة" });
        Assert.Equal(400, s1);

        var (_, req, e2) = await du.PostAsync<HelpRequestDto>("/api/v1/help/requests",
            new { helpMode = "online", category = "reading", description = "عايز حد يقرأ لي جواب", approximateArea = "القاهرة", urgency = "normal" });
        Assert.Null(e2);
        Assert.Equal("open", req!.Status);

        var (_, mine, _) = await du.GetAsync<List<HelpRequestDto>>("/api/v1/help/requests/mine");
        Assert.Contains(mine!, r => r.RequestId == req.RequestId);

        var (_, open, eo) = await vol.GetAsync<PagedResult<HelpRequestDto>>("/api/v1/help/requests/open");
        Assert.Null(eo);
        Assert.Contains(open!.Items, r => r.RequestId == req.RequestId);

        var (_, offer, e3) = await vol.PostAsync<HelpOfferDto>($"/api/v1/help/requests/{req.RequestId}/offers", new { message = "أنا جاهز" });
        Assert.Null(e3);
        var (s4, _, _) = await vol.PostAsync<HelpOfferDto>($"/api/v1/help/requests/{req.RequestId}/offers", new { message = "تاني" });
        Assert.Equal(409, s4);

        var (_, detail, _) = await du.GetAsync<HelpRequestDetailDto>($"/api/v1/help/requests/{req.RequestId}");
        Assert.Single(detail!.Offers);
        Assert.False(detail.ChatOpen);

        var (_, _, e5) = await du.PostAsync<object?>($"/api/v1/help/requests/{req.RequestId}/accept", new { offerId = offer!.OfferId });
        Assert.Null(e5);

        var (_, _, e6) = await vol.PostAsync<object?>($"/api/v1/help/requests/{req.RequestId}/messages", new { text = "مرحبا" });
        Assert.Null(e6);
        var (_, msgs, _) = await du.GetAsync<List<HelpMessageDto>>($"/api/v1/help/requests/{req.RequestId}/messages");
        Assert.Single(msgs!);

        var (_, _, e7) = await du.PostAsync<object?>($"/api/v1/help/requests/{req.RequestId}/complete");
        Assert.Null(e7);
        var (_, _, e8) = await du.PostAsync<object?>($"/api/v1/help/requests/{req.RequestId}/feedback", new { rating = 5, comment = "ممتاز" });
        Assert.Null(e8);
        var (s9, _, _) = await du.PostAsync<object?>($"/api/v1/help/requests/{req.RequestId}/feedback", new { rating = 4 });
        Assert.Equal(409, s9);
        var (_, _, e10) = await vol.PostAsync<object?>($"/api/v1/help/requests/{req.RequestId}/safety-report", new { description = "اختبار" });
        Assert.Null(e10);

        // An unrelated disabled user cannot read the request.
        var other = Authed((await Register("disabled_user", "Other", Email("hdo"), Phone())).AccessToken);
        var (s11, _, _) = await other.GetAsync<HelpRequestDetailDto>($"/api/v1/help/requests/{req.RequestId}");
        Assert.Equal(403, s11);
    }

    [Fact]
    public async Task Help_endpoints_require_authentication()
    {
        var (status, _, _) = await Client.GetAsync<object?>("/api/v1/help/requests/mine");
        Assert.Equal(401, status);
    }
}
