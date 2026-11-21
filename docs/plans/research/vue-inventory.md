# Vue storefront inventory (C:/Dev/orkidea/frontend, branch ui/brand-refresh). Nuxt 4.6.4, Vue 3.5.45, Tailwind 4.3.5 (@tailwindcss/vite)
Paths relative to frontend/. No middleware/, error.vue, plugins/ or toast component exists.

## 1. Pages (route: purpose; sections in order; components)
- pages/index.vue (/): catalogue home. Hero (badge "100% طبيعي • مصنوع يدويًا", h1 "اكتشفي جمال الطبيعة مع أوركيديا", CTAs "تسوقي الآن"/"تواصلي معنا", 3 stats, flower logo disc) > catalogue (h2 "منتجات أوركيديا", category chip nav, skeleton/error/empty/grid, pagination السابق/التالي) > "كيف تطلبي؟" 3 steps (اختاري منتجك / أضيفيه للسلة / نوصلك الطلب) + "الدفع عند الاستلام" > about "أوركيديا" > contact (واتساب/فيسبوك/إنستغرام, hidden when empty). Uses ProductCard. useFetch /api/v1/catalogue/products?page&categoryId.
- pages/products/[id].vue: detail. Skeleton (l.52), error card (l.63), gallery + thumbnails, h1 (l.111), price aria-live (l.116), variant radio chips (l.122), quantity stepper with output (l.136), status msg (l.149), mobile sticky buy bar (l.163), "عن المنتج" (l.158). Uses useCart.
- pages/cart.vue (/cart): "سلة التسوق"; loading/error/empty ("سلتك فارغة.")/items with + - حذف/"ملخص السلة"/"إتمام الطلب". OLD look.
- pages/checkout/index.vue: "إتمام الطلب"; delivery options (carrier radio, city/area/office selects l.131-175), customer fields (الاسم الأول، اسم العائلة، رقم الواتساب، العنوان l.177-188), summary dl l.193. Posts /api/checkout/shipping and /orders. OLD look.
- pages/checkout/receipt.vue: "إيصال الطلب", "تم استلام طلبك", order no., amounts; loading/error/unavailable states. OLD look.
- pages/account/index.vue: login (email/password/remember) + TOTP/recovery step; signed in shows AccountSettings (phone, password) + logout. account/register.vue: email/password/confirm.
- pages/order-and-delivery.vue and pages/privacy.vue: static policy pages (5 sections each). order-and-delivery hardcodes FB/IG URLs instead of runtimeConfig.
- Admin pages (layout admin, noindex): index, orders/index, orders/[id], orders/manual, delivery, inventory/index, inventory/[id], products/index, products/[id] (create+edit), categories.
- Layouts: layouts/default.vue (134 l.): skip link l.60, sticky header, hash-link focus management l.14-33, cart count badge, register/login buttons, mobile menu (aria-expanded, Esc closes, focus returns l.34-38), gradient footer (about, social icons, pills, legal links). layouts/admin.vue (22 l.): skip link, plain 8-link nav, no active state, no logout. app.vue: preview banner + NuxtLayout.
- Components: ProductCard (stretched link, image fallback, size chips, "من" price, add button "تمت الإضافة ✓"), AccountSettings, AdminOrderFulfillment, AdminOrderList, AdminReadError, AdminStockBalances.
- Composables: useCart.ts (useState cart/busy/message; load; mutate POST/PATCH/DELETE, fetches CSRF token from /api/session on every mutation). Utils: adminDeliveryState, adminHandoffMode, adminOrderStatus, adminStockUnit, postAccount. Types: catalogue.ts, checkout.ts, admin.ts.

## 2. Design system
- Tailwind 4 CSS-first: assets/css/main.css `@import "tailwindcss"` + `@theme`; no tailwind.config; nuxt.config.ts:5 vite plugin. Font @fontsource-variable/cairo (main.css:1). RTL via nuxt.config head htmlAttrs. Only one CSS file.
- Tokens (main.css:4-17): brand #751D82, brand-soft #8B3689, accent #A70A78, lavender #E6D3E5, lavender-deep #CFB3C8, cream #F6F0EA, plum #30081A, gold #C9A24B, ink #2A1530, ink-soft #6B5A72; font-sans Cairo; radius-card 16px; radius-field 12px (unused anywhere); shadow-soft. No dark mode; no success/danger/warning tokens; no type or spacing scale.
- Base: body cream, line-height 1.7; global :focus-visible 3px brand outline (main.css:25).
- @utility: storefront-action (l.33), btn-primary (l.42, pill 48px), btn-secondary (l.55), card (l.68), chip (l.73), price (l.79 area). Hard-coded `#fff` in btn-primary and card; rgb shadow in main.css token.
- Hex in .vue files: none. rgb() literals: pages/products/[id].vue:163, ProductCard.vue:69. theme-color #751D82 in nuxt.config.ts:21.
- Default-palette (purple/gray/slate etc.) class counts per file (approx, matches of numbered palette classes): checkout/index 45, cart 28, checkout/receipt 16, admin/orders/[id] 12, AdminOrderFulfillment 11, admin/delivery 9, admin/inventory/[id] 7, admin/index 5, admin/products/[id] 5, account/index 4, AdminOrderList 4, admin/orders/manual 4, admin/products/index 4, categories 3, AdminStockBalances 3, register 2, inventory/index 2, orders/index 2, order-and-delivery 2, privacy 2, AdminReadError 1. Zero in index.vue, products/[id].vue, ProductCard, default.vue (these use tokens).
- Status colours (red/green/amber/blue-N) in 14 files with no tokens: admin/products/index 8, products/[id] admin 5, AccountSettings 4, cart 4, manual 3, others 1-2.

