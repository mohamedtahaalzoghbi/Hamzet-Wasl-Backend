using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class PostMedium
{
    public Guid MediaId { get; set; }

    public Guid PostId { get; set; }

    public string MediaType { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string? AlternativeText { get; set; }

    public string? Transcript { get; set; }

    public string? CaptionsStorageKey { get; set; }

    public virtual Post Post { get; set; } = null!;
}
