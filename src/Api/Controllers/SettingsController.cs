using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public SettingsController(IWebHostEnvironment env)
    {
        _env = env;
    }

    public record NotificationSettingsDto(
        string BusinessEmail, 
        string WhatsAppPhone,
        string GreenApiInstanceId,
        string GreenApiToken,
        string SmtpUsername,
        string SmtpPassword
    );

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotificationSettings()
    {
        var filePath = Path.Combine(_env.ContentRootPath, "appsettings.json");
        if (!System.IO.File.Exists(filePath))
            return NotFound("appsettings.json not found");

        var jsonStr = await System.IO.File.ReadAllTextAsync(filePath);
        var document = JsonNode.Parse(jsonStr);

        var notifications = document?["Notifications"];
        var email = notifications?["BusinessEmail"]?.ToString() ?? "";
        var phone = notifications?["WhatsAppPhone"]?.ToString() ?? "";
        var instanceId = notifications?["GreenApiInstanceId"]?.ToString() ?? "";
        var token = notifications?["GreenApiToken"]?.ToString() ?? "";
        
        var smtp = notifications?["Smtp"];
        var smtpUser = smtp?["Username"]?.ToString() ?? "";
        var smtpPass = smtp?["Password"]?.ToString() ?? "";

        return Ok(new NotificationSettingsDto(email, phone, instanceId, token, smtpUser, smtpPass));
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] NotificationSettingsDto dto)
    {
        var filePath = Path.Combine(_env.ContentRootPath, "appsettings.json");
        if (!System.IO.File.Exists(filePath))
            return NotFound("appsettings.json not found");

        var jsonStr = await System.IO.File.ReadAllTextAsync(filePath);
        var document = JsonNode.Parse(jsonStr);

        if (document != null && document["Notifications"] is JsonObject notifications)
        {
            if (!string.IsNullOrWhiteSpace(dto.BusinessEmail))
                notifications["BusinessEmail"] = dto.BusinessEmail;
                
            if (!string.IsNullOrWhiteSpace(dto.WhatsAppPhone))
                notifications["WhatsAppPhone"] = dto.WhatsAppPhone;
            
            if (!string.IsNullOrWhiteSpace(dto.GreenApiInstanceId))
                notifications["GreenApiInstanceId"] = dto.GreenApiInstanceId;
            
            if (!string.IsNullOrWhiteSpace(dto.GreenApiToken))
                notifications["GreenApiToken"] = dto.GreenApiToken;

            if (notifications["Smtp"] is JsonObject smtp)
            {
                if (!string.IsNullOrWhiteSpace(dto.SmtpUsername))
                    smtp["Username"] = dto.SmtpUsername;
                    
                if (!string.IsNullOrWhiteSpace(dto.SmtpPassword))
                    smtp["Password"] = dto.SmtpPassword;
            }

            var options = new JsonSerializerOptions { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            };
            var updatedJson = document.ToJsonString(options);

            await System.IO.File.WriteAllTextAsync(filePath, updatedJson);
            return Ok(new { message = "Settings updated successfully" });
        }

        return BadRequest("Invalid configuration format");
    }
}
