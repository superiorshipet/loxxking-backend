using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public InvoicesController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var invoices = await _unitOfWork.Invoices.Query()
            .Include(i => i.Order)
                .ThenInclude(o => o.Customer)
            .Include(i => i.Order)
                .ThenInclude(o => o.Country)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.TotalAmount,
                i.IssuedAt,
                Order = new
                {
                    i.Order.Id,
                    CustomerName = i.Order.Customer.Name,
                    Country = i.Order.Country.Name
                }
            })
            .ToListAsync(cancellationToken);

        return Ok(invoices);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager");

        var invoice = await _unitOfWork.Invoices.Query()
            .Include(i => i.Order)
                .ThenInclude(o => o.Customer)
            .Include(i => i.Order)
                .ThenInclude(o => o.Country)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
            return NotFound();

        if (!isStaff && invoice.Order.CustomerId != userId)
            return Forbid();

        return Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.TotalAmount,
            invoice.IssuedAt,
            Order = new
            {
                invoice.Order.Id,
                CustomerName = invoice.Order.Customer.Name,
                Country = invoice.Order.Country.Name
            }
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,StoreManager")]
    public async Task<IActionResult> Create(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.Query()
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound(new { message = "Order not found." });

        var existing = await _unitOfWork.Invoices.Query()
            .AnyAsync(i => i.OrderId == orderId, cancellationToken);

        if (existing)
            return BadRequest(new { message = "Invoice already exists for this order." });

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{orderId.ToString().Substring(0, 8)}",
            TotalAmount = order.TotalAmount,
            IssuedAt = DateTime.UtcNow
        };

        await _unitOfWork.Invoices.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
    }

    [HttpGet("{id}/pdf")]
    [Authorize]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager");

        var invoice = await _unitOfWork.Invoices.Query()
            .Include(i => i.Order)
                .ThenInclude(o => o.Customer)
            .Include(i => i.Order)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice is null)
            return NotFound();

        if (!isStaff && invoice.Order.CustomerId != userId)
            return Forbid();

        var pdfGenerator = HttpContext.RequestServices.GetRequiredService<IInvoicePdfGenerator>();
        var pdfBytes = await pdfGenerator.GenerateAsync(invoice, cancellationToken);

        return File(pdfBytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
