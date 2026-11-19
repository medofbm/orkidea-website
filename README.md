# Orkidea

Orkidea is an Arabic, mobile-first online store for natural oils and soap, serving Libya.

## Stack

- Backend: ASP.NET Core 10 (net10), EF Core and SQL Server, in `Orkideya/`. It holds the existing Razor store and administration, Identity, migrations and domain services.
- Frontend: Nuxt 4, Vue 3, TypeScript and Tailwind, in `frontend/`. The Vue storefront is under migration; the existing deployed store remains the release entry point.

## Repo map

- `Orkideya/` - C# application (`Orkideya.csproj`); solution file `Orkideya.sln`.
- `frontend/` - Nuxt storefront and admin ([frontend/README.md](frontend/README.md)).
- `tests/` - JavaScript, backend and isolated SQL/HTTP regression checks ([tests/README.md](tests/README.md)).
- `deploy/` - staging and preview deployment files ([deploy/README.md](deploy/README.md)).
- `docs/` - durable documentation; see [docs/README.md](docs/README.md). Operational runbooks are in `docs/runbooks/`.
- `.github/workflows/master_orkidea.yml` - CI/CD workflow.

## Quick start

Install the SDK selected by `global.json` and Node.js 24 LTS (24.15.0 or later within 24.x). Configure a dedicated development SQL connection through ASP.NET configuration and set `Initialization:Run` and the notification flags deliberately. Keep development data separate from staging and production.

Backend:

```
dotnet restore Orkideya.sln
dotnet build Orkideya.sln -c Release
dotnet run --project Orkideya/Orkideya.csproj
```

Frontend (its backend origin, `NUXT_BACKEND_ORIGIN`, must point to the running local ASP.NET service):

```
cd frontend
npm ci
npm run dev
npm run typecheck
npm run build
```

## Tests

```
node --test tests/cart.test.cjs tests/checkout.test.cjs tests/review-rating.test.cjs tests/admin-order-status.test.cjs
python -I tests/check_safety.py
```

The backend regression runner is `tests/Orkideya.Regression` (restore, build, then `dotnet run --project tests/Orkideya.Regression/Orkideya.Regression.csproj -c Release`). The disposable-database runner `tests/Orkideya.SqlRegression` is described in [tests/README.md](tests/README.md).

## CI/CD

`.github/workflows/master_orkidea.yml` ("Orkidea verification") runs on pull requests to master, pushes to master and manual dispatch.

- `frontend`: npm ci, typecheck, build and SSR test.
- `verify`: JavaScript regressions, source safety checks, backend build and synthetic backend regressions.
- `sql-server`: disposable loopback SQL Server migration/rollback tests and a Chromium run of the Vue and ASP.NET preview.
- `preview-image`: builds and smoke-tests the backend and Vue preview images and uploads them as short-lived artifacts.
- `publish-image` (push to master): publishes the verified backend image to GHCR.
- `publish-vue-preview` and `deploy-vue-preview` (push to master, or manual dispatch with `deploy_vue_preview`): publish both images by digest and update the isolated Vue preview host through a protected environment.

Nothing is deployed to production automatically. Production rollout and staging setup are manual; see [deploy/README.md](deploy/README.md) and [docs/README.md](docs/README.md).

## Notes

Product prices, availability, shipping coverage and brand claims require operator review before launch. Store secrets in the environment or secret storage; never commit runtime configuration or customer exports.
