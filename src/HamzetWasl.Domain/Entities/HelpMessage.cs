using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class HelpMessage
{
    public Guid MessageId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string? TextBody { get; set; }

    public string? AudioStorageKey { get; set; }

    public DateTime SentAt { get; set; }

    public virtual HelpConversation Conversation { get; set; } = null!;

    public virtual UserAccount Sender { get; set; } = null!;
}
