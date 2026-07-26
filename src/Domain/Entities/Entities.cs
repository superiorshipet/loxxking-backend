namespace Domain.Entities;
using Domain.Enums;

public class User {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Country {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public string DefaultLanguage { get; set; } = "en";
}
public class Category {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
}
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
public class ProductPrice {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public decimal Price { get; set; }
}
public class Inventory {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class Order {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public User Customer { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public OrderStatus Status { get; set; } = OrderStatus.NewOrder;
    public PaymentMethod PaymentMethod { get; set; }
    public string? ShipmentCode { get; set; }
    public string Address { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
public class OrderItem {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal PriceAtOrder { get; set; }
}
public class OrderEditLog {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid EditedBy { get; set; }
    public User Editor { get; set; } = default!;
    public string FieldName { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
public class Invoice {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string InvoiceNumber { get; set; } = default!;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
public class Review {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public int Rating { get; set; }
    public string Comment { get; set; } = default!;
    public ReviewStatus Status { get; set; } = ReviewStatus.Visible;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class SupportMessage {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = default!;
    public Guid? RelatedReviewId { get; set; }
    public Review? RelatedReview { get; set; }
    public string Message { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Notification {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class SiteVisit {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public string Page { get; set; } = default!;
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
}
public class Offer {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public decimal DiscountPercent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
public class BankTransfer {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string ProofImage { get; set; } = default!;
    public BankTransferStatus Status { get; set; } = BankTransferStatus.Pending;
}
