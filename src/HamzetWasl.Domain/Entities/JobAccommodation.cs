using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class JobAccommodation
{
    public Guid JobId { get; set; }

    public int AccommodationId { get; set; }

    public bool Active { get; set; }

    public virtual Accommodation Accommodation { get; set; } = null!;

    public virtual Job Job { get; set; } = null!;
}
