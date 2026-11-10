namespace HamzetWasl.Domain.Constants;

/// <summary>Role names exactly as constrained by ACCOUNT_ROLE.role_name.</summary>
public static class RoleNames
{
    public const string DisabledUser = "disabled_user";
    public const string Volunteer = "volunteer";
    public const string Association = "association";
    public const string Company = "company";
    public const string Admin = "admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        DisabledUser, Volunteer, Association, Company, Admin
    };

    /// <summary>Roles that may be chosen through public registration.</summary>
    public static readonly IReadOnlySet<string> PublicRegistration = new HashSet<string>(StringComparer.Ordinal)
    {
        DisabledUser, Volunteer, Association, Company
    };

    public static bool IsOrganization(string role) => role is Association or Company;
}
