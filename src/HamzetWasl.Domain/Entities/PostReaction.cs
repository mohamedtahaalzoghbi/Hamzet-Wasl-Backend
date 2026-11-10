using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class PostReaction
{
    public Guid PostId { get; set; }

    public Guid UserId { get; set; }

    public string ReactionType { get; set; } = null!;

    public virtual Post Post { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
