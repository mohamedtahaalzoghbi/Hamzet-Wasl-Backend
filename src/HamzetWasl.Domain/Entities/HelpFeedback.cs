using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class HelpFeedback
{
    public Guid FeedbackId { get; set; }

    public Guid RequestId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid RatedUserId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UserAccount Author { get; set; } = null!;

    public virtual UserAccount RatedUser { get; set; } = null!;

    public virtual HelpRequest Request { get; set; } = null!;
}
