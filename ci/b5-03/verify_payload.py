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
    "source_head": "8774ebdfa7dfe078fbcf366390bd5d830db6769a",
    "source_tree": "b76ab434580f7ebbf77d0799c92cce5b255e3f49",
    "source_aggregate": "840c1bf2d8824815b9f1ef11b9768c2f28b39812d9fb7c0e3b4ba49008bd914a",
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
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--write-manifest", type=Path)
    args = parser.parse_args()
    payload, source = args.payload.resolve(), args.source.resolve()
    if bool(args.manifest) == bool(args.write_manifest):
        parser.error("provide exactly one of --manifest or --write-manifest")

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
    mismatches = [(name, PINS[name], actuals[name]) for name in PINS if PINS[name] != actuals[name]]
    if mismatches:
        for name, expected, actual in mismatches:
            print(f"{name}: expected {expected}, got {actual}", file=sys.stderr)
        return 1

    if args.write_manifest:
        manifest_path = args.write_manifest.resolve()
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps({"schema": 1, **actuals}, indent=2, sort_keys=True) + "\n")
        print(json.dumps({"status": "frozen", "manifest": str(manifest_path), **actuals}, indent=2, sort_keys=True))
        return 0

    manifest_path = args.manifest.resolve()
    expected = json.loads(manifest_path.read_text())
    payload_keys = ("pack", "harness", "sim", "tests")
    payload_mismatches = [(name, expected.get(name), actuals[name])
                          for name in payload_keys if expected.get(name) != actuals[name]]
    if payload_mismatches:
        for name, expected_hash, actual_hash in payload_mismatches:
            print(f"{name}: manifest expected {expected_hash}, got {actual_hash}", file=sys.stderr)
        return 1
    print(json.dumps({"status": "verified", **actuals}, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
