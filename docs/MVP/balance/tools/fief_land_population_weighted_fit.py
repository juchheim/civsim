#!/usr/bin/env python3
"""Estimate a population-weighted AGR-11 capacity response and fit a diagnostic band."""

from __future__ import annotations

import argparse
import csv
import random
import shlex
from collections import defaultdict
from pathlib import Path


TICKS_PER_SEASON = 150


def fields(line: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for part in shlex.split(line)[1:]:
        key, separator, value = part.partition("=")
        if separator:
            result[key] = value
    return result


def weighted_mean(values: list[float], weights: list[float]) -> float:
    total = sum(weights)
    return sum(value * weight for value, weight in zip(values, weights, strict=True)) / total


def weighted_quantile(values: list[float], weights: list[float], quantile: float) -> float:
    pairs = sorted(zip(values, weights, strict=True))
    threshold = quantile * sum(weights)
    cumulative = 0.0
    for value, weight in pairs:
        cumulative += weight
        if cumulative >= threshold:
            return value
    return pairs[-1][0]


def bootstrap_mean(values: list[float], weights: list[float], draws: int, seed: int) -> tuple[float, float]:
    rng = random.Random(seed)
    count = len(values)
    estimates = []
    for _ in range(draws):
        indexes = [rng.randrange(count) for _ in range(count)]
        denominator = sum(weights[index] for index in indexes)
        estimates.append(sum(values[index] * weights[index] for index in indexes) / denominator)
    estimates.sort()
    return estimates[int(0.025 * (draws - 1))], estimates[int(0.975 * (draws - 1))]


def describe(values: list[float]) -> str:
    if not values:
        return "n=0"
    ordered = sorted(values)
    middle = len(ordered) // 2
    median = ordered[middle] if len(ordered) % 2 else (ordered[middle - 1] + ordered[middle]) / 2
    return (f"n={len(values)} mean={sum(values) / len(values):.4f} median={median:.4f} "
            f"range={ordered[0]:.4f}..{ordered[-1]:.4f}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--seats", type=Path, required=True,
                        help="the frozen seed/focus-seat TSV used to produce this output")
    parser.add_argument("--offset", type=int, default=12)
    parser.add_argument("--bootstrap-draws", type=int, default=10000)
    parser.add_argument("--bootstrap-seed", type=int, default=20261114)
    args = parser.parse_args()
    if args.offset < 0 or args.bootstrap_draws < 1:
        parser.error("--offset must be nonnegative and --bootstrap-draws must be positive")

    with args.seats.open(newline="") as handle:
        selection_lines = (line for line in handle if not line.lstrip().startswith("#"))
        selections = csv.DictReader(selection_lines, delimiter="\t")
        if selections.fieldnames != ["seed", "focusCiv"]:
            raise ValueError("seat list must have exactly the columns seed and focusCiv")
        seat_by_seed: dict[int, int] = {}
        for row in selections:
            seed, focus = int(row["seed"]), int(row["focusCiv"])
            if seed in seat_by_seed:
                raise ValueError(f"duplicate seed in seat list: {seed}")
            if not 0 <= focus < 4:
                raise ValueError(f"focusCiv outside 0..3 for seed={seed}: {focus}")
            seat_by_seed[seed] = focus
    if not seat_by_seed:
        raise ValueError("seat list has no seed rows")

    manifests: dict[tuple[int, str], dict[str, str]] = {}
    samples: dict[tuple[int, str, int, int], dict[str, str]] = {}
    starts: dict[tuple[int, int], dict[str, str]] = {}
    censored: set[tuple[int, str, int, int]] = set()
    no_anchor: set[tuple[int, str, int]] = set()
    for line in args.output.read_text().splitlines():
        if line.startswith("FIEF_LAND_MANIFEST "):
            row = fields(line)
            key = (int(row["seed"]), row["arm"])
            if key in manifests:
                raise ValueError(f"duplicate manifest {key}")
            manifests[key] = row
        elif line.startswith("FIEF_LAND_SAMPLE "):
            row = fields(line)
            key = (int(row["seed"]), row["arm"], int(row["civ"]), int(row["offsetSeasons"]))
            if key in samples:
                raise ValueError(f"duplicate sample {key}")
            samples[key] = row
        elif line.startswith("FIEF_LAND_START "):
            row = fields(line)
            starts[(int(row["seed"]), int(row["civ"]))] = row
        elif line.startswith("FIEF_LAND_CENSORED "):
            row = fields(line)
            if row["civ"] == row["focusCiv"]:
                if row.get("reason") == "no_key5_by_horizon":
                    no_anchor.add((int(row["seed"]), row["arm"], int(row["civ"])))
                for offset in row.get("missingOffsets", "").split(","):
                    if offset:
                        censored.add((int(row["seed"]), row["arm"], int(row["civ"]), int(offset)))

    seeds = sorted(seat_by_seed)
    manifest_seeds = {seed for seed, _ in manifests}
    unexpected = manifest_seeds - set(seeds)
    missing = set(seeds) - manifest_seeds
    if unexpected:
        raise ValueError(f"output contains seeds outside frozen seat list: {sorted(unexpected)[:8]}")
    if missing:
        raise ValueError(f"output is missing selected seed manifests: {sorted(missing)[:8]}")
    treatment, control = "agr11", "control_no_agr11"
    if any(arm not in (treatment, control) for _, arm in manifests):
        raise ValueError("output contains an unexpected treatment arm")
    if len(manifests) != len(seeds) * 2:
        raise ValueError("output must contain exactly one manifest for each selected seed and both arms")
    records: list[dict[str, float | str | int]] = []
    failed_pairs = []
    for seed in seeds:
        t_manifest = manifests.get((seed, treatment))
        c_manifest = manifests.get((seed, control))
        if t_manifest is None or c_manifest is None:
            raise ValueError(f"missing treatment manifest for selected seed={seed}")
        focus = seat_by_seed[seed]
        for row in (t_manifest, c_manifest):
            if int(row["focusCiv"]) != focus:
                raise ValueError(f"manifest focus seat differs from frozen selection for seed={seed}")
            if args.offset not in {int(value) for value in row["offsetSeasons"].split(",") if value}:
                raise ValueError(f"requested offset is absent from manifest for seed={seed}")
        for field in ("focusCiv", "packHash", "worldFingerprint", "matchStartChecksum", "minutes", "offsetSeasons"):
            if t_manifest[field] != c_manifest[field]:
                raise ValueError(f"matched manifest {field} differs for seed={seed}")
        t0 = samples.get((seed, treatment, focus, 0))
        c0 = samples.get((seed, control, focus, 0))
        t = samples.get((seed, treatment, focus, args.offset))
        c = samples.get((seed, control, focus, args.offset))
        if (seed, treatment, focus) in no_anchor or (seed, control, focus) in no_anchor:
            failed_pairs.append((seed, "focus seat never reached KEY-5 by horizon"))
            continue
        if None in (t0, c0, t, c):
            missing_offsets = []
            if t0 is None: missing_offsets.append((treatment, 0))
            if c0 is None: missing_offsets.append((control, 0))
            if t is None: missing_offsets.append((treatment, args.offset))
            if c is None: missing_offsets.append((control, args.offset))
            if not missing_offsets or any((seed, arm, focus, offset) not in censored for arm, offset in missing_offsets):
                raise ValueError(f"sample missing without matching censor record for seed={seed}")
            failed_pairs.append((seed, "missing focus anchor or target sample (censored)"))
            continue
        assert t0 is not None and c0 is not None and t is not None and c is not None
        if t0["stateChecksum"] != c0["stateChecksum"] or t0["tick"] != c0["tick"]:
            raise ValueError(f"offset-zero focus state differs for seed={seed}")
        for row, offset in ((t0, 0), (c0, 0), (t, args.offset), (c, args.offset)):
            if int(row["civ"]) != focus or int(row["focusCiv"]) != focus or int(row["offsetSeasons"]) != offset:
                raise ValueError(f"focus sample identity/offset mismatch for seed={seed}, offset={offset}")
        if (int(t["tick"]) != int(t0["tick"]) + args.offset * TICKS_PER_SEASON or
                int(c["tick"]) != int(c0["tick"]) + args.offset * TICKS_PER_SEASON or t["tick"] != c["tick"]):
            raise ValueError(f"target sample is not the requested +{args.offset}-season anchor for seed={seed}")
        for field in ("anchorSeason", "archetype", "tile", "tags"):
            if len({row[field] for row in (t0, c0, t, c)}) != 1:
                raise ValueError(f"matched focus sample {field} differs across arms/offsets for seed={seed}")
        if (seed, treatment, focus, args.offset) in censored or (seed, control, focus, args.offset) in censored:
            failed_pairs.append((seed, "target sample marked censored"))
            continue
        control_capacity = float(c["civSustainableCapacity"])
        treatment_capacity = float(t["civSustainableCapacity"])
        control_population = float(c["civPopulation"])
        base_population = float(c0["civPopulation"])
        control_food_cap = float(c["civFoodCap"])
        treatment_food_cap = float(t["civFoodCap"])
        if min(control_capacity, treatment_capacity, control_food_cap, treatment_food_cap,
               control_population, base_population) <= 0:
            failed_pairs.append((seed, "nonpositive population or capacity"))
            continue
        start = starts.get((seed, focus))
        if start is None:
            raise ValueError(f"selected start metadata is missing for seed={seed}, civ={focus}")
        for field in ("archetype", "tile", "tags"):
            if t0[field] != start[field]:
                raise ValueError(f"focus sample {field} differs from world start metadata for seed={seed}")
        records.append({
            "seed": seed,
            "focus": focus,
            "archetype": t["archetype"],
            "tags": start["tags"],
            "basePopulation": base_population,
            "controlPopulation": control_population,
            "capacityRatio": treatment_capacity / control_capacity,
            "foodCapRatio": treatment_food_cap / control_food_cap,
            "populationRatio": float(t["civPopulation"]) / control_population,
            "controlCoverage": control_population / control_capacity,
            "treatmentCoverage": float(t["civPopulation"]) / treatment_capacity,
        })

    if not records:
        raise ValueError("no complete matched focus-seat rows at requested offset")
    weights = [float(row["basePopulation"]) for row in records]
    control_followup_weights = [float(row["controlPopulation"]) for row in records]
    capacity_ratios = [float(row["capacityRatio"]) for row in records]
    food_cap_ratios = [float(row["foodCapRatio"]) for row in records]
    population_ratios = [float(row["populationRatio"]) for row in records]
    control_coverage = [float(row["controlCoverage"]) for row in records]
    treatment_coverage = [float(row["treatmentCoverage"]) for row in records]
    estimate = weighted_mean(capacity_ratios, weights)
    ci = bootstrap_mean(capacity_ratios, weights, args.bootstrap_draws, args.bootstrap_seed)
    band = (weighted_quantile(capacity_ratios, weights, 0.05), weighted_quantile(capacity_ratios, weights, 0.95))
    food_cap_band = (weighted_quantile(food_cap_ratios, weights, 0.05),
                     weighted_quantile(food_cap_ratios, weights, 0.95))
    archetypes = defaultdict(list)
    tags = defaultdict(list)
    for row in records:
        archetypes[str(row["archetype"])].append(row)
        tags[str(row["tags"])].append(row)

    print("F-T4 population-weighted held-out estimate (AGR-11/control; diagnostics only)")
    print(f"paired manifests={len(seeds)} complete focus pairs={len(records)} incomplete={len(failed_pairs)} offset=+{args.offset}")
    print("weight=control civilization population at offset 0 (before AGR-11); held-out seats were sampled without geography screening")
    print(f"focus sustainable-capacity ratio: {describe(capacity_ratios)}")
    print(f"population-weighted capacity ratio={estimate:.4f} ({(estimate - 1) * 100:+.2f}%); "
          f"seed bootstrap 95% CI={ci[0]:.4f}..{ci[1]:.4f}")
    print(f"provisional population-weighted 90% fitted band for capacity ratio={band[0]:.4f}..{band[1]:.4f} "
          f"({(band[0] - 1) * 100:+.2f}%..{(band[1] - 1) * 100:+.2f}% capacity change)")
    print(f"population-weighted FoodCap ratio={weighted_mean(food_cap_ratios, weights):.4f}; "
          f"population ratio={weighted_mean(population_ratios, weights):.4f}")
    print(f"provisional population-weighted 90% fitted band for civilization FoodCap ratio="
          f"{food_cap_band[0]:.4f}..{food_cap_band[1]:.4f}")
    print(f"weighted realized-population / sustainable-capacity coverage: control={weighted_mean(control_coverage, weights):.4f}; "
          f"treatment={weighted_mean(treatment_coverage, weights):.4f}")
    print(f"sensitivity using control population at +{args.offset} as weight: "
          f"capacity ratio={weighted_mean(capacity_ratios, control_followup_weights):.4f}; "
          f"band={weighted_quantile(capacity_ratios, control_followup_weights, 0.05):.4f}.."
          f"{weighted_quantile(capacity_ratios, control_followup_weights, 0.95):.4f}")
    print("complete held-out start tags (overlapping):")
    for tag in sorted({tag for row in records for tag in str(row["tags"]).split("|")}):
        selected = [row for row in records if tag in str(row["tags"]).split("|")]
        print(f"  {tag}: n={len(selected)} weighted capacity ratio="
              f"{weighted_mean([float(row['capacityRatio']) for row in selected], [float(row['basePopulation']) for row in selected]):.4f}")
    print("by archetype:")
    for archetype, rows in sorted(archetypes.items()):
        ratio = [float(row["capacityRatio"]) for row in rows]
        archetype_weights = [float(row["basePopulation"]) for row in rows]
        print(f"  {archetype}: {describe(ratio)} weighted={weighted_mean(ratio, archetype_weights):.4f}")
    print(f"censored or excluded pairs={len(failed_pairs)}")
    for seed, reason in failed_pairs:
        print(f"  seed={seed}: {reason}")
    if len(records) < len(seeds):
        print("WARNING: report the fitted band only for complete pairs; do not silently impute censored seats")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
