using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class FacilityType
{
    public int FacilityTypeId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<PlaceFacility> PlaceFacilities { get; set; } = new List<PlaceFacility>();
}
