using Api.DTOs.Common;
using Api.DTOs.Users;
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

    [HttpPost("admin/create-manager")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateStoreManager([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        return await CreateStaff(request, UserRole.StoreManager, cancellationToken);
    }

    [HttpPost("staff/create-employee")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> CreateSalesEmployee([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        return await CreateStaff(request, UserRole.SalesEmployee, cancellationToken);
    }

    private async Task<IActionResult> CreateStaff(CreateStaffRequest request, UserRole role, CancellationToken cancellationToken)
    {
        var existingEmail = await _unitOfWork.Users.Query()
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);
        
        if (existingEmail)
            return BadRequest(new { message = "Email already registered." });

        var existingPhone = await _unitOfWork.Users.Query()
            .AnyAsync(u => u.Phone == request.Phone, cancellationToken);
        
        if (existingPhone)
            return BadRequest(new { message = "Phone number already registered." });

        var country = await _unitOfWork.Countries.Query()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CountryName.ToLower(), cancellationToken);

        if (country is null)
            return BadRequest(new { message = $"Country '{request.CountryName}' not found." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CountryId = country.Id,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            .Select(u => new StaffResponse
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                Role = u.Role.ToString(),
                Country = u.Country.Name,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var response = new PaginatedResponse<StaffResponse>
        {
            Data = staff,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Ok(response);
    }

    [HttpPatch("staff/{id}/toggle-status")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> ToggleStaffStatus(Guid id, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        
        if (user is null)
            return NotFound(new { message = "User not found." });

        var currentUserId = GetCurrentUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Cannot deactivate your own account." });

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

    [HttpDelete("admin/{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        
        if (user is null)
            return NotFound(new { message = "User not found." });

        var currentUserId = GetCurrentUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Cannot delete your own account." });

        if (user.Role == UserRole.Admin)
            return BadRequest(new { message = "Cannot delete another Admin." });

        _unitOfWork.Users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
            return NotFound(new { message = "User not found." });

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters long." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = NotificationType.AccountUpdate,
            Message = "Your password has been changed successfully.",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Password changed successfully." });
    }

    [HttpPost("staff/reset-password")]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> ResetStaffPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return NotFound(new { message = "User not found." });

        if (user.Role != UserRole.StoreManager && user.Role != UserRole.SalesEmployee)
            return BadRequest(new { message = "Can only reset password for staff members." });

        var currentUserId = GetCurrentUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Use 'change-password' endpoint to change your own password." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters long." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = NotificationType.AccountUpdate,
            Message = $"Your password has been reset by {User.Identity?.Name ?? "Admin"}.",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Password reset successfully for {user.Name}." });
    }

    [HttpPost("admin/change-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminChangePassword([FromBody] AdminChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return NotFound(new { message = "User not found." });

        var currentUserId = GetCurrentUserId();
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Use 'change-password' endpoint to change your own password." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters long." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _unitOfWork.Notifications.AddAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = NotificationType.AccountUpdate,
            Message = $"Your password has been changed by an Administrator.",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"Password changed successfully for {user.Name}." });
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
