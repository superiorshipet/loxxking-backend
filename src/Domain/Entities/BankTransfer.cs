using Domain.Enums;

namespace Domain.Entities;

public class BankTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string ProofImageUrl { get; set; } = string.Empty;
    public BankTransferStatus Status { get; set; }

    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
