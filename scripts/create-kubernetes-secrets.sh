#!/usr/bin/env bash
set -euo pipefail
# Read credentials from the process environment, never write a Secret to Git.
context=${1:?Usage: scripts/create-kubernetes-secrets.sh CONTEXT}
: "${POSTGRES_PASSWORD:?Export POSTGRES_PASSWORD}"
: "${BROKER_PASSWORD:?Export BROKER_PASSWORD}"
: "${GRAFANA_ADMIN_PASSWORD:?Export GRAFANA_ADMIN_PASSWORD}"
if [[ ! "$POSTGRES_PASSWORD" =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo 'For this helper, POSTGRES_PASSWORD must use letters, digits, _ or -.' >&2
  exit 2
fi
root=$(cd "$(dirname "$0")/.." && pwd)
kubectl --context "$context" apply -f "$root/kubernetes/base/namespace.yaml"
# Output is only the Secret resource name. Password values are not logged.
kubectl --context "$context" -n mundialnube-v3 create secret generic mundial-secrets \
  --from-literal="postgres-password=$POSTGRES_PASSWORD" \
  --from-literal="database-connection=Host=tournament-db;Port=5432;Database=tournament_db;Username=tournament_svc;Password=$POSTGRES_PASSWORD" \
  --from-literal="broker-user=${BROKER_USER:-mundial}" \
  --from-literal="broker-password=$BROKER_PASSWORD" \
  --from-literal="grafana-password=$GRAFANA_ADMIN_PASSWORD" \
  --dry-run=client -o yaml | kubectl --context "$context" -n mundialnube-v3 apply -f -
