"""Pool C3 midpoint logs and evaluate hazard ratios plus age-standardized elder e45.

Example:
    python analyze_c3_logs.py /tmp/civsim-m3-c3-40-final-report

Exact-age hazards are pooled by person-seasons. The elder life table reports a
tail-sensitivity range because starvation combines a Gompertz-scaled baseline
budget with a separate winter shortfall surcharge, and exposure depends on the
observed warmth-deficit mix. Seed-cluster bootstrap intervals expose sampling
uncertainty; estimates remain conditional on the observed survivor/ecology mix.
"""
from __future__ import annotations

import argparse
import random
from pathlib import Path


ERAS = ("E0", "E1", "E2")
AGES = ("child", "adult", "elder")
CAUSES = ("endemic", "epidemic", "starvation", "exposure", "injury", "predators", "violence", "senescence")
LIFE_TABLE_CAUSES = tuple(cause for cause in CAUSES if cause != "starvation") + (
    "starvation_baseline", "winter_shortfall")
MORT = {
    "child": (0.036, 0.033, 0.035),
    "adult": (0.012, 0.011, 0.012),
}
E45_TARGETS = (15.0, 16.0, 16.0)
WINTER = (2.0, 1.7, 1.4)
GOMPERTZ = 1.08872
BOOTSTRAP_REPLICATES = 500


def fields(line: str) -> dict[str, str]:
    return dict(part.split("=", 1) for part in line.split() if "=" in part)


def cause_fields(value: str) -> dict[str, float]:
    value = value.removeprefix("[").removesuffix("]")
    return {part.split("=", 1)[0]: float(part.split("=", 1)[1]) for part in value.split(",") if "=" in part}


def interpolate(left: float, right: float, fraction: float) -> float:
    if left > 0.0 and right > 0.0:
        return left * ((right / left) ** fraction)
    return max(0.0, left + (right - left) * fraction)


def percentile(values: list[float], probability: float) -> float:
    ordered = sorted(values)
    position = (len(ordered) - 1) * probability
    lower = int(position)
    upper = min(lower + 1, len(ordered) - 1)
    fraction = position - lower
    return ordered[lower] + (ordered[upper] - ordered[lower]) * fraction


def pooled_exact_rows(seed_rows: dict[str, dict], selected_seeds: list[str]) -> dict:
    pooled: dict[tuple[str, int, int], dict] = {}
    for seed in selected_seeds:
        for key, row in seed_rows[seed].items():
            total = pooled.setdefault(key, {"exposure": 0, "hazard": {cause: 0.0 for cause in CAUSES}, "winter_shortfall": 0.0})
            total["exposure"] += row["exposure"]
            total["winter_shortfall"] += row["winter_shortfall"]
            for cause in CAUSES:
                total["hazard"][cause] += row["hazard"][cause]
    return pooled


def seasonal_curves(pooled: dict, era: str) -> tuple[list[dict[int, dict[str, float]]], list[int], list[int]]:
    curves: list[dict[int, dict[str, float]]] = [dict() for _ in range(4)]
    support: list[dict[int, int]] = [dict() for _ in range(4)]
    for (row_era, age, season), row in pooled.items():
        if row_era != era or row["exposure"] <= 0:
            continue
        rates = {cause: row["hazard"][cause] / row["exposure"] for cause in CAUSES}
        rates["winter_shortfall"] = row["winter_shortfall"] / row["exposure"]
        rates["starvation_baseline"] = max(0.0, rates["starvation"] - rates["winter_shortfall"])
        curves[season][age] = rates
        support[season][age] = row["exposure"]
    max_by_season = [max(season_curves, default=0) for season_curves in curves]
    union_ages = sorted({age for season_curves in curves for age in season_curves})
    return curves, max_by_season, union_ages


