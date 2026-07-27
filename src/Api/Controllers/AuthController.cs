using Api.DTOs.Auth;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
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
            Role = UserRole.Customer,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var accessToken = _jwtGenerator.GenerateAccessToken(user);
        var refreshToken = _jwtGenerator.GenerateRefreshToken();

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1), // Default 1 hour expiry
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role.ToString(),
                Country = country.Name,
                CreatedAt = user.CreatedAt
            }
        };

        return Ok(response);
    }

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

        // Generate tokens
        var accessToken = _jwtGenerator.GenerateAccessToken(user);
        var refreshToken = _jwtGenerator.GenerateRefreshToken();

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1), // Default 1 hour expiry
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
}
