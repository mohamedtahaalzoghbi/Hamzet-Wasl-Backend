using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class InquiryMessage
{
    public Guid MessageId { get; set; }

    public Guid InquiryId { get; set; }

    public Guid SenderId { get; set; }

    public string Body { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public virtual ServiceInquiry Inquiry { get; set; } = null!;

    public virtual UserAccount Sender { get; set; } = null!;
}
