using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Community
{
    public Guid CommunityId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Rules { get; set; } = null!;

    public virtual ICollection<CommunityMembership> CommunityMemberships { get; set; } = new List<CommunityMembership>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
