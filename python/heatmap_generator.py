#!/usr/bin/env python3
"""Generate per-unit-type occupancy heatmaps across all unit-stat presets.

For each preset in PRESETS:
  1. POST the stats to /unit-stats to activate the preset in the running backend.
  2. Run N random games via POST /BattleCalculator/calculate-random-team.
  3. Replay each BattleResult.Actions log to reconstruct per-turn unit positions.
  4. Aggregate turn-cell occupancy per UnitType and render a 1x5 figure.

Output per preset in --outdir (default: python/heatmaps/):
  <slug>.png  — 1x5 figure at 300 DPI.
  <slug>.json — raw occupancy counts (one 20x20 int matrix per UnitType) plus
                metadata (title, n_games, mirror, stats payload). Human-readable:
                each row of the 20x20 matrices sits on its own line. Load with
                json.load(open(path)); heatmaps are under the "heatmaps" key.
"""

import argparse
import json
import os
import string
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed

import matplotlib
matplotlib.use("Agg")  # headless; avoids Tk/main-thread issues when mixing with ThreadPoolExecutor.
import matplotlib.pyplot as plt  # noqa: E402
import numpy as np  # noqa: E402
import requests  # noqa: E402
import seaborn as sns  # noqa: E402
import urllib3  # noqa: E402
from tqdm import tqdm  # noqa: E402

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

BOARD = 20
UNIT_TYPES = ["Light", "Heavy", "Fast", "ShortRange", "LongRange"]
TYPE_INT = {"Light": 0, "Heavy": 1, "Fast": 2, "ShortRange": 3, "LongRange": 4}

# Standard range/movement for most presets (UnitFactory.cs).
_DEFAULT_RM = {
    "Light":      (1, 12),
    "Heavy":      (1, 5),
    "Fast":       (1, 17),
    "ShortRange": (7, 5),
    "LongRange":  (17, 2),
}


def _stats(attack_defence, rm_overrides=None):
    """Build a /unit-stats payload from {unit: (atk, def)} plus optional (range, move) overrides."""
    rm = dict(_DEFAULT_RM)
    if rm_overrides:
        rm.update(rm_overrides)
    out = {}
    for ut in UNIT_TYPES:
        atk, dfn = attack_defence[ut]
        r, m = rm[ut]
        out[ut] = {
            "type": TYPE_INT[ut],
            "attack": atk,
            "defence": dfn,
            "range": r,
            "movement": m,
        }
    return out


# (slug, display title, payload). Copied verbatim from Arena.AI.Core/Logic/UnitFactory.cs.
PRESETS = [
    ("25_wb", "2.5 — Winning-contribution balanced", _stats({
        "Light": (27, 23), "Heavy": (27, 30), "Fast": (28, 25),
        "ShortRange": (30, 3), "LongRange": (5, 9),
    })),
    ("25_sr", "2.5 — Semi-random balanced", _stats({
        "Light": (0, 29), "Heavy": (30, 23), "Fast": (0, 30),
        "ShortRange": (17, 1), "LongRange": (1, 8),
    })),
    ("25_global_v1", "2.5 — Globally balanced (v1)", _stats({
        "Light": (8, 21), "Heavy": (5, 30), "Fast": (27, 0),
        "ShortRange": (29, 10), "LongRange": (5, 0),
    })),
    ("25_global_v2", "2.5 — Globally balanced (v2)", _stats({
        "Light": (1, 26), "Heavy": (30, 14), "Fast": (10, 12),
        "ShortRange": (30, 4), "LongRange": (0, 3),
    })),
    ("25_strict", "2.5 — Strictly balanced", _stats({
        "Light": (25, 25), "Heavy": (23, 30), "Fast": (17, 27),
        "ShortRange": (23, 29), "LongRange": (5, 30),
    })),
    ("35_wb", "3.5 — Winning-contribution balanced (active)", _stats({
        "Light": (22, 21), "Heavy": (12, 29), "Fast": (30, 16),
        "ShortRange": (30, 1), "LongRange": (9, 11),
    })),
    ("35_global", "3.5 — Globally balanced", _stats({
        "Light": (18, 9), "Heavy": (5, 25), "Fast": (7, 16),
        "ShortRange": (30, 2), "LongRange": (0, 19),
    })),
    ("35_global_sr", "3.5 — Globally balanced (SR)", _stats({
        "Light": (23, 24), "Heavy": (29, 23), "Fast": (24, 21),
        "ShortRange": (28, 0), "LongRange": (0, 28),
    })),
    ("35_strict", "3.5 — Strictly balanced", _stats({
        "Light": (21, 0), "Heavy": (3, 17), "Fast": (16, 3),
        "ShortRange": (16, 14), "LongRange": (0, 14),
    })),
    ("35_strict_sr", "3.5 — Strictly balanced (SR)", _stats({
        "Light": (29, 13), "Heavy": (11, 26), "Fast": (27, 14),
        "ShortRange": (9, 17), "LongRange": (1, 4),
    })),
    # BBB overrides Light movement and the two ranged units' range/move.
    ("35_survival", "3.5 — Survival balanced", _stats(
        {
            "Light": (29, 29), "Heavy": (8, 29), "Fast": (29, 27),
            "ShortRange": (7, 0), "LongRange": (0, 1),
        },
        rm_overrides={
            "Light": (1, 11),
            "ShortRange": (6, 4),
            "LongRange": (9, 2),
        },
    )),
]


