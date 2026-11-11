using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using HamzetWasl.Application.Contracts.Admin;
using HamzetWasl.Application.Contracts.Auth;
using HamzetWasl.Application.Contracts.Organization;
using HamzetWasl.Application.Contracts.Profile;
using HamzetWasl.Application.Contracts.Volunteer;
using HamzetWasl.Application.Validation;
using HamzetWasl.Domain.Rules;
using Xunit;

namespace HamzetWasl.UnitTests;

public class ValidatorTests
{
    private static T Mut<T>(T source, Action<T> mutate) where T : class
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        var clone = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source, options), options)!;
        mutate(clone);
        return clone;
    }

    [Fact]
    public void Register_requires_public_role_email_phone_password_and_terms()
    {
        var validator = new RegisterRequestValidator();

        var valid = new RegisterRequest
        {
            Role = "volunteer",
            Name = "Ahmed",
            Email = "a@example.com",
            Phone = "01012345678",
            Password = "Passw0rd!",
            ConfirmPassword = "Passw0rd!",
            TermsAccepted = true
        };
        Assert.True(validator.Validate(valid).IsValid);

        Assert.False(validator.Validate(Mut(valid, x => x.Role = "admin")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Role = "admin")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Email = "not-an-email")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Phone = "01999999999")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.ConfirmPassword = "different")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.TermsAccepted = false)).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Password = "1234567")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Name = "")).IsValid);
    }

    [Fact]
    public void Register_for_organizations_requires_organization_name()
    {
        var validator = new RegisterRequestValidator();
        var company = new RegisterRequest
        {
            Role = "company",
            OrganizationName = "Acme",
            Email = "a@example.com",
            Phone = "01012345678",
            Password = "Passw0rd!",
            ConfirmPassword = "Passw0rd!",
            TermsAccepted = true
        };
        Assert.True(validator.Validate(company).IsValid);

        Assert.False(validator.Validate(Mut(company, x => x.OrganizationName = "")).IsValid);
        Assert.True(validator.Validate(Mut(company, x => x.Name = "ignored for organisations")).IsValid);
    }

    [Fact]
    public void Login_requires_identifier_and_password()
    {
        var validator = new LoginRequestValidator();
        Assert.True(validator.Validate(new LoginRequest { EmailOrPhone = "a@example.com", Password = "x" }).IsValid);
        Assert.False(validator.Validate(new LoginRequest { EmailOrPhone = "", Password = "x" }).IsValid);
        Assert.False(validator.Validate(new LoginRequest { EmailOrPhone = "a@example.com", Password = "" }).IsValid);
    }

    [Fact]
    public void Accessibility_profile_enforces_need_type_theme_font_and_modes()
    {
        var validator = new UpsertAccessibilityRequestValidator();
        var valid = new UpsertAccessibilityRequest
        {
            NeedType = "visual",
            Theme = "system",
            FontScale = 1.0m,
            ReducedMotion = false,
            InteractionMode = "touch",
            NotificationMode = "in_app",
            ManualOverrides = null
        };
        Assert.True(validator.Validate(valid).IsValid);

        Assert.False(validator.Validate(Mut(valid, x => x.NeedType = "other")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Theme = "blue")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.FontScale = 0.5m)).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.FontScale = 3m)).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.InteractionMode = "mouse")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.NotificationMode = "email")).IsValid);
    }

    [Fact]
    public void Accessibility_manual_overrides_must_be_valid_json_or_null()
    {
        var validator = new UpsertAccessibilityRequestValidator();
        var valid = new UpsertAccessibilityRequest
        {
            NeedType = "hearing",
            Theme = "dark",
            FontScale = 1.25m,
            ReducedMotion = false,
            InteractionMode = "screen_reader",
            NotificationMode = "visual_alert",
            ManualOverrides = null
        };
        Assert.True(validator.Validate(valid).IsValid);

        Assert.True(validator.Validate(Mut(valid, x =>
            x.ManualOverrides = JsonDocument.Parse("""{"highContrast":true,"captions":"on"}""").RootElement)).IsValid);

        // Deep but legal JSON is accepted; the validator only requires valid JSON.
        Assert.True(validator.Validate(Mut(valid, x =>
            x.ManualOverrides = JsonDocument.Parse("""{"nested":{"deep":{"deeper":"text"}}}""").RootElement)).IsValid);
    }

    [Fact]
    public void Accessibility_need_types_are_one_to_three_distinct_and_primary_first()
    {
        var validator = new UpsertAccessibilityRequestValidator();
        var valid = new UpsertAccessibilityRequest
        {
            NeedType = "visual",
            NeedTypes = new[] { "visual", "hearing" },
            Theme = "system",
            FontScale = 1.0m,
            ReducedMotion = false,
            InteractionMode = "touch",
            NotificationMode = "in_app",
            ManualOverrides = null
        };
        Assert.True(validator.Validate(valid).IsValid);

        // فئة واحدة مقبولة، وغياب الحقل يعني فئة واحدة (توافق مع العملاء القدامى).
        Assert.True(validator.Validate(Mut(valid, x => x.NeedTypes = new[] { "visual" })).IsValid);
        Assert.True(validator.Validate(Mut(valid, x => x.NeedTypes = null)).IsValid);

        // صفر فئات، أربع فئات، تكرار، عدم تطابق الفئة الأولى، وفئة غير معروفة: كلها مرفوضة.
        Assert.False(validator.Validate(Mut(valid, x => x.NeedTypes = Array.Empty<string>())).IsValid);
        Assert.False(validator.Validate(Mut(valid, x =>
            x.NeedTypes = new[] { "visual", "hearing", "motor", "visual" })).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.NeedTypes = new[] { "visual", "visual" })).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.NeedTypes = new[] { "hearing", "visual" })).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.NeedTypes = new[] { "visual", "other" })).IsValid);
    }

    [Fact]
    public void Accessibility_manual_overrides_are_limited_to_four_kilobytes()
    {
        var validator = new UpsertAccessibilityRequestValidator();
        var valid = new UpsertAccessibilityRequest
        {
            NeedType = "visual",
            Theme = "system",
            FontScale = 1.0m,
            ReducedMotion = false,
            InteractionMode = "touch",
            NotificationMode = "in_app",
            ManualOverrides = null
        };

        // ٤ كيلوبايت بالضبط (٨ أحرف للغلاف {"x":"..."}) مقبولة.
        var atLimit = "{\"x\":\"" + new string('a', AccessibilityOverrides.MaxJsonBytes - 8) + "\"}";
        Assert.Equal(AccessibilityOverrides.MaxJsonBytes, System.Text.Encoding.UTF8.GetByteCount(atLimit));
        Assert.True(validator.Validate(Mut(valid, x =>
            x.ManualOverrides = JsonDocument.Parse(atLimit).RootElement)).IsValid);

        // بايت واحد زيادة مرفوض.
        var tooLarge = "{\"x\":\"" + new string('a', AccessibilityOverrides.MaxJsonBytes - 7) + "\"}";
        Assert.False(validator.Validate(Mut(valid, x =>
            x.ManualOverrides = JsonDocument.Parse(tooLarge).RootElement)).IsValid);
    }

    [Fact]
    public void Volunteer_profile_accepts_only_known_help_mode_and_skill_level()
    {
        var validator = new UpdateVolunteerProfileRequestValidator();
        var valid = new UpdateVolunteerProfileRequest
        {
            HelpMode = "online_only",
            SkillLevel = "beginner",
            WeeklyHours = 5,
            SkillIds = Array.Empty<int>()
        };
        Assert.True(validator.Validate(valid).IsValid);

        Assert.False(validator.Validate(Mut(valid, x => x.HelpMode = "sometimes")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.SkillLevel = "expert")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.WeeklyHours = 0)).IsValid);
        Assert.True(validator.Validate(Mut(valid, x => x.WeeklyHours = 100)).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.WeeklyHours = 200)).IsValid);
    }

    [Fact]
    public void Volunteer_location_validator_checks_coordinates_and_radius_only()
    {
        var validator = new VolunteerLocationRequestValidator();
        var valid = new VolunteerLocationRequest
        {
            Governorate = "cairo",
            ServiceArea = "nasr_city",
            ServiceRadiusKm = 10
        };
        Assert.True(validator.Validate(valid).IsValid);

        // The governorate/area rules moved to VolunteerService because the catalogue now lives
        // in GOVERNORATE/AREA (IGeoCatalog). They are covered by GeoCatalogTests.
        Assert.True(validator.Validate(Mut(valid, x => x.Governorate = "paris")).IsValid);
        Assert.True(validator.Validate(Mut(valid, x => x.ServiceArea = "unknown_area")).IsValid);

        Assert.False(validator.Validate(Mut(valid, x => x.ServiceRadiusKm = 0)).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.ServiceRadiusKm = 201)).IsValid);
        Assert.True(validator.Validate(Mut(valid, x => x.ServiceRadiusKm = null)).IsValid);

        var lat = new VolunteerLocationRequest
        {
            Governorate = "cairo",
            ServiceArea = "nasr_city",
            Latitude = 91
        };
        Assert.False(validator.Validate(lat).IsValid);

        var lon = new VolunteerLocationRequest
        {
            Governorate = "cairo",
            ServiceArea = "nasr_city",
            Longitude = 181
        };
        Assert.False(validator.Validate(lon).IsValid);
    }

    [Fact]
    public void Organization_update_validator_checks_metadata_and_website()
    {
        var validator = new UpdateOrganizationRequestValidator();
        var valid = new UpdateOrganizationRequest
        {
            Name = "Acme",
            Field = "technology",
            Description = "Software",
            Governorate = "cairo",
            City = "nasr_city",
            Website = "https://acme.example.com",
            EmployeeCountRange = "11-50",
            ContactInformation = "info@acme.example.com"
        };
        Assert.True(validator.Validate(valid).IsValid);

        Assert.False(validator.Validate(Mut(valid, x => x.Name = "")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Field = "astronomy")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.EmployeeCountRange = "2050")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Website = "ftp://acme.com")).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.Website = "not a url")).IsValid);

        // The governorate/city rules moved to OrganizationService (IGeoCatalog) and are covered
        // by GeoCatalogTests; the validator must no longer reject them.
        Assert.True(validator.Validate(Mut(valid, x => x.Governorate = "paris")).IsValid);
        Assert.True(validator.Validate(Mut(valid, x => x.City = "unknown")).IsValid);
    }

    [Fact]
    public void Admin_decisions_require_notes_on_rejection()
    {
        var validator = new DecisionRequestValidator();
        Assert.True(validator.Validate(new DecisionRequest { Approve = true }).IsValid);
        Assert.True(validator.Validate(new DecisionRequest { Approve = false, Notes = "ID unclear" }).IsValid);
        Assert.False(validator.Validate(new DecisionRequest { Approve = false, Notes = "" }).IsValid);
        Assert.False(validator.Validate(new DecisionRequest { Approve = false, Notes = "   " }).IsValid);
    }

    [Fact]
    public void Interview_slots_validate_future_time_and_duration()
    {
        var validator = new CreateInterviewSlotRequestValidator();
        var valid = new CreateInterviewSlotRequest { StartsAt = DateTime.UtcNow.AddDays(1), DurationMinutes = 30 };
        Assert.True(validator.Validate(valid).IsValid);

        Assert.False(validator.Validate(Mut(valid, x => x.StartsAt = DateTime.UtcNow.AddDays(-1))).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.DurationMinutes = 0)).IsValid);
        Assert.False(validator.Validate(Mut(valid, x => x.DurationMinutes = 500)).IsValid);
    }

    [Fact]
    public void Training_attempts_need_score_between_0_and_100()
    {
        var validator = new SubmitTrainingAttemptRequestValidator();
        Assert.True(validator.Validate(new SubmitTrainingAttemptRequest { Score = 70 }).IsValid);
        Assert.False(validator.Validate(new SubmitTrainingAttemptRequest { Score = -1 }).IsValid);
        Assert.False(validator.Validate(new SubmitTrainingAttemptRequest { Score = 101 }).IsValid);
    }

    [Fact]
    public void Availability_slots_must_be_ordered_and_in_the_future()
    {
        var validator = new SetAvailabilityRequestValidator();
        var now = DateTime.UtcNow;
        var future = now.AddDays(1);

        var valid = new SetAvailabilityRequest
        {
            Slots =
            [
                new AvailabilityInput { StartsAt = future, EndsAt = future.AddHours(2), Timezone = "Africa/Cairo" },
                new AvailabilityInput { StartsAt = future.AddHours(2), EndsAt = future.AddHours(4), Timezone = "Africa/Cairo" }
            ]
        };
        Assert.True(validator.Validate(valid).IsValid);

        var overlapping = new SetAvailabilityRequest
        {
            Slots =
            [
                new AvailabilityInput { StartsAt = future, EndsAt = future.AddHours(2), Timezone = "Africa/Cairo" },
                new AvailabilityInput { StartsAt = future.AddHours(1), EndsAt = future.AddHours(3), Timezone = "Africa/Cairo" }
            ]
        };
        Assert.False(validator.Validate(overlapping).IsValid);

        var reversed = new SetAvailabilityRequest
        {
            Slots = [new AvailabilityInput { StartsAt = future.AddHours(2), EndsAt = future.AddHours(1), Timezone = "Africa/Cairo" }]
        };
        Assert.False(validator.Validate(reversed).IsValid);

        var past = new SetAvailabilityRequest
        {
            Slots = [new AvailabilityInput { StartsAt = now.AddDays(-1), EndsAt = now, Timezone = "Africa/Cairo" }]
        };
        Assert.False(validator.Validate(past).IsValid);
    }
}

