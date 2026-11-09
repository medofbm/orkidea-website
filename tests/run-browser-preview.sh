#!/usr/bin/env bash
set -euo pipefail
umask 077

if [[ "${GITHUB_ACTIONS:-}" != true || "${ORKIDEA_RUN_SQL_TESTS:-}" != 1 ||
      ! "${ORKIDEA_SQL_TEST_CONNECTION:-}" =~ ^Server=127\.0\.0\.1,[0-9]+\;Database=master\;User\ Id=sa\; ]]; then
    printf 'Browser preview requires the disposable GitHub Actions SQL container.\n' >&2
    exit 1
fi

scratch=$(mktemp -d "${RUNNER_TEMP:?}/orkidea-browser.XXXXXX")
backend_pid=
frontend_pid=
cleanup() {
    local result=$?
    if [[ -n "$frontend_pid" ]]; then kill "$frontend_pid" 2>/dev/null || true; fi
    if [[ -n "$backend_pid" ]]; then kill "$backend_pid" 2>/dev/null || true; fi
    if [[ "$result" != 0 ]]; then
        tail -n 45 "$scratch/backend.log" "$scratch/frontend.log" 2>/dev/null || true
    fi
    rm -rf -- "$scratch"
}
trap cleanup EXIT

export ASPNETCORE_ENVIRONMENT=Staging
export ASPNETCORE_URLS=http://127.0.0.1:5081
export ASPNETCORE_HTTPS_PORT=443
export AllowedHosts='localhost;127.0.0.1'
export ConnectionStrings__DefaultConnection="${ORKIDEA_SQL_TEST_CONNECTION/Database=master;/Database=OrkideaStaging;}"
export Notifications__WhatsAppEnabled=false Notifications__OutboxWorkerEnabled=false
export Carriers__Sabil__QuoteEnabled=false
export DataProtection__KeysPath="$scratch/keys" DataProtection__ApplicationName=Orkidea.BrowserCi
export ORKIDEA_BROWSER_ADMIN_EMAIL=browser-admin@orkidea.test
ORKIDEA_BROWSER_ADMIN_PASSWORD="Qa1_$(openssl rand -hex 24)"
printf '::add-mask::%s\n' "$ORKIDEA_BROWSER_ADMIN_PASSWORD"
export ORKIDEA_BROWSER_ADMIN_PASSWORD

(
    cd Orkideya
    Initialization__Run=true Initialization__SeedPreviewData=true \
        Bootstrap__AdminEmail="$ORKIDEA_BROWSER_ADMIN_EMAIL" \
        Bootstrap__AdminPassword="$ORKIDEA_BROWSER_ADMIN_PASSWORD" \
        dotnet bin/Release/net10.0/Orkideya.dll
) >"$scratch/backend.log" 2>&1

(
    cd Orkideya
    export Initialization__Run=false
    exec dotnet bin/Release/net10.0/Orkideya.dll
) >>"$scratch/backend.log" 2>&1 &
backend_pid=$!
curl --fail --silent --show-error --retry 20 --retry-connrefused --max-time 3 http://127.0.0.1:5081/health/ready

(
    cd frontend
    export HOST=127.0.0.1 PORT=3081 NUXT_BACKEND_ORIGIN=http://127.0.0.1:5081 NUXT_PUBLIC_PREVIEW=true
    exec node .output/server/index.mjs
) >"$scratch/frontend.log" 2>&1 &
frontend_pid=$!
curl --fail --silent --show-error --retry 20 --retry-connrefused --max-time 3 http://127.0.0.1:3081/ -o /dev/null

export ORKIDEA_BROWSER_CERT="$scratch/cert.pem" ORKIDEA_BROWSER_KEY="$scratch/key.pem"
openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj /CN=localhost \
    -addext 'subjectAltName=IP:127.0.0.1,DNS:localhost' \
    -keyout "$ORKIDEA_BROWSER_KEY" -out "$ORKIDEA_BROWSER_CERT" 2>/dev/null
cd frontend
node --test tests/browser.test.mjs
