using System.Text.Json;
using HamzetWasl.Domain.Constants;

namespace HamzetWasl.Domain.Rules;

/// <summary>
/// Business rules that the SQL schema cannot express. Kept in the domain so
/// every application service and test uses the same implementation.
/// </summary>
public static class BusinessRules
{
    public static bool OrganizationTypeMatchesRole(string roleName, string organizationType) =>
        (roleName == RoleNames.Association && organizationType == OrganizationTypes.Association) ||
        (roleName == RoleNames.Company && organizationType == OrganizationTypes.Company);

    public static bool CanOwnDirectoryItem(string roleName) => roleName == RoleNames.Association;

    public static bool CanOwnJob(string roleName) => roleName == RoleNames.Company;

    public static bool CanPublish(string approvalStatus) =>
        approvalStatus == OrganizationApprovalStatuses.Approved;

    public static bool IsVolunteerVerificationApproved(string verificationStatus) =>
        verificationStatus == VerificationStatuses.Approved;

    public static bool CanSubmitHelpOffer(string roleName, string verificationStatus) =>
        roleName == RoleNames.Volunteer && IsVolunteerVerificationApproved(verificationStatus);

    public static bool HelpModeCompatible(string volunteerHelpMode, string requestMode) =>
        requestMode switch
        {
            "online" => volunteerHelpMode is HelpModes.OnlineOnly or HelpModes.OnlineAndField,
            "field" => volunteerHelpMode == HelpModes.OnlineAndField,
            "online_or_field" => true,
            _ => false
        };

    public static bool CanHaveAccessibilityProfile(string roleName) =>
        roleName == RoleNames.DisabledUser;

    public static bool IsSelf(Guid userId, Guid otherUserId) => userId == otherUserId;

    public static bool IsValidRating(int rating) => rating is >= 1 and <= 5;

    public static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool IsValidRadius(int? radiusKm) => radiusKm is null or >= 1 and <= 200;

    public static bool IsValidFontScale(decimal fontScale) => fontScale is >= 1.0m and <= 2.0m;
}
