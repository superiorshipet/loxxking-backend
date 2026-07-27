using Api.Middlewares;
using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

// Database - Read from environment variable
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string is required");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Redis Cache - Read from environment variable
var redisConnection = builder.Configuration.GetConnectionString("RedisConnection") 
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__RedisConnection")
    ?? "localhost:6379";

builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = redisConnection;
});

// Services (DI)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
builder.Services.AddScoped<IInvoicePdfGenerator, QuestPdfInvoiceGenerator>();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS - Read from environment
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Environment.GetEnvironmentVariable("Cors__AllowedOrigins")?.Split(',')
    ?? new[] { 
        "http://localhost:5173", 
        "http://localhost:3000",
        "https://loxxking.vercel.app",
        "https://loxxking.netlify.app"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("LoxxKingCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Response compression
builder.Services.AddResponseCompression();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 100,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] 
    ?? Environment.GetEnvironmentVariable("Jwt__Secret")
    ?? throw new InvalidOperationException("JWT Secret is required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LoxxKingApi",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LoxxKingClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Migration + Seed (only if in development or with ENV flag)
if (app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("RUN_MIGRATIONS") == "true")
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        await DbSeeder.SeedCountriesAsync(dbContext);
        await DbSeeder.SeedAdminAsync(dbContext);
    }
}

// Middleware pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Loxx King API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseCors("LoxxKingCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