def estimate_cause(curve: dict[int, dict[str, float]], age: int, cause: str,
                   tail_mode: str) -> float:
    def rate_at(sample_age: int) -> float:
        return curve[sample_age][cause]

    if age in curve:
        return rate_at(age)
    sampled = sorted(curve)
    if not sampled:
        raise ValueError("No sampled exact-age hazards for this season")
    lower = [sampled_age for sampled_age in sampled if sampled_age < age]
    upper = [sampled_age for sampled_age in sampled if sampled_age > age]
    if lower and upper:
        lo, hi = max(lower), min(upper)
        return interpolate(rate_at(lo), rate_at(hi), (age - lo) / (hi - lo))

    if lower:
        anchor = max(lower)
        factor = GOMPERTZ ** (age - anchor)
        if tail_mode in {"winter-shortfall-flat", "winter-shortfall-and-exposure-flat"} and cause == "winter_shortfall":
            factor = 1.0
        elif tail_mode in {"exposure-flat", "winter-shortfall-and-exposure-flat"} and cause == "exposure":
            factor = 1.0
        return rate_at(anchor) * factor

    anchor = min(upper)
    factor = GOMPERTZ ** (age - anchor)
    if tail_mode in {"winter-shortfall-flat", "winter-shortfall-and-exposure-flat"} and cause == "winter_shortfall":
        factor = 1.0
    elif tail_mode in {"exposure-flat", "winter-shortfall-and-exposure-flat"} and cause == "exposure":
        factor = 1.0
    return rate_at(anchor) * factor


TAIL_MODES = (
    ("all-causes-gompertz", "Scale every cause, including the additive winter shortfall surcharge, by the Gompertz slope."),
    ("winter-shortfall-flat", "Hold the additive winter shortfall surcharge flat; Gompertz-scale baseline starvation and other causes."),
    ("exposure-flat", "Hold exposure flat and Gompertz-scale the winter shortfall surcharge and other causes."),
    ("winter-shortfall-and-exposure-flat", "Hold the additive winter shortfall surcharge and exposure flat; Gompertz-scale baseline starvation and remaining causes."),
)


