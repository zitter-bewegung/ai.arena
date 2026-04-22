"""Accumulates per-unit-type occupancy heatmaps from many games."""

import numpy as np
from tqdm import tqdm

from .domain import BOARD, UNIT_TYPES, UnitState, is_team_a, mirror_x, parse_cell


class OccupancyAggregator:
    """Accumulates per-unit-type turn-cell occupancy across many games.

    "Turn-cell occupancy" = after each action step in the game, every unit
    still alive contributes +1 to its current cell, bucketed by unit type.
    A unit that sits still for 10 consecutive actions contributes 10 to its
    cell. Fast-moving and short-lived units leave thin trails; slow
    survivors leave dense bright cells.

    Team B positions are always reflected across the vertical centerline
    (x → 21 - x) before being written, so both teams land in a single
    side-agnostic distribution per unit type.
    """

    def __init__(self):
        self.heatmaps = {ut: np.zeros((BOARD, BOARD), dtype=np.int64) for ut in UNIT_TYPES}

    def consume_game(self, battle_result):
        """Replay one game's action log and fold it into the heatmaps."""
        live = {}  # unit_name -> UnitState
        for action in battle_result.get("actions", []):
            self._apply(live, action)
            self._snapshot(live)

    def _apply(self, live, action):
        """Mutate `live` based on a single BattleAction."""
        atype = action.get("actionType")
        name = action.get("unitName")
        dest = action.get("destination")

        if atype == "Appears" and dest:
            x, y = parse_cell(dest)
            live[name] = UnitState(
                unit_type=action.get("unitType"),
                x=x, y=y,
                is_team_a=is_team_a(name),
            )
        elif atype == "Moves" and dest and name in live:
            unit = live[name]
            unit.x, unit.y = parse_cell(dest)
        elif atype == "Dies":
            live.pop(name, None)
        # Attacks / LosesHealth / SkipsTurn: no position change.

    def _snapshot(self, live):
        """Record every live unit's current cell into its unit-type heatmap."""
        for unit in live.values():
            x = unit.x if unit.is_team_a else mirror_x(unit.x)
            if 1 <= x <= BOARD and 1 <= unit.y <= BOARD and unit.unit_type in self.heatmaps:
                self.heatmaps[unit.unit_type][unit.y - 1, x - 1] += 1


def collect_occupancy(client, preset, n_games):
    """Activate a preset on the backend, run N games, return a filled OccupancyAggregator."""
    client.activate_preset(preset)
    aggregator = OccupancyAggregator()
    for _ in tqdm(range(n_games), desc=preset.slug, leave=False):
        aggregator.consume_game(client.run_random_game())
    return aggregator
