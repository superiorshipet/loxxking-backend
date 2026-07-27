using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(IUnitOfWork unitOfWork, IJwtTokenGenerator jwtTokenGenerator)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public record RegisterRequest(string Name, string Email, string Phone, string Password, string CountryName);
    public record LoginRequest(string Email, string Password);
    public record RefreshRequest(string RefreshToken);
    public record GoogleLoginRequest(string IdToken);

    [HttpGet("countries")]
    public async Task<IActionResult> GetCountries(CancellationToken cancellationToken)
    {
        var countries = await _unitOfWork.Countries.Query()
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        return Ok(countries);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var emailExists = await _unitOfWork.Users.Query().AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
            return Conflict(new { message = "Email already registered." });

        var country = await _unitOfWork.Countries.Query()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CountryName.ToLower(), cancellationToken);

        if (country is null)
            return BadRequest(new { message = "Invalid country name. See GET /api/auth/countries for valid names." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Customer,
            CountryId = country.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        user.Country = country;
        return await IssueTokensAsync(user, cancellationToken);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        return await IssueTokensAsync(user, cancellationToken);
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);
        }
        catch (Exception)
        {
            return BadRequest(new { message = "Invalid Google token." });
        }

        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .FirstOrDefaultAsync(u => u.Email == payload.Email, cancellationToken);

        if (user is null)
        {
            var defaultCountry = await _unitOfWork.Countries.Query().FirstOrDefaultAsync(cancellationToken);
            if (defaultCountry is null)
                return StatusCode(500, new { message = "No default country configured in the system." });

            user = new User
            {
                Id = Guid.NewGuid(),
                Name = payload.Name ?? "Google User",
                Email = payload.Email,
                Phone = string.Empty,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Role = UserRole.Customer,
                CountryId = defaultCountry.Id,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            user.Country = defaultCountry;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var candidates = await _unitOfWork.Users.Query()
            .Where(u => u.RefreshTokenHash != null && u.RefreshTokenExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var user = candidates.FirstOrDefault(u =>
            BCrypt.Net.BCrypt.Verify(request.RefreshToken, u.RefreshTokenHash));

        if (user is null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        user = await _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await _unitOfWork.Users.Query()
            .Include(u => u.Country)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email,
            Role = user.Role.ToString(),
            Country = user.Country?.Name
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var id))
            return Unauthorized();

        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken);
        if (user is not null)
        {
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiresAt = null;
            _unitOfWork.Users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private async Task<IActionResult> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(7);
        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            accessToken,
            refreshToken,
            user = new
            {
                user.Id,
                user.Name,
                user.Email,
                Role = user.Role.ToString(),
                Country = user.Country?.Name
            }
        });
    }
}
