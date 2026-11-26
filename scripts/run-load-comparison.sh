#!/usr/bin/env bash
set -euo pipefail
context=${1:?Usage: scripts/run-load-comparison.sh CONTEXT INGRESS_URL OUTPUT_DIR}
ingress=${2:?For example http://127.0.0.1:18080}
out=${3:?Choose an evidence directory}
root=$(cd "$(dirname "$0")/.." && pwd)
namespace=mundialnube-v3
k=(kubectl --context "$context" -n "$namespace")
mkdir -p "$out"
out=$(cd "$out" && pwd)
original_replicas=$("${k[@]}" get deployment tournaments-api -o jsonpath='{.spec.replicas}')
"${k[@]}" get hpa tournaments-api -o json | python3 -c 'import sys,json; o=json.load(sys.stdin); o.pop("status",None); o["metadata"]={"name":o["metadata"]["name"],"namespace":o["metadata"]["namespace"]}; json.dump(o,sys.stdout)' > "$out/hpa-before.json"
"${k[@]}" get nodes -o json > "$out/nodes.json"
"${k[@]}" delete hpa tournaments-api
forward_pid=''
cleanup() {
  if [[ -n "$forward_pid" ]]; then kill "$forward_pid" 2>/dev/null || true; fi
  "${k[@]}" scale deployment/tournaments-api --replicas="$original_replicas" >/dev/null || true
  "${k[@]}" apply -f "$out/hpa-before.json" >/dev/null || true
}
trap cleanup EXIT
set_replicas() {
  "${k[@]}" scale deployment/tournaments-api --replicas="$1"
  "${k[@]}" rollout status deployment/tournaments-api --timeout=300s
  "${k[@]}" wait --for=condition=Ready pod -l app=tournaments-api --timeout=180s
}
run_trial() {
  scenario=$1
  url=$2
  "${k[@]}" get pods -o wide > "$out/$scenario-pods.txt"
  "${k[@]}" top pods > "$out/$scenario-resources-before.txt" 2>&1 || true
  SCENARIO="$scenario" RESULT_JSON="$out/$scenario.json" TARGET_HOST=mundial.local \
    "${LOCUST_BIN:-locust}" -f "$root/loadtests/locustfile.py" --headless \
    --host "$url" --users "${USERS:-20}" --spawn-rate "${SPAWN_RATE:-5}" \
    --run-time "${DURATION:-60s}" --stop-timeout 10 \
    --csv "$out/$scenario" --csv-full-history --html "$out/$scenario.html" \
    > "$out/$scenario.log" 2>&1 &
  load_pid=$!
  while kill -0 "$load_pid" 2>/dev/null; do
    date -u '+%Y-%m-%dT%H:%M:%SZ' >> "$out/$scenario-resources-during.txt"
    "${k[@]}" top pods >> "$out/$scenario-resources-during.txt" 2>&1 || true
    sleep 5
  done
  wait "$load_pid"
  "${k[@]}" top pods > "$out/$scenario-resources-after.txt" 2>&1 || true
  "${k[@]}" get pods -o json > "$out/$scenario-pods.json"
}
set_replicas 1
# kubectl port-forward to Service selects ONE backing Pod; ClusterIP itself is not being benchmarked here.
"${k[@]}" port-forward service/tournaments-api "${DIRECT_PORT:-18082}:8080" > "$out/port-forward.log" 2>&1 &
forward_pid=$!
for attempt in $(seq 1 30); do
  if curl --max-time 10 -fsS "http://127.0.0.1:${DIRECT_PORT:-18082}/ready" >/dev/null; then break; fi
  sleep 1
done
curl --max-time 10 -fsS "http://127.0.0.1:${DIRECT_PORT:-18082}/tournaments" > "$out/dataset-before.json"
run_trial no-ingress-port-forward "http://127.0.0.1:${DIRECT_PORT:-18082}"
kill "$forward_pid"; forward_pid=''
run_trial ingress-1-pod "$ingress"
set_replicas 3
run_trial ingress-3-pods "$ingress"
curl --max-time 10 -fsS -H 'Host: mundial.local' "$ingress/tournaments" > "$out/dataset-after.json"
python3 - "$out" <<'PY'
import json, pathlib, sys
p=pathlib.Path(sys.argv[1])
a=json.loads((p/'dataset-before.json').read_text()); b=json.loads((p/'dataset-after.json').read_text())
assert a==b, 'Dataset changed during benchmark; repeat with a stable dataset.'
rows=[json.loads((p/(name+'.json')).read_text()) for name in ['no-ingress-port-forward','ingress-1-pod','ingress-3-pods']]
assert rows[0]['pods_seen']==1 and rows[1]['pods_seen']==1, 'Single-Pod baseline not demonstrated.'
assert rows[2]['pods_seen']>=2, 'Ingress distribution not demonstrated; inspect per-Pod results.'
(p/'comparison.json').write_text(json.dumps({'dataset_tournaments':len(a),'runs':rows},indent=2)+'\n')
print('Comparison saved to',p/'comparison.json')
PY
