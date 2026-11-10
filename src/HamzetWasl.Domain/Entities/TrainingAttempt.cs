using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class TrainingAttempt
{
    public Guid AttemptId { get; set; }

    public Guid VolunteerId { get; set; }

    public Guid ModuleId { get; set; }

    public decimal? Score { get; set; }

    public string? Result { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual TrainingModule Module { get; set; } = null!;

    public virtual VolunteerProfile Volunteer { get; set; } = null!;
}
