"""Thin HTTP wrapper around the Arena.AI backend.

Keeps the base URL and the requests.Session in one place so callers don't
have to thread them through every function.
"""

import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


class ArenaClient:

    def __init__(self, base_url):
        self.base_url = base_url
        self.session = requests.Session()

    def ping(self):
        """Fail fast if the backend isn't reachable."""
        r = self.session.get(f"{self.base_url}/unit-stats", verify=False, timeout=5)
        r.raise_for_status()

    def activate_preset(self, preset):
        """Swap the backend's active unit stats to this preset."""
        r = self.session.post(
            f"{self.base_url}/unit-stats", json=preset.stats, verify=False, timeout=15,
        )
        r.raise_for_status()

    def run_random_game(self):
        """Play one game between two randomly-composed teams. Returns the BattleResult dict."""
        r = self.session.post(
            f"{self.base_url}/BattleCalculator/calculate-random-team",
            verify=False, timeout=60,
        )
        r.raise_for_status()
        return r.json()
