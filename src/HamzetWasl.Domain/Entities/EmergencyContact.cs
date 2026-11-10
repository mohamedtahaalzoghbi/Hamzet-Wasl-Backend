using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class EmergencyContact
{
    public Guid ContactId { get; set; }

    public Guid OwnerUserId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string ProtectedContactDestination { get; set; } = null!;

    public virtual UserAccount OwnerUser { get; set; } = null!;

    public virtual ICollection<SosDelivery> SosDeliveries { get; set; } = new List<SosDelivery>();
}
