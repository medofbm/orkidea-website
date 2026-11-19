# Order status history release

This is the first order/inventory foundation batch. It does not reserve or consume stock, change the five existing status values, or equate Processing with the newly agreed confirmation step. Oil reservations after admin confirmation and consumption on filling are the next separate feature.

## Schema and behaviour
- Add Orders.StatusRevision (existing rows start at zero) and OrderStatusChanges. No synthetic historic audit events, data deletion, or notification backfill.
- Admin status POST requires the revision rendered in the page. Authenticated user ID is taken server-side. One SaveChanges commits state, revision and audit together. Concurrent losers get HTTP409; refresh before retry.
- Retrying the current state/current revision creates no history entry. An old revision conflicts even if the requested status now matches.
- Details show UTC change time, before/after and account name. A deleted account has a readable fallback; its original ID remains in history. Admin-only access remains.
- Orders with history cannot be physically deleted (controller message plus restrictive FK). Legacy orders without history retain the previous deletion action. This is not a general immutable audit system or complete soft-delete conversion.

## Deployment boundary
Not applied to VPS by development tests. Before staging deployment, review the additive SQL migration 20261029180000_OrderStatusHistory, run all isolated CI gates, stop the staging web writer, apply migration only through its dedicated maintenance credentials, then start the new web image and verify readiness and synthetic admin flow. Keep notifications disabled. Other VPS projects are outside scope. Do not run old and new writers concurrently.

The old application can still write status without revisions/history, so returning to an old writable image after adopting this feature is not an acceptable rollback. Prefer a forward fix on the new schema; if unavailable, stop the staging web writer while diagnosing. Down migration explicitly refuses when history exists. Do not delete history or reset revisions to force a downgrade. No new backup automation is part of this release, per user direction.

## Acceptance
SQL regression covers upgrade preservation/model parity, audit data/UTC, replay, stale/missing/invalid inputs, atomic failure, parallel contenders, restrictive deletion and guarded downgrade. Real Identity HTTP checks cover server-derived actor, HTTP409, details and deletion guard. No test claims stock accounting or live VPS deployment.
