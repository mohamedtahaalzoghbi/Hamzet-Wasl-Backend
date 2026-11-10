using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class OrganizationReview
{
    public Guid ReviewId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid? ReviewerId { get; set; }

    public string Status { get; set; } = null!;

    public string? DecisionNotes { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual UserAccount? Reviewer { get; set; }
}
