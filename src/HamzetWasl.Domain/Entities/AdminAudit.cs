using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class AdminAudit
{
    public Guid AuditId { get; set; }

    public Guid ActorUserId { get; set; }

    public string Action { get; set; } = null!;

    public string TargetType { get; set; } = null!;

    public Guid TargetId { get; set; }

    public string Outcome { get; set; } = null!;

    public DateTime OccurredAt { get; set; }

    public virtual UserAccount ActorUser { get; set; } = null!;
}
