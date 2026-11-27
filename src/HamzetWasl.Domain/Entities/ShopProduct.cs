namespace HamzetWasl.Domain.Entities;

public partial class ShopProduct
{
    public Guid ProductId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public bool Available { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual Organization Organization { get; set; } = null!;
}
