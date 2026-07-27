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
            .Include(sm => sm.Sender)
            .OrderBy(sm => sm.CreatedAt)
            .Select(sm => new
            {
                sm.Id,
                sm.Message,
                sm.CreatedAt,
                sm.IsRead,
                Sender = new
                {
                    sm.Sender.Id,
                    sm.Sender.Name,
                    sm.Sender.Role
                },
                sm.RecipientId
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

        // Mark messages as read
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
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager");

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = request.ConversationId,
            SenderId = userId,
            RecipientId = request.RecipientId,
            Message = request.Message,
            AttachmentUrl = request.AttachmentUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send notification to recipient
        if (request.RecipientId.HasValue)
        {
            await _unitOfWork.Notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.RecipientId.Value,
                Type = NotificationType.ChatMessage,
                Message = $"New message from {User.Identity?.Name ?? "User"}",
                RelatedEntityId = request.ConversationId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message.Id, message.Message, message.CreatedAt });
    }

    [HttpPost("conversation")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        // Check if conversation already exists between these users
        var existing = await _unitOfWork.SupportMessages.Query()
            .FirstOrDefaultAsync(sm =>
                (sm.SenderId == userId && sm.RecipientId == request.RecipientId) ||
                (sm.SenderId == request.RecipientId && sm.RecipientId == userId),
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
            SenderId = userId,
            RecipientId = request.RecipientId,
            Message = request.InitialMessage ?? "Hello!",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { conversationId });
    }

    public class SendMessageRequest
    {
        public Guid ConversationId { get; set; }
        public Guid? RecipientId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
    }

    public class CreateConversationRequest
    {
        public Guid RecipientId { get; set; }
        public string? InitialMessage { get; set; }
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
