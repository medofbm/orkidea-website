using System.Text.RegularExpressions;
using Orkideya.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Orkideya.Services;

public class WhatsAppNotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppNotificationService> _logger;
    public WhatsAppNotificationService(IConfiguration configuration, ILogger<WhatsAppNotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendNotification(Order order)
    {
        // Explicit opt-in; building/testing/staging must not send real messages.
        if (!_configuration.GetValue<bool>("Notifications:WhatsAppEnabled")) return;
        var accountSid = _configuration["Twilio:AccountSID"];
        var authToken = _configuration["Twilio:AuthToken"];
        var sender = _configuration["Twilio:PhoneNumber"];
        var recipient = _configuration["Twilio:AdminNumber"];
        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) ||
            !IsInternationalPhone(sender) || !IsInternationalPhone(recipient))
        {
            _logger.LogWarning("WhatsApp notification configuration is incomplete; no message was sent.");
            return;
        }
        try
        {
            TwilioClient.Init(accountSid, authToken);
            var messageOptions = new CreateMessageOptions(new PhoneNumber($"whatsapp:{recipient}"))
            {
                From = new PhoneNumber($"whatsapp:{sender}"),
                Body = $"طلب جديد! رقم الطلب: {order.OrderId}\n" +
                    $"اسم العميل: {order.CustomerName}\n" +
                    $"رقم الواتساب: {order.WhatsAppNumber}\n" +
                    $"المبلغ الإجمالي: {order.TotalAmount:0.00} د.ل"
            };
            await MessageResource.CreateAsync(messageOptions);
        }
        catch (Exception exception)
        {
            // Do not log provider payloads, tokens, phone numbers or customer content.
            _logger.LogWarning("WhatsApp notification failed for order {OrderId}; failure type {FailureType}.",
                order.OrderId, exception.GetType().Name);
        }
    }

    private static bool IsInternationalPhone(string? value) => value != null &&
        Regex.IsMatch(value, @"^\+[1-9][0-9]{6,14}$");
}
