namespace Application.Common.Interfaces;

public record OrderNotificationData(
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    string Address,
    string Country,
    string PaymentMethod,
    decimal TotalAmount,
    List<OrderNotificationItem> Items,
    DateTime CreatedAt
);

public record OrderNotificationItem(string ProductName, int Quantity, decimal UnitPrice);

public interface IOrderNotificationService
{
    /// <summary>
    /// Sends invoice email to the business owner and a WhatsApp alert.
    /// Fire-and-forget safe — exceptions are logged, never rethrown.
    /// </summary>
    Task NotifyNewOrderAsync(OrderNotificationData order, CancellationToken cancellationToken = default);
}
