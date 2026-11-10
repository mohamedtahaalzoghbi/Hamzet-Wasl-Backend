using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class AvailabilitySlot
{
    public Guid SlotId { get; set; }

    public Guid VolunteerId { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string Timezone { get; set; } = null!;

    public virtual VolunteerProfile Volunteer { get; set; } = null!;
}
