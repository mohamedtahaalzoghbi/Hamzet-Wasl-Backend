using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class Skill
{
    public int SkillId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<HelpRequest> HelpRequests { get; set; } = new List<HelpRequest>();

    public virtual ICollection<UserAccount> Users { get; set; } = new List<UserAccount>();

    public virtual ICollection<VolunteerProfile> Volunteers { get; set; } = new List<VolunteerProfile>();
}
