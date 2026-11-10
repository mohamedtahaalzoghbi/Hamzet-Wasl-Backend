using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class HelpAssignment
{
    public Guid RequestId { get; set; }

    public Guid OfferId { get; set; }

    public DateTime? RequesterAcceptedAt { get; set; }

    public DateTime? VolunteerAcceptedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual HelpConversation? HelpConversation { get; set; }

    public virtual HelpOffer Offer { get; set; } = null!;

    public virtual HelpRequest Request { get; set; } = null!;
}
