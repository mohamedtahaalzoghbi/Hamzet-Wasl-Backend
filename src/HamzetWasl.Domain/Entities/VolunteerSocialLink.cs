using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class VolunteerSocialLink
{
    public Guid VolunteerId { get; set; }

    public string Platform { get; set; } = null!;

    public string Url { get; set; } = null!;

    public virtual VolunteerProfile Volunteer { get; set; } = null!;
}
