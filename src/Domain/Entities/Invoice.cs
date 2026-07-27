namespace Domain.Entities;

public class Invoice {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string InvoiceNumber { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
