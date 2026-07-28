using Api.Middlewares;
using Application.Common.Interfaces;
using Infrastructure.Services;
using Infrastructure;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==================== Serilog ====================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// ==================== QuestPDF ====================
QuestPDF.Settings.License = LicenseType.Community;

// ==================== PostgreSQL ====================

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=loxxking_db;Username=superior;Password=Superior 2004";

Console.WriteLine("========== DATABASE ==========");
Console.WriteLine(connectionString);

if (!string.IsNullOrWhiteSpace(connectionString) &&
    (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
     connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
{
    var uri = new Uri(connectionString);

    var userInfo = uri.UserInfo.Split(':', 2);

    connectionString =
        $"Host={uri.Host};" +
        $"Port={uri.Port};" +
        $"Database={uri.AbsolutePath.TrimStart('/')};" +
        $"Username={userInfo[0]};" +
        $"Password={userInfo[1]};" +
        $"SSL Mode=Require;" +
        $"Trust Server Certificate=true";

    Console.WriteLine("Converted:");
    Console.WriteLine(connectionString);
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ==================== Redis ====================

var redisConnection =
    Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection")
    ?? builder.Configuration.GetConnectionString("RedisConnection")
    ?? "localhost:6379";

Console.WriteLine("========== REDIS ==========");
Console.WriteLine(redisConnection);

if (!string.IsNullOrWhiteSpace(redisConnection) &&
    redisConnection.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
{
    var uri = new Uri(redisConnection);

    string password = "";

    if (!string.IsNullOrEmpty(uri.UserInfo))
    {
        var split = uri.UserInfo.Split(':', 2);

        if (split.Length == 2)
            password = split[1];
    }

    redisConnection =
        $"{uri.Host}:{uri.Port},password={password}";
}

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
});

// ==================== DI ====================
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
builder.Services.AddScoped<IInvoicePdfGenerator, QuestPdfInvoiceGenerator>();
builder.Services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();

// ==================== Notification Service (Email + WhatsApp) ====================
builder.Services.AddHttpClient("callmebot", c =>
{
    c.BaseAddress = new Uri("https://api.callmebot.com");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IOrderNotificationService, OrderNotificationService>();

// ==================== GeoLocation Service ====================
builder.Services.AddGeoLocationService(builder.Configuration);

// ==================== Controllers ====================

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddResponseCompression();

// ==================== Rate Limiting ====================

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 1000,
                    QueueLimit = 100
                }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ==================== JWT ====================

var jwtSecret =
    Environment.GetEnvironmentVariable("Jwt__Secret")
    ?? builder.Configuration["Jwt:Secret"]
    ?? "LOXX_KING_SUPER_SECRET_KEY_32BYTES_LONG_MINIMUM";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),

                ValidIssuer =
                    Environment.GetEnvironmentVariable("Jwt__Issuer")
                    ?? builder.Configuration["Jwt:Issuer"]
                    ?? "LoxxKingApi",

                ValidAudience =
                    Environment.GetEnvironmentVariable("Jwt__Audience")
                    ?? builder.Configuration["Jwt:Audience"]
                    ?? "LoxxKingClient",

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret))
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ==================== Migration ====================

if ((Environment.GetEnvironmentVariable("RUN_MIGRATIONS") ?? "false")
    .Equals("true", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();

    await DbSeeder.SeedCountriesAsync(db);
    await DbSeeder.SeedAdminAsync(db);
}

// ==================== Middleware ====================

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<GeoLocationMiddleware>();

// CORS must be first — before HttpsRedirection and Auth — so OPTIONS preflight succeeds
app.UseCors("AllowAll");

app.UseResponseCompression();

// Skip HTTPS redirect in development so the frontend can talk to http://localhost:5196
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
