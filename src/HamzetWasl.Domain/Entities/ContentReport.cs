using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class ContentReport
{
    public Guid ReportId { get; set; }

    public Guid ReporterId { get; set; }

    public Guid? PostId { get; set; }

    public Guid? CommentId { get; set; }

    public Guid? ReviewerId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Resolution { get; set; }

    public virtual Comment? Comment { get; set; }

    public virtual Post? Post { get; set; }

    public virtual UserAccount Reporter { get; set; } = null!;

    public virtual UserAccount? Reviewer { get; set; }
}
