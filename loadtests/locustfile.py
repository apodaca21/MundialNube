"""Identical read workload for port-forward and ingress; counts actual serving Pods."""
import json
import os
import time
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

from locust import HttpUser, between, events, task

pod_requests = Counter()
missing_pod_header = 0
started_at = None
peak_users = 0


class TournamentUser(HttpUser):
    wait_time = between(0.05, 0.15)

    def on_start(self):
        self.client.headers.update({"Host": os.getenv("TARGET_HOST", "mundial.local")})

    @task(1)
    def health(self):
        self.client.get("/health", name="GET /health")

    @task(3)
    def tournaments(self):
        self.client.get("/tournaments", name="GET /tournaments")


@events.test_start.add_listener
def on_start(environment, **kwargs):
    global started_at, peak_users, missing_pod_header
    started_at = time.time()
    peak_users = 0
    missing_pod_header = 0
    pod_requests.clear()


@events.request.add_listener
def on_request(response=None, exception=None, **kwargs):
    global missing_pod_header
    if response is not None:
        pod = response.headers.get("X-Pod-Name")
        if pod:
            pod_requests[pod] += 1
        else:
            missing_pod_header += 1


@events.spawning_complete.add_listener
def on_spawn(user_count, **kwargs):
    global peak_users
    peak_users = max(peak_users, user_count)


@events.test_stop.add_listener
def on_stop(environment, **kwargs):
    total = environment.stats.total
    now = time.time()
    result = {
        "scenario": os.getenv("SCENARIO", "unspecified"),
        "host": environment.host,
        "started_at_utc": datetime.fromtimestamp(started_at, timezone.utc).isoformat(),
        "ended_at_utc": datetime.fromtimestamp(now, timezone.utc).isoformat(),
        "duration_seconds": now - started_at,
        "configured_users": environment.parsed_options.num_users,
        "peak_users": peak_users,
        "requests": total.num_requests,
        "failures": total.num_failures,
        "requests_per_second": total.total_rps,
        "latency_ms": {
            "average": total.avg_response_time,
            "min": total.min_response_time,
            "p50": total.get_response_time_percentile(0.50),
            "p95": total.get_response_time_percentile(0.95),
            "p99": total.get_response_time_percentile(0.99),
            "max": total.max_response_time,
        },
        "pods_seen": len(pod_requests),
        "requests_by_pod": dict(sorted(pod_requests.items())),
        "responses_without_pod_header": missing_pod_header,
    }
    output = Path(os.environ.get("RESULT_JSON", "loadtests/results/summary.json"))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n")
    if total.num_requests == 0 or total.num_failures or missing_pod_header:
        environment.process_exit_code = 1
