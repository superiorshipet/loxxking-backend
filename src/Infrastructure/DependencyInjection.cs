using Application.Common.Interfaces;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGeoLocationService(this IServiceCollection services, IConfiguration configuration)
    {
        // Register HttpClientFactory and service
        services.AddHttpClient();
        services.AddScoped<IGeoLocationService, IpApiGeoLocationService>();
        
        return services;
    }
}
