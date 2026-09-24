#!/usr/bin/env bash
set -euo pipefail
commit="$1"
image="$2"
[[ "$commit" =~ ^[a-f0-9]{40}$ && "$image" =~ ^[a-z0-9][a-z0-9./:_-]+$ ]] || exit 2
cd /opt/secureshare
test -f .env
test -f secrets/protection.pfx
# Refuse to overwrite changes on the deployment checkout.
git diff --quiet
git diff --cached --quiet
git fetch origin "$commit"
# Remove stale checkout files while preserving ignored runtime secrets.
git clean -fd
git checkout --detach "$commit"
bash scripts/deploy.sh "$image"
