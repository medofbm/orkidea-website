# Isolated Vue preview

The preview runs Nuxt, the matching C# image and a separate synthetic SQL database in the Compose project `orkidea-vue-preview`. Nuxt is the only published service, bound to an unused loopback port. The preview uses its own network, media volume, data-protection keys and database volume. Its runtime SQL login has data reader/writer roles. The separate initializer uses a migration login. Both carrier and notification actions are disabled.

## Prepare the host

1. Use a successful pull request run that includes **Build and verify the Vue preview image and isolated Compose stack**. Download both image artifacts for the same commit. Transfer and load the archives with `docker load -i`, then inspect both `org.opencontainers.image.revision` labels. The tags are `orkidea-vue-preview:<commit>` and `orkidea-preview:<commit>`. Use the matching deployment files from that same commit. Builds run in GitHub.
2. Verify free memory and disk, the proposed loopback port, and Docker subnet availability on the destination. The preview can use up to about 4.3 GiB across SQL and both web processes. Choose an independent hostname and verify its DNS, certificate and Basic Auth file before installing the Nginx template.
3. Use a dedicated preview directory and a mode-600 environment file outside the repository. Set every value below. Generate distinct database secrets using `printf 'Aa1_'; openssl rand -hex 24`. The app and migration passwords must use this exact format. Set a preview-only admin email and password separately.

| Variable | Value |
|---|---|
| `ORKIDEA_PREVIEW_BACKEND_IMAGE` | Loaded backend tag for the tested commit |
| `ORKIDEA_PREVIEW_FRONTEND_IMAGE` | Loaded Nuxt tag for the same commit |
| `ORKIDEA_PREVIEW_PORT` | Verified free loopback port |
| `ORKIDEA_PREVIEW_SUBNET` | Verified unused Docker CIDR |
| `ORKIDEA_PREVIEW_PROXY_IP` | Free address inside that CIDR, assigned to Nuxt and trusted by C# |
| `ORKIDEA_PREVIEW_SQL_PASSWORD` | Dedicated SQL administrator password |
| `ORKIDEA_PREVIEW_APP_PASSWORD` | Dedicated runtime SQL login password |
| `ORKIDEA_PREVIEW_MIGRATION_PASSWORD` | Dedicated initializer SQL login password |
| `ORKIDEA_PREVIEW_ADMIN_EMAIL` | Preview-only web administrator email |
| `ORKIDEA_PREVIEW_ADMIN_PASSWORD` | Preview-only web administrator password |

## First start after host approval

From the matching checkout, with the environment file supplied by absolute path:

```bash
set -euo pipefail
preview_env=/path/to/private/vue-preview.env
compose() {
  docker compose --project-name orkidea-vue-preview --env-file "$preview_env" \
    -f deploy/compose.vue-preview.yaml "$@"
}
compose config --quiet
compose up -d --wait --wait-timeout 180 db
bash deploy/provision-vue-preview-db.sh "$preview_env"
compose --profile maintenance run --rm initialize
compose up -d --wait --wait-timeout 180 frontend
compose ps
```

The provisioning command stops if the database or either login already exists. The initializer accepts only the dedicated staging database name and seeds synthetic data. On failure, inspect the preview project and determine which step completed before retrying.

Verify the loopback health endpoint and test the forwarded HTTPS catalogue before adding Nginx. Confirm the preview banner, noindex meta and synthetic product rows. Then fill the separate Nginx template, run `nginx -t`, and reload only after a successful check. Verify unauthenticated HTTPS returns 401, authenticated requests show the preview, and the existing Razor staging endpoint still responds. Run the mobile path through administration, gallery, cart and checkout using synthetic data.

To pause the preview, remove or disable its dedicated Nginx route and stop the `orkidea-vue-preview` Compose services. Keep the preview volumes for review. The existing staging project uses its own configuration and volumes.
