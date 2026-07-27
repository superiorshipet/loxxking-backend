using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public OrdersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public record OrderItemRequest(Guid ProductId, int Quantity);
    public record CreateOrderRequest(string Address, string Phone, string? Notes, PaymentMethod PaymentMethod, List<OrderItemRequest> Items);
    public record UpdateOrderStatusRequest(OrderStatus Status);
    public record UpdateOrderRequest(string Address, string Phone, string? Notes);

    // ------------------------------------------------------------
    // GET /api/orders?status=&countryId=&paymentMethod=&search=&dateFrom=&dateTo=&page=&pageSize=
    // متاح لـ Admin, StoreManager, SalesEmployee (لوحة التحكم الداخلية)
    // ------------------------------------------------------------
    [HttpGet]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] Guid? countryId,
        [FromQuery] PaymentMethod? paymentMethod,
        [FromQuery] string? search,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.Country)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (countryId.HasValue)
            query = query.Where(o => o.CountryId == countryId.Value);

        if (paymentMethod.HasValue)
            query = query.Where(o => o.PaymentMethod == paymentMethod.Value);

        if (dateFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(o => o.CreatedAt <= dateTo.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(o =>
                o.Phone.ToLower().Contains(s) ||
                o.Address.ToLower().Contains(s) ||
                (o.ShipmentCode != null && o.ShipmentCode.ToLower().Contains(s)) ||
                o.Customer.Name.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var data = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                CustomerName = o.Customer.Name,
                o.Phone,
                o.Address,
                Country = o.Country.Name,
                Status = o.Status.ToString(),
                PaymentMethod = o.PaymentMethod.ToString(),
                o.ShipmentCode,
                o.TotalAmount,
                o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    // ------------------------------------------------------------
    // GET /api/orders/{id}
    // متاح للموظفين، وللعميل صاحب الطلب نفسه بس
    // ------------------------------------------------------------
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.Country)
            .Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        if (!IsStaff() && order.CustomerId != GetCurrentUserId())
            return Forbid();

        return Ok(new
        {
            order.Id,
            Customer = new { order.Customer.Id, order.Customer.Name, order.Customer.Phone },
            Country = order.Country.Name,
            Status = order.Status.ToString(),
            PaymentMethod = order.PaymentMethod.ToString(),
            order.ShipmentCode,
            order.Address,
            order.Phone,
            order.Notes,
            order.TotalAmount,
            order.CreatedAt,
            Items = order.OrderItems.Select(i => new
            {
                i.Id,
                ProductName = i.Product.NameEn,
                i.Quantity,
                i.PriceAtOrder
            })
        });
    }

    // ------------------------------------------------------------
    // POST /api/orders — العميل بينشئ طلب
    // ------------------------------------------------------------
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Order must contain at least one item." });

        var customerId = GetCurrentUserId();
        var customer = await _dbContext.Users.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer is null)
            return Unauthorized();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CountryId = customer.CountryId,
            Status = OrderStatus.NewOrder,
            PaymentMethod = request.PaymentMethod,
            Address = request.Address,
            Phone = request.Phone,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var item in request.Items)
        {
            var price = await _dbContext.ProductPrices
                .Where(p => p.ProductId == item.ProductId && p.CountryId == customer.CountryId)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (price is null)
                return BadRequest(new { message = $"Product {item.ProductId} has no price for your country." });

            var inventory = await _dbContext.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.CountryId == customer.CountryId, cancellationToken);

            if (inventory is null || inventory.Quantity < item.Quantity)
                return BadRequest(new { message = $"Insufficient stock for product {item.ProductId}." });

            inventory.Quantity -= item.Quantity;
            inventory.UpdatedAt = DateTime.UtcNow;

            order.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                PriceAtOrder = price.Value
            });

            total += price.Value * item.Quantity;
        }

        order.TotalAmount = total;
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { order.Id, order.TotalAmount });
    }

    // ------------------------------------------------------------
    // PATCH /api/orders/{id}/status — Admin, StoreManager, SalesEmployee
    // ------------------------------------------------------------
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
            return NotFound();

        var oldStatus = order.Status;
        order.Status = request.Status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // إشعار تلقائي للعميل بتغيير حالة طلبه
        if (oldStatus != request.Status)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = order.CustomerId,
                Type = NotificationType.OrderUpdate,
                Message = $"Your order status changed to {request.Status}.",
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { order.Id, Status = order.Status.ToString() });
    }

    // ------------------------------------------------------------
    // PUT /api/orders/{id} — Admin ONLY. كل تغيير بيتسجل في ORDER_EDIT_LOG
    // ------------------------------------------------------------
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
            return NotFound();

        var editorId = GetCurrentUserId();
        var now = DateTime.UtcNow;

        void LogIfChanged(string fieldName, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            _dbContext.OrderEditLogs.Add(new OrderEditLog
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                EditedBy = editorId,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                EditedAt = now
            });
        }

        LogIfChanged(nameof(order.Address), order.Address, request.Address);
        LogIfChanged(nameof(order.Phone), order.Phone, request.Phone);
        LogIfChanged(nameof(order.Notes), order.Notes, request.Notes);

        order.Address = request.Address;
        order.Phone = request.Phone;
        order.Notes = request.Notes;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { order.Id, message = "Order updated and changes logged." });
    }

    // ------------------------------------------------------------
    // GET /api/orders/{id}/edit-logs — Admin ONLY
    // ------------------------------------------------------------
    [HttpGet("{id}/edit-logs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetEditLogs(Guid id, CancellationToken cancellationToken)
    {
        var logs = await _dbContext.OrderEditLogs
            .Include(l => l.Editor)
            .Where(l => l.OrderId == id)
            .OrderByDescending(l => l.EditedAt)
            .Select(l => new
            {
                l.Id,
                l.FieldName,
                l.OldValue,
                l.NewValue,
                EditedBy = l.Editor.Name,
                l.EditedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------
    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }

    private bool IsStaff() =>
        User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");
}
