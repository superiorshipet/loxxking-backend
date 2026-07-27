namespace Domain.Entities;
using Domain.Enums;

public class Order {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty; // Added
    public Guid? UserId { get; set; } // Added - for guest orders
    public User? User { get; set; } // Added - navigation
    public Guid? CustomerId { get; set; } // Made nullable
    public User? Customer { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public OrderStatus Status { get; set; } = OrderStatus.NewOrder;
    public PaymentMethod PaymentMethod { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? ShipmentCode { get; set; }
    public string Address { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Notes { get; set; }
    // Guest-specific fields (populated when CustomerId is null)
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