def parse_cell(s):
    """Chess notation -> 1-indexed (x, y). 'B5' -> (2, 5)."""
    return ord(s[0].upper()) - ord("A") + 1, int(s[1:])


def post_preset(base_url, session, stats):
    r = session.post(f"{base_url}/unit-stats", json=stats, verify=False, timeout=15)
    r.raise_for_status()


def run_game(base_url, session):
    r = session.post(
        f"{base_url}/BattleCalculator/calculate-random-team",
        verify=False,
        timeout=60,
    )
    r.raise_for_status()
    return r.json()


def _is_team_a(unit_name):
    return unit_name.lower().startswith("teama")


def replay_into(actions, heatmaps, mirror):
    """Replay one game's action log; increment heatmaps on each action step."""
    # unit_name -> (unit_type, x, y, is_team_a)
    live = {}
    for act in actions:
        atype = act.get("actionType")
        name = act.get("unitName")
        utype = act.get("unitType")
        dest = act.get("destination")

        if atype == "Appears" and dest:
            x, y = parse_cell(dest)
            live[name] = (utype, x, y, _is_team_a(name))
        elif atype == "Moves" and dest and name in live:
            _, _, _, ta = live[name]
            x, y = parse_cell(dest)
            live[name] = (utype, x, y, ta)
        elif atype == "Dies":
            live.pop(name, None)
        # Attacks / LosesHealth / SkipsTurn: no position change.

        # Snapshot every live unit's position after this action step.
        for ut, x, y, ta in live.values():
            xp = (BOARD + 1 - x) if (mirror and not ta) else x
            if 1 <= xp <= BOARD and 1 <= y <= BOARD and ut in heatmaps:
                heatmaps[ut][y - 1, xp - 1] += 1


def aggregate(base_url, session, stats, n_games, mirror, desc, workers):
    """Run n_games in parallel (HTTP only); replay sequentially on the main thread.

    Worker threads issue POSTs concurrently — ASP.NET Core handles them on its
    own thread pool, so backend throughput scales with `workers` up to the
    server's core count. The replay side mutates shared numpy arrays, so it
    stays single-threaded: results are consumed as they complete.
    """
    post_preset(base_url, session, stats)
    heatmaps = {ut: np.zeros((BOARD, BOARD), dtype=np.int64) for ut in UNIT_TYPES}

    if workers <= 1:
        for _ in tqdm(range(n_games), desc=desc, leave=False):
            result = run_game(base_url, session)
            replay_into(result.get("actions", []), heatmaps, mirror)
        return heatmaps

    with ThreadPoolExecutor(max_workers=workers) as pool:
        futures = [pool.submit(run_game, base_url, session) for _ in range(n_games)]
        for fut in tqdm(as_completed(futures), total=n_games, desc=desc, leave=False):
            result = fut.result()
            replay_into(result.get("actions", []), heatmaps, mirror)
    return heatmaps


def render(slug, title, heatmaps, outdir, n_games, mirror):
    sns.set_context("paper", font_scale=1.0)
    sns.set_style("white")
    plt.rcParams.update({
        "font.family": "serif",
        "axes.titlesize": 11,
        "figure.titlesize": 12,
        "axes.labelsize": 9,
    })

    fig, axes = plt.subplots(1, 5, figsize=(16.5, 3.8), constrained_layout=True)

    xticklabels = list(string.ascii_uppercase[:BOARD])
    yticklabels = list(range(1, BOARD + 1))

    for ax, ut in zip(axes, UNIT_TYPES):
        data = heatmaps[ut].astype(np.float64)
        total = data.sum()
        if total > 0:
            data = data / total  # probability mass per cell
        sns.heatmap(
            data,
            ax=ax,
            cmap="rocket_r",
            cbar=True,
            cbar_kws={"shrink": 0.75, "format": "%.3f", "pad": 0.02},
            square=True,
            xticklabels=xticklabels,
            yticklabels=yticklabels,
            linewidths=0,
            rasterized=True,
        )
        ax.invert_yaxis()  # y=1 at the bottom, matching chess convention
        ax.set_title(ut)
        ax.set_xlabel("")
        ax.set_ylabel("")
        ax.tick_params(labelsize=6, length=0)
        for spine in ax.spines.values():
            spine.set_visible(True)
            spine.set_linewidth(0.4)

    mirror_note = "Team B mirrored" if mirror else "teams un-mirrored"
    fig.suptitle(f"{title}   (N = {n_games} games, {mirror_note})", y=1.02)

    os.makedirs(outdir, exist_ok=True)
    png_path = os.path.join(outdir, f"{slug}.png")
    fig.savefig(png_path, dpi=300, bbox_inches="tight")
    plt.close(fig)
    return png_path


