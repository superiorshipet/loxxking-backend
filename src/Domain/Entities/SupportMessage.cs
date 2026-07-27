namespace Domain.Entities;

public class SupportMessage {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = default!;
    public Guid? RecipientId { get; set; }
    public string Message { get; set; } = default!;
    public string? AttachmentUrl { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
