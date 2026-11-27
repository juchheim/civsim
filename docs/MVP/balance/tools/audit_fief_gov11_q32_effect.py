#!/usr/bin/env python3
"""Audit GOV-11's per-opportunity 11/10 multiplier in Q32 traces."""
from __future__ import annotations

import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path
from typing import Iterable

SCALE = 1 << 32
MULTIPLIER_RAW = (11 * SCALE) // 10
GROWTH_PREFIX = "wp7-gov11-growth "
TERMINAL_PREFIX = "wp7-match-terminal "


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


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


def read_seeds(path: Path) -> set[str]:
    seeds: set[str] = set()
    for number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        value = raw.split("#", 1)[0].strip()
        if not value:
            continue
        if not value.isdigit() or value in seeds:
            raise ValueError(f"{path}:{number}: invalid or duplicate seed {value!r}")
        seeds.add(value)
    return seeds


def audit(paths: Iterable[str], expected_seeds_path: Path) -> dict[str, object]:
    expected = read_seeds(expected_seeds_path)
    terminal: set[str] = set()
    counts: dict[str, list[int]] = defaultdict(lambda: [0, 0, 0, 0, 0])
    mismatches: list[dict[str, int | str]] = []
    rows = 0

    for path in trace_files(paths):
        with path.open(encoding="utf-8", errors="replace") as stream:
            for line_number, line in enumerate(stream, 1):
                if not line.startswith((GROWTH_PREFIX, TERMINAL_PREFIX)):
                    continue
                fields = dict(token.split("=", 1) for token in line.split()[1:] if "=" in token)
                seed = fields.get("seed", "")
                if seed not in expected:
                    raise ValueError(f"{path}:{line_number}: unexpected/missing seed {seed!r}")
                if line.startswith(TERMINAL_PREFIX):
                    if seed in terminal:
                        raise ValueError(f"{path}:{line_number}: duplicate terminal row for seed {seed}")
                    terminal.add(seed)
                    continue
                if fields.get("gov11") != "true":
                    continue
                if fields.get("actualMode") != "treatment":
                    raise ValueError(f"{path}:{line_number}: expected a treatment trace")
                try:
                    control = int(fields["controlConceptionChance"])
                    treatment = int(fields["treatmentConceptionChance"])
                except (KeyError, ValueError) as exc:
                    raise ValueError(f"{path}:{line_number}: invalid fixed-point chance") from exc
                if control < 0 or treatment < 0:
                    raise ValueError(f"{path}:{line_number}: negative chance")
                expected_treatment = (control * MULTIPLIER_RAW) // SCALE
                rows += 1
                row = counts[seed]
                row[0] += 1
                row[1] += control
                row[2] += treatment
                row[3] += expected_treatment
                if control == 0:
                    row[4] += 1
                elif treatment != expected_treatment:
                    mismatches.append({"seed": seed, "line": line_number, "control": control, "expected": expected_treatment, "actual": treatment})

    if terminal != expected:
        raise ValueError(f"terminal seeds mismatch: missing={sorted(expected-terminal, key=int)} extra={sorted(terminal-expected, key=int)}")
    exposed = sorted(counts, key=int)
    per_seed = []
    for seed in exposed:
        count, control, treatment, fixed_target, zero_rows = counts[seed]
        per_seed.append({
            "seed": int(seed),
            "opportunities": count,
            "zero_control_rows": zero_rows,
            "control_chance_raw_sum": control,
            "treatment_chance_raw_sum": treatment,
            "fixed_point_target_raw_sum": fixed_target,
            "treatment_to_control_ratio": treatment / control if control else None,
            "fixed_point_target_ratio": fixed_target / control if control else None,
        })
    return {
        "schema": "civsim.gov11-q32-effect-audit/v1",
        "expected_seeds": len(expected),
        "terminal_seeds": len(terminal),
        "exposed_seeds": len(exposed),
        "opportunity_rows": rows,
        "nonzero_control_rows": sum(int(x["opportunities"]) - int(x["zero_control_rows"]) for x in per_seed),
        "zero_control_rows": sum(int(x["zero_control_rows"]) for x in per_seed),
        "multiplier_raw": MULTIPLIER_RAW,
        "q32_scale": SCALE,
        "multiplier_value": MULTIPLIER_RAW / SCALE,
        "mismatch_rows": len(mismatches),
        "mismatch_examples": mismatches[:10],
        "status": "pass" if len(exposed) == len(expected) and not mismatches else "fail",
        "seeds_sha256": sha256(expected_seeds_path),
        "per_seed": per_seed,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--traces", nargs="+", required=True)
    parser.add_argument("--expected-seeds", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = audit(args.traces, args.expected_seeds)
    payload = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        args.output.write_text(payload, encoding="utf-8")
    print(payload, end="")
    return 0 if result["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
