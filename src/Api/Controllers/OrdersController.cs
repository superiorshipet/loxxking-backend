using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderNumberGenerator _orderNumberGenerator;
    private readonly IOrderNotificationService _notifications;
    private readonly IInvoicePdfGenerator _pdfGenerator;

    public OrdersController(
        IUnitOfWork unitOfWork,
        IOrderNumberGenerator orderNumberGenerator,
        IOrderNotificationService notifications,
        IInvoicePdfGenerator pdfGenerator)
    {
        _unitOfWork = unitOfWork;
        _orderNumberGenerator = orderNumberGenerator;
        _notifications = notifications;
        _pdfGenerator = pdfGenerator;
    }

    public record OrderItemRequest(Guid ProductId, int Quantity);
    // Standard (authenticated) order request — country resolved from token/geo
    public record CreateOrderRequest(string Address, string Phone, string? Notes, PaymentMethod PaymentMethod, List<OrderItemRequest> Items, Guid? CountryId = null);
    // Guest order request — country supplied by name (matched to DB record)
    public record CreateGuestOrderRequest(string Name, string Phone, string Address, string CountryName, PaymentMethod PaymentMethod, List<OrderItemRequest> Items, string? Notes = null);
    // Track an order by its human-readable number + phone verification
    public record TrackOrderRequest(string OrderNumber, string Phone);
    public record UpdateOrderStatusRequest(OrderStatus Status);
    public record UpdateOrderRequest(string Address, string Phone, string? Notes);

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

        var query = _unitOfWork.Orders.Query()
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
                (o.Customer != null && o.Customer.Name.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var data = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                CustomerName = o.Customer != null ? o.Customer.Name : "Guest",
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.Query()
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
            Customer = order.Customer != null ? new { order.Customer.Id, order.Customer.Name, order.Customer.Phone } : null,
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

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Order must contain at least one item." });

        // ── Resolve country ──────────────────────────────────────────────────────
        // Priority: (1) CountryId in request body, (2) authenticated customer's country,
        //           (3) X-Geo-Country header set by GeoLocationMiddleware, (4) first country in DB.
        Guid? resolvedCountryId = null;

        if (request.CountryId.HasValue && request.CountryId.Value != Guid.Empty)
        {
            resolvedCountryId = request.CountryId.Value;
        }
        else if (User.Identity?.IsAuthenticated == true)
        {
            var customerId = GetCurrentUserId();
            if (customerId != Guid.Empty)
            {
                var customer = await _unitOfWork.Users.GetByIdAsync(customerId, cancellationToken);
                resolvedCountryId = customer?.CountryId;
            }
        }

        if (resolvedCountryId is null)
        {
            var geoCountryName = HttpContext.Request.Headers["X-Geo-Country"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(geoCountryName))
            {
                var geoCountry = await _unitOfWork.Countries.Query()
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == geoCountryName.ToLower(), cancellationToken);
                resolvedCountryId = geoCountry?.Id;
            }
        }

        if (resolvedCountryId is null)
        {
            // Fallback: pick the first available country so the order is never blocked
            var fallback = await _unitOfWork.Countries.Query().FirstOrDefaultAsync(cancellationToken);
            resolvedCountryId = fallback?.Id;
        }

        if (resolvedCountryId is null)
            return BadRequest(new { message = "Unable to determine your country. Please specify a countryId." });

        // ── Resolve optional authenticated customer ───────────────────────────────
        Guid? authenticatedCustomerId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var uid = GetCurrentUserId();
            if (uid != Guid.Empty) authenticatedCustomerId = uid;
        }

        // ── Generate unique order number ─────────────────────────────────────────
        var orderNumber = await _orderNumberGenerator.GenerateAsync(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = authenticatedCustomerId,
            CountryId = resolvedCountryId.Value,
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
            // Try price for resolved country; fall back to any price for the product
            var price = await _unitOfWork.ProductPrices.Query()
                .Where(p => p.ProductId == item.ProductId && p.CountryId == resolvedCountryId.Value)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            price ??= await _unitOfWork.ProductPrices.Query()
                .Where(p => p.ProductId == item.ProductId)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (price is null)
                return BadRequest(new { message = $"Product {item.ProductId} has no price configured." });

            // Try inventory for resolved country; fall back to any inventory for the product
            var inventory = await _unitOfWork.Inventories.Query()
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.CountryId == resolvedCountryId.Value, cancellationToken);

            inventory ??= await _unitOfWork.Inventories.Query()
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId, cancellationToken);

            if (inventory is null || inventory.Quantity < item.Quantity)
                return BadRequest(new { message = $"Insufficient stock for product {item.ProductId}." });

            inventory.Quantity -= item.Quantity;
            inventory.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Inventories.Update(inventory);

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
        await _unitOfWork.Orders.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Fire invoice notifications (email + WhatsApp) ─────────────────────
        try
        {
            // Resolve product names for invoice
            var productIds = order.OrderItems.Select(i => i.ProductId).ToList();
            var products = await _unitOfWork.Products.Query()
                .Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);

            var country = await _unitOfWork.Countries.GetByIdAsync(order.CountryId, cancellationToken);
            
            foreach (var item in order.OrderItems)
            {
                item.Product = products.FirstOrDefault(p => p.Id == item.ProductId);
            }
            order.Country = country;
            
            var invoiceObj = new Invoice
            {
                OrderId = order.Id,
                Order = order,
                InvoiceNumber = $"INV-{order.OrderNumber}",
                TotalAmount = order.TotalAmount,
                IssuedAt = DateTime.UtcNow
            };
            var pdfBytes = await _pdfGenerator.GenerateAsync(invoiceObj, cancellationToken);

            var notifData = new OrderNotificationData(
                OrderNumber:   order.OrderNumber,
                CustomerName:  request.Phone, // phone as name for anonymous orders
                CustomerPhone: request.Phone,
                Address:       request.Address,
                Country:       country?.Name ?? "—",
                PaymentMethod: order.PaymentMethod.ToString(),
                TotalAmount:   order.TotalAmount,
                Items: order.OrderItems.Select(i => new OrderNotificationItem(i.Product?.NameEn ?? i.ProductId.ToString(), i.Quantity, i.PriceAtOrder)).ToList(),
                CreatedAt: order.CreatedAt,
                PdfAttachment: pdfBytes
            );
            await _notifications.NotifyNewOrderAsync(notifData, cancellationToken);
        }
        catch { /* swallow — notification must never break order flow */ }

        return CreatedAtAction(nameof(GetById), new { id = order.Id },
            new { order.Id, order.OrderNumber, order.TotalAmount, CountryId = resolvedCountryId });

    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var oldStatus = order.Status;
        order.Status = request.Status;
        _unitOfWork.Orders.Update(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (oldStatus != request.Status && order.CustomerId.HasValue)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = order.CustomerId.Value,
                Type = NotificationType.OrderUpdate,
                Message = $"Your order status changed to {request.Status}.",
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { order.Id, Status = order.Status.ToString() });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(id, cancellationToken);
        if (order is null)
            return NotFound();

        var editorId = GetCurrentUserId();
        var now = DateTime.UtcNow;

        async Task LogIfChangedAsync(string fieldName, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            await _unitOfWork.OrderEditLogs.AddAsync(new OrderEditLog
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                EditedBy = editorId,
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                EditedAt = now
            }, cancellationToken);
        }

        await LogIfChangedAsync(nameof(order.Address), order.Address, request.Address);
        await LogIfChangedAsync(nameof(order.Phone), order.Phone, request.Phone);
        await LogIfChangedAsync(nameof(order.Notes), order.Notes, request.Notes);

        order.Address = request.Address;
        order.Phone = request.Phone;
        order.Notes = request.Notes;
        _unitOfWork.Orders.Update(order);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { order.Id, message = "Order updated and changes logged." });
    }

    [HttpGet("{id}/edit-logs")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetEditLogs(Guid id, CancellationToken cancellationToken)
    {
        var logs = await _unitOfWork.OrderEditLogs.Query()
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

    // ─── Guest Order (by name) ───────────────────────────────────────────────

    [HttpPost("guest")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateGuestOrder([FromBody] CreateGuestOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Order must contain at least one item." });

        // Resolve country by name
        var country = await _unitOfWork.Countries.Query()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CountryName.ToLower(), cancellationToken);

        if (country is null)
            return BadRequest(new { message = $"Country '{request.CountryName}' not found. Please check available countries." });

        var orderNumber = await _orderNumberGenerator.GenerateAsync(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = null,
            CountryId = country.Id,
            Country = country,
            Status = OrderStatus.NewOrder,
            PaymentMethod = request.PaymentMethod,
            Address = request.Address,
            Phone = request.Phone,
            Notes = request.Notes,
            GuestName = request.Name,
            GuestPhone = request.Phone,
            GuestAddress = request.Address,
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var item in request.Items)
        {
            var price = await _unitOfWork.ProductPrices.Query()
                .Where(p => p.ProductId == item.ProductId && p.CountryId == country.Id)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            price ??= await _unitOfWork.ProductPrices.Query()
                .Where(p => p.ProductId == item.ProductId)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (price is null)
                return BadRequest(new { message = $"Product {item.ProductId} has no price configured." });

            var inventory = await _unitOfWork.Inventories.Query()
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.CountryId == country.Id, cancellationToken);

            inventory ??= await _unitOfWork.Inventories.Query()
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId, cancellationToken);

            if (inventory is null || inventory.Quantity < item.Quantity)
                return BadRequest(new { message = $"Insufficient stock for product {item.ProductId}." });

            inventory.Quantity -= item.Quantity;
            inventory.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Inventories.Update(inventory);

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
        await _unitOfWork.Orders.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Fire invoice notifications (email + WhatsApp) ─────────────────────
        try
        {
            var productIds = order.OrderItems.Select(i => i.ProductId).ToList();
            var products = await _unitOfWork.Products.Query()
                .Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);

            foreach (var item in order.OrderItems)
            {
                item.Product = products.FirstOrDefault(p => p.Id == item.ProductId);
            }
            order.Country = country;
            
            var invoiceObj = new Invoice
            {
                OrderId = order.Id,
                Order = order,
                InvoiceNumber = $"INV-{order.OrderNumber}",
                TotalAmount = order.TotalAmount,
                IssuedAt = DateTime.UtcNow
            };
            var pdfBytes = await _pdfGenerator.GenerateAsync(invoiceObj, cancellationToken);

            var notifData = new OrderNotificationData(
                OrderNumber:   order.OrderNumber,
                CustomerName:  request.Name,
                CustomerPhone: request.Phone,
                Address:       request.Address,
                Country:       country.Name,
                PaymentMethod: order.PaymentMethod.ToString(),
                TotalAmount:   order.TotalAmount,
                Items: order.OrderItems.Select(i => new OrderNotificationItem(i.Product?.NameEn ?? i.ProductId.ToString(), i.Quantity, i.PriceAtOrder)).ToList(),
                CreatedAt: order.CreatedAt,
                PdfAttachment: pdfBytes
            );
            await _notifications.NotifyNewOrderAsync(notifData, cancellationToken);
        }
        catch { /* swallow */ }

        return CreatedAtAction(nameof(GetById), new { id = order.Id },
            new { order.Id, order.OrderNumber, order.TotalAmount, Country = country.Name });
    }

    // ─── Public order tracking ────────────────────────────────────────────────

    [HttpPost("track")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackOrder([FromBody] TrackOrderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderNumber) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "OrderNumber and Phone are required." });

        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Country)
            .Include(o => o.OrderItems).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o =>
                o.OrderNumber == request.OrderNumber &&
                (o.Phone == request.Phone || o.GuestPhone == request.Phone),
                cancellationToken);

        if (order is null)
            return NotFound(new { message = "Order not found. Please check your Order Number and Phone." });

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            CustomerName = order.GuestName ?? order.Customer?.Name ?? "Customer",
            Country = order.Country?.Name,
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

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }

    private bool IsStaff() =>
        User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");
}
