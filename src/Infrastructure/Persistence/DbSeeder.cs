using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedCountriesAsync(AppDbContext dbContext)
    {
        var alreadySeeded = await dbContext.Countries.AnyAsync();
        if (alreadySeeded)
            return;

        var countries = new List<Country>
        {
            new() { Id = Guid.NewGuid(), Name = "Egypt", Currency = "EGP", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Iraq", Currency = "IQD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Saudi Arabia", Currency = "SAR", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "United Arab Emirates", Currency = "AED", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Kuwait", Currency = "KWD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Qatar", Currency = "QAR", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Bahrain", Currency = "BHD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Oman", Currency = "OMR", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Jordan", Currency = "JOD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Lebanon", Currency = "LBP", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Syria", Currency = "SYP", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Libya", Currency = "LYD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Morocco", Currency = "MAD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Algeria", Currency = "DZD", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Tunisia", Currency = "TND", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Sudan", Currency = "SDG", DefaultLanguage = "ar" },
            new() { Id = Guid.NewGuid(), Name = "Turkey", Currency = "TRY", DefaultLanguage = "tr" },
        };

        await dbContext.Countries.AddRangeAsync(countries);
        await dbContext.SaveChangesAsync();
    }
}
