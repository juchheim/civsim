#!/usr/bin/env python3
"""Run the frozen natural F-T5 siege-trace cohort without overwriting raw results."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import subprocess
import sys
from pathlib import Path

EXPECTED_MANIFEST_SHA256 = "f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786"
DEFAULT_MANIFEST = Path(__file__).resolve().parents[1] / "m12-e5-8-f-t5-natural-seeds-2026-11-15.txt"
DEFAULT_HARNESS = Path(__file__).resolve().parents[4] / "src/CivSim.Harness/bin/Release/net10.0/CivSim.Harness.dll"
DEFAULT_OUTPUT = Path(__file__).resolve().parents[1] / "m12-e5-8-f-t5-natural-output-2026-11-15"
FIEF_SIEGE_TRACE_OPTION = "--fief-siege-trace"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def has_fief_siege_trace_option(dll_bytes: bytes) -> bool:
    """Recognize the option literal in common .NET IL string encodings."""
    return any(encoding in dll_bytes for encoding in (
        FIEF_SIEGE_TRACE_OPTION.encode("ascii"),
        FIEF_SIEGE_TRACE_OPTION.encode("utf-16le"),
    ))


def load_manifest(path: Path) -> list[int]:
    seeds: list[int] = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.lower() == "seed":
            continue
        try:
            seed = int(line)
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: expected an integer seed, got {line!r}") from exc
        if not 0 <= seed < 2**64:
            raise ValueError(f"{path}:{line_number}: seed is outside unsigned 64-bit range")
        seeds.append(seed)
    if len(seeds) != 20 or len(set(seeds)) != 20:
        raise ValueError(f"frozen F-T5 manifest must contain 20 unique seeds; found {len(seeds)} rows")
    return seeds


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(description=__doc__)
    result.add_argument("--pack", required=True, type=Path, help="Frozen natural Fief content pack")
    result.add_argument("--expected-pack-sha256", required=True, help="Pack hash recorded during preflight")
    result.add_argument("--harness-dll", type=Path, default=DEFAULT_HARNESS)
    result.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    result.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    result.add_argument("--dotnet", default="dotnet")
    result.add_argument("--minutes", type=int, default=130)
    return result


def run(args: argparse.Namespace) -> int:
    args.pack = args.pack.resolve()
    args.harness_dll = args.harness_dll.resolve()
    args.manifest = args.manifest.resolve()
    args.output_dir = args.output_dir.resolve()
    for path, label in ((args.pack, "content pack"), (args.harness_dll, "Release harness DLL"), (args.manifest, "seed manifest")):
        if not path.is_file():
            raise FileNotFoundError(f"{label} does not exist: {path}")
    if not has_fief_siege_trace_option(args.harness_dll.read_bytes()):
        raise ValueError("Release harness DLL does not contain the F-T5 --fief-siege-trace option; rebuild after the D20 output lock is released")
    manifest_hash = sha256(args.manifest)
    if manifest_hash != EXPECTED_MANIFEST_SHA256:
        raise ValueError(f"frozen manifest hash mismatch: expected {EXPECTED_MANIFEST_SHA256}, got {manifest_hash}")
    seeds = load_manifest(args.manifest)
    if args.minutes != 130:
        raise ValueError("the frozen F-T5 horizon is fixed at 130 minutes")
    if len(args.expected_pack_sha256) != 64 or any(c not in "0123456789abcdefABCDEF" for c in args.expected_pack_sha256):
        raise ValueError("--expected-pack-sha256 must be a 64-character hexadecimal SHA-256")
    pack_hash = sha256(args.pack)
    if pack_hash.lower() != args.expected_pack_sha256.lower():
        raise ValueError(f"content pack hash mismatch: expected {args.expected_pack_sha256}, got {pack_hash}")
    if args.output_dir.exists():
        raise FileExistsError(f"output directory already exists; refusing to overwrite raw results: {args.output_dir}")
    args.output_dir.mkdir(parents=True)

    metadata = {
        "status": "running",
        "startedUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
        "seedCount": len(seeds), "seeds": seeds, "minutes": 130,
        "packPath": str(args.pack), "packSha256": pack_hash,
        "harnessDllPath": str(args.harness_dll), "harnessDllSha256": sha256(args.harness_dll),
        "manifestPath": str(args.manifest), "manifestSha256": manifest_hash,
        "commandTemplate": [args.dotnet, str(args.harness_dll), "fief-match", "--pack", str(args.pack),
                            "--seed", "<manifest-seed>", "--minutes", "130", "--fief-siege-trace"],
        "runs": [],
    }
    metadata_path = args.output_dir / "run_metadata.json"

    def write_metadata() -> None:
        metadata_path.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    write_metadata()
    for seed in seeds:
        raw_path = args.output_dir / f"seed-{seed}.txt"
        command = [args.dotnet, str(args.harness_dll), "fief-match", "--pack", str(args.pack),
                   "--seed", str(seed), "--minutes", "130", "--fief-siege-trace"]
        with raw_path.open("x", encoding="utf-8", newline="\n") as output:
            completed = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT, check=False, text=True)
        run_info = {"seed": seed, "command": command, "rawFile": raw_path.name,
                    "exitCode": completed.returncode, "rawSha256": sha256(raw_path)}
        metadata["runs"].append(run_info)
        write_metadata()
        if completed.returncode != 0:
            metadata["status"] = "failed"
            metadata["failedSeed"] = seed
            write_metadata()
            return completed.returncode
    metadata["status"] = "complete"
    metadata["completedUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    write_metadata()
    return 0


def main() -> int:
    args = parser().parse_args()
    try:
        return run(args)
    except (OSError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
