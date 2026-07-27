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
            .Select(sm => new
            {
                sm.Id,
                sm.ConversationId,
                sm.SenderType,
                sm.SenderName,
                sm.Message,
                sm.IsRead,
                sm.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(messages);
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

        var isStaff = User.IsInRole("Admin") || User.IsInRole("StoreManager") || User.IsInRole("SalesEmployee");
        if (isStaff)
        {
            senderType = "Staff";
            senderName = User.FindFirst(ClaimTypes.Name)?.Value
                      ?? User.FindFirst("name")?.Value
                      ?? "Support Team";
        }
        else
        {
            senderType = "Customer";
            senderName = request.SenderName ?? "Customer";
        }

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
                var ordered  = g.OrderByDescending(m => m.CreatedAt).ToList();
                var lastMsg  = ordered.First();
                // Use the first message's sender name as the conversation label
                var firstMsg = g.OrderBy(m => m.CreatedAt).First();
                return new
                {
                    ConversationId  = g.Key,
                    CustomerName    = firstMsg.SenderName,
                    LastMessage     = lastMsg.Message,
                    LastMessageAt   = lastMsg.CreatedAt,
                    UnreadCount     = g.Count(m => !m.IsRead && m.SenderType == "Customer"),
                    MessageCount    = g.Count()
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
        var msgs = await _unitOfWork.SupportMessages.Query()
            .Where(m => m.ConversationId == conversationId && !m.IsRead)
            .ToListAsync(cancellationToken);

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
