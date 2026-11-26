#!/usr/bin/env python3
"""Add fully simulated demonstration tournaments; never reset or delete existing data.

Each invocation creates additional tournaments with unique names and new team IDs.
If a request fails, the script stops and leaves completed tournaments (and the
current partially completed tournament) available for inspection in the API.
Requires only Python's standard library.
"""
import argparse
import json
import re
import secrets
import sys
from collections import Counter
from datetime import datetime, timezone
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import Request, urlopen


class SeedError(RuntimeError):
    pass


def check(condition, message):
    if not condition:
        raise SeedError(message)


def positive_count(value):
    try:
        number = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("count debe ser un número entero") from error
    if number < 1:
        raise argparse.ArgumentTypeError("count debe ser mayor que cero")
    return number


class Api:
    def __init__(self, base_url):
        parts = urlsplit(base_url)
        if parts.scheme not in ("http", "https") or not parts.netloc or parts.query or parts.fragment or parts.username:
            raise SeedError("base-url debe ser una URL HTTP(S) sin credenciales, parámetros ni fragmentos")
        self.base_url = base_url.rstrip("/")

    def request(self, path, method="GET", body=None, expected=200):
        payload = None if body is None else json.dumps(body).encode("utf-8")
        headers = {"Accept": "application/json"}
        if payload is not None:
            headers["Content-Type"] = "application/json"
        request = Request(self.base_url + path, data=payload, headers=headers, method=method)
        try:
            with urlopen(request, timeout=90) as response:
                raw = response.read().decode("utf-8")
                check(response.status == expected, f"{method} {path}: HTTP {response.status}; se esperaba {expected}")
                try:
                    return json.loads(raw) if raw else None
                except json.JSONDecodeError as error:
                    raise SeedError(f"{method} {path}: la respuesta no es JSON válido") from error
        except HTTPError as error:
            detail = error.read().decode("utf-8", errors="replace")[:1000]
            raise SeedError(f"{method} {path}: HTTP {error.code}: {detail}") from error
        except (URLError, TimeoutError) as error:
            raise SeedError(f"{method} {path}: conexión fallida o tiempo agotado: {error}") from error


def simulate(api, name, all_team_ids):
    created = api.request("/tournaments", "POST", {
        "name": name,
        "format": {"type": "ROUND_ROBIN", "maxGroups": 8, "maxTeamsPerGroup": 4},
    }, expected=201)
    check(isinstance(created, dict) and isinstance(created.get("id"), str), "El torneo creado no tiene ID")
    tournament_id = created["id"]
    check(re.fullmatch(r"[A-Za-z0-9-]+", tournament_id), "El ID del torneo tiene un formato inesperado")
    print(f"  Creado {name}: {tournament_id}", flush=True)
    prefix = f"/tournaments/{tournament_id}"
    teams = api.request(prefix + "/teams")
    check(isinstance(teams, list) and len(teams) == 32, f"{tournament_id}: se esperaban 32 equipos")
    team_ids = {team["id"] for team in teams}
    check(len(team_ids) == 32 and not team_ids.intersection(all_team_ids), "Los equipos no son distintos entre torneos")
    all_team_ids.update(team_ids)

    generated = api.request(prefix + "/groups/generate", "POST")
    check(len(generated) == 8, f"{tournament_id}: se esperaban ocho grupos")
    filled = api.request(prefix + "/groups/fill", "POST")
    check(len(filled) == 8 and all(len(group["teamIds"]) == 4 for group in filled), "Los grupos no quedaron completos")
    check({team_id for group in filled for team_id in group["teamIds"]} == team_ids, "La asignación de participantes no coincide")
    fixtures = api.request(prefix + "/matches/generate", "POST")
    check(len(fixtures) == 48, "La fase de grupos debe tener 48 partidos")
    simulated = api.request(prefix + "/matches/simulate", "POST")
    check(len(simulated) == 48 and all(match["isCompleted"] for match in simulated), "Hay partidos de grupos incompletos")
    standings = api.request(prefix + "/standings")
    check(len(standings) == 8 and all(len(group["teams"]) == 4 for group in standings), "La tabla de grupos está incompleta")
    check(all(team["played"] == 3 for group in standings for team in group["teams"]), "Cada participante debe jugar tres partidos de grupos")
    champion = api.request(prefix + "/knockout", "POST")
    check(champion["tournamentId"] == tournament_id and champion["teamId"] in team_ids, "El campeón no pertenece al torneo")

    matches = api.request(prefix + "/matches")
    rounds = Counter(match["round"] for match in matches)
    check(len(matches) == 63 and rounds == Counter({0: 48, 1: 8, 2: 4, 3: 2, 4: 1}), "Las rondas no contienen los 63 partidos esperados")
    check(all(match["isCompleted"] for match in matches), "La simulación dejó partidos incompletos")
    check(all(match["score"]["homeTeamScore"] != match["score"]["visitorTeamScore"]
              for match in matches if match["round"] > 0), "Una eliminatoria terminó empatada")
    final = next(match for match in matches if match["round"] == 4)
    winner_id = final["homeTeamId"] if final["score"]["homeTeamScore"] > final["score"]["visitorTeamScore"] else final["visitorTeamId"]
    check(champion["teamId"] == winner_id, "El campeón guardado no coincide con el ganador de la final")
    return {"tournamentId": tournament_id, "tournamentName": name,
            "champion": champion["teamName"], "championTeamId": champion["teamId"],
            "teams": 32, "matches": len(matches), "completedAtUtc": champion["completedAtUtc"]}


def main():
    parser = argparse.ArgumentParser(description="Añade torneos DEMO nuevos con 32 equipos, 63 partidos y campeón. No borra ni reinicia datos.")
    parser.add_argument("--count", type=positive_count, default=20, help="Número de torneos adicionales (predeterminado: 20)")
    parser.add_argument("--base-url", default="http://localhost:8183", help="URL de la API (predeterminado: http://localhost:8183)")
    args = parser.parse_args()
    completed = []
    try:
        api = Api(args.base_url)
        api.request("/ready")
        run_id = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + "-" + secrets.token_hex(3)
        print(f"Añadiendo {args.count} torneos DEMO adicionales. Los datos existentes se conservarán.", flush=True)
        all_team_ids = set()
        for index in range(1, args.count + 1):
            name = f"DEMO V3 {run_id} {index:02d}"
            result = simulate(api, name, all_team_ids)
            completed.append(result)
            print(f"  [{index}/{args.count}] Campeón: {result['champion']}; 63 partidos completos.", flush=True)
        print(json.dumps({"addedTournaments": len(completed), "addedTeams": len(all_team_ids),
                          "addedMatches": len(completed) * 63, "results": completed}, ensure_ascii=False, indent=2))
        return 0
    except (SeedError, KeyError, TypeError, ValueError, StopIteration) as error:
        print(f"ERROR: {error}\nTorneos completados en esta ejecución: {len(completed)}. "
              "No se eliminó ningún dato. El último torneo mostrado puede haber quedado parcial; "
              "revísalo antes de volver a ejecutar, pues cada ejecución agrega torneos nuevos.", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("\nInterrumpido. Los torneos ya creados se conservan; no se revirtió ni borró ningún dato.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
