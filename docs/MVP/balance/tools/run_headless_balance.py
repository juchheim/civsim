"""Run the staged E0–E2 living-economy balance harness.

Example:
    uv run --no-project --with numpy python run_headless_balance.py \
      --harness ../../../../src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll \
      --pack /path/to/economy.pack --seeds 1,2,3,4,5,6,7,8,23,100

The C1 cap schedule comes from params.py. C3 mechanism projects use the positions
specified in 07c §5; p=0.5 picks are represented by a seed-rotated pairing of two
civilizations, keeping the fraction exact in each four-civilization run.
"""
from __future__ import annotations

import argparse
import concurrent.futures
import subprocess
from pathlib import Path

import params as P


MILESTONE_TICKS = (0, 4650, 10650, 17700)
ARCHETYPES = "Balanced,Grower,Builder,Wanderer"
C3_PROJECTS = (
    ("FIR-01", 0, 0.50, False),
    ("FIR-02", 0, 0.50, False),
    ("COM-01", 0, 0.50, False),
    ("SOC-02", 0, 0.50, True),
    ("DEF-01", 0, 0.50, True),
    ("FIB-02", 1, 0.15, False),
    ("HNT-03", 1, 0.45, False),
    ("FIB-04", 1, 0.50, True),
    ("HNT-05", 1, 0.50, True),
    ("HUS-01", 1, 0.50, True),
    ("MED-01", 1, 0.50, True),
    ("MED-02", 1, 0.50, True),
    ("WAY-02", 1, 0.50, False),
    ("BLD-03", 1, 0.55, False),
    ("GOV-01", 2, 0.15, False),
    ("CHM-03", 2, 0.50, True),
    ("MED-03", 2, 0.50, True),
    ("GOV-03", 2, 0.50, True),
)
KEYSTONES = (("KEY-1", 4650), ("KEY-2", 10650), ("KEY-3", 17700))
C3_MIDPOINT_WINDOWS = ((2400, 2850), (7800, 8250), (14250, 14700))
SEASON_TICKS = 150


def staged_tick(era: int, position: float) -> int:
    return MILESTONE_TICKS[era] + int(
        (MILESTONE_TICKS[era + 1] - MILESTONE_TICKS[era]) * position + 0.5
    )


def build_events(include_c3_projects: bool = True, isolate_c3_midpoints: bool = False) -> tuple[dict[str, tuple[int, int, str]], tuple[str, ...]]:
    """Return unique project completions and the optional p=.5 project IDs."""
    events: dict[str, tuple[int, int, str]] = {}
    for order, (project_id, _name, era, position, *_rest) in enumerate(P.CAPS):
        if era <= 2:
            events.setdefault(project_id, (staged_tick(era, position), order, project_id))

    optional: list[str] = []
    if include_c3_projects:
        c3_projects = C3_PROJECTS
    else:
        # FIR-01/02 are part of the canonical C4 cap schedule as well.
        c3_projects = (("FIR-01", 0, 0.50, False), ("FIR-02", 0, 0.50, False))
    for order, (project_id, era, position, is_optional) in enumerate(c3_projects, start=100):
        candidate = (staged_tick(era, position), order, project_id)
        previous = events.get(project_id)
        if previous is not None and previous[0] != candidate[0]:
            raise ValueError(f"Conflicting staged completion for {project_id}: {previous} vs {candidate}")
        events.setdefault(project_id, candidate)
        if is_optional:
            optional.append(project_id)

    for order, (project_id, tick) in enumerate(KEYSTONES, start=300):
        events[project_id] = (tick, order, project_id)

    if isolate_c3_midpoints:
        # E1 has BLD-03 at .55 and FOR-04 at .60 inside the four-sample window.
        # Hold those later-position cap projects until the window has ended so all
        # observations represent the complete .50 midpoint state.
        e1_window_end = C3_MIDPOINT_WINDOWS[1][1]
        for project_id, _name, era, position, *_rest in P.CAPS:
            if era != 1 or position <= 0.50:
                continue
            current = events.get(project_id)
            if current is not None and current[0] <= e1_window_end:
                events[project_id] = (e1_window_end + SEASON_TICKS, current[1], project_id)
    return events, tuple(optional)


