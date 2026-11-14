#!/usr/bin/env python3
"""Select a fixed, unscreened F-T4 natural-start holdout cohort."""

from __future__ import annotations

import argparse
import csv
import random
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed-first", type=int, default=20270001)
    parser.add_argument("--seed-pool", type=int, default=10000)
    parser.add_argument("--pairs", type=int, default=80)
    parser.add_argument("--selection-seed", type=int, default=20261114)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.seed_pool < args.pairs or args.pairs < 1:
        parser.error("--seed-pool must be at least --pairs, and --pairs must be positive")

    rng = random.Random(args.selection_seed)
    seeds = sorted(rng.sample(range(args.seed_first, args.seed_first + args.seed_pool), args.pairs))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", newline="") as handle:
        handle.write(f"# F-T4 unscreened holdout: seed pool {args.seed_first}..{args.seed_first + args.seed_pool - 1}; "
                     f"{args.pairs} worlds sampled without replacement; selection seed {args.selection_seed}; "
                     "focus seat drawn uniformly from 0..3.\n")
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("seed", "focusCiv"))
        for seed in seeds:
            writer.writerow((seed, rng.randrange(4)))
    print(f"selected {len(seeds)} worlds from {args.seed_first}..{args.seed_first + args.seed_pool - 1} into {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
