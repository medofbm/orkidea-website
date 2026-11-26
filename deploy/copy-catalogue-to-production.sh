#!/usr/bin/env bash
# One-time copy of the catalogue from the Vue preview into the new, empty production database:
# categories, products, sizes (prices and stock consumption), stock accounts (units only, balances reset to 0),
# delivery rates, and the uploaded product images. Orders, customers, reviews, messages and accounts are NOT copied.
# Stops before writing anything if production already has catalogue or order rows, or if the two schemas differ.
set -euo pipefail
set +x
umask 077

if [[ $# != 3 || ! -f "$1" || ! -f "$2" || ! -f "$3" ]]; then
    printf 'Usage: bash deploy/copy-catalogue-to-production.sh <preview-compose-file> <preview-env-file> <production-env-file>\n' >&2
    exit 2
fi
preview_compose=$1 preview_env=$2 production_env=$3
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
stop() { printf 'STOP: %s\n' "$1" >&2; exit 1; }

preview() { docker compose --project-name orkidea-vue-preview --env-file "$preview_env" -f "$preview_compose" "$@"; }
production() { docker compose --project-name orkidea-production --env-file "$production_env" -f "$root/compose.production.yaml" "$@"; }
# sqlcmd inside a db container; the SA password never leaves the container.
sql() {
    local side=$1 database=$2
    "$side" exec -T db sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S tcp:127.0.0.1,1433 -U sa -C -b -l 10 -t 120 -h -1 -W -d "$1"' sh "$database"
}
bcp_run() {
    local side=$1; shift
    "$side" exec -T db sh -c 'exec /opt/mssql-tools18/bin/bcp "$@" -S tcp:127.0.0.1,1433 -U sa -P "$MSSQL_SA_PASSWORD" -u' sh "$@" >/dev/null
}

tables=(Categories Products ProductVariants StockAccounts ShippingRates)
preview_db=OrkideaStaging
production_db=Orkidea

preview_db_container=$(preview ps -q db)
production_db_container=$(production ps -q db)
[[ -n "$preview_db_container" && -n "$production_db_container" && "$preview_db_container" != "$production_db_container" ]] ||
    stop 'Both preview and production databases must be running.'

# 1. Same migrations on both sides.
migrations="SET NOCOUNT ON; SELECT STRING_AGG(CAST(MigrationId AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY MigrationId) FROM __EFMigrationsHistory;"
[[ "$(sql preview "$preview_db" <<< "$migrations")" == "$(sql production "$production_db" <<< "$migrations")" ]] ||
    stop 'Preview and production are not on the same migrations. Deploy the same images to both first.'

# 2. Identical column layout for every copied table (native bcp relies on it).
layout() {
    local list
    list=$(printf "'%s'," "${tables[@]}")
    printf "SET NOCOUNT ON; SELECT TABLE_NAME+':'+COLUMN_NAME+':'+DATA_TYPE+':'+CAST(ORDINAL_POSITION AS varchar(4)) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME IN (%s) ORDER BY TABLE_NAME, ORDINAL_POSITION;" "${list%,}"
}
[[ "$(sql preview "$preview_db" <<< "$(layout)")" == "$(sql production "$production_db" <<< "$(layout)")" ]] ||
    stop 'Table layouts differ between preview and production.'

# 3. Production must be empty.
emptiness="SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM Categories)+(SELECT COUNT(*) FROM Products)+(SELECT COUNT(*) FROM ProductVariants)+(SELECT COUNT(*) FROM StockAccounts)+(SELECT COUNT(*) FROM ShippingRates)+(SELECT COUNT(*) FROM Orders);"
[[ "$(sql production "$production_db" <<< "$emptiness")" == 0 ]] || stop 'Production already has catalogue or order rows. Nothing was copied.'

# 4. Copy rows in foreign-key order, keeping ids (native format, so Arabic text and decimals copy exactly).
work=$(mktemp -d)
cleanup() {
    rm -rf -- "$work"
    preview exec -T db sh -c 'rm -f /tmp/orkidea-catalogue-*.bcp' >/dev/null 2>&1 || true
    production exec -T db sh -c 'rm -f /tmp/orkidea-catalogue-*.bcp' >/dev/null 2>&1 || true
}
trap cleanup EXIT
for table in "${tables[@]}"; do
    file="/tmp/orkidea-catalogue-$table.bcp"
    bcp_run preview "dbo.$table" out "$file" -d "$preview_db" -n
    docker cp "$preview_db_container:$file" "$work/$table.bcp" >/dev/null
    docker cp "$work/$table.bcp" "$production_db_container:$file" >/dev/null
    bcp_run production "dbo.$table" in "$file" -d "$production_db" -n -E -k -h "TABLOCK"
done

# 5. Stock starts at zero in production (the owner enters real counts); identity seeds follow the copied ids.
sql production "$production_db" <<'SQL' >/dev/null
SET NOCOUNT ON;
SET XACT_ABORT ON;
UPDATE StockAccounts SET OnHand = 0, Reserved = 0;
DBCC CHECKIDENT ('dbo.Categories') WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.Products') WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.ProductVariants') WITH NO_INFOMSGS;
DBCC CHECKIDENT ('dbo.ShippingRates') WITH NO_INFOMSGS;
SQL

# 6. Row counts must match the preview.
counts="SET NOCOUNT ON; SELECT CONCAT((SELECT COUNT(*) FROM Categories),'/',(SELECT COUNT(*) FROM Products),'/',(SELECT COUNT(*) FROM ProductVariants),'/',(SELECT COUNT(*) FROM StockAccounts),'/',(SELECT COUNT(*) FROM ShippingRates));"
copied=$(sql production "$production_db" <<< "$counts")
[[ "$copied" == "$(sql preview "$preview_db" <<< "$counts")" ]] || stop "Row counts differ after the copy ($copied)."

# 7. Uploaded product images (media volume), read-only from the preview side.
backend_image=$(sed -n 's/^ORKIDEA_PROD_BACKEND_IMAGE=//p' "$production_env")
[[ -n "$backend_image" ]] || stop 'ORKIDEA_PROD_BACKEND_IMAGE is missing.'
docker volume inspect orkidea-vue-preview_media orkidea-production_media >/dev/null
docker run --rm --network none --user 1654:1654 --entrypoint sh \
    -v orkidea-vue-preview_media:/from:ro -v orkidea-production_media:/to "$backend_image" \
    -c 'cp -R /from/. /to/ && ls -A /to | wc -l'

printf 'PASS: catalogue copied (categories/products/sizes/stock accounts/delivery rates = %s); stock balances set to 0.\n' "$copied"
