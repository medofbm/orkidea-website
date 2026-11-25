# Vue preview: applying migrations and runtime settings

Migrations now run automatically. `web` depends on the one-shot `migrate` service (`service_completed_successfully`), so every `compose up` of `web` first runs `migrate` with the new backend image. It uses the `orkidea_preview_migrate` database user, applies pending EF migrations, creates no users, seeds nothing (`Initialization__SeedPreviewData=false`, default shipping rates are opt-in and stay off) and exits 0. When nothing is pending it is a quick no-op, and EF Core takes a SQL Server migration lock, so concurrent runs are safe. `web` keeps the least-privilege app user. If `migrate` fails, `web` is not started and the host script rolls back.

Sections 1 to 3 below are the manual fallback only (for example if `migrate` itself fails and you need to see why). Do not skip `depends_on` when deploying: avoid `docker compose up --no-deps web`.

All commands run on the server as root, in the live preview directory. Never paste secrets into chat or git.

## 1. Go to the preview directory and define a compose helper
```bash
cd /opt/orkidea-vue-preview-release-6a9a3fe-47f80bde
compose() { docker compose --project-name orkidea-vue-preview --env-file .preview.env -f compose.vue-preview.yaml "$@"; }
compose config --quiet && echo config-ok
```

## 2. Fallback: apply migrations by hand with the new backend image
The failed deploy already pulled the new images by digest, and compose uses `pull_policy: never`, so a tag reference is not found. Find the local image id by its revision label (backend runs as user `1654`, frontend as `node`):
```bash
SHA=<full master commit sha>
docker images -q --no-trunc | sort -u | while read id; do echo "$id $(docker image inspect -f '{{index .Config.Labels "org.opencontainers.image.revision"}} {{.Config.User}}' $id)"; done | grep "$SHA"
```
Then run the migration service with the backend id (the line ending in `1654`):
```bash
ORKIDEA_PREVIEW_BACKEND_IMAGE=sha256:<backend image id> compose run --rm migrate
```
If no line appears, pull the image first: `docker pull ghcr.io/medofbm/orkidea-website:preview-backend-sha-$SHA`.
`migrate` applies pending migrations only to `OrkideaStaging` and exits. (The `initialize` service under the `maintenance` profile still exists for first-time setup: it also seeds preview data and creates the bootstrap admin.) Migrations in this project are additive, so the running (older) image keeps working while this runs.

## 3. Re-run the deploy (fallback)
GitHub → Actions → the master run of that commit → **Re-run failed jobs**. The deploy job is skipped if a newer master commit exists; in that case deploy the newest commit instead.

## 4. Runtime settings for the web and frontend containers
The compose file passes only the variables listed in each service's `environment`. Values in `.preview.env` alone do not reach the containers.

These settings are now in `compose.vue-preview.yaml` (reference copy below). The keys match `EmailOptions` (`Email:Smtp:*`, `Email:FromEmail`, `Email:FromName`) and `AppOptions` (`App:PublicBaseUrl`). `web` environment:
```yaml
      Email__Smtp__Host: ${ORKIDEA_EMAIL_SMTP_HOST:-}
      Email__Smtp__Port: ${ORKIDEA_EMAIL_SMTP_PORT:-587}
      Email__Smtp__Username: ${ORKIDEA_EMAIL_SMTP_USERNAME:-}
      Email__Smtp__Password: ${ORKIDEA_EMAIL_SMTP_PASSWORD:-}
      Email__FromEmail: ${ORKIDEA_EMAIL_FROM:-noreply@orkidea.ly}
      Email__FromName: "أوركيديا"
      App__PublicBaseUrl: ${ORKIDEA_PUBLIC_BASE_URL:-https://vue-preview.orkidea.ly}
```
`frontend` environment (matches `runtimeConfig.public.whatsappNumber` in `nuxt.config.ts`):
```yaml
      NUXT_PUBLIC_WHATSAPP_NUMBER: ${ORKIDEA_PUBLIC_WHATSAPP_NUMBER:-}
```
Only the values need adding to `.preview.env` (mode 600):
```
ORKIDEA_EMAIL_SMTP_HOST=smtp-relay.brevo.com
ORKIDEA_EMAIL_SMTP_PORT=587
ORKIDEA_EMAIL_SMTP_USERNAME=<Brevo SMTP login>
ORKIDEA_EMAIL_SMTP_PASSWORD=<Brevo SMTP key>
ORKIDEA_EMAIL_FROM=noreply@orkidea.ly
ORKIDEA_PUBLIC_BASE_URL=https://vue-preview.orkidea.ly
ORKIDEA_PUBLIC_WHATSAPP_NUMBER=<digits only, e.g. 2189XXXXXXXX>
```
Values go without quotes or `< >` around them (the username is the Brevo SMTP login `...@smtp-brevo.com`, the key starts with `xsmtpsib-`). Check without printing secrets (both must print 1):
```bash
grep -c '^ORKIDEA_EMAIL_SMTP_USERNAME=.*@smtp-brevo\.com$' .preview.env
grep -c '^ORKIDEA_EMAIL_SMTP_PASSWORD=xsmtpsib-' .preview.env
```
Editing `compose.vue-preview.yaml` changes its checksum. The deploy script pins it (line `sha256sum --check --strict` in `/usr/local/sbin/orkidea-vue-preview-deploy`), and so does `SHA256SUMS` in the release folder. Update both, or the next deploy stops before swapping images:
```bash
f=/usr/local/sbin/orkidea-vue-preview-deploy
cp -p "$f" "/root/orkidea-vue-preview-deploy.before-$(date +%Y%m%d)"
old=$(grep -oE '[0-9a-f]{64}  compose[.]vue-preview[.]yaml' "$f" | cut -d' ' -f1)
new=$(sha256sum compose.vue-preview.yaml | cut -d' ' -f1)
sed -i "s/$old/$new/" "$f" && grep -n "$new" "$f"
cp SHA256SUMS "SHA256SUMS.before-$(date +%Y%m%d)"
grep -v 'compose.vue-preview.yaml' "SHA256SUMS.before-$(date +%Y%m%d)" | tr -d '\r' > SHA256SUMS
sha256sum compose.vue-preview.yaml >> SHA256SUMS && sha256sum -c SHA256SUMS
```

Apply without changing images:
```bash
compose config --quiet && compose up -d --wait --wait-timeout 180 web frontend
```

## 5. Check
```bash
compose ps
curl -s -o /dev/null -w "%{http_code}\n" -u claude-review https://vue-preview.orkidea.ly/
```
Then request a password reset for a test account on the preview and confirm the email arrives from `أوركيديا <noreply@orkidea.ly>`.

## Rollback
Migrations are additive; leave them. To return to the previous images, re-run the deploy of the previous master commit, or restore the digests the host script printed.
