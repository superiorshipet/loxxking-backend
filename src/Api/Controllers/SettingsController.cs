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

    public record NotificationSettingsDto(string BusinessEmail, string WhatsAppPhone);

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

        return Ok(new NotificationSettingsDto(email, phone));
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
            notifications["BusinessEmail"] = dto.BusinessEmail;
            notifications["WhatsAppPhone"] = dto.WhatsAppPhone;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = document.ToJsonString(options);

            await System.IO.File.WriteAllTextAsync(filePath, updatedJson);
            return Ok(new { message = "Settings updated successfully" });
        }

        return BadRequest("Invalid configuration format");
    }
}
