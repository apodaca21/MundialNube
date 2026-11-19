# MundialNube Locust — World Cup hasta el campeón

Cada usuario simulado crea un mundial FIFA completo (32 equipos, 8 grupos):

1. Crea equipos y torneo.
2. Genera fase de grupos (`POST .../matches/group-stage`).
3. Anota todos los partidos de grupo, octavos, cuartos, semis, tercero y final.
4. Verifica 64 partidos completados y obtiene el campeón (ganador de la `FINAL`).

## Instalar

```bash
python3 -m venv ~/.venvs/mundialnube-load
~/.venvs/mundialnube-load/bin/python -m pip install -r load_test/requirements.txt
```

## Arrancar la API primero

La API debe estar en `http://localhost:5080` (u otro host que pases a Locust),
con PostgreSQL disponible.

```bash
ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=mundialnube;Username=tournament_svc;Password=password' \
ASPNETCORE_URLS='http://127.0.0.1:5080' \
dotnet run --project src/TournamentServices.Api --no-launch-profile
```

## Ejecutar (headless)

```bash
~/.venvs/mundialnube-load/bin/locust -f load_test/locustfile.py \
  --host http://127.0.0.1:5080 \
  --headless --users 1 --spawn-rate 1 --run-time 10m \
  --stop-timeout 120 \
  --csv /tmp/mundialnube-load --html /tmp/mundialnube-load.html
```

Empieza con **1 usuario**: un escenario completo hace muchas peticiones
(creación de 32 equipos + 8 grupos + 48+ scores). Sube concurrencia con cuidado.

Interactivo: quita `--headless` y abre http://localhost:8089.
