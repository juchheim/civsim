#!/usr/bin/env python3
"""Summarize paired natural-start F-T4 anchor rows from iron-match output.

Input files contain filtered FIEF_LAND_* rows from matched arms. They may also
include focused E4 trajectory rows from one-arm replays of earlier anchors.
This reports observations only; it does not apply a pass/fail band or infer
physical grain mass balance from sustainable-capacity readouts.
"""

from __future__ import annotations

import argparse
import math
import shlex
from collections import Counter, defaultdict
from pathlib import Path


def fields(line: str) -> tuple[str, dict[str, str]]:
    parts = shlex.split(line)
    kind = parts[0]
    values = {}
    for part in parts[1:]:
        key, sep, value = part.partition("=")
        if sep:
            values[key] = value
    return kind, values


def quantile(values: list[float], q: float) -> float:
    ordered = sorted(values)
    if not ordered:
        return math.nan
    index = (len(ordered) - 1) * q
    lower = math.floor(index)
    upper = math.ceil(index)
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (index - lower)


def describe(values: list[float]) -> str:
    if not values:
        return "n=0"
    return (f"n={len(values)} mean={sum(values) / len(values):.3f} "
            f"median={quantile(values, .5):.3f} p10={quantile(values, .1):.3f} "
            f"p90={quantile(values, .9):.3f} range={min(values):.3f}..{max(values):.3f}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("observations", type=Path, nargs="+", help="filtered paired iron-match FIEF_LAND_* rows")
    args = parser.parse_args()

    manifests: dict[tuple[int, str], dict[str, str]] = {}
    starts: dict[tuple[int, int], dict[str, str]] = {}
    samples: dict[tuple[int, str, int, int], dict[str, str]] = {}
    match_samples: dict[tuple[int, str, int, int], dict[str, str]] = {}
    match_censored = Counter()
    end_rows: dict[tuple[int, str, int], dict[str, str]] = {}
    e4_samples: dict[tuple[int, str, int, int], dict[str, str]] = {}
    censored = Counter()
    e4_censored = Counter()
    for observation in args.observations:
        for raw in observation.read_text().splitlines():
            if not raw.startswith("FIEF_LAND_"):
                continue
            kind, row = fields(raw)
            if kind == "FIEF_LAND_MANIFEST":
                manifests[(int(row["seed"]), row["arm"])] = row
            elif kind == "FIEF_LAND_START":
                key = (int(row["seed"]), int(row["civ"]))
                if key in starts and starts[key] != row:
                    raise ValueError(f"natural start differs across matched arms at {key}")
                starts[key] = row
            elif kind == "FIEF_LAND_SAMPLE":
                key = (int(row["seed"]), row["arm"], int(row["civ"]), int(row["offsetSeasons"]))
                if key in samples:
                    raise ValueError(f"duplicate sample key {key}")
                samples[key] = row
            elif kind == "FIEF_LAND_MATCH_SAMPLE":
                key = (int(row["seed"]), row["arm"], int(row["civ"]), int(row["offsetSeasons"]))
                if key in match_samples:
                    raise ValueError(f"duplicate synchronized match sample key {key}")
                match_samples[key] = row
            elif kind == "FIEF_LAND_MATCH_CENSORED":
                match_censored[(int(row["seed"]), row["arm"])] += 1
            elif kind == "FIEF_LAND_END":
                key = (int(row["seed"]), row["arm"], int(row["civ"]))
                if key in end_rows:
                    raise ValueError(f"duplicate final match row {key}")
                end_rows[key] = row
            elif kind == "FIEF_LAND_E4_SAMPLE":
                key = (int(row["seed"]), row["arm"], int(row["civ"]), int(row["offsetKey4Seasons"]))
                if key in e4_samples:
                    raise ValueError(f"duplicate E4 trajectory sample key {key}")
                e4_samples[key] = row
            elif kind == "FIEF_LAND_CENSORED":
                if row.get("focusCiv", "all") == "all" or row.get("focusCiv") == row["civ"]:
                    censored[(int(row["seed"]), row["arm"], int(row["civ"]))] += 1
            elif kind == "FIEF_LAND_E4_CENSORED":
                e4_censored[(int(row["seed"]), row["arm"], int(row["civ"]))] += 1

    treatment = "agr11"
    control = "control_no_agr11"
    seeds = sorted({seed for seed, _ in manifests})
    if not seeds:
        raise ValueError("no FIEF_LAND_MANIFEST rows found")

    print("F-T4 natural-start whole-match anchors (observations only; no pass/fail)")
    focus_by_seed: dict[int, set[int] | None] = {}
    for (seed, _), manifest in manifests.items():
        focus = manifest["focusCiv"]
        if focus == "all":
            focus_by_seed[seed] = None
        elif focus_by_seed.get(seed) is not None:
            focus_by_seed.setdefault(seed, set()).add(int(focus))
        else:
            focus_by_seed[seed] = {int(focus)}
    focused_starts = {key: row for key, row in starts.items()
                      if key[0] in focus_by_seed and
                      (focus_by_seed[key[0]] is None or key[1] in focus_by_seed[key[0]])}
    mirror_pairs = len({(seed, civ % 2) for seed, civ in focused_starts})
    print(f"seeds={seeds[0]}..{seeds[-1]} worlds={len(seeds)} focused anchors={len(focused_starts)} "
          f"({mirror_pairs} mirror-pair locations under the generator's 180-degree symmetry) "
          f"samples treatment={sum(k[1] == treatment for k in samples)} control={sum(k[1] == control for k in samples)}")

    manifest_pairs = 0
    initial_checksums_equal = 0
    for seed in seeds:
        left = manifests.get((seed, treatment))
        right = manifests.get((seed, control))
        if not left or not right:
            print(f"seed {seed}: missing manifest for one arm")
            continue
        manifest_pairs += 1
        same = all(left.get(key) == right.get(key) for key in ("packHash", "worldFingerprint", "matchStartChecksum"))
        initial_checksums_equal += left.get("matchStartChecksum") == right.get("matchStartChecksum")
        print(f"seed {seed}: same pack/world/start checksum={same} pack={left['packHash']} world={left['worldFingerprint']} start={left['matchStartChecksum']}")
    print(f"paired manifests={manifest_pairs}; identical initial match checksums={initial_checksums_equal}/{manifest_pairs}")

    start_tag_counts: Counter[str] = Counter()
    start_tag_worlds: dict[str, set[int]] = defaultdict(set)
    for row in focused_starts.values():
        for tag in row["tags"].split("|"):
            start_tag_counts[tag] += 1
            start_tag_worlds[tag].add(int(row["seed"]))
    print("focused natural-start tag coverage (overlapping tags):")
    for tag in sorted(start_tag_counts):
        print(f"  {tag}: starts={start_tag_counts[tag]} worlds={len(start_tag_worlds[tag])}")

    paired: dict[int, list[tuple[dict[str, str], dict[str, str]]]] = defaultdict(list)
    offset_zero_equal = 0
    offset_zero_pairs = 0
    for key, treated in samples.items():
        seed, arm, civ, offset = key
        if arm != treatment:
            continue
        if focus_by_seed.get(seed) is not None and civ not in focus_by_seed[seed]:
            continue
        control_row = samples.get((seed, control, civ, offset))
        if control_row is None:
            continue
        if treated["tags"] != control_row["tags"] or treated["tile"] != control_row["tile"]:
            raise ValueError(f"natural start differs between arms at {(seed, civ, offset)}")
        paired[offset].append((treated, control_row))
        if offset == 0:
            offset_zero_pairs += 1
            offset_zero_equal += treated["stateChecksum"] == control_row["stateChecksum"]

    print(f"offset-0 whole-match checksums equal={offset_zero_equal}/{offset_zero_pairs}")
    strict_keys: set[tuple[int, int]] = set()
    for (seed, arm, civ, offset), treated in samples.items():
        if arm != treatment or offset != 0:
            continue
        if focus_by_seed.get(seed) is not None and civ not in focus_by_seed[seed]:
            continue
        held = samples.get((seed, control, civ, offset))
        if held is not None and treated["stateChecksum"] == held["stateChecksum"]:
            strict_keys.add((seed, civ))
    print("strict identical-state anchor seats=" + ", ".join(
        f"{seed}/c{civ}" for seed, civ in sorted(strict_keys)))
    print("paired outcomes by offset and natural-start cohort; FoodCap/capacity are sustainable model readouts, not physical grain stocks:")
    cohort_names = ["all", "river_access", "no_river", "coastal", "inland", "deep_inland", "hill", "no_hill"]
    measures = (
        ("homeFoodCap", "home FoodCap ratio"),
        ("homeSustainableCapacity", "home sustainable capacity ratio"),
        ("civFoodCap", "civilization FoodCap ratio"),
        ("civSustainableCapacity", "civilization sustainable capacity ratio"),
        ("cityShare", "city-share change, percentage points"),
        ("civPopulation", "civilization population ratio"),
        ("homePop", "home population ratio"),
    )
    for offset in sorted(paired):
        rows = paired[offset]
        print(f"offset +{offset} seasons: paired={len(rows)}")
        for cohort in cohort_names:
            selected = rows if cohort == "all" else [pair for pair in rows if cohort in pair[0]["tags"].split("|")]
            if not selected:
                print(f"  {cohort}: n=0")
                continue
            distinct_pairs = len({(int(treated["seed"]), int(treated["civ"]) % 2) for treated, _ in selected})
            output = []
            for field, label in measures:
                values = []
                for treated, held in selected:
                    t = float(treated[field])
                    c = float(held[field])
                    if field == "cityShare":
                        values.append(t - c)
                    elif c > 0:
                        values.append(t / c)
                output.append(f"{label}: {describe(values)}")
            print(f"  {cohort} seat-pairs={len(selected)} mirrored-start-pairs={distinct_pairs}; " + "; ".join(output))
        for archetype in sorted({treated["archetype"] for treated, _ in rows}):
            selected = [(treated, held) for treated, held in rows if treated["archetype"] == archetype]
            capacity = [float(treated["civSustainableCapacity"]) / float(held["civSustainableCapacity"])
                        for treated, held in selected if float(held["civSustainableCapacity"]) > 0]
            population = [float(treated["civPopulation"]) / float(held["civPopulation"])
                          for treated, held in selected if float(held["civPopulation"]) > 0]
            print(f"  archetype {archetype} seat-pairs={len(selected)}; civ sustainable-capacity ratio {describe(capacity)}; "
                  f"civ population ratio {describe(population)}")

    print("strict subset, selected only when offset-0 whole-match checksums match:")
    for offset in sorted(paired):
        selected = [(treated, held) for treated, held in paired[offset]
                    if (int(treated["seed"]), int(treated["civ"])) in strict_keys]
        if not selected:
            continue
        output = []
        for field, label in measures:
            values = []
            for treated, held in selected:
                t = float(treated[field])
                c = float(held[field])
                if field == "cityShare":
                    values.append(t - c)
                elif c > 0:
                    values.append(t / c)
            output.append(f"{label}: {describe(values)}")
        print(f"  offset +{offset} seasons seat-pairs={len(selected)}; " + "; ".join(output))

    e4_treatment = {(seed, civ, offset): row for (seed, arm, civ, offset), row in e4_samples.items()
                    if arm == treatment}
    e4_control = {(seed, civ, offset): row for (seed, arm, civ, offset), row in e4_samples.items()
                  if arm == control}
    trajectory_pairs = 0
    trajectory_equal = 0
    for key, treated in e4_treatment.items():
        held = e4_control.get(key)
        if held is None:
            continue
        trajectory_pairs += 1
        same = treated["stateChecksum"] == held["stateChecksum"]
        same &= treated["key4Season"] == held["key4Season"] and treated["key5Season"] == held["key5Season"]
        trajectory_equal += same
    print(f"live E4 trajectory rows: focused anchors={len({(seed, civ) for seed, civ, _ in e4_treatment})}; "
          f"matched treatment/control snapshots equal={trajectory_equal}/{trajectory_pairs}; "
          f"E4-censored seat-arms={sum(e4_censored.values())}")
    anchor_durations = {(seed, civ): int(row["key5Season"]) - int(row["key4Season"])
                        for (seed, civ, offset), row in e4_treatment.items()
                        if offset == 0 and int(row["key5Season"]) >= 0}
    durations = list(anchor_durations.values())
    if durations:
        print(f"KEY-4 to KEY-5 first-observation boundary interval: {describe([float(x) for x in durations])} seasons across {len(durations)} focused anchors")
    print("live E4 trajectory (pre-KEY-5 only; one treatment row per focused anchor and offset):")
    for offset in sorted({key[2] for key in e4_treatment}):
        rows = [row for (seed, civ, row_offset), row in e4_treatment.items() if row_offset == offset]
        population = [float(row["civPopulation"]) for row in rows]
        capacity = [float(row["civSustainableCapacity"]) for row in rows]
        coverage = [float(row["civPopulation"]) / float(row["civSustainableCapacity"])
                    for row in rows if float(row["civSustainableCapacity"]) > 0]
        print(f"  KEY-4 +{offset} seasons n={len(rows)}; civ population {describe(population)}; "
              f"civ sustainable-capacity coverage {describe(coverage)}")

    if match_samples:
        print("synchronized whole-match trajectory (all four civilizations sampled at fixed offsets after the focus seat's KEY-5):")
        match_arms: dict[tuple[int, str, int], dict[int, dict[str, str]]] = defaultdict(dict)
        for (seed, arm, civ, offset), row in match_samples.items():
            match_arms[(seed, arm, offset)][civ] = row
        whole_match_pairs: dict[int, list[dict[str, float]]] = defaultdict(list)
        whole_match_by_archetype: dict[tuple[int, str], list[dict[str, float]]] = defaultdict(list)
        offset_zero_checksums = 0
        offset_zero_equal = 0
        for seed in seeds:
            for offset in sorted({key[2] for key in match_arms if key[0] == seed}):
                treatment_rows = match_arms.get((seed, treatment, offset), {})
                control_rows = match_arms.get((seed, control, offset), {})
                if set(treatment_rows) != set(range(4)) or set(control_rows) != set(range(4)):
                    continue
                treatment_ticks = {row["tick"] for row in treatment_rows.values()}
                control_ticks = {row["tick"] for row in control_rows.values()}
                treatment_checksums = {row["stateChecksum"] for row in treatment_rows.values()}
                control_checksums = {row["stateChecksum"] for row in control_rows.values()}
                if len(treatment_ticks) != 1 or len(control_ticks) != 1 or treatment_ticks != control_ticks:
                    raise ValueError(f"synchronized sample tick mismatch at seed={seed} offset={offset}")
                if len(treatment_checksums) != 1 or len(control_checksums) != 1:
                    raise ValueError(f"civilizations have inconsistent whole-match checksums at seed={seed} offset={offset}")
                if offset == 0:
                    offset_zero_checksums += 1
                    offset_zero_equal += treatment_checksums == control_checksums
                focus = int(manifests[(seed, treatment)]["focusCiv"])

                def totals(rows: dict[int, dict[str, str]]) -> tuple[float, float, float, float, float]:
                    population = sum(float(row["civPopulation"]) for row in rows.values())
                    city_population = sum(float(row["cityPopulation"]) for row in rows.values())
                    capacity = sum(float(row["civSustainableCapacity"]) for row in rows.values())
                    focus_population = float(rows[focus]["civPopulation"])
                    rival_population = population - focus_population
                    city_share = 100 * city_population / population if population > 0 else 0.0
                    return population, capacity, city_share, focus_population, rival_population

                t_total, t_capacity, t_city_share, t_focus_pop, t_rival_pop = totals(treatment_rows)
                c_total, c_capacity, c_city_share, c_focus_pop, c_rival_pop = totals(control_rows)
                metrics = {
                    "totalPopulationRatio": t_total / c_total if c_total else math.nan,
                    "capacityRatio": t_capacity / c_capacity if c_capacity else math.nan,
                    "cityShareChange": t_city_share - c_city_share,
                    "focusPopulationRatio": t_focus_pop / c_focus_pop if c_focus_pop else math.nan,
                    "rivalPopulationRatio": t_rival_pop / c_rival_pop if c_rival_pop else math.nan,
                }
                whole_match_pairs[offset].append(metrics)
                whole_match_by_archetype[(offset, treatment_rows[focus]["archetype"])].append(metrics)
        print(f"  offset-0 synchronized whole-match checksum equal={offset_zero_equal}/{offset_zero_checksums}")
        for offset, rows in sorted(whole_match_pairs.items()):
            print(f"  focus KEY-5 +{offset} seasons; matched worlds={len(rows)}; " + "; ".join(
                f"{label}: {describe([row[field] for row in rows])}"
                for field, label in (
                    ("totalPopulationRatio", "whole-match population ratio"),
                    ("capacityRatio", "whole-match sustainable-capacity ratio"),
                    ("cityShareChange", "whole-match city-share change, pp"),
                    ("focusPopulationRatio", "focus population ratio"),
                    ("rivalPopulationRatio", "rival population ratio"),
                )))
            for (archetype_offset, archetype), archetype_rows in sorted(whole_match_by_archetype.items()):
                if archetype_offset != offset:
                    continue
                print(f"    focus archetype {archetype}; worlds={len(archetype_rows)}; " + "; ".join(
                    f"{label}: {describe([row[field] for row in archetype_rows])}"
                    for field, label in (
                        ("totalPopulationRatio", "whole-match population ratio"),
                        ("capacityRatio", "whole-match capacity ratio"),
                        ("cityShareChange", "city-share change, pp"),
                    )))
        if match_censored:
            print(f"  censored whole-match sample arms={sum(match_censored.values())}; "
                  f"arms with any censoring={len(match_censored)}")
        else:
            print("  censored whole-match sample arms=0")

    if end_rows:
        print("final whole-match horizon rows:")
        durations = sorted({row["minutesSimulated"] for row in end_rows.values()})
        end_states = sorted({(row["endReason"], row["ended"]) for row in end_rows.values()})
        print(f"  simulated minutes={','.join(durations)}; end states={end_states}; "
              f"civilization rows={len(end_rows)}; KEY-6 completions={sum(row['key6Minute'] != '-' for row in end_rows.values())}")
        final_pairs = []
        for seed in seeds:
            treatment_rows = {civ: end_rows[(seed, treatment, civ)] for civ in range(4)
                              if (seed, treatment, civ) in end_rows}
            control_rows = {civ: end_rows[(seed, control, civ)] for civ in range(4)
                            if (seed, control, civ) in end_rows}
            if set(treatment_rows) != set(range(4)) or set(control_rows) != set(range(4)):
                continue
            if {row["minutesSimulated"] for row in treatment_rows.values()} != {row["minutesSimulated"] for row in control_rows.values()}:
                continue
            focus = int(manifests[(seed, treatment)]["focusCiv"])
            t_population = sum(int(row["civPopulation"]) for row in treatment_rows.values())
            c_population = sum(int(row["civPopulation"]) for row in control_rows.values())
            t_city = sum(int(row["cityPopulation"]) for row in treatment_rows.values())
            c_city = sum(int(row["cityPopulation"]) for row in control_rows.values())
            t_capacity = sum(float(row["civSustainableCapacity"]) for row in treatment_rows.values())
            c_capacity = sum(float(row["civSustainableCapacity"]) for row in control_rows.values())
            final_pairs.append({
                "totalPopulationRatio": t_population / c_population if c_population else math.nan,
                "capacityRatio": t_capacity / c_capacity if c_capacity else math.nan,
                "cityShareChange": (100 * t_city / t_population - 100 * c_city / c_population)
                if t_population and c_population else math.nan,
                "focusPopulationRatio": int(treatment_rows[focus]["civPopulation"]) /
                int(control_rows[focus]["civPopulation"]) if int(control_rows[focus]["civPopulation"]) else math.nan,
            })
        if final_pairs:
            print(f"  final matched worlds={len(final_pairs)}; " + "; ".join(
                f"{label}: {describe([row[field] for row in final_pairs])}"
                for field, label in (
                    ("totalPopulationRatio", "whole-match population ratio"),
                    ("capacityRatio", "whole-match sustainable-capacity ratio"),
                    ("cityShareChange", "whole-match city-share change, pp"),
                    ("focusPopulationRatio", "focus population ratio"),
                )))

    if censored:
        print("censored seat-arms:")
        for (seed, arm, civ), count in sorted(censored.items()):
            print(f"  seed={seed} arm={arm} civ={civ} censorRows={count}")
    else:
        print("censored seat-arms: 0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
