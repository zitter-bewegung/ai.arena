"""Paper-ready figure rendering (1x5 seaborn heatmaps per preset).

The Agg backend is activated in arena_heatmap/__init__.py, before any
submodule imports pyplot.
"""

import os
import string

import matplotlib.pyplot as plt
import numpy as np
import seaborn as sns

from .domain import BOARD, UNIT_TYPES


def _apply_paper_style():
    sns.set_context("paper", font_scale=1.0)
    sns.set_style("white")
    plt.rcParams.update({
        "font.family": "serif",
        "axes.titlesize": 11,
        "figure.titlesize": 12,
        "axes.labelsize": 9,
    })


def _draw_unit_heatmap(ax, counts, title):
    """Render one unit type's 20x20 occupancy distribution into `ax`."""
    data = counts.astype(np.float64)
    total = data.sum()
    if total > 0:
        data = data / total  # probability mass per cell

    sns.heatmap(
        data, ax=ax, cmap="rocket_r",
        cbar=True, cbar_kws={"shrink": 0.75, "format": "%.3f", "pad": 0.02},
        square=True,
        xticklabels=list(string.ascii_uppercase[:BOARD]),
        yticklabels=list(range(1, BOARD + 1)),
        linewidths=0, rasterized=True,
    )
    ax.invert_yaxis()  # y=1 at the bottom, matching chess convention
    ax.set_title(title)
    ax.set_xlabel("")
    ax.set_ylabel("")
    ax.tick_params(labelsize=6, length=0)
    for spine in ax.spines.values():
        spine.set_visible(True)
        spine.set_linewidth(0.4)


def render_figure(preset, heatmaps, n_games, outdir):
    """Save a 1×5 figure (one heatmap per unit type) as <slug>.png."""
    _apply_paper_style()
    fig, axes = plt.subplots(1, len(UNIT_TYPES), figsize=(16.5, 3.8), constrained_layout=True)
    for ax, ut in zip(axes, UNIT_TYPES):
        _draw_unit_heatmap(ax, heatmaps[ut], title=ut)

    fig.suptitle(f"{preset.title}   (N = {n_games} games)", y=1.02)

    os.makedirs(outdir, exist_ok=True)
    png_path = os.path.join(outdir, f"{preset.slug}.png")
    fig.savefig(png_path, dpi=300, bbox_inches="tight")
    plt.close(fig)
    return png_path
