# Verification

Run from the Git repository root:

```sh
node --test tests/cart.test.cjs tests/checkout.test.cjs tests/review-rating.test.cjs
python tests/check_safety.py
dotnet restore tests/Orkideya.Regression/Orkideya.Regression.csproj
dotnet build tests/Orkideya.Regression/Orkideya.Regression.csproj -c Release --no-restore
dotnet run --project tests/Orkideya.Regression/Orkideya.Regression.csproj -c Release --no-build --no-restore
```

The console runner uses synthetic objects and in-memory workbooks. It does not start the website, initialize accounts, migrate or connect to SQL Server, or send real notifications. JavaScript tests use a minimal DOM harness; Python checks source boundaries. These checks do not prove HTTP authorization, relational transactions, migration safety, browser accessibility, or production readiness.

Tests in this repository are authoritative. The parent workspace retains compatibility entry points for previous verification commands and source-only build bundles.

The GitHub workflow performs verification only. It contains no publish, upload-artifact, deployment, Azure login, environment secrets or write permissions. This replaces the old automatic Azure deployment when the change reaches master; the remote master remains unchanged until then.
