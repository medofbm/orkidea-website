# Private checkout summary

The customer confirmation page now reads the latest completed order associated with the existing server-side checkout session. It shows the order number, current status, quantity, saved merchandise subtotal, shipping fee, inclusive total, saved payment method and recorded UTC time. It does not display customer name, phone, address, checkout token or carrier information. This is an order acknowledgement, not proof of payment or stock reservation.

The action accepts no lookup ID/token/phone from the request. Missing, invalid or uncommitted session completion grants redirect home instead of showing false success. URL parameters cannot select another order or grant another cookie session access. Cache-Control no-store and X-Robots-Tag noindex protect the summary. Refresh is read-only and does not consume a new basket, create notifications or change reservation/delivery/payment states. Catalogue edits cannot change the summary's saved order-item prices.

Access lasts only with the existing browsing session; the URL alone is not a transferable tracking link. There is no new public order-search API, login requirement, customer-account decision, provider integration or migration. Public cross-device tracking requires a separate scoped decision. Source and CI acceptance,staging rollout and live UI/SQL checks remain separate.
