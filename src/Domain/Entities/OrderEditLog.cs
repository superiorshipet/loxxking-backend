namespace Domain.Entities;

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
