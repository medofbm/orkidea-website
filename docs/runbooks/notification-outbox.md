# Order notification outbox

Checkout commits the order, its items and one notification intent in the same EF transaction. It never calls Twilio from the checkout request. A filtered checkout retry key protects order creation; a unique OrderId index protects notification intent. The additive migration creates an empty outbox and does not send or backfill historical orders.

## Default and activation boundary

Both `Notifications:WhatsAppEnabled` and `Notifications:OutboxWorkerEnabled` default to false. Missing provider configuration prevents claims and consumes no attempts. Real sending requires both explicit flags and complete Twilio configuration (`AccountSID`, `AuthToken`, international `PhoneNumber` and `AdminNumber`), supplied outside Git. Restart the application after changing worker activation: a worker started disabled does not hot-start itself.

Pending intents accumulate while sending is disabled. Before activation, review the pending backlog, age and intended recipient. Use separate staging and production databases; do not enable real sending against synthetic test orders. Apply the reviewed schema with backups before deploying this code to an actual environment. CI and this implementation do not authorize or perform live activation.

## States and recovery

| State | Meaning | Automatic action |
|---|---|---|
| Pending | Intent exists; due time may be in the future | Claim when due and configured |
| Processing | One worker owns a fenced two-minute claim | No other worker sends it |
| Accepted | Provider returned a message reference | Do not send again; this does not mean delivered |
| Failed | Permanent rejection or five-attempt cap reached | Operator review |
| NeedsReview | Ambiguous response, lost acknowledgement or expired claim | Reconcile before any resend |

The worker polls every 30 seconds and considers at most 20 due records per pass. SQL conditional updates make claims and acknowledgements atomic across multiple workers. Each claim increments the attempt count. A definite HTTP 429 rejection is safe to retry with exponential backoff; other 4xx responses are permanent failures. Network failures, timeouts and 5xx responses may follow provider acceptance, so they require reconciliation rather than blind retries.

A 30-second provider wait is bounded independently of the two-minute lease. An expired lease is **not** automatically returned to Pending: a crashed worker may already have sent the message. Provider acceptance followed by a database acknowledgement failure leaves the claim unresolved; recovery holds it for review. An acknowledgement is conditional on the same lease token, so a stale worker cannot overwrite a newer resolution.

No external exactly-once or delivered guarantee is claimed. Delivery callbacks, signature verification, an operator reconciliation UI and live Twilio integration remain separate work. Before retrying NeedsReview, inspect the provider history/reference and order number; never bulk-reset these records to Pending. Do not use the migration Down method as an automatic rollback: it deletes notification intent. Preserve the outbox and disable processing while planning an application/schema rollback.

## Read-only queue inspection

This query contains no credentials or customer contact fields:

```sql
SELECT Id, OrderId, State, Attempts, CreatedAtUtc, NextAttemptAtUtc,
       LeaseUntilUtc, ProviderMessageId, LastFailureCode
FROM OrderNotifications
ORDER BY CreatedAtUtc;
```

The outbox stores an order reference and delivery metadata, not a second customer payload or provider exception text. Fixed failure codes are persisted; credentials, phone numbers, provider response bodies and exception messages are not logged by the transport/worker.

## Verification

The guarded SQL runner uses the application's Identity registration and retry policy, a dedicated generated test database and a fake notification transport only. It checks atomic intent creation/rollback, duplicate order/event protection, disabled and incomplete configuration, due-time/backoff/attempt limits, permanent and ambiguous failures, expired claims, eight competing workers and a real SQL constraint failure after simulated provider acceptance. Existing HTTP checks verify that enabling this code with default flags starts no database work or real notifications.

References: [Twilio 20429](https://www.twilio.com/docs/api/errors/20429), [Messages resource](https://www.twilio.com/docs/messaging/api/message-resource), [EF ExecuteUpdate](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete).
