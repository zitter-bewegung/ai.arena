"""Human-readable JSON dump of raw heatmap counts + preset metadata."""

import json
import os

from .domain import BOARD, UNIT_TYPES


def _format_json(obj, indent=0):
    """Pretty-print JSON, but keep innermost numeric arrays on a single line.

    A 20×20 matrix becomes 20 lines of 20 numbers — readable in any editor.
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
        is_numeric_row = all(isinstance(x, (int, float)) and not isinstance(x, bool) for x in obj)
        if is_numeric_row:
            return json.dumps(obj)
        parts = [f"{inner_pad}{_format_json(v, indent + 1)}" for v in obj]
        return "[\n" + ",\n".join(parts) + f"\n{pad}]"

    return json.dumps(obj, ensure_ascii=False)


def save_json(preset, heatmaps, n_games, outdir, kind):
    """Dump raw counts and metadata into outdir/<kind>/<slug>.json."""
    target_dir = os.path.join(outdir, kind)
    os.makedirs(target_dir, exist_ok=True)
    json_path = os.path.join(target_dir, f"{preset.slug}.json")
    payload = {
        "slug": preset.slug,
        "title": preset.title,
        "kind": kind,
        "n_games": n_games,
        "board": BOARD,
        "unit_types": list(UNIT_TYPES),
        "stats": preset.stats,
        "heatmaps": {ut: heatmaps[ut].tolist() for ut in UNIT_TYPES},
    }
    with open(json_path, "w") as f:
        f.write(_format_json(payload))
        f.write("\n")
    return json_path
