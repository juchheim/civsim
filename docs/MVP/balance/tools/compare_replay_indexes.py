#!/usr/bin/env python3
"""Compare CivSim replay-index-v1 JSON artifacts, including every per-tick checksum."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any


SCHEMA = "civsim-replay-index-v1"
FROZEN_MANIFEST_SHA256 = "1c05d1be1edf3b6b4cad9bcbf35503ab6b454133515388cc8f42ca82a1ad3ce6"


def load_index(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        artifact = json.load(stream)
    if not isinstance(artifact, dict) or artifact.get("schema") != SCHEMA:
        raise ValueError(f"{path}: expected schema {SCHEMA}")
    seeds = artifact.get("seeds")
    if not isinstance(seeds, list):
        raise ValueError(f"{path}: seeds must be an array")
    by_seed: dict[int, dict[str, Any]] = {}
    for entry in seeds:
        if not isinstance(entry, dict) or not isinstance(entry.get("seed"), int):
            raise ValueError(f"{path}: each seed entry must contain an integer seed")
        seed = entry["seed"]
        if seed in by_seed:
            raise ValueError(f"{path}: duplicate seed {seed}")
        if not isinstance(entry.get("checksums"), list):
            raise ValueError(f"{path}: seed {seed} checksums must be an array")
        if not isinstance(entry.get("verified"), bool):
            raise ValueError(f"{path}: seed {seed} verified must be a boolean")
        by_seed[seed] = entry
    return {"artifact": artifact, "seeds": by_seed, "orderedSeeds": [entry["seed"] for entry in seeds]}


def compare(left: dict[str, Any], right: dict[str, Any], expected_seeds: list[int]) -> list[str]:
    mismatches: list[str] = []
    for label, index in (("left", left), ("right", right)):
        if index["orderedSeeds"] != expected_seeds:
            actual = index["orderedSeeds"]
            missing = sorted(set(expected_seeds) - set(actual))
            extra = sorted(set(actual) - set(expected_seeds))
            mismatches.append(f"{label} index seed coverage/order differs from frozen manifest; missing={missing[:10]}, extra={extra[:10]}")
    left_artifact, right_artifact = left["artifact"], right["artifact"]
    if left_artifact.get("packHash") != right_artifact.get("packHash"):
        mismatches.append("packHash differs")
    left_seeds, right_seeds = left["seeds"], right["seeds"]
    for seed in sorted(left_seeds.keys() | right_seeds.keys()):
        if seed not in left_seeds:
            mismatches.append(f"seed {seed}: missing from left index")
            continue
        if seed not in right_seeds:
            mismatches.append(f"seed {seed}: missing from right index")
            continue
        a, b = left_seeds[seed], right_seeds[seed]
        for key in ("verified", "ticks", "commandCount", "checksumInterval", "checksumCount", "packHash", "mode", "finalChecksum"):
            if a.get(key) != b.get(key):
                mismatches.append(f"seed {seed}: {key} differs ({a.get(key)!r} != {b.get(key)!r})")
        if not a.get("verified") or not b.get("verified"):
            mismatches.append(f"seed {seed}: one or both replay verifications failed")
        a_checks, b_checks = a["checksums"], b["checksums"]
        for i in range(max(len(a_checks), len(b_checks))):
            if i >= len(a_checks):
                item = b_checks[i]
                mismatches.append(f"seed {seed}: left missing checksum at tick {item.get('tick', '?')}")
            elif i >= len(b_checks):
                item = a_checks[i]
                mismatches.append(f"seed {seed}: right missing checksum at tick {item.get('tick', '?')}")
            else:
                ac, bc = a_checks[i], b_checks[i]
                if ac.get("tick") != bc.get("tick"):
                    mismatches.append(f"seed {seed}: checksum tick differs at sequence index {i} ({ac.get('tick')!r} != {bc.get('tick')!r})")
                if ac.get("checksum") != bc.get("checksum"):
                    mismatches.append(f"seed {seed}: checksum differs at tick {ac.get('tick', '?')} ({ac.get('checksum')!r} != {bc.get('checksum')!r})")
    return mismatches


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("left", type=Path, help="first replay index JSON")
    parser.add_argument("right", type=Path, help="second replay index JSON")
    parser.add_argument("--seeds-file", type=Path, required=True,
                        help="the frozen B5-03 manifest whose exact seed sequence both indexes must cover")
    args = parser.parse_args(argv)
    try:
        raw_manifest = args.seeds_file.read_bytes()
        actual_manifest_hash = hashlib.sha256(raw_manifest).hexdigest()
        if actual_manifest_hash != FROZEN_MANIFEST_SHA256:
            raise ValueError(f"frozen manifest hash mismatch: expected {FROZEN_MANIFEST_SHA256}, got {actual_manifest_hash}")
        expected_seeds = [int(line) for line in raw_manifest.decode("ascii").splitlines()]
        mismatches = compare(load_index(args.left), load_index(args.right), expected_seeds)
    except (OSError, json.JSONDecodeError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 2
    if mismatches:
        print(f"replay indexes differ ({len(mismatches)} mismatch(es))")
        for mismatch in mismatches:
            print(f"- {mismatch}")
        return 1
    print("replay indexes match: every seed and every tick checksum agrees")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
