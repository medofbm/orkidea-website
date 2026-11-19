# Bulk stock reservations and filling

## Approved operating rule
New orders do not reserve stock. An administrator explicitly confirms availability to reserve it, then records filling/preparation to consume the reserved amount. Oil uses millilitres; soap uses whole pieces. The explicit reservation action advances Pending to Processing with a status-history entry, so confirmed requests leave the new-order queue. Setting Processing through the generic status menu never implicitly reserves/consumes stock. Shipping/delivery of a tracked order requires filling first.

Example (synthetic): 1000ml on hand; 2 x 100ml + 1 x 250ml reserves 450ml, leaving 550ml available and 1000ml physically on hand. Filling changes on hand to 550ml and reserved to zero. Cancellation before filling releases the reservation once. Cancellation after filling does not restore the bulk oil; inspect any returned goods and record a deliberate correction with a reason if appropriate. Ready filled bottles, lot/expiry tracking, packaging consumption and returns grading are not implemented in this batch.

## Configuration and legacy data
- Admin > Inventory: explicitly enable each product in ml or pieces, starting at zero. Record the actual opening stock as an addition with a reason/reference. Never infer balances or numeric volume from a size label. Unit changes/deleting accounts are not offered after setup.
- Configure consumption per variant. A configured stock product with an unconfigured size cannot complete checkout. Configure every sellable size before commercial use.
- New carts mixing tracked and untracked products fail closed until all products in that cart are configured; this prevents a mixed order bypassing the stock lifecycle. Carts containing only wholly untracked products retain the legacy workflow. Do not enable products gradually in a live shop without completing the configuration and checking pending legacy orders.
- Existing orders/items stay Untracked with null stock snapshots. No inferred historical reservations, movements, balances or notifications. They retain the previous manual status flow and must be reconciled separately before accounting for them in opening stock. No automatic legacy conversion.
- Each new tracked item captures account, base unit and consumption per sold unit. Subsequent variant edits/deletion cannot recalculate a stored order. Configured variants cannot be reassigned to another product; concurrency checks prevent a simultaneous reassignment/configuration race.

## Consistency and access
Order stock actions share the existing StatusRevision concurrency token with status changes/cancellation. Each affected stock account also has a revision. One SaveChanges transaction includes all accounts, order state and ledger movements; failures roll back the entire multi-product operation. Conflicts require a refreshed form, not an unconditional retry. Reserved <= on hand, nonnegative balances and integer pieces are also SQL constraints.

Repeated confirmation/filling never creates a second movement. Adjustment forms use a request GUID, recorded as the movement key; replaying the same payload is a no-op and changing that payload conflicts. All adjustments require a reason. Server Identity supplies the actor; clients cannot select it. Ledger rows have signed physical/reserved deltas, UTC, reference and reason. The ledger is append-only through available app actions, not tamper-proof against a privileged database owner.

Admin-only forms have antiforgery protection. Stock ledger has 50-row pages. Orders/products linked to stock history cannot be physically deleted. Cancellation of tracked orders uses the details form with a reason. Filling and stock cancellation are distinct from cash collection and do not record courier income, withdrawals or a payment event. No real messages are enabled.

## Schema and release boundary
Migration 20261031155358_BulkStockReservations adds StockAccounts/StockMovements, optional item/variant snapshots, StockState default Untracked, and cancellation reason. It does not alter existing monetary SQL column types; the four existing decimal(18,2) mappings are now explicit. Stock quantities use decimal(18,3).

The migration was generated with EF 10.0.14; additional concurrency metadata and protective check/downgrade guards were reviewed, then model parity checked with EF. Down refuses to discard any configuration, movement, tracked state or cancellation reason. Do not force a downgrade by deleting ledger rows. Never run an old stock-unaware writer alongside the new release. Prefer forward fixes or stop only the staging writer during diagnosis.

This source change is not a VPS migration/deployment. Before staging update, verify the approved commit/image, expected schema and unchanged legacy fixtures; stop only staging web, apply with its dedicated migration identity, start the new image and check readiness/data/role behaviour. No new backup schedule or offsite setup is part of this work, per the user's instruction. Root production launch, real stock opening quantities, prices and delivery promises remain separate.

## Verification
SQL regressions cover snapshot/no-reservation checkout, retries, missing/mixed configuration rejection, shared-size calculation, immutable snapshots, cancel-before/after-filling, whole pieces, balance constraints, multi-product insufficiency and atomic SQL failure, concurrent overbooking/fill-cancel races, adjustment replay, ledger reconciliation and guarded downgrade. Real Identity HTTP exercises setup/size/receipt, repeated receipt, reserve/fill/stale form/cancel. Role tests protect every new action and CSRF. Build/CI acceptance and staging acceptance must be recorded separately.
