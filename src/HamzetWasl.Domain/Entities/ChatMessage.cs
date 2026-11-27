using System;

namespace HamzetWasl.Domain.Entities;

public partial class ChatMessage
{
    public Guid MessageId { get; set; }

    public Guid ConversationId { get; set; }

    public Guid SenderId { get; set; }

    public string Body { get; set; } = null!;

    public DateTime SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual ChatConversation Conversation { get; set; } = null!;

    public virtual UserAccount Sender { get; set; } = null!;
}
