#!/usr/bin/env python3
"""Run opt-in Fief diagnostics only against a complete, hash-frozen candidate.

Manifest JSON schema: {source_tree_sha256, pack_sha256, harness_sha256, sim_sha256,
source_manifest_sha256, source_manifest}. The source manifest is the exact JSON emitted by
docs/MVP/balance/tools/hash_e5_workspace.py.
Every expected identity must be present; this tool never builds artifacts or reuses outputs.
"""
from __future__ import annotations
import argparse, hashlib, json, os, signal, subprocess, sys
from datetime import datetime, timezone
from pathlib import Path

class RunInterrupted(Exception):
    def __init__(self, signum: int):
        self.signum = signum
        super().__init__(f"signal {signum}")

def append_event(path: Path, event: dict) -> None:
    event = {"time_utc": datetime.now(timezone.utc).isoformat(), **event}
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(event, sort_keys=True) + "\n")

def run_frozen_command(cmd: list[str], stdout) -> int:
    process = None
    def stop(signum, _frame): raise RunInterrupted(signum)
    previous = {sig: signal.signal(sig, stop) for sig in (signal.SIGINT, signal.SIGTERM)}
    try:
        process = subprocess.Popen(cmd, stdout=stdout, stderr=subprocess.STDOUT,
                                   start_new_session=(os.name == "posix"))
        return process.wait()
    except RunInterrupted as interrupted:
        if process is not None and process.poll() is None:
            if os.name == "posix": os.killpg(process.pid, signal.SIGTERM)
            else: process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                if os.name == "posix": os.killpg(process.pid, signal.SIGKILL)
                else: process.kill()
                process.wait()
        raise interrupted
    finally:
        for sig, handler in previous.items(): signal.signal(sig, handler)

def sha(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""): h.update(block)
    return h.hexdigest()

BUILD_DIRS = {"bin", "obj", "build", "dist", ".godot", ".vs", "__pycache__"}
SOURCE_ROOTS = ("content/", "src/", "game/")

def git_paths(root: Path) -> set[str]:
    """Match hash_e5_workspace.py: tracked plus non-ignored untracked files."""
    tracked = subprocess.run(["git", "-C", str(root), "ls-files", "-z"], check=True,
                             stdout=subprocess.PIPE).stdout.decode().split("\0")
    untracked = subprocess.run(["git", "-C", str(root), "ls-files", "--others",
                                "--exclude-standard", "-z"], check=True,
                               stdout=subprocess.PIPE).stdout.decode().split("\0")
    paths = {p for p in tracked + untracked if p.startswith(SOURCE_ROOTS)}
    return {p for p in paths if not (set(Path(p).parts) & BUILD_DIRS)}

def source_tree_hash(root: Path, manifest: dict) -> str:
    files = manifest.get("files")
    head = manifest.get("head")
    if not isinstance(files, list) or not files or not isinstance(head, str) or not head:
        raise ValueError("source manifest must be the canonical hash_e5_workspace.py JSON")
    root = root.resolve()
    records = {record.get("path"): record for record in files if isinstance(record, dict)}
    if len(records) != len(files) or list(records) != sorted(records):
        raise ValueError("source manifest paths must be unique and sorted")
    expected = {rel for rel, record in records.items() if record.get("sha256") is not None}
    actual = git_paths(root)
    if expected != actual: raise ValueError("source manifest must cover exactly every non-build file under content/, src/, and game/")
    for rel, record in records.items():
        p = (root / rel).resolve()
        if root not in p.parents: raise ValueError(f"source path escapes root: {rel}")
        digest = record.get("sha256")
        if digest is None:
            if p.exists(): raise ValueError(f"source deletion differs from manifest: {rel}")
        elif not p.is_file() or sha(p) != digest:
            raise ValueError(f"source file missing or hash mismatch: {rel}")
    payload = {"head": head, "files": files}
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()
    actual_hash = hashlib.sha256(canonical).hexdigest()
    if manifest.get("aggregate_sha256") != actual_hash: raise ValueError("source aggregate does not match its canonical source manifest")
    return actual_hash

