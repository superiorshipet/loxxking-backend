using Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Services;

public class IpApiGeoLocationService : IGeoLocationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IpApiGeoLocationService> _logger;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24);

    public IpApiGeoLocationService(
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache,
        ILogger<IpApiGeoLocationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetCountryCodeFromIpAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var info = await GetLocationInfoAsync(ipAddress, cancellationToken);
        return info?.CountryCode;
    }

    public async Task<string?> GetCountryNameFromIpAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        var info = await GetLocationInfoAsync(ipAddress, cancellationToken);
        return info?.CountryName;
    }

    public async Task<GeoLocationInfo?> GetLocationInfoAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        // Skip for localhost
        if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress == "localhost")
        {
            return new GeoLocationInfo
            {
                CountryCode = "EG",
                CountryName = "Egypt",
                Currency = "EGP"
            };
        }

        var cacheKey = $"geo:{ipAddress}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<GeoLocationInfo>(cached);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://ip-api.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "LoxxKing/1.0");

            var response = await client.GetStringAsync(
                $"json/{ipAddress}?fields=status,message,country,countryCode,region,city,timezone,currency", 
                cancellationToken);
            
            var data = JsonSerializer.Deserialize<IpApiResponse>(response);

            if (data is null || data.Status != "success")
            {
                _logger.LogWarning("Failed to get location for IP {IpAddress}: {Message}", ipAddress, data?.Message);
                return null;
            }

            var info = new GeoLocationInfo
            {
                CountryCode = data.CountryCode,
                CountryName = data.Country,
                City = data.City,
                Region = data.Region,
                Timezone = data.Timezone,
                Currency = data.Currency
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(info), 
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheDuration }, 
                cancellationToken);

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location for IP {IpAddress}", ipAddress);
            return null;
        }
    }

    private class IpApiResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string CountryCode { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Timezone { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
    }
}
