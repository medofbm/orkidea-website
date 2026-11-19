# Manual Facebook orders

Admins can record an agreed Facebook order from **Admin → طلب من فيسبوك**. Enter the full customer name, phone, address, configured delivery city and up to 30 variant rows. Review the server-calculated prices and shipping fee, then explicitly save as a new order. Confirmation/reservation, filling and delivery remain separate existing actions; creating the order does not reserve stock, send a message, prove collection or access the carrier wallet.

## Integrity and replay

- The browser supplies variant IDs and quantities, never trusted prices, stock consumption, source or creator. Duplicate variants are normalized; their total quantity must not exceed 99. Mixed tracked/untracked items and incomplete stock configuration are rejected consistently with checkout.
- The review is a server-session draft, owned by the authenticated admin, independent of the shopping cart. Confirmation requires CSRF, the request GUID and the fingerprint of the rendered review. Editing or opening another draft makes an older review fail safely. Session expiration/restart before saving requires a new review; an already committed save can still replay to the original order.
- Confirmation re-reads catalogue, rate and consumption configuration inside a short serializable SQL transaction. Any change requires explicit review again. Catalogue/rate/configuration reads remain stable through commit; transient retries repeat the operation using the same key. Eight concurrent saves must yield one parent, one provenance row and one notification intent.
- The existing OrderService saves order, items, creation provenance and queued notification atomically. Pending notifications remain unsent when the staging provider/worker switches are disabled.
- New website orders receive `Website` provenance; new manual orders receive `FacebookManual`, authenticated creator ID and server UTC timestamp. Older rows receive no inferred origin or creator. OrderCreations uses a restrictive parent FK; administration uses cancellation instead of deleting recorded provenance. Source is shared by list filters, scoped status counts and XLSX export (new column 12, prior monetary columns preserved).

## Schema and deployment

`20261031235949_ManualOrderCreation` adds OrderCreations only; it does not backfill or rewrite Orders or existing operational data. SQL validates source/creator combinations. Downgrade fails with 51005 once provenance exists; use a forward correction. Do not resume an older order writer after accepting this feature. Staging deployment and live UI/SQL acceptance are separate from CI. Keep current provider and worker disabled during QA. Production deployment is outside this development batch.

## Scope

No Meta import/API or access to conversations, free price overrides, discounts, electronic payments, automatic reservation or carrier settlement. Use synthetic customers and products for QA. Private customer data belongs in the operational database, not in repository examples, logs or audit notes.
