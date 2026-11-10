using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class InterviewSlot
{
    public Guid InterviewSlotId { get; set; }

    public Guid ReviewerId { get; set; }

    public DateTime StartsAt { get; set; }

    public int DurationMinutes { get; set; }

    public string Status { get; set; } = null!;

    public virtual UserAccount Reviewer { get; set; } = null!;

    public virtual VerificationReview? VerificationReview { get; set; }
}
