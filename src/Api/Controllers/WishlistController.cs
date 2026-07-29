using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/wishlist")]
public class WishlistController : ControllerBase
{
    private readonly AppDbContext _db;

    public WishlistController(AppDbContext db)
    {
        _db = db;
    }

    // ─── GET /api/wishlist — current user's or guest's wishlist ────────────────
    [HttpGet]
    public async Task<IActionResult> GetMyWishlist([FromHeader(Name = "X-Guest-Id")] string? guestId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty && string.IsNullOrWhiteSpace(guestId))
            return Unauthorized(new { message = "You must be logged in or provide a guest ID." });

        var query = _db.WishlistItems.AsQueryable();
        if (userId != Guid.Empty)
            query = query.Where(w => w.UserId == userId);
        else
            query = query.Where(w => w.GuestId == guestId);

        var items = await query
            .Include(w => w.Product)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => new
            {
                w.Id,
                w.ProductId,
                w.AddedAt,
                Product = new
                {
                    w.Product.Id,
                    w.Product.NameEn,
                    w.Product.NameAr,
                    w.Product.Description,
                    w.Product.BasePrice,
                    w.Product.CategoryId
                }
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    // ─── POST /api/wishlist/{productId} — add to wishlist ────────────────────
    [HttpPost("{productId}")]
    public async Task<IActionResult> AddToWishlist(Guid productId, [FromHeader(Name = "X-Guest-Id")] string? guestId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty && string.IsNullOrWhiteSpace(guestId))
            return Unauthorized(new { message = "You must be logged in or provide a guest ID." });

        // Check product exists
        var productExists = await _db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists) return NotFound(new { message = "Product not found." });

        // Ignore duplicate (idempotent)
        var existing = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.ProductId == productId && (userId != Guid.Empty ? w.UserId == userId : w.GuestId == guestId), cancellationToken);
            
        if (existing != null)
            return Ok(new { message = "Already in wishlist.", wishlistItemId = existing.Id });

        var item = new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId != Guid.Empty ? userId : null,
            GuestId = userId == Guid.Empty ? guestId : null,
            ProductId = productId,
            AddedAt = DateTime.UtcNow
        };

        _db.WishlistItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Added to wishlist.", wishlistItemId = item.Id });
    }

    // ─── DELETE /api/wishlist/{productId} — remove from wishlist ─────────────
    [HttpDelete("{productId}")]
    public async Task<IActionResult> RemoveFromWishlist(Guid productId, [FromHeader(Name = "X-Guest-Id")] string? guestId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty && string.IsNullOrWhiteSpace(guestId))
            return Unauthorized(new { message = "You must be logged in or provide a guest ID." });

        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.ProductId == productId && (userId != Guid.Empty ? w.UserId == userId : w.GuestId == guestId), cancellationToken);

        if (item == null) return NotFound(new { message = "Item not in wishlist." });

        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Removed from wishlist." });
    }

    // ─── GET /api/wishlist/check/{productId} — is it in wishlist? ────────────
    [HttpGet("check/{productId}")]
    public async Task<IActionResult> CheckWishlist(Guid productId, [FromHeader(Name = "X-Guest-Id")] string? guestId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty && string.IsNullOrWhiteSpace(guestId)) 
            return Ok(new { inWishlist = false });

        var inWishlist = await _db.WishlistItems
            .AnyAsync(w => w.ProductId == productId && (userId != Guid.Empty ? w.UserId == userId : w.GuestId == guestId), cancellationToken);

        return Ok(new { inWishlist });
    }

    private Guid GetUserId()
    {
        var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("nameid")?.Value
               ?? User.FindFirst("sub")?.Value;
        return val != null && Guid.TryParse(val, out var id) ? id : Guid.Empty;
    }
}
