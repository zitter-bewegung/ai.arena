"""Library for generating unit-position heatmaps from Arena.AI battles.

Submodules:
  domain      — constants, Preset / UnitState dataclasses, coordinate helpers
  presets     — the catalog of 11 unit-stat presets copied from UnitFactory.cs
  client      — ArenaClient: thin HTTP wrapper over the Arena.AI backend
  aggregator  — OccupancyAggregator + collect_occupancy(): run games, fold into heatmaps
  rendering   — render_figure(): paper-ready 1x5 seaborn figure per preset
  storage     — save_json(): human-readable per-preset data dump
  cli         — argparse + main() used by the heatmap_generator.py entrypoint
"""

# Force the headless matplotlib backend before any submodule imports pyplot.
# We never display figures — only save them to disk — and the 'Agg' backend
# avoids Tk/main-thread issues and makes the library safe to import anywhere.
import matplotlib
matplotlib.use("Agg")
