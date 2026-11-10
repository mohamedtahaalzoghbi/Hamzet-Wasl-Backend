using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class SosDelivery
{
    public Guid DeliveryId { get; set; }

    public Guid AlertId { get; set; }

    public Guid ContactId { get; set; }

    public string DeliveryStatus { get; set; } = null!;

    public DateTime AttemptedAt { get; set; }

    public virtual SosAlert Alert { get; set; } = null!;

    public virtual EmergencyContact Contact { get; set; } = null!;
}
