using System.Net.Http.Headers;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Jobs;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

[Collection("Api")]
public class JobsFlowTests(ApiFixture fixture)
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


    [Fact]
    public async Task Jobs_lifecycle()
    {
        var admin = Authed((await AdminLogin()).AccessToken);
        var org = Authed((await Register("company", "Jobs Co", Email("jc"), Phone())).AccessToken);
        var du = Authed((await Register("disabled_user", "Job Seeker", Email("js"), Phone())).AccessToken);

        var job = new { title = "مطور", description = "وصف", employmentType = "full_time", workArrangement = "remote", location = "القاهرة", accessibilityInformation = "مكتب مجهز" };
        var (sUn, _, _) = await org.PostAsync<JobDto>("/api/v1/jobs", job);
        Assert.Equal(403, sUn);

        await org.PutAsync<OrganizationResponse>("/api/v1/me/organization", new { name = "Jobs Co", field = "technology", description = "x", governorate = "cairo", city = "nasr_city", website = "https://example.com", employeeCountRange = "11-50", contactInformation = "c@example.com" });
        await org.UploadAsync<OrganizationResponse>("/api/v1/me/organization/logo", "logo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png");
        var pdf = "%PDF-1.4\n%Test document\n%%EOF"u8.ToArray();
        foreach (var t in new[] { "commercial_register", "tax_card", "address_proof" })
            await org.UploadAsync<OrganizationResponse>($"/api/v1/me/organization/documents/{t}", $"{t}.pdf", pdf, "application/pdf");
        var (_, sub, _) = await org.PostAsync<SubmitOrganizationResponse>("/api/v1/me/organization/submit");
        await admin.PostAsync<object?>($"/api/v1/admin/organization-reviews/{sub!.ReviewId}/decision", new { approve = true, notes = (string?)null });

        var (sBad, _, _) = await org.PostAsync<JobDto>("/api/v1/jobs", new { title = "", description = "d", employmentType = "full_time", workArrangement = "remote", location = "x", accessibilityInformation = "x" });
        Assert.Equal(400, sBad);
        var (_, created, e1) = await org.PostAsync<JobDto>("/api/v1/jobs", job);
        Assert.Null(e1);

        var (sAnon, list, _) = await Client.GetAsync<PagedResult<JobDto>>("/api/v1/jobs");
        Assert.Equal(200, sAnon);
        Assert.Contains(list!.Items, j => j.JobId == created!.JobId);

        var (_, _, e2) = await du.PostAsync<object?>($"/api/v1/jobs/{created!.JobId}/apply", new { applicationText = "عايز الوظيفة", voluntarilySharedNeeds = "مدخل صوتي" });
        Assert.Null(e2);
        var (sDup, _, _) = await du.PostAsync<object?>($"/api/v1/jobs/{created.JobId}/apply", new { applicationText = "تاني" });
        Assert.Equal(409, sDup);
        var (sOrgApply, _, _) = await org.PostAsync<object?>($"/api/v1/jobs/{created.JobId}/apply", new { applicationText = "x" });
        Assert.Equal(403, sOrgApply);

        var (_, apps, _) = await org.GetAsync<List<ApplicationDto>>($"/api/v1/jobs/{created.JobId}/applications");
        Assert.Single(apps!);
        var (_, _, e3) = await org.PutAsync<object?>($"/api/v1/jobs/applications/{apps![0].ApplicationId}/status", new { status = "interview" });
        Assert.Null(e3);
        var (_, mine, _) = await du.GetAsync<List<ApplicationDto>>("/api/v1/jobs/applications/mine");
        Assert.Equal("interview", mine![0].Status);
        var (sForeign, _, _) = await du.GetAsync<List<ApplicationDto>>($"/api/v1/jobs/{created.JobId}/applications");
        Assert.Equal(403, sForeign);

        var (_, _, e4) = await admin.PostAsync<object?>($"/api/v1/jobs/{created.JobId}/close");
        Assert.Null(e4);
        var (sClosed, _, _) = await Client.GetAsync<JobDto>($"/api/v1/jobs/{created.JobId}");
        Assert.Equal(404, sClosed);
    }
}
