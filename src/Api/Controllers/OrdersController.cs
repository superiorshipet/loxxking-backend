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

    public OrdersController(IUnitOfWork unitOfWork, IOrderNumberGenerator orderNumberGenerator)
    {
        _unitOfWork = unitOfWork;
        _orderNumberGenerator = orderNumberGenerator;
    }

    public record OrderItemRequest(Guid ProductId, int Quantity);
    public record CreateGuestOrderRequest(
        string GuestName,
        string GuestPhone,
        string GuestAddress,
        string CountryName,
        PaymentMethod PaymentMethod,
        List<OrderItemRequest> Items,
        string? Notes = null
    );
    public record CreateStaffOrderRequest(
        Guid? UserId,
        string GuestName,
        string GuestPhone,
        string GuestAddress,
        string CountryName,
        PaymentMethod PaymentMethod,
        List<OrderItemRequest> Items,
        string? Notes = null
    );
    public record UpdateOrderStatusRequest(OrderStatus Status);
    public record UpdateOrderRequest(string Address, string Phone, string? Notes);
    public record TrackOrderRequest(string OrderNumber, string Phone);

    // ============================================================
    // PUBLIC ENDPOINTS (no auth required)
    // ============================================================

    [HttpPost("track")]
    [AllowAnonymous]
    public async Task<IActionResult> TrackOrder([FromBody] TrackOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Country)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o =>
                o.OrderNumber == request.OrderNumber &&
                o.GuestPhone == request.Phone,
                cancellationToken);

        if (order is null)
            return NotFound(new { message = "Order not found. Please check your order number and phone." });

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            order.GuestName,
            order.GuestPhone,
            order.GuestAddress,
            Country = order.Country.Name,
            Status = order.Status.ToString(),
            order.PaymentMethod,
            order.PaymentStatus,
            order.ShipmentCode,
            order.TotalAmount,
            order.CreatedAt,
            Items = order.OrderItems.Select(i => new
            {
                i.Id,
                ProductName = i.Product.NameEn,
                i.Quantity,
                i.PriceAtOrder,
                Total = i.Quantity * i.PriceAtOrder
            })
        });
    }

    [HttpPost("guest")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateGuestOrder([FromBody] CreateGuestOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Order must contain at least one item." });

        var country = await _unitOfWork.Countries.Query()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CountryName.ToLower(), cancellationToken);

        if (country is null)
            return BadRequest(new { message = $"Country '{request.CountryName}' not found." });

        var orderNumber = await _orderNumberGenerator.GenerateAsync(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            GuestName = request.GuestName,
            GuestPhone = request.GuestPhone,
            GuestAddress = request.GuestAddress,
            CountryId = country.Id,
            Status = OrderStatus.NewOrder,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var item in request.Items)
        {
            var price = await _unitOfWork.ProductPrices.Query()
                .Where(p => p.ProductId == item.ProductId && p.CountryId == country.Id)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (price is null)
                return BadRequest(new { message = $"Product {item.ProductId} has no price for this country." });

            var inventory = await _unitOfWork.Inventories.Query()
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.CountryId == country.Id, cancellationToken);

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

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new
        {
            order.Id,
            order.OrderNumber,
            order.TotalAmount,
            Message = "Order created successfully. Please save your order number for tracking.",
            TrackingHint = "Use /api/orders/track with your order number and phone to track your order."
        });
    }

    // ============================================================
    // STAFF-ONLY ENDPOINTS (auth required)
    // ============================================================

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
            .Include(o => o.Country)
            .Include(o => o.User)
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
                o.GuestPhone.ToLower().Contains(s) ||
                o.GuestAddress.ToLower().Contains(s) ||
                o.GuestName.ToLower().Contains(s) ||
                (o.ShipmentCode != null && o.ShipmentCode.ToLower().Contains(s)) ||
                (o.User != null && o.User.Name.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var data = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                CustomerName = o.User != null ? o.User.Name : o.GuestName,
                Phone = o.User != null ? o.User.Phone : o.GuestPhone,
                o.GuestAddress,
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
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Country)
            .Include(o => o.User)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
            return NotFound();

        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");
        var userId = GetCurrentUserId();

        if (!isStaff && order.UserId != userId)
            return Forbid();

        object customerInfo;
        if (order.User != null)
        {
            customerInfo = new { order.User.Id, order.User.Name, order.User.Phone };
        }
        else
        {
            customerInfo = new { Id = (Guid?)null, Name = order.GuestName, Phone = order.GuestPhone };
        }

        return Ok(new
        {
            order.Id,
            order.OrderNumber,
            Customer = customerInfo,
            Country = order.Country.Name,
            Status = order.Status.ToString(),
            PaymentMethod = order.PaymentMethod.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            order.ShipmentCode,
            order.GuestAddress,
            order.Notes,
            order.TotalAmount,
            order.CreatedAt,
            Items = order.OrderItems.Select(i => new
            {
                i.Id,
                ProductName = i.Product.NameEn,
                i.Quantity,
                i.PriceAtOrder,
                Total = i.Quantity * i.PriceAtOrder
            })
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> CreateStaffOrder([FromBody] CreateStaffOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { message = "Order must contain at least one item." });

        var country = await _unitOfWork.Countries.Query()
            .FirstOrDefaultAsync(c => c.Name.ToLower() == request.CountryName.ToLower(), cancellationToken);

        if (country is null)
            return BadRequest(new { message = $"Country '{request.CountryName}' not found." });

        User? user = null;
        if (request.UserId.HasValue)
        {
            user = await _unitOfWork.Users.GetByIdAsync(request.UserId.Value, cancellationToken);
            if (user is null)
                return BadRequest(new { message = "User not found." });
        }

        var orderNumber = await _orderNumberGenerator.GenerateAsync(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            UserId = request.UserId,
            GuestName = user != null ? user.Name : request.GuestName,
            GuestPhone = user != null ? user.Phone : request.GuestPhone,
            GuestAddress = user != null ? user.Country?.Name ?? request.GuestAddress : request.GuestAddress,
            CountryId = country.Id,
            Status = OrderStatus.NewOrder,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        foreach (var item in request.Items)
        {
            var price = await _unitOfWork.ProductPrices.Query()
                .Where(p => p.ProductId == item.ProductId && p.CountryId == country.Id)
                .Select(p => (decimal?)p.Price)
                .FirstOrDefaultAsync(cancellationToken);

            if (price is null)
                return BadRequest(new { message = $"Product {item.ProductId} has no price for this country." });

            var inventory = await _unitOfWork.Inventories.Query()
                .FirstOrDefaultAsync(i => i.ProductId == item.ProductId && i.CountryId == country.Id, cancellationToken);

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

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new
        {
            order.Id,
            order.OrderNumber,
            order.TotalAmount
        });
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

        if (oldStatus != request.Status && order.UserId.HasValue)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = order.UserId.Value,
                Type = NotificationType.OrderUpdate,
                Message = $"Your order {order.OrderNumber} status changed to {request.Status}.",
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

        await LogIfChangedAsync(nameof(order.GuestAddress), order.GuestAddress, request.Address);
        await LogIfChangedAsync(nameof(order.GuestPhone), order.GuestPhone, request.Phone);
        await LogIfChangedAsync(nameof(order.Notes), order.Notes, request.Notes);

        order.GuestAddress = request.Address;
        order.GuestPhone = request.Phone;
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

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
