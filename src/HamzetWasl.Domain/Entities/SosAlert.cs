using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class SosAlert
{
    public Guid AlertId { get; set; }

    public Guid UserId { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime RetentionUntil { get; set; }

    public virtual ICollection<SosDelivery> SosDeliveries { get; set; } = new List<SosDelivery>();

    public virtual UserAccount User { get; set; } = null!;
}
