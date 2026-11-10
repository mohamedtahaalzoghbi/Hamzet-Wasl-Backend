using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class PlaceFacility
{
    public Guid PlaceId { get; set; }

    public int FacilityTypeId { get; set; }

    public string AvailabilityStatus { get; set; } = null!;

    public string? Notes { get; set; }

    public virtual FacilityType FacilityType { get; set; } = null!;

    public virtual Place Place { get; set; } = null!;
}
