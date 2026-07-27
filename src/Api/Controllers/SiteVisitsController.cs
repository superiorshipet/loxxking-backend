using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/site-visits")]
public class SiteVisitsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SiteVisitsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public record CreateSiteVisitRequest(Guid CountryId, string Page);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSiteVisitRequest request, CancellationToken cancellationToken)
    {
        // Validate country exists
        var country = await _unitOfWork.Countries.GetByIdAsync(request.CountryId, cancellationToken);
        if (country is null)
            return BadRequest(new { message = "Invalid country." });

        // Create site visit
        var siteVisit = new SiteVisit
        {
            Id = Guid.NewGuid(),
            CountryId = request.CountryId,
            Page = request.Page,
            VisitedAt = DateTime.UtcNow
        };

        await _unitOfWork.SiteVisits.AddAsync(siteVisit, cancellationToken);

        // Get all admin users
        var admins = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.Admin)
            .ToListAsync(cancellationToken);

        // Create notifications for each admin
        foreach (var admin in admins)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = admin.Id,
                Type = NotificationType.SystemAlert,
                Message = $"New visit from {country.Name} on {request.Page}",
                RelatedEntityId = siteVisit.Id,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { siteVisit.Id, Message = "Site visit tracked successfully." });
    }

    [HttpGet("today-count")]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> GetTodayCount(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var count = await _unitOfWork.SiteVisits.Query()
            .Where(sv => sv.VisitedAt >= today && sv.VisitedAt < today.AddDays(1))
            .CountAsync(cancellationToken);

        return Ok(new { todayCount = count });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> GetVisits(
        [FromQuery] Guid? countryId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _unitOfWork.SiteVisits.Query()
            .Include(sv => sv.Country)
            .AsQueryable();

        if (countryId.HasValue)
            query = query.Where(sv => sv.CountryId == countryId.Value);

        if (dateFrom.HasValue)
            query = query.Where(sv => sv.VisitedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(sv => sv.VisitedAt <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var data = await query
            .OrderByDescending(sv => sv.VisitedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sv => new
            {
                sv.Id,
                CountryName = sv.Country.Name,
                sv.Page,
                sv.VisitedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
