using Microsoft.EntityFrameworkCore;
using Orkideya.Data;
using Orkideya.Models;

namespace Orkideya.Application.Accounts;

/// <summary>Records when an account was created and when it signed in (for the admin «الزبائن» page).</summary>
public interface IAccountActivityRecorder
{
    /// <summary>A new account was created. <paramref name="signedIn"/> is true when the user is signed in right away.</summary>
    Task RecordCreatedAsync(string userId, bool signedIn, CancellationToken cancellationToken = default);

    /// <summary>A successful sign-in (password or verification code).</summary>
    Task RecordSignInAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Database recorder. Activity is a convenience for the admin, so a failure here is logged and never blocks
/// registration or sign-in.
/// </summary>
public sealed class AccountActivityRecorder(ApplicationDbContext db, TimeProvider clock,
    ILogger<AccountActivityRecorder> logger) : IAccountActivityRecorder
{
    public async Task RecordCreatedAsync(string userId, bool signedIn, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var activity = new AccountActivity
        {
            UserId = userId, CreatedAtUtc = now,
            LastSignInAtUtc = signedIn ? now : null, SignInCount = signedIn ? 1 : 0
        };
        try
        {
            if (await db.AccountActivities.AnyAsync(row => row.UserId == userId, cancellationToken)) return;
            db.AccountActivities.Add(activity);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never leave a failed row tracked: a later SaveChanges in the same request would retry it.
            db.Entry(activity).State = EntityState.Detached;
            logger.LogWarning("Account activity (created) was not recorded: {Error}", exception.GetType().Name);
        }
    }

    public async Task RecordSignInAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            // Atomic increment, so two sign-ins at the same moment both count.
            if (await IncrementAsync(userId, now, cancellationToken) > 0) return;
            // No row yet: the account is older than this table, so its creation date stays unknown.
            var activity = new AccountActivity { UserId = userId, CreatedAtUtc = null, LastSignInAtUtc = now, SignInCount = 1 };
            db.AccountActivities.Add(activity);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Another request inserted the row first: count this sign-in on that row.
                db.Entry(activity).State = EntityState.Detached;
                await IncrementAsync(userId, now, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            foreach (var entry in db.ChangeTracker.Entries<AccountActivity>().Where(entry => entry.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            logger.LogWarning("Account activity (sign-in) was not recorded: {Error}", exception.GetType().Name);
        }
    }

    private Task<int> IncrementAsync(string userId, DateTime now, CancellationToken cancellationToken) =>
        db.AccountActivities.Where(activity => activity.UserId == userId).ExecuteUpdateAsync(setters => setters
            .SetProperty(activity => activity.LastSignInAtUtc, now)
            .SetProperty(activity => activity.SignInCount, activity => activity.SignInCount + 1), cancellationToken);
}