def run_seed(seed: int, harness: Path, pack: Path, output_dir: Path,
             ticks: int, report_c3: bool, include_c3_projects: bool) -> tuple[int, int, Path, str]:
    events, optional_ids = build_events(include_c3_projects, isolate_c3_midpoints=report_c3)
    command = [
        "dotnet", str(harness), "economy", "--pack", str(pack), "--seed", str(seed),
        "--ticks", str(ticks), "--archetypes", ARCHETYPES, "--report-balance-reference",
    ]
    if report_c3:
        command.append("--report-c3-midpoints")

    assignments: list[str] = []
    for civ in range(4):
        for optional_index, project_id in enumerate(optional_ids):
            selected = {(seed + optional_index) % 4, (seed + optional_index + 2) % 4}
            if civ in selected:
                tick, _order, _project = events[project_id]
                assignments.append(f"{tick}:{civ}:{project_id}")
        for tick, _order, project_id in sorted(events.values()):
            if project_id not in optional_ids:
                assignments.append(f"{tick}:{civ}:{project_id}")
    for assignment in assignments:
        command.extend(("--complete", assignment))

    output_dir.mkdir(parents=True, exist_ok=True)
    log_path = output_dir / f"balance-seed-{seed}.log"
    with log_path.open("w") as log:
        log.write(
            f"# seed={seed} ticks={ticks} archetypes={ARCHETYPES} "
            f"uniqueProjects={len(events)} commandCount={len(assignments)} "
            f"optionalPairing=seed-rotated-two-of-four c3Projects={include_c3_projects} "
            f"c3Report={report_c3} midpointIsolated={report_c3}\n"
        )
        log.flush()
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=False)

    lines = log_path.read_text().splitlines()
    final_rows = [line for line in lines if f"checkpoint=annual tick={ticks}" in line and "firstFissionTick=" in line]
    summaries = []
    for line in final_rows:
        fields = dict(part.split("=", 1) for part in line.split() if "=" in part)
        summaries.append(f"{fields.get('archetype')} pop={fields.get('population')} fission={fields.get('firstFissionTick')}")
    summary = "; ".join(summaries) if summaries else "no final annual summary found"
    return seed, result.returncode, log_path, summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--harness", required=True, type=Path, help="built CivSim.Harness.dll")
    parser.add_argument("--pack", required=True, type=Path, help="compiled economy content pack")
    parser.add_argument("--seeds", default="1,2,3,4,5,6,7,8,23,100")
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--ticks", type=int, default=18000)
    parser.add_argument("--output-dir", type=Path, default=Path("headless-balance-logs"))
    parser.add_argument("--without-c3-report", action="store_true", help="skip midpoint hazard diagnostics")
    parser.add_argument("--without-c3-projects", action="store_true", help="run only the canonical C1/C4 project schedule")
    args = parser.parse_args()

    harness = args.harness.resolve()
    pack = args.pack.resolve()
    output_dir = args.output_dir.resolve()
    if not harness.is_file():
        parser.error(f"Harness DLL does not exist: {harness}")
    if not pack.is_file():
        parser.error(f"Content pack does not exist: {pack}")
    if args.workers < 1 or args.ticks < 1:
        parser.error("--workers and --ticks must be positive")
    if not args.without_c3_report and args.ticks < C3_MIDPOINT_WINDOWS[-1][1]:
        parser.error(f"--ticks must reach {C3_MIDPOINT_WINDOWS[-1][1]} to include all C3 midpoint windows")
    seeds = [int(value) for value in args.seeds.split(",") if value.strip()]
    if not seeds or len(set(seeds)) != len(seeds):
        parser.error("--seeds must contain one or more unique integers")

    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        results = pool.map(
            lambda seed: run_seed(seed, harness, pack, output_dir, args.ticks,
                                  not args.without_c3_report, not args.without_c3_projects),
            seeds,
        )
        failures = 0
        for seed, code, log_path, summary in sorted(results):
            print(f"seed={seed} exit={code} log={log_path} {summary}")
            failures += code != 0
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
