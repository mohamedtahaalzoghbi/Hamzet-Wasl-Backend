using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Comment
{
    public Guid CommentId { get; set; }

    public Guid PostId { get; set; }

    public Guid AuthorId { get; set; }

    public string Body { get; set; } = null!;

    public string ModerationStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual UserAccount Author { get; set; } = null!;

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual Post Post { get; set; } = null!;
}
