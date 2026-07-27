using Api.Controllers;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using MockQueryable.Moq;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace Api.Tests.Controllers;

public class OffersControllerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly OffersController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public OffersControllerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _cacheMock = new Mock<IDistributedCache>();
        _controller = new OffersController(_unitOfWorkMock.Object, _cacheMock.Object);
        
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
    public async Task GetAll_ActiveOnly_ReturnsActiveOffers()
    {
        // Arrange
        var offers = new List<Offer>
        {
            new Offer 
            { 
                Id = Guid.NewGuid(), 
                ProductId = Guid.NewGuid(),
                Product = new Product { NameEn = "Test Product" },
                DiscountPercent = 10,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddDays(1)
            }
        }.BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Offers.Query()).Returns(offers.Object);
        _cacheMock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null!);

        // Act
        var result = await _controller.GetAll(activeOnly: true, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateOffer_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var request = new OffersController.CreateOfferRequest(
            productId,
            10,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(7)
        );

        var products = new List<Product> { new Product { Id = productId } }.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Products.Query()).Returns(products.Object);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(OffersController.GetById), createdResult.ActionName);
    }

    [Fact]
    public async Task CreateOffer_InvalidProduct_ReturnsBadRequest()
    {
        // Arrange
        var request = new OffersController.CreateOfferRequest(
            Guid.NewGuid(),
            10,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(7)
        );

        var products = new List<Product>().BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Products.Query()).Returns(products.Object);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingOffer_ReturnsOffer()
    {
        // Arrange
        var offerId = Guid.NewGuid();
        var offer = new Offer 
        { 
            Id = offerId, 
            ProductId = Guid.NewGuid(),
            Product = new Product { NameEn = "Test Product" },
            DiscountPercent = 10,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(7)
        };

        var offers = new List<Offer> { offer }.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Offers.Query()).Returns(offers.Object);

        // Act
        var result = await _controller.GetById(offerId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task UpdateOffer_ValidRequest_ReturnsOk()
    {
        // Arrange
        var offerId = Guid.NewGuid();
        var request = new OffersController.UpdateOfferRequest(15, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(5));
        var offer = new Offer { Id = offerId };

        _unitOfWorkMock.Setup(u => u.Offers.GetByIdAsync(offerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        // Act
        var result = await _controller.Update(offerId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task DeleteOffer_ExistingOffer_ReturnsNoContent()
    {
        // Arrange
        var offerId = Guid.NewGuid();
        var offer = new Offer { Id = offerId };

        _unitOfWorkMock.Setup(u => u.Offers.GetByIdAsync(offerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        // Act
        var result = await _controller.Delete(offerId, CancellationToken.None);

        // Assert
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContent.StatusCode);
    }
}
