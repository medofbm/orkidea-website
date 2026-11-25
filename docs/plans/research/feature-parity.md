# Razor vs Vue feature-parity matrix (repo C:/Dev/orkidea, read-only review, 2026-11-21)
Paths are under `Orkideya/` (backend) or `frontend/`. Line numbers are from the controller grep; verify before editing. "Vue" = page/component; "Nitro" = `frontend/server/api/*` proxy. Static review only, nothing run.

## A. Storefront
| Capability | Razor | JSON API | Nitro | Vue | Gap / size | Notes |
|---|---|---|---|---|---|---|
| Catalogue browse + paging | HomeController.Index :18 | GET /api/v1/catalogue/products (CatalogueController.cs:13) | v1/catalogue/[...path].ts | pages/index.vue + ProductCard | none | Featured/home content OK |
| Categories filter | HomeController.ProductsByCategory :32 | `categoryId` query on products :14 | same | index.vue (verify chips) | none/S | Confirm category chips exist in index.vue |
| Search (public) | none found in Views | none | none | none | n/a | Admin-only search exists; public search is a new feature, not parity |
| Product detail, gallery, variants, related | HomeController.ProductDetail :74 (+related by category :91) | GET catalogue/products/{id} :30 (images, variants) | yes | products/[id].vue (gallery, variant radios, sticky add) | S | Related products not in JSON/Vue |
| Reviews list (public) | HomeController.Index :21 (carousel in Home/Index.cshtml:223) | none | none | none | M | Visible reviews never exposed to Vue; homepage has no social proof |
| Review submit + star rating | Only admin Create/Edit (Reviews :94/:127); `Review` has Rating | none | none | none | M | No public submit in Razor either; tests/review-rating.test.cjs covers rating logic |
| Contact form | HomeController.SendMessage :46 (POST) | none | none | none (index.vue:142 shows contact links only) | M | Spam/rate-limit needed (honeypot, throttle) |
| Cart add/update/remove/count | CartController :25/:72/:90/:106, count :143 | /api/v1/cart, /cart/items POST/PATCH/DELETE (CartController.Api.cs:21-74) | cart/[...path].ts, cart/index.get.ts | cart.vue, product page mutate | none | Session cookie + antiforgery via /api/v1/session |
| Checkout: city/delivery quote | CheckoutController.Index :28, PreviewShipping :47 | GET /api/v1/checkout, POST /checkout/shipping (CheckoutController.Api.cs:16-19) | checkout/[path].ts, checkout/index.get.ts | checkout/index.vue | none | Rates come from server (ShippingRateId) |
| Sabil carrier choices | CheckoutController :20,:49-60 (SabilCheckoutQuotes, flag-gated) | same endpoints (carrierDestination, quoteToken) | yes | checkout/index.vue:11,:52,:78 | none | Verify end-to-end with flag ON on vue-preview |
| Validation + idempotency | PlaceOrder :75, CheckoutToken/CompletedCheckoutToken :52 | POST /checkout/orders :28 (409 on stale cart) | yes | yes | none | Server-owned; good |
| Order confirmation / receipt | Confirmation :197 | GET /checkout/receipt :42 | yes | checkout/receipt.vue | none | Session-based; reload after expiry = 404 |
| WhatsApp number at checkout | CheckoutInput.WhatsAppNumber | in order body | yes | checkout/index.vue:185 (required field) | S | Razor has explicit opt-in wording (Checkout/Index.cshtml); confirm Vue shows opt-in/consent text and sends the same flag |
| Static pages (privacy, order-and-delivery) | HomeController.Privacy :69 | n/a | n/a | privacy.vue, order-and-delivery.vue | none | |

