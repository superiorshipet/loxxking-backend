using Domain.Enums;

namespace Domain.Entities;

public class OfferProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OfferId { get; set; }
    public Offer Offer { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
