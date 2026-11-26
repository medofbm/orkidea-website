#!/usr/bin/env bash
# CI rehearsal of the production launch: seeded preview + empty production, provision, admin bootstrap,
# catalogue copy, then checks that production serves the same catalogue without any preview markers or test data.
set -Eeuo pipefail
umask 077
trap 'printf "FAILED at line %s: %s
" "$LINENO" "$BASH_COMMAND" >&2' ERR

if [[ "${GITHUB_ACTIONS:-}" != true || ! "${GITHUB_SHA:-}" =~ ^[0-9a-f]{40}$ ]]; then
    printf 'This check is only for disposable GitHub Actions runners.\n' >&2
    exit 1
fi

preview_env=$(mktemp "${RUNNER_TEMP:?}/orkidea-preview.XXXXXX")
production_env=$(mktemp "${RUNNER_TEMP:?}/orkidea-production.XXXXXX")
backups=$(mktemp -d "${RUNNER_TEMP:?}/orkidea-backups.XXXXXX")
chmod 777 "$backups"
preview() { docker compose --project-name orkidea-vue-preview --env-file "$preview_env" -f deploy/compose.vue-preview.yaml "$@"; }
production() { docker compose --project-name orkidea-production --env-file "$production_env" -f deploy/compose.production.yaml "$@"; }
cleanup() {
    local result=$?
    if [[ "$result" != 0 ]]; then production logs --tail 40 frontend web db || true; fi
    production down --volumes --remove-orphans || true
    preview down --volumes --remove-orphans || true
    rm -rf -- "$preview_env" "$production_env" "$backups" "$production_env".*
}
trap cleanup EXIT

secret() { local value; value="Aa1_$(openssl rand -hex 24)"; printf '::add-mask::%s\n' "$value" >&2; printf '%s' "$value"; }
{
    printf 'ORKIDEA_PREVIEW_BACKEND_IMAGE=orkidea-preview:%s\n' "$GITHUB_SHA"
    printf 'ORKIDEA_PREVIEW_FRONTEND_IMAGE=orkidea-vue-preview:%s\n' "$GITHUB_SHA"
    printf 'ORKIDEA_PREVIEW_SUBNET=172.30.90.0/24\nORKIDEA_PREVIEW_PROXY_IP=172.30.90.10\nORKIDEA_PREVIEW_PORT=3087\n'
    printf 'ORKIDEA_PREVIEW_ADMIN_EMAIL=preview-ci@orkidea.test\n'
    for variable in SQL_PASSWORD APP_PASSWORD MIGRATION_PASSWORD ADMIN_PASSWORD; do
        printf 'ORKIDEA_PREVIEW_%s=%s\n' "$variable" "$(secret)"
    done
} > "$preview_env"
{
    printf 'ORKIDEA_PROD_BACKEND_IMAGE=orkidea-preview:%s\n' "$GITHUB_SHA"
    printf 'ORKIDEA_PROD_FRONTEND_IMAGE=orkidea-vue-preview:%s\n' "$GITHUB_SHA"
    printf 'ORKIDEA_PROD_SUBNET=172.30.91.0/24\nORKIDEA_PROD_PROXY_IP=172.30.91.10\nORKIDEA_PROD_PORT=3088\n'
    printf 'ORKIDEA_PROD_BACKUP_DIR=%s\n' "$backups"
    printf 'ORKIDEA_PROD_ADMIN_EMAIL=owner-ci@orkidea.test\n'
    for variable in SQL_PASSWORD APP_PASSWORD MIGRATION_PASSWORD ADMIN_PASSWORD; do
        printf 'ORKIDEA_PROD_%s=%s\n' "$variable" "$(secret)"
    done
} > "$production_env"

# Seeded preview (source of the catalogue).
preview config --quiet
preview up --detach --wait --wait-timeout 180 db
bash deploy/provision-vue-preview-db.sh "$preview_env"
preview --profile maintenance run --rm initialize
preview up --detach --wait --wait-timeout 180 frontend

