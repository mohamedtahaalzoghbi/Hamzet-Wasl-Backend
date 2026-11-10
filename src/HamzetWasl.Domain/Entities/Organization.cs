using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Organization
{
    public Guid OrganizationId { get; set; }

    public Guid OwnerUserId { get; set; }

    public string OrganizationType { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Field { get; set; }

    public string? Description { get; set; }

    public string? Governorate { get; set; }

    public string? City { get; set; }

    public string? Website { get; set; }

    public string? LogoStorageKey { get; set; }

    public string? EmployeeCountRange { get; set; }

    public string? CoverStorageKey { get; set; }

    public string? ContactInformation { get; set; }

    public string ApprovalStatus { get; set; } = null!;

    public virtual ICollection<DirectoryItem> DirectoryItems { get; set; } = new List<DirectoryItem>();

    public virtual ICollection<Job> Jobs { get; set; } = new List<Job>();

    public virtual ICollection<OrganizationDocument> OrganizationDocuments { get; set; } = new List<OrganizationDocument>();

    public virtual ICollection<OrganizationReview> OrganizationReviews { get; set; } = new List<OrganizationReview>();

    public virtual UserAccount OwnerUser { get; set; } = null!;

    public virtual ICollection<ServiceInquiry> ServiceInquiries { get; set; } = new List<ServiceInquiry>();
}
