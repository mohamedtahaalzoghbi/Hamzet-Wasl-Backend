using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class ConsentRecord
{
    public Guid ConsentId { get; set; }

    public Guid UserId { get; set; }

    public string Purpose { get; set; } = null!;

    public string PolicyVersion { get; set; } = null!;

    public DateTime GrantedAt { get; set; }

    public DateTime? WithdrawnAt { get; set; }

    public virtual ICollection<AccessibilityProfile> AccessibilityProfiles { get; set; } = new List<AccessibilityProfile>();

    public virtual UserAccount User { get; set; } = null!;
}
