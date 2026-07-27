namespace Domain.Entities;

public class ProductPrice {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public decimal Price { get; set; }
}
