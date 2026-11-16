#!/usr/bin/env python3
"""Analyze exact-snapshot, four-season GOV-11 City-year counterfactual rows."""
from __future__ import annotations

import argparse
import json
import math
import re
import random
import statistics
from collections import Counter, defaultdict
from pathlib import Path
from typing import Iterable

PAIR_PREFIX = "wp7-gov11-city-year-pair "
PAIR_EXCLUDED_PREFIX = "wp7-gov11-city-year-pair-excluded "
PAIR_CENSORED_PREFIX = "wp7-gov11-city-year-pair-censored "
MATCH_TERMINAL_PREFIX = "wp7-match-terminal "
KEY = ("seed", "year", "civStart", "settlement")


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


def read_pairs(paths: Iterable[str]) -> tuple[dict[tuple[str, ...], dict[str, str]], Counter[str], set[str]]:
    rows: dict[tuple[str, ...], dict[str, str]] = {}
    counts: Counter[str] = Counter()
    terminal_seeds: set[str] = set()
    for path in trace_files(paths):
        with path.open(encoding="utf-8", errors="replace") as stream:
            for line_number, line in enumerate(stream, 1):
                if line.startswith(PAIR_EXCLUDED_PREFIX):
                    fields = dict(token.split("=", 1) for token in line.split()[1:] if "=" in token)
                    counts["excluded:" + fields.get("exclusionReason", "unspecified")] += 1
                    continue
                if line.startswith(MATCH_TERMINAL_PREFIX):
                    fields = dict(token.split("=", 1) for token in line.split()[1:] if "=" in token)
                    seed = fields.get("seed", "")
                    if not seed:
                        raise ValueError(f"{path}:{line_number}: terminal row missing seed")
                    if seed in terminal_seeds:
                        raise ValueError(f"{path}:{line_number}: duplicate match terminal row for seed {seed}")
                    terminal_seeds.add(seed)
                    counts["match_terminal_rows"] += 1
                    continue
                if line.startswith(PAIR_CENSORED_PREFIX):
                    counts["censored"] += 1
                    continue
                if not line.startswith(PAIR_PREFIX):
                    continue
                fields = dict(token.split("=", 1) for token in line.split()[1:] if "=" in token)
                treatment_checksum = fields.get("treatmentStartChecksum", "")
                control_checksum = fields.get("controlStartChecksum", "")
                if (not re.fullmatch(r"[0-9a-fA-F]{16}", treatment_checksum) or
                        not re.fullmatch(r"[0-9a-fA-F]{16}", control_checksum) or
                        fields.get("sameStartingState") != "true" or treatment_checksum != control_checksum):
                    raise ValueError(f"{path}:{line_number}: paired row does not prove a shared starting checksum")
                if fields.get("treatmentEligibleAllYear") != "true" or fields.get("controlEligibleAllYear") != "true":
                    raise ValueError(f"{path}:{line_number}: eligible pair row has an ineligible branch")
                if fields.get("treatmentEligibleAtStart") != "true" or fields.get("controlEligibleAtStart") != "true":
                    raise ValueError(f"{path}:{line_number}: pair does not begin with both branches eligible")
                if fields.get("treatmentGov11Start") != "true" or fields.get("controlGov11Start") != "true":
                    raise ValueError(f"{path}:{line_number}: pair does not begin with GOV-11 in both branches")
                if fields.get("treatmentGov11End") != "true" or fields.get("controlGov11End") != "true":
                    raise ValueError(f"{path}:{line_number}: pair lost GOV-11 charter eligibility")
                if fields.get("treatmentLiveEnd") != "true" or fields.get("controlLiveEnd") != "true":
                    raise ValueError(f"{path}:{line_number}: settlement loss must be emitted as an excluded row")
                if fields.get("observedSeasons") != "4" or fields.get("pairedBasis") != "same_snapshot_four_season_branch":
                    raise ValueError(f"{path}:{line_number}: row is not a full four-season same-snapshot pair")
                key = tuple(fields.get(name, "") for name in KEY)
                if not all(key):
                    raise ValueError(f"{path}:{line_number}: missing city-year pairing key")
                if key in rows:
                    raise ValueError(f"{path}:{line_number}: duplicate city-year pair key {key}")
                try:
                    starts = (int(fields["treatmentPopulationStart"]), int(fields["controlPopulationStart"]))
                    ends = (int(fields["treatmentPopulationEnd"]), int(fields["controlPopulationEnd"]))
                except (KeyError, ValueError) as exc:
                    raise ValueError(f"{path}:{line_number}: invalid paired population endpoints") from exc
                if starts[0] <= 0 or ends[0] <= 0 or ends[1] <= 0 or starts[0] != starts[1]:
                    raise ValueError(f"{path}:{line_number}: invalid or unequal paired start population")
                fields["_source"] = str(path)
                rows[key] = fields
                counts["eligible_paired_city_years"] += 1
    return rows, counts, terminal_seeds


def percentile(sorted_values: list[float], probability: float) -> float:
    if not sorted_values:
        raise ValueError("percentile needs values")
    position = (len(sorted_values) - 1) * probability
    lower = int(math.floor(position))
    upper = int(math.ceil(position))
    fraction = position - lower
    return sorted_values[lower] * (1.0 - fraction) + sorted_values[upper] * fraction


