using Application.Common.Interfaces;
using Domain.Entities;
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

    // ─── Customer / Guest: get messages in their conversation ────────────────
    [HttpGet("messages/{conversationId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMessages(Guid conversationId, CancellationToken cancellationToken)
    {
        var messages = await _unitOfWork.SupportMessages.Query()
            .Where(sm => sm.ConversationId == conversationId)
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

    // ─── Send a message (customer or staff) ─────────────────────────────────
    [HttpPost("send")]
    [AllowAnonymous]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message cannot be empty." });

        // Determine sender type and name
        string senderType;
        string senderName;

        // null = guest/anonymous (no FK violation)
        Guid? senderIdToStore = userId != Guid.Empty ? userId : (Guid?)null;

        // New conversation if zero GUID sent
        var conversationId = (request.ConversationId == Guid.Empty)
            ? Guid.NewGuid()
            : request.ConversationId;

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderType = senderType,
            SenderName = senderName,
            Message = request.Message,
            GuestName = request.GuestName,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SupportMessages.AddAsync(message, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message.Id,
            conversationId = message.ConversationId,
            message.SenderType,
            message.SenderName,
            message.Message,
            message.CreatedAt
        });
    }

    // ─── Admin / Staff: list all conversation threads ────────────────────────
    [HttpGet("conversations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");

        // Only staff may see all conversations
        if (!isStaff)
            return Unauthorized(new { message = "Staff login required." });

        var rawMessages = await _unitOfWork.SupportMessages.Query()
            .OrderBy(sm => sm.CreatedAt)
            .ToListAsync(cancellationToken);

        var conversations = rawMessages
            .GroupBy(sm => sm.ConversationId)
            .Select(g =>
            {
                var msgs = g.OrderByDescending(sm => sm.CreatedAt).ToList();
                var lastMsg = msgs.First();
                // Best name: registered user name > GuestName on any message > "Customer"
                var firstMsg = g.OrderBy(sm => sm.CreatedAt).First();
                var displayName = lastMsg.Sender?.Name
                    ?? g.Where(m => m.GuestName != null).Select(m => m.GuestName).FirstOrDefault()
                    ?? "Customer";
                return new
                {
                    ConversationId = g.Key,
                    LastMessage = lastMsg.Message,
                    LastMessageAt = lastMsg.CreatedAt,
                    SenderName = displayName,
                    SenderRole = lastMsg.Sender?.Role.ToString() ?? "Guest",
                    UnreadCount = g.Count(m => !m.IsRead && m.SenderId == null),
                    MessageCount = g.Count()
                };
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();

        return Ok(conversations);
    }

    // ─── Mark messages as read ───────────────────────────────────────────────
    [HttpPatch("conversations/{conversationId}/read")]
    [Authorize(Roles = "Admin,StoreManager,SalesEmployee")]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken cancellationToken)
    {
        public Guid ConversationId { get; set; }
        public Guid? RecipientId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? GuestName { get; set; }   // optional: guest's name shown to admin
    }

        foreach (var m in msgs) { m.IsRead = true; _unitOfWork.SupportMessages.Update(m); }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new { marked = msgs.Count });
    }

    // ─── Models ──────────────────────────────────────────────────────────────

    public class SendMessageRequest
    {
        public Guid   ConversationId { get; set; }
        public string Message        { get; set; } = string.Empty;
        public string? SenderName   { get; set; }
    }
}
