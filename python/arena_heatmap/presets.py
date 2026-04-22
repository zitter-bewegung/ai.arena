"""The 11 unit-stat presets copied verbatim from Arena.AI.Core/Logic/UnitFactory.cs.

Kept client-side rather than fetched from the backend so the set is stable
across runs and trivially inspectable in version control.
"""

from .domain import DEFAULT_RM, TYPE_INT, UNIT_TYPES, Preset


def build_preset(slug, title, attack_defence, rm_overrides=None):
    """Compose a Preset from {unit: (atk, def)} plus optional (range, movement) overrides."""
    rm = dict(DEFAULT_RM)
    if rm_overrides:
        rm.update(rm_overrides)
    stats = {
        ut: {
            "type": TYPE_INT[ut],
            "attack": attack_defence[ut][0],
            "defence": attack_defence[ut][1],
            "range": rm[ut][0],
            "movement": rm[ut][1],
        }
        for ut in UNIT_TYPES
    }
    return Preset(slug=slug, title=title, stats=stats)


PRESETS = [
    build_preset("25_wb", "2.5 — Winning-contribution balanced", {
        "Light": (27, 23), "Heavy": (27, 30), "Fast": (28, 25),
        "ShortRange": (30, 3), "LongRange": (5, 9),
    }),
    build_preset("25_sr", "2.5 — Semi-random balanced", {
        "Light": (0, 29), "Heavy": (30, 23), "Fast": (0, 30),
        "ShortRange": (17, 1), "LongRange": (1, 8),
    }),
    build_preset("25_global_v1", "2.5 — Globally balanced (v1)", {
        "Light": (8, 21), "Heavy": (5, 30), "Fast": (27, 0),
        "ShortRange": (29, 10), "LongRange": (5, 0),
    }),
    build_preset("25_global_v2", "2.5 — Globally balanced (v2)", {
        "Light": (1, 26), "Heavy": (30, 14), "Fast": (10, 12),
        "ShortRange": (30, 4), "LongRange": (0, 3),
    }),
    build_preset("25_strict", "2.5 — Strictly balanced", {
        "Light": (25, 25), "Heavy": (23, 30), "Fast": (17, 27),
        "ShortRange": (23, 29), "LongRange": (5, 30),
    }),
    build_preset("35_wb", "3.5 — Winning-contribution balanced (active)", {
        "Light": (22, 21), "Heavy": (12, 29), "Fast": (30, 16),
        "ShortRange": (30, 1), "LongRange": (9, 11),
    }),
    build_preset("35_global", "3.5 — Globally balanced", {
        "Light": (18, 9), "Heavy": (5, 25), "Fast": (7, 16),
        "ShortRange": (30, 2), "LongRange": (0, 19),
    }),
    build_preset("35_global_sr", "3.5 — Globally balanced (SR)", {
        "Light": (23, 24), "Heavy": (29, 23), "Fast": (24, 21),
        "ShortRange": (28, 0), "LongRange": (0, 28),
    }),
    build_preset("35_strict", "3.5 — Strictly balanced", {
        "Light": (21, 0), "Heavy": (3, 17), "Fast": (16, 3),
        "ShortRange": (16, 14), "LongRange": (0, 14),
    }),
    build_preset("35_strict_sr", "3.5 — Strictly balanced (SR)", {
        "Light": (29, 13), "Heavy": (11, 26), "Fast": (27, 14),
        "ShortRange": (9, 17), "LongRange": (1, 4),
    }),
    build_preset(
        "35_survival", "3.5 — Survival balanced",
        {
            "Light": (29, 29), "Heavy": (8, 29), "Fast": (29, 27),
            "ShortRange": (7, 0), "LongRange": (0, 1),
        },
        rm_overrides={
            "Light": (1, 11),
            "ShortRange": (6, 4),
            "LongRange": (9, 2),
        },
    ),
]
