# Production (orkidea.ly)

Production runs on the same VPS as the Vue preview, as its own Compose project `orkidea-production`:
own network, volumes, data-protection keys and database (SQL Server **Express**, free for production use).
Nuxt is the only published service, on loopback port 3089, behind Nginx with a Let's Encrypt certificate.

## Data
- Fresh database `Orkidea`. Only the catalogue is copied once from the preview
  (`deploy/copy-catalogue-to-production.sh`): categories, products, sizes with prices and stock consumption,
  stock accounts (units only, balances reset to 0) and delivery rates, plus uploaded product images.
- Not copied: orders, customers, accounts, reviews from the site, contact messages, stock history.
  The imported Facebook reviews come with the migrations.
- The owner's administrator account is created by the `initialize` service; the password is typed on the server
  and never stored in a file.

## Launch steps (owner runs them from PowerShell; scripts live in the private releases folder)
1. `Run-PrepareProduction.ps1 -AdminEmail <owner email>`: checks memory/disk/port/subnet, builds the private stack
   with the preview's tested images, provisions the database, creates the admin, copies the catalogue, installs the
   daily backup (03:30 UTC, 14 days, database `.bak` + images `.tgz` in `/opt/orkidea-production-backups`).
2. DNS: A records `orkidea.ly` and `www.orkidea.ly` → the server IPv4. Then `Run-EnableProductionHttps.ps1`:
   certificate for both names, Nginx site from `nginx.production.conf.template`, still behind the preview login
   (soft launch). `www` redirects to `orkidea.ly`.
3. Test the real store (enter real stock, place a real order, cancel it), then `Run-OpenProductionToPublic.ps1`.
   `open-production-to-public.sh --close` puts the login back.

## Updates
Every merge to master deploys to the preview automatically. Production changes only when the owner runs
`Run-UpdateProduction.ps1`: it backs up, switches production to the images running on the preview, verifies, and
restores the previous images if a check fails (a migration is not undone; restore the backup in that case).

## CI
`tests/check-production-compose.sh` rehearses the whole launch on every PR: seeded preview + empty production,
provisioning (twice must stop), admin bootstrap, catalogue copy (twice must stop), stock at 0, no orders,
no preview banner or noindex, production robots.txt, admin API denied, and a database backup.
