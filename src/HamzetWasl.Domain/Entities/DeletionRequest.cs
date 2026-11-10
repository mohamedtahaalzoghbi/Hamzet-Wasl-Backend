using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class DeletionRequest
{
    public Guid DeletionRequestId { get; set; }

    public Guid UserId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
