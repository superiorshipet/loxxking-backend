#!/usr/bin/env bash
set -e

echo "=== Initializing Loxx King E-Commerce Clean Architecture Backend (.NET 10) ==="

dotnet new sln -n LoxxKingApi
dotnet new classlib -n Domain -o src/Domain
dotnet new classlib -n Application -o src/Application
dotnet new classlib -n Infrastructure -o src/Infrastructure
dotnet new webapi -n Api -o src/Api

dotnet sln LoxxKingApi.sln add src/Domain/Domain.csproj src/Application/Application.csproj src/Infrastructure/Infrastructure.csproj src/Api/Api.csproj

dotnet add src/Application/Application.csproj reference src/Domain/Domain.csproj
dotnet add src/Infrastructure/Infrastructure.csproj reference src/Application/Application.csproj
dotnet add src/Api/Api.csproj reference src/Infrastructure/Infrastructure.csproj src/Application/Application.csproj

dotnet add src/Application/Application.csproj package MediatR
dotnet add src/Application/Application.csproj package FluentValidation.DependencyInjectionExtensions
dotnet add src/Infrastructure/Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/Infrastructure/Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Infrastructure/Infrastructure.csproj package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add src/Api/Api.csproj package Serilog.AspNetCore
dotnet add src/Api/Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer

mkdir -p src/Domain/Entities src/Domain/Enums src/Domain/Events
mkdir -p src/Infrastructure/Persistence src/Infrastructure/Services src/Api/Middlewares

cat << 'EOF2' > src/Domain/Enums/Enums.cs
namespace Domain.Enums;
public enum UserRole { Admin, StoreManager, SalesEmployee, Customer }
public enum OrderStatus { NewOrder, PendingApproval, Prepared, Shipping, Delivered, Cancelled, Incomplete }
public enum PaymentMethod { Cash, BankTransfer }
public enum ReviewStatus { Visible, Hidden }
public enum NotificationType { OrderUpdate, SiteVisit, NewReview, ChatMessage }
public enum BankTransferStatus { Pending, Confirmed, Rejected }
EOF2

cat << 'EOF2' > src/Domain/Entities/Entities.cs
namespace Domain.Entities;
public enum UserRole { Admin, StoreManager, SalesEmployee, Customer }
public enum OrderStatus { NewOrder, PendingApproval, Prepared, Shipping, Delivered, Cancelled, Incomplete }
public enum PaymentMethod { Cash, BankTransfer }
public enum ReviewStatus { Visible, Hidden }
public enum NotificationType { OrderUpdate, SiteVisit, NewReview, ChatMessage }
public enum BankTransferStatus { Pending, Confirmed, Rejected }

public class User {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Country {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string Currency { get; set; } = default!;
    public string DefaultLanguage { get; set; } = "en";
}
public class Category {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
}
public class Product {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Description { get; set; } = default!;
    public decimal BasePrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class ProductPrice {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public decimal Price { get; set; }
}
public class Inventory {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class Order {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public User Customer { get; set; } = default!;
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public OrderStatus Status { get; set; } = OrderStatus.NewOrder;
    public PaymentMethod PaymentMethod { get; set; }
    public string? ShipmentCode { get; set; }
    public string Address { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
public class OrderItem {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal PriceAtOrder { get; set; }
}
public class OrderEditLog {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid EditedBy { get; set; }
    public User Editor { get; set; } = default!;
    public string FieldName { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
public class Invoice {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string InvoiceNumber { get; set; } = default!;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
public class Review {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public int Rating { get; set; }
    public string Comment { get; set; } = default!;
    public ReviewStatus Status { get; set; } = ReviewStatus.Visible;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class SupportMessage {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = default!;
    public Guid? RelatedReviewId { get; set; }
    public Review? RelatedReview { get; set; }
    public string Message { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class Notification {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public NotificationType Type { get; set; }
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class SiteVisit {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = default!;
    public string Page { get; set; } = default!;
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
}
public class Offer {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public decimal DiscountPercent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
public class BankTransfer {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string ProofImage { get; set; } = default!;
    public BankTransferStatus Status { get; set; } = BankTransferStatus.Pending;
}
EOF2

cat << 'EOF2' > src/Infrastructure/Persistence/AppDbContext.cs
namespace Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;

public class AppDbContext : DbContext {
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderEditLog> OrderEditLogs => Set<OrderEditLog>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SiteVisit> SiteVisits => Set<SiteVisit>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<BankTransfer> BankTransfers => Set<BankTransfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Order>().HasIndex(o => o.Status);
        modelBuilder.Entity<Order>().HasIndex(o => o.CountryId);
        modelBuilder.Entity<Order>().HasIndex(o => o.CustomerId);
        modelBuilder.Entity<Product>().HasIndex(p => p.CategoryId);
        modelBuilder.Entity<ProductPrice>().HasIndex(pp => new { pp.ProductId, pp.CountryId }).IsUnique();
        modelBuilder.Entity<Inventory>().HasIndex(i => new { i.ProductId, i.CountryId }).IsUnique();
        modelBuilder.Entity<Review>().HasIndex(r => r.ProductId);
        modelBuilder.Entity<Notification>().HasIndex(n => n.UserId);
    }
}
EOF2

cat << 'EOF2' > src/Infrastructure/Services/RedisCacheService.cs
namespace Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

public class RedisCacheService {
    private readonly IDistributedCache _cache;
    public RedisCacheService(IDistributedCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) {
        var val = await _cache.GetStringAsync(key, cancellationToken);
        return val is null ? default : JsonSerializer.Deserialize<T>(val);
    }
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) {
        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue) options.SetAbsoluteExpiration(expiration.Value);
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options, cancellationToken);
    }
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        await _cache.RemoveAsync(key, cancellationToken);
}
EOF2

cat << 'EOF2' > src/Api/Middlewares/GlobalExceptionHandlingMiddleware.cs
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
            _logger.LogError(ex, "Unhandled performance exception caught safely.");
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Internal Server Error" }));
        }
    }
}
EOF2

cat << 'EOF2' > src/Api/Program.cs
using Api.Middlewares;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection") ?? "localhost:6379";
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

builder.Services.AddRateLimiter(options => {
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "loxx-client",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 200, Window = TimeSpan.FromMinutes(1) }));
});

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "LOXX_KING_SUPER_SECRET_KEY_32BYTES_LONG_MINIMUM";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
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

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
EOF2

echo "=== Loxx King Backend Fully Generated Successfully via Terminal cat! ==="
