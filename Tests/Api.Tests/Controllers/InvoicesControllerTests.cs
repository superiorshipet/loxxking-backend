using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.Moq;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Api.Tests.Controllers;

public class InvoicesControllerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly InvoicesController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _invoiceId = Guid.NewGuid();

    public InvoicesControllerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _controller = new InvoicesController(_unitOfWorkMock.Object);
        
        var claims = new List<Claim>
        {
            new Claim("sub", _userId.ToString()),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetAll_Admin_ReturnsInvoices()
    {
        // Arrange
        var invoices = new List<Invoice>
        {
            new Invoice 
            { 
                Id = Guid.NewGuid(),
                InvoiceNumber = "INV-001",
                TotalAmount = 100,
                IssuedAt = DateTime.UtcNow,
                Order = new Order 
                { 
                    Id = Guid.NewGuid(),
                    Customer = new User { Name = "Test Customer" },
                    Country = new Country { Name = "Egypt" }
                }
            }
        }.BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Invoices.Query()).Returns(invoices.Object);

        // Act
        var result = await _controller.GetAll(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetById_ExistingInvoice_ReturnsInvoice()
    {
        // Arrange
        var invoice = new Invoice
        {
            Id = _invoiceId,
            InvoiceNumber = "INV-001",
            TotalAmount = 100,
            IssuedAt = DateTime.UtcNow,
            Order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = _userId,
                Customer = new User { Name = "Test Customer" },
                Country = new Country { Name = "Egypt" }
            }
        };

        var invoices = new List<Invoice> { invoice }.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Invoices.Query()).Returns(invoices.Object);

        // Act
        var result = await _controller.GetById(_invoiceId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateInvoice_ValidOrder_ReturnsCreated()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            TotalAmount = 100,
            Customer = new User { Name = "Test Customer" }
        };

        var orders = new List<Order> { order }.BuildMockDbSet();
        var invoices = new List<Invoice>().BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Orders.Query()).Returns(orders.Object);
        _unitOfWorkMock.Setup(u => u.Invoices.Query()).Returns(invoices.Object);

        // Act
        var result = await _controller.Create(orderId, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(InvoicesController.GetById), createdResult.ActionName);
    }
}
