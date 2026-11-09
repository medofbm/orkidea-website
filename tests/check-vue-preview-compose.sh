#!/usr/bin/env bash
set -euo pipefail
umask 077

if [[ "${GITHUB_ACTIONS:-}" != true || ! "${GITHUB_SHA:-}" =~ ^[0-9a-f]{40}$ ]]; then
    printf 'This check is only for disposable GitHub Actions runners.\n' >&2
    exit 1
fi

config=$(mktemp "${RUNNER_TEMP:?}/orkidea-vue-preview.XXXXXX")
compose() {
    docker compose --project-name orkidea-vue-preview --env-file "$config" -f deploy/compose.vue-preview.yaml "$@"
}
cleanup() {
    local result=$?
    if [[ "$result" != 0 ]]; then compose logs --tail 30 frontend web db || true; fi
    compose down --volumes --remove-orphans || true
    rm -f -- "$config" "$config.home"
}
trap cleanup EXIT

{
    printf 'ORKIDEA_PREVIEW_BACKEND_IMAGE=orkidea-preview:%s\n' "$GITHUB_SHA"
    printf 'ORKIDEA_PREVIEW_FRONTEND_IMAGE=orkidea-vue-preview:%s\n' "$GITHUB_SHA"
    printf 'ORKIDEA_PREVIEW_SUBNET=172.30.90.0/24\nORKIDEA_PREVIEW_PROXY_IP=172.30.90.10\nORKIDEA_PREVIEW_PORT=3087\n'
    printf 'ORKIDEA_PREVIEW_ADMIN_EMAIL=preview-ci@orkidea.test\n'
    for variable in SQL_PASSWORD APP_PASSWORD MIGRATION_PASSWORD ADMIN_PASSWORD; do
        value="Aa1_$(openssl rand -hex 24)"
        printf '::add-mask::%s\n' "$value" >&2
        printf 'ORKIDEA_PREVIEW_%s=%s\n' "$variable" "$value"
    done
} > "$config"

compose config --quiet
compose up --detach --wait --wait-timeout 180 db
bash deploy/provision-vue-preview-db.sh "$config"
compose --profile maintenance run --rm initialize
compose up --detach --wait --wait-timeout 180 frontend

test -z "$(docker port "$(compose ps -q web)")"
test -z "$(docker port "$(compose ps -q db)")"
test "$(docker port "$(compose ps -q frontend)" 3000/tcp)" = '127.0.0.1:3087'
test "$(curl --fail --silent --show-error --max-time 10 http://127.0.0.1:3087/health/live)" = live

origin=http://127.0.0.1:3087
headers=(-H 'X-Forwarded-Proto: https' -H 'X-Forwarded-For: 127.0.0.1')
curl --fail --silent --show-error --max-time 15 "${headers[@]}" "$origin/" -o "$config.home"
grep -Fq 'نسخة تجريبية. استخدم بيانات اختبار فقط.' "$config.home"
grep -Fq 'noindex, nofollow, noarchive' "$config.home"
grep -Fq 'زيت الحبة السوداء' "$config.home"
code=$(curl --silent --show-error --max-time 10 "${headers[@]}" -o /dev/null -w '%{http_code}' "$origin/api/admin/products")
test "$code" = 401

printf 'PASS: isolated Vue Compose stack, seeded SSR catalogue, preview marker, private backend/database and denied anonymous admin access.\n'
