using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
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
    public DbSet<SupportConversation> SupportConversations => Set<SupportConversation>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Order indexes
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Status);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CountryId);

        // User indexes
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Phone)
            .IsUnique();

        // Product indexes
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.CategoryId);

        modelBuilder.Entity<ProductPrice>()
            .HasIndex(pp => new { pp.ProductId, pp.CountryId })
            .IsUnique();

        modelBuilder.Entity<Inventory>()
            .HasIndex(i => new { i.ProductId, i.CountryId })
            .IsUnique();

        modelBuilder.Entity<Review>()
            .HasIndex(r => r.ProductId);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.UserId);

        modelBuilder.Entity<BankTransfer>()
            .HasIndex(bt => bt.OrderId);

        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.OrderId)
            .IsUnique();

        // SupportMessage: index on ConversationId for fast thread lookup (no FK - supports guests)
        modelBuilder.Entity<SupportMessage>(entity =>
        {
            entity.HasIndex(sm => sm.ConversationId);
            entity.Property(sm => sm.SenderType).HasMaxLength(20).HasDefaultValue("Customer");
            entity.Property(sm => sm.SenderName).HasMaxLength(100).HasDefaultValue("Customer");
        });

        // Order relationships
        modelBuilder.Entity<Order>()
            .HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Country)
            .WithMany()
            .HasForeignKey(o => o.CountryId);

        modelBuilder.Entity<WishlistItem>(entity =>
        {
            // One wishlist entry per user per product
            entity.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
            entity.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(w => w.Product).WithMany().HasForeignKey(w => w.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

    }
}
