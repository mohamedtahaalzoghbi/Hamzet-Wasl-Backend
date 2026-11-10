using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Accommodation
{
    public int AccommodationId { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public virtual ICollection<JobAccommodation> JobAccommodations { get; set; } = new List<JobAccommodation>();
}
