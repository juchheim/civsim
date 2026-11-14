#!/usr/bin/env python3
"""Run the matched F-T4 cohort through a long Fief horizon.

The cohort input files are prior filtered iron-match outputs with one focus
civilization per seed. This runner replays both AGR-11 treatment arms with the
same world, promoted pack and focus seat, retaining only FIEF_LAND_* records.
"""

from __future__ import annotations

import argparse
import shlex
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path


ARMS = ("agr11", "control_no_agr11")


def fields(line: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for part in shlex.split(line)[1:]:
        key, separator, value = part.partition("=")
        if separator:
            result[key] = value
    return result


def load_cohort(paths: list[Path]) -> dict[int, int]:
    cohort: dict[int, int] = {}
    arm_focus: dict[tuple[int, str], int] = {}
    for path in paths:
        for line in path.read_text().splitlines():
            if not line.startswith("FIEF_LAND_MANIFEST "):
                continue
            row = fields(line)
            if row["focusCiv"] == "all":
                continue
            seed, focus = int(row["seed"]), int(row["focusCiv"])
            key = (seed, row["arm"])
            if key in arm_focus and arm_focus[key] != focus:
                raise ValueError(f"conflicting focus seat for seed={seed} arm={row['arm']}")
            arm_focus[key] = focus
            if seed in cohort and cohort[seed] != focus:
                raise ValueError(f"conflicting focus seats for seed={seed}")
            cohort[seed] = focus
    if not cohort:
        raise ValueError("cohort inputs contain no focused FIEF_LAND_MANIFEST rows")
    return cohort


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", required=True, type=Path, help="temporary promoted E5 content pack")
    parser.add_argument("--harness", type=Path,
                        default=Path("src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll"))
    parser.add_argument("--minutes", type=int, default=180)
    parser.add_argument("--workers", type=int, default=6)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("cohort", nargs="+", type=Path, help="prior focused cohort output files")
    args = parser.parse_args()
    if args.minutes < 130 or args.workers < 1:
        parser.error("--minutes must be at least 130 and --workers must be positive")
    if not args.pack.is_file() or not args.harness.is_file():
        parser.error("the promoted pack and built harness must exist")

    cohort = load_cohort(args.cohort)
    jobs = [(seed, focus, arm) for seed, focus in sorted(cohort.items()) for arm in ARMS]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    completed: set[tuple[int, str]] = set()
    if args.output.exists():
        for line in args.output.read_text().splitlines():
            if line.startswith("FIEF_LAND_MANIFEST "):
                row = fields(line)
                completed.add((int(row["seed"]), row["arm"]))
    pending = [job for job in jobs if (job[0], job[2]) not in completed]

    def run(job: tuple[int, int, str]) -> tuple[tuple[int, int, str], list[str], float]:
        seed, focus, arm = job
        command = ["dotnet", str(args.harness), "iron-match", "--pack", str(args.pack),
                   "--seed", str(seed), "--minutes", str(args.minutes), "--fief", "--grant-e5",
                   "--fief-land-long-horizon", "--fief-land-focus-civ", str(focus)]
        command.append("--fief-land-anchor" if arm == "agr11" else "--fief-land-control")
        started = time.monotonic()
        result = subprocess.run(command, capture_output=True, text=True, check=False)
        elapsed = time.monotonic() - started
        rows = [line for line in result.stdout.splitlines() if line.startswith("FIEF_LAND_")]
        if result.returncode != 0 or not any(line.startswith("FIEF_LAND_MANIFEST ") for line in rows):
            raise RuntimeError(f"seed={seed} arm={arm} failed (exit {result.returncode}):\n{result.stderr[-4000:]}\n{result.stdout[-1000:]}")
        manifest = fields(next(line for line in rows if line.startswith("FIEF_LAND_MANIFEST ")))
        if manifest["focusCiv"] != str(focus) or manifest["arm"] != arm:
            raise RuntimeError(f"seed={seed} arm={arm} returned an unexpected manifest")
        return job, rows, elapsed

    if not args.output.exists():
        args.output.write_text("# Filtered output from run_fief_land_long_horizon.py\n")
    print(f"cohort={len(cohort)} pairs; runs={len(jobs)}; already present={len(completed)}; workers={args.workers}; horizon={args.minutes} minutes", flush=True)
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = {executor.submit(run, job): job for job in pending}
        for count, future in enumerate(as_completed(futures), start=1):
            job, rows, elapsed = future.result()
            with args.output.open("a") as output:
                output.write("\n".join(rows) + "\n")
            completed.add((job[0], job[2]))
            print(f"completed {count}/{len(pending)} seed={job[0]} focus=c{job[1]} arm={job[2]} elapsed={elapsed:.1f}s", flush=True)

    if len(completed) != len(jobs):
        print(f"incomplete: {len(completed)}/{len(jobs)} arms written to {args.output}", file=sys.stderr)
        return 1
    print(f"complete: {args.output}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
