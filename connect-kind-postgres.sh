#!/usr/bin/env bash
# Conecta MundialNube al Postgres del cluster Kind (tournaments).
set -euo pipefail

KIND_CLUSTER="${KIND_CLUSTER:-kind}"
export KUBECONFIG="${KUBECONFIG:-$HOME/.kube/config}"

if ! kubectl config get-contexts "kind-${KIND_CLUSTER}" >/dev/null 2>&1; then
  kind export kubeconfig --name "$KIND_CLUSTER"
fi
kubectl config use-context "kind-${KIND_CLUSTER}"

echo "Postgres Kind:"
kubectl -n tournaments get pods

# Libera 5432 si quedó un port-forward viejo
if lsof -nP -iTCP:5432 -sTCP:LISTEN >/dev/null 2>&1; then
  echo "Puerto 5432 ocupado; intenta matar el proceso anterior o usa 5433."
fi

echo "Iniciando port-forward 5432:5432 (deja esta terminal abierta)..."
kubectl -n tournaments port-forward service/tournament-db 5432:5432
