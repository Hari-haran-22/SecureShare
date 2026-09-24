#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
app_private_ip="$1"
[[ "$app_private_ip" =~ ^[0-9]{1,3}(\.[0-9]{1,3}){3}$ ]] || { echo 'Invalid application private IP'; exit 2; }
compose=(docker compose -f docker-compose.yml -f docker-compose.production.yml)

cat > deploy/prometheus-runtime.yml <<EOF
global:
  scrape_interval: 15s
rule_files:
  - /etc/prometheus/alerts.yml
alerting:
  alertmanagers:
    - static_configs:
        - targets: ['alertmanager:9093']
scrape_configs:
  - job_name: secureshare-api
    static_configs:
      - targets: ['$app_private_ip:8080']
EOF

"${compose[@]}" pull clamav prometheus alertmanager grafana
"${compose[@]}" --profile services up -d --no-build --wait --wait-timeout 900 clamav
"${compose[@]}" --profile services up -d --no-build prometheus alertmanager grafana
echo 'Scanner and monitoring services are running.'