def e45_for(pooled: dict, era: str, tail_mode: str) -> tuple[float, float, list[int], list[int]]:
    curves, max_by_season, sampled_ages = seasonal_curves(pooled, era)
    if not sampled_ages:
        raise ValueError(f"{era}: no exact-age person-time")
    survival = 1.0
    e45 = 0.0
    for age in range(45, 100):
        e45 += survival
        survival_year = 1.0
        for season in range(4):
            total_rate = sum(estimate_cause(curves[season], age, cause, tail_mode) for cause in LIFE_TABLE_CAUSES)
            survival_year *= 1.0 - min(1.0, max(0.0, total_rate))
        survival *= survival_year
    return e45, survival, max_by_season, sampled_ages


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("log_dir", type=Path)
    parser.add_argument("--bootstrap", type=int, default=BOOTSTRAP_REPLICATES,
                        help=f"seed-cluster bootstrap replicates (default: {BOOTSTRAP_REPLICATES})")
    args = parser.parse_args()
    logs = sorted(args.log_dir.glob("balance-seed-*.log"))
    if not logs:
        parser.error(f"No balance-seed-*.log files found in {args.log_dir}")

    band_exposure = {(era, age): 0 for era in ERAS for age in AGES}
    band_hazard = {(era, age): 0.0 for era in ERAS for age in AGES}
    exact_by_seed: dict[str, dict] = {}

    for log in logs:
        seed = log.stem.removeprefix("balance-seed-")
        exact_by_seed[seed] = {}
        for line in log.read_text().splitlines():
            if "economy-diagnostic c3-age-band " in line:
                row = fields(line)
                era, age = row.get("era"), row.get("age")
                if era not in ERAS or age not in AGES or row.get("personSeasons") in (None, "0"):
                    continue
                exposure = int(row["personSeasons"])
                band_exposure[(era, age)] += exposure
                band_hazard[(era, age)] += float(row["expectedAnnualHazard"]) * exposure
            elif "economy-diagnostic c3-exact-age-season " in line:
                row = fields(line)
                era = row.get("era")
                if era not in ERAS:
                    continue
                age, season = int(row["age"]), int(row["season"])
                exposure = int(row["personSeasons"])
                key = (era, age, season)
                total = exact_by_seed[seed].setdefault(key, {"exposure": 0, "hazard": {cause: 0.0 for cause in CAUSES}, "winter_shortfall": 0.0})
                total["exposure"] += exposure
                total["winter_shortfall"] += float(row.get("winterShortfallHazard", "0")) * exposure
                for cause, value in cause_fields(row.get("causes", "[]")).items():
                    if cause in CAUSES:
                        total["hazard"][cause] += value * exposure

    print(f"C3 pooled logs: {len(logs)}")
    print("C3 midpoint age-band person-time:")
    for era in ERAS:
        rows = []
        for age in AGES:
            exposure = band_exposure[(era, age)]
            rows.append(f"{age}={exposure} seasons/{exposure / 4.0:.2f} years")
        print(f"  {era}: " + "; ".join(rows))
    annual_effective_targets: dict[tuple[str, str], float] = {}
    ratio_pass = True
    for age in ("child", "adult"):
        age_targets = MORT[age]
        for index, era in enumerate(ERAS):
            annual_effective_targets[(era, age)] = age_targets[index] * (3.0 + WINTER[index]) / 4.0
        e0_exposure = band_exposure[("E0", age)]
        e0_hazard = band_hazard[("E0", age)] / e0_exposure if e0_exposure else 0.0
        print(f"\n{age.capitalize()} C3 annual-effective rate by person-time:")
        for index, era in enumerate(ERAS):
            exposure = band_exposure[(era, age)]
            rate = band_hazard[(era, age)] / exposure if exposure else 0.0
            target = annual_effective_targets[(era, age)]
            absolute_delta = (rate / target - 1.0) * 100.0 if target else 0.0
            print(f"  {era}: personSeasons={exposure} rate={rate:.6f} target={target:.6f} absoluteDiagnostic={absolute_delta:+.1f}%")
            if index == 0:
                continue
            actual_ratio = rate / e0_hazard if e0_hazard else 0.0
            target_ratio = target / annual_effective_targets[("E0", age)]
            delta = (actual_ratio / target_ratio - 1.0) * 100.0 if target_ratio else 0.0
            passed = abs(delta) <= 10.0
            ratio_pass &= passed
            print(f"    {era}/E0 ratio={actual_ratio:.4f} target={target_ratio:.4f} delta={delta:+.1f}% within10={passed}")

    life_table_pass = True
    seed_names = sorted(exact_by_seed)
    rng = random.Random(20261019)
    bootstrap_samples = [rng.choices(seed_names, k=len(seed_names)) for _ in range(max(0, args.bootstrap))]
    pooled_all = pooled_exact_rows(exact_by_seed, seed_names)
    for era_index, era in enumerate(ERAS):
        target = E45_TARGETS[era_index]
        low, high = target * 0.9, target * 1.1
        point_results = {mode: e45_for(pooled_all, era, mode) for mode, _ in TAIL_MODES}
        boot_results = {mode: [] for mode, _ in TAIL_MODES}
        for selected in bootstrap_samples:
            pooled_sample = pooled_exact_rows(exact_by_seed, selected)
            for mode, _ in TAIL_MODES:
                boot_results[mode].append(e45_for(pooled_sample, era, mode)[0])

        all_values = [result[0] for result in point_results.values()]
        all_ci_inside = True
        print(f"\n{era} age-standardized elder e45 target={target:.1f} acceptanceBand=[{low:.1f},{high:.1f}]")
        print("  personSeasons by exact age and season (unobserved ages omitted):")
        curves, max_by_season, sampled_ages = seasonal_curves(pooled_all, era)
        for season in range(4):
            pooled = pooled_all
            support = {age: pooled[(era, age, season)]["exposure"]
                       for row_era, age, row_season in pooled
                       if row_era == era and row_season == season}
            support_text = ",".join(f"{age}:{support[age]}" for age in sorted(support))
            print(f"    season={season} maxAge={max_by_season[season]} support={support_text}")

        for mode, description in TAIL_MODES:
            e45, survival100, mode_max, sampled = point_results[mode]
            samples = boot_results[mode]
            ci_low = percentile(samples, 0.025) if samples else e45
            ci_high = percentile(samples, 0.975) if samples else e45
            mode_inside = low <= ci_low and ci_high <= high
            all_ci_inside &= mode_inside
            delta = (e45 / target - 1.0) * 100.0
            print(f"  {mode}: e45={e45:.2f} delta={delta:+.1f}% seedBootstrap95=[{ci_low:.2f},{ci_high:.2f}] within10={low <= e45 <= high} ciWithin10={mode_inside} survivalAt100={survival100:.6f}")
            print(f"    assumption: {description}")
        span = (min(all_values), max(all_values))
        robust = all_ci_inside and span[0] >= low and span[1] <= high
        life_table_pass &= robust
        print(f"  tailSensitivityRange=[{span[0]:.2f},{span[1]:.2f}] robustWithin10={robust} sampledAgeRange=[{min(sampled_ages)},{max(sampled_ages)}] bootstrapClusters={len(seed_names)} replicates={len(bootstrap_samples)}")
        if any(max_age < 75 for max_age in max_by_season):
            print("  warning: at least one season has observed support below age 75; the tail and mechanism mix are extrapolated")

    return 0 if ratio_pass and life_table_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
