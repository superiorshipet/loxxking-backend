using Domain.Enums;

namespace Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;

    // Guest fields (for non-logged-in customers)
    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public string GuestAddress { get; set; } = string.Empty;

    // Optional staff/customer user (null for guests)
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public Guid CountryId { get; set; }
    public Country Country { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.NewOrder;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? ShipmentCode { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
