using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class JobApplication
{
    public Guid ApplicationId { get; set; }

    public Guid JobId { get; set; }

    public Guid ApplicantId { get; set; }

    public string ApplicationText { get; set; } = null!;

    public string? CvStorageKey { get; set; }

    public string? VoluntarilySharedNeeds { get; set; }

    public string Status { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public virtual UserAccount Applicant { get; set; } = null!;

    public virtual ICollection<ApplicationStatusChange> ApplicationStatusChanges { get; set; } = new List<ApplicationStatusChange>();

    public virtual Job Job { get; set; } = null!;
}
