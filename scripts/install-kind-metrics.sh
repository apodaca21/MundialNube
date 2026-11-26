#!/usr/bin/env bash
set -euo pipefail
context=${1:?Usage: scripts/install-kind-metrics.sh kind-CONTEXT}
case "$context" in kind-*) ;; *) echo 'This test helper only accepts a kind context.' >&2; exit 2;; esac
# 0.7.x supports Kubernetes 1.27+, including the inventory's 1.29 cluster.
kubectl --context "$context" apply -f https://github.com/kubernetes-sigs/metrics-server/releases/download/v0.7.2/components.yaml
# kind kubelet certificates lack a public CA; this local-lab exception is never applied elsewhere.
kubectl --context "$context" -n kube-system patch deployment metrics-server --type=json \
  -p='[{"op":"add","path":"/spec/template/spec/containers/0/args/-","value":"--kubelet-insecure-tls"}]'
kubectl --context "$context" -n kube-system rollout status deployment/metrics-server --timeout=180s
