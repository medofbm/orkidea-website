# Delivery tracking and non-delivery returns

## Scope
Orkidea records the delivery operator, optional shipment reference and delivery/return events. Darb Al Sabeel manages its own wallet, collections, withdrawals and settlements in its external app; this release adds no carrier financial ledger, provider API integration, payment confirmation or real notification.

Current delivery states: HandedOver, Delivered, Returning and Returned. Returning means the customer did not receive the parcel and it is awaiting return; Returned means the administrator explicitly confirmed receipt of the whole parcel. These are not customer refunds after successful receipt.

## Integrity and user actions
- Explicit handoff requires Processing, at least one order item, and Filled for tracked stock. Untracked historical orders require manual preparation confirmation and never acquire fabricated stock snapshots.
- Each change uses the shared order StatusRevision plus the delivery revision and one atomic SaveChanges for the current delivery, order and immutable application-level event. OrderStatusChanges is added only when the coarse order status actually changes. Returning stays Shipped; physical return receipt closes the order as Cancelled while Filled stays Filled.
- The admin's Identity claim supplies the actor. POST actions require Admin and CSRF. A request GUID is globally unique and bound to a SHA-256 fingerprint of normalized payload, order, expected revision and actor; equal replay is a no-op, changed payload conflicts. Competing versions cannot both commit.
- Return receipt requires explicit confirmation. Resuming delivery before physical return needs a reason. Redispatch requires a received return, a reason and explicit confirmation that the same complete package was reviewed and is suitable for its original customer; it never reserves/consumes bulk stock again. Reallocation to another order, partial deliveries/returns, split shipments and packing new goods are outside this operation.
- Mistaken carrier/reference data can be corrected with a reason and new snapshots. A mistakenly recorded Delivered can be corrected back to HandedOver, or Returned back to Returning; previous events remain. These corrections do not refund or move money.
- Generic order-status changes cannot bypass an existing shipment. Tracked orders must use explicit handoff for Shipped/Delivered. Legacy untracked status behavior remains available until explicitly recording a shipment; existing shipping data is never inferred or backfilled from old statuses.
- Old delivery snapshots and audit events are not deletable through admin order deletion. SQL restrict FKs and check constraints protect parents, metadata, transitions and required confirmations/reasons.

## Migration and deployment
Migration: 20261031215916_OrderDeliveryTracking. It creates OrderDeliveries and OrderDeliveryEvents with restrictive FKs, indexes and constraints, and leaves existing orders, stock, money columns, notifications and history untouched. Down refuses to drop any delivery records/events (SQL 51004). No fake shipment is seeded.

Build and validate the reviewed revision first. Stop only the staging web writer, run the existing initializer with the staging migration identity and bootstrap credentials blank, and verify both new tables, migration history and unchanged pre-existing counts before bringing up the new web image. Database container and named volumes stay in place. Ready checks already require no pending migrations; connection-only success is insufficient.

Do not run old/new writers together. Once delivery records exist, reverting to a pre-delivery web image would bypass the workflow guards even if the schema remains: use a forward fix. Before any delivery records have been created, a failed web rollout may restore the previously verified image, preserving the additive empty schema. No production migration or new backup scheduling is performed by preparing this release.

## Acceptance evidence to collect
1. Release build, EF model/snapshot parity and existing JS/security/backend checks.
2. Disposable SQL: full lifecycle, same-key/different-key concurrent handoff, delivery/return race, event SQL failure rollback, ledger/amount invariance and guarded downgrade.
3. Real Identity-cookie HTTP forms: role/CSRF, authentic actor, replay/stale page, physical receipt confirmation, same-package redispatch, rendered delivery list/details and protected deletion.
4. Staging: exact image/schema, readiness and a bounded synthetic UI journey with independent SQL readback. CI alone does not prove this deployment or real carrier integration.
