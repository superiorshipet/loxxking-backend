using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/support-chat")]
[Authorize]
public class SupportChatController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SupportChatController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("messages/{conversationId}")]
    public async Task<IActionResult> GetMessages(Guid conversationId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager");

        var messages = await _unitOfWork.SupportMessages.Query()
            .Where(sm => sm.ConversationId == conversationId)
            .OrderBy(sm => sm.CreatedAt)
            .Select(sm => new
            {
                sm.Id,
                sm.SenderName,
                sm.SenderRole,
                sm.Message,
                sm.IsRead,
                sm.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (!messages.Any())
            return NotFound(new { message = "No messages found for this conversation." });

        // Check if user has access to this conversation
        var firstMessage = await _unitOfWork.SupportMessages.Query()
            .FirstOrDefaultAsync(sm => sm.ConversationId == conversationId, cancellationToken);

        if (firstMessage is null)
            return NotFound();

        if (!isStaff && firstMessage.SenderId != userId)
            return Forbid();

        // Mark messages as read for staff
        if (isStaff)
        {
            var unreadMessages = await _unitOfWork.SupportMessages.Query()
                .Where(sm => sm.ConversationId == conversationId && !sm.IsRead)
                .ToListAsync(cancellationToken);

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                _unitOfWork.SupportMessages.Update(msg);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(messages);
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        
        if (user is null)
            return Unauthorized();

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderName = user.Name,
            SenderRole = user.Role.ToString(),
            SenderId = userId,
            Message = request.Message,
            AttachmentUrl = request.AttachmentUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification to staff (if sender is customer)
        if (user.Role == UserRole.Customer)
        {
            await NotifyStaffAsync($"New message from {user.Name}", cancellationToken);
        }

        return Ok(new { message.Id, message.Message, message.CreatedAt });
    }

    [HttpPost("conversation")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        
        if (user is null)
            return Unauthorized();

        // Check if conversation already exists between these users
        var existing = await _unitOfWork.SupportMessages.Query()
            .FirstOrDefaultAsync(sm =>
                sm.SenderId == userId,
                cancellationToken);

        if (existing is not null)
        {
            return Ok(new { conversationId = existing.ConversationId });
        }

        var conversationId = Guid.NewGuid();

        // Create first message
        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderName = user.Name,
            SenderRole = user.Role.ToString(),
            SenderId = userId,
            Message = request.InitialMessage ?? "Hello!",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Notify staff
        await NotifyStaffAsync($"New conversation started by {user.Name}", cancellationToken);

        return Ok(new { conversationId });
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task NotifyStaffAsync(string message, CancellationToken cancellationToken)
    {
        var staff = await _unitOfWork.Users.Query()
            .Where(u => u.Role == UserRole.Admin || u.Role == UserRole.StoreManager || u.Role == UserRole.SalesEmployee)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var staffId in staff)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = staffId,
                Type = NotificationType.SupportMessage,
                Message = message,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }

    public class SendMessageRequest
    {
        public Guid ConversationId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
    }

    public class CreateConversationRequest
    {
        public string? InitialMessage { get; set; }
    }
}
