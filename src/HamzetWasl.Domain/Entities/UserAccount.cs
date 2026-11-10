using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class UserAccount
{
    public Guid UserId { get; set; }

    public int RoleId { get; set; }

    public string Email { get; set; } = null!;

    public bool EmailVerified { get; set; }

    public string? Phone { get; set; }

    public bool PhoneVerified { get; set; }

    public string AccountStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual AccessibilityProfile? AccessibilityProfile { get; set; }

    public virtual ICollection<AdminAudit> AdminAudits { get; set; } = new List<AdminAudit>();

    public virtual ICollection<ApplicationStatusChange> ApplicationStatusChanges { get; set; } = new List<ApplicationStatusChange>();

    public virtual ICollection<AssistantLog> AssistantLogs { get; set; } = new List<AssistantLog>();

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<CommunityMembership> CommunityMemberships { get; set; } = new List<CommunityMembership>();

    public virtual ICollection<ConsentRecord> ConsentRecords { get; set; } = new List<ConsentRecord>();

    public virtual ICollection<ContentReport> ContentReportReporters { get; set; } = new List<ContentReport>();

    public virtual ICollection<ContentReport> ContentReportReviewers { get; set; } = new List<ContentReport>();

    public virtual ICollection<DeletionRequest> DeletionRequests { get; set; } = new List<DeletionRequest>();

    public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();

    public virtual ICollection<EmergencyContact> EmergencyContacts { get; set; } = new List<EmergencyContact>();

    public virtual ICollection<HelpFeedback> HelpFeedbackAuthors { get; set; } = new List<HelpFeedback>();

    public virtual ICollection<HelpFeedback> HelpFeedbackRatedUsers { get; set; } = new List<HelpFeedback>();

    public virtual ICollection<HelpMessage> HelpMessages { get; set; } = new List<HelpMessage>();

    public virtual ICollection<HelpRequest> HelpRequests { get; set; } = new List<HelpRequest>();

    public virtual ICollection<InquiryMessage> InquiryMessages { get; set; } = new List<InquiryMessage>();

    public virtual ICollection<InterviewSlot> InterviewSlots { get; set; } = new List<InterviewSlot>();

    public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<ObstacleReport> ObstacleReports { get; set; } = new List<ObstacleReport>();

    public virtual Organization? Organization { get; set; }

    public virtual ICollection<OrganizationReview> OrganizationReviews { get; set; } = new List<OrganizationReview>();

    public virtual ICollection<PhoneVerification> PhoneVerifications { get; set; } = new List<PhoneVerification>();

    public virtual ICollection<PlaceReview> PlaceReviews { get; set; } = new List<PlaceReview>();

    public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual AccountRole Role { get; set; } = null!;

    public virtual ICollection<SafetyReport> SafetyReportReportedUsers { get; set; } = new List<SafetyReport>();

    public virtual ICollection<SafetyReport> SafetyReportReporters { get; set; } = new List<SafetyReport>();

    public virtual ICollection<SafetyReport> SafetyReportReviewers { get; set; } = new List<SafetyReport>();

    public virtual ICollection<ServiceInquiry> ServiceInquiries { get; set; } = new List<ServiceInquiry>();

    public virtual ICollection<SosAlert> SosAlerts { get; set; } = new List<SosAlert>();

    public virtual ICollection<UserFollow> UserFollowFolloweds { get; set; } = new List<UserFollow>();

    public virtual ICollection<UserFollow> UserFollowFollowers { get; set; } = new List<UserFollow>();

    public virtual UserProfile? UserProfile { get; set; }

    public virtual ICollection<VerificationReview> VerificationReviews { get; set; } = new List<VerificationReview>();

    public virtual VolunteerProfile? VolunteerProfile { get; set; }

    public virtual ICollection<Interest> Interests { get; set; } = new List<Interest>();

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
