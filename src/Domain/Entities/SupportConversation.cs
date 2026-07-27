using Domain.Enums;

namespace Domain.Entities;

public class SupportConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public Guid? AssignedTo { get; set; }
    public SupportStatus Status { get; set; } = SupportStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
