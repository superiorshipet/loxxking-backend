namespace Domain.Entities;
using Domain.Enums;

public class BankTransfer {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string ProofImageUrl { get; set; } = default!;
    public BankTransferStatus Status { get; set; } = BankTransferStatus.PendingReview;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
