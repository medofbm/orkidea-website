# Vue preview: applying migrations and runtime settings

The CI job `deploy-vue-preview` only swaps images on the server (`orkidea-preview-deploy`). It does **not** run EF migrations. When a release adds a migration, the new backend image fails its health check and the host script restores the previous images. Apply the migration first, then re-run the deploy.

All commands run on the server as root, in the live preview directory. Never paste secrets into chat or git.

## 1. Go to the preview directory and define a compose helper
```bash
cd /opt/orkidea-vue-preview-release-6a9a3fe-47f80bde
compose() { docker compose --project-name orkidea-vue-preview --env-file .preview.env -f compose.vue-preview.yaml "$@"; }
compose config --quiet && echo config-ok
```

## 2. Apply migrations with the new backend image
Use the full master commit SHA of the release (the image tag is published by CI as `preview-backend-sha-<sha>`):
```bash
SHA=<full master commit sha>
ORKIDEA_PREVIEW_BACKEND_IMAGE=ghcr.io/medofbm/orkidea-website:preview-backend-sha-$SHA compose --profile maintenance run --rm initialize
```
`initialize` applies pending migrations only to `OrkideaStaging`, skips preview seeding when products already exist, and exits. Migrations in this project are additive, so the running (older) image keeps working while this runs.

## 3. Re-run the deploy
GitHub → Actions → the master run of that commit → **Re-run failed jobs**. The deploy job is skipped if a newer master commit exists; in that case deploy the newest commit instead.

## 4. Runtime settings for the web and frontend containers
The compose file passes only the variables listed in each service's `environment`. Values in `.preview.env` alone do not reach the containers.

Add to the `web` service `environment` (under the existing entries):
```yaml
      Email__Smtp__Host: ${ORKIDEA_EMAIL_SMTP_HOST:-}
      Email__Smtp__Port: ${ORKIDEA_EMAIL_SMTP_PORT:-587}
      Email__Smtp__Username: ${ORKIDEA_EMAIL_SMTP_USERNAME:-}
      Email__Smtp__Password: ${ORKIDEA_EMAIL_SMTP_PASSWORD:-}
      Email__FromEmail: ${ORKIDEA_EMAIL_FROM:-noreply@orkidea.ly}
      Email__FromName: "أوركيديا"
      App__PublicBaseUrl: ${ORKIDEA_PUBLIC_BASE_URL:-https://vue-preview.orkidea.ly}
```
Add to the `frontend` service `environment`:
```yaml
      NUXT_PUBLIC_WHATSAPP_NUMBER: ${ORKIDEA_PUBLIC_WHATSAPP_NUMBER:-}
```
Add the values to `.preview.env` (mode 600):
```
ORKIDEA_EMAIL_SMTP_HOST=smtp-relay.brevo.com
ORKIDEA_EMAIL_SMTP_PORT=587
ORKIDEA_EMAIL_SMTP_USERNAME=<Brevo SMTP login>
ORKIDEA_EMAIL_SMTP_PASSWORD=<Brevo SMTP key>
ORKIDEA_EMAIL_FROM=noreply@orkidea.ly
ORKIDEA_PUBLIC_BASE_URL=https://vue-preview.orkidea.ly
ORKIDEA_PUBLIC_WHATSAPP_NUMBER=<digits only, e.g. 2189XXXXXXXX>
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
