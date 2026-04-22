#!/usr/bin/env python3
"""Entry point — delegates to arena_heatmap.cli.main().

For the full pipeline and module breakdown, see arena_heatmap/__init__.py.
Usage:
    python heatmap_generator.py [--games N] [--presets SLUG...] [--no-mirror]
"""

import sys

from arena_heatmap.cli import main


if __name__ == "__main__":
    sys.exit(main())
