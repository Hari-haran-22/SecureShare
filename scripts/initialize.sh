#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
domain="${1:-localhost}"
[[ "$domain" =~ ^[a-zA-Z0-9.-]+$ ]] || { echo 'Invalid hostname'; exit 2; }
test ! -e .env || { echo '.env exists; existing secrets preserved.'; exit 1; }
test ! -e secrets/protection.pfx || { echo 'Certificate exists; restore your .env backup.'; exit 1; }
umask 077
mkdir -p secrets backups
sql="Ss9!$(openssl rand -hex 32)"
grafana=$(openssl rand -hex 32)
openssl rand -hex 32 > secrets/protection-password
openssl rand -hex 32 > secrets/backup-password
openssl req -x509 -newkey rsa:4096 -sha256 -days 1825 -nodes -subj '/CN=SecureShare Key Protection' \
    -keyout secrets/protection.key -out secrets/protection.crt >/dev/null 2>&1
openssl pkcs12 -export -inkey secrets/protection.key -in secrets/protection.crt \
    -out secrets/protection.pfx -passout file:secrets/protection-password
# Only the generated intermediate key and certificate are removed.
rm -- secrets/protection.key secrets/protection.crt
printf 'MSSQL_SA_PASSWORD=%s\nGRAFANA_ADMIN_PASSWORD=%s\nDOMAIN=%s\n' "$sql" "$grafana" "$domain" > .env
chown root:1654 secrets secrets/protection.pfx secrets/protection-password
chmod 750 secrets
chmod 640 secrets/protection.pfx secrets/protection-password
echo 'Generated private secrets. Store the backup password separately from backup archives.'
