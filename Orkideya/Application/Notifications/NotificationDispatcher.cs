using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Notifications;

public sealed class NotificationDispatcher(ApplicationDbContext context, IOrderNotificationTransport transport,
    IConfiguration configuration, TimeProvider clock)
{
    public const int MaxAttempts = 5;

    public async Task<int> DispatchAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Notifications:OutboxWorkerEnabled") ||
            !configuration.GetValue<bool>("Notifications:WhatsAppEnabled") || !transport.IsConfigured) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        // An expired claim might already have reached the provider. Never resend it blindly.
        await context.OrderNotifications.Where(n => n.State == NotificationState.Processing && n.LeaseUntilUtc <= now)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.State, NotificationState.NeedsReview)
                .SetProperty(n => n.LastFailureCode, "lease_expired")
                .SetProperty(n => n.LeaseToken, (Guid?)null).SetProperty(n => n.LeaseUntilUtc, (DateTime?)null), cancellationToken);
        var candidates = await context.OrderNotifications.AsNoTracking()
            .Where(n => n.State == NotificationState.Pending && n.NextAttemptAtUtc <= now && n.Attempts < MaxAttempts)
            .OrderBy(n => n.CreatedAtUtc).Take(20).Select(n => n.Id).ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var id in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            now = clock.GetUtcNow().UtcDateTime;
            var lease = Guid.NewGuid();
            var claimed = await context.OrderNotifications
                .Where(n => n.Id == id && n.State == NotificationState.Pending && n.NextAttemptAtUtc <= now && n.Attempts < MaxAttempts)
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.State, NotificationState.Processing)
                    .SetProperty(n => n.Attempts, n => n.Attempts + 1)
                    .SetProperty(n => n.LeaseToken, lease).SetProperty(n => n.LeaseUntilUtc, now.AddMinutes(2)), cancellationToken);
            if (claimed != 1) continue;
            var record = await context.OrderNotifications.AsNoTracking().Include(n => n.Order)
                .SingleOrDefaultAsync(n => n.Id == id && n.LeaseToken == lease, cancellationToken);
            if (record == null) continue;
            NotificationAttempt attempt;
            try { attempt = await transport.SendAsync(record.Order, cancellationToken); }
            catch (Exception) { attempt = new(NotificationOutcome.Uncertain); }

            var state = NotificationState.NeedsReview;
            string? code = "uncertain_outcome", providerId = null;
            var completedAt = clock.GetUtcNow().UtcDateTime;
            var next = completedAt;
            if (attempt.Outcome == NotificationOutcome.Accepted &&
                !string.IsNullOrWhiteSpace(attempt.ProviderMessageId) && attempt.ProviderMessageId.Length <= 64)
            { state = NotificationState.Accepted; code = null; providerId = attempt.ProviderMessageId; }
            else if (attempt.Outcome == NotificationOutcome.Retryable)
            {
                state = record.Attempts < MaxAttempts ? NotificationState.Pending : NotificationState.Failed;
                code = state == NotificationState.Failed ? "retry_exhausted" : "rate_limited";
                next = completedAt.AddMinutes(Math.Min(60, Math.Pow(2, record.Attempts)));
            }
            else if (attempt.Outcome == NotificationOutcome.PermanentFailure)
            { state = NotificationState.Failed; code = "provider_rejected"; }
            else if (attempt.Outcome == NotificationOutcome.NotConfigured)
            { code = "configuration_changed"; }

            // A provider acceptance followed by a DB failure remains a claim for reconciliation,
            // not an invitation to send again. The bounded acknowledgement survives shutdown cancellation.
            using var acknowledgement = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await context.OrderNotifications.Where(n => n.Id == id && n.State == NotificationState.Processing && n.LeaseToken == lease)
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.State, state)
                    .SetProperty(n => n.ProviderMessageId, providerId).SetProperty(n => n.LastFailureCode, code)
                    .SetProperty(n => n.NextAttemptAtUtc, next).SetProperty(n => n.LeaseToken, (Guid?)null)
                    .SetProperty(n => n.LeaseUntilUtc, (DateTime?)null), acknowledgement.Token);
            processed++;
        }
        return processed;
    }
}
