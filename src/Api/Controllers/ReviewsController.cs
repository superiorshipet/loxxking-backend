using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public ReviewsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public record CreateReviewRequest(Guid ProductId, int Rating, string Comment);

    [HttpGet("product/{productId}")]
    public async Task<IActionResult> GetByProduct(Guid productId, CancellationToken cancellationToken)
    {
        var reviews = await _unitOfWork.Reviews.Query()
            .Include(r => r.User)
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Visible)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Comment,
                r.CreatedAt,
                User = new
                {
                    r.User.Id,
                    r.User.Name
                }
            })
            .ToListAsync(cancellationToken);

        return Ok(reviews);
    }

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        // Check if user has purchased this product (order must be Delivered)
        var hasPurchased = await _unitOfWork.OrderItems.Query()
            .Include(oi => oi.Order)
            .AnyAsync(oi => oi.ProductId == request.ProductId && oi.Order.UserId == userId && oi.Order.Status == OrderStatus.Delivered, cancellationToken);

        if (!hasPurchased)
            return BadRequest(new { message = "You can only review products you have purchased and received." });

        var existing = await _unitOfWork.Reviews.Query()
            .AnyAsync(r => r.ProductId == request.ProductId && r.UserId == userId, cancellationToken);

        if (existing)
            return BadRequest(new { message = "You have already reviewed this product." });

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            UserId = userId,
            Rating = request.Rating,
            Comment = request.Comment,
            Status = ReviewStatus.Pending,
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Reviews.AddAsync(review, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify admins
        var admins = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.Admin)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var adminId in admins)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = adminId,
                Type = NotificationType.NewReview,
                Message = "A new review needs moderation.",
                RelatedEntityId = review.Id,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = review.Id }, review);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var review = await _unitOfWork.Reviews.Query()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
            return NotFound();

        return Ok(new
        {
            review.Id,
            review.Rating,
            review.Comment,
            review.CreatedAt,
            review.Status,
            review.IsApproved,
            User = new
            {
                review.User.Id,
                review.User.Name
            }
        });
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var pending = await _unitOfWork.Reviews.Query()
            .Include(r => r.User)
            .Where(r => r.Status == ReviewStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Comment,
                r.CreatedAt,
                User = new
                {
                    r.User.Id,
                    r.User.Name
                }
            })
            .ToListAsync(cancellationToken);

        return Ok(pending);
    }

    [HttpPatch("{id}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var review = await _unitOfWork.Reviews.Query()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
            return NotFound();

        review.Status = ReviewStatus.approved;
        review.IsApproved = true;
        _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = review.UserId,
            Type = NotificationType.ReviewResponse,
            Message = "Your review has been approved and is now visible.",
            RelatedEntityId = review.Id,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Review approved successfully." });
    }

    [HttpPatch("{id}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        var review = await _unitOfWork.Reviews.Query()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
            return NotFound();

        review.Status = ReviewStatus.Rejected;
        review.IsApproved = false;
        _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = review.UserId,
            Type = NotificationType.ReviewResponse,
            Message = "Your review has been rejected. Please check the guidelines.",
            RelatedEntityId = review.Id,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Review rejected successfully." });
    }

    [HttpPatch("{id}/hide")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Hide(Guid id, CancellationToken cancellationToken)
    {
        var review = await _unitOfWork.Reviews.Query()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
            return NotFound();

        review.Status = ReviewStatus.Hidden;
        review.IsApproved = false;
        _unitOfWork.Reviews.Update(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Review hidden successfully." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var review = await _unitOfWork.Reviews.Query()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
            return NotFound();

        _unitOfWork.Reviews.Remove(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
