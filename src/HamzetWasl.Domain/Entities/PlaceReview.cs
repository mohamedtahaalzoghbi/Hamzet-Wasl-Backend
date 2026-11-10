using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class PlaceReview
{
    public Guid ReviewId { get; set; }

    public Guid PlaceId { get; set; }

    public Guid UserId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Place Place { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
