using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.Moq;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Api.Tests.Controllers;

public class OrdersControllerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IOrderNumberGenerator> _orderNumberGeneratorMock;
    private readonly OrdersController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public OrdersControllerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _orderNumberGeneratorMock = new Mock<IOrderNumberGenerator>();
        _controller = new OrdersController(_unitOfWorkMock.Object, _orderNumberGeneratorMock.Object);
        
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
    public async Task CreateGuestOrder_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new OrdersController.CreateGuestOrderRequest(
            "Test Guest",
            "01000000000",
            "123 Test St",
            "Egypt",
            PaymentMethod.CashOnDelivery,
            new List<OrdersController.OrderItemRequest>
            {
                new OrdersController.OrderItemRequest(Guid.NewGuid(), 2)
            }
        );

        var country = new Country { Id = Guid.NewGuid(), Name = "Egypt" };
        var countries = new List<Country> { country }.BuildMockDbSet();
        var products = new List<Product>().BuildMockDbSet();
        var inventories = new List<Inventory>().BuildMockDbSet();
        var productPrices = new List<ProductPrice>().BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Countries.Query()).Returns(countries.Object);
        _unitOfWorkMock.Setup(u => u.Products.Query()).Returns(products.Object);
        _unitOfWorkMock.Setup(u => u.Inventories.Query()).Returns(inventories.Object);
        _unitOfWorkMock.Setup(u => u.ProductPrices.Query()).Returns(productPrices.Object);
        
        _orderNumberGeneratorMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("LOXX-20250727-0001");

        // Act
        var result = await _controller.CreateGuestOrder(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(OrdersController.GetById), createdResult.ActionName);
    }

    [Fact]
    public async Task TrackOrder_ValidOrderNumberAndPhone_ReturnsOrder()
    {
        // Arrange
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "LOXX-20250727-0001",
            GuestName = "Test Guest",
            GuestPhone = "01000000000",
            GuestAddress = "123 Test St",
            Country = new Country { Name = "Egypt" },
            Status = OrderStatus.NewOrder,
            TotalAmount = 100,
            CreatedAt = DateTime.UtcNow,
            OrderItems = new List<OrderItem>()
        };

        var orders = new List<Order> { order }.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Orders.Query()).Returns(orders.Object);

        var request = new OrdersController.TrackOrderRequest("LOXX-20250727-0001", "01000000000");

        // Act
        var result = await _controller.TrackOrder(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
