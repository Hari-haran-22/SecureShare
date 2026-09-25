#!/usr/bin/env bash
set -euo pipefail
commit="$1"
image="$2"
role="${3:-app}"
peer_private_ip="${4:-}"
public_host="${5:-}"
[[ "$commit" =~ ^[a-f0-9]{40}$ && "$image" =~ ^[a-z0-9][a-z0-9./:_-]+$ ]] || exit 2
[[ "$role" == app || "$role" == services ]] || exit 2
cd /opt/secureshare
if [[ ! -d .git ]]; then
    git clone https://github.com/Hari-haran-22/SecureShare.git .
fi
if [[ "$role" == services && ! -f .env ]]; then
    umask 077
    printf 'MSSQL_SA_PASSWORD=%s\nGRAFANA_ADMIN_PASSWORD=%s\nDOMAIN=services.invalid\n' \
        "$(openssl rand -base64 24)" "$(openssl rand -base64 24)" > .env
fi
if [[ "$role" == app ]]; then
    [[ "$public_host" =~ ^[a-zA-Z0-9.-]+$ ]] || { echo 'A valid public app hostname is required.'; exit 2; }
    if [[ ! -e .env && ! -e secrets/protection.pfx ]]; then
        sudo bash scripts/initialize.sh "${public_host}.nip.io"
        sudo chown "$(id -u):$(id -g)" .env
    elif [[ ! -f .env || ! -f secrets/protection.pfx ]]; then
        echo 'Incomplete application secrets; restore both .env and secrets/protection.pfx.'
        exit 1
    fi
fi
test -f .env || { echo 'Deployment environment file is missing.'; exit 1; }
# Refuse to overwrite changes on the deployment checkout.
git diff --quiet
git diff --cached --quiet
git fetch origin "$commit"
# Remove stale checkout files while preserving runtime state even when an older
# checkout does not yet contain the current ignore rules.
git clean -fd -e .env -e secrets/ -e backups/
git checkout --detach "$commit"
if [[ "$role" == services ]]; then
    bash scripts/deploy-services.sh "$peer_private_ip"
else
    bash scripts/deploy.sh "$image" "$peer_private_ip"
fi
