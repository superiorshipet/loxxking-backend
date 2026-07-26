namespace Api.Middlewares;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;

public class GlobalExceptionHandlingMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger) {
        _next = next;
        _logger = logger;
    }
    public async Task InvokeAsync(HttpContext context) {
        try { await _next(context); }
        catch (Exception ex) {
            _logger.LogError(ex, "Unhandled performance exception caught securely.");
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Internal Server Error" }));
        }
    }
}
