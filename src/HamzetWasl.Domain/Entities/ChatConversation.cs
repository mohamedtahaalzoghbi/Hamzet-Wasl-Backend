using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class ChatConversation
{
    public Guid ConversationId { get; set; }

    public Guid UserLowId { get; set; }

    public Guid UserHighId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual UserAccount UserHigh { get; set; } = null!;

    public virtual UserAccount UserLow { get; set; } = null!;
}
