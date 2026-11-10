using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class NeedType
{
    public int NeedTypeId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<AccessibilityProfile> AccessibilityProfiles { get; set; } = new List<AccessibilityProfile>();
}
