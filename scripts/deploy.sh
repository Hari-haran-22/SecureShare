#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
image="$1"
[[ "$image" =~ ^[a-z0-9][a-z0-9./:_-]+$ ]] || { echo 'Invalid image'; exit 2; }
compose=(docker compose -f docker-compose.yml -f docker-compose.production.yml)
container=$("${compose[@]}" ps -q secureshare-api)
previous=""
previous_format=""
if [[ -n "$container" ]]; then
    previous=$(docker inspect --format '{{.Config.Image}}' "$container")
    previous_format=$(docker inspect --format '{{ index .Config.Labels "org.secureshare.encryption-version" }}' "$container")
fi
export API_IMAGE="$image"
"${compose[@]}" pull secureshare-api
# Ensure first-time deployments have healthy migration dependencies. On later
# deployments this is a no-op for the already running services.
"${compose[@]}" up -d --no-build --wait --wait-timeout 300 db clamav
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
"${compose[@]}" up -d --no-build --wait --wait-timeout 300
trap - ERR
# Persist the successful immutable image so a later reboot or compose run uses it.
if grep -q '^API_IMAGE=' .env; then
    sed -i "s|^API_IMAGE=.*|API_IMAGE=$image|" .env
else
    printf '\nAPI_IMAGE=%s\n' "$image" >> .env
fi
echo 'Deployment passed readiness checks.'
