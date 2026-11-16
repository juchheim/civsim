"""Shared provenance helpers for frozen E5 simulation cohort runners."""
from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Any


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def capture(repo_root: Path, pack: Path, harness: Path, sim: Path) -> dict[str, Any]:
    repo_root = repo_root.resolve()
    hash_tool = repo_root / "docs/MVP/balance/tools/hash_e5_workspace.py"
    with tempfile.TemporaryDirectory(prefix="e5-cohort-provenance-") as tmp:
        output = Path(tmp) / "source.json"
        proc = subprocess.Popen([sys.executable, str(hash_tool), "--output", str(output)],
                                cwd=repo_root, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        _, stderr = proc.communicate()
        if proc.returncode:
            raise subprocess.CalledProcessError(proc.returncode, proc.args, stderr=stderr)
        source = json.loads(output.read_text(encoding="utf-8"))
    proc = subprocess.Popen(["git", "-C", str(repo_root), "rev-parse", "HEAD"],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    head, stderr = proc.communicate()
    if proc.returncode:
        raise subprocess.CalledProcessError(proc.returncode, proc.args, stderr=stderr)
    head = head.strip()
    return {
        "headCommit": head,
        "sourceAggregateSha256": source["aggregate_sha256"],
        "packSha256": sha256(pack.resolve()),
        "harnessDllSha256": sha256(harness.resolve()),
        "simDllSha256": sha256(sim.resolve()),
    }


def verify_expected(actual: dict[str, Any], expected: dict[str, str | None]) -> None:
    mismatches = [f"{key}: expected {value}, got {actual.get(key)}"
                  for key, value in expected.items() if value is not None and actual.get(key) != value]
    if mismatches:
        raise ValueError("frozen cohort provenance mismatch: " + "; ".join(mismatches))
