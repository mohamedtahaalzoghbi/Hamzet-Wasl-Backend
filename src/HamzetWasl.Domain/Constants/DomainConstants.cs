namespace HamzetWasl.Domain.Constants;

public static class AccountStatuses
{
    public const string Active = "active";
    public const string PendingApproval = "pending_approval";
    public const string Suspended = "suspended";
}

public static class OrganizationTypes
{
    public const string Association = "association";
    public const string Company = "company";
}

public static class OrganizationApprovalStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class OrganizationReviewStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public static class DocumentTypes
{
    public const string CommercialRegister = "commercial_register";
    public const string TaxCard = "tax_card";
    public const string AddressProof = "address_proof";
    public const string License = "license";

    public static readonly string[] Required = [CommercialRegister, TaxCard, AddressProof];
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        CommercialRegister, TaxCard, AddressProof, License
    };
}

public static class VerificationStatuses
{
    public const string Unverified = "unverified";
    public const string Pending = "pending";
    public const string InterviewScheduled = "interview_scheduled";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Suspended = "suspended";
}

public static class VerificationReviewStatuses
{
    public const string Draft = "draft";
    public const string Pending = "pending";
    public const string InterviewScheduled = "interview_scheduled";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}

public static class VerificationLevels
{
    public const string Basic = "basic";
    public const string Field = "field";
}

public static class EvidenceTypes
{
    public const string NationalIdFront = "national_id_front";
    public const string NationalIdBack = "national_id_back";
    public const string FacePhoto = "face_photo";

    public static readonly string[] Required = [NationalIdFront, NationalIdBack, FacePhoto];
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        NationalIdFront, NationalIdBack, FacePhoto
    };
}

public static class SocialPlatforms
{
    public const string Facebook = "facebook";
    public const string LinkedIn = "linkedin";
    public const string Instagram = "instagram";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Facebook, LinkedIn, Instagram
    };
}

public static class InterviewSlotStatuses
{
    public const string Available = "available";
    public const string Booked = "booked";
    public const string Unavailable = "unavailable";
}

public static class HelpModes
{
    public const string OnlineOnly = "online_only";
    public const string OnlineAndField = "online_and_field";
}

public static class SkillLevels
{
    public const string Beginner = "beginner";
    public const string Intermediate = "intermediate";
    public const string Advanced = "advanced";
}

public static class NeedTypes
{
    public const string Visual = "visual";
    public const string Motor = "motor";
    public const string Hearing = "hearing";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Visual, Motor, Hearing
    };
}

public static class ProfileVisibilities
{
    public const string Public = "public";
    public const string Followers = "followers";
    public const string Private = "private";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Public, Followers, Private
    };
}

public static class AccessibilityThemes
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";
    public const string HighContrast = "high_contrast";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        System, Light, Dark, HighContrast
    };
}

public static class InteractionModes
{
    public const string Touch = "touch";
    public const string Keyboard = "keyboard";
    public const string ScreenReader = "screen_reader";
    public const string Voice = "voice";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Touch, Keyboard, ScreenReader, Voice
    };
}

public static class NotificationModes
{
    public const string InApp = "in_app";
    public const string Push = "push";
    public const string VisualAlert = "visual_alert";
    public const string Vibration = "vibration";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        InApp, Push, VisualAlert, Vibration
    };
}

public static class NotificationChannels
{
    public const string InApp = "in_app";
    public const string Push = "push";
    public const string VisualAlert = "visual_alert";
    public const string Vibration = "vibration";
}

public static class NotificationTypes
{
    public const string OrganizationApproved = "organization_approved";
    public const string OrganizationRejected = "organization_rejected";
    public const string VerificationApproved = "verification_approved";
    public const string VerificationRejected = "verification_rejected";
    public const string InterviewBooked = "interview_booked";
    public const string InterviewChanged = "interview_changed";
    public const string InterviewCancelled = "interview_cancelled";
}

public static class DeletionRequestStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Completed = "completed";
}

public static class AdminActions
{
    public const string ApproveVolunteer = "approve_volunteer";
    public const string RejectVolunteer = "reject_volunteer";
    public const string ApproveOrganization = "approve_organization";
    public const string RejectOrganization = "reject_organization";
    public const string CreateInterviewSlot = "create_interview_slot";
    public const string DeleteInterviewSlot = "delete_interview_slot";
    public const string SuspendUser = "suspend_user";
    public const string ReactivateUser = "reactivate_user";
    public const string ResolveDeletionRequest = "resolve_deletion_request";
}

public static class AuditOutcomes
{
    public const string Success = "success";
    public const string Failure = "failure";
}

public static class TrainingResults
{
    public const string Passed = "passed";
    public const string Failed = "failed";
}

public static class ConsentPurposes
{
    public const string Terms = "terms";
    public const string Accessibility = "accessibility";
}