## B. Customer account
| Capability | Razor | JSON API | Nitro | Vue | Gap / size | Notes |
|---|---|---|---|---|---|---|
| Register | Identity/Pages/Account/Register | POST account/register (AccountController.cs:46) | account/[action].ts | account/register.vue | none | |
| Login / logout | Account/Login, Logout | POST login :103, logout :125, verify :113 | yes | account/index.vue | none | `verify` meaning (confirm/2FA?) not clear; check |
| Forgot / reset password | ForgotPassword, ResetPassword (+Confirmation) | none | none | none | M | Needs email sender; critical for locked-out customers |
| Email confirm / resend / change email | ConfirmEmail, ResendEmailConfirmation, Manage/Email | none | none | none | M | Only if RequireConfirmedAccount is on (AccountController.cs:54 refuses that mode) |
| Manage: phone, password | Manage/Index, ChangePassword | POST account/phone :74, password :88 | yes | AccountSettings.vue | none | |
| Set password / external logins / lockout / access denied | SetPassword, ExternalLogin(s), Lockout, AccessDenied | none | none | none | S/M | External login probably unused; low priority |
| 2FA, recovery codes, download/delete personal data | Manage/EnableAuthenticator, *Recovery*, DownloadPersonalData, DeletePersonalData | none | none | none | L | Scaffolded defaults; owner likely does not need; keep Razor or drop |
| Order history (customer) | none in Razor | none | none | none | n/a | New feature, not parity; useful later |

## C. Admin
| Capability | Razor | JSON API | Nitro (admin/[...path].ts allowlist) | Vue | Gap / size | Notes |
|---|---|---|---|---|---|---|
| Dashboard metrics | Admin/HomeController.Index :16 | GET admin/dashboard (AdminReadController.cs:19) | yes | admin/index.vue | S | Sales chart (HomeController.GetSalesChartData :42, DashboardMetrics.ChartAsync) not in JSON/Vue |
| Products list/search | ProductsController.Index :30 | GET admin/products :21 | yes | admin/products/index.vue | none | |
| Product create/edit + main image | ProductsController :71/:120 | POST admin/products, /{id} (AdminProductsController.cs:68/:86, optimistic version) | yes (streamed upload) | admin/products/[id].vue | none | |
| Gallery upload/remove | same (galleryFiles) | same (galleryFiles, removeGallery :87) | yes | products/[id].vue (verify UI) | S | Confirm Vue has multi-file picker + remove checkboxes |
| Product delete | ProductsController.DeleteConfirmed :188 | none | none | none | S | Add soft delete/archive endpoint; reuse ProductEditor |
| Variants/prices add/edit | ProductsController.AddVariant :148; ProductVariantsController (full CRUD) | POST products/{id}/variants (+/{variantId}) :110/:126 | yes | products/[id].vue | S | Variant delete missing (ProductVariantsController.DeleteConfirmed :107) |
| Categories CRUD | CategoriesController (Create/Edit/Delete, Ajax) | GET/POST admin/categories (AdminCategoriesController.cs:18-48) | yes | admin/categories.vue | S | Delete missing in JSON/Vue |
| Inventory list/detail/open/configure/adjust | InventoryController :15-:65 | admin/inventory GET, {id}, open/configure/adjust | yes | admin/inventory/index.vue, [id].vue, AdminStockBalances | none | |
| Bulk stock / filling | InventoryController.Open (unit) / Configure | covered by open/configure | yes | inventory/[id].vue | none/S | No bulk grid; per-variant only |
| Orders list + filters (status, source, search, page) | OrdersController.Index :33 | GET admin/orders :37 | yes | admin/orders/index.vue + AdminOrderList | none | |
| Order detail + status history | OrdersController.Details :64 | GET admin/orders/{id} :77 | yes | admin/orders/[id].vue | none | |
| Status change | OrdersController.UpdateStatus :114 | POST admin/orders/{id}/status (OrdersController.cs:122, in Razor area controller) | yes | [id].vue | none | |
| Cancel with reason | InventoryController.Cancel :81 (ChangeAsync "Cancelled", reason) | covered only if status endpoint accepts reason | yes | [id].vue | S | Verify reason field + RequiresDeliveryOperation path in Vue |
| Order stock ops (reserve/release/deduct/return) | InventoryController.OrderOperation :73 | POST admin/orders/{id}/stock (AdminFulfillmentController.cs:23) | yes | AdminOrderFulfillment.vue | none | Returns handled through stock operation, no separate "returns" screen anywhere |
| Delivery tracking/events | DeliveryController.Index :13 / Apply :21 | GET admin/delivery :59, POST orders/{id}/delivery :50 | yes | admin/delivery.vue, AdminOrderFulfillment | none | |
| Manual Facebook orders | ManualOrdersController :34/:41/:63 | admin/orders/manual GET, review, confirm | yes | admin/orders/manual.vue | none | Razor edit-draft (`edit` Guid) maybe not in Vue |
| Excel export | OrdersController.ExportToExcel :198 | GET admin/orders/export :46 | yes | admin/orders/index.vue:94 (link) | none | |
| Order delete | OrdersController.DeleteConfirmed :163 | none | none | none | S | Probably should stay unsupported (use cancel); decide |
| Reviews moderation (toggle, edit, create, delete) | Admin/ReviewsController :27-:178 | none | none | none | M | Whole module missing |
| Contact messages (list, view, delete, add-to-reviews) | Admin/ContactMessagesController :27-:119 | none | none | none | M | Whole module missing |
| Users / roles | no controller or UI; only RoleManager in Program.cs:81 | none | none | none | n/a | Admin made by seed/CLI; backend-only |

