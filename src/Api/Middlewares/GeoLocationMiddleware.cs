using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Middlewares;

public class GeoLocationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GeoLocationMiddleware> _logger;

    public GeoLocationMiddleware(RequestDelegate next, ILogger<GeoLocationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IGeoLocationService geoLocationService, IDistributedCache cache)
    {
        // Get client IP
        var ipAddress = GetClientIp(context);
        
        if (!string.IsNullOrEmpty(ipAddress))
        {
            // Try to get location from cache first
            var cacheKey = $"user_geo:{ipAddress}";
            var cached = await cache.GetStringAsync(cacheKey);
            
            if (!string.IsNullOrEmpty(cached))
            {
                // Add to request headers for controllers to use
                context.Request.Headers["X-Geo-Country"] = cached;
            }
            else
            {
                // Get location from service
                var location = await geoLocationService.GetCountryNameFromIpAsync(ipAddress);
                if (!string.IsNullOrEmpty(location))
                {
                    await cache.SetStringAsync(cacheKey, location, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                    });
                    context.Request.Headers["X-Geo-Country"] = location;
                }
            }
        }

        await _next(context);
    }

    private string? GetClientIp(HttpContext context)
    {
        // Check for forwarded IP (for proxies/load balancers)
        var forwardedHeader = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedHeader))
        {
            return forwardedHeader.Split(',').First().Trim();
        }

        // Check for Cloudflare
        var cfConnectingIp = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(cfConnectingIp))
        {
            return cfConnectingIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
