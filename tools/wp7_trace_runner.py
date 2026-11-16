#!/usr/bin/env python3
"""Provenance-guarded launcher for frozen WP-E5-7 fief-match manifests.

The runner intentionally does not choose seeds or alter simulation arguments. A
manifest is a JSON object containing candidate hashes and a non-empty `seeds`
array. Each seed is run once into a fresh directory and its raw output is hashed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone

PROTOCOL = "WP-E5-7/GOV-11"
PROVISIONAL_CRITERIA = "autonomous_d20_working_criteria_not_separately_user_approved"


def sha256(path):
    path = Path(path)
    h = hashlib.sha256()
    if path.is_dir():
        for item in sorted(path.rglob("*")):
            if not item.is_file() or any(part in {".git", "bin", "obj", ".godot", "__pycache__"} for part in item.relative_to(path).parts):
                continue
            rel = item.relative_to(path).as_posix().encode("utf-8")
            h.update(len(rel).to_bytes(8, "big")); h.update(rel)
            with item.open("rb") as f:
                for block in iter(lambda: f.read(1024 * 1024), b""):
                    h.update(block)
    else:
        with path.open("rb") as f:
            for block in iter(lambda: f.read(1024 * 1024), b""):
                h.update(block)
    return h.hexdigest()


def git_head(source):
    source = Path(source).resolve()
    if not source.is_dir():
        raise ValueError("source candidate must be the repository root directory")
    top = subprocess.run(["git", "-C", str(source), "rev-parse", "--show-toplevel"],
                         check=True, capture_output=True, text=True).stdout.strip()
    if Path(top).resolve() != source:
        raise ValueError("source candidate must be the Git worktree root")
    return subprocess.run(["git", "-C", str(source), "rev-parse", "HEAD"],
                          check=True, capture_output=True, text=True).stdout.strip()


def read_seed_file(path):
    values = []
    for line_no, line in enumerate(Path(path).read_text(encoding="utf-8").splitlines(), 1):
        value = line.strip()
        if not value:
            continue
        if not value.isdigit():
            raise ValueError(f"seed file line {line_no} is not a nonnegative integer")
        values.append(int(value))
    if not values or len(values) != len(set(values)):
        raise ValueError("seed file must contain distinct nonnegative seeds")
    return values


def verify(manifest, candidates, cohort, seed_file):
    required = ("source", "pack", "harness", "sim")
    expected = manifest.get("hashes")
    if not isinstance(expected, dict) or any(k not in expected for k in required):
        raise ValueError("manifest hashes must include source, pack, harness, and sim")
    for key in required:
        actual = sha256(candidates[key])
        if actual.lower() != str(expected[key]).lower():
            raise ValueError(f"{key} hash mismatch: expected {expected[key]}, got {actual}")
    actual_head = git_head(candidates["source"])
    if manifest.get("head") != actual_head:
        raise ValueError(f"source HEAD mismatch: expected {manifest.get('head')}, got {actual_head}")
    if manifest.get("protocol") != PROTOCOL or manifest.get("cohort") != cohort:
        raise ValueError("manifest protocol/cohort mismatch")
    if manifest.get("criteria_status") != PROVISIONAL_CRITERIA:
        raise ValueError("manifest must preserve D20's autonomous provisional status")
    actual_runner_sha = sha256(Path(__file__).resolve())
    if str(manifest.get("runner_sha256", "")).lower() != actual_runner_sha.lower():
        raise ValueError(f"runner hash mismatch: expected {manifest.get('runner_sha256')}, got {actual_runner_sha}")
    seeds = manifest.get("seeds")
    expected_count = {"calibration": 20, "holdout": 80}[cohort]
    if not isinstance(seeds, list) or len(seeds) != expected_count or any(type(s) is not int or s < 0 for s in seeds):
        raise ValueError(f"{cohort} manifest must contain exactly {expected_count} nonnegative seeds")
    if len(set(seeds)) != expected_count:
        raise ValueError("manifest contains duplicate seeds")
    seed_file = Path(seed_file)
    if sha256(seed_file) != manifest.get("seed_file_sha256"):
        raise ValueError("preregistered seed-file SHA-256 mismatch")
    if read_seed_file(seed_file) != seeds:
        raise ValueError("manifest seeds do not exactly match the preregistered seed file")
    minutes = manifest.get("minutes")
    if minutes != 130:
        raise ValueError("WP7 GOV-11 manifest must freeze the established 130-minute horizon")
    if Path(candidates["harness"]).name != "CivSim.Harness.dll" or "Release" not in Path(candidates["harness"]).parts:
        raise ValueError("harness must be the Release CivSim.Harness.dll")
    if Path(candidates["sim"]).name != "CivSim.Sim.dll" or Path(candidates["sim"]).parent != Path(candidates["harness"]).parent:
        raise ValueError("sim must be the adjacent Release CivSim.Sim.dll")
    return {"head": actual_head, "hashes": {k: sha256(candidates[k]) for k in required}}


def run(manifest_path, candidates, output, *, cohort, seed_file, manifest_sha256,
        dotnet="dotnet", preflight_only=False):
    manifest_path = Path(manifest_path)
    actual_manifest_sha = sha256(manifest_path)
    if actual_manifest_sha.lower() != manifest_sha256.lower():
        raise ValueError(f"manifest SHA-256 mismatch: expected {manifest_sha256}, got {actual_manifest_sha}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    seed_file = Path(seed_file)
    pre_hashes = verify(manifest, candidates, cohort, seed_file)
    output = Path(output)
    if output.exists():
        raise ValueError(f"output path already exists: {output}")
    source_root = Path(candidates["source"]).resolve()
    if output.resolve() == source_root or source_root in output.resolve().parents:
        raise ValueError("output root must be outside the source tree to prevent provenance drift")
    output.mkdir(parents=True)
    runmeta = {"protocol": PROTOCOL, "cohort": cohort, "criteria_status": PROVISIONAL_CRITERIA,
               "manifest_sha256": actual_manifest_sha, "seed_file": str(seed_file.resolve()),
               "seed_file_sha256": sha256(seed_file), "head": pre_hashes["head"],
               "pre_hashes": pre_hashes["hashes"], "post_hashes": None,
               "minutes": manifest["minutes"], "runner_sha256": manifest["runner_sha256"],
               "output": str(output.resolve()), "seeds": []}
    (output / "manifest.json").write_bytes(manifest_path.read_bytes())
    (output / "seed-file.txt").write_bytes(seed_file.read_bytes())
    if preflight_only:
        runmeta["preflight"] = "passed"
        runmeta["post_hashes"] = verify(manifest, candidates, cohort, seed_file)["hashes"]
        (output / "runner.json").write_text(json.dumps(runmeta, indent=2) + "\n", encoding="utf-8")
        return runmeta
    try:
        for seed in manifest["seeds"]:
            before = verify(manifest, candidates, cohort, seed_file)
            raw = output / f"seed-{seed}.log"
            command = [dotnet, candidates["harness"], "fief-match", "--pack", candidates["pack"],
                       "--seed", str(seed), "--minutes", str(manifest["minutes"]), "--fief-wp7-paired"]
            entry = {"seed": seed, "status": "running", "raw_file": raw.name,
                     "start_utc": datetime.now(timezone.utc).isoformat(), "pre_hashes": before["hashes"],
                     "command": command}
            runmeta["seeds"].append(entry)
            (output / "runner.json").write_text(json.dumps(runmeta, indent=2) + "\n", encoding="utf-8")
            try:
                with raw.open("w", encoding="utf-8") as raw_stream:
                    proc = subprocess.run(command, stdout=raw_stream, stderr=subprocess.STDOUT)
                after = verify(manifest, candidates, cohort, seed_file)
                terminals = [line for line in raw.read_text(encoding="utf-8", errors="replace").splitlines()
                             if line.startswith("wp7-match-terminal ")]
                terminal_ok = len(terminals) == 1 and f"seed={seed}" in terminals[0].split()
                entry.update({"status": "complete" if proc.returncode == 0 and terminal_ok else "error",
                              "exit": proc.returncode, "terminal_row_count": len(terminals),
                              "terminal_row_valid": terminal_ok, "sha256": sha256(raw),
                              "byte_size": raw.stat().st_size, "post_hashes": after["hashes"],
                              "end_utc": datetime.now(timezone.utc).isoformat()})
                if proc.returncode != 0 or not terminal_ok:
                    raise RuntimeError(f"seed {seed} failed or lacked exactly one matching terminal row; raw output preserved")
            except Exception as e:
                entry["status"] = "error"
                entry["failure"] = str(e)
                entry["sha256"] = sha256(raw) if raw.exists() else None
                entry["byte_size"] = raw.stat().st_size if raw.exists() else 0
                entry["end_utc"] = datetime.now(timezone.utc).isoformat()
                raise
            finally:
                (output / "runner.json").write_text(json.dumps(runmeta, indent=2) + "\n", encoding="utf-8")
    finally:
        try:
            runmeta["post_hashes"] = verify(manifest, candidates, cohort, seed_file)["hashes"]
        except Exception as e:
            runmeta["post_hash_error"] = str(e)
        (output / "runner.json").write_text(json.dumps(runmeta, indent=2) + "\n", encoding="utf-8")
    return runmeta


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--manifest", required=True)
    p.add_argument("--source", required=True)
    p.add_argument("--pack", required=True)
    p.add_argument("--harness", required=True, help="Release Harness DLL")
    p.add_argument("--sim", required=True, help="adjacent Sim DLL")
    p.add_argument("--output", required=True)
    p.add_argument("--cohort", required=True, choices=("calibration", "holdout"))
    p.add_argument("--seed-file", required=True)
    p.add_argument("--manifest-sha256", required=True)
    p.add_argument("--dotnet", default="dotnet")
    p.add_argument("--preflight-only", action="store_true", help="verify hashes and fresh output without launching matches")
    a = p.parse_args()
    candidates = {k: getattr(a, k) for k in ("source", "pack", "harness", "sim")}
    try:
        result = run(a.manifest, candidates, a.output, cohort=a.cohort, seed_file=a.seed_file,
                     manifest_sha256=a.manifest_sha256, dotnet=a.dotnet, preflight_only=a.preflight_only)
        print(json.dumps(result, indent=2))
        return 0
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError) as exc:
        print(f"wp7 runner: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
