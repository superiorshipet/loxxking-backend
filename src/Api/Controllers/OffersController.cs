using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/offers")]
public class OffersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public OffersController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public record CreateOfferRequest(Guid ProductId, decimal DiscountPercent, DateTime StartDate, DateTime EndDate);
    public record UpdateOfferRequest(decimal DiscountPercent, DateTime StartDate, DateTime EndDate);

    // ------------------------------------------------------------
    // GET /api/offers?activeOnly=true — عام لأي زائر، عشان يظهر في الموقع
    // ------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = _unitOfWork.Offers.Query().Include(o => o.Product).AsQueryable();

        if (activeOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(o => o.StartDate <= now && o.EndDate >= now);
        }

        var offers = await query
            .OrderByDescending(o => o.StartDate)
            .Select(o => new
            {
                o.Id,
                ProductId = o.ProductId,
                ProductName = o.Product.NameEn,
                o.DiscountPercent,
                o.StartDate,
                o.EndDate
            })
            .ToListAsync(cancellationToken);

        return Ok(offers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _unitOfWork.Offers.Query()
            .Include(o => o.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer is null)
            return NotFound();

        return Ok(new
        {
            offer.Id,
            ProductId = offer.ProductId,
            ProductName = offer.Product.NameEn,
            offer.DiscountPercent,
            offer.StartDate,
            offer.EndDate
        });
    }

    // ------------------------------------------------------------
    // POST /api/offers — Admin, StoreManager
    // ------------------------------------------------------------
    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Create([FromBody] CreateOfferRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
            return BadRequest(new { message = "EndDate must be after StartDate." });

        var productExists = await _unitOfWork.Products.Query().AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
            return BadRequest(new { message = "Invalid product." });

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            DiscountPercent = request.DiscountPercent,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await _unitOfWork.Offers.AddAsync(offer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = offer.Id }, new { offer.Id });
    }

    // ------------------------------------------------------------
    // PUT /api/offers/{id} — Admin, StoreManager
    // ------------------------------------------------------------
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferRequest request, CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
            return BadRequest(new { message = "EndDate must be after StartDate." });

        var offer = await _unitOfWork.Offers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
            return NotFound();

        offer.DiscountPercent = request.DiscountPercent;
        offer.StartDate = request.StartDate;
        offer.EndDate = request.EndDate;

        _unitOfWork.Offers.Update(offer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { offer.Id });
    }

    // ------------------------------------------------------------
    // DELETE /api/offers/{id} — Admin, StoreManager
    // ------------------------------------------------------------
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _unitOfWork.Offers.GetByIdAsync(id, cancellationToken);
        if (offer is null)
            return NotFound();

        _unitOfWork.Offers.Remove(offer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
