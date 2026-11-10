using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class TrainingModule
{
    public Guid ModuleId { get; set; }

    public string Title { get; set; } = null!;

    public string Version { get; set; } = null!;

    public virtual ICollection<TrainingAttempt> TrainingAttempts { get; set; } = new List<TrainingAttempt>();
}
