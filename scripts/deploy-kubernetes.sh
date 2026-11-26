#!/usr/bin/env bash
set -euo pipefail
context=${1:?Usage: scripts/deploy-kubernetes.sh CONTEXT IMAGE}
image=${2:?Pass an immutable image tag or digest}
if [[ ! "$image" =~ ^[A-Za-z0-9][A-Za-z0-9._:/@-]+$ ]]; then echo 'Invalid image name.' >&2; exit 2; fi
root=$(cd "$(dirname "$0")/.." && pwd)
namespace=mundialnube-v3
k=(kubectl --context "$context" -n "$namespace")
rendered=$(mktemp)
job=$(mktemp)
trap 'rm -f "$rendered" "$job"' EXIT
# File inputs are tracked inside this repository; manifests contain no Secret values.
kubectl kustomize --load-restrictor LoadRestrictionsNone "$root/kubernetes/base" \
  | sed "s|image: mundialnube-api:dev|image: $image|g" > "$rendered"
sed "s|image: mundialnube-api:dev|image: $image|g" "$root/kubernetes/migration-job.yaml" > "$job"
kubectl --context "$context" apply -f "$root/kubernetes/base/namespace.yaml"
"${k[@]}" get secret mundial-secrets >/dev/null
# Only the DB is applied before migration. Existing API replicas keep serving their old version.
"${k[@]}" apply -f "$root/kubernetes/base/postgres.yaml"
"${k[@]}" rollout status statefulset/tournament-db --timeout=180s
"${k[@]}" delete job tournaments-migrate --ignore-not-found
"${k[@]}" apply -f "$job"
if ! "${k[@]}" wait --for=condition=complete job/tournaments-migrate --timeout=300s; then
  "${k[@]}" logs job/tournaments-migrate --all-containers=true >&2 || true
  exit 1
fi
"${k[@]}" apply -f "$rendered"
"${k[@]}" rollout status deployment/tournaments-api --timeout=300s
"${k[@]}" rollout status deployment/tournaments-consumer --timeout=180s
"${k[@]}" get pods -o wide
"${k[@]}" top pods || true