def analyze(paths: Iterable[str], *, bootstrap_replicates: int = 10_000,
            bootstrap_seed: int = 20261116,
            expected_seeds_path: str | None = None) -> dict[str, object]:
    rows, counts, terminal_seeds = read_pairs(paths)
    pair_seeds = {key[0] for key in rows}
    if expected_seeds_path is not None:
        expected_seeds = set()
        with Path(expected_seeds_path).open(encoding="utf-8") as stream:
            for line_number, line in enumerate(stream, 1):
                value = line.strip()
                if not value or value.startswith("#"):
                    continue
                if not value.isdigit():
                    raise ValueError(f"{expected_seeds_path}:{line_number}: expected one numeric seed per line")
                if value in expected_seeds:
                    raise ValueError(f"{expected_seeds_path}:{line_number}: duplicate expected seed {value}")
                expected_seeds.add(value)
        missing_terminal = expected_seeds - terminal_seeds
        unexpected_terminal = terminal_seeds - expected_seeds
        if missing_terminal or unexpected_terminal:
            raise ValueError("terminal output does not match expected seed manifest: "
                             f"missing={sorted(missing_terminal, key=int)} "
                             f"unexpected={sorted(unexpected_terminal, key=int)}")
    else:
        expected_seeds = terminal_seeds.copy()
    seeds_without_pairs = expected_seeds - pair_seeds
    unexpected_pair_seeds = pair_seeds - expected_seeds
    if unexpected_pair_seeds:
        raise ValueError(f"paired City-year rows have no expected terminal seed: {sorted(unexpected_pair_seeds, key=int)}")
    by_seed: dict[str, list[float]] = defaultdict(list)
    pairs: list[dict[str, object]] = []
    for key in sorted(rows):
        row = rows[key]
        tr_start = int(row["treatmentPopulationStart"])
        tr_end = int(row["treatmentPopulationEnd"])
        co_start = int(row["controlPopulationStart"])
        co_end = int(row["controlPopulationEnd"])
        ratio = (tr_end / tr_start) / (co_end / co_start)
        by_seed[key[0]].append(math.log(ratio))
        pairs.append({
            "seed": int(key[0]), "year": int(key[1]), "civ": int(key[2]), "settlement": int(key[3]),
            "population_start": tr_start, "treatment_end": tr_end, "control_end": co_end,
            "treatment_control_population_factor_ratio": ratio,
            "starting_state_checksum": row["treatmentStartChecksum"],
        })
    world_log_effects = [statistics.fmean(values) for values in by_seed.values()]
    estimate = math.exp(statistics.fmean(world_log_effects)) if world_log_effects else None
    ci: list[float] | None = None
    if len(world_log_effects) >= 2 and bootstrap_replicates > 0:
        rng = random.Random(bootstrap_seed)
        bootstrap = []
        for _ in range(bootstrap_replicates):
            draw = [world_log_effects[rng.randrange(len(world_log_effects))] for _ in world_log_effects]
            bootstrap.append(math.exp(statistics.fmean(draw)))
        bootstrap.sort()
        ci = [percentile(bootstrap, 0.025), percentile(bootstrap, 0.975)]
    enough_exposure = len(world_log_effects) >= 20
    if not enough_exposure or estimate is None or ci is None:
        status = "inconclusive"
    elif 1.08 <= estimate <= 1.12 and ci[0] <= 1.10 <= ci[1]:
        status = "pass"
    else:
        status = "fail"
    return {
        "status": status,
        "effect_definition": "geometric mean of per-seed mean log[(treatment population end/start)/(control population end/start)]",
        "pairing_basis": "same complete MatchModel checksum at calendar-year start; both branches advanced four seasons",
        "pairing_key": list(KEY),
        "paired_city_years": len(pairs),
        "seed_accounting": {
            "expected_seeds": len(expected_seeds),
            "observed_terminal_seeds": len(terminal_seeds),
            "missing_terminal_seeds": sorted(expected_seeds - terminal_seeds, key=int),
            "seeds_with_eligible_pairs": len(pair_seeds),
            "seeds_without_eligible_pairs": sorted(seeds_without_pairs, key=int),
        },
        "independent_match_seeds": len(world_log_effects),
        "effect_estimate": estimate,
        "match_clustered_bootstrap_ci95": ci,
        "gate": {"estimate_min": 1.08, "estimate_max": 1.12, "ci_must_include": 1.10, "minimum_independent_match_seeds": 20},
        "inconclusive_reason": None if enough_exposure and ci is not None else "fewer_than_20_independent_match_seeds_for_clustered_estimate",
        "trace_counts": dict(counts),
        "pairs": pairs,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--traces", nargs="+", required=True, help="trace files or directories produced by --fief-wp7-paired")
    parser.add_argument("--bootstrap-replicates", type=int, default=10_000)
    parser.add_argument("--bootstrap-seed", type=int, default=20261116)
    parser.add_argument("--expected-seeds", help="frozen manifest with one numeric seed per line; requires exactly one match terminal row per seed")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = analyze(args.traces, bootstrap_replicates=args.bootstrap_replicates,
                     bootstrap_seed=args.bootstrap_seed, expected_seeds_path=args.expected_seeds)
    serialized = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(serialized, encoding="utf-8")
    print(serialized, end="")


if __name__ == "__main__":
    main()
