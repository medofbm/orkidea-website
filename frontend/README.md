# Orkidea frontend

Nuxt (Vue 3, TypeScript, Tailwind) storefront and admin. For the repo overview, see the [root README](../README.md).

- Run the ASP.NET application, then set `NUXT_BACKEND_ORIGIN` to its local HTTP origin (default `http://127.0.0.1:5000`). Set it in the deployment environment, never through a customer request.
- Use Node 24 LTS (at least 24.15.0). In `frontend/` run `npm ci`, `npm run dev`, `npm run typecheck` and `npm run build`. For editor types without starting the app: `npm exec -- nuxt prepare`.
- Nuxt serves the catalogue API and image proxy on the same origin; both accept reads only. Cart writes go through allowlisted routes that forward the antiforgery header and cookies to ASP.NET. Cart data is not cached. The backend stays authoritative for checkout, and returns invariant decimal prices as strings.
- Features: catalogue, product gallery, session-backed cart, guest checkout with delivery estimates, order confirmation and a session-bound receipt, account screens using the existing ASP.NET Identity cookies, and Vue admin pages (dashboard, orders, delivery, inventory, products, categories) through the existing Admin role. Email recovery and verification and two-factor enrollment remain migration milestones.
- `NUXT_PUBLIC_PREVIEW=true` shows a preview banner and a noindex meta tag. A staging host still needs protected access and its own noindex response header.
- CI exercises the built frontend in Chromium against real ASP.NET on a disposable SQL Server; see [test execution](../tests/README.md). The [Vue preview runbook](../deploy/vue-preview.md) covers host preflight, first start and phone acceptance.
