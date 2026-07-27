namespace Domain.Entities;
using Domain.Enums;

public class BankTransfer {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string ProofImage { get; set; } = default!;
    public BankTransferStatus Status { get; set; } = BankTransferStatus.Pending;
}