def validate(manifest_path: Path, root: Path, pack: Path, harness: Path, sim: Path,
             test_assembly: Path | None = None, expected_test_assembly_sha256: str | None = None) -> dict:
    m = json.loads(manifest_path.read_text())
    required = ("source_tree_sha256", "pack_sha256", "harness_sha256", "sim_sha256", "source_manifest_sha256", "source_manifest")
    if any(not m.get(k) for k in required): raise ValueError("provenance manifest is incomplete; all source/pack/Harness/Sim hashes are required")
    sm = (manifest_path.parent / m["source_manifest"]).resolve()
    if sha(sm) != m["source_manifest_sha256"]: raise ValueError("source manifest hash mismatch")
    if source_tree_hash(root, json.loads(sm.read_text())) != m["source_tree_sha256"]: raise ValueError("source tree hash mismatch")
    for label, path, key in (("pack", pack, "pack_sha256"), ("Harness", harness, "harness_sha256"), ("Sim", sim, "sim_sha256")):
        if not path.is_file() or sha(path) != m[key]: raise ValueError(f"{label} missing or hash mismatch")
    if sim.resolve() != (harness.parent / "CivSim.Sim.dll").resolve():
        raise ValueError("Sim must be the adjacent CivSim.Sim.dll loaded by the frozen Harness")
    if test_assembly is not None:
        if not expected_test_assembly_sha256:
            raise ValueError("combined cohort requires an expected test-assembly SHA-256")
        if not test_assembly.is_file() or sha(test_assembly) != expected_test_assembly_sha256:
            raise ValueError("test assembly missing or hash mismatch")
    return m

def read_seeds(path: Path) -> list[int]:
    vals = [int(x.strip()) for x in path.read_text().splitlines()
            if x.strip() and not x.strip().startswith("#") and x.strip().lower() != "seed"]
    if not vals or len(vals) != len(set(vals)) or any(n < 0 for n in vals): raise ValueError("seed file must contain unique nonnegative seeds")
    return vals

TRACE_FLAGS = {
    "power": "--fief-power-ledger",
    "health": "--fief-health-trace",
    "institution": "--fief-institution-trace",
    "wp7": "--fief-wp7-paired",
    "sea10": "--fief-sea10-trace",
    "territory": "--fief-territory-trace",
    "siege": "--fief-siege-trace",
    "siege-strategist": "--fief-siege-strategist-trace",
}
COMBINED_E5_TRACES = ("power", "health", "institution", "wp7", "sea10", "territory", "siege", "siege-strategist")

def parse_combined_power_ledger(raw_path: Path, seed: int) -> dict:
    """Reuse the F-T2 fail-closed parser for power-ledger rows in combined runs."""
    tools_dir = Path(__file__).resolve().parents[1] / "docs" / "MVP" / "balance" / "tools"
    if str(tools_dir) not in sys.path:
        sys.path.insert(0, str(tools_dir))
    import run_fief_ft2_natural_ledger
    return run_fief_ft2_natural_ledger.parse_result(raw_path.read_text(encoding="utf-8", errors="replace"), seed)

def parse_combined_wp7(raw_path: Path, seed: int) -> dict:
    """Reuse the WP7 fail-closed terminal and paired-row parser."""
    tools_dir = Path(__file__).resolve().parent
    if str(tools_dir) not in sys.path:
        sys.path.insert(0, str(tools_dir))
    from wp7_trace_runner import parse_output
    return parse_output(raw_path.read_text(encoding="utf-8", errors="replace"), seed)

