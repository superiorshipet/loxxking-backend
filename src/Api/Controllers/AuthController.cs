using Api.DTOs.Auth;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public AuthController(IUnitOfWork unitOfWork, IJwtTokenGenerator jwtGenerator)
    {
        _unitOfWork = unitOfWork;
        _jwtGenerator = jwtGenerator;
    }

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
    {
        var countries = await _unitOfWork.Countries.Query()
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }

    // REGISTER REMOVED - Staff accounts created by Admin only via UsersController

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.IsActive)
            return Unauthorized(new { message = "Account is deactivated. Please contact support." });

        var accessToken = _jwtGenerator.GenerateAccessToken(user);
        var refreshToken = _jwtGenerator.GenerateRefreshToken();

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                Country = user.Country.Name,
                CreatedAt = user.CreatedAt
            }
        };

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("nameid")?.Value 
            ?? User.FindFirst("sub")?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(new { message = "Invalid token: No user ID found." });

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid user ID in token." });

        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return NotFound(new { message = "User not found in database." });

        return Ok(new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role.ToString(),
            Country = user.Country.Name,
            CreatedAt = user.CreatedAt
        });
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("nameid")?.Value 
            ?? User.FindFirst("sub")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
