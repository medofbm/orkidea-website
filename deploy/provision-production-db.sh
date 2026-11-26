#!/usr/bin/env bash
# Creates the empty production database and its two logins (runtime app, migrations). Stops if any already exists.
set -euo pipefail
set +x

if [[ $# != 1 || ! -f "$1" ]]; then
    printf 'Usage: bash deploy/provision-production-db.sh <production-env-file>\n' >&2
    exit 2
fi

config=$1
root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
app_password=$(sed -n 's/^ORKIDEA_PROD_APP_PASSWORD=//p' "$config")
migration_password=$(sed -n 's/^ORKIDEA_PROD_MIGRATION_PASSWORD=//p' "$config")
if [[ ! "$app_password" =~ ^Aa1_[0-9a-f]{48}$ || ! "$migration_password" =~ ^Aa1_[0-9a-f]{48}$ ||
      "$app_password" == "$migration_password" ]]; then
    printf 'Use distinct production database passwords in the documented format.\n' >&2
    exit 1
fi

docker compose --project-name orkidea-production --env-file "$config" -f "$root/compose.production.yaml" \
    exec -T db sh -c 'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S tcp:127.0.0.1,1433 -U sa -C -b -l 10 -t 60' <<SQL
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_ID(N'Orkidea') IS NOT NULL
   OR SUSER_ID(N'orkidea_app') IS NOT NULL
   OR SUSER_ID(N'orkidea_migrate') IS NOT NULL
    THROW 51000, 'Production database or login already exists. Inspect the production project.', 1;
GO
CREATE DATABASE [Orkidea];
GO
ALTER DATABASE [Orkidea] SET RECOVERY SIMPLE;
GO
BEGIN TRANSACTION;
CREATE LOGIN [orkidea_migrate] WITH PASSWORD=N'$migration_password', DEFAULT_DATABASE=[Orkidea];
CREATE LOGIN [orkidea_app] WITH PASSWORD=N'$app_password', DEFAULT_DATABASE=[Orkidea];
USE [Orkidea];
CREATE USER [orkidea_migrate] FOR LOGIN [orkidea_migrate];
CREATE USER [orkidea_app] FOR LOGIN [orkidea_app];
ALTER ROLE [db_owner] ADD MEMBER [orkidea_migrate];
ALTER ROLE [db_datareader] ADD MEMBER [orkidea_app];
ALTER ROLE [db_datawriter] ADD MEMBER [orkidea_app];
COMMIT TRANSACTION;
GO
SQL
printf 'Production database and isolated logins provisioned.\n'