def combined_candidate_metadata(source_root: Path, pack: Path, harness: Path, sim: Path,
                               provenance_path: Path, provenance: dict, seed_manifest_path: Path,
                               seed_manifest_sha: str, runner_sha: str) -> dict:
    """Capture the fields consumed by the F-T1 80-world combined-output analyzer."""
    tools_dir = Path(__file__).resolve().parents[1] / "docs" / "MVP" / "balance" / "tools"
    if str(tools_dir) not in sys.path:
        sys.path.insert(0, str(tools_dir))
    from run_fief_ft1_calibration import read_base_cost_from_eras, record_hash
    eras = source_root / "content" / "registries" / "eras.yaml"
    if not eras.is_file():
        raise ValueError(f"E5 era registry is missing: {eras}")
    source_base = read_base_cost_from_eras(eras)
    try:
        pack_data = json.loads(pack.read_text(encoding="utf-8"))
        pack_eras = pack_data["registries"]["eras"]
        pack_base = int(next(row for row in pack_eras if int(row["id"]) == 5)["base_cost"])
    except (OSError, KeyError, TypeError, ValueError, StopIteration, json.JSONDecodeError) as exc:
        raise ValueError(f"could not verify E5 base_cost in numeric pack {pack}") from exc
    if source_base != pack_base:
        raise ValueError(f"E5 source/pack base_cost mismatch: {source_base} != {pack_base}")
    source_manifest_path = (provenance_path.resolve().parent / provenance["source_manifest"]).resolve()
    source_manifest = json.loads(source_manifest_path.read_text(encoding="utf-8"))
    head = source_manifest.get("head")
    if not isinstance(head, str) or not head:
        raise ValueError("source manifest has no frozen HEAD")
    base_record_hash = record_hash({"id": 5, "base_cost": source_base})
    pre_post = {"headCommit": head, "sourceAggregateSha256": provenance["source_tree_sha256"],
                "packSha256": provenance["pack_sha256"], "harnessDllSha256": provenance["harness_sha256"],
                "simDllSha256": provenance["sim_sha256"], "runnerSha256": runner_sha,
                "erasSha256": sha(eras), "manifestSha256": seed_manifest_sha,
                "manifestPath": str(seed_manifest_path.resolve())}
    pre_post.update({"e5BaseCost": source_base, "packE5BaseCost": pack_base,
                     "e5BaseCostRecordSha256": base_record_hash,
                     "packE5BaseCostRecordSha256": base_record_hash})
    return {"headCommit": head, "workspaceAggregateSha256": provenance["source_tree_sha256"],
            "sourceAggregateSha256": provenance["source_tree_sha256"], "sourceManifestSha256": seed_manifest_sha,
            "packSha256": provenance["pack_sha256"], "harnessDllSha256": provenance["harness_sha256"],
            "simDllSha256": provenance["sim_sha256"], "runnerSha256": runner_sha,
            "fiefEraBaseCost": source_base, "e5BaseCost": source_base, "packE5BaseCost": pack_base,
            "e5BaseCostRecordSha256": base_record_hash, "packE5BaseCostRecordSha256": base_record_hash,
            "erasPath": str(eras.resolve()), "erasSha256": sha(eras),
            "provenanceBefore": pre_post, "provenanceAfter": pre_post}

def selected_trace_flags(args) -> list[str]:
    """Return a stable, nonempty selection while preserving legacy trace_kind callers."""
    kinds = getattr(args, "trace_kinds", None)
    if kinds is None:
        kinds = [getattr(args, "trace_kind", "sea10")]
    if not kinds or len(kinds) != len(set(kinds)):
        raise ValueError("select one or more distinct trace kinds")
    unknown = set(kinds) - TRACE_FLAGS.keys()
    if unknown:
        raise ValueError(f"unsupported trace kind: {sorted(unknown)[0]}")
    # Keep command and freeze ordering deterministic regardless of CLI argument order.
    return [TRACE_FLAGS[kind] for kind in TRACE_FLAGS if kind in kinds]

