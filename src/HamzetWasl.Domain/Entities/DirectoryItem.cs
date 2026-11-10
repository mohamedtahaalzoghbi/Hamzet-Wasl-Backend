using System;
using System.Collections.Generic;

namespace HamzetWasl.Domain.Entities;

public partial class DirectoryItem
{
    public Guid ItemId { get; set; }

    public Guid OrganizationId { get; set; }

    public string ItemType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string PublicationStatus { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<ServiceInquiry> ServiceInquiries { get; set; } = new List<ServiceInquiry>();
}
