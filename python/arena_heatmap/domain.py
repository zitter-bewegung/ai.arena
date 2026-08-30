"""Core domain types, constants, and coordinate helpers.

The Arena.AI board is a 20x20 grid addressed in chess notation: columns A..T
map to x=1..20, rows are y=1..20. These primitives are shared by every
other module in the package.
"""

from dataclasses import dataclass


# ── Board geometry ──────────────────────────────────────────────────────────

BOARD = 20

UNIT_TYPES = ("Light", "Heavy", "Fast", "ShortRange", "LongRange")

# Matches Arena.AI.Core/Models/UnitType.cs ordering (Light=0 ... LongRange=4).
TYPE_INT = {ut: i for i, ut in enumerate(UNIT_TYPES)}

# Default (range, movement) per unit type — shared by all but the survival preset.
DEFAULT_RM = {
    "Light":      (1, 12),
    "Heavy":      (1, 5),
    "Fast":       (1, 17),
    "ShortRange": (7, 5),
    "LongRange":  (17, 2),
}


# ── Data types ──────────────────────────────────────────────────────────────

@dataclass(frozen=True)
class Preset:
    """One UnitFactory stats preset, ready to POST to /unit-stats."""
    slug: str
    title: str
    stats: dict  # UnitType name -> {type, attack, defence, range, movement}


@dataclass
class UnitState:
    """Mutable position/type for one live unit while replaying a game."""
    unit_type: str
    x: int
    y: int
    is_team_a: bool


# ── Coordinate helpers ──────────────────────────────────────────────────────

def parse_cell(chess_notation):
    """Chess notation -> 1-indexed (x, y). 'B5' -> (2, 5)."""
    return ord(chess_notation[0].upper()) - ord("A") + 1, int(chess_notation[1:])


def mirror_x(x):
    """Reflect column x across the board's vertical centerline."""
    return BOARD + 1 - x


def is_team_a(unit_name):
    """Unit names look like 'teamA_3' / 'teamB_7' (case-insensitive)."""
    return unit_name.lower().startswith("teama")
