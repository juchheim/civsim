#!/usr/bin/env python3
"""Analyze GOV-11's paired conception-probability effect by independent match seed."""
from __future__ import annotations

import argparse
import json
import math
import random
import statistics
from collections import defaultdict
from pathlib import Path
from typing import Iterable

GROWTH_PREFIX = "wp7-gov11-growth "
TERMINAL_PREFIX = "wp7-match-terminal "
FIXED_SCALE = 1 << 32


def trace_files(paths: Iterable[str]) -> list[Path]:
    result: list[Path] = []
    for raw in paths:
        path = Path(raw)
        if path.is_file():
            result.append(path)
        elif path.is_dir():
            result.extend(p for p in sorted(path.rglob("*")) if p.is_file() and p.suffix.lower() in {".txt", ".log", ".out"})
        else:
            raise FileNotFoundError(path)
    return result


def read_expected(path: str) -> set[str]:
    seeds: set[str] = set()
    with Path(path).open(encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            value = line.strip()
            if not value or value.startswith("#"):
                continue
            if not value.isdigit():
                raise ValueError(f"{path}:{line_number}: expected one numeric seed per line")
            if value in seeds:
                raise ValueError(f"{path}:{line_number}: duplicate expected seed {value}")
            seeds.add(value)
    return seeds


def percentile(values: list[float], probability: float) -> float:
    position = (len(values) - 1) * probability
    lower, upper = math.floor(position), math.ceil(position)
    fraction = position - lower
    return values[lower] * (1.0 - fraction) + values[upper] * fraction


def analyze(paths: Iterable[str], expected_seeds_path: str, *, bootstrap_replicates: int = 10_000,
            bootstrap_seed: int = 20261127) -> dict[str, object]:
    expected = read_expected(expected_seeds_path)
    totals: dict[str, list[int]] = defaultdict(lambda: [0, 0, 0, 0])
    terminal_seeds: set[str] = set()
    seen_opportunities: set[tuple[str, str, str, str, str]] = set()
    opportunity_rows = 0
    excluded_zero_chance = 0

    for path in trace_files(paths):
        with path.open(encoding="utf-8", errors="replace") as stream:
            for line_number, line in enumerate(stream, 1):
                prefix = TERMINAL_PREFIX if line.startswith(TERMINAL_PREFIX) else GROWTH_PREFIX if line.startswith(GROWTH_PREFIX) else None
                if prefix is None:
                    continue
                fields = dict(token.split("=", 1) for token in line.split()[1:] if "=" in token)
                seed = fields.get("seed", "")
                if not seed or seed not in expected:
                    raise ValueError(f"{path}:{line_number}: missing or unexpected seed {seed!r}")
                if prefix == TERMINAL_PREFIX:
                    if seed in terminal_seeds:
                        raise ValueError(f"{path}:{line_number}: duplicate terminal row for seed {seed}")
                    terminal_seeds.add(seed)
                    continue
                if fields.get("gov11") != "true":
                    continue
                if fields.get("actualMode") != "treatment" or fields.get("pairedBasis") != "same_figure_same_state_same_keyed_roll":
                    raise ValueError(f"{path}:{line_number}: GOV-11 growth row lacks its paired treatment basis")
                key = tuple(fields.get(name, "") for name in ("seed", "season", "civ", "settlement", "figure"))
                if not all(key) or key in seen_opportunities:
                    raise ValueError(f"{path}:{line_number}: incomplete or duplicate conception opportunity {key}")
                seen_opportunities.add(key)
                try:
                    control = int(fields["controlConceptionChance"])
                    treatment = int(fields["treatmentConceptionChance"])
                except (KeyError, ValueError) as exc:
                    raise ValueError(f"{path}:{line_number}: invalid conception chance") from exc
                if control < 0 or treatment < control:
                    raise ValueError(f"{path}:{line_number}: GOV-11 did not preserve or increase conception chance")
                opportunity_rows += 1
                if control == 0:
                    if treatment != 0:
                        raise ValueError(f"{path}:{line_number}: zero control chance has nonzero treatment chance")
                    excluded_zero_chance += 1
                    continue
                totals[seed][0] += 1
                totals[seed][1] += control
                totals[seed][2] += treatment
                totals[seed][3] += int(fields.get("treatmentOutcome") == "conception") - int(fields.get("controlOutcome") == "conception")

    missing_terminal = expected - terminal_seeds
    unexpected_terminal = terminal_seeds - expected
    if missing_terminal or unexpected_terminal:
        raise ValueError(f"terminal output differs from expected seeds: missing={sorted(missing_terminal, key=int)} unexpected={sorted(unexpected_terminal, key=int)}")
    no_exposure = expected - totals.keys()
    per_seed = []
    for seed in sorted(totals, key=int):
        count, control_sum, treatment_sum, paired_outcome_delta = totals[seed]
        per_seed.append({
            "seed": int(seed),
            "opportunities": count,
            "control_expected_conceptions": control_sum / FIXED_SCALE,
            "treatment_expected_conceptions": treatment_sum / FIXED_SCALE,
            "paired_outcome_delta": paired_outcome_delta,
            "chance_ratio": treatment_sum / control_sum,
        })
    logs = [math.log(float(row["chance_ratio"])) for row in per_seed]
    estimate = math.exp(statistics.fmean(logs)) if logs else None
    ci: list[float] | None = None
    if len(logs) >= 2 and bootstrap_replicates > 0:
        rng = random.Random(bootstrap_seed)
        bootstrap = sorted(
            math.exp(statistics.fmean(logs[rng.randrange(len(logs))] for _ in logs))
            for _ in range(bootstrap_replicates)
        )
        ci = [percentile(bootstrap, 0.025), percentile(bootstrap, 0.975)]
    enough_exposure = len(logs) >= 20
    if not enough_exposure or estimate is None or ci is None:
        status = "inconclusive"
    elif 1.08 <= estimate <= 1.12 and ci[0] <= 1.10 <= ci[1]:
        status = "pass"
    else:
        status = "fail"
    return {
        "status": status,
        "effect_definition": "geometric mean of per-seed ratios of summed paired conception probabilities in GOV-11 City female-seasons",
        "effect_target": 1.10,
        "expected_seeds": len(expected),
        "observed_terminal_seeds": len(terminal_seeds),
        "seeds_with_exposure": len(logs),
        "seeds_without_exposure": sorted(no_exposure, key=int),
        "opportunity_rows": opportunity_rows,
        "nonzero_control_opportunities": sum(int(row["opportunities"]) for row in per_seed),
        "zero_control_chance_rows": excluded_zero_chance,
        "estimate": estimate,
        "match_clustered_bootstrap_ci95": ci,
        "gate": {"estimate_min": 1.08, "estimate_max": 1.12, "ci_must_include": 1.10, "minimum_exposed_seeds": 20},
        "per_seed": per_seed,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--traces", nargs="+", required=True, help="WP7 raw trace files or directories")
    parser.add_argument("--expected-seeds", required=True, help="frozen manifest with one numeric seed per line")
    parser.add_argument("--bootstrap-replicates", type=int, default=10_000)
    parser.add_argument("--bootstrap-seed", type=int, default=20261127)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = analyze(args.traces, args.expected_seeds, bootstrap_replicates=args.bootstrap_replicates,
                     bootstrap_seed=args.bootstrap_seed)
    rendered = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0 if result["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
