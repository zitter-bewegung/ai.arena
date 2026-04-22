"""Command-line entry point: argparse, preset selection, and the main loop."""

import argparse
import os
import sys

from .aggregator import collect_occupancy
from .client import ArenaClient
from .presets import PRESETS
from .rendering import render_figure
from .storage import save_json


def parse_args():
    default_outdir = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "heatmaps",
    )
    p = argparse.ArgumentParser(
        description="Generate unit-position heatmaps across unit-stat presets.",
    )
    p.add_argument("--games", type=int, default=1000, help="Games per preset (default: 1000).")
    p.add_argument("--base-url", default="http://localhost:5222",
                   help="Backend base URL (default: http://localhost:5222).")
    p.add_argument("--outdir", default=default_outdir, help="Output directory.")
    p.add_argument("--presets", nargs="+", default=None,
                   help="Subset of preset slugs to run. Default: all.")
    return p.parse_args()


def select_presets(slugs):
    """Return the Preset objects matching `slugs` (or all of them if None)."""
    by_slug = {p.slug: p for p in PRESETS}
    if not slugs:
        return PRESETS
    unknown = [s for s in slugs if s not in by_slug]
    if unknown:
        print(f"Unknown preset(s): {unknown}\nAvailable: {list(by_slug)}", file=sys.stderr)
        sys.exit(2)
    return [by_slug[s] for s in slugs]


def main():
    args = parse_args()
    presets = select_presets(args.presets)

    client = ArenaClient(args.base_url)
    try:
        client.ping()
    except Exception as e:
        print(f"Backend not reachable at {args.base_url}: {e}", file=sys.stderr)
        return 3

    for preset in presets:
        print(f"=== {preset.slug}: {preset.title} ===", flush=True)
        aggregator = collect_occupancy(client, preset, args.games)
        png_path = render_figure(preset, aggregator.heatmaps, args.games, args.outdir)
        json_path = save_json(preset, aggregator.heatmaps, args.games, args.outdir)
        print(f"  saved: {png_path}")
        print(f"         {json_path}", flush=True)

    return 0
