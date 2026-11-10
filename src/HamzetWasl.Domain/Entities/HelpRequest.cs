using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class HelpRequest
{
    public Guid RequestId { get; set; }

    public Guid RequesterId { get; set; }

    public int? SkillId { get; set; }

    public string HelpMode { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string? Description { get; set; }

    public string? VoiceStorageKey { get; set; }

    public string ApproximateArea { get; set; } = null!;

    public string Urgency { get; set; } = null!;

    public DateTime? ScheduledAt { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual HelpAssignment? HelpAssignment { get; set; }

    public virtual ICollection<HelpFeedback> HelpFeedbacks { get; set; } = new List<HelpFeedback>();

    public virtual ICollection<HelpOffer> HelpOffers { get; set; } = new List<HelpOffer>();

    public virtual UserAccount Requester { get; set; } = null!;

    public virtual ICollection<SafetyReport> SafetyReports { get; set; } = new List<SafetyReport>();

    public virtual Skill? Skill { get; set; }
}
