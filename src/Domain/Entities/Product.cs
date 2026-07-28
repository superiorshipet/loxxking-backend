namespace Domain.Entities;

public class Product {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Description { get; set; } = default!;
    public List<string> Images { get; set; } = new();

    /// <summary>Newline-separated list of feature bullet points.</summary>
    public string? Features { get; set; }

    /// <summary>Shipping policy text shown in the product detail tab.</summary>
    public string? ShippingPolicy { get; set; }

    /// <summary>Return &amp; refund policy text shown in the product detail tab.</summary>
    public string? ReturnPolicy { get; set; }

    public decimal BasePrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
