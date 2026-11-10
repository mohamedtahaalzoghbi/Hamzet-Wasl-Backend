using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class ServiceInquiry
{
    public Guid InquiryId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid ItemId { get; set; }

    public Guid SenderId { get; set; }

    public string Subject { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<InquiryMessage> InquiryMessages { get; set; } = new List<InquiryMessage>();

    public virtual DirectoryItem Item { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;

    public virtual UserAccount Sender { get; set; } = null!;
}
