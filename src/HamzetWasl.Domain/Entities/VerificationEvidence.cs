using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class VerificationEvidence
{
    public Guid EvidenceId { get; set; }

    public Guid ReviewId { get; set; }

    public string EvidenceType { get; set; } = null!;

    public string EncryptedStorageKey { get; set; } = null!;

    public DateTime RetentionUntil { get; set; }

    public virtual VerificationReview Review { get; set; } = null!;
}
