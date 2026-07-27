using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public CategoriesController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public record CategoryRequest(string NameAr, string NameEn);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _unitOfWork.Categories.Query()
            .Select(c => new { c.Id, c.NameAr, c.NameEn })
            .ToListAsync(cancellationToken);

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
        return NoContent();
    }
}
