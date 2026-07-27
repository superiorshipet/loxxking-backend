using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public record CreateProductRequest(Guid CategoryId, string NameAr, string NameEn, string Description, decimal BasePrice);
    public record UpdateProductRequest(Guid CategoryId, string NameAr, string NameEn, string Description, decimal BasePrice);
    public record UpsertPriceRequest(Guid CountryId, decimal Price);
    public record UpsertInventoryRequest(Guid CountryId, int Quantity);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] Guid? countryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _unitOfWork.Products.Query().Include(p => p.Category).AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.NameEn.ToLower().Contains(s) || p.NameAr.Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.NameAr,
                p.NameEn,
                p.Description,
                p.BasePrice,
                Category = p.Category.NameEn,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (!countryId.HasValue)
            return Ok(new { data = products, totalCount, page, pageSize, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) });

        var productIds = products.Select(p => p.Id).ToList();

        var prices = await _unitOfWork.ProductPrices.Query()
            .Where(pp => productIds.Contains(pp.ProductId) && pp.CountryId == countryId.Value)
            .ToDictionaryAsync(pp => pp.ProductId, pp => pp.Price, cancellationToken);

        var stock = await _unitOfWork.Inventories.Query()
            .Where(i => productIds.Contains(i.ProductId) && i.CountryId == countryId.Value)
            .ToDictionaryAsync(i => i.ProductId, i => i.Quantity, cancellationToken);

        var enriched = products.Select(p => new
        {
            p.Id,
            p.NameAr,
            p.NameEn,
            p.Description,
            p.Category,
            p.CreatedAt,
            Price = prices.TryGetValue(p.Id, out var price) ? price : p.BasePrice,
            Stock = stock.TryGetValue(p.Id, out var qty) ? qty : 0
        });

        return Ok(new { data = enriched, totalCount, page, pageSize, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? countryId, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return NotFound();

        decimal price = product.BasePrice;
        int stock = 0;

        if (countryId.HasValue)
        {
            price = await _unitOfWork.ProductPrices.Query()
                .Where(pp => pp.ProductId == id && pp.CountryId == countryId.Value)
                .Select(pp => pp.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (price == 0) price = product.BasePrice;

            stock = await _unitOfWork.Inventories.Query()
                .Where(i => i.ProductId == id && i.CountryId == countryId.Value)
                .Select(i => i.Quantity)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Ok(new
        {
            product.Id,
            product.NameAr,
            product.NameEn,
            product.Description,
            Category = product.Category.NameEn,
            Price = price,
            Stock = stock,
            product.CreatedAt
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var categoryExists = await _unitOfWork.Categories.Query().AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            return BadRequest(new { message = "Invalid category." });

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Description = request.Description,
            BasePrice = request.BasePrice,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new { product.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        product.CategoryId = request.CategoryId;
        product.NameAr = request.NameAr;
        product.NameEn = request.NameEn;
        product.Description = request.Description;
        product.BasePrice = request.BasePrice;

        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new { product.Id });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        _unitOfWork.Products.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id}/prices")]
    public async Task<IActionResult> GetPrices(Guid id, CancellationToken cancellationToken)
    {
        var prices = await _unitOfWork.ProductPrices.Query()
            .Include(pp => pp.Country)
            .Where(pp => pp.ProductId == id)
            .Select(pp => new { Country = pp.Country.Name, pp.CountryId, pp.Price })
            .ToListAsync(cancellationToken);

        return Ok(prices);
    }

    [HttpPut("{id}/prices")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> UpsertPrice(Guid id, [FromBody] UpsertPriceRequest request, CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.ProductPrices.Query()
            .FirstOrDefaultAsync(pp => pp.ProductId == id && pp.CountryId == request.CountryId, cancellationToken);

        if (existing is null)
        {
            await _unitOfWork.ProductPrices.AddAsync(new ProductPrice
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                CountryId = request.CountryId,
                Price = request.Price
            }, cancellationToken);
        }
        else
        {
            existing.Price = request.Price;
            _unitOfWork.ProductPrices.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Price updated." });
    }

    [HttpGet("{id}/inventory")]
    public async Task<IActionResult> GetInventory(Guid id, CancellationToken cancellationToken)
    {
        var stock = await _unitOfWork.Inventories.Query()
            .Include(i => i.Country)
            .Where(i => i.ProductId == id)
            .Select(i => new { Country = i.Country.Name, i.CountryId, i.Quantity, i.UpdatedAt })
            .ToListAsync(cancellationToken);

        return Ok(stock);
    }

    [HttpPut("{id}/inventory")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> UpsertInventory(Guid id, [FromBody] UpsertInventoryRequest request, CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Inventories.Query()
            .FirstOrDefaultAsync(i => i.ProductId == id && i.CountryId == request.CountryId, cancellationToken);

        if (existing is null)
        {
            await _unitOfWork.Inventories.AddAsync(new Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                CountryId = request.CountryId,
                Quantity = request.Quantity,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            existing.Quantity = request.Quantity;
            existing.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Inventories.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Inventory updated." });
    }
}
