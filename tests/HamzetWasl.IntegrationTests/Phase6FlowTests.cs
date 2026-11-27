using System.Net.Http.Headers;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Phase6;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class Phase6FlowTests(ApiFixture fixture)
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
        name = role is "association" or "company" or "shop" ? null : name,
        organizationName = role is "association" or "company" or "shop" ? name : null,
        email,
        phone,
        password = "Passw0rd!",
        confirmPassword = "Passw0rd!",
        termsAccepted = true,
        policyVersion = "1.0"
    };

    private async Task<AuthResponse> AdminLogin()
    {
        var (status, data, _) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new { emailOrPhone = "admin@hamzetwasl.local", password = "Admin@12345" });
        Assert.Equal(200, status);
        return data!;
    }

    private async Task<HttpClient> ApprovedOrg(HttpClient admin, string role, string name, string prefix)
    {
        var org = Authed((await Register(role, name, Email(prefix), Phone())).AccessToken);
        await org.PutAsync<OrganizationResponse>("/api/v1/me/organization", new { name, field = "technology", description = "x", governorate = "cairo", city = "nasr_city", website = "https://example.com", employeeCountRange = "11-50", contactInformation = "c@example.com" });
        await org.UploadAsync<OrganizationResponse>("/api/v1/me/organization/logo", "logo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png");
        var pdf = "%PDF-1.4\n%Test document\n%%EOF"u8.ToArray();
        foreach (var t in new[] { "commercial_register", "tax_card", "address_proof" })
            await org.UploadAsync<OrganizationResponse>($"/api/v1/me/organization/documents/{t}", $"{t}.pdf", pdf, "application/pdf");
        var (_, sub, _) = await org.PostAsync<SubmitOrganizationResponse>("/api/v1/me/organization/submit");
        await admin.PostAsync<object?>($"/api/v1/admin/organization-reviews/{sub!.ReviewId}/decision", new { approve = true, notes = (string?)null });
        return org;
    }

    [Fact]
    public async Task Directory_places_sos_stats()
    {
        var admin = Authed((await AdminLogin()).AccessToken);
        var assoc = await ApprovedOrg(admin, "association", "Dir Assoc", "da");
        var du = Authed((await Register("disabled_user", "Seeker", Email("ds"), Phone())).AccessToken);
        var vol = Authed((await Register("volunteer", "Vol", Email("dv"), Phone())).AccessToken);

        // Directory: only associations create; drafts are hidden from the public.
        var (sDu, _, _) = await du.PostAsync<DirectoryItemDto>("/api/v1/directory/items", new { itemType = "service", title = "x", description = "d", publish = true });
        Assert.Equal(403, sDu);
        var (sBad, _, _) = await assoc.PostAsync<DirectoryItemDto>("/api/v1/directory/items", new { itemType = "bogus", title = "x", description = "d", publish = true });
        Assert.Equal(400, sBad);
        var (sC, pub, _) = await assoc.PostAsync<DirectoryItemDto>("/api/v1/directory/items", new { itemType = "service", title = "دعم نفسي", description = "جلسات", publish = true });
        Assert.Equal(201, sC);
        var (_, draft, _) = await assoc.PostAsync<DirectoryItemDto>("/api/v1/directory/items", new { itemType = "event", title = "مسودة", description = "d", publish = false });
        var (_, list, _) = await Client.GetAsync<PagedResult<DirectoryItemDto>>("/api/v1/directory/items?pageSize=100");
        Assert.Contains(list!.Items, i => i.ItemId == pub!.ItemId);
        Assert.DoesNotContain(list.Items, i => i.ItemId == draft!.ItemId);
        var (_, mine, _) = await assoc.GetAsync<List<DirectoryItemDto>>("/api/v1/directory/items/mine");
        Assert.Contains(mine!, i => i.ItemId == draft!.ItemId);

        // Inquiry thread between a user and the association.
        var (sInq, inq, _) = await du.PostAsync<InquiryDto>($"/api/v1/directory/items/{pub!.ItemId}/inquiries", new { subject = "استفسار", body = "عايز أعرف المواعيد" });
        Assert.Equal(201, sInq);
        var (_, recv, _) = await assoc.GetAsync<List<InquiryDto>>("/api/v1/directory/inquiries?received=true");
        Assert.Contains(recv!, i => i.InquiryId == inq!.InquiryId);
        var (sRep, rep, _) = await assoc.PostAsync<InquiryDto>($"/api/v1/directory/inquiries/{inq!.InquiryId}/messages", new { body = "السبت 5 مساءً" });
        Assert.Equal(200, sRep);
        Assert.Equal(2, rep!.Messages.Count);
        var (sOther, _, _) = await vol.GetAsync<InquiryDto>($"/api/v1/directory/inquiries/{inq.InquiryId}");
        Assert.Equal(404, sOther);

        // Places: admin creates; users review and report obstacles.
        var (sPd, _, _) = await du.PostAsync<PlaceDto>("/api/v1/places", new { name = "x", address = "y", latitude = 30.0, longitude = 31.0, verificationSource = "admin" });
        Assert.Equal(403, sPd);
        var (_, types, _) = await Client.GetAsync<List<string>>("/api/v1/places/facility-types");
        var facs = types!.Count > 0 ? new[] { new { type = types[0], status = "available", notes = (string?)null } } : Array.Empty<object>().Select(_ => new { type = "", status = "", notes = (string?)null }).ToArray();
        var (sP, place, e1) = await admin.PostAsync<PlaceDto>("/api/v1/places", new { name = "مستشفى", address = "القاهرة", latitude = 30.05, longitude = 31.23, verificationSource = "admin", facilities = facs });
        Assert.True(sP == 201, e1?.Message);
        var (_, plist, _) = await Client.GetAsync<PagedResult<PlaceDto>>("/api/v1/places?pageSize=100");
        Assert.Contains(plist!.Items, p => p.PlaceId == place!.PlaceId);
        var (sRv, rv, _) = await du.PutAsync<PlaceDto>($"/api/v1/places/{place!.PlaceId}/review", new { rating = 5, comment = "ممتاز" });
        Assert.Equal(200, sRv);
        Assert.Equal(1, rv!.ReviewsCount);
        var (sRb, _, _) = await du.PutAsync<PlaceDto>($"/api/v1/places/{place.PlaceId}/review", new { rating = 9, comment = "x" });
        Assert.Equal(400, sRb);
        var (sOb, ob, _) = await du.PostAsync<ObstacleDto>("/api/v1/places/obstacles", new { placeId = place.PlaceId, description = "السلم بدون منحدر", latitude = 30.05, longitude = 31.23 });
        Assert.Equal(201, sOb);
        var (sObAdm, _, _) = await du.PutAsync<ObstacleDto>($"/api/v1/places/obstacles/{ob!.ObstacleId}/status", new { status = "resolved" });
        Assert.Equal(403, sObAdm);
        var (sObOk, obR, _) = await admin.PutAsync<ObstacleDto>($"/api/v1/places/obstacles/{ob.ObstacleId}/status", new { status = "resolved" });
        Assert.Equal(200, sObOk);
        Assert.Equal("resolved", obR!.Status);

        // SOS: needs a contact first; contact is masked; association cannot use it.
        var (sNo, _, eNo) = await du.PostAsync<SosAlertDto>("/api/v1/sos/alerts", new { latitude = 30.0, longitude = 31.0, message = "help" });
        Assert.Equal(422, sNo);
        Assert.Equal("NO_EMERGENCY_CONTACTS", eNo?.Code);
        var (sBadC, _, _) = await du.PostAsync<EmergencyContactDto>("/api/v1/sos/contacts", new { displayName = "أمي", destination = "abc" });
        Assert.Equal(400, sBadC);
        var (sCt, ct, _) = await du.PostAsync<EmergencyContactDto>("/api/v1/sos/contacts", new { displayName = "أمي", destination = "01012345678" });
        Assert.Equal(201, sCt);
        Assert.DoesNotContain("01012345678", ct!.MaskedDestination);
        var (sAl, al, _) = await du.PostAsync<SosAlertDto>("/api/v1/sos/alerts", new { latitude = 30.0, longitude = 31.0, message = "help" });
        Assert.Equal(201, sAl);
        Assert.Single(al!.Deliveries);
        var (_, mineAl, _) = await du.GetAsync<List<SosAlertDto>>("/api/v1/sos/alerts/mine");
        Assert.Contains(mineAl!, a => a.AlertId == al.AlertId);
        var (sAs, _, _) = await assoc.GetAsync<List<EmergencyContactDto>>("/api/v1/sos/contacts");
        Assert.Equal(403, sAs);

        // Stats.
        var (sPs, ps, _) = await Client.GetAsync<PublicStatsDto>("/api/v1/stats/public");
        Assert.Equal(200, sPs);
        Assert.True(ps!.Places >= 1);
        var (sAd, ad, _) = await admin.GetAsync<AdminStatsDto>("/api/v1/stats/admin");
        Assert.Equal(200, sAd);
        Assert.True(ad!.SosAlertsLast30Days >= 1);
        var (sAdU, _, _) = await du.GetAsync<AdminStatsDto>("/api/v1/stats/admin");
        Assert.Equal(403, sAdU);
    }
}