def run(args) -> int:
    combined = bool(getattr(args, "combined_e5_traces", False))
    test_assembly = getattr(args, "test_assembly", None)
    expected_test_sha = getattr(args, "expected_test_assembly_sha256", None)
    expected_manifest_sha = getattr(args, "expected_manifest_sha256", None)
    expected_runner_sha = getattr(args, "expected_runner_sha256", None)
    calibration_seeds_path = getattr(args, "calibration_seeds", None)
    expected_calibration_sha = getattr(args, "expected_calibration_manifest_sha256", None)
    if combined:
        if args.minutes != 130:
            raise ValueError("combined WP-E5 natural cohort is fixed at 130 minutes")
        if test_assembly is None or not expected_test_sha:
            raise ValueError("combined cohort requires --test-assembly and --expected-test-assembly-sha256")
        if (not expected_manifest_sha or not expected_runner_sha or calibration_seeds_path is None or
                not expected_calibration_sha):
            raise ValueError("combined cohort requires calibration/held-out manifest and runner SHA-256 pins")
        if sha(Path(__file__).resolve()) != expected_runner_sha:
            raise ValueError("runner SHA-256 mismatch")
        if not args.seeds.is_file() or sha(args.seeds) != expected_manifest_sha:
            raise ValueError("seed manifest missing or SHA-256 mismatch")
        if not calibration_seeds_path.is_file() or sha(calibration_seeds_path) != expected_calibration_sha:
            raise ValueError("calibration seed manifest missing or SHA-256 mismatch")
        args.trace_kinds = list(COMBINED_E5_TRACES)
        args.test_assembly = args.test_assembly.resolve()
        test_assembly = args.test_assembly
    def validate_combined_pins() -> None:
        if not combined:
            return
        if sha(Path(__file__).resolve()) != expected_runner_sha:
            raise ValueError("runner SHA-256 mismatch during run")
        if not args.seeds.is_file() or sha(args.seeds) != expected_manifest_sha:
            raise ValueError("seed manifest missing or SHA-256 mismatch during run")
        if not calibration_seeds_path.is_file() or sha(calibration_seeds_path) != expected_calibration_sha:
            raise ValueError("calibration seed manifest missing or SHA-256 mismatch during run")
    manifest = validate(args.provenance, args.source_root, args.pack, args.harness, args.sim,
                        test_assembly if combined else None, expected_test_sha)
    validate_combined_pins()
    seed_file_hash = sha(args.seeds)
    seeds = read_seeds(args.seeds)
    if combined and len(seeds) != 80:
        raise ValueError(f"combined WP-E5 held-out cohort requires exactly 80 seeds; found {len(seeds)}")
    calibration_seeds = read_seeds(calibration_seeds_path) if combined else []
    if set(seeds) & set(calibration_seeds):
        raise ValueError("held-out and calibration seed manifests overlap")
    if sha(args.seeds) != seed_file_hash: raise ValueError("seed file changed while being frozen")
    candidate_metadata = (combined_candidate_metadata(args.source_root.resolve(), args.pack.resolve(),
                         args.harness.resolve(), args.sim.resolve(), args.provenance, manifest,
                         args.seeds, seed_file_hash, expected_runner_sha) if combined else {})
    if args.output.exists(): raise ValueError(f"output path already exists; refusing overwrite: {args.output}")
    args.output.mkdir(parents=True, exist_ok=False)
    trace_flags = selected_trace_flags(args)
    legacy_trace = trace_flags[0] if len(trace_flags) == 1 else None
    freeze = {"provenance": manifest, "source_root": str(args.source_root.resolve()), "pack": str(args.pack.resolve()),
             "harness": str(args.harness.resolve()), "sim": str(args.sim.resolve()), "minutes": args.minutes,
             "runner": str(Path(__file__).resolve()), "runner_sha256": sha(Path(__file__).resolve()),
             "output": str(args.output.resolve()),
             "seed_file": str(args.seeds.resolve()), "seed_file_sha256": seed_file_hash,
             "seeds": seeds, "trace": legacy_trace, "traces": trace_flags}
    if combined:
        freeze.update({"cohort_role": "held_out", "test_assembly": str(test_assembly),
                       "test_assembly_sha256": expected_test_sha,
                       "expected_manifest_sha256": expected_manifest_sha,
                       "expected_runner_sha256": expected_runner_sha,
                       "calibration_seed_file": str(calibration_seeds_path.resolve()),
                       "calibration_seed_file_sha256": expected_calibration_sha,
                       "calibration_seeds": calibration_seeds})
    with (args.output / "run-freeze.json").open("x", encoding="utf-8") as f:
        json.dump(freeze, f, indent=2, sort_keys=True); f.write("\n")
    run_status = args.output / "run-status.jsonl"
    run_status.open("x", encoding="utf-8").close()
    if combined:
        import shutil
        manifest_copy_name = "heldout-seeds.txt"
        shutil.copyfile(args.seeds, args.output / manifest_copy_name)
        run_metadata = {
            "status": "running", "combinedNaturalTraceCohort": True, "cohortRole": "held_out",
            "traceFlags": trace_flags, "manifestPath": str(args.seeds.resolve()),
            "manifestSha256": seed_file_hash, "expectedManifestSha256": seed_file_hash,
            "sourceManifestSha256": seed_file_hash, "manifestCopy": manifest_copy_name,
            "seeds": seeds, "seedCount": len(seeds), "minutes": args.minutes, "grantE5": False,
            "rawOutput": str((args.output / "combined-output.txt").resolve()),
            "runFreezePath": str((args.output / "run-freeze.json").resolve()),
            "sourceRoot": str(args.source_root.resolve()), "provenancePath": str(args.provenance.resolve()),
            "packPath": str(args.pack.resolve()), "harnessDllPath": str(args.harness.resolve()),
            "simDllPath": str(args.sim.resolve()), "testAssemblyPath": str(test_assembly),
            "testAssemblySha256": expected_test_sha, "runnerSha256": expected_runner_sha,
            "provenanceBefore": candidate_metadata["provenanceBefore"],
            "provenanceAfter": candidate_metadata["provenanceAfter"],
            "fiefEraBaseCost": candidate_metadata["fiefEraBaseCost"],
            "e5BaseCost": candidate_metadata["e5BaseCost"],
            "packE5BaseCost": candidate_metadata["packE5BaseCost"],
            "e5BaseCostRecordSha256": candidate_metadata["e5BaseCostRecordSha256"],
            "packE5BaseCostRecordSha256": candidate_metadata["packE5BaseCostRecordSha256"],
            "erasPath": candidate_metadata["erasPath"], "erasSha256": candidate_metadata["erasSha256"],
            "workspaceAggregateSha256": candidate_metadata["workspaceAggregateSha256"],
            "sourceAggregateSha256": candidate_metadata["sourceAggregateSha256"],
            "packSha256": manifest["pack_sha256"], "harnessDllSha256": manifest["harness_sha256"],
            "simDllSha256": manifest["sim_sha256"],
            "protocol": {"naturalFief": True, "powerLedger": True, "grantE5": False,
                         "townWindowSeasonsInclusive": 12,
                         "grainCoverageMeasure": "adultEquivalentCapacityProxy",
                         "secondWindmillAllowed": True}, "runs": []}
        (args.output / "run_metadata.json").write_text(json.dumps(run_metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    append_event(run_status, {"status": "running", "seed_count": len(seeds),
                              "trace": legacy_trace, "traces": trace_flags})
    current_seed = None
    current_status = None
    current_output = None
    current_exit_code = None
    try:
        for seed in seeds:
            validate_combined_pins()
            current_seed = seed
            validate(args.provenance, args.source_root, args.pack, args.harness, args.sim,
                     test_assembly if combined else None, expected_test_sha)
            out = args.output / f"seed-{seed}.txt"
            current_output = out
            out.open("x").close()
            current_status = args.output / f"seed-{seed}.status.jsonl"
            current_status.open("x", encoding="utf-8").close()
            cmd = ["dotnet", str(args.harness), "fief-match", "--pack", str(args.pack), "--seed", str(seed),
                   "--minutes", str(args.minutes), *trace_flags]
            append_event(current_status, {"seed": seed, "status": "running", "command": cmd})
            with out.open("ab") as stdout:
                exit_code = run_frozen_command(cmd, stdout)
            current_exit_code = exit_code
            validate(args.provenance, args.source_root, args.pack, args.harness, args.sim,
                     test_assembly if combined else None, expected_test_sha)
            power_parse = parse_combined_power_ledger(out, seed) if combined and exit_code == 0 else None
            wp7_parse = parse_combined_wp7(out, seed) if combined and exit_code == 0 else None
            quality_failure = bool(power_parse and power_parse["parseErrors"])
            status = "completed" if exit_code == 0 and not quality_failure else "failed"
            append_event(current_status, {"seed": seed, "status": status, "exit_code": exit_code, "output_sha256": sha(out)})
            if combined:
                metadata_path = args.output / "run_metadata.json"
                run_metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
                run_metadata["runs"].append({"seed": seed, "status": "complete" if status == "completed" else "failed",
                                             "exitCode": exit_code,
                                             "rawFile": out.name, "rawSha256": sha(out), "command": cmd,
                                             "provenanceBefore": candidate_metadata["provenanceBefore"],
                                             "provenanceAfter": candidate_metadata["provenanceAfter"],
                                             **(power_parse or {}), "wp7Accounting": wp7_parse})
                run_metadata["status"] = "complete" if len(run_metadata["runs"]) == len(seeds) else "running"
                metadata_path.write_text(json.dumps(run_metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            if exit_code != 0 or quality_failure:
                append_event(run_status, {"status": "failed", "seed": seed, "exit_code": exit_code,
                                          "output_sha256": sha(out),
                                          "reason": "power_ledger_validation" if quality_failure else "nonzero_exit"})
                return 1
        validate_combined_pins()
        if combined:
            combined_path = args.output / "combined-output.txt"
            with combined_path.open("x", encoding="utf-8", newline="\n") as combined_output:
                for seed in seeds:
                    combined_output.write((args.output / f"seed-{seed}.txt").read_text(encoding="utf-8", errors="replace"))
            run_metadata = json.loads((args.output / "run_metadata.json").read_text(encoding="utf-8"))
            run_metadata["rawOutputSha256"] = sha(combined_path)
            (args.output / "run_metadata.json").write_text(json.dumps(run_metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        append_event(run_status, {"status": "completed", "seed_count": len(seeds)})
        return 0
    except RunInterrupted as interrupted:
        if current_status is not None:
            append_event(current_status, {"seed": current_seed, "status": "interrupted", "signal": interrupted.signum,
                                          "output_sha256": sha(current_output) if current_output and current_output.exists() else None})
        append_event(run_status, {"status": "interrupted", "seed": current_seed, "signal": interrupted.signum})
        return 128 + interrupted.signum
    except BaseException as error:
        if current_status is not None:
            append_event(current_status, {"seed": current_seed, "status": "failed", "reason": type(error).__name__,
                                          "detail": str(error),
                                          "exit_code": current_exit_code,
                                          "output_sha256": sha(current_output) if current_output and current_output.exists() else None})
        append_event(run_status, {"status": "failed", "seed": current_seed, "reason": type(error).__name__, "detail": str(error)})
        raise

def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--provenance", type=Path, required=True); p.add_argument("--source-root", type=Path, required=True)
    p.add_argument("--pack", type=Path, required=True); p.add_argument("--harness", type=Path, required=True)
    p.add_argument("--sim", type=Path, required=True); p.add_argument("--seeds", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True); p.add_argument("--minutes", type=int, required=True)
    trace_group = p.add_mutually_exclusive_group()
    trace_group.add_argument("--trace-kind", choices=tuple(TRACE_FLAGS), default="sea10",
                             help="legacy single-trace option (default: sea10)")
    trace_group.add_argument("--trace-kinds", choices=tuple(TRACE_FLAGS), nargs="+",
                             help="capture this selected subset of compatible natural trace kinds")
    trace_group.add_argument("--combined-e5-traces", action="store_true",
                             help="run all seven compatible traces for a frozen 130-minute held-out WP-E5 natural cohort")
    p.add_argument("--test-assembly", type=Path, help="exact Release CivSim.Tests.dll for --combined-e5-traces")
    p.add_argument("--expected-test-assembly-sha256")
    p.add_argument("--expected-manifest-sha256")
    p.add_argument("--expected-runner-sha256")
    p.add_argument("--calibration-seeds", type=Path, help="disjoint preregistered calibration seed block for held-out runs")
    p.add_argument("--expected-calibration-manifest-sha256")
    a = p.parse_args()
    if a.minutes <= 0: p.error("--minutes must be positive")
    try: return run(a)
    except (ValueError, OSError, RuntimeError, json.JSONDecodeError) as e: print(f"Fief trace runner: {e}", file=sys.stderr); return 2
if __name__ == "__main__": raise SystemExit(main())
