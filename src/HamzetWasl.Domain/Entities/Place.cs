using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Place
{
    public Guid PlaceId { get; set; }

    public string Name { get; set; } = null!;

    public string Address { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    public string VerificationSource { get; set; } = null!;

    public virtual ICollection<ObstacleReport> ObstacleReports { get; set; } = new List<ObstacleReport>();

    public virtual ICollection<PlaceFacility> PlaceFacilities { get; set; } = new List<PlaceFacility>();

    public virtual ICollection<PlaceReview> PlaceReviews { get; set; } = new List<PlaceReview>();
}
