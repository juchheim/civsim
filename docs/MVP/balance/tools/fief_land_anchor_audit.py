#!/usr/bin/env python3
"""Summarize paired natural-start F-T4 anchor rows from iron-match output.

Input is the filtered text containing FIEF_LAND_MANIFEST, FIEF_LAND_START,
FIEF_LAND_SAMPLE and FIEF_LAND_CENSORED lines from both arms. This reports
observations only; it does not apply a pass/fail band or infer physical grain
mass balance from sustainable-capacity readouts.
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
    parser.add_argument("observations", type=Path, help="filtered paired iron-match FIEF_LAND_* rows")
    args = parser.parse_args()

    manifests: dict[tuple[int, str], dict[str, str]] = {}
    starts: dict[tuple[int, int], dict[str, str]] = {}
    samples: dict[tuple[int, str, int, int], dict[str, str]] = {}
    censored = Counter()
    for raw in args.observations.read_text().splitlines():
        if not raw.startswith("FIEF_LAND_"):
            continue
        kind, row = fields(raw)
        if kind == "FIEF_LAND_MANIFEST":
            manifests[(int(row["seed"]), row["arm"])] = row
        elif kind == "FIEF_LAND_START":
            starts[(int(row["seed"]), int(row["civ"]))] = row
        elif kind == "FIEF_LAND_SAMPLE":
            key = (int(row["seed"]), row["arm"], int(row["civ"]), int(row["offsetSeasons"]))
            if key in samples:
                raise ValueError(f"duplicate sample key {key}")
            samples[key] = row
        elif kind == "FIEF_LAND_CENSORED":
            censored[(int(row["seed"]), row["arm"], int(row["civ"]))] += 1

    treatment = "agr11"
    control = "control_no_agr11"
    seeds = sorted({seed for seed, _ in manifests})
    if not seeds:
        raise ValueError("no FIEF_LAND_MANIFEST rows found")

    print("F-T4 natural-start whole-match anchors (observations only; no pass/fail)")
    mirror_pairs = len({(seed, civ % 2) for seed, civ in starts})
    print(f"seeds={seeds[0]}..{seeds[-1]} worlds={len(seeds)} starts={len(starts)} "
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
    for row in starts.values():
        for tag in row["tags"].split("|"):
            start_tag_counts[tag] += 1
            start_tag_worlds[tag].add(int(row["seed"]))
    print("natural-start tag coverage (overlapping tags):")
    for tag in sorted(start_tag_counts):
        print(f"  {tag}: starts={start_tag_counts[tag]} worlds={len(start_tag_worlds[tag])}")

    paired: dict[int, list[tuple[dict[str, str], dict[str, str]]]] = defaultdict(list)
    offset_zero_equal = 0
    offset_zero_pairs = 0
    for key, treated in samples.items():
        seed, arm, civ, offset = key
        if arm != treatment:
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

    if censored:
        print("censored seat-arms:")
        for (seed, arm, civ), count in sorted(censored.items()):
            print(f"  seed={seed} arm={arm} civ={civ} censorRows={count}")
    else:
        print("censored seat-arms: 0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
