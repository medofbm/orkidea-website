using Orkideya.Models;

namespace Orkideya.Application.Notifications;

public enum NotificationOutcome { Accepted, Retryable, PermanentFailure, Uncertain, NotConfigured }
public sealed record NotificationAttempt(NotificationOutcome Outcome, string? ProviderMessageId = null);

public interface IOrderNotificationTransport
{
    bool IsConfigured { get; }
    Task<NotificationAttempt> SendAsync(Order order, CancellationToken cancellationToken);
}
