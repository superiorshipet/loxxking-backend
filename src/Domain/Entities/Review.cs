namespace Domain.Entities;
using Domain.Enums;

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
