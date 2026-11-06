# Vue catalogue foundation

The frontend is a Nuxt application using Vue, TypeScript and Tailwind. It contains a catalogue, product gallery and a session-backed cart. Checkout and admin remain migration milestones.

Run the ASP.NET application, then set NUXT_BACKEND_ORIGIN to its local HTTP origin. The default is http://127.0.0.1:5000. In frontend run npm ci, npm run dev, npm run typecheck and npm run build.

Nuxt serves the catalogue API and image proxy on the same frontend origin. The catalogue and image proxies accept reads only. Cart writes go through explicit allowlisted routes and forward the existing antiforgery header and cookies to ASP.NET. Cart data is not cached. Configure NUXT_BACKEND_ORIGIN in the deployment environment, never through a customer request. The backend routes return product DTOs and invariant decimal prices as strings. The backend remains authoritative for checkout.

The existing Docker image and application routes still serve the current store. A frontend runtime and reverse proxy rollout have not been configured yet.
