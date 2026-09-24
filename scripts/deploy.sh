#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
image="$1"
scanner_host="${2:-clamav}"
[[ "$image" =~ ^[a-z0-9][a-z0-9./:_-]+$ ]] || { echo 'Invalid image'; exit 2; }
[[ "$scanner_host" =~ ^[a-zA-Z0-9.-]+$ ]] || { echo 'Invalid scanner host'; exit 2; }
compose=(docker compose -f docker-compose.yml -f docker-compose.production.yml)
container=$("${compose[@]}" ps -q secureshare-api)
previous=""
previous_format=""
if [[ -n "$container" ]]; then
    previous=$(docker inspect --format '{{.Config.Image}}' "$container")
    previous_format=$(docker inspect --format '{{ index .Config.Labels "org.secureshare.encryption-version" }}' "$container")
fi
export API_IMAGE="$image"
export SCANNER_HOST="$scanner_host"
"${compose[@]}" pull secureshare-api
# The scanner can run on a second small instance. Wait for its private endpoint
# before changing the application so uploads always fail closed.
for attempt in {1..120}; do
    if timeout 2 bash -c "</dev/tcp/$scanner_host/3310" 2>/dev/null; then break; fi
    if [[ "$attempt" == 120 ]]; then echo 'Scanner did not become reachable'; exit 1; fi
    sleep 5
done
# Remove the heavyweight services from the application node after the remote
# scanner is ready. Persistent application volumes are left untouched.
"${compose[@]}" stop clamav prometheus alertmanager grafana || true
# Initialize ownership on persistent volumes before the no-dependencies
# migration container opens SQLite for the first time.
"${compose[@]}" run --rm volume-init
# Keep a verified pre-migration backup. This uses a brief maintenance window.
if [[ -n "$container" ]]; then bash scripts/backup.sh --production; fi
"${compose[@]}" stop secureshare-api
rollback() {
    if [[ -n "$previous" && "$previous_format" == "2" ]]; then
        API_IMAGE="$previous" "${compose[@]}" up -d --no-build --wait --wait-timeout 180 secureshare-api
        echo 'Previous application image restored. Database was not downgraded.'
    else
        echo 'Automatic rollback is unavailable for an original-format image. Restore the verified pre-upgrade database and file archive together.'
    fi
}
trap 'rollback' ERR
"${compose[@]}" run --rm --no-deps secureshare-api --migrate-only
"${compose[@]}" up -d --no-build --wait --wait-timeout 300 secureshare-api caddy
trap - ERR
# Persist the successful immutable image so a later reboot or compose run uses it.
if grep -q '^API_IMAGE=' .env; then
    sed -i "s|^API_IMAGE=.*|API_IMAGE=$image|" .env
else
    printf '\nAPI_IMAGE=%s\n' "$image" >> .env
fi
echo 'Deployment passed readiness checks.'
