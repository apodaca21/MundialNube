#!/usr/bin/env bash
set -euo pipefail
cluster=${1:?Usage: scripts/load-kind-images.sh CLUSTER IMAGE [IMAGE...]}
shift
if [[ $# -eq 0 ]]; then echo 'At least one image is required.' >&2; exit 2; fi
# Docker Desktop's containerd store may retain only one platform of an OCI index.
# Import that platform explicitly instead of asking ctr for absent attestations/platforms.
for node in $("${KIND_BIN:-kind}" get nodes --name "$cluster"); do
  for image in "$@"; do
    arch=$(docker image inspect "$image" --format '{{.Architecture}}')
    docker save --platform "linux/$arch" "$image" \
      | docker exec -i "$node" ctr --namespace=k8s.io images import --platform "linux/$arch" -
  done
done
