# Orkidea

Arabic, mobile-first storefront for natural oils and soap. The backend uses ASP.NET Core 10, EF Core and SQL Server.

## Current structure

- Orkideya/ contains the C# application, existing Razor store and administration, Identity, migrations and domain services.
- frontend/ contains the Vue 3 storefront under migration, built with Nuxt, TypeScript and Tailwind.
- tests/ contains JavaScript, backend and isolated SQL/HTTP regression checks.
- deploy/ contains deployment configuration and operational instructions.
- docs/ contains durable system documentation.

The Vue frontend covers the catalogue, product gallery, variant selection, cart, guest checkout and basic account screens. Vue administration includes the dashboard, order status and history, manual delivery and returns, inventory, product and gallery editing, prices and categories. Advanced account flows and staging browser acceptance remain open. The existing deployed store remains the release entry point. Backend folder relocation is a later change that must update solution, Docker and CI paths together.

## Local development

Install the SDK selected by global.json and Node.js 24 LTS (24.15.0 or later within 24.x). Restore and build Orkideya.sln. Configure a dedicated development SQL connection through ASP.NET configuration, then run Orkideya/Orkideya.csproj. Configure Initialization:Run and notification flags deliberately for the development environment.

For the Vue application, see [frontend/README.md](frontend/README.md). Its backend origin must point to the running local ASP.NET service. Keep development data separate from staging and production.

## Verification and deployment

GitHub runs frontend type checking, build and SSR checks, backend regressions, disposable SQL/HTTP integration tests and a non-root image smoke test. After a successful master run, it publishes the tested backend image to GHCR. Automated server rollout and image retention still require infrastructure configuration.

See [test execution](tests/README.md) and [staging deployment](deploy/README.md). The domain-specific files in deploy/ explain order history, delivery, stock and shipping behavior, and remain operational references. [Notification outbox](docs/notification-outbox.md) documents notification recovery.

Product prices, availability, shipping coverage and brand claims require operator review before launch. Store secrets in the environment or secret storage; never commit runtime configuration or customer exports.
