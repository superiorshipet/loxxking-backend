namespace Domain.Entities;

public class SupportMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid? SenderId { get; set; }          // null = guest/anonymous
    public User? Sender { get; set; }
    public Guid? RecipientId { get; set; } // Added
    public string Message { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public Guid? RelatedOrderId { get; set; }
    public Guid? RelatedReviewId { get; set; }
    public string? GuestName { get; set; }      // name for guest/anonymous senders
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
