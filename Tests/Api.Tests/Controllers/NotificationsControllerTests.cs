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

public class NotificationsControllerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly NotificationsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationsControllerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _controller = new NotificationsController(_unitOfWorkMock.Object);
        
        var claims = new List<Claim>
        {
            new Claim("sub", _userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetMyNotifications_ReturnsNotifications()
    {
        // Arrange
        var notifications = new List<Notification>
        {
            new Notification 
            { 
                Id = Guid.NewGuid(),
                UserId = _userId,
                Type = NotificationType.OrderUpdate,
                Message = "Test notification",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }
        }.BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Notifications.Query()).Returns(notifications.Object);

        // Act
        var result = await _controller.GetMyNotifications(false, 1, 20, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCount()
    {
        // Arrange
        var notifications = new List<Notification>
        {
            new Notification { UserId = _userId, IsRead = false },
            new Notification { UserId = _userId, IsRead = false },
            new Notification { UserId = _userId, IsRead = true }
        }.BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Notifications.Query()).Returns(notifications.Object);

        // Act
        var result = await _controller.GetUnreadCount(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task MarkAsRead_ExistingNotification_ReturnsNoContent()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var notification = new Notification { Id = notificationId, UserId = _userId, IsRead = false };

        _unitOfWorkMock.Setup(u => u.Notifications.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        // Act
        var result = await _controller.MarkAsRead(notificationId, CancellationToken.None);

        // Assert
        var noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(204, noContent.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_UpdatesAllUnread()
    {
        // Arrange
        var notifications = new List<Notification>
        {
            new Notification { UserId = _userId, IsRead = false },
            new Notification { UserId = _userId, IsRead = false }
        }.BuildMockDbSet();

        _unitOfWorkMock.Setup(u => u.Notifications.Query()).Returns(notifications.Object);

        // Act
        var result = await _controller.MarkAllAsRead(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
