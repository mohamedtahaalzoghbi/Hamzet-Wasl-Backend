using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class AssistantLog
{
    public Guid LogId { get; set; }

    public Guid UserId { get; set; }

    public string Tool { get; set; } = null!;

    public bool Refused { get; set; }

    public decimal? Confidence { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
