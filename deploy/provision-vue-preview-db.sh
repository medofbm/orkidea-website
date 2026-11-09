#!/usr/bin/env bash
set -euo pipefail
set +x

if [[ $# != 1 || ! -f "$1" ]]; then
    printf 'Usage: bash deploy/provision-vue-preview-db.sh <preview-env-file>\n' >&2
    exit 2
fi

config=$1
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
app_password=$(sed -n 's/^ORKIDEA_PREVIEW_APP_PASSWORD=//p' "$config")
migration_password=$(sed -n 's/^ORKIDEA_PREVIEW_MIGRATION_PASSWORD=//p' "$config")
if [[ ! "$app_password" =~ ^Aa1_[0-9a-f]{48}$ || ! "$migration_password" =~ ^Aa1_[0-9a-f]{48}$ ||
      "$app_password" == "$migration_password" ]]; then
    printf 'Use distinct preview database passwords in the documented format.\n' >&2
    exit 1
fi

# Pin both the Compose project and definition so this command targets only its db service.
export ORKIDEA_PREVIEW_APP_PASSWORD="$app_password" ORKIDEA_PREVIEW_MIGRATION_PASSWORD="$migration_password"
docker compose --project-name orkidea-vue-preview --env-file "$config" -f "$root/compose.vue-preview.yaml" \
    exec -T db sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S tcp:127.0.0.1,1433 -U sa -C -b -l 10 -t 60' <<SQL
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_ID(N'OrkideaStaging') IS NOT NULL
   OR SUSER_ID(N'orkidea_preview_app') IS NOT NULL
   OR SUSER_ID(N'orkidea_preview_migrate') IS NOT NULL
    THROW 51000, 'Preview database or login already exists. Inspect the preview project.', 1;
GO
CREATE DATABASE [OrkideaStaging];
GO
BEGIN TRANSACTION;
CREATE LOGIN [orkidea_preview_migrate] WITH PASSWORD=N'$migration_password', DEFAULT_DATABASE=[OrkideaStaging];
CREATE LOGIN [orkidea_preview_app] WITH PASSWORD=N'$app_password', DEFAULT_DATABASE=[OrkideaStaging];
USE [OrkideaStaging];
CREATE USER [orkidea_preview_migrate] FOR LOGIN [orkidea_preview_migrate];
CREATE USER [orkidea_preview_app] FOR LOGIN [orkidea_preview_app];
ALTER ROLE [db_owner] ADD MEMBER [orkidea_preview_migrate];
ALTER ROLE [db_datareader] ADD MEMBER [orkidea_preview_app];
ALTER ROLE [db_datawriter] ADD MEMBER [orkidea_preview_app];
COMMIT TRANSACTION;
GO
SQL
printf 'Preview database and isolated logins provisioned.\n'
