using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "categories:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public CategoriesController(IUnitOfWork unitOfWork, IDistributedCache cache)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public record CategoryRequest(string NameAr, string NameEn);
    public record CategoryDto(Guid Id, string NameAr, string NameEn);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        // Try to get from cache
        var cached = await _cache.GetStringAsync(CacheKey, cancellationToken);
        if (cached is not null)
        {
            var categories = JsonSerializer.Deserialize<List<CategoryDto>>(cached);
            return Ok(categories);
        }

        // Query from database
        var categories = await _unitOfWork.Categories.Query()
            .Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn))
            .ToListAsync(cancellationToken);

        // Store in cache
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        };
        await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(categories), options, cancellationToken);

        return Ok(categories);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            NameAr = request.NameAr,
            NameEn = request.NameEn
        };

        await _unitOfWork.Categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync(CacheKey, cancellationToken);

        return Ok(new { category.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return NotFound();

        category.NameAr = request.NameAr;
        category.NameEn = request.NameEn;
        _unitOfWork.Categories.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync(CacheKey, cancellationToken);

        return Ok(new { category.Id });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var category = await _unitOfWork.Categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return NotFound();

        _unitOfWork.Categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync(CacheKey, cancellationToken);

        return NoContent();
    }
}
