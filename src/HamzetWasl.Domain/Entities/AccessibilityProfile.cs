using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class AccessibilityProfile
{
    public Guid UserId { get; set; }

    public int NeedTypeId { get; set; }

    public Guid ConsentId { get; set; }

    public string Theme { get; set; } = null!;

    public decimal FontScale { get; set; }

    public bool ReducedMotion { get; set; }

    public string InteractionMode { get; set; } = null!;

    public string NotificationMode { get; set; } = null!;

    public string? ManualOverrides { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ConsentRecord Consent { get; set; } = null!;

    public virtual NeedType NeedType { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
