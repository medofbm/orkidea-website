# Vue catalogue foundation

The frontend is a Nuxt application using Vue, TypeScript and Tailwind. It contains a read-only catalogue and product page. Cart, checkout and admin remain migration milestones.

Run the ASP.NET application, then set NUXT_BACKEND_ORIGIN to its local HTTP origin. The default is http://127.0.0.1:5000. In frontend run npm install (the dependency lock is being generated and verified in CI), npm run dev, npm run typecheck and npm run build.

Nuxt serves the catalogue API and image proxy on the same frontend origin. The proxy accepts catalogue GET routes and image paths only. Configure NUXT_BACKEND_ORIGIN in the deployment environment, never through a customer request. The backend routes return product DTOs and invariant decimal prices as strings. The backend remains authoritative for checkout.

The existing Docker image and application routes still serve the current store. A frontend runtime and reverse proxy rollout have not been configured yet.
