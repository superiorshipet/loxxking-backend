namespace Domain.Entities;

public class SupportMessage {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = default!;
    public Guid? RelatedReviewId { get; set; }
    public Review? RelatedReview { get; set; }
    public string Message { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
