# Phase 4A API contract (no schema change)

Shared contract for backend and frontend agents. All JSON. All POSTs require the antiforgery header `RequestVerificationToken` (token from `GET /api/v1/session`, same as the cart). Errors: `400/409/429` with `{ "message": "<Arabic, feminine voice>" }`. Admin routes: `[Authorize(Roles = "Admin")]`. Only GET and POST are used (Nitro proxies allow no PUT/PATCH/DELETE).

## Public
| Method | Route | Body / query | Response |
|---|---|---|---|
| GET | `/api/v1/reviews` | — | `{ items: [{ id, message, rating }] }` latest 12 with `IsVisible = true`. **No names** (privacy decision). |
| POST | `/api/v1/reviews` | `{ name, rating (1-5), message (10-500 chars) }` | `202 { message: "شكرًا! رأيك وصلنا وبيظهر بعد المراجعة." }`. Saved with `IsVisible = false`. Rate limit policy `public-forms`: 3 per IP per hour → 429. |
| POST | `/api/v1/contact` | `{ name (2-80), phone?, email?, message (10-1000) }` — phone **or** email required; phone digits/+ only; email valid | `202 { message: "وصلتنا رسالتك، بنرد عليك في أقرب وقت." }`. Email stored as `""` when absent. Same `public-forms` rate limit. |

## Admin
| Method | Route | Body / query | Response |
|---|---|---|---|
| GET | `/api/v1/admin/reviews` | — | `{ items: [{ id, customerName, message, rating, isVisible }] }` newest first (by id desc) |
| POST | `/api/v1/admin/reviews/{id}/visibility` | `{ visible: bool }` | `200 { id, isVisible }` |
| POST | `/api/v1/admin/reviews/{id}/delete` | — | `204` |
| GET | `/api/v1/admin/contact-messages` | — | `{ items: [{ id, name, email, phoneNumber, message, receivedAt }] }` newest first |
| POST | `/api/v1/admin/contact-messages/{id}/delete` | — | `204` |
| GET | `/api/v1/admin/sales-chart` | `?days=7\|30` (default 7) | `{ labels: string[], data: number[] }` (reuse `DashboardMetrics.ChartAsync`) |
| POST | `/api/v1/admin/products/{id}/delete` | — | `204`, or `409 { message }` if the product has order items or a stock account |
| POST | `/api/v1/admin/products/{productId}/variants/{variantId}/delete` | — | `204`, or `409` if it is the product's last variant |
| POST | `/api/v1/admin/categories/{id}/delete` | — | `204`, or `409 { message }` if any product uses it |

## Nitro (frontend/server)
- New public proxies: `server/api/reviews.get.ts`, `server/api/reviews.post.ts`, `server/api/contact.post.ts` forwarding cookies and the antiforgery header like the cart proxy.
- Admin proxy `server/api/admin/[...path].ts`: allow GET `reviews`, `contact-messages`, `sales-chart`; allow POST `reviews/{id}/(visibility|delete)`, `contact-messages/{id}/delete`, `products/{id}/delete`, `products/{id}/variants/{id}/delete`, `categories/{id}/delete`.
