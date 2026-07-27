using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/bank-transfers")]
[Authorize]
public class BankTransfersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorageService;

    public BankTransfersController(IUnitOfWork unitOfWork, IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _fileStorageService = fileStorageService;
    }

    public record ReviewTransferRequest(bool Approved, string? RejectionReason);

    [HttpPost]
    [Authorize(Roles = "Customer")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] Guid orderId, [FromForm] IFormFile proofImage, CancellationToken cancellationToken)
    {
        if (proofImage is null || proofImage.Length == 0)
            return BadRequest(new { message = "Proof image is required." });

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp" };
        if (!allowedTypes.Contains(proofImage.ContentType))
            return BadRequest(new { message = "Only image files are allowed (png, jpg, webp)." });

        var userId = GetCurrentUserId();

        var order = await _unitOfWork.Orders.Query()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == userId, cancellationToken);

        if (order is null)
            return NotFound(new { message = "Order not found." });

        var existing = await _unitOfWork.BankTransfers.Query()
            .AnyAsync(bt => bt.OrderId == orderId, cancellationToken);

        if (existing)
            return BadRequest(new { message = "A transfer proof was already submitted for this order." });

        var imageUrl = await _fileStorageService.UploadAsync(proofImage, "bank-transfers", cancellationToken);

        var transfer = new BankTransfer
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProofImageUrl = imageUrl,
            Status = BankTransferStatus.PendingReview,
            SubmittedAt = DateTime.UtcNow
        };

        await _unitOfWork.BankTransfers.AddAsync(transfer, cancellationToken);

        order.PaymentStatus = PaymentStatus.PendingVerification;
        _unitOfWork.Orders.Update(order);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reviewers = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.StoreManager)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var reviewerId in reviewers)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = reviewerId,
                Type = NotificationType.BankTransferSubmitted,
                Message = "A new bank transfer proof needs review.",
                RelatedEntityId = order.Id,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByOrderId), new { orderId }, new { transfer.Id, transfer.Status });
    }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetByOrderId(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager");

        var transfer = await _unitOfWork.BankTransfers.Query()
            .Include(bt => bt.Order)
                .ThenInclude(o => o.Customer)
            .FirstOrDefaultAsync(bt => bt.OrderId == orderId, cancellationToken);

        if (transfer is null)
            return NotFound();

        if (!isStaff && transfer.Order.CustomerId != userId)
            return Forbid();

        return Ok(new
        {
            transfer.Id,
            transfer.OrderId,
            transfer.ProofImageUrl,
            Status = transfer.Status.ToString(),
            transfer.SubmittedAt,
            transfer.ReviewedAt,
            transfer.RejectionReason,
            CustomerName = transfer.Order.Customer?.Name ?? "Guest"
        });
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var pending = await _unitOfWork.BankTransfers.Query()
            .Include(bt => bt.Order)
                .ThenInclude(o => o.Customer)
            .Where(bt => bt.Status == BankTransferStatus.PendingReview)
            .OrderBy(bt => bt.SubmittedAt)
            .Select(bt => new
            {
                bt.Id,
                bt.OrderId,
                CustomerName = bt.Order.Customer != null ? bt.Order.Customer.Name : "Guest",
                bt.ProofImageUrl,
                bt.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(pending);
    }

    [HttpPatch("{id}/review")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewTransferRequest request, CancellationToken cancellationToken)
    {
        var transfer = await _unitOfWork.BankTransfers.Query()
            .Include(bt => bt.Order)
            .FirstOrDefaultAsync(bt => bt.Id == id, cancellationToken);

        if (transfer is null)
            return NotFound();

        if (!request.Approved && string.IsNullOrWhiteSpace(request.RejectionReason))
            return BadRequest(new { message = "RejectionReason is required when rejecting a transfer." });

        transfer.Status = request.Approved ? BankTransferStatus.Approved : BankTransferStatus.Rejected;
        transfer.ReviewedAt = DateTime.UtcNow;
        transfer.RejectionReason = request.Approved ? null : request.RejectionReason;
        _unitOfWork.BankTransfers.Update(transfer);

        transfer.Order.PaymentStatus = request.Approved ? PaymentStatus.Paid : PaymentStatus.Rejected;
        _unitOfWork.Orders.Update(transfer.Order);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (transfer.Order.CustomerId.HasValue)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = transfer.Order.CustomerId.Value,
                Type = NotificationType.BankTransferReviewed,
                Message = request.Approved
                    ? "Your bank transfer was approved. Your order is now confirmed."
                    : $"Your bank transfer was rejected: {request.RejectionReason}",
                RelatedEntityId = transfer.OrderId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { transfer.Id, transfer.Status });
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
