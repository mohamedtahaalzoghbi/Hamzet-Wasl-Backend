using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Post
{
    public Guid PostId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid CommunityId { get; set; }

    public string Body { get; set; } = null!;

    public string ModerationStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual UserAccount Author { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual Community Community { get; set; } = null!;

    public virtual ICollection<ContentReport> ContentReports { get; set; } = new List<ContentReport>();

    public virtual ICollection<PostMedium> PostMedia { get; set; } = new List<PostMedium>();

    public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();
}
