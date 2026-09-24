#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
umask 077
mkdir -p backups
compose=(docker compose -f docker-compose.yml)
production=false
if [[ "${1:-}" == "--production" ]]; then
    compose+=(-f docker-compose.production.yml)
    production=true
fi
"${compose[@]}" --profile tools build backup
was_running=$("${compose[@]}" ps --status running -q secureshare-api)
# Maintenance window keeps database metadata and files at the same point in time.
if [[ -n "$was_running" ]]; then
    "${compose[@]}" stop secureshare-api
    trap '"${compose[@]}" start secureshare-api' EXIT
fi
if $production; then
    "${compose[@]}" run --rm --no-deps secureshare-api --check-database
else
    "${compose[@]}" exec -T db bash -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "BACKUP DATABASE [SecureShareDb] TO DISK = '\''/var/opt/mssql/backups/secureshare.bak'\'' WITH INIT, CHECKSUM; RESTORE VERIFYONLY FROM DISK = '\''/var/opt/mssql/backups/secureshare.bak'\'' WITH CHECKSUM;"'
fi
"${compose[@]}" --profile tools run --rm --no-deps backup
if [[ -n "${BACKUP_S3_URI:-}" ]]; then
    aws s3 sync backups/ "$BACKUP_S3_URI" --exclude '*' --include 'secureshare-*.tar.gz.gpg' --only-show-errors
fi
# Retention applies only to archives in this explicitly resolved workspace directory.
find "$PWD/backups" -maxdepth 1 -type f -name 'secureshare-*.tar.gz.gpg' -mtime +30 -delete
echo 'Verified database backup and encrypted file/key archive created.'
