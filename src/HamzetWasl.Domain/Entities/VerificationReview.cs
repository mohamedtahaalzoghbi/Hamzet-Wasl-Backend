using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class VerificationReview
{
    public Guid ReviewId { get; set; }

    public Guid VolunteerId { get; set; }

    public Guid? ReviewerId { get; set; }

    public string VerificationLevel { get; set; } = null!;

    public string Status { get; set; } = null!;

    public Guid? InterviewSlotId { get; set; }

    public DateTime? InterviewAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public virtual InterviewSlot? InterviewSlot { get; set; }

    public virtual UserAccount? Reviewer { get; set; }

    public virtual ICollection<VerificationEvidence> VerificationEvidences { get; set; } = new List<VerificationEvidence>();

    public virtual VolunteerProfile Volunteer { get; set; } = null!;
}
