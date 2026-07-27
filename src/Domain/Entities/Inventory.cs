namespace Domain.Entities;

public class Inventory {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
