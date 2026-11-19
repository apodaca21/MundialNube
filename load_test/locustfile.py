"""Load-test MundialNube through a full FIFA World Cup path to a champion."""

from __future__ import annotations

import logging
from time import perf_counter
from uuid import uuid4

from locust import HttpUser, between, events, task
from locust.exception import StopUser


class ScenarioFailed(Exception):
    """A failed request or contract check; do not issue dependent requests."""


STAGES = (
    "GROUP",
    "ROUND_OF_16",
    "QUARTERFINAL",
    "SEMIFINAL",
    "THIRD_PLACE",
    "FINAL",
)


@events.init_command_line_parser.add_listener
def add_options(parser):
    parser.add_argument(
        "--full-world-cup",
        action="store_true",
        default=True,
        help="Run the 32-team / 8-group World Cup scenario (default).",
    )


class MundialUser(HttpUser):
    wait_time = between(2, 5)

    def request(self, method, path, name, status=200, payload=None):
        with self.client.request(
            method,
            path,
            name=name,
            json=payload,
            timeout=60,
            catch_response=True,
            allow_redirects=False,
        ) as response:
            try:
                if response.status_code != status:
                    raise ValueError(f"Expected HTTP {status}, got {response.status_code}")
                if status in (204,):
                    return None
                if not response.text:
                    return None
                try:
                    return response.json()
                except ValueError:
                    return response.text
            except (ValueError, TypeError, KeyError) as error:
                response.failure(str(error))
                raise ScenarioFailed(f"{name}: {error}") from error

    def create_team(self, name: str) -> dict:
        return self.request("POST", "/teams", "POST /teams", 201, {"name": name})

    def create_tournament(self, name: str) -> dict:
        return self.request(
            "POST",
            "/tournaments",
            "POST /tournaments",
            201,
            {
                "name": name,
                "format": {"type": "FIFA", "maxGroups": 8, "maxTeamsPerGroup": 4},
            },
        )

    def create_group(self, tournament_id: str, name: str) -> dict:
        return self.request(
            "POST",
            f"/tournaments/{tournament_id}/groups",
            "POST /tournaments/{tournamentId}/groups",
            201,
            {"name": name},
        )

    def assign_teams(self, tournament_id: str, group_id: str, team_ids: list[str]) -> None:
        self.request(
            "PATCH",
            f"/tournaments/{tournament_id}/groups/{group_id}/teams",
            "PATCH /tournaments/{tournamentId}/groups/{groupId}/teams",
            204,
            {"teamIds": team_ids},
        )

    def generate_group_stage(self, tournament_id: str) -> list:
        matches = self.request(
            "POST",
            f"/tournaments/{tournament_id}/matches/group-stage",
            "POST /tournaments/{tournamentId}/matches/group-stage",
            200,
        )
        if not isinstance(matches, list) or len(matches) != 48:
            raise ScenarioFailed(f"Expected 48 group matches, got {type(matches)}/{getattr(matches, '__len__', lambda: '?')()}")
        return matches

    def get_matches(self, tournament_id: str) -> list:
        matches = self.request(
            "GET",
            f"/tournaments/{tournament_id}/matches",
            "GET /tournaments/{tournamentId}/matches",
            200,
        )
        if not isinstance(matches, list):
            raise ScenarioFailed("Expected match list")
        return matches

    def score_incomplete(self, tournament_id: str, stage: str, home: int, visitor: int) -> int:
        matches = [
            match
            for match in self.get_matches(tournament_id)
            if match.get("stage") == stage and not match.get("isCompleted")
        ]
        for match in matches:
            scored = self.request(
                "PATCH",
                f"/tournaments/{tournament_id}/matches/{match['id']}/score",
                f"PATCH /tournaments/{{tournamentId}}/matches/{{matchId}}/score [{stage}]",
                200,
                {"homeTeamScore": home, "visitorTeamScore": visitor},
            )
            if not scored.get("isCompleted"):
                raise ScenarioFailed(f"Match {match['id']} not completed after score")
            if stage != "GROUP" and scored.get("winner") is None:
                raise ScenarioFailed(f"Knockout match {match['id']} has no winner")
        return len(matches)

    def wait_for_stage(self, tournament_id: str, stage: str, expected: int) -> None:
        matches = [m for m in self.get_matches(tournament_id) if m.get("stage") == stage]
        if len(matches) != expected:
            raise ScenarioFailed(f"Expected {expected} {stage} matches, got {len(matches)}")

    @task
    def world_cup_to_champion(self):
        started = perf_counter()
        error = None
        champion = None
        try:
            champion = self.run_world_cup()
        except ScenarioFailed as failure:
            error = failure
            logging.error("World Cup scenario failed: %s", failure)
        except StopUser:
            raise
        except Exception as failure:  # noqa: BLE001 - report unexpected errors as scenario failures
            error = failure
            logging.exception("Unexpected scenario failure")

        self.environment.events.request.fire(
            request_type="SCENARIO",
            name="World Cup to champion",
            response_time=(perf_counter() - started) * 1000,
            response_length=0,
            exception=error,
            context={**(self.context() or {}), "champion": champion},
        )

    def run_world_cup(self) -> str:
        self.request("GET", "/health", "GET /health", 200)
        run_id = uuid4().hex[:10]

        teams = []
        for index in range(32):
            team = self.create_team(f"mn-{run_id}-t{index:02d}")
            if not isinstance(team, dict) or "id" not in team:
                raise ScenarioFailed("Team create did not return id")
            teams.append(team["id"])

        tournament = self.create_tournament(f"mn-{run_id}-world-cup")
        tournament_id = tournament["id"]

        for group_index in range(8):
            group = self.create_group(tournament_id, f"Group-{chr(ord('A') + group_index)}")
            group_id = group["id"]
            assigned = teams[group_index * 4 : (group_index + 1) * 4]
            self.assign_teams(tournament_id, group_id, assigned)

        self.generate_group_stage(tournament_id)

        # Distinct home winners so standings advance cleanly into knockouts.
        self.score_incomplete(tournament_id, "GROUP", 1, 0)
        self.wait_for_stage(tournament_id, "ROUND_OF_16", 8)

        self.score_incomplete(tournament_id, "ROUND_OF_16", 2, 1)
        self.wait_for_stage(tournament_id, "QUARTERFINAL", 4)

        self.score_incomplete(tournament_id, "QUARTERFINAL", 2, 0)
        self.wait_for_stage(tournament_id, "SEMIFINAL", 2)

        self.score_incomplete(tournament_id, "SEMIFINAL", 3, 1)
        self.wait_for_stage(tournament_id, "THIRD_PLACE", 1)
        self.wait_for_stage(tournament_id, "FINAL", 1)

        self.score_incomplete(tournament_id, "THIRD_PLACE", 1, 0)
        self.score_incomplete(tournament_id, "FINAL", 2, 1)

        matches = self.get_matches(tournament_id)
        if len(matches) != 64:
            raise ScenarioFailed(f"Expected 64 total matches, got {len(matches)}")
        if any(not match.get("isCompleted") for match in matches):
            raise ScenarioFailed("Not all matches completed")

        final = next(match for match in matches if match.get("stage") == "FINAL")
        winner = final.get("winner")
        if winner not in ("HOME", "VISITOR"):
            raise ScenarioFailed(f"Final has no winner: {winner!r}")

        champion_id = final["homeTeamId"] if winner == "HOME" else final["visitorTeamId"]
        standings = self.request(
            "GET",
            f"/tournaments/{tournament_id}/standings",
            "GET /tournaments/{tournamentId}/standings",
            200,
        )
        if not isinstance(standings, list) or len(standings) != 32:
            raise ScenarioFailed("Standings should list 32 teams")

        logging.info("Champion team id=%s tournament=%s", champion_id, tournament_id)
        return champion_id
