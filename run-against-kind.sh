#!/usr/bin/env bash
# Corre la API contra Postgres de Kind (requiere port-forward activo en otra terminal).
set -euo pipefail
cd "$(dirname "$0")"

# DB dedicada (mundialnube) para que EF cree su schema sin chocar con el SQL del profe.
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=mundialnube;Username=tournament_svc;Password=password'
export ASPNETCORE_URLS='http://127.0.0.1:5080'

echo "Connection string -> Kind Postgres (password=password)"
echo "API en http://127.0.0.1:5080"
dotnet run --project src/TournamentServices.Api --no-launch-profile
