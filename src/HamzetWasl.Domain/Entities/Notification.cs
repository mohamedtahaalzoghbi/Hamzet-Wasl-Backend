using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Notification
{
    public Guid NotificationId { get; set; }

    public Guid RecipientId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Channel { get; set; } = null!;

    public string? TextBody { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual UserAccount Recipient { get; set; } = null!;
}
