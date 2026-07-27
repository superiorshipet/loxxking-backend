using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public UsersController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public record CreateStaffRequest(
        string Name,
        string Email,
        string Phone,
        string Password,
        string CountryName
    );

    /// <summary>
    /// Admin only - Create a Store Manager
    /// </summary>
    [HttpPost("admin/create-manager")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateStoreManager([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        return await CreateStaff(request, UserRole.StoreManager, cancellationToken);
    }

    /// <summary>
    /// Admin or Store Manager - Create a Sales Employee
    /// </summary>
    [HttpPost("staff/create-employee")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> CreateSalesEmployee([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        return await CreateStaff(request, UserRole.SalesEmployee, cancellationToken);
    }

    private async Task<IActionResult> CreateStaff(CreateStaffRequest request, UserRole role, CancellationToken cancellationToken)
    {
        // Validate email uniqueness
        var existingEmail = await _unitOfWork.Users.Query()
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);
        
        if (existingEmail)
            return BadRequest(new { message = "Email already registered." });

        // Validate phone uniqueness
        var existingPhone = await _unitOfWork.Users.Query()
            .AnyAsync(u => u.Phone == request.Phone, cancellationToken);
        
        if (existingPhone)
            return BadRequest(new { message = "Phone number already registered." });

        // Get country
        var country = await _unitOfWork.Countries.Query()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CountryName.ToLower(), cancellationToken);

        if (country is null)
            return BadRequest(new { message = $"Country '{request.CountryName}' not found." });

        // Hash password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = hashedPassword,
            CountryId = country.Id,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification to the new user (optional)
        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = NotificationType.AccountUpdate,
            Message = $"Welcome! You have been added as a {role}.",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            Role = user.Role.ToString(),
            Country = country.Name,
            Message = $"{role} created successfully."
        });
    }

    /// <summary>
    /// Get all staff (Store Managers + Sales Employees) - Admin or Store Manager
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetStaff(
        [FromQuery] UserRole? role = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .Where(u => u.Role == UserRole.StoreManager || u.Role == UserRole.SalesEmployee);

        if (role.HasValue)
            query = query.Where(u => u.Role == role.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.Name.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s) ||
                u.Phone.Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var staff = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Phone,
                Role = u.Role.ToString(),
                Country = u.Country.Name,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = staff,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Admin or Store Manager - Toggle user active status (deactivate/reactivate)
    /// </summary>
    [HttpPatch("staff/{id}/toggle-status")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> ToggleStaffStatus(Guid id, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        
        if (user is null)
            return NotFound(new { message = "User not found." });

        // Prevent deactivating self
        var currentUserId = GetCurrentUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Cannot deactivate your own account." });

        // Only allow toggling for StoreManager and SalesEmployee
        if (user.Role != UserRole.StoreManager && user.Role != UserRole.SalesEmployee)
            return BadRequest(new { message = "Can only toggle status for staff members." });

        user.IsActive = !user.IsActive;
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var status = user.IsActive ? "activated" : "deactivated";
        return Ok(new 
        { 
            user.Id, 
            user.Name,
            user.IsActive,
            Message = $"User {status} successfully."
        });
    }

    /// <summary>
    /// Admin only - Delete a user
    /// </summary>
    [HttpDelete("admin/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        
        if (user is null)
            return NotFound(new { message = "User not found." });

        // Prevent deleting self
        var currentUserId = GetCurrentUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Cannot delete your own account." });

        // Don't allow deleting other Admins
        if (user.Role == UserRole.Admin)
            return BadRequest(new { message = "Cannot delete another Admin." });

        _unitOfWork.Users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
