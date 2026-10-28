using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Orkideya.Application.Notifications;
using Orkideya.Application.Orders;
using Orkideya.Data;
using Orkideya.Models;

internal static class OutboxScenario
{
    public static async Task Run(DbContextOptions<ApplicationDbContext> options, Action<bool, string> check)
    {
        ApplicationDbContext Open() => new(options);
        await using (var guard = Open())
            if (!guard.Database.GetDbConnection().Database.StartsWith("OrkideaTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Outbox fixtures require the generated test database.");
        var clock = new TestClock();
        var enabled = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Notifications:WhatsAppEnabled"] = "true", ["Notifications:OutboxWorkerEnabled"] = "true" }).Build();
        async Task<Guid> Seed()
        {
            await using var db = Open();
            await db.OrderNotifications.ExecuteDeleteAsync(); // Only generated synthetic fixtures are cleared.
            var order = new Order { CheckoutToken = Guid.NewGuid(), CheckoutRequestHash = new string('A', 64),
                CustomerName = "Synthetic notification customer", WhatsAppNumber = "+218000000000", City = "Tripoli",
                ShippingAddress = "Synthetic address", ShippingCost = 10, TotalAmount = 11,
                Items = new() { new() { ProductId = 1, UnitPrice = 1, Quantity = 1 } } };
            await new OrderService(db).PlaceAsync(order);
            if (!(await new OrderService(db).PlaceAsync(order)).IsNew && order.Notifications.Count == 1) { }
            else throw new InvalidOperationException("Reusing a committed aggregate must not create another notification.");
            return order.Notifications.Single().Id;
        }
        async Task<OrderNotification> Row(Guid id)
        { await using var db = Open(); return await db.OrderNotifications.AsNoTracking().SingleAsync(n => n.Id == id); }
        async Task<int> Dispatch(FakeTransport transport, IConfiguration? config = null)
        { await using var db = Open(); return await new NotificationDispatcher(db, transport, config ?? enabled, clock).DispatchAsync(); }

        var id = await Seed();
        var fake = new FakeTransport();
        await Dispatch(fake, new ConfigurationBuilder().Build());
        check(fake.Calls == 0 && (await Row(id)).Attempts == 0, "Disabled flags leave the outbox pending without contacting a provider");
        fake.Configured = false;
        await Dispatch(fake);
        check(fake.Calls == 0 && (await Row(id)).Attempts == 0, "Missing provider configuration does not consume attempts");
        fake.Configured = true;
        await Dispatch(fake); await Dispatch(fake);
        var row = await Row(id);
        check(fake.Calls == 1 && row.State == NotificationState.Accepted, "Provider acceptance is stored and not sent twice");
        check(row.ProviderMessageId != null && row.Attempts == 1 && row.LeaseToken == null, "Accepted event retains the provider reference and releases its claim");

        id = await Seed();
        fake = new FakeTransport { Result = call => new(call == 1 ? NotificationOutcome.Retryable : NotificationOutcome.Accepted, "SM-synthetic") };
        await Dispatch(fake);
        row = await Row(id);
        check(row.State == NotificationState.Pending && row.Attempts == 1 && row.NextAttemptAtUtc > clock.GetUtcNow().UtcDateTime, "A definite rate-limit rejection schedules backoff");
        await Dispatch(fake);
        check(fake.Calls == 1, "A retry is not attempted before its due time");
        clock.Advance(TimeSpan.FromMinutes(3)); await Dispatch(fake);
        check(fake.Calls == 2 && (await Row(id)).State == NotificationState.Accepted, "A due retry can succeed without creating another event");

        id = await Seed(); fake = new FakeTransport { Result = _ => new(NotificationOutcome.Retryable) };
        for (var i = 0; i < NotificationDispatcher.MaxAttempts + 1; i++)
        { await Dispatch(fake); clock.Advance(TimeSpan.FromHours(1)); }
        check(fake.Calls == NotificationDispatcher.MaxAttempts && (await Row(id)).State == NotificationState.Failed,
            "Rate-limited retries stop at the configured attempt cap");

        id = await Seed(); fake = new FakeTransport { Result = _ => new(NotificationOutcome.PermanentFailure) };
        await Dispatch(fake); await Dispatch(fake);
        check(fake.Calls == 1 && (await Row(id)).State == NotificationState.Failed, "Permanent rejection is not retried automatically");

        id = await Seed(); fake = new FakeTransport { Result = _ => new(NotificationOutcome.Uncertain) };
        await Dispatch(fake); await Dispatch(fake);
        check(fake.Calls == 1 && (await Row(id)).State == NotificationState.NeedsReview, "An ambiguous provider outcome requires reconciliation instead of blind resend");

        id = await Seed(); fake = new FakeTransport { Throw = true };
        await Dispatch(fake); await Dispatch(fake);
        row = await Row(id);
        check(fake.Calls == 1 && row.State == NotificationState.NeedsReview, "A transport exception is durable and is not resent blindly");
        check(row.LastFailureCode == "uncertain_outcome", "Provider exception content is not persisted in the failure code");

        id = await Seed(); fake = new FakeTransport();
        await using (var db = Open())
            await db.OrderNotifications.Where(n => n.Id == id).ExecuteUpdateAsync(set => set
                .SetProperty(n => n.State, NotificationState.Processing).SetProperty(n => n.Attempts, 1)
                .SetProperty(n => n.LeaseToken, Guid.NewGuid()).SetProperty(n => n.LeaseUntilUtc, clock.GetUtcNow().UtcDateTime.AddMinutes(1)));
        await Dispatch(fake);
        check(fake.Calls == 0 && (await Row(id)).State == NotificationState.Processing, "Another worker's unexpired claim is respected");
        clock.Advance(TimeSpan.FromMinutes(3)); await Dispatch(fake);
        check(fake.Calls == 0 && (await Row(id)).State == NotificationState.NeedsReview, "An expired claim after a crash is held for review, not replayed");

        id = await Seed(); fake = new FakeTransport { Delay = true };
        var counts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Dispatch(fake)));
        check(counts.Sum() == 1 && fake.Calls == 1, "Eight concurrent workers make exactly one provider attempt");
        check((await Row(id)).State == NotificationState.Accepted && (await Row(id)).Attempts == 1, "The winning worker durably acknowledges the single claim");

