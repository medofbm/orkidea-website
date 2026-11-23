using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace Orkideya.Services.Email;

/// <summary>Sends HTML email (with a plain-text alternative) over SMTP with STARTTLS. Never logs addresses, links or tokens.</summary>
public sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Smtp.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            logger.LogWarning("Email is not configured (Email:Smtp:Host / Email:FromEmail empty); message not sent.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
            Body = ToPlainText(htmlMessage)
        };
        message.To.Add(new MailAddress(email));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlMessage, Encoding.UTF8, "text/html"));

        using var client = new SmtpClient(settings.Smtp.Host, settings.Smtp.Port)
        {
            EnableSsl = true,
            Timeout = 15000,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(settings.Smtp.Username, settings.Smtp.Password)
        };
        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Messages can contain addresses, so only codes and types are logged.
            logger.LogError("Email sending failed ({ErrorType}, SMTP status {SmtpStatus}, cause {Cause}).",
                ex.GetType().Name, DescribeStatus(ex), DescribeCause(ex));
        }
    }

    /// <summary>SMTP reply code, e.g. MustIssueStartTlsFirst or 535 auth failure (GeneralFailure covers network errors).</summary>
    public static string DescribeStatus(Exception ex) =>
        ex is SmtpException smtp ? $"{smtp.StatusCode} ({(int)smtp.StatusCode})" : "none";

    /// <summary>Type of the innermost exception, plus the socket error for network failures. Never includes text.</summary>
    public static string DescribeCause(Exception ex)
    {
        var inner = ex;
        while (inner.InnerException is not null) inner = inner.InnerException;
        if (ReferenceEquals(inner, ex)) return "none";
        return inner is System.Net.Sockets.SocketException socket
            ? $"{inner.GetType().Name}:{socket.SocketErrorCode}"
            : inner.GetType().Name;
    }

    /// <summary>Plain-text version: links become "label: url", other tags are removed.</summary>
    public static string ToPlainText(string html)
    {
        var text = Regex.Replace(html, "<style.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = AnchorRegex().Replace(text, m => m.Groups[2].Value.Trim() + ": " + WebUtility.HtmlDecode(m.Groups[1].Value));
        text = Regex.Replace(text, "<(br|/p|/h1|/div)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\n\s*\n+", "\n\n").Trim();
    }

    [GeneratedRegex("<a[^>]*href=\"([^\"]+)\"[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();
}
