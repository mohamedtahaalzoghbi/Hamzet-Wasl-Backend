using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class AccountRole
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<UserAccount> UserAccounts { get; set; } = new List<UserAccount>();
}
