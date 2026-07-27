using Domain.Entities;

namespace Domain.Entities;

public class SupportMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    
    // Sender info (for both staff and guests)
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public Guid? SenderId { get; set; } // Optional: for staff users
    public User? Sender { get; set; } // Navigation property for staff
    
    public string Message { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
