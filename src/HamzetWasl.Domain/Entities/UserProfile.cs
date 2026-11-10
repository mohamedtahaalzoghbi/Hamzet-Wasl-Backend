using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class UserProfile
{
    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Bio { get; set; }

    public string? City { get; set; }

    public string? AvatarStorageKey { get; set; }

    public string Visibility { get; set; } = null!;

    public virtual UserAccount User { get; set; } = null!;
}
