# Vue catalogue foundation

The frontend is a Nuxt application using Vue, TypeScript and Tailwind. It contains a catalogue, product gallery and a session-backed cart. Guest checkout includes delivery estimates, reviewed totals, order confirmation and a session-bound receipt. The account page uses the existing ASP.NET Identity cookies for login, two-factor verification and logout. Registration follows the current Identity confirmation policy. The account screen supports phone and password updates. Email recovery and verification and two-factor enrollment remain migration milestones. Vue admin pages provide dashboard metrics, filtered orders, order status and history, order stock operations, manual delivery/returns, a paged delivery queue, product inventory with a movement ledger, product, gallery and variant editing, and category creation and renaming through the existing Admin role.

Run the ASP.NET application, then set NUXT_BACKEND_ORIGIN to its local HTTP origin. The default is http://127.0.0.1:5000. Use Node 24 LTS (at least 24.15.0). In frontend run npm ci, npm run dev, npm run typecheck and npm run build. To generate editor types without starting the app, run npm exec -- nuxt prepare.

Nuxt serves the catalogue API and image proxy on the same frontend origin. The catalogue and image proxies accept reads only. Cart writes go through explicit allowlisted routes and forward the existing antiforgery header and cookies to ASP.NET. Cart data is not cached. Configure NUXT_BACKEND_ORIGIN in the deployment environment, never through a customer request. The backend routes return product DTOs and invariant decimal prices as strings. The backend remains authoritative for checkout.

GitHub CI exercises the built frontend with Chromium and real ASP.NET on a disposable SQL Server over a loopback HTTPS gateway. It covers an admin product/gallery setup and a separate mobile guest purchase through its session-bound receipt. See [test execution](../tests/README.md).

Set NUXT_PUBLIC_PREVIEW=true for a preview banner and a noindex meta tag. A staging host still needs protected access and its own noindex response header.

The existing Docker image and application routes serve the current store. CI builds and verifies a separate Nuxt image and isolated Compose preview. The [Vue preview runbook](../deploy/vue-preview.md) covers host preflight, first start and phone acceptance.
