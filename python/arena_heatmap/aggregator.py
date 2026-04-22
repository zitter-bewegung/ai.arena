"""Accumulates per-unit-type occupancy and death heatmaps from many games."""

import numpy as np
from tqdm import tqdm

from .domain import BOARD, UNIT_TYPES, UnitState, is_team_a, mirror_x, parse_cell


def _apply(live, action):
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


def _record(heatmap, unit):
    """Add +1 to `unit`'s side-agnostic cell in a single unit-type heatmap."""
    x = unit.x if unit.is_team_a else mirror_x(unit.x)
    if 1 <= x <= BOARD and 1 <= unit.y <= BOARD:
        heatmap[unit.y - 1, x - 1] += 1


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
            _apply(live, action)
            for unit in live.values():
                if unit.unit_type in self.heatmaps:
                    _record(self.heatmaps[unit.unit_type], unit)


class DeathAggregator:
    """Accumulates per-unit-type death-cell counts across many games.

    Each unit contributes exactly one +1 to the cell where it dies, bucketed
    by unit type. Team B deaths are mirrored across the vertical centerline
    just like occupancy, so both teams share one side-agnostic distribution.
    """

    def __init__(self):
        self.heatmaps = {ut: np.zeros((BOARD, BOARD), dtype=np.int64) for ut in UNIT_TYPES}

    def consume_game(self, battle_result):
        """Replay one game's action log and record each unit's death cell."""
        live = {}
        for action in battle_result.get("actions", []):
            if action.get("actionType") == "Dies":
                unit = live.get(action.get("unitName"))
                if unit is not None and unit.unit_type in self.heatmaps:
                    _record(self.heatmaps[unit.unit_type], unit)
            _apply(live, action)


def collect(client, preset, n_games):
    """Activate a preset, run N games, return (occupancy, deaths) aggregators."""
    client.activate_preset(preset)
    occupancy = OccupancyAggregator()
    deaths = DeathAggregator()
    for _ in tqdm(range(n_games), desc=preset.slug, leave=False):
        result = client.run_random_game()
        occupancy.consume_game(result)
        deaths.consume_game(result)
    return occupancy, deaths
