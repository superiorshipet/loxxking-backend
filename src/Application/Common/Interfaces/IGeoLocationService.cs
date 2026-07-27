namespace Application.Common.Interfaces;

public interface IGeoLocationService
{
    Task<string?> GetCountryCodeFromIpAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task<string?> GetCountryNameFromIpAsync(string ipAddress, CancellationToken cancellationToken = default);
    Task<GeoLocationInfo?> GetLocationInfoAsync(string ipAddress, CancellationToken cancellationToken = default);
}

public class GeoLocationInfo
{
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Timezone { get; set; }
    public string? Currency { get; set; }
}
