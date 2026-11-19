#!/usr/bin/env bash
# Locust MundialNube: mundial completo hasta campeón.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Reutiliza el venv del repo del profe si existe
LOCUST_PY="${LOCUST_PY:-$HOME/Downloads/tournaments-convert-to-csharp/.venvs/tournaments-load/bin/python}"
WRAPPER="${WRAPPER:-$HOME/Downloads/tournaments-convert-to-csharp/load_test/run_locust.py}"
HOST="${HOST:-http://127.0.0.1:5080}"

if [[ ! -x "$LOCUST_PY" ]]; then
  echo "No encuentro Locust en $LOCUST_PY"
  echo "Crea el venv: python3 -m venv .venv && .venv/bin/pip install -r load_test/requirements.txt"
  exit 1
fi

if ! curl -sf "$HOST/health" >/dev/null; then
  echo "API MundialNube no responde en $HOST"
  echo "Arráncala primero, por ejemplo:"
  echo "  ConnectionStrings__DefaultConnection='Host=localhost;Port=15432;Database=mundialnube;Username=tournament_svc;Password=password' \\"
  echo "  ASPNETCORE_URLS='http://127.0.0.1:5080' \\"
  echo "  dotnet run --project src/TournamentServices.Api --no-launch-profile"
  exit 1
fi

cd "$ROOT"
if [[ -f "$WRAPPER" ]]; then
  exec "$LOCUST_PY" "$WRAPPER" -f load_test/locustfile.py \
    --host "$HOST" \
    --headless --users "${USERS:-1}" --spawn-rate 1 --run-time "${RUNTIME:-15m}" \
    --stop-timeout 180 \
    --csv /tmp/mundialnube-load --html /tmp/mundialnube-load.html
else
  exec "$LOCUST_PY" -m locust -f load_test/locustfile.py \
    --host "$HOST" \
    --headless --users "${USERS:-1}" --spawn-rate 1 --run-time "${RUNTIME:-15m}" \
    --stop-timeout 180 \
    --csv /tmp/mundialnube-load --html /tmp/mundialnube-load.html
fi
