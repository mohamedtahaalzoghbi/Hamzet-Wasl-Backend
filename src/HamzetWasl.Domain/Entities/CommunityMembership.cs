using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class CommunityMembership
{
    public Guid CommunityId { get; set; }

    public Guid UserId { get; set; }

    public DateTime JoinedAt { get; set; }

    public virtual Community Community { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
