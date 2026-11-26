#!/usr/bin/env bash
set -euo pipefail
mode=${1:?Usage: scripts/create-kind.sh single|multi}
case "$mode" in single|multi) ;; *) echo 'Choose single or multi.' >&2; exit 2;; esac
root=$(cd "$(dirname "$0")/.." && pwd)
name="mundial-v3-$mode"
# A dedicated kubeconfig avoids changing the user's active kubectl context.
mkdir -p "$root/loadtests/results"
config="$root/loadtests/results/$name.kubeconfig"
kind create cluster --name "$name" --config "$root/kubernetes/kind/$mode.yaml" \
  --image "${KIND_NODE_IMAGE:-kindest/node:v1.34.0}" --kubeconfig "$config" --wait 180s
printf 'Cluster ready. Use KUBECONFIG=%q with context kind-%s.\n' "$config" "$name"
