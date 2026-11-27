#!/usr/bin/env python3
"""Check the byte-identical B5-03 runtime payload for the frozen E5 candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path


PINS = {
    "source_head": "0f7bdd2a182568425f5b1c8531a1442513ca6008",
    "source_tree": "893aefd5123fc663a2668a28ef022bcd1d1cf329",
    "source_aggregate": "c2324286d5578983f6b11ff52d1b6c19bd80ae3933f22edd0eb64ea2a1418aa4",
    "pack": "ef8602092c1adda628a2862f1fc84fba4a40e6f2ab292b95fb40fa988aecbc90",
    "harness": "e9197fe7c18836ce5afdf626707978e6963c76f3af625c486dd47dce621181aa",
    "sim": "ba2c95c1108fb89c85e3f74b74f1c9da91aee7cafa835e619305d97838dc72e6",
    "tests": "2deffd7038e4333ec839b04af16ecdff12bb0209a1212b36dc0bb89ea150a254",
    "runner": "fac86cdd7cb2b86691d34f34da234ea2c8bcb69b7c2a1fcc45d59a10ae136618",
    "test_source": "0233a824a7e7adb94711f813f23afd0ad502973acc3cd1da074da200d9d022e9",
    "merger": "c84180f7e0dc54133594a2d610b505ef37226482862049a60a4657a8c5ddeec5",
    "comparator": "8a5b3eec59ad401685cbe7a77776e962833ce149b73776575c46e57cf2a43b20",
    "manifest": "12f313f9878c568798b32e723f1aa1783b2dbb3039bfae60749f7a279d1d9895",
}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--payload", required=True, type=Path)
    parser.add_argument("--source", required=True, type=Path)
    args = parser.parse_args()
    payload, source = args.payload.resolve(), args.source.resolve()

    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=source, check=True,
                          capture_output=True, text=True).stdout.strip()
    tree = subprocess.run(["git", "rev-parse", "HEAD^{tree}"], cwd=source, check=True,
                          capture_output=True, text=True).stdout.strip()
    snapshot = subprocess.run([sys.executable, str(source / "docs/MVP/balance/tools/hash_e5_workspace.py")],
                              cwd=source, check=True, capture_output=True, text=True)
    source_aggregate = json.loads(snapshot.stdout)["aggregate_sha256"]
    actuals = {
        "source_head": head,
        "source_tree": tree,
        "source_aggregate": source_aggregate,
        "pack": digest(payload / "numeric-e5.pack"),
        "harness": digest(payload / "harness/CivSim.Harness.dll"),
        "sim": digest(payload / "harness/CivSim.Sim.dll"),
        "tests": digest(payload / "tests/CivSim.Tests.dll"),
        "runner": digest(source / "docs/MVP/balance/tools/run_b5_03_determinism.py"),
        "test_source": digest(source / "docs/MVP/balance/tools/test_run_b5_03_determinism.py"),
        "merger": digest(source / "docs/MVP/balance/tools/merge_b5_03_shards.py"),
        "comparator": digest(source / "docs/MVP/balance/tools/compare_replay_indexes.py"),
        "manifest": digest(source / "docs/MVP/balance/m12-e5-9-b5-03-reviewed-seeds-2026-11-16.txt"),
    }
    mismatches = [(name, PINS[name], value) for name, value in actuals.items() if PINS[name] != value]
    if mismatches:
        for name, expected, actual in mismatches:
            print(f"{name}: expected {expected}, got {actual}", file=sys.stderr)
        return 1
    print(json.dumps({"status": "verified", **actuals}, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
