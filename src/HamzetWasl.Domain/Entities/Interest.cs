using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Interest
{
    public int InterestId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<UserAccount> Users { get; set; } = new List<UserAccount>();
}
