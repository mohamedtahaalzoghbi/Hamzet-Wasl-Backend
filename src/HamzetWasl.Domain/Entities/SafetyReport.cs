using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class SafetyReport
{
    public Guid ReportId { get; set; }

    public Guid ReporterId { get; set; }

    public Guid ReportedUserId { get; set; }

    public Guid RequestId { get; set; }

    public Guid? ReviewerId { get; set; }

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Resolution { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual UserAccount ReportedUser { get; set; } = null!;

    public virtual UserAccount Reporter { get; set; } = null!;

    public virtual HelpRequest Request { get; set; } = null!;

    public virtual UserAccount? Reviewer { get; set; }
}
