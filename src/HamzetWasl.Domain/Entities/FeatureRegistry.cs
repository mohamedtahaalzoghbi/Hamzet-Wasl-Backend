using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class FeatureRegistry
{
    public string FeatureKey { get; set; } = null!;

    public string DeliveryPhase { get; set; } = null!;

    public string ImplementationStatus { get; set; } = null!;
}