def _format_json(obj, indent=0):
    """Pretty-print JSON with innermost numeric arrays kept on a single line.

    Produces a 20x20 matrix as 20 lines of 20 numbers — readable in any editor.
    """
    pad = "  " * indent
    inner_pad = "  " * (indent + 1)
    if isinstance(obj, dict):
        if not obj:
            return "{}"
        parts = [
            f"{inner_pad}{json.dumps(k, ensure_ascii=False)}: {_format_json(v, indent + 1)}"
            for k, v in obj.items()
        ]
        return "{\n" + ",\n".join(parts) + f"\n{pad}}}"
    if isinstance(obj, list):
        if not obj:
            return "[]"
        if all(isinstance(x, (int, float)) and not isinstance(x, bool) for x in obj):
            return json.dumps(obj)  # inner row of numbers — collapse to one line
        parts = [f"{inner_pad}{_format_json(v, indent + 1)}" for v in obj]
        return "[\n" + ",\n".join(parts) + f"\n{pad}]"
    return json.dumps(obj, ensure_ascii=False)


def save_data(slug, title, heatmaps, stats, outdir, n_games, mirror):
    """Persist raw counts + metadata as human-readable JSON."""
    os.makedirs(outdir, exist_ok=True)
    json_path = os.path.join(outdir, f"{slug}.json")
    payload = {
        "slug": slug,
        "title": title,
        "n_games": n_games,
        "mirror": mirror,
        "board": BOARD,
        "unit_types": UNIT_TYPES,
        "stats": stats,
        "heatmaps": {ut: heatmaps[ut].tolist() for ut in UNIT_TYPES},
    }
    with open(json_path, "w") as f:
        f.write(_format_json(payload))
        f.write("\n")
    return json_path


def main():
    parser = argparse.ArgumentParser(
        description="Generate unit-position heatmaps across unit-stat presets.",
    )
    parser.add_argument("--games", type=int, default=1000, help="Games per preset (default: 1000).")
    parser.add_argument("--base-url", default="http://localhost:5222",
                        help="Backend base URL (default: http://localhost:5222).")
    parser.add_argument(
        "--outdir",
        default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "heatmaps"),
        help="Output directory.",
    )
    parser.add_argument("--presets", nargs="+", default=None,
                        help="Subset of preset slugs to run. Default: all.")
    parser.add_argument("--no-mirror", action="store_true",
                        help="Do not mirror Team B across the vertical centerline.")
    parser.add_argument("--workers", type=int, default=8,
                        help="Concurrent in-flight games per preset (default: 8). Use 1 for sequential.")
    args = parser.parse_args()

    slugs_available = [p[0] for p in PRESETS]
    if args.presets:
        unknown = [s for s in args.presets if s not in slugs_available]
        if unknown:
            print(f"Unknown preset(s): {unknown}\nAvailable: {slugs_available}", file=sys.stderr)
            return 2
        selected = [p for p in PRESETS if p[0] in args.presets]
    else:
        selected = PRESETS

    session = requests.Session()
    # Size the urllib3 connection pool to match concurrency so we don't queue behind the default pool of 10.
    pool_size = max(args.workers * 2, 10)
    adapter = requests.adapters.HTTPAdapter(pool_connections=pool_size, pool_maxsize=pool_size)
    session.mount("http://", adapter)
    session.mount("https://", adapter)
    mirror = not args.no_mirror

    # Quick reachability check so we fail fast rather than mid-loop.
    try:
        r = session.get(f"{args.base_url}/unit-stats", verify=False, timeout=5)
        r.raise_for_status()
    except Exception as e:
        print(f"Backend not reachable at {args.base_url}: {e}", file=sys.stderr)
        return 3

    for slug, title, stats in selected:
        desc = f"{slug}"
        print(f"=== {slug}: {title} ===", flush=True)
        heatmaps = aggregate(args.base_url, session, stats, args.games, mirror, desc, args.workers)
        png_path = render(slug, title, heatmaps, args.outdir, args.games, mirror)
        npz_path = save_data(slug, title, heatmaps, stats, args.outdir, args.games, mirror)
        print(f"  saved: {png_path}")
        print(f"         {npz_path}", flush=True)

    return 0


if __name__ == "__main__":
    sys.exit(main())
