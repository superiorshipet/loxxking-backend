using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/tracking")]
public class TrackingController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public TrackingController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> TrackOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound(new { message = "Order not found." });

        // Check if user can track this order
        var userId = GetCurrentUserId();
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");
        
        if (!isStaff && order.CustomerId != userId)
            return Forbid();

        // Get customer name
        string customerName = "Unknown";
        if (order.CustomerId.HasValue)
        {
            var customer = await _unitOfWork.Users.GetByIdAsync(order.CustomerId.Value, cancellationToken);
            customerName = customer?.Name ?? "Unknown";
        }

        // Get tracking status history
        var trackingHistory = await _unitOfWork.OrderEditLogs.Query()
            .Where(l => l.OrderId == orderId)
            .OrderBy(l => l.EditedAt)
            .Select(l => new
            {
                l.FieldName,
                l.OldValue,
                l.NewValue,
                l.EditedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.Status,
            order.ShipmentCode,
            order.Address,
            order.Phone,
            CustomerName = customerName,
            EstimatedDelivery = order.CreatedAt.AddDays(5),
            LastUpdate = DateTime.UtcNow,
            History = trackingHistory
        });
    }

    [HttpPatch("order/{orderId}/status")]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> UpdateTrackingStatus(Guid orderId, [FromBody] UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
            return NotFound();

        order.Status = request.Status;
        _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { order.Id, order.Status, Message = "Status updated." });
    }

    public record UpdateStatusRequest(OrderStatus Status);

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
