# Verification

Run from the Git repository root:

```sh
node --test tests/cart.test.cjs tests/checkout.test.cjs tests/review-rating.test.cjs
python tests/check_safety.py
dotnet restore tests/Orkideya.Regression/Orkideya.Regression.csproj
dotnet build tests/Orkideya.Regression/Orkideya.Regression.csproj -c Release --no-restore
dotnet run --project tests/Orkideya.Regression/Orkideya.Regression.csproj -c Release --no-build --no-restore
```

The console runner uses synthetic objects and in-memory workbooks. HTTP security checks start an in-memory TestServer with synthetic configuration, ephemeral data-protection keys and an interceptor that rejects all database connections. No public port, production database, migration, account initialization or real notification is used. JavaScript tests use a minimal DOM harness; Python checks source boundaries. HTTP checks cover anonymous admin/export access, blocked public workbooks, missing antiforgery tokens and GET mutation rejection. They do not prove authenticated role separation, relational transactions, migration safety, browser accessibility or production readiness.

Tests in this repository are authoritative. The parent workspace retains compatibility entry points for previous verification commands and source-only build bundles.

The GitHub workflow performs verification only. It contains no publish, upload-artifact, deployment, Azure login, environment secrets or write permissions. This replaces the old automatic Azure deployment when the change reaches master; the remote master remains unchanged until then.
