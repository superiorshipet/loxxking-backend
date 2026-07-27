using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/support-chat")]
public class SupportChatController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public SupportChatController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    // ─── Get all messages in a conversation ─────────────────────────────────
    [HttpGet("messages/{conversationId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMessages(Guid conversationId, CancellationToken cancellationToken)
    {
        var messages = await _unitOfWork.SupportMessages.Query()
            .Where(sm => sm.ConversationId == conversationId)
            .Include(sm => sm.Sender)
            .OrderBy(sm => sm.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = messages.Select(sm => new
        {
            sm.Id,
            sm.ConversationId,
            sm.Message,
            sm.CreatedAt,
            sm.IsRead,
            Sender = sm.Sender != null
                ? new { Id = (Guid?)sm.Sender.Id, Name = sm.Sender.Name, Role = sm.Sender.Role.ToString() }
                : new { Id = sm.SenderId,          Name = "Customer",     Role = "Customer" },
            sm.RecipientId
        });

        return Ok(result);
    }

    // ─── Send a message (guest or authenticated) ─────────────────────────────
    [HttpPost("send")]
    [AllowAnonymous]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message content cannot be empty." });

        var userId = GetCurrentUserId();

        // null = guest/anonymous (no FK violation)
        Guid? senderIdToStore = userId != Guid.Empty ? userId : (Guid?)null;

        // Treat zero/missing GUID as "start a new conversation"
        var conversationId = (request.ConversationId == Guid.Empty || request.ConversationId == default)
            ? Guid.NewGuid()
            : request.ConversationId;

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderIdToStore,
            RecipientId = request.RecipientId,
            Message = request.Message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Always return the real conversationId so the frontend can persist it
        return Ok(new
        {
            message.Id,
            conversationId = message.ConversationId,
            message.Message,
            message.CreatedAt
        });
    }

    // ─── Create a new conversation (legacy helper) ───────────────────────────
    [HttpPost("conversation")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var conversationId = Guid.NewGuid();
        var userId = GetCurrentUserId();

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            RecipientId = request.RecipientId,
            Message = request.InitialMessage ?? "Hello support!",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { conversationId });
    }

    // ─── List all conversations (admin sees all; others see their own) ────────
    [HttpGet("conversations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");

        var query = _unitOfWork.SupportMessages.Query()
            .Include(sm => sm.Sender)
            .AsQueryable();

        // Non-staff, authenticated users: only their own conversations
        if (!isStaff && userId != Guid.Empty)
            query = query.Where(sm => sm.SenderId == userId || sm.RecipientId == userId);

        var rawMessages = await query.ToListAsync(cancellationToken);

        var conversations = rawMessages
            .GroupBy(sm => sm.ConversationId)
            .Select(g =>
            {
                var lastMsg = g.OrderByDescending(sm => sm.CreatedAt).FirstOrDefault();
                return new
                {
                    ConversationId = g.Key,
                    LastMessage = lastMsg?.Message ?? "",
                    LastMessageAt = g.Max(sm => sm.CreatedAt),
                    SenderName = lastMsg?.Sender?.Name ?? "Customer",
                    SenderRole = lastMsg?.Sender?.Role.ToString() ?? "Customer"
                };
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();

        return Ok(conversations);
    }

    // ─── Request / Response models ───────────────────────────────────────────

    public class SendMessageRequest
    {
        public Guid ConversationId { get; set; }
        public Guid? RecipientId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CreateConversationRequest
    {
        public Guid RecipientId { get; set; }
        public string? InitialMessage { get; set; }
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value;
        return userId is not null && Guid.TryParse(userId, out var id) ? id : Guid.Empty;
    }
}
