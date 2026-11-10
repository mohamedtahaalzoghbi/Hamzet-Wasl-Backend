using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class UserFollow
{
    public Guid FollowerId { get; set; }

    public Guid FollowedId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UserAccount Followed { get; set; } = null!;

    public virtual UserAccount Follower { get; set; } = null!;
}
