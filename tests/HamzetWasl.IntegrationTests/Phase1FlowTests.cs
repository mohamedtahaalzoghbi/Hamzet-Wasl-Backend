using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Meta;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HamzetWasl.IntegrationTests;

/// <summary>
/// End-to-end Phase-1 flows against a real SQL Server (LocalDB) created from
/// the authoritative schema: onboarding, disabled-user settings, volunteer
/// verification, organization onboarding, admin queues and audit.
/// </summary>
[Collection("Api")]
public class Phase1FlowTests(ApiFixture fixture)
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

    /// <summary>Registers a user and asserts the call succeeded.</summary>
    private async Task<AuthResponse> Register(string role, string name, string email, string phone)
    {
        var (status, data, error) =
            await Client.PostAsync<AuthResponse>("/api/v1/auth/register", RegisterBody(role, name, email, phone));
        Assert.Equal(201, status);
        Assert.Null(error);
        Assert.NotNull(data);
        return data!;
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
    public async Task Health_and_meta_endpoints_are_public()
    {
        var healthResponse = await Client.GetAsync("/health");
        Assert.True(healthResponse.IsSuccessStatusCode);
        var healthBody = await healthResponse.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", healthBody);

        var (_, accountTypes, _) = await Client.GetAsync<IReadOnlyList<AccountTypeDto>>("/api/v1/meta/account-types");
        Assert.Contains(accountTypes, t => t.Key == "company");
        Assert.Contains(accountTypes, t => t.Key == "association");
        Assert.Contains(accountTypes, t => t.Key == "disabled_user");
        Assert.Contains(accountTypes, t => t.Key == "volunteer");

        var (_, governorates, _) = await Client.GetAsync<IReadOnlyList<GovernorateDto>>("/api/v1/meta/governorates");
        Assert.Contains(governorates, g => g.Key == "cairo");

        var (_, areas, _) = await Client.GetAsync<IReadOnlyList<AreaDto>>("/api/v1/meta/governorates/cairo/areas");
        Assert.Contains(areas, a => a.Key == "nasr_city");

        var (_, businessFields, _) = await Client.GetAsync<IReadOnlyList<BusinessFieldDto>>("/api/v1/meta/business-fields");
        Assert.Contains(businessFields, f => f.Key == "technology");

        var (_, ranges, _) = await Client.GetAsync<IReadOnlyList<EmployeeRangeDto>>("/api/v1/meta/employee-ranges");
        Assert.Contains(ranges, r => r.Key == "11-50");

        var (_, needTypes, _) = await Client.GetAsync<IReadOnlyList<NeedTypeDto>>("/api/v1/meta/need-types");
        Assert.Equal(3, needTypes.Count);
    }

    [Fact]
    public async Task Disabled_user_profile_and_accessibility_flow()
    {
        var email = Email("dis");
        var user = await Register("disabled_user", "Disabled Tester", email, Phone());
        var client = Authed(user.AccessToken);

        // Interests lookup.
        var (_, interests, interestsLookupError) = await Client.GetAsync<IReadOnlyList<LookupDto>>("/api/v1/meta/interests");
        Assert.Null(interestsLookupError);
        Assert.Contains(interests, i => i.NameAr.Length > 0);
        var (_, skills, skillsLookupError) = await Client.GetAsync<IReadOnlyList<LookupDto>>("/api/v1/meta/skills");
        Assert.Null(skillsLookupError);
        Assert.Contains(skills, s => s.NameAr.Length > 0);

        var (_, interestError, interestPutError) = await client.PutAsync<object?>("/api/v1/me/interests", new
        {
            interestIds = interests.Select(i => i.Id).ToList()
        });
        Assert.Null(interestPutError);

        var (_, _, skillPutError) = await client.PutAsync<object?>("/api/v1/me/skills", new
        {
            skillIds = skills.Select(s => s.Id).ToList()
        });
        Assert.Null(skillPutError);

        // Profile read and update.
        var (profileStatus, profile, profileError) = await client.GetAsync<ProfileResponse>("/api/v1/me/profile");
        Assert.Equal(200, profileStatus);
        Assert.Null(profileError);
        Assert.Equal("Disabled Tester", profile!.DisplayName);

        var (_, updated, updateError) = await client.PutAsync<ProfileResponse>("/api/v1/me/profile", new
        {
            displayName = "Disabled Tester",
            bio = "Disabled user bio",
            city = "Nasr City",
            visibility = "public"
        });

        Assert.Null(updateError);
        Assert.Equal("Disabled user bio", updated!.Bio);
        Assert.Equal("Nasr City", updated.City);

        // Accessibility settings.
        var (_, accessibility, accessibilityError) = await client.PutAsync<AccessibilityResponse>("/api/v1/me/accessibility", new
        {
            needType = "visual",
            theme = "dark",
            fontScale = 1.25m,
            reducedMotion = true,
            interactionMode = "screen_reader",
            notificationMode = "visual_alert",
            manualOverrides = (object?)null
        });
        Assert.Null(accessibilityError);
        Assert.Equal("visual", accessibility!.NeedType);
        Assert.False(accessibility.EffectivePreset.HighContrast);

        var (_, readBack, readError) = await client.GetAsync<AccessibilityResponse>("/api/v1/me/accessibility");
        Assert.Null(readError);
        Assert.Equal("visual", readBack!.NeedType);

        var (_, _, deleteError) = await client.DeleteAsync<object?>("/api/v1/me/accessibility");
        Assert.Null(deleteError);

        var (_, _, readAfterDeleteError) = await client.GetAsync<AccessibilityResponse>("/api/v1/me/accessibility");
        Assert.Equal("NOT_FOUND", readAfterDeleteError!.Code);
    }

    [Fact]
    public async Task Volunteer_verification_flow()
    {
        var email = Email("vol");
        var phone = Phone();
        var user = await Register("volunteer", "Volunteer Tester", email, phone);
        var client = Authed(user.AccessToken);

        var admin = await AdminLogin();
        var adminClient = Authed(admin.AccessToken);

        // The volunteer must verify the email before uploading verification evidence.
        var (_, _, sendCodeError) = await client.PostAsync<object?>("/api/v1/auth/email/send-code");
        Assert.Null(sendCodeError);

        var code = fixture.Factory.Messages.LastCode;
        Assert.NotNull(code);

        var (_, _, verifyCodeError) = await client.PostAsync<object?>("/api/v1/auth/email/verify", new { code });
        Assert.Null(verifyCodeError);

        // The phone must be verified as well before the review can start.
        var (_, _, sendPhoneCodeError) = await client.PostAsync<object?>("/api/v1/auth/phone/send-code", new { phone });
        Assert.Null(sendPhoneCodeError);

        var phoneCode = fixture.Factory.Messages.LastCode;
        Assert.NotNull(phoneCode);

        var (_, _, verifyPhoneError) = await client.PostAsync<object?>("/api/v1/auth/phone/verify", new { code = phoneCode });
        Assert.Null(verifyPhoneError);

        // The volunteer must upload the three verification evidence files before booking.
        var jpg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        foreach (var evidenceType in new[] { "national_id_front", "national_id_back", "face_photo" })
        {
            var (_, _, evidenceError) = await client.UploadFormAsync<object?>(
                "/api/v1/me/verification/evidence", $"{evidenceType}.jpg", jpg, "image/jpeg",
                new Dictionary<string, string> { ["evidenceType"] = evidenceType });
            Assert.Null(evidenceError);
        }

        // A social link is part of the pre-interview checklist.
        var (_, _, socialLinksError) = await client.PutAsync<object?>("/api/v1/me/verification/social-links", new
        {
            links = new[]
            {
                new { platform = "facebook", url = "https://facebook.com/example" }
            }
        });
        Assert.Null(socialLinksError);

        // Admin creates an interview slot.
        var (_, slot, slotError) = await adminClient.PostAsync<InterviewSlotAdminDto>("/api/v1/admin/interview-slots", new
        {
            startsAt = DateTime.UtcNow.AddDays(3),
            durationMinutes = 30
        });
        Assert.Null(slotError);
        Assert.Equal("available", slot!.Status);

        // Volunteer books the slot.
        var (_, booked, bookError) = await client.PostAsync<VerificationResponse>("/api/v1/me/verification/interview", new
        {
            interviewSlotId = slot.InterviewSlotId
        });
        Assert.Null(bookError);
        Assert.Equal("interview_scheduled", booked!.Status);
        Assert.NotNull(booked.ReviewId);

        // Admin approves.
        var (_, _, approveError) = await adminClient.PostAsync<object?>(
            $"/api/v1/admin/volunteer-reviews/{booked.ReviewId}/decision", new { approve = true, notes = (string?)null });
        Assert.Null(approveError);

        // Audit records the action.
        var (_, audit, auditError) = await adminClient.GetAsync<PagedResult<AdminAuditDto>>("/api/v1/admin/audit");
        Assert.Null(auditError);
        Assert.Contains(audit!.Items, item => item.Action == "approve_volunteer");
    }

    [Fact]
    public async Task Organization_onboarding_flow()
    {
        var email = Email("org");
        var user = await Register("company", "Test Company", email, Phone());
        var client = Authed(user.AccessToken);

        var (_, org, orgError) = await client.PutAsync<OrganizationResponse>("/api/v1/me/organization", new
        {
            name = "Test Company",
            field = "technology",
            description = "Software for good",
            governorate = "cairo",
            city = "nasr_city",
            website = "https://example.com",
            employeeCountRange = "11-50",
            contactInformation = "contact@example.com"
        });
        Assert.Null(orgError);
        Assert.Equal("technology", org!.Field);
        Assert.False(org.CanSubmit);

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var (_, logo, logoError) = await client.UploadAsync<OrganizationResponse>(
            "/api/v1/me/organization/logo", "logo.png", png, "image/png");
        Assert.Null(logoError);
        Assert.NotNull(logo!.LogoUrl);

        var pdf = "%PDF-1.4\n%Test document\n%%EOF"u8.ToArray();
        foreach (var documentType in new[] { "commercial_register", "tax_card", "address_proof" })
        {
            var (_, uploaded, documentError) = await client.UploadAsync<OrganizationResponse>(
                $"/api/v1/me/organization/documents/{documentType}", $"{documentType}.pdf", pdf, "application/pdf");
            Assert.Null(documentError);
        }

        var (_, submit, submitError) = await client.PostAsync<SubmitOrganizationResponse>("/api/v1/me/organization/submit");
        Assert.Null(submitError);
        Assert.Equal("pending", submit!.ReviewStatus);

        var admin = await AdminLogin();
        var adminClient = Authed(admin.AccessToken);

        var (_, _, rejectError) = await adminClient.PostAsync<object?>(
            $"/api/v1/admin/organization-reviews/{submit.ReviewId}/decision", new
            {
                approve = false,
                notes = "Documents incomplete"
            });
        Assert.Null(rejectError);

        var (_, audit, _) = await adminClient.GetAsync<PagedResult<AdminAuditDto>>("/api/v1/admin/audit");
        Assert.Contains(audit!.Items, item => item.Action == "reject_organization");

        var (_, resubmit, resubmitError) = await client.PostAsync<SubmitOrganizationResponse>("/api/v1/me/organization/submit");
        Assert.Null(resubmitError);
        Assert.Equal("pending", resubmit!.ReviewStatus);

        var (_, _, approveError) = await adminClient.PostAsync<object?>(
            $"/api/v1/admin/organization-reviews/{resubmit.ReviewId}/decision", new
            {
                approve = true,
                notes = (string?)null
            });
        Assert.Null(approveError);

        var (_, status, statusError) = await client.GetAsync<OrganizationReviewStatusResponse>("/api/v1/me/organization/review-status");
        Assert.Null(statusError);
        Assert.Equal("approved", status!.ApprovalStatus);
    }

    [Fact]
    public async Task Auth_validation_and_security_requirements()
    {
        var email = Email("sec");
        await Register("disabled_user", "Sec User", email, Phone());

        var duplicateError = await RegisterFailing("disabled_user", "Sec User", email, Phone(), 409);
        Assert.Equal("EMAIL_ALREADY_EXISTS", duplicateError.Code);

        var adminError = await RegisterFailing("admin", "Admin", Email("adm"), Phone(), 400);
        Assert.Equal("VALIDATION_ERROR", adminError.Code);

        var (_, _, invalidLoginError) = await Client.PostAsync<AuthResponse>("/api/v1/auth/login", new
        {
            emailOrPhone = Email("nobody"),
            password = "WrongPass"
        });
        Assert.Equal("INVALID_CREDENTIALS", invalidLoginError!.Code);

        var (_, _, anonymousProfileError) = await Client.GetAsync<ProfileResponse>("/api/v1/me/profile");
        Assert.Equal("UNAUTHORIZED", anonymousProfileError!.Code);
    }

    [Fact]
    public async Task Role_separation_is_enforced()
    {
        var email = Email("sep");
        var volunteer = await Register("volunteer", "Vol Separation", email, Phone());
        var client = Authed(volunteer.AccessToken);

        var (_, _, adminForbidden) = await client.GetAsync<PagedResult<AdminAuditDto>>("/api/v1/admin/audit");
        Assert.Equal("FORBIDDEN", adminForbidden!.Code);

        var (_, _, organizationNotAllowed) = await client.GetAsync<OrganizationResponse>("/api/v1/me/organization");
        Assert.Equal("FORBIDDEN", organizationNotAllowed!.Code);
    }
}
