namespace Domain.Entities;

/// <summary>A single message in a support conversation thread.</summary>
public class SupportMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Groups messages into one conversation thread.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>"Customer" or "Staff"</summary>
    public string SenderType { get; set; } = "Customer";

    /// <summary>Display name — guest name or staff name.</summary>
    public string SenderName { get; set; } = "Customer";

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
