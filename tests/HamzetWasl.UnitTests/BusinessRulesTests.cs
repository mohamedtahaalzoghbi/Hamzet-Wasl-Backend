using HamzetWasl.Domain.Constants;
using HamzetWasl.Domain.Rules;
using Xunit;

namespace HamzetWasl.UnitTests;

/// <summary>Covers every business rule listed in MASTER_PROMPT §1.9.</summary>
public class BusinessRulesTests
{
    [Theory]
    [InlineData(RoleNames.Association, OrganizationTypes.Association, true)]
    [InlineData(RoleNames.Company, OrganizationTypes.Company, true)]
    [InlineData(RoleNames.Company, OrganizationTypes.Association, false)]
    [InlineData(RoleNames.Association, OrganizationTypes.Company, false)]
    [InlineData(RoleNames.Volunteer, OrganizationTypes.Association, false)]
    public void Organization_type_must_match_owner_role(string role, string type, bool expected) =>
        Assert.Equal(expected, BusinessRules.OrganizationTypeMatchesRole(role, type));

    [Theory]
    [InlineData(RoleNames.Association, true)]
    [InlineData(RoleNames.Company, false)]
    [InlineData(RoleNames.Volunteer, false)]
    [InlineData(RoleNames.Admin, false)]
    public void Only_assocations_own_directory_items(string role, bool expected) =>
        Assert.Equal(expected, BusinessRules.CanOwnDirectoryItem(role));

    [Theory]
    [InlineData(RoleNames.Company, true)]
    [InlineData(RoleNames.Association, false)]
    [InlineData(RoleNames.Volunteer, false)]
    public void Only_companies_own_jobs(string role, bool expected) =>
        Assert.Equal(expected, BusinessRules.CanOwnJob(role));

    [Theory]
    [InlineData(OrganizationApprovalStatuses.Approved, true)]
    [InlineData(OrganizationApprovalStatuses.Pending, false)]
    [InlineData(OrganizationApprovalStatuses.Rejected, false)]
    public void Only_approved_organizations_can_publish(string status, bool expected) =>
        Assert.Equal(expected, BusinessRules.CanPublish(status));

    [Theory]
    [InlineData(RoleNames.Volunteer, VerificationStatuses.Approved, true)]
    [InlineData(RoleNames.Volunteer, VerificationStatuses.Pending, false)]
    [InlineData(RoleNames.Volunteer, VerificationStatuses.Unverified, false)]
    [InlineData(RoleNames.DisabledUser, VerificationStatuses.Approved, false)]
    public void Only_verified_volunteers_can_submit_help_offers(string role, string status, bool expected) =>
        Assert.Equal(expected, BusinessRules.CanSubmitHelpOffer(role, status));

    [Theory]
    [InlineData(HelpModes.OnlineOnly, "online", true)]
    [InlineData(HelpModes.OnlineAndField, "online", true)]
    [InlineData(HelpModes.OnlineAndField, "field", true)]
    [InlineData(HelpModes.OnlineOnly, "field", false)]
    [InlineData(HelpModes.OnlineOnly, "online_or_field", true)]
    [InlineData(HelpModes.OnlineOnly, "unknown", false)]
    public void Help_mode_must_be_compatible_with_request_mode(string mode, string requestMode, bool expected) =>
        Assert.Equal(expected, BusinessRules.HelpModeCompatible(mode, requestMode));

    [Theory]
    [InlineData(RoleNames.DisabledUser, true)]
    [InlineData(RoleNames.Volunteer, false)]
    [InlineData(RoleNames.Association, false)]
    [InlineData(RoleNames.Company, false)]
    [InlineData(RoleNames.Admin, false)]
    public void Only_disabled_users_have_accessibility_profiles(string role, bool expected) =>
        Assert.Equal(expected, BusinessRules.CanHaveAccessibilityProfile(role));

    [Fact]
    public void Self_checks_and_ratings_follow_the_rules()
    {
        var id = Guid.NewGuid();
        Assert.True(BusinessRules.IsSelf(id, id));
        Assert.False(BusinessRules.IsSelf(id, Guid.NewGuid()));

        Assert.True(BusinessRules.IsValidRating(1));
        Assert.True(BusinessRules.IsValidRating(5));
        Assert.False(BusinessRules.IsValidRating(0));
        Assert.False(BusinessRules.IsValidRating(6));
    }

    [Theory]
    [InlineData(@"{""theme"":""dark""}", true)]
    [InlineData("null", true)]
    [InlineData("  ", true)]
    [InlineData(null, true)]
    [InlineData("{", false)]
    [InlineData("not json", false)]
    public void Manual_overrides_must_be_valid_json(string? json, bool expected) =>
        Assert.Equal(expected, BusinessRules.IsValidJson(json));

    [Theory]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(null, true)]
    [InlineData(0, false)]
    [InlineData(201, false)]
    [InlineData(-5, false)]
    public void Service_radius_is_between_1_and_200_km(int? radius, bool expected) =>
        Assert.Equal(expected, BusinessRules.IsValidRadius(radius));

    [Theory]
    [InlineData("1.0", true)]
    [InlineData("2.0", true)]
    [InlineData("1.5", true)]
    [InlineData("0.9", false)]
    [InlineData("2.1", false)]
    public void Font_scale_is_between_1_and_2(string value, bool expected) =>
        Assert.Equal(expected, BusinessRules.IsValidFontScale(decimal.Parse(value)));
}