## D. Backend-only (no UI in either app)
- Notification outbox + automatic WhatsApp sending: removed 2026-11-25 (owner decision); see docs/runbooks/notification-outbox.md. Customer wa.me links remain in the frontend.
- Sabil catalogue sync and quote caching (Infrastructure/Shipping/SabilCatalogue.cs); no admin view of cities/rates or sync status.
- Order revision/stale-update protection, idempotency tokens, stock actor audit (StockService.ValidActor).
- Admin user and role creation (RoleManager) has no screen.
- Health endpoint (frontend/server/routes/health/live.get.ts).

## E. Top 10 gaps to close for Vue to replace staging (shop-owner priority)
1. Reviews moderation + public reviews on home: add AdminReviewsController (copy toggle/edit/delete from Admin/ReviewsController), add GET /api/v1/catalogue/reviews (visible only), Nitro allowlist entry, admin/reviews.vue, reviews strip in index.vue. (M)
2. Contact form + admin ContactMessages: add POST /api/v1/contact reusing HomeController.SendMessage logic (add throttle/honeypot), AdminContactMessagesController (list/get/delete/add-to-reviews), pages contact section + admin/messages.vue. (M)
3. Forgot/reset password: add POST account/forgot and account/reset using UserManager token + existing email sender, Nitro whitelist in account/[action].ts, pages account/forgot.vue and reset.vue. (M)
4. Verify WhatsApp opt-in in checkout: compare Checkout/Index.cshtml wording to checkout/index.vue:185, add the same consent checkbox and pass it through CheckoutApiOrder. (S)
5. Cancel order with reason in Vue: confirm /api/v1/admin/orders/{id}/status passes `reason` to statuses.ChangeAsync (OrdersController.cs:122), add reason textarea in orders/[id].vue and handle RequiresDeliveryOperation. (S)
6. Product delete/archive and variant delete: add DELETE (or archive POST) in AdminProductsController reusing ProductVariantsController delete rules (block when orders/stock exist), buttons in products/[id].vue, Nitro regex update. (S)
7. Category delete: add POST /api/v1/admin/categories/{id}/delete guarded against products in category (reuse CategoriesController.DeleteConfirmed :235), button in admin/categories.vue. (S)
8. Dashboard sales chart: expose DashboardMetrics.ChartAsync(days) at GET admin/dashboard/chart, whitelist in Nitro, render a simple bar chart in admin/index.vue. (S)
9. Verify gallery upload/remove and category chips in Vue UI against the existing endpoints (galleryFiles, removeGallery); add UI if missing. (S)
10. Retire-Razor housekeeping: decide on order delete (recommend keep unsupported), drop scaffolded 2FA/external-login/personal-data pages or keep them on a minimal Razor fallback, add related products to product JSON/page. (S-L, low urgency)

Not required for parity (new features): public search, customer order history, admin users/roles UI, notification-outbox monitor, Sabil sync status.
