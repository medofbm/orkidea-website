using Orkideya.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Orkideya.Services
{
    public class WhatsAppNotificationService
    {
        private readonly IConfiguration _configuration;

        public WhatsAppNotificationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendNotification(Order order)
        {
            var accountSid = _configuration["Twilio:AccountSID"];
            var authToken = _configuration["Twilio:AuthToken"];
            var twilioNumber = _configuration["Twilio:PhoneNumber"];

            // --- رقم هاتفك الشخصي الذي سيستقبل الإشعار ---
            var adminNumber = "+218916749962";

            TwilioClient.Init(accountSid, authToken);

            var messageBody = $"طلب جديد! 🎉\n" +
                              $"رقم الطلب: {order.OrderId}\n" +
                              $"اسم العميل: {order.CustomerName}\n" +
                              $"رقم الواتساب: {order.WhatsAppNumber}\n" +
                              $"المبلغ الإجمالي: {order.TotalAmount:0.00} د.ل";

            try
            {
                var messageOptions = new CreateMessageOptions(
                    new PhoneNumber($"whatsapp:{adminNumber}"))
                {
                    From = new PhoneNumber($"whatsapp:{twilioNumber}"),
                    Body = messageBody
                };

                var message = await MessageResource.CreateAsync(messageOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}