using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class ObstacleReport
{
    public Guid ObstacleId { get; set; }

    public Guid ReporterId { get; set; }

    public Guid? PlaceId { get; set; }

    public string Description { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ReportedAt { get; set; }

    public virtual Place? Place { get; set; }

    public virtual UserAccount Reporter { get; set; } = null!;
}
