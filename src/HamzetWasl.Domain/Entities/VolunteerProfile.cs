using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class VolunteerProfile
{
    public Guid UserId { get; set; }

    public string? Introduction { get; set; }

    public string HelpMode { get; set; } = null!;

    public string ServiceArea { get; set; } = null!;

    public string? Governorate { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public int? ServiceRadiusKm { get; set; }

    public string SkillLevel { get; set; } = null!;

    public string? ExperienceNotes { get; set; }

    public int WeeklyHours { get; set; }

    public string VerificationStatus { get; set; } = null!;

    public virtual ICollection<AvailabilitySlot> AvailabilitySlots { get; set; } = new List<AvailabilitySlot>();

    public virtual ICollection<HelpOffer> HelpOffers { get; set; } = new List<HelpOffer>();

    public virtual ICollection<TrainingAttempt> TrainingAttempts { get; set; } = new List<TrainingAttempt>();

    public virtual UserAccount User { get; set; } = null!;

    public virtual ICollection<VerificationReview> VerificationReviews { get; set; } = new List<VerificationReview>();

    public virtual ICollection<VolunteerSocialLink> VolunteerSocialLinks { get; set; } = new List<VolunteerSocialLink>();

    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
