# Phase 4B schema and API contract (owner approved the migration, 2026-11-22)

One additive EF migration `Phase4bFeedbackAndArchive`. No column is dropped or renamed; existing rows get safe defaults. Preview database only until production is approved separately with a backup first.

## Schema
| Table | Column | Type | Default for existing rows |
|---|---|---|---|
| Reviews | `CreatedAtUtc` | datetime2 not null | migration time |
| Reviews | `Source` | nvarchar(16) not null (`site` \| `facebook`) | `site` |
| Reviews | `AllowName` | bit not null | `false` |
| Reviews | `OrderId` | int null, FK -> Orders (SetNull / NoAction, whichever the model allows), **unique filtered index** where not null | null |
| ContactMessages | `IsRead` | bit not null | `false` |
| Products, ProductVariants, Categories | `IsArchived` | bit not null | `false` |

Data seed in the same migration: the 5 Facebook comments from `frontend/data/testimonials.ts` as Reviews with `Source='facebook'`, `IsVisible=true`, `AllowName=false`, `CustomerName=''`, `Rating=5`, text exactly as in that file (emoji kept). After this, `frontend/data/testimonials.ts` is deleted and the home page reads everything from the API.

## Behaviour
- Archived products are hidden from the catalogue list, category counts, related products and product detail (`404`); archived variants are hidden from product detail; archived categories are hidden from the storefront category list. Adding an archived product/variant to the cart is refused, and checkout re-validation rejects archived lines with an Arabic message. Admin lists still show archived items with a flag.
- Product delete 409 message stays ("... تقدري تخفيه لاحقًا من الأرشفة.") and now points to archiving.

## API changes
| Method | Route | Body | Response |
|---|---|---|---|
| GET | `/api/v1/reviews` | — | `{ items: [{ id, message, rating, source, name }] }` latest 12 visible; `name` is the first word of `CustomerName` only when `AllowName = true`, else `null` |
| POST | `/api/v1/checkout/receipt/review` | `{ rating (1-5), message? (0-500), allowName: bool }` | `202 { message: "شكرًا على تقييمك! 💜" }`; uses the same receipt cookie/session as `GET /api/v1/checkout/receipt` to find the order; `409 { message: "قيّمتي هذا الطلب قبل." }` if the order already has a review; `404` when there is no receipt; antiforgery + `public-forms` rate limit. CustomerName = order's customer first name. Saved hidden (`IsVisible=false`). |
| GET | `/api/v1/admin/reviews` | — | items add `source`, `allowName`, `createdAtUtc`, `orderId` |
| GET | `/api/v1/admin/contact-messages` | — | items add `isRead` |
| POST | `/api/v1/admin/contact-messages/{id}/read` | `{ read: bool }` | `200 { id, isRead }` |
| POST | `/api/v1/admin/products/{id}/archive` | `{ archived: bool }` | `200 { id, isArchived }` |
| POST | `/api/v1/admin/products/{productId}/variants/{variantId}/archive` | `{ archived: bool }` | `200`; `409` when archiving the last visible variant of a visible product |
| POST | `/api/v1/admin/categories/{id}/archive` | `{ archived: bool }` | `200 { id, isArchived }` |
| GET | admin product list/detail and categories | — | include `isArchived` |

Nitro: public proxy `server/api/checkout/receipt/review.post.ts` (or extend the existing checkout proxy allowlist); admin allowlist adds `contact-messages/{id}/read`, `products/{id}/archive`, `products/{id}/variants/{id}/archive`, `categories/{id}/archive`.

## Frontend
- Receipt page: optional rating card "شن رأيك في تجربتك؟" (5 stars, optional comment, checkbox "اسمحي بعرض اسمي الأول مع رأيي"), hidden after success or 409.
- Home testimonials: API only; source badge from `source`; show `name` when present.
- Admin: archive toggles (product page, variant rows, categories), archived badge in lists; messages read/unread state (bold unread, toggle, unread count in nav); reviews show source badge, date, linked order number.
