using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class HelpConversation
{
    public Guid ConversationId { get; set; }

    public Guid RequestId { get; set; }

    public DateTime OpenedAt { get; set; }

    public virtual ICollection<HelpMessage> HelpMessages { get; set; } = new List<HelpMessage>();

    public virtual HelpAssignment Request { get; set; } = null!;
}
