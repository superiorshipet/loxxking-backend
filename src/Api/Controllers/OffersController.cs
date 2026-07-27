using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize]
public class OffersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public OffersController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var offers = await _unitOfWork.Offers.Query()
            .Include(o => o.OfferProducts)
                .ThenInclude(op => op.Product)
            .Where(o => o.IsActive && o.StartDate <= DateTime.UtcNow && o.EndDate >= DateTime.UtcNow)
            .Select(o => new
            {
                o.Id,
                Title = o.TitleEn,
                TitleAr = o.TitleAr,
                o.Description,
                o.DiscountPercentage,
                o.StartDate,
                o.EndDate,
                o.IsActive,
                Products = o.OfferProducts.Select(op => new
                {
                    op.ProductId,
                    ProductName = op.Product.NameEn,
                    ProductPrice = op.Product.BasePrice,
                    op.DiscountPercentage
                })
            })
            .ToListAsync(cancellationToken);

        return Ok(offers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _unitOfWork.Offers.Query()
            .Include(o => o.OfferProducts)
                .ThenInclude(op => op.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer is null)
            return NotFound();

        return Ok(new
        {
            offer.Id,
            Title = offer.TitleEn,
            TitleAr = offer.TitleAr,
            offer.Description,
            offer.DiscountPercentage,
            offer.StartDate,
            offer.EndDate,
            offer.IsActive,
            Products = offer.OfferProducts.Select(op => new
            {
                op.ProductId,
                ProductName = op.Product.NameEn,
                ProductPrice = op.Product.BasePrice,
                op.DiscountPercentage
            })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Create([FromBody] CreateOfferRequest request, CancellationToken cancellationToken)
    {
        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            TitleAr = request.TitleAr,
            TitleEn = request.TitleEn,
            Description = request.Description,
            DiscountPercentage = request.DiscountPercentage,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Offers.AddAsync(offer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Add products to offer using the OfferProducts repository
        foreach (var productId in request.ProductIds)
        {
            var offerProduct = new OfferProduct
            {
                Id = Guid.NewGuid(),
                OfferId = offer.Id,
                ProductId = productId,
                Quantity = 1,
                DiscountPercentage = request.DiscountPercentage,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.OfferProducts.AddAsync(offerProduct, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = offer.Id }, offer);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOfferRequest request, CancellationToken cancellationToken)
    {
        var offer = await _unitOfWork.Offers.Query()
            .Include(o => o.OfferProducts)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer is null)
            return NotFound();

        if (!string.IsNullOrEmpty(request.TitleAr))
            offer.TitleAr = request.TitleAr;
        if (!string.IsNullOrEmpty(request.TitleEn))
            offer.TitleEn = request.TitleEn;
        if (!string.IsNullOrEmpty(request.Description))
            offer.Description = request.Description;
        if (request.DiscountPercentage.HasValue)
            offer.DiscountPercentage = request.DiscountPercentage.Value;
        if (request.StartDate.HasValue)
            offer.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue)
            offer.EndDate = request.EndDate.Value;
        if (request.IsActive.HasValue)
            offer.IsActive = request.IsActive.Value;

        _unitOfWork.Offers.Update(offer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(offer);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _unitOfWork.Offers.Query()
            .Include(o => o.OfferProducts)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (offer is null)
            return NotFound();

        // Remove associated offer products
        foreach (var offerProduct in offer.OfferProducts.ToList())
        {
            _unitOfWork.OfferProducts.Remove(offerProduct);
        }

        _unitOfWork.Offers.Remove(offer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    public class CreateOfferRequest
    {
        public string TitleAr { get; set; } = string.Empty;
        public string TitleEn { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DiscountPercentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Guid> ProductIds { get; set; } = new();
    }

    public class UpdateOfferRequest
    {
        public string? TitleAr { get; set; }
        public string? TitleEn { get; set; }
        public string? Description { get; set; }
        public decimal? DiscountPercentage { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? IsActive { get; set; }
    }
}
