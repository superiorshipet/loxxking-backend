using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<User> Users { get; }
    IRepository<Country> Countries { get; }
    IRepository<Category> Categories { get; }
    IRepository<Product> Products { get; }
    IRepository<ProductPrice> ProductPrices { get; }
    IRepository<Inventory> Inventories { get; }
    IRepository<Order> Orders { get; }
    IRepository<OrderItem> OrderItems { get; }
    IRepository<OrderEditLog> OrderEditLogs { get; }
    IRepository<Invoice> Invoices { get; }
    IRepository<Review> Reviews { get; }
    IRepository<SupportMessage> SupportMessages { get; }
    IRepository<Notification> Notifications { get; }
    IRepository<SiteVisit> SiteVisits { get; }
    IRepository<Offer> Offers { get; }
    IRepository<BankTransfer> BankTransfers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
