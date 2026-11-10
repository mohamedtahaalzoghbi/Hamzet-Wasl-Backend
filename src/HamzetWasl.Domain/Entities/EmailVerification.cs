using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class EmailVerification
{
    public Guid VerificationId { get; set; }

    public Guid UserId { get; set; }

    public string CodeHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