# Fresh production: database, logins, migrations and the owner's admin account; then the catalogue copy.
production config --quiet
grep -Fq 'MSSQL_PID: Express' <(production config)
production up --detach --wait --wait-timeout 180 db
bash deploy/provision-production-db.sh "$production_env"
if bash deploy/provision-production-db.sh "$production_env" 2>/dev/null; then
    printf 'Provisioning twice must stop.\n' >&2; exit 1
fi
production --profile maintenance run --rm initialize
bash deploy/copy-catalogue-to-production.sh deploy/compose.vue-preview.yaml "$preview_env" "$production_env"
if bash deploy/copy-catalogue-to-production.sh deploy/compose.vue-preview.yaml "$preview_env" "$production_env" 2>/dev/null; then
    printf 'A second catalogue copy must stop.\n' >&2; exit 1
fi
production up --detach --wait --wait-timeout 180 frontend

sql() {
    production exec -T db sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S tcp:127.0.0.1,1433 -U sa -C -b -h -1 -W -d Orkidea'
}
test "$(sql <<< 'SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM Orders)+(SELECT COUNT(*) FROM Reviews)+(SELECT COUNT(*) FROM ContactMessages)+(SELECT COUNT(*) FROM StockMovements);')" = 0
test "$(sql <<< 'SET NOCOUNT ON; SELECT COUNT(*) FROM StockAccounts WHERE OnHand <> 0 OR Reserved <> 0;')" = 0
test "$(sql <<< "SET NOCOUNT ON; SELECT COUNT(*) FROM AspNetUsers;")" = 1
test "$(sql <<< "SET NOCOUNT ON; SELECT COUNT(*) FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE u.NormalizedEmail='OWNER-CI@ORKIDEA.TEST' AND r.Name='Admin';")" = 1
test "$(sql <<< 'SET NOCOUNT ON; SELECT COUNT(*) FROM ProductVariants;')" -gt 0
test "$(sql <<< 'SET NOCOUNT ON; SELECT COUNT(*) FROM ShippingRates;')" -gt 0

test -z "$(docker port "$(production ps -q web)")"
test -z "$(docker port "$(production ps -q db)")"
test "$(docker port "$(production ps -q frontend)" 3000/tcp)" = '127.0.0.1:3088'
origin=http://127.0.0.1:3088
headers=(-H 'X-Forwarded-Proto: https' -H 'X-Forwarded-For: 127.0.0.1')
page=$(curl --fail --silent --show-error --max-time 15 "${headers[@]}" "$origin/")
grep -Fq 'زيت الحبة السوداء' <<< "$page"
if grep -Fq 'نسخة تجريبية' <<< "$page" || grep -Fq 'noindex' <<< "$page"; then
    printf 'Production must not show preview markers.\n' >&2; exit 1
fi
robots=$(curl --fail --silent --show-error --max-time 10 "${headers[@]}" "$origin/robots.txt")
grep -Fq 'Sitemap: https://orkidea.ly/sitemap.xml' <<< "$robots"
if grep -Fxq 'Disallow: /' <<< "$robots"; then printf 'Production robots.txt must allow indexing.\n' >&2; exit 1; fi
test "$(curl --silent --max-time 10 "${headers[@]}" -o /dev/null -w '%{http_code}' "$origin/api/admin/products")" = 401
# The backend must not add the staging noindex header in production.
if curl --silent --max-time 10 "${headers[@]}" -D - -o /dev/null "$origin/api/v1/catalogue/products" | grep -iq '^x-robots-tag: noindex'; then
    printf 'Production API must not send the staging noindex header.\n' >&2; exit 1
fi

# Backup: a full backup lands in the mounted backup folder.
production exec -T db sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S tcp:127.0.0.1,1433 -U sa -C -b -Q "BACKUP DATABASE [Orkidea] TO DISK = N'"'"'/var/opt/backups/ci.bak'"'"' WITH INIT, CHECKSUM" -o /dev/null'
test -s "$backups/ci.bak"

printf 'PASS: production stack (Express, Production env), provisioning, admin bootstrap, catalogue copy with stock at 0, no preview markers, indexing allowed, backup.\n'
