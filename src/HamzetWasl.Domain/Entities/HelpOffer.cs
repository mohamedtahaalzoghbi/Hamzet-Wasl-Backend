using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class HelpOffer
{
    public Guid OfferId { get; set; }

    public Guid RequestId { get; set; }

    public Guid VolunteerId { get; set; }

    public string? Message { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual HelpAssignment? HelpAssignment { get; set; }

    public virtual HelpRequest Request { get; set; } = null!;

    public virtual VolunteerProfile Volunteer { get; set; } = null!;
}