## 3. Motion (every one gated by prefers-reduced-motion except none)
- main.css:22 smooth scroll; main.css:34-40 storefront-action 160ms transition + active scale .98; btn-primary l.47 and btn-secondary l.58 same.
- components/ProductCard.vue:67-70 hover lift -2px + shadow 160ms.
- pages/products/[id].vue:174-178 skeleton-pulse 1.4s, gallery-image-fade 180ms.
- pages/index.vue:91,93,94 skeleton `motion-safe:animate-pulse`.
- None: no <Transition>/<TransitionGroup>, page transitions, mobile-menu animation, toast animation or scroll reveals.

## 4. Nitro server routes (backendOrigin default http://127.0.0.1:5000)
- api/session.ts GET -> /api/v1/session (CSRF requestToken).
- api/v1/catalogue/[...path].ts GET products, products/{id} -> /api/v1/catalogue/...
- api/cart/index.get.ts -> /api/v1/cart; api/cart/[...path].ts: GET '', POST items, PATCH/DELETE items/{id}.
- api/checkout/index.get.ts -> /api/v1/checkout; checkout/[path].ts: GET receipt, POST shipping|orders.
- api/account/index.get.ts -> /api/v1/account; account/[action].ts POST login|verify|logout|register|phone|password.
- api/admin/[...path].ts -> /api/v1/admin/...: GET dashboard, delivery, categories, orders/export, orders/manual, products(/categories|/{id}), orders|inventory(/{id}); POST categories(/{id}), orders/{id}/status|stock|delivery, orders/manual/review|confirm, inventory/{id}/open|configure|adjust, products(/{id}(/variants(/{vid}))). streamRequest for product uploads. No PUT/PATCH/DELETE. no-store + X-Robots-Tag.
- routes/images/[...path].get.ts GET png/jpg/webp -> backend /images/...; routes/health/live.get.ts returns "live".
- No Nuxt auth middleware: /admin pages render client shell for anyone; protection depends on backend 401/403 only.

## 5. Admin capabilities in Vue
- Dashboard: shortcuts + latest orders. Orders list: filters status/source/search, Excel export link (orders/index.vue:41-94). Order detail: status, stock actions, delivery actions (HandOver, Redispatch, CorrectDetails, MarkReturning, Resume, ReceiveReturn, CorrectState) with handoff mode + history. Manual Facebook order: options > review > confirm (orders/manual.vue).
- Delivery/returns list linking to orders. Inventory list; detail: open account (ml/piece), configure variant units, adjust delta+reason (revision check).
- Products: list; create/edit, cover + gallery upload (png/jpg), remove/undo stored images, variant add/edit (products/[id].vue:175). Categories: create/edit.
- NOT in Vue: reviews, contact messages, product/category/variant delete, customers, users/roles, notification settings, reports beyond dashboard, admin logout.

## 6. Accessibility / UX
- Present: skip links (default.vue:60, admin.vue:9, admin one lacks focus styling), landmarks, aria-labelledby, nav aria-labels, lang=ar dir=rtl, global focus ring, hash-nav focus handling, Esc closes menu, role=status/alert and aria-live on loads/errors/price/qty/summary/pagination, 44-48px targets, alt text and image fallback.
- Skeletons only on home and product detail; cart/checkout/receipt/admin use plain "جاري التحميل…". Empty states: home, cart, filters. Error + retry: home, product, cart, checkout.
- Gaps: no toast system (inline messages only); no confirm for cart delete/admin actions; no error.vue (unbranded 404/500); checkout/account errors in one top list without aria-invalid/aria-describedby; admin nav has no aria-current/active state; no breadcrumbs, search, per-product OG/structured data; cart +/- buttons plain; emoji in copy (🌿 home empty state, ❤ footer).

## 7. Consistency issues
- Old look: cart, checkout, receipt, order-and-delivery, privacy, account/*, AccountSettings and every admin page/component (purple-900/950, slate, rounded-2xl/xl/lg, red-700). Cart/checkout CTAs use `bg-purple-900 rounded-xl` (cart.vue ~l.38, l.60) not btn-primary. Raw <button> styles in ~11 admin/account files vs btn-* only in index, product detail, ProductCard, header.
- Radii mixed: pill (btn), 16px (card), rounded-[2rem] hero, rounded-2xl/xl/lg elsewhere; --radius-field unused. Inputs differ: `border-purple-200` (checkout), plain `border` (account), `slate-400` (admin).
- Voice: storefront addresses women (اختاري، أضيفي، تسوقي، تواصلي، راجعي، احتفظي، "كيف تطلبي؟"). Mixed: order-and-delivery.vue mixes Libyan colloquial ("تقدري، تبي، مش ظاهرة، ما ظهرش") with MSA; admin uses masculine imperative ("اختر طريقة التسليم", AdminOrderFulfillment.vue:145) and first-person "استلمت الراجع" (:60); account uses masculine "تذكّر تسجيل الدخول" (account/index.vue ~l.82).
- Content: "5 ★ تقييم" hard-coded in hero (index.vue ~l.36) not from data; "100% طبيعي / صنع يدوي" repeated in hero, stats, footer. Cart back link uses "→" (RTL-correct but text arrow).
- Naming: "الحساب" vs "حساب أوركيديا"; "دخول" vs "تسجيل الدخول"; "إتمام الطلب" vs "إيصال الطلب".
- Note: line numbers for main.css utilities and a few template lines are approximate (+/-3).
