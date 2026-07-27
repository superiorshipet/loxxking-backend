using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedCountriesAsync(AppDbContext context)
    {
        if (await context.Countries.AnyAsync())
            return;

        var countries = new List<Country>
        {
            new() { Id = Guid.NewGuid(), Name = "Egypt", Currency = "EGP", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Iraq", Currency = "IQD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Saudi Arabia", Currency = "SAR", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "UAE", Currency = "AED", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Kuwait", Currency = "KWD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Qatar", Currency = "QAR", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Bahrain", Currency = "BHD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Oman", Currency = "OMR", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Yemen", Currency = "YER", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Jordan", Currency = "JOD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Lebanon", Currency = "LBP", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Palestine", Currency = "ILS", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Syria", Currency = "SYP", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Libya", Currency = "LYD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Tunisia", Currency = "TND", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Algeria", Currency = "DZD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Morocco", Currency = "MAD", DefaultLanguage = "ar" }
        };

        await context.Countries.AddRangeAsync(countries);
        await context.SaveChangesAsync();
    }

    public static async Task SeedAdminAsync(AppDbContext context)
    {
        // Check if admin already exists
        var adminExists = await context.Users.AnyAsync(u => u.Role == UserRole.Admin);
        if (adminExists)
            return;

        // Get Egypt country
        var egypt = await context.Countries.FirstOrDefaultAsync(c => c.Name == "Egypt");
        if (egypt is null)
            return;

        // Create default admin - using BCrypt.Net.BCrypt
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin@123456");

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Name = "Super Admin",
            Email = "admin@loxxking.com",
            Phone = "01000000000",
            PasswordHash = hashedPassword,
            CountryId = egypt.Id,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();
    }
}
