using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class OrderNumberGenerator : IOrderNumberGenerator
{
    private readonly IUnitOfWork _unitOfWork;

    public OrderNumberGenerator(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        
        // Get count of orders today
        var count = await _unitOfWork.Orders.Query()
            .CountAsync(o => o.CreatedAt.Date == today.Date, cancellationToken);
        
        var sequence = (count + 1).ToString("D4");
        return $"LOXX-{datePart}-{sequence}";
    }
}
