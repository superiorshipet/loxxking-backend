using Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Net.Http.Json;

namespace Infrastructure.Services;

public class OrderNotificationService : IOrderNotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<OrderNotificationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderNotificationService(
        IConfiguration config,
        ILogger<OrderNotificationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task NotifyNewOrderAsync(OrderNotificationData order, CancellationToken cancellationToken = default)
    {
        // Run both notifications in parallel — never block the API response
        var emailTask   = SendEmailAsync(order, cancellationToken);
        var whatsAppTask = SendWhatsAppAsync(order, cancellationToken);

        await Task.WhenAll(emailTask, whatsAppTask);
    }

    // ─── EMAIL ────────────────────────────────────────────────────────────────
    private async Task SendEmailAsync(OrderNotificationData order, CancellationToken ct)
    {
        var smtp = _config.GetSection("Notifications:Smtp");
        var host     = smtp["Host"]     ?? "smtp.gmail.com";
        var port     = int.Parse(smtp["Port"] ?? "587");
        var user     = smtp["Username"] ?? "";
        var pass     = smtp["Password"] ?? "";
        var fromName = smtp["FromName"] ?? "LoxxKing System";
        var toEmail  = _config["Notifications:BusinessEmail"] ?? "luxiraholding@gmail.com";

        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            _logger.LogWarning("Email not configured — skipping invoice email for order {OrderNumber}", order.OrderNumber);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, user));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = $"🛒 New Order #{order.OrderNumber} — {order.TotalAmount:N2} EGP";

            var builder = new BodyBuilder { HtmlBody = BuildInvoiceHtml(order) };
            if (order.PdfAttachment != null)
            {
                builder.Attachments.Add($"Invoice_{order.OrderNumber}.pdf", order.PdfAttachment, new ContentType("application", "pdf"));
            }
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(user, pass, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Invoice email sent for order {OrderNumber}", order.OrderNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email for order {OrderNumber}", order.OrderNumber);
        }
    }

    // ─── WHATSAPP via Green-API ───────────────────────────────────────────────
    private async Task SendWhatsAppAsync(OrderNotificationData order, CancellationToken ct)
    {
        var waPhone  = _config["Notifications:WhatsAppPhone"] ?? "+905388952964";
        var instanceId = _config["Notifications:GreenApiInstanceId"] ?? "";
        var apiToken = _config["Notifications:GreenApiToken"] ?? "";

        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(apiToken))
        {
            _logger.LogWarning("GreenAPI credentials not configured — skipping WhatsApp for order {OrderNumber}", order.OrderNumber);
            return;
        }

        // 1. Send to Admin
        var adminText = BuildWhatsAppMessage(order);
        var adminPhone = new string(waPhone.Where(char.IsDigit).ToArray());
        await SendWhatsAppToChatAsync(order, adminPhone, adminText, instanceId, apiToken, ct);

        // 2. Send to Customer
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
        {
            var customerText = BuildCustomerWhatsAppMessage(order);
            var customerPhone = new string(order.CustomerPhone.Where(char.IsDigit).ToArray());
            
            // Format phone with country code if missing
            var dialCode = GetCountryDialCode(order.Country);
            if (!string.IsNullOrEmpty(dialCode))
            {
                if (customerPhone.StartsWith("0"))
                    customerPhone = dialCode + customerPhone.Substring(1);
                else if (!customerPhone.StartsWith(dialCode))
                    customerPhone = dialCode + customerPhone;
            }

            if (customerPhone.Length > 8) // Basic sanity check
            {
                await SendWhatsAppToChatAsync(order, customerPhone, customerText, instanceId, apiToken, ct);
            }
        }
    }

    private static string GetCountryDialCode(string? countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName)) return "";
        return countryName.Trim().ToLowerInvariant() switch
        {
            "egypt" => "20",
            "مصر" => "20",
            "qatar" => "974",
            "قطر" => "974",
            "saudi arabia" => "966",
            "ksa" => "966",
            "السعودية" => "966",
            "uae" => "971",
            "united arab emirates" => "971",
            "الامارات" => "971",
            "kuwait" => "965",
            "الكويت" => "965",
            "tunisia" => "216",
            "تونس" => "216",
            "libya" => "218",
            "ليبيا" => "218",
            "algeria" => "213",
            "الجزائر" => "213",
            "yemen" => "967",
            "اليمن" => "967",
            "jordan" => "962",
            "الاردن" => "962",
            "syria" => "963",
            "سوريا" => "963",
            "lebanon" => "961",
            "لبنان" => "961",
            "morocco" => "212",
            "المغرب" => "212",
            "iraq" => "964",
            "العراق" => "964",
            "bahrain" => "973",
            "البحرين" => "973",
            "oman" => "968",
            "عمان" => "968",
            "palestine" => "970",
            "فلسطين" => "970",
            "turkey" => "90",
            "تركيا" => "90",
            _ => ""
        };
    }

    private async Task SendWhatsAppToChatAsync(OrderNotificationData order, string cleanPhone, string text, string instanceId, string apiToken, CancellationToken ct)
    {
        var chatId = $"{cleanPhone}@c.us";
        try
        {
            var http = _httpClientFactory.CreateClient("callmebot");
            HttpResponseMessage resp;

            if (order.PdfAttachment != null)
            {
                var url = $"https://api.green-api.com/waInstance{instanceId}/sendFileByUpload/{apiToken}";
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(chatId), "chatId");
                form.Add(new StringContent(text), "caption");
                
                var fileContent = new ByteArrayContent(order.PdfAttachment);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                form.Add(fileContent, "file", $"Invoice_{order.OrderNumber}.pdf");
                
                resp = await http.PostAsync(url, form, ct);
            }
            else
            {
                var url = $"https://api.green-api.com/waInstance{instanceId}/sendMessage/{apiToken}";
                var payload = new { chatId, message = text };
                resp = await http.PostAsJsonAsync(url, payload, ct);
            }
            
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp sent for order {OrderNumber} to {Phone}", order.OrderNumber, cleanPhone);
            }
            else
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("WhatsApp returned {Status} for order {OrderNumber} to {Phone}. Body: {Body}", resp.StatusCode, order.OrderNumber, cleanPhone, err);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp for order {OrderNumber} to {Phone}", order.OrderNumber, cleanPhone);
        }
    }

    // ─── Message builders ────────────────────────────────────────────────────
    private static string BuildWhatsAppMessage(OrderNotificationData o)
    {
        var items = string.Join("\n", o.Items.Select(i => $"  • {i.ProductName} x{i.Quantity} = {i.UnitPrice * i.Quantity:N2} EGP"));
        return $"""
🛒 *New Order — #{o.OrderNumber}*
━━━━━━━━━━━━━━━━━━━━
👤 Customer: {o.CustomerName}
📱 Phone: {o.CustomerPhone}
📍 Address: {o.Address}
🌍 Country: {o.Country}
💳 Payment: {o.PaymentMethod}
━━━━━━━━━━━━━━━━━━━━
📦 *Items:*
{items}
━━━━━━━━━━━━━━━━━━━━
💰 *Total: {o.TotalAmount:N2} EGP*
🕒 {o.CreatedAt:dd MMM yyyy HH:mm} UTC
""";
    }

    private static string BuildCustomerWhatsAppMessage(OrderNotificationData o)
    {
        var items = string.Join("\n", o.Items.Select(i => $"  • {i.ProductName} x{i.Quantity}"));
        return $"""
🎉 *شكراً لطلبك من LoxxKing!*
━━━━━━━━━━━━━━━━━━━━
أهلاً بك {o.CustomerName}،
تم استلام طلبك بنجاح، رقم الطلب الخاص بك هو: *#{o.OrderNumber}*

مرفق مع هذه الرسالة إيصال الدفع بصيغة PDF.

📦 *مشترياتك:*
{items}
━━━━━━━━━━━━━━━━━━━━
💰 *الإجمالي: {o.TotalAmount:N2} EGP*

سيتم التواصل معك قريباً لتأكيد الشحن.
نتمنى لك يوماً سعيداً! 👑
""";
    }

    private static string BuildInvoiceHtml(OrderNotificationData o)
    {
        var rows = o.Items.Select(i => $"""
            <tr>
              <td style="padding:10px 14px;border-bottom:1px solid #e5e7eb;">{i.ProductName}</td>
              <td style="padding:10px 14px;border-bottom:1px solid #e5e7eb;text-align:center;">{i.Quantity}</td>
              <td style="padding:10px 14px;border-bottom:1px solid #e5e7eb;text-align:right;">{i.UnitPrice:N2} EGP</td>
              <td style="padding:10px 14px;border-bottom:1px solid #e5e7eb;text-align:right;font-weight:600;">{i.UnitPrice * i.Quantity:N2} EGP</td>
            </tr>
        """).Aggregate("", (a, b) => a + b);

        return $"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Invoice #{o.OrderNumber}</title>
</head>
<body style="margin:0;padding:0;background:#f3f4f6;font-family:'Segoe UI',Arial,sans-serif;">
  <div style="max-width:620px;margin:32px auto;background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

    <!-- Header -->
    <div style="background:linear-gradient(135deg,#4f46e5,#7c3aed);padding:32px 36px;color:#fff;">
      <div style="display:flex;justify-content:space-between;align-items:flex-start;">
        <div>
          <div style="font-size:28px;font-weight:800;letter-spacing:-0.5px;">LoxxKing</div>
          <div style="opacity:0.8;font-size:13px;margin-top:2px;">Order Invoice</div>
        </div>
        <div style="text-align:right;">
          <div style="background:rgba(255,255,255,0.15);border-radius:8px;padding:8px 16px;">
            <div style="font-size:11px;opacity:0.8;">ORDER NUMBER</div>
            <div style="font-size:20px;font-weight:700;">#{o.OrderNumber}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- Customer Info -->
    <div style="padding:24px 36px;display:flex;gap:24px;background:#f9fafb;border-bottom:1px solid #e5e7eb;">
      <div style="flex:1;">
        <div style="font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:6px;">Customer</div>
        <div style="font-size:15px;font-weight:600;color:#111827;">{o.CustomerName}</div>
        <div style="font-size:13px;color:#6b7280;margin-top:2px;">{o.CustomerPhone}</div>
      </div>
      <div style="flex:1;">
        <div style="font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:6px;">Delivery</div>
        <div style="font-size:13px;color:#374151;">{o.Address}</div>
        <div style="font-size:13px;color:#6b7280;">{o.Country}</div>
      </div>
      <div style="flex:1;">
        <div style="font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:6px;">Date</div>
        <div style="font-size:13px;color:#374151;">{o.CreatedAt:dd MMM yyyy}</div>
        <div style="font-size:13px;color:#6b7280;">{o.CreatedAt:HH:mm} UTC</div>
        <div style="margin-top:6px;display:inline-block;background:#dbeafe;color:#1d4ed8;border-radius:99px;padding:2px 10px;font-size:11px;font-weight:600;">{o.PaymentMethod}</div>
      </div>
    </div>

    <!-- Items Table -->
    <div style="padding:24px 36px;">
      <table style="width:100%;border-collapse:collapse;">
        <thead>
          <tr style="background:#f3f4f6;">
            <th style="padding:10px 14px;text-align:left;font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;">Product</th>
            <th style="padding:10px 14px;text-align:center;font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;">Qty</th>
            <th style="padding:10px 14px;text-align:right;font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;">Unit Price</th>
            <th style="padding:10px 14px;text-align:right;font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:0.5px;">Total</th>
          </tr>
        </thead>
        <tbody>
          {rows}
        </tbody>
      </table>
    </div>

    <!-- Total -->
    <div style="padding:0 36px 28px;display:flex;justify-content:flex-end;">
      <div style="background:linear-gradient(135deg,#4f46e5,#7c3aed);color:#fff;border-radius:12px;padding:16px 28px;text-align:right;min-width:200px;">
        <div style="font-size:12px;opacity:0.8;margin-bottom:4px;">TOTAL AMOUNT</div>
        <div style="font-size:28px;font-weight:800;">{o.TotalAmount:N2} EGP</div>
      </div>
    </div>

    <!-- Notes -->
    <div style="padding:0 36px 28px;font-size:12px;color:#9ca3af;text-align:center;border-top:1px solid #f3f4f6;padding-top:20px;">
      This is an automated invoice from the LoxxKing system. Please do not reply to this email.
    </div>
  </div>
</body>
</html>
""";
    }
}
