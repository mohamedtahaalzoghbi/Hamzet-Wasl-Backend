using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class ApplicationStatusChange
{
    public Guid ChangeId { get; set; }

    public Guid ApplicationId { get; set; }

    public Guid ActorUserId { get; set; }

    public string PreviousStatus { get; set; } = null!;

    public string NewStatus { get; set; } = null!;

    public DateTime ChangedAt { get; set; }

    public virtual UserAccount ActorUser { get; set; } = null!;

    public virtual JobApplication Application { get; set; } = null!;
}
