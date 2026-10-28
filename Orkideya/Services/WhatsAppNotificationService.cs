using System.Text.RegularExpressions;
using Orkideya.Application.Notifications;
using Orkideya.Models;
using Twilio.Clients;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Orkideya.Services;

public sealed class WhatsAppNotificationService(IConfiguration configuration, ILogger<WhatsAppNotificationService> logger)
    : IOrderNotificationTransport
{
    public bool IsConfigured => configuration.GetValue<bool>("Notifications:WhatsAppEnabled") &&
        !string.IsNullOrWhiteSpace(configuration["Twilio:AccountSID"]) &&
        !string.IsNullOrWhiteSpace(configuration["Twilio:AuthToken"]) &&
        IsInternationalPhone(configuration["Twilio:PhoneNumber"]) && IsInternationalPhone(configuration["Twilio:AdminNumber"]);

    public async Task<NotificationAttempt> SendAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Notifications:WhatsAppEnabled")) return new(NotificationOutcome.NotConfigured);
        if (!IsConfigured)
        {
            logger.LogWarning("WhatsApp notification configuration is incomplete; no message was sent.");
            return new(NotificationOutcome.NotConfigured);
        }
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Per-call client avoids mutable global credentials; accepted is not delivered.
            var client = new TwilioRestClient(configuration["Twilio:AccountSID"], configuration["Twilio:AuthToken"]);
            var options = new CreateMessageOptions(new PhoneNumber($"whatsapp:{configuration["Twilio:AdminNumber"]}"))
            {
                From = new PhoneNumber($"whatsapp:{configuration["Twilio:PhoneNumber"]}"),
                Body = $"طلب جديد! رقم الطلب: {order.OrderId}\n" +
                    $"اسم العميل: {order.CustomerName}\nرقم الواتساب: {order.WhatsAppNumber}\n" +
                    $"المبلغ الإجمالي: {order.TotalAmount:0.00} د.ل"
            };
            var message = await MessageResource.CreateAsync(options, client: client)
                .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return string.IsNullOrWhiteSpace(message.Sid) ? new(NotificationOutcome.Uncertain)
                : new(NotificationOutcome.Accepted, message.Sid);
        }
        catch (ApiException exception) when (exception.Status == 429)
        { return new(NotificationOutcome.Retryable); }
        catch (ApiException exception) when (exception.Status is >= 400 and < 500)
        { return new(NotificationOutcome.PermanentFailure); }
        catch (Exception)
        {
            // A timeout, 5xx or lost response may follow provider acceptance. Do not auto-resend.
            return new(NotificationOutcome.Uncertain);
        }
    }

    private static bool IsInternationalPhone(string? value) => value != null &&
        Regex.IsMatch(value, @"^\+[1-9][0-9]{6,14}$");
}
