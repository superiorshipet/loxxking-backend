using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IInvoicePdfGenerator
{
    Task<byte[]> GenerateAsync(Invoice invoice, CancellationToken cancellationToken);
}
