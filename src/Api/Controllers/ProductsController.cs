using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private readonly IFileStorageService _fileStorageService;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private const string VersionKey = "products:list:version";

    public ProductsController(IUnitOfWork unitOfWork, IDistributedCache cache, IFileStorageService fileStorageService)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _fileStorageService = fileStorageService;
    }

    public record CreateProductRequest(
        Guid CategoryId, string NameAr, string NameEn, string Description, decimal BasePrice,
        string? Features = null, string? ShippingPolicy = null, string? ReturnPolicy = null,
        List<string>? Images = null);
    public record UpdateProductRequest(
        Guid CategoryId, string NameAr, string NameEn, string Description, decimal BasePrice,
        string? Features = null, string? ShippingPolicy = null, string? ReturnPolicy = null,
        List<string>? Images = null);
    public record UpsertPriceRequest(Guid CountryId, decimal Price);
    public record UpsertInventoryRequest(Guid CountryId, int Quantity);
    public record SetPriceRequest(decimal Price);
    public record SubmitReviewRequest(int Rating, string Comment);

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

        var version = await _cache.GetStringAsync(VersionKey, cancellationToken) ?? "1";
        var cacheKey = $"products:list:{categoryId}:{search}:{countryId}:{page}:{pageSize}:v{version}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            var cachedResult = JsonSerializer.Deserialize<object>(cached);
            return Ok(cachedResult);
        }

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
                p.CreatedAt,
                p.Images
            })
            .ToListAsync(cancellationToken);

        object finalResult;

        if (!countryId.HasValue)
        {
            finalResult = new { data = products, totalCount, page, pageSize, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) };
            
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalResult), options, cancellationToken);
            
            return Ok(finalResult);
        }

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

        finalResult = new { data = enriched, totalCount, page, pageSize, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) };

        var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalResult), cacheOptions, cancellationToken);

        return Ok(finalResult);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? countryId, CancellationToken cancellationToken)
    {
        var cacheKey = $"products:{id}:{countryId}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            var cachedResult = JsonSerializer.Deserialize<object>(cached);
            return Ok(cachedResult);
        }

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

        var finalResult = new
        {
            product.Id,
            product.NameAr,
            product.NameEn,
            product.Description,
            Category = product.Category.NameEn,
            Price = price,
            Stock = stock,
            product.CreatedAt
        };

        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalResult), options, cancellationToken);

        return Ok(finalResult);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var categoryExists = await _unitOfWork.Categories.Query().AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
            return BadRequest(new { message = "Invalid category." });

        var processedImages = await ProcessBase64ImagesAsync(request.Images, cancellationToken);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Description = request.Description,
            Features = request.Features,
            ShippingPolicy = request.ShippingPolicy,
            ReturnPolicy = request.ReturnPolicy,
            BasePrice = request.BasePrice,
            Images = processedImages,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await InvalidateProductCacheAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, new { product.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        product.CategoryId   = request.CategoryId;
        product.NameAr        = request.NameAr;
        product.NameEn        = request.NameEn;
        product.Description   = request.Description;
        product.Features      = request.Features;
        product.ShippingPolicy= request.ShippingPolicy;
        product.ReturnPolicy  = request.ReturnPolicy;
        product.BasePrice     = request.BasePrice;
        if (request.Images != null) 
        {
            product.Images = await ProcessBase64ImagesAsync(request.Images, cancellationToken);
        }

        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await InvalidateProductCacheAsync(cancellationToken);

        return Ok(new { product.Id });
    }

    [HttpPost("{id}/images")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No image provided.");

        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        var imageUrl = await _fileStorageService.UploadAsync(file, "loxxking/products", cancellationToken);
        
        product.Images.Add(imageUrl);
        _unitOfWork.Products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        await InvalidateProductCacheAsync(cancellationToken);
        return Ok(new { imageUrl });
    }

    [HttpDelete("{id}/images")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> DeleteImage(Guid id, [FromQuery] string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("No URL provided.");

        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        if (product.Images.Remove(url))
        {
            _unitOfWork.Products.Update(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Try to delete from Cloudinary in background to not block
            _ = _fileStorageService.DeleteAsync(url, CancellationToken.None);
        }

        await InvalidateProductCacheAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<string>> ProcessBase64ImagesAsync(List<string>? images, CancellationToken cancellationToken)
    {
        if (images == null || images.Count == 0) return new List<string>();

        var processedImages = new List<string>();
        foreach (var img in images)
        {
            if (img.StartsWith("data:image/"))
            {
                try
                {
                    var commaIndex = img.IndexOf(',');
                    if (commaIndex > -1)
                    {
                        var base64Data = img.Substring(commaIndex + 1);
                        var bytes = Convert.FromBase64String(base64Data);
                        
                        var mimeTypePart = img.Substring(0, commaIndex).Split(';')[0];
                        var extension = mimeTypePart.Split('/').LastOrDefault() ?? "png";
                        var fileName = $"upload_{Guid.NewGuid()}.{extension}";

                        using var stream = new MemoryStream(bytes);
                        var formFile = new FormFile(stream, 0, stream.Length, "file", fileName)
                        {
                            Headers = new HeaderDictionary(),
                            ContentType = mimeTypePart.Replace("data:", "")
                        };

                        var uploadedUrl = await _fileStorageService.UploadAsync(formFile, "loxxking/products", cancellationToken);
                        processedImages.Add(uploadedUrl);
                    }
                    else
                    {
                        processedImages.Add(img);
                    }
                }
                catch
                {
                    // Fallback to storing the raw base64 string if Cloudinary upload fails
                    processedImages.Add(img);
                }
            }
            else
            {
                processedImages.Add(img);
            }
        }
        return processedImages;
    }

    // ─── GET /api/products/best-sellers ──────────────────────────────────────
    [HttpGet("best-sellers")]
    public async Task<IActionResult> GetBestSellers(
        [FromQuery] int top = 20,
        [FromQuery] Guid? countryId = null,
        CancellationToken cancellationToken = default)
    {
        // Count sold units per product from OrderItems
        var soldCounts = await _unitOfWork.OrderItems.Query()
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, SoldCount = g.Sum(oi => oi.Quantity) })
            .OrderByDescending(x => x.SoldCount)
            .Take(top)
            .ToListAsync(cancellationToken);

        var productIds = soldCounts.Select(x => x.ProductId).ToList();

        var products = await _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Merge with sold counts and sort
        var result = soldCounts.Select(s =>
        {
            var p = products.FirstOrDefault(x => x.Id == s.ProductId);
            if (p == null) return null;
            return new
            {
                p.Id, p.NameEn, p.NameAr, p.Description, p.BasePrice,
                Category = p.Category?.NameEn,
                s.SoldCount
            };
        }).Where(x => x != null).ToList();

        return Ok(result);
    }

    // ─── GET /api/products/{id}/detail — rich product page data ──────────────
    [HttpGet("{id}/detail")]
    public async Task<IActionResult> GetDetail(Guid id, [FromQuery] Guid? countryId, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.Query()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null) return NotFound();

        decimal price = product.BasePrice;
        int stock = 0;
        if (countryId.HasValue)
        {
            var pp = await _unitOfWork.ProductPrices.Query()
                .FirstOrDefaultAsync(x => x.ProductId == id && x.CountryId == countryId.Value, cancellationToken);
            price = pp?.Price ?? product.BasePrice;

            stock = await _unitOfWork.Inventories.Query()
                .Where(i => i.ProductId == id && i.CountryId == countryId.Value)
                .Select(i => i.Quantity)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Reviews (approved only for public)
        var reviews = await _unitOfWork.Reviews.Query()
            .Include(r => r.User)
            .Where(r => r.ProductId == id && r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Comment,
                r.CreatedAt,
                ReviewerName = r.User != null ? r.User.Name : (r.GuestName ?? "Customer")
            })
            .ToListAsync(cancellationToken);

        double avgRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;
        int soldCount = await _unitOfWork.OrderItems.Query()
            .Where(oi => oi.ProductId == id)
            .SumAsync(oi => oi.Quantity, cancellationToken);

        // Features as list
        var featuresList = product.Features?
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim().TrimStart('-', '•', '*').Trim())
            .Where(f => f.Length > 0)
            .ToList() ?? new List<string>();

        return Ok(new
        {
            product.Id,
            product.NameEn,
            product.NameAr,
            product.Description,
            product.Features,
            FeaturesList = featuresList,
            product.ShippingPolicy,
            product.ReturnPolicy,
            Category = product.Category?.NameEn,
            Price = price,
            Stock = stock,
            SoldCount = soldCount,
            AverageRating = Math.Round(avgRating, 1),
            ReviewCount = reviews.Count,
            Reviews = reviews,
            product.Images,
            product.CreatedAt
        });
    }

    // ─── POST /api/products/{id}/reviews — submit a review ───────────────────
    [HttpPost("{id}/reviews")]
    [Authorize]
    public async Task<IActionResult> SubmitReview(Guid id, [FromBody] SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("nameid")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest(new { message = "Rating must be between 1 and 5." });

        var productExists = await _unitOfWork.Products.Query().AnyAsync(p => p.Id == id, cancellationToken);
        if (!productExists) return NotFound();

        // One review per user per product
        var existing = await _unitOfWork.Reviews.Query()
            .FirstOrDefaultAsync(r => r.ProductId == id && r.UserId == userId, cancellationToken);

        if (existing != null)
        {
            existing.Rating = request.Rating;
            existing.Comment = request.Comment;
            existing.IsApproved = true; // auto-approve
            _unitOfWork.Reviews.Update(existing);
        }
        else
        {
            var review = new Review
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                UserId = userId,
                Rating = request.Rating,
                Comment = request.Comment,
                IsApproved = true, // auto-approve
                Status = Domain.Enums.ReviewStatus.approved,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Reviews.AddAsync(review, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Review submitted successfully." });
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

        await InvalidateProductCacheAsync(cancellationToken);

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

        await InvalidateProductCacheAsync(cancellationToken);

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

        await InvalidateProductCacheAsync(cancellationToken);

        return Ok(new { message = "Inventory updated." });
    }

    [HttpGet("price/{countryId}/{id}")]
    public async Task<IActionResult> GetProductPriceByCountry(Guid id, Guid countryId, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        var price = await _unitOfWork.ProductPrices.Query()
            .Where(pp => pp.ProductId == id && pp.CountryId == countryId)
            .Select(pp => pp.Price)
            .FirstOrDefaultAsync(cancellationToken);

        if (price == 0)
            price = product.BasePrice;

        var country = await _unitOfWork.Countries.GetByIdAsync(countryId, cancellationToken);

        return Ok(new
        {
            ProductId = id,
            CountryId = countryId,
            CountryName = country?.Name ?? "Unknown",
            Price = price,
            Currency = country?.Currency ?? "EGP"
        });
    }

    [HttpPut("price/{countryId}/{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> SetProductPriceByCountry(Guid id, Guid countryId, [FromBody] SetPriceRequest request, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();

        var country = await _unitOfWork.Countries.GetByIdAsync(countryId, cancellationToken);
        if (country is null)
            return BadRequest(new { message = "Country not found." });

        var existing = await _unitOfWork.ProductPrices.Query()
            .FirstOrDefaultAsync(pp => pp.ProductId == id && pp.CountryId == countryId, cancellationToken);

        if (existing is null)
        {
            var newPrice = new ProductPrice
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                CountryId = countryId,
                Price = request.Price
            };
            await _unitOfWork.ProductPrices.AddAsync(newPrice, cancellationToken);
        }
        else
        {
            existing.Price = request.Price;
            _unitOfWork.ProductPrices.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ProductId = id,
            CountryId = countryId,
            CountryName = country.Name,
            Price = request.Price,
            Currency = country.Currency,
            Message = "Price updated successfully."
        });
    }

    [HttpGet("prices-by-country")]
    public async Task<IActionResult> GetProductsWithPricesByCountry([FromQuery] Guid? countryId, CancellationToken cancellationToken)
    {
        var query = from p in _unitOfWork.Products.Query()
                    join pp in _unitOfWork.ProductPrices.Query() on p.Id equals pp.ProductId into prices
                    from pp in prices.DefaultIfEmpty()
                    select new
                    {
                        p.Id,
                        p.NameEn,
                        p.NameAr,
                        p.BasePrice,
                        CountryId = pp != null ? pp.CountryId : (Guid?)null,
                        CountryName = pp != null ? pp.Country.Name : null,
                        Price = pp != null ? pp.Price : (decimal?)null,
                        Currency = pp != null ? pp.Country.Currency : null
                    };

        var results = await query.ToListAsync(cancellationToken);

        var grouped = results
            .GroupBy(x => new { x.Id, x.NameEn, x.NameAr, x.BasePrice })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.NameEn,
                g.Key.NameAr,
                g.Key.BasePrice,
                CountryPrices = g.Where(x => x.CountryId.HasValue)
                    .Select(x => new
                    {
                        CountryId = x.CountryId.Value,
                        CountryName = x.CountryName,
                        Price = x.Price ?? 0,
                        Currency = x.Currency ?? "EGP"
                    })
            })
            .ToList();

        if (countryId.HasValue)
        {
            grouped = grouped.Select(g => new
            {
                g.Id,
                g.NameEn,
                g.NameAr,
                g.BasePrice,
                CountryPrices = g.CountryPrices.Where(cp => cp.CountryId == countryId.Value)
            }).ToList();
        }

        return Ok(grouped);
    }

    private async Task InvalidateProductCacheAsync(CancellationToken cancellationToken)
    {
        var currentVersion = await _cache.GetStringAsync(VersionKey, cancellationToken) ?? "1";
        var newVersion = (int.Parse(currentVersion) + 1).ToString();
        await _cache.SetStringAsync(VersionKey, newVersion, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        }, cancellationToken);
    }
}
