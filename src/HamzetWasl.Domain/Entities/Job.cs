using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Job
{
    public Guid JobId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string EmploymentType { get; set; } = null!;

    public string WorkArrangement { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string AccessibilityInformation { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime PublishedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual ICollection<JobAccommodation> JobAccommodations { get; set; } = new List<JobAccommodation>();

    public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();

    public virtual Organization Organization { get; set; } = null!;
}
