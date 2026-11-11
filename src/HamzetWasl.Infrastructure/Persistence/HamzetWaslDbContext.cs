using System;
using System.Collections.Generic;
using HamzetWasl.Domain.Entities;
using HamzetWasl.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HamzetWasl.Infrastructure.Persistence;

public partial class HamzetWaslDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public HamzetWaslDbContext(DbContextOptions<HamzetWaslDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AccessibilityProfile> AccessibilityProfiles { get; set; }

    public virtual DbSet<Accommodation> Accommodations { get; set; }

    public virtual DbSet<AccountRole> AccountRoles { get; set; }

    public virtual DbSet<AdminAudit> AdminAudits { get; set; }

    public virtual DbSet<ApplicationStatusChange> ApplicationStatusChanges { get; set; }

    public virtual DbSet<Area> Areas { get; set; }

    public virtual DbSet<AssistantLog> AssistantLogs { get; set; }

    public virtual DbSet<AvailabilitySlot> AvailabilitySlots { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<Community> Communities { get; set; }

    public virtual DbSet<CommunityMembership> CommunityMemberships { get; set; }

    public virtual DbSet<ConsentRecord> ConsentRecords { get; set; }

    public virtual DbSet<ContentReport> ContentReports { get; set; }

    public virtual DbSet<DeletionRequest> DeletionRequests { get; set; }

    public virtual DbSet<DirectoryItem> DirectoryItems { get; set; }

    public virtual DbSet<EmailVerification> EmailVerifications { get; set; }

    public virtual DbSet<EmergencyContact> EmergencyContacts { get; set; }

    public virtual DbSet<FacilityType> FacilityTypes { get; set; }

    public virtual DbSet<FeatureRegistry> FeatureRegistries { get; set; }

    public virtual DbSet<Governorate> Governorates { get; set; }

    public virtual DbSet<HelpAssignment> HelpAssignments { get; set; }

    public virtual DbSet<HelpConversation> HelpConversations { get; set; }

    public virtual DbSet<HelpFeedback> HelpFeedbacks { get; set; }

    public virtual DbSet<HelpMessage> HelpMessages { get; set; }

    public virtual DbSet<HelpOffer> HelpOffers { get; set; }

    public virtual DbSet<HelpRequest> HelpRequests { get; set; }

    public virtual DbSet<InquiryMessage> InquiryMessages { get; set; }

    public virtual DbSet<Interest> Interests { get; set; }

    public virtual DbSet<InterviewSlot> InterviewSlots { get; set; }

    public virtual DbSet<Job> Jobs { get; set; }

    public virtual DbSet<JobAccommodation> JobAccommodations { get; set; }

    public virtual DbSet<JobApplication> JobApplications { get; set; }

    public virtual DbSet<NeedType> NeedTypes { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<ObstacleReport> ObstacleReports { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<OrganizationDocument> OrganizationDocuments { get; set; }

    public virtual DbSet<OrganizationReview> OrganizationReviews { get; set; }

    public virtual DbSet<PhoneVerification> PhoneVerifications { get; set; }

    public virtual DbSet<Place> Places { get; set; }

    public virtual DbSet<PlaceFacility> PlaceFacilities { get; set; }

    public virtual DbSet<PlaceReview> PlaceReviews { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostMedium> PostMedia { get; set; }

    public virtual DbSet<PostReaction> PostReactions { get; set; }

    public virtual DbSet<SafetyReport> SafetyReports { get; set; }

    public virtual DbSet<ServiceInquiry> ServiceInquiries { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<SosAlert> SosAlerts { get; set; }

    public virtual DbSet<SosDelivery> SosDeliveries { get; set; }

    public virtual DbSet<TrainingAttempt> TrainingAttempts { get; set; }

    public virtual DbSet<TrainingModule> TrainingModules { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<UserFollow> UserFollows { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<VerificationEvidence> VerificationEvidences { get; set; }

    public virtual DbSet<VerificationReview> VerificationReviews { get; set; }

    public virtual DbSet<VolunteerProfile> VolunteerProfiles { get; set; }

    public virtual DbSet<VolunteerSocialLink> VolunteerSocialLinks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AccessibilityProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("ACCESSIBILITY_PROFILE");

            entity.HasIndex(e => e.ConsentId, "IX_ACCESSIBILITY_PROFILE_consent_id");

            entity.HasIndex(e => e.NeedTypeId, "IX_ACCESSIBILITY_PROFILE_need_type_id");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.ConsentId).HasColumnName("consent_id");
            entity.Property(e => e.FontScale)
                .HasColumnType("decimal(4, 2)")
                .HasColumnName("font_scale");
            entity.Property(e => e.InteractionMode)
                .HasMaxLength(50)
                .HasColumnName("interaction_mode");
            entity.Property(e => e.ManualOverrides).HasColumnName("manual_overrides");
            entity.Property(e => e.NeedTypeId).HasColumnName("need_type_id");
            entity.Property(e => e.NotificationMode)
                .HasMaxLength(50)
                .HasColumnName("notification_mode");
            entity.Property(e => e.ReducedMotion).HasColumnName("reduced_motion");
            entity.Property(e => e.Theme)
                .HasMaxLength(50)
                .HasColumnName("theme");
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ACCESSIBILITY_PROFILE_updated_at")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Consent).WithMany(p => p.AccessibilityProfiles)
                .HasForeignKey(d => d.ConsentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACCESSIBILITY_PROFILE_consent_id");

            entity.HasOne(d => d.NeedType).WithMany(p => p.AccessibilityProfiles)
                .HasForeignKey(d => d.NeedTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACCESSIBILITY_PROFILE_need_type_id");

            entity.HasOne(d => d.User).WithOne(p => p.AccessibilityProfile)
                .HasForeignKey<AccessibilityProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACCESSIBILITY_PROFILE_user_id");
        });

        modelBuilder.Entity<Accommodation>(entity =>
        {
            entity.ToTable("ACCOMMODATION");

            entity.Property(e => e.AccommodationId).HasColumnName("accommodation_id");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasColumnName("category");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.AreaId);

            entity.ToTable("AREA");

            entity.HasIndex(e => new { e.GovernorateId, e.Code }, "UQ_AREA_governorate_code").IsUnique();

            entity.HasIndex(e => e.GovernorateId, "IX_AREA_governorate_id");

            entity.Property(e => e.AreaId)
                .ValueGeneratedOnAdd()
                .HasColumnName("area_id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.GovernorateId).HasColumnName("governorate_id");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.NameAr)
                .HasMaxLength(100)
                .HasColumnName("name_ar");

            entity.HasOne(d => d.Governorate).WithMany(p => p.Areas)
                .HasForeignKey(d => d.GovernorateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AREA_governorate_id");
        });

        modelBuilder.Entity<Governorate>(entity =>
        {
            entity.HasKey(e => e.GovernorateId);

            entity.ToTable("GOVERNORATE");

            entity.HasIndex(e => e.Code, "UQ_GOVERNORATE_code").IsUnique();

            entity.Property(e => e.GovernorateId)
                .ValueGeneratedOnAdd()
                .HasColumnName("governorate_id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.NameAr)
                .HasMaxLength(100)
                .HasColumnName("name_ar");
        });

        modelBuilder.Entity<AccountRole>(entity =>
        {
            entity.HasKey(e => e.RoleId);

            entity.ToTable("ACCOUNT_ROLE");

            entity.HasIndex(e => e.RoleName, "UQ_ACCOUNT_ROLE_role_name").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<AdminAudit>(entity =>
        {
            entity.HasKey(e => e.AuditId);

            entity.ToTable("ADMIN_AUDIT");

            entity.HasIndex(e => e.ActorUserId, "IX_ADMIN_AUDIT_actor_user_id");

            entity.Property(e => e.AuditId)
                .HasDefaultValueSql("(newsequentialid())", "DF_ADMIN_AUDIT_audit_id")
                .HasColumnName("audit_id");
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .HasColumnName("action");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.OccurredAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ADMIN_AUDIT_occurred_at")
                .HasColumnName("occurred_at");
            entity.Property(e => e.Outcome)
                .HasMaxLength(50)
                .HasColumnName("outcome");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .HasColumnName("target_type");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.AdminAudits)
                .HasForeignKey(d => d.ActorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ADMIN_AUDIT_actor_user_id");
        });

        modelBuilder.Entity<ApplicationStatusChange>(entity =>
        {
            entity.HasKey(e => e.ChangeId);

            entity.ToTable("APPLICATION_STATUS_CHANGE");

            entity.HasIndex(e => e.ActorUserId, "IX_APPLICATION_STATUS_CHANGE_actor_user_id");

            entity.HasIndex(e => e.ApplicationId, "IX_APPLICATION_STATUS_CHANGE_application_id");

            entity.Property(e => e.ChangeId)
                .HasDefaultValueSql("(newsequentialid())", "DF_APPLICATION_STATUS_CHANGE_change_id")
                .HasColumnName("change_id");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.ApplicationId).HasColumnName("application_id");
            entity.Property(e => e.ChangedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_APPLICATION_STATUS_CHANGE_changed_at")
                .HasColumnName("changed_at");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(50)
                .HasColumnName("new_status");
            entity.Property(e => e.PreviousStatus)
                .HasMaxLength(50)
                .HasColumnName("previous_status");

            entity.HasOne(d => d.ActorUser).WithMany(p => p.ApplicationStatusChanges)
                .HasForeignKey(d => d.ActorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_APPLICATION_STATUS_CHANGE_actor_user_id");

            entity.HasOne(d => d.Application).WithMany(p => p.ApplicationStatusChanges)
                .HasForeignKey(d => d.ApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_APPLICATION_STATUS_CHANGE_application_id");
        });

        modelBuilder.Entity<AssistantLog>(entity =>
        {
            entity.HasKey(e => e.LogId);

            entity.ToTable("ASSISTANT_LOG");

            entity.HasIndex(e => e.UserId, "IX_ASSISTANT_LOG_user_id");

            entity.Property(e => e.LogId)
                .HasDefaultValueSql("(newsequentialid())", "DF_ASSISTANT_LOG_log_id")
                .HasColumnName("log_id");
            entity.Property(e => e.Confidence)
                .HasColumnType("decimal(4, 3)")
                .HasColumnName("confidence");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ASSISTANT_LOG_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Refused).HasColumnName("refused");
            entity.Property(e => e.Tool)
                .HasMaxLength(50)
                .HasColumnName("tool");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AssistantLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ASSISTANT_LOG_user_id");
        });

        modelBuilder.Entity<AvailabilitySlot>(entity =>
        {
            entity.HasKey(e => e.SlotId);

            entity.ToTable("AVAILABILITY_SLOT");

            entity.HasIndex(e => e.VolunteerId, "IX_AVAILABILITY_SLOT_volunteer_id");

            entity.Property(e => e.SlotId)
                .HasDefaultValueSql("(newsequentialid())", "DF_AVAILABILITY_SLOT_slot_id")
                .HasColumnName("slot_id");
            entity.Property(e => e.EndsAt)
                .HasPrecision(0)
                .HasColumnName("ends_at");
            entity.Property(e => e.StartsAt)
                .HasPrecision(0)
                .HasColumnName("starts_at");
            entity.Property(e => e.Timezone)
                .HasMaxLength(50)
                .HasColumnName("timezone");
            entity.Property(e => e.VolunteerId).HasColumnName("volunteer_id");

            entity.HasOne(d => d.Volunteer).WithMany(p => p.AvailabilitySlots)
                .HasForeignKey(d => d.VolunteerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AVAILABILITY_SLOT_volunteer_id");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("COMMENT");

            entity.HasIndex(e => e.AuthorId, "IX_COMMENT_author_id");

            entity.HasIndex(e => e.PostId, "IX_COMMENT_post_id");

            entity.Property(e => e.CommentId)
                .HasDefaultValueSql("(newsequentialid())", "DF_COMMENT_comment_id")
                .HasColumnName("comment_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_COMMENT_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.ModerationStatus)
                .HasMaxLength(50)
                .HasColumnName("moderation_status");
            entity.Property(e => e.PostId).HasColumnName("post_id");

            entity.HasOne(d => d.Author).WithMany(p => p.Comments)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_COMMENT_author_id");

            entity.HasOne(d => d.Post).WithMany(p => p.Comments)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_COMMENT_post_id");
        });

        modelBuilder.Entity<Community>(entity =>
        {
            entity.ToTable("COMMUNITY");

            entity.Property(e => e.CommunityId)
                .HasDefaultValueSql("(newsequentialid())", "DF_COMMUNITY_community_id")
                .HasColumnName("community_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Rules).HasColumnName("rules");
        });

        modelBuilder.Entity<CommunityMembership>(entity =>
        {
            entity.HasKey(e => new { e.CommunityId, e.UserId });

            entity.ToTable("COMMUNITY_MEMBERSHIP");

            entity.HasIndex(e => e.UserId, "IX_COMMUNITY_MEMBERSHIP_user_id");

            entity.Property(e => e.CommunityId).HasColumnName("community_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.JoinedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_COMMUNITY_MEMBERSHIP_joined_at")
                .HasColumnName("joined_at");

            entity.HasOne(d => d.Community).WithMany(p => p.CommunityMemberships)
                .HasForeignKey(d => d.CommunityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_COMMUNITY_MEMBERSHIP_community_id");

            entity.HasOne(d => d.User).WithMany(p => p.CommunityMemberships)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_COMMUNITY_MEMBERSHIP_user_id");
        });

        modelBuilder.Entity<ConsentRecord>(entity =>
        {
            entity.HasKey(e => e.ConsentId);

            entity.ToTable("CONSENT_RECORD");

            entity.HasIndex(e => e.UserId, "IX_CONSENT_RECORD_user_id");

            entity.Property(e => e.ConsentId)
                .HasDefaultValueSql("(newsequentialid())", "DF_CONSENT_RECORD_consent_id")
                .HasColumnName("consent_id");
            entity.Property(e => e.GrantedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_CONSENT_RECORD_granted_at")
                .HasColumnName("granted_at");
            entity.Property(e => e.PolicyVersion)
                .HasMaxLength(50)
                .HasColumnName("policy_version");
            entity.Property(e => e.Purpose)
                .HasMaxLength(200)
                .HasColumnName("purpose");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WithdrawnAt)
                .HasPrecision(0)
                .HasColumnName("withdrawn_at");

            entity.HasOne(d => d.User).WithMany(p => p.ConsentRecords)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CONSENT_RECORD_user_id");
        });

        modelBuilder.Entity<ContentReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.ToTable("CONTENT_REPORT");

            entity.HasIndex(e => e.CommentId, "IX_CONTENT_REPORT_comment_id");

            entity.HasIndex(e => e.PostId, "IX_CONTENT_REPORT_post_id");

            entity.HasIndex(e => e.ReporterId, "IX_CONTENT_REPORT_reporter_id");

            entity.HasIndex(e => e.ReviewerId, "IX_CONTENT_REPORT_reviewer_id");

            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("(newsequentialid())", "DF_CONTENT_REPORT_report_id")
                .HasColumnName("report_id");
            entity.Property(e => e.CommentId).HasColumnName("comment_id");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReporterId).HasColumnName("reporter_id");
            entity.Property(e => e.Resolution).HasColumnName("resolution");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.HasOne(d => d.Comment).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.CommentId)
                .HasConstraintName("FK_CONTENT_REPORT_comment_id");

            entity.HasOne(d => d.Post).WithMany(p => p.ContentReports)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("FK_CONTENT_REPORT_post_id");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ContentReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CONTENT_REPORT_reporter_id");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.ContentReportReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .HasConstraintName("FK_CONTENT_REPORT_reviewer_id");
        });

        modelBuilder.Entity<DeletionRequest>(entity =>
        {
            entity.ToTable("DELETION_REQUEST");

            entity.HasIndex(e => e.UserId, "IX_DELETION_REQUEST_user_id");

            entity.Property(e => e.DeletionRequestId)
                .HasDefaultValueSql("(newsequentialid())", "DF_DELETION_REQUEST_deletion_request_id")
                .HasColumnName("deletion_request_id");
            entity.Property(e => e.RequestedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_DELETION_REQUEST_requested_at")
                .HasColumnName("requested_at");
            entity.Property(e => e.ResolvedAt)
                .HasPrecision(0)
                .HasColumnName("resolved_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.DeletionRequests)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DELETION_REQUEST_user_id");
        });

        modelBuilder.Entity<DirectoryItem>(entity =>
        {
            entity.HasKey(e => e.ItemId);

            entity.ToTable("DIRECTORY_ITEM");

            entity.HasIndex(e => e.OrganizationId, "IX_DIRECTORY_ITEM_organization_id");

            entity.Property(e => e.ItemId)
                .HasDefaultValueSql("(newsequentialid())", "DF_DIRECTORY_ITEM_item_id")
                .HasColumnName("item_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndsAt)
                .HasPrecision(0)
                .HasColumnName("ends_at");
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .HasColumnName("item_type");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.PublicationStatus)
                .HasMaxLength(50)
                .HasColumnName("publication_status");
            entity.Property(e => e.StartsAt)
                .HasPrecision(0)
                .HasColumnName("starts_at");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.Organization).WithMany(p => p.DirectoryItems)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DIRECTORY_ITEM_organization_id");
        });

        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.HasKey(e => e.VerificationId);

            entity.ToTable("EMAIL_VERIFICATION");

            entity.HasIndex(e => e.UserId, "IX_EMAIL_VERIFICATION_user_id");

            entity.Property(e => e.VerificationId)
                .HasDefaultValueSql("(newsequentialid())", "DF_EMAIL_VERIFICATION_verification_id")
                .HasColumnName("verification_id");
            entity.Property(e => e.CodeHash)
                .HasMaxLength(200)
                .HasColumnName("code_hash");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(0)
                .HasColumnName("expires_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VerifiedAt)
                .HasPrecision(0)
                .HasColumnName("verified_at");

            entity.HasOne(d => d.User).WithMany(p => p.EmailVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMAIL_VERIFICATION_user_id");
        });

        modelBuilder.Entity<EmergencyContact>(entity =>
        {
            entity.HasKey(e => e.ContactId);

            entity.ToTable("EMERGENCY_CONTACT");

            entity.HasIndex(e => e.OwnerUserId, "IX_EMERGENCY_CONTACT_owner_user_id");

            entity.Property(e => e.ContactId)
                .HasDefaultValueSql("(newsequentialid())", "DF_EMERGENCY_CONTACT_contact_id")
                .HasColumnName("contact_id");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(200)
                .HasColumnName("display_name");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.ProtectedContactDestination)
                .HasMaxLength(500)
                .HasColumnName("protected_contact_destination");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.EmergencyContacts)
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EMERGENCY_CONTACT_owner_user_id");
        });

        modelBuilder.Entity<FacilityType>(entity =>
        {
            entity.ToTable("FACILITY_TYPE");

            entity.HasIndex(e => e.Name, "UQ_FACILITY_TYPE_name").IsUnique();

            entity.Property(e => e.FacilityTypeId).HasColumnName("facility_type_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
        });

        modelBuilder.Entity<FeatureRegistry>(entity =>
        {
            entity.HasKey(e => e.FeatureKey);

            entity.ToTable("FEATURE_REGISTRY");

            entity.Property(e => e.FeatureKey)
                .HasMaxLength(500)
                .HasColumnName("feature_key");
            entity.Property(e => e.DeliveryPhase)
                .HasMaxLength(50)
                .HasColumnName("delivery_phase");
            entity.Property(e => e.ImplementationStatus)
                .HasMaxLength(50)
                .HasColumnName("implementation_status");
        });

        modelBuilder.Entity<HelpAssignment>(entity =>
        {
            entity.HasKey(e => e.RequestId);

            entity.ToTable("HELP_ASSIGNMENT");

            entity.HasIndex(e => e.OfferId, "UQ_HELP_ASSIGNMENT_offer_id").IsUnique();

            entity.Property(e => e.RequestId)
                .ValueGeneratedNever()
                .HasColumnName("request_id");
            entity.Property(e => e.OfferId).HasColumnName("offer_id");
            entity.Property(e => e.RequesterAcceptedAt)
                .HasPrecision(0)
                .HasColumnName("requester_accepted_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.VolunteerAcceptedAt)
                .HasPrecision(0)
                .HasColumnName("volunteer_accepted_at");

            entity.HasOne(d => d.Offer).WithOne(p => p.HelpAssignment)
                .HasForeignKey<HelpAssignment>(d => d.OfferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_ASSIGNMENT_offer_id");

            entity.HasOne(d => d.Request).WithOne(p => p.HelpAssignment)
                .HasForeignKey<HelpAssignment>(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_ASSIGNMENT_request_id");
        });

        modelBuilder.Entity<HelpConversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId);

            entity.ToTable("HELP_CONVERSATION");

            entity.HasIndex(e => e.RequestId, "UQ_HELP_CONVERSATION_request_id").IsUnique();

            entity.Property(e => e.ConversationId)
                .HasDefaultValueSql("(newsequentialid())", "DF_HELP_CONVERSATION_conversation_id")
                .HasColumnName("conversation_id");
            entity.Property(e => e.OpenedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_HELP_CONVERSATION_opened_at")
                .HasColumnName("opened_at");
            entity.Property(e => e.RequestId).HasColumnName("request_id");

            entity.HasOne(d => d.Request).WithOne(p => p.HelpConversation)
                .HasForeignKey<HelpConversation>(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_CONVERSATION_request_id");
        });

        modelBuilder.Entity<HelpFeedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId);

            entity.ToTable("HELP_FEEDBACK");

            entity.HasIndex(e => e.AuthorId, "IX_HELP_FEEDBACK_author_id");

            entity.HasIndex(e => e.RatedUserId, "IX_HELP_FEEDBACK_rated_user_id");

            entity.HasIndex(e => e.RequestId, "IX_HELP_FEEDBACK_request_id");

            entity.HasIndex(e => new { e.RequestId, e.AuthorId }, "UQ_HELP_FEEDBACK_request_id_author_id").IsUnique();

            entity.Property(e => e.FeedbackId)
                .HasDefaultValueSql("(newsequentialid())", "DF_HELP_FEEDBACK_feedback_id")
                .HasColumnName("feedback_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_HELP_FEEDBACK_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.RatedUserId).HasColumnName("rated_user_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.RequestId).HasColumnName("request_id");

            entity.HasOne(d => d.Author).WithMany(p => p.HelpFeedbackAuthors)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_FEEDBACK_author_id");

            entity.HasOne(d => d.RatedUser).WithMany(p => p.HelpFeedbackRatedUsers)
                .HasForeignKey(d => d.RatedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_FEEDBACK_rated_user_id");

            entity.HasOne(d => d.Request).WithMany(p => p.HelpFeedbacks)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_FEEDBACK_request_id");
        });

        modelBuilder.Entity<HelpMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.ToTable("HELP_MESSAGE");

            entity.HasIndex(e => e.ConversationId, "IX_HELP_MESSAGE_conversation_id");

            entity.HasIndex(e => e.SenderId, "IX_HELP_MESSAGE_sender_id");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("(newsequentialid())", "DF_HELP_MESSAGE_message_id")
                .HasColumnName("message_id");
            entity.Property(e => e.AudioStorageKey)
                .HasMaxLength(500)
                .HasColumnName("audio_storage_key");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.SentAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_HELP_MESSAGE_sent_at")
                .HasColumnName("sent_at");
            entity.Property(e => e.TextBody).HasColumnName("text_body");

            entity.HasOne(d => d.Conversation).WithMany(p => p.HelpMessages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_MESSAGE_conversation_id");

            entity.HasOne(d => d.Sender).WithMany(p => p.HelpMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_MESSAGE_sender_id");
        });

        modelBuilder.Entity<HelpOffer>(entity =>
        {
            entity.HasKey(e => e.OfferId);

            entity.ToTable("HELP_OFFER");

            entity.HasIndex(e => e.RequestId, "IX_HELP_OFFER_request_id");

            entity.HasIndex(e => e.VolunteerId, "IX_HELP_OFFER_volunteer_id");

            entity.Property(e => e.OfferId)
                .HasDefaultValueSql("(newsequentialid())", "DF_HELP_OFFER_offer_id")
                .HasColumnName("offer_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_HELP_OFFER_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.VolunteerId).HasColumnName("volunteer_id");

            entity.HasOne(d => d.Request).WithMany(p => p.HelpOffers)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_OFFER_request_id");

            entity.HasOne(d => d.Volunteer).WithMany(p => p.HelpOffers)
                .HasForeignKey(d => d.VolunteerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_OFFER_volunteer_id");
        });

        modelBuilder.Entity<HelpRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId);

            entity.ToTable("HELP_REQUEST");

            entity.HasIndex(e => e.RequesterId, "IX_HELP_REQUEST_requester_id");

            entity.HasIndex(e => e.SkillId, "IX_HELP_REQUEST_skill_id");

            entity.Property(e => e.RequestId)
                .HasDefaultValueSql("(newsequentialid())", "DF_HELP_REQUEST_request_id")
                .HasColumnName("request_id");
            entity.Property(e => e.ApproximateArea)
                .HasMaxLength(200)
                .HasColumnName("approximate_area");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasColumnName("category");
            entity.Property(e => e.CompletedAt)
                .HasPrecision(0)
                .HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_HELP_REQUEST_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.HelpMode)
                .HasMaxLength(50)
                .HasColumnName("help_mode");
            entity.Property(e => e.RequesterId).HasColumnName("requester_id");
            entity.Property(e => e.ScheduledAt)
                .HasPrecision(0)
                .HasColumnName("scheduled_at");
            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Urgency)
                .HasMaxLength(50)
                .HasColumnName("urgency");
            entity.Property(e => e.VoiceStorageKey)
                .HasMaxLength(500)
                .HasColumnName("voice_storage_key");

            entity.HasOne(d => d.Requester).WithMany(p => p.HelpRequests)
                .HasForeignKey(d => d.RequesterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HELP_REQUEST_requester_id");

            entity.HasOne(d => d.Skill).WithMany(p => p.HelpRequests)
                .HasForeignKey(d => d.SkillId)
                .HasConstraintName("FK_HELP_REQUEST_skill_id");
        });

        modelBuilder.Entity<InquiryMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.ToTable("INQUIRY_MESSAGE");

            entity.HasIndex(e => e.InquiryId, "IX_INQUIRY_MESSAGE_inquiry_id");

            entity.HasIndex(e => e.SenderId, "IX_INQUIRY_MESSAGE_sender_id");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("(newsequentialid())", "DF_INQUIRY_MESSAGE_message_id")
                .HasColumnName("message_id");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.InquiryId).HasColumnName("inquiry_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.SentAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_INQUIRY_MESSAGE_sent_at")
                .HasColumnName("sent_at");

            entity.HasOne(d => d.Inquiry).WithMany(p => p.InquiryMessages)
                .HasForeignKey(d => d.InquiryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_INQUIRY_MESSAGE_inquiry_id");

            entity.HasOne(d => d.Sender).WithMany(p => p.InquiryMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_INQUIRY_MESSAGE_sender_id");
        });

        modelBuilder.Entity<Interest>(entity =>
        {
            entity.ToTable("INTEREST");

            entity.HasIndex(e => e.Name, "UQ_INTEREST_name").IsUnique();

            entity.Property(e => e.InterestId).HasColumnName("interest_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
        });

        modelBuilder.Entity<InterviewSlot>(entity =>
        {
            entity.ToTable("INTERVIEW_SLOT");

            entity.HasIndex(e => e.ReviewerId, "IX_INTERVIEW_SLOT_reviewer_id");

            entity.HasIndex(e => new { e.ReviewerId, e.StartsAt }, "UQ_INTERVIEW_SLOT_reviewer_start").IsUnique();

            entity.Property(e => e.InterviewSlotId)
                .HasDefaultValueSql("(newsequentialid())", "DF_INTERVIEW_SLOT_interview_slot_id")
                .HasColumnName("interview_slot_id");
            entity.Property(e => e.DurationMinutes)
                .HasDefaultValue(30, "DF_INTERVIEW_SLOT_duration")
                .HasColumnName("duration_minutes");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.StartsAt)
                .HasPrecision(0)
                .HasColumnName("starts_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("available", "DF_INTERVIEW_SLOT_status")
                .HasColumnName("status");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.InterviewSlots)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_INTERVIEW_SLOT_reviewer_id");
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("JOB");

            entity.HasIndex(e => e.OrganizationId, "IX_JOB_organization_id");

            entity.Property(e => e.JobId)
                .HasDefaultValueSql("(newsequentialid())", "DF_JOB_job_id")
                .HasColumnName("job_id");
            entity.Property(e => e.AccessibilityInformation).HasColumnName("accessibility_information");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EmploymentType)
                .HasMaxLength(50)
                .HasColumnName("employment_type");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(0)
                .HasColumnName("expires_at");
            entity.Property(e => e.Location)
                .HasMaxLength(200)
                .HasColumnName("location");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.PublishedAt)
                .HasPrecision(0)
                .HasColumnName("published_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.WorkArrangement)
                .HasMaxLength(50)
                .HasColumnName("work_arrangement");

            entity.HasOne(d => d.Organization).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JOB_organization_id");
        });

        modelBuilder.Entity<JobAccommodation>(entity =>
        {
            entity.HasKey(e => new { e.JobId, e.AccommodationId });

            entity.ToTable("JOB_ACCOMMODATION");

            entity.HasIndex(e => e.AccommodationId, "IX_JOB_ACCOMMODATION_accommodation_id");

            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.AccommodationId).HasColumnName("accommodation_id");
            entity.Property(e => e.Active).HasColumnName("active");

            entity.HasOne(d => d.Accommodation).WithMany(p => p.JobAccommodations)
                .HasForeignKey(d => d.AccommodationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JOB_ACCOMMODATION_accommodation_id");

            entity.HasOne(d => d.Job).WithMany(p => p.JobAccommodations)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JOB_ACCOMMODATION_job_id");
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(e => e.ApplicationId);

            entity.ToTable("JOB_APPLICATION");

            entity.HasIndex(e => e.ApplicantId, "IX_JOB_APPLICATION_applicant_id");

            entity.HasIndex(e => e.JobId, "IX_JOB_APPLICATION_job_id");

            entity.HasIndex(e => new { e.JobId, e.ApplicantId }, "UQ_JOB_APPLICATION_job_id_applicant_id").IsUnique();

            entity.Property(e => e.ApplicationId)
                .HasDefaultValueSql("(newsequentialid())", "DF_JOB_APPLICATION_application_id")
                .HasColumnName("application_id");
            entity.Property(e => e.ApplicantId).HasColumnName("applicant_id");
            entity.Property(e => e.ApplicationText).HasColumnName("application_text");
            entity.Property(e => e.CvStorageKey)
                .HasMaxLength(500)
                .HasColumnName("cv_storage_key");
            entity.Property(e => e.JobId).HasColumnName("job_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.SubmittedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_JOB_APPLICATION_submitted_at")
                .HasColumnName("submitted_at");
            entity.Property(e => e.VoluntarilySharedNeeds).HasColumnName("voluntarily_shared_needs");

            entity.HasOne(d => d.Applicant).WithMany(p => p.JobApplications)
                .HasForeignKey(d => d.ApplicantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JOB_APPLICATION_applicant_id");

            entity.HasOne(d => d.Job).WithMany(p => p.JobApplications)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JOB_APPLICATION_job_id");
        });

        modelBuilder.Entity<NeedType>(entity =>
        {
            entity.ToTable("NEED_TYPE");

            entity.HasIndex(e => e.Name, "UQ_NEED_TYPE_name").IsUnique();

            entity.Property(e => e.NeedTypeId).HasColumnName("need_type_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("NOTIFICATION");

            entity.HasIndex(e => e.RecipientId, "IX_NOTIFICATION_recipient_id");

            entity.Property(e => e.NotificationId)
                .HasDefaultValueSql("(newsequentialid())", "DF_NOTIFICATION_notification_id")
                .HasColumnName("notification_id");
            entity.Property(e => e.Channel)
                .HasMaxLength(50)
                .HasColumnName("channel");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_NOTIFICATION_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.NotificationType)
                .HasMaxLength(50)
                .HasColumnName("notification_type");
            entity.Property(e => e.ReadAt)
                .HasPrecision(0)
                .HasColumnName("read_at");
            entity.Property(e => e.RecipientId).HasColumnName("recipient_id");
            entity.Property(e => e.TextBody).HasColumnName("text_body");

            entity.HasOne(d => d.Recipient).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NOTIFICATION_recipient_id");
        });

        modelBuilder.Entity<ObstacleReport>(entity =>
        {
            entity.HasKey(e => e.ObstacleId);

            entity.ToTable("OBSTACLE_REPORT");

            entity.HasIndex(e => e.PlaceId, "IX_OBSTACLE_REPORT_place_id");

            entity.HasIndex(e => e.ReporterId, "IX_OBSTACLE_REPORT_reporter_id");

            entity.Property(e => e.ObstacleId)
                .HasDefaultValueSql("(newsequentialid())", "DF_OBSTACLE_REPORT_obstacle_id")
                .HasColumnName("obstacle_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("longitude");
            entity.Property(e => e.PlaceId).HasColumnName("place_id");
            entity.Property(e => e.ReportedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_OBSTACLE_REPORT_reported_at")
                .HasColumnName("reported_at");
            entity.Property(e => e.ReporterId).HasColumnName("reporter_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.HasOne(d => d.Place).WithMany(p => p.ObstacleReports)
                .HasForeignKey(d => d.PlaceId)
                .HasConstraintName("FK_OBSTACLE_REPORT_place_id");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ObstacleReports)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OBSTACLE_REPORT_reporter_id");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("ORGANIZATION");

            entity.HasIndex(e => e.OwnerUserId, "UQ_ORGANIZATION_owner_user_id").IsUnique();

            entity.Property(e => e.OrganizationId)
                .HasDefaultValueSql("(newsequentialid())", "DF_ORGANIZATION_organization_id")
                .HasColumnName("organization_id");
            entity.Property(e => e.ApprovalStatus)
                .HasMaxLength(50)
                .HasColumnName("approval_status");
            entity.Property(e => e.City)
                .HasMaxLength(200)
                .HasColumnName("city");
            entity.Property(e => e.ContactInformation).HasColumnName("contact_information");
            entity.Property(e => e.CoverStorageKey)
                .HasMaxLength(500)
                .HasColumnName("cover_storage_key");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EmployeeCountRange)
                .HasMaxLength(50)
                .HasColumnName("employee_count_range");
            entity.Property(e => e.Field)
                .HasMaxLength(200)
                .HasColumnName("field");
            entity.Property(e => e.Governorate)
                .HasMaxLength(100)
                .HasColumnName("governorate");
            entity.Property(e => e.LogoStorageKey)
                .HasMaxLength(500)
                .HasColumnName("logo_storage_key");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.OrganizationType)
                .HasMaxLength(50)
                .HasColumnName("organization_type");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.Website)
                .HasMaxLength(300)
                .HasColumnName("website");

            entity.HasOne(d => d.OwnerUser).WithOne(p => p.Organization)
                .HasForeignKey<Organization>(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORGANIZATION_owner_user_id");
        });

        modelBuilder.Entity<OrganizationDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId);

            entity.ToTable("ORGANIZATION_DOCUMENT");

            entity.HasIndex(e => e.OrganizationId, "IX_ORGANIZATION_DOCUMENT_organization_id");

            entity.HasIndex(e => new { e.OrganizationId, e.DocumentType }, "UQ_ORGANIZATION_DOCUMENT_type").IsUnique();

            entity.Property(e => e.DocumentId)
                .HasDefaultValueSql("(newsequentialid())", "DF_ORGANIZATION_DOCUMENT_document_id")
                .HasColumnName("document_id");
            entity.Property(e => e.DocumentType)
                .HasMaxLength(50)
                .HasColumnName("document_type");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.StorageKey)
                .HasMaxLength(500)
                .HasColumnName("storage_key");
            entity.Property(e => e.UploadedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ORGANIZATION_DOCUMENT_uploaded_at")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationDocuments)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORGANIZATION_DOCUMENT_organization_id");
        });

        modelBuilder.Entity<OrganizationReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId);

            entity.ToTable("ORGANIZATION_REVIEW");

            entity.HasIndex(e => e.OrganizationId, "IX_ORGANIZATION_REVIEW_organization_id");

            entity.HasIndex(e => e.ReviewerId, "IX_ORGANIZATION_REVIEW_reviewer_id");

            entity.Property(e => e.ReviewId)
                .HasDefaultValueSql("(newsequentialid())", "DF_ORGANIZATION_REVIEW_review_id")
                .HasColumnName("review_id");
            entity.Property(e => e.DecidedAt)
                .HasPrecision(0)
                .HasColumnName("decided_at");
            entity.Property(e => e.DecisionNotes).HasColumnName("decision_notes");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.RequestedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_ORGANIZATION_REVIEW_requested_at")
                .HasColumnName("requested_at");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.HasOne(d => d.Organization).WithMany(p => p.OrganizationReviews)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORGANIZATION_REVIEW_organization_id");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.OrganizationReviews)
                .HasForeignKey(d => d.ReviewerId)
                .HasConstraintName("FK_ORGANIZATION_REVIEW_reviewer_id");
        });

        modelBuilder.Entity<PhoneVerification>(entity =>
        {
            entity.HasKey(e => e.VerificationId);

            entity.ToTable("PHONE_VERIFICATION");

            entity.HasIndex(e => e.UserId, "IX_PHONE_VERIFICATION_user_id");

            entity.Property(e => e.VerificationId)
                .HasDefaultValueSql("(newsequentialid())", "DF_PHONE_VERIFICATION_verification_id")
                .HasColumnName("verification_id");
            entity.Property(e => e.CodeHash)
                .HasMaxLength(200)
                .HasColumnName("code_hash");
            entity.Property(e => e.ExpiresAt)
                .HasPrecision(0)
                .HasColumnName("expires_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VerifiedAt)
                .HasPrecision(0)
                .HasColumnName("verified_at");

            entity.HasOne(d => d.User).WithMany(p => p.PhoneVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PHONE_VERIFICATION_user_id");
        });

        modelBuilder.Entity<Place>(entity =>
        {
            entity.ToTable("PLACE");

            entity.Property(e => e.PlaceId)
                .HasDefaultValueSql("(newsequentialid())", "DF_PLACE_place_id")
                .HasColumnName("place_id");
            entity.Property(e => e.Address)
                .HasMaxLength(400)
                .HasColumnName("address");
            entity.Property(e => e.LastVerifiedAt)
                .HasPrecision(0)
                .HasColumnName("last_verified_at");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.VerificationSource)
                .HasMaxLength(200)
                .HasColumnName("verification_source");
        });

        modelBuilder.Entity<PlaceFacility>(entity =>
        {
            entity.HasKey(e => new { e.PlaceId, e.FacilityTypeId });

            entity.ToTable("PLACE_FACILITY");

            entity.HasIndex(e => e.FacilityTypeId, "IX_PLACE_FACILITY_facility_type_id");

            entity.Property(e => e.PlaceId).HasColumnName("place_id");
            entity.Property(e => e.FacilityTypeId).HasColumnName("facility_type_id");
            entity.Property(e => e.AvailabilityStatus)
                .HasMaxLength(50)
                .HasColumnName("availability_status");
            entity.Property(e => e.Notes).HasColumnName("notes");

            entity.HasOne(d => d.FacilityType).WithMany(p => p.PlaceFacilities)
                .HasForeignKey(d => d.FacilityTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PLACE_FACILITY_facility_type_id");

            entity.HasOne(d => d.Place).WithMany(p => p.PlaceFacilities)
                .HasForeignKey(d => d.PlaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PLACE_FACILITY_place_id");
        });

        modelBuilder.Entity<PlaceReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId);

            entity.ToTable("PLACE_REVIEW");

            entity.HasIndex(e => e.PlaceId, "IX_PLACE_REVIEW_place_id");

            entity.HasIndex(e => e.UserId, "IX_PLACE_REVIEW_user_id");

            entity.Property(e => e.ReviewId)
                .HasDefaultValueSql("(newsequentialid())", "DF_PLACE_REVIEW_review_id")
                .HasColumnName("review_id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_PLACE_REVIEW_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.PlaceId).HasColumnName("place_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Place).WithMany(p => p.PlaceReviews)
                .HasForeignKey(d => d.PlaceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PLACE_REVIEW_place_id");

            entity.HasOne(d => d.User).WithMany(p => p.PlaceReviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PLACE_REVIEW_user_id");
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("POST");

            entity.HasIndex(e => e.AuthorId, "IX_POST_author_id");

            entity.HasIndex(e => e.CommunityId, "IX_POST_community_id");

            entity.Property(e => e.PostId)
                .HasDefaultValueSql("(newsequentialid())", "DF_POST_post_id")
                .HasColumnName("post_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.CommunityId).HasColumnName("community_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_POST_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.ModerationStatus)
                .HasMaxLength(50)
                .HasColumnName("moderation_status");

            entity.HasOne(d => d.Author).WithMany(p => p.Posts)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POST_author_id");

            entity.HasOne(d => d.Community).WithMany(p => p.Posts)
                .HasForeignKey(d => d.CommunityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POST_community_id");
        });

        modelBuilder.Entity<PostMedium>(entity =>
        {
            entity.HasKey(e => e.MediaId);

            entity.ToTable("POST_MEDIA");

            entity.HasIndex(e => e.PostId, "IX_POST_MEDIA_post_id");

            entity.Property(e => e.MediaId)
                .HasDefaultValueSql("(newsequentialid())", "DF_POST_MEDIA_media_id")
                .HasColumnName("media_id");
            entity.Property(e => e.AlternativeText).HasColumnName("alternative_text");
            entity.Property(e => e.CaptionsStorageKey)
                .HasMaxLength(500)
                .HasColumnName("captions_storage_key");
            entity.Property(e => e.MediaType)
                .HasMaxLength(50)
                .HasColumnName("media_type");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.StorageKey)
                .HasMaxLength(500)
                .HasColumnName("storage_key");
            entity.Property(e => e.Transcript).HasColumnName("transcript");

            entity.HasOne(d => d.Post).WithMany(p => p.PostMedia)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POST_MEDIA_post_id");
        });

        modelBuilder.Entity<PostReaction>(entity =>
        {
            entity.HasKey(e => new { e.PostId, e.UserId });

            entity.ToTable("POST_REACTION");

            entity.HasIndex(e => e.UserId, "IX_POST_REACTION_user_id");

            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ReactionType)
                .HasMaxLength(50)
                .HasColumnName("reaction_type");

            entity.HasOne(d => d.Post).WithMany(p => p.PostReactions)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POST_REACTION_post_id");

            entity.HasOne(d => d.User).WithMany(p => p.PostReactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_POST_REACTION_user_id");
        });

        modelBuilder.Entity<SafetyReport>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.ToTable("SAFETY_REPORT");

            entity.HasIndex(e => e.ReportedUserId, "IX_SAFETY_REPORT_reported_user_id");

            entity.HasIndex(e => e.ReporterId, "IX_SAFETY_REPORT_reporter_id");

            entity.HasIndex(e => e.RequestId, "IX_SAFETY_REPORT_request_id");

            entity.HasIndex(e => e.ReviewerId, "IX_SAFETY_REPORT_reviewer_id");

            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("(newsequentialid())", "DF_SAFETY_REPORT_report_id")
                .HasColumnName("report_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SAFETY_REPORT_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ReportedUserId).HasColumnName("reported_user_id");
            entity.Property(e => e.ReporterId).HasColumnName("reporter_id");
            entity.Property(e => e.RequestId).HasColumnName("request_id");
            entity.Property(e => e.Resolution).HasColumnName("resolution");
            entity.Property(e => e.ResolvedAt)
                .HasPrecision(0)
                .HasColumnName("resolved_at");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");

            entity.HasOne(d => d.ReportedUser).WithMany(p => p.SafetyReportReportedUsers)
                .HasForeignKey(d => d.ReportedUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SAFETY_REPORT_reported_user_id");

            entity.HasOne(d => d.Reporter).WithMany(p => p.SafetyReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SAFETY_REPORT_reporter_id");

            entity.HasOne(d => d.Request).WithMany(p => p.SafetyReports)
                .HasForeignKey(d => d.RequestId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SAFETY_REPORT_request_id");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.SafetyReportReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .HasConstraintName("FK_SAFETY_REPORT_reviewer_id");
        });

        modelBuilder.Entity<ServiceInquiry>(entity =>
        {
            entity.HasKey(e => e.InquiryId);

            entity.ToTable("SERVICE_INQUIRY");

            entity.HasIndex(e => e.ItemId, "IX_SERVICE_INQUIRY_item_id");

            entity.HasIndex(e => e.OrganizationId, "IX_SERVICE_INQUIRY_organization_id");

            entity.HasIndex(e => e.SenderId, "IX_SERVICE_INQUIRY_sender_id");

            entity.Property(e => e.InquiryId)
                .HasDefaultValueSql("(newsequentialid())", "DF_SERVICE_INQUIRY_inquiry_id")
                .HasColumnName("inquiry_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SERVICE_INQUIRY_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.Subject)
                .HasMaxLength(200)
                .HasColumnName("subject");

            entity.HasOne(d => d.Item).WithMany(p => p.ServiceInquiries)
                .HasForeignKey(d => d.ItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SERVICE_INQUIRY_item_id");

            entity.HasOne(d => d.Organization).WithMany(p => p.ServiceInquiries)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SERVICE_INQUIRY_organization_id");

            entity.HasOne(d => d.Sender).WithMany(p => p.ServiceInquiries)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SERVICE_INQUIRY_sender_id");
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.ToTable("SKILL");

            entity.HasIndex(e => e.Name, "UQ_SKILL_name").IsUnique();

            entity.Property(e => e.SkillId).HasColumnName("skill_id");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
        });

        modelBuilder.Entity<SosAlert>(entity =>
        {
            entity.HasKey(e => e.AlertId);

            entity.ToTable("SOS_ALERT");

            entity.HasIndex(e => e.UserId, "IX_SOS_ALERT_user_id");

            entity.Property(e => e.AlertId)
                .HasDefaultValueSql("(newsequentialid())", "DF_SOS_ALERT_alert_id")
                .HasColumnName("alert_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SOS_ALERT_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("longitude");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.RetentionUntil)
                .HasPrecision(0)
                .HasColumnName("retention_until");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.SosAlerts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOS_ALERT_user_id");
        });

        modelBuilder.Entity<SosDelivery>(entity =>
        {
            entity.HasKey(e => e.DeliveryId);

            entity.ToTable("SOS_DELIVERY");

            entity.HasIndex(e => e.AlertId, "IX_SOS_DELIVERY_alert_id");

            entity.HasIndex(e => e.ContactId, "IX_SOS_DELIVERY_contact_id");

            entity.Property(e => e.DeliveryId)
                .HasDefaultValueSql("(newsequentialid())", "DF_SOS_DELIVERY_delivery_id")
                .HasColumnName("delivery_id");
            entity.Property(e => e.AlertId).HasColumnName("alert_id");
            entity.Property(e => e.AttemptedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_SOS_DELIVERY_attempted_at")
                .HasColumnName("attempted_at");
            entity.Property(e => e.ContactId).HasColumnName("contact_id");
            entity.Property(e => e.DeliveryStatus)
                .HasMaxLength(50)
                .HasColumnName("delivery_status");

            entity.HasOne(d => d.Alert).WithMany(p => p.SosDeliveries)
                .HasForeignKey(d => d.AlertId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOS_DELIVERY_alert_id");

            entity.HasOne(d => d.Contact).WithMany(p => p.SosDeliveries)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SOS_DELIVERY_contact_id");
        });

        modelBuilder.Entity<TrainingAttempt>(entity =>
        {
            entity.HasKey(e => e.AttemptId);

            entity.ToTable("TRAINING_ATTEMPT");

            entity.HasIndex(e => e.ModuleId, "IX_TRAINING_ATTEMPT_module_id");

            entity.HasIndex(e => e.VolunteerId, "IX_TRAINING_ATTEMPT_volunteer_id");

            entity.Property(e => e.AttemptId)
                .HasDefaultValueSql("(newsequentialid())", "DF_TRAINING_ATTEMPT_attempt_id")
                .HasColumnName("attempt_id");
            entity.Property(e => e.CompletedAt)
                .HasPrecision(0)
                .HasColumnName("completed_at");
            entity.Property(e => e.ModuleId).HasColumnName("module_id");
            entity.Property(e => e.Result)
                .HasMaxLength(50)
                .HasColumnName("result");
            entity.Property(e => e.Score)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("score");
            entity.Property(e => e.VolunteerId).HasColumnName("volunteer_id");

            entity.HasOne(d => d.Module).WithMany(p => p.TrainingAttempts)
                .HasForeignKey(d => d.ModuleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TRAINING_ATTEMPT_module_id");

            entity.HasOne(d => d.Volunteer).WithMany(p => p.TrainingAttempts)
                .HasForeignKey(d => d.VolunteerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TRAINING_ATTEMPT_volunteer_id");
        });

        modelBuilder.Entity<TrainingModule>(entity =>
        {
            entity.HasKey(e => e.ModuleId);

            entity.ToTable("TRAINING_MODULE");

            entity.Property(e => e.ModuleId)
                .HasDefaultValueSql("(newsequentialid())", "DF_TRAINING_MODULE_module_id")
                .HasColumnName("module_id");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.Version)
                .HasMaxLength(50)
                .HasColumnName("version");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("USER_ACCOUNT");

            entity.HasIndex(e => e.RoleId, "IX_USER_ACCOUNT_role_id");

            entity.HasIndex(e => e.Email, "UQ_USER_ACCOUNT_email").IsUnique();

            entity.HasIndex(e => e.Phone, "UX_USER_ACCOUNT_phone")
                .IsUnique()
                .HasFilter("([phone] IS NOT NULL)");

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("(newsequentialid())", "DF_USER_ACCOUNT_user_id")
                .HasColumnName("user_id");
            entity.Property(e => e.AccountStatus)
                .HasMaxLength(50)
                .HasColumnName("account_status");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_USER_ACCOUNT_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerified).HasColumnName("email_verified");
            entity.Property(e => e.Phone)
                .HasMaxLength(30)
                .HasColumnName("phone");
            entity.Property(e => e.PhoneVerified).HasColumnName("phone_verified");
            entity.Property(e => e.RoleId).HasColumnName("role_id");

            entity.HasOne(d => d.Role).WithMany(p => p.UserAccounts)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_ACCOUNT_role_id");

            entity.HasMany(d => d.Interests).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserInterest",
                    r => r.HasOne<Interest>().WithMany()
                        .HasForeignKey("InterestId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_USER_INTEREST_interest_id"),
                    l => l.HasOne<UserAccount>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_USER_INTEREST_user_id"),
                    j =>
                    {
                        j.HasKey("UserId", "InterestId");
                        j.ToTable("USER_INTEREST");
                        j.HasIndex(new[] { "InterestId" }, "IX_USER_INTEREST_interest_id");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<int>("InterestId").HasColumnName("interest_id");
                    });

            entity.HasMany(d => d.Skills).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserSkill",
                    r => r.HasOne<Skill>().WithMany()
                        .HasForeignKey("SkillId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_USER_SKILL_skill_id"),
                    l => l.HasOne<UserAccount>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_USER_SKILL_user_id"),
                    j =>
                    {
                        j.HasKey("UserId", "SkillId");
                        j.ToTable("USER_SKILL");
                        j.HasIndex(new[] { "SkillId" }, "IX_USER_SKILL_skill_id");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<int>("SkillId").HasColumnName("skill_id");
                    });
        });

        modelBuilder.Entity<UserFollow>(entity =>
        {
            entity.HasKey(e => new { e.FollowerId, e.FollowedId });

            entity.ToTable("USER_FOLLOW");

            entity.HasIndex(e => e.FollowedId, "IX_USER_FOLLOW_followed_id");

            entity.Property(e => e.FollowerId).HasColumnName("follower_id");
            entity.Property(e => e.FollowedId).HasColumnName("followed_id");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_USER_FOLLOW_created_at")
                .HasColumnName("created_at");

            entity.HasOne(d => d.Followed).WithMany(p => p.UserFollowFolloweds)
                .HasForeignKey(d => d.FollowedId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_FOLLOW_followed_id");

            entity.HasOne(d => d.Follower).WithMany(p => p.UserFollowFollowers)
                .HasForeignKey(d => d.FollowerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_FOLLOW_follower_id");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("USER_PROFILE");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.AvatarStorageKey)
                .HasMaxLength(500)
                .HasColumnName("avatar_storage_key");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.City)
                .HasMaxLength(200)
                .HasColumnName("city");
            entity.Property(e => e.DisplayName)
                .HasMaxLength(200)
                .HasColumnName("display_name");
            entity.Property(e => e.Visibility)
                .HasMaxLength(50)
                .HasColumnName("visibility");

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USER_PROFILE_user_id");
        });

        modelBuilder.Entity<VerificationEvidence>(entity =>
        {
            entity.HasKey(e => e.EvidenceId);

            entity.ToTable("VERIFICATION_EVIDENCE");

            entity.HasIndex(e => e.ReviewId, "IX_VERIFICATION_EVIDENCE_review_id");

            entity.Property(e => e.EvidenceId)
                .HasDefaultValueSql("(newsequentialid())", "DF_VERIFICATION_EVIDENCE_evidence_id")
                .HasColumnName("evidence_id");
            entity.Property(e => e.EncryptedStorageKey)
                .HasMaxLength(500)
                .HasColumnName("encrypted_storage_key");
            entity.Property(e => e.EvidenceType)
                .HasMaxLength(50)
                .HasColumnName("evidence_type");
            entity.Property(e => e.RetentionUntil)
                .HasPrecision(0)
                .HasColumnName("retention_until");
            entity.Property(e => e.ReviewId).HasColumnName("review_id");

            entity.HasOne(d => d.Review).WithMany(p => p.VerificationEvidences)
                .HasForeignKey(d => d.ReviewId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VERIFICATION_EVIDENCE_review_id");
        });

        modelBuilder.Entity<VerificationReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId);

            entity.ToTable("VERIFICATION_REVIEW");

            entity.HasIndex(e => e.ReviewerId, "IX_VERIFICATION_REVIEW_reviewer_id");

            entity.HasIndex(e => e.VolunteerId, "IX_VERIFICATION_REVIEW_volunteer_id");

            entity.HasIndex(e => e.InterviewSlotId, "UX_VERIFICATION_REVIEW_interview_slot")
                .IsUnique()
                .HasFilter("([interview_slot_id] IS NOT NULL)");

            entity.Property(e => e.ReviewId)
                .HasDefaultValueSql("(newsequentialid())", "DF_VERIFICATION_REVIEW_review_id")
                .HasColumnName("review_id");
            entity.Property(e => e.DecidedAt)
                .HasPrecision(0)
                .HasColumnName("decided_at");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())", "DF_VERIFICATION_REVIEW_created_at")
                .HasColumnName("created_at");
            entity.Property(e => e.InterviewAt)
                .HasPrecision(0)
                .HasColumnName("interview_at");
            entity.Property(e => e.InterviewSlotId).HasColumnName("interview_slot_id");
            entity.Property(e => e.ReviewerId).HasColumnName("reviewer_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.VerificationLevel)
                .HasMaxLength(50)
                .HasColumnName("verification_level");
            entity.Property(e => e.VolunteerId).HasColumnName("volunteer_id");

            entity.HasOne(d => d.InterviewSlot).WithOne(p => p.VerificationReview)
                .HasForeignKey<VerificationReview>(d => d.InterviewSlotId)
                .HasConstraintName("FK_VERIFICATION_REVIEW_interview_slot_id");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.VerificationReviews)
                .HasForeignKey(d => d.ReviewerId)
                .HasConstraintName("FK_VERIFICATION_REVIEW_reviewer_id");

            entity.HasOne(d => d.Volunteer).WithMany(p => p.VerificationReviews)
                .HasForeignKey(d => d.VolunteerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VERIFICATION_REVIEW_volunteer_id");
        });

        modelBuilder.Entity<VolunteerProfile>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("VOLUNTEER_PROFILE");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.ExperienceNotes).HasColumnName("experience_notes");
            entity.Property(e => e.Governorate)
                .HasMaxLength(100)
                .HasColumnName("governorate");
            entity.Property(e => e.HelpMode)
                .HasMaxLength(50)
                .HasColumnName("help_mode");
            entity.Property(e => e.Introduction).HasColumnName("introduction");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(9, 6)")
                .HasColumnName("longitude");
            entity.Property(e => e.ServiceArea)
                .HasMaxLength(200)
                .HasColumnName("service_area");
            entity.Property(e => e.ServiceRadiusKm).HasColumnName("service_radius_km");
            entity.Property(e => e.SkillLevel)
                .HasMaxLength(50)
                .HasColumnName("skill_level");
            entity.Property(e => e.VerificationStatus)
                .HasMaxLength(50)
                .HasColumnName("verification_status");
            entity.Property(e => e.WeeklyHours).HasColumnName("weekly_hours");

            entity.HasOne(d => d.User).WithOne(p => p.VolunteerProfile)
                .HasForeignKey<VolunteerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOLUNTEER_PROFILE_user_id");

            entity.HasMany(d => d.Skills).WithMany(p => p.Volunteers)
                .UsingEntity<Dictionary<string, object>>(
                    "VolunteerSkill",
                    r => r.HasOne<Skill>().WithMany()
                        .HasForeignKey("SkillId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_VOLUNTEER_SKILL_skill_id"),
                    l => l.HasOne<VolunteerProfile>().WithMany()
                        .HasForeignKey("VolunteerId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_VOLUNTEER_SKILL_volunteer_id"),
                    j =>
                    {
                        j.HasKey("VolunteerId", "SkillId");
                        j.ToTable("VOLUNTEER_SKILL");
                        j.HasIndex(new[] { "SkillId" }, "IX_VOLUNTEER_SKILL_skill_id");
                        j.IndexerProperty<Guid>("VolunteerId").HasColumnName("volunteer_id");
                        j.IndexerProperty<int>("SkillId").HasColumnName("skill_id");
                    });
        });

        modelBuilder.Entity<VolunteerSocialLink>(entity =>
        {
            entity.HasKey(e => new { e.VolunteerId, e.Platform });

            entity.ToTable("VOLUNTEER_SOCIAL_LINK");

            entity.Property(e => e.VolunteerId).HasColumnName("volunteer_id");
            entity.Property(e => e.Platform)
                .HasMaxLength(50)
                .HasColumnName("platform");
            entity.Property(e => e.Url)
                .HasMaxLength(500)
                .HasColumnName("url");

            entity.HasOne(d => d.Volunteer).WithMany(p => p.VolunteerSocialLinks)
                .HasForeignKey(d => d.VolunteerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VOLUNTEER_SOCIAL_LINK_volunteer_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
