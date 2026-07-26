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
