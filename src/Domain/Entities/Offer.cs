namespace Domain.Entities;

public class Offer 
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DiscountPercentage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OfferProduct> OfferProducts { get; set; } = new List<OfferProduct>();
}
