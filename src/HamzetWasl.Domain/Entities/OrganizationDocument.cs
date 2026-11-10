using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class OrganizationDocument
{
    public Guid DocumentId { get; set; }

    public Guid OrganizationId { get; set; }

    public string DocumentType { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public DateTime UploadedAt { get; set; }

    public virtual Organization Organization { get; set; } = null!;
}
