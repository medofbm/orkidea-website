# Admin order-list pagination

The admin list now reads 25 matching orders per page instead of materializing the whole result set. Newest order date wins; order identity breaks equal-date ties. Previous/next navigation retains normalized search and status. Filters restart on page 1; oversized positive pages clamp to the last available page, while malformed/nonpositive page inputs return HTTP 400. An empty search result is page 1 of 1 with zero records.

Search supports customer name, saved phone, city, or positive order identity (optionally prefixed with #). Status is restricted to existing order statuses or all; search is trimmed and limited to 100 characters. The list and private XLSX export share the same filter and stable ordering. Export intentionally includes every matching row, not only the current page. This is offset pagination, not a snapshot across multiple requests: new orders/deletions between page visits can shift page boundaries. No business ledger or stock balances are calculated from page rows.

No schema migration, configuration change, account UI change, outbound notification, order mutation, or stock mutation is part of this release. Existing order status CSRF/revision controls remain in place. Unknown saved statuses are shown as unknown rather than falsely labeled new.

Acceptance: the disposable SQL suite creates 53 synthetic rows with the same timestamp and verifies all three pages, correct filtered counts, no duplicate/missing IDs for the unchanged dataset, empty/huge/invalid pages, order-number search, and full filtered XLSX content. Real Identity-cookie HTTP tests cover invalid bindings, unsupported export status and rendered Razor pagination. These 53 rows must never be seeded on staging or production.

After CI passes, stage as a web-only image update, leaving the existing database container and schema unchanged. Verify readiness and admin list/search/export in the authenticated staging session. Keep the previous web image available for reverting the UI release. CI acceptance and a merged PR are not evidence that the server has been updated.
