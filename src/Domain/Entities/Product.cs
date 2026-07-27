namespace Domain.Entities;

public class Product {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal BasePrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
