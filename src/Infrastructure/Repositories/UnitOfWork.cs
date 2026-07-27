using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        Users = new Repository<User>(dbContext);
        Countries = new Repository<Country>(dbContext);
        Categories = new Repository<Category>(dbContext);
        Products = new Repository<Product>(dbContext);
        ProductPrices = new Repository<ProductPrice>(dbContext);
        Inventories = new Repository<Inventory>(dbContext);
        Orders = new Repository<Order>(dbContext);
        OrderItems = new Repository<OrderItem>(dbContext);
        OrderEditLogs = new Repository<OrderEditLog>(dbContext);
        Invoices = new Repository<Invoice>(dbContext);
        Reviews = new Repository<Review>(dbContext);
        SupportMessages = new Repository<SupportMessage>(dbContext);
        Notifications = new Repository<Notification>(dbContext);
        SiteVisits = new Repository<SiteVisit>(dbContext);
        Offers = new Repository<Offer>(dbContext);
        BankTransfers = new Repository<BankTransfer>(dbContext);
        SupportConversations = new Repository<SupportConversation>(dbContext);
    }

    public IRepository<User> Users { get; }
    public IRepository<Country> Countries { get; }
    public IRepository<Category> Categories { get; }
    public IRepository<Product> Products { get; }
    public IRepository<ProductPrice> ProductPrices { get; }
    public IRepository<Inventory> Inventories { get; }
    public IRepository<Order> Orders { get; }
    public IRepository<OrderItem> OrderItems { get; }
    public IRepository<OrderEditLog> OrderEditLogs { get; }
    public IRepository<Invoice> Invoices { get; }
    public IRepository<Review> Reviews { get; }
    public IRepository<SupportMessage> SupportMessages { get; }
    public IRepository<Notification> Notifications { get; }
    public IRepository<SiteVisit> SiteVisits { get; }
    public IRepository<Offer> Offers { get; }
    public IRepository<BankTransfer> BankTransfers { get; }
    public IRepository<SupportConversation> SupportConversations { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);
}
