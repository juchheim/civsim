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

def validate(manifest_path: Path, root: Path, pack: Path, harness: Path, sim: Path) -> dict:
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
    return m

def read_seeds(path: Path) -> list[int]:
    vals = [int(x.strip()) for x in path.read_text().splitlines() if x.strip()]
    if not vals or len(vals) != len(set(vals)) or any(n < 0 for n in vals): raise ValueError("seed file must contain unique nonnegative seeds")
    return vals

TRACE_FLAGS = {
    "sea10": "--fief-sea10-trace",
    "territory": "--fief-territory-trace",
    "siege": "--fief-siege-trace",
}

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
    manifest = validate(args.provenance, args.source_root, args.pack, args.harness, args.sim)
    seed_file_hash = sha(args.seeds)
    seeds = read_seeds(args.seeds)
    if sha(args.seeds) != seed_file_hash: raise ValueError("seed file changed while being frozen")
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
    with (args.output / "run-freeze.json").open("x", encoding="utf-8") as f:
        json.dump(freeze, f, indent=2, sort_keys=True); f.write("\n")
    run_status = args.output / "run-status.jsonl"
    run_status.open("x", encoding="utf-8").close()
    append_event(run_status, {"status": "running", "seed_count": len(seeds),
                              "trace": legacy_trace, "traces": trace_flags})
    current_seed = None
    current_status = None
    current_output = None
    current_exit_code = None
    try:
        for seed in seeds:
            current_seed = seed
            validate(args.provenance, args.source_root, args.pack, args.harness, args.sim)
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
            validate(args.provenance, args.source_root, args.pack, args.harness, args.sim)
            status = "completed" if exit_code == 0 else "failed"
            append_event(current_status, {"seed": seed, "status": status, "exit_code": exit_code, "output_sha256": sha(out)})
            if exit_code != 0:
                append_event(run_status, {"status": "failed", "seed": seed, "exit_code": exit_code,
                                          "output_sha256": sha(out), "reason": "nonzero_exit"})
                return 1
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
    a = p.parse_args()
    if a.minutes <= 0: p.error("--minutes must be positive")
    try: return run(a)
    except (ValueError, OSError, RuntimeError, json.JSONDecodeError) as e: print(f"Fief trace runner: {e}", file=sys.stderr); return 2
if __name__ == "__main__": raise SystemExit(main())