        id = await Seed(); fake = new FakeTransport();
        await using (var db = Open())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderNotifications ADD CONSTRAINT CK_OutboxAckFailure CHECK (State <> N'Accepted')");
        var ackFailed = false;
        try { await Dispatch(fake); }
        catch (SqlException exception) when (exception.Number == 547) { ackFailed = true; }
        check(ackFailed && fake.Calls == 1 && (await Row(id)).State == NotificationState.Processing, "Provider acceptance followed by DB failure retains an unresolved claim");
        await using (var db = Open()) await db.Database.ExecuteSqlRawAsync("ALTER TABLE OrderNotifications DROP CONSTRAINT CK_OutboxAckFailure");
        clock.Advance(TimeSpan.FromMinutes(3)); await Dispatch(fake);
        check(fake.Calls == 1 && (await Row(id)).State == NotificationState.NeedsReview, "Recovery after acknowledgement failure does not duplicate the provider request");
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow.AddDays(1);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan interval) => now += interval;
    }
    private sealed class FakeTransport : IOrderNotificationTransport
    {
        private int calls;
        public int Calls => calls;
        public bool Configured { get; set; } = true;
        public bool IsConfigured => Configured;
        public bool Delay { get; set; }
        public bool Throw { get; set; }
        public Func<int, NotificationAttempt> Result { get; set; } = _ => new(NotificationOutcome.Accepted, "SM-synthetic");
        public async Task<NotificationAttempt> SendAsync(Order order, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref calls);
            if (Delay) await Task.Delay(100, cancellationToken);
            if (Throw) throw new InvalidOperationException("Synthetic provider payload must not be persisted.");
            return Result(attempt);
        }
    }
}
