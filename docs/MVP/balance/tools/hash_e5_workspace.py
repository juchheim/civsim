#!/usr/bin/env python3
"""Hash the current E5 source/content workspace for reproducible cohort provenance.

Usage from any directory inside the repository:
    python3 docs/MVP/balance/tools/hash_e5_workspace.py
    python3 docs/MVP/balance/tools/hash_e5_workspace.py --output provenance.json

The JSON records HEAD plus every tracked or non-ignored untracked file under
content/, src/, and game/. File hashes are taken from the working tree, so
local modifications are represented even when HEAD itself is unchanged. The
aggregate is SHA-256 of canonical JSON containing HEAD and the sorted file
records (excluding the aggregate field itself).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import sys
from pathlib import Path


ROOTS = ("content/", "src/", "game/")
BUILD_DIRS = {"bin", "obj", "build", "dist", ".godot", ".vs", "__pycache__"}


def git(root: Path, *args: str) -> bytes:
    return subprocess.run(
        ["git", "-C", str(root), *args], check=True, stdout=subprocess.PIPE
    ).stdout


def in_scope(path: str) -> bool:
    if not path.startswith(ROOTS):
        return False
    return not any(part in BUILD_DIRS for part in Path(path).parts)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="write JSON here instead of stdout")
    args = parser.parse_args()

    try:
        root = Path(git(Path.cwd(), "rev-parse", "--show-toplevel").decode().strip())
        head = git(root, "rev-parse", "HEAD").decode().strip()
        tracked = git(root, "ls-files", "-z").decode().split("\0")
        untracked = git(root, "ls-files", "--others", "--exclude-standard", "-z").decode().split("\0")
        statuses = {}
        for record in git(root, "status", "--porcelain=v1", "-z", "--untracked-files=all").decode().split("\0"):
            if not record:
                continue
            code, path = record[:2], record[3:]
            # Porcelain -z uses the destination path first for a rename/copy.
            statuses[path] = code

        paths = sorted({p for p in tracked + untracked if p and in_scope(p)})
        files = []
        for rel in paths:
            path = root / rel
            exists = path.is_file()
            digest = hashlib.sha256(path.read_bytes()).hexdigest() if exists else None
            code = statuses.get(rel, "  ")
            status = "untracked" if code == "??" else (code if code != "  " else "clean")
            files.append({"path": rel, "status": status, "sha256": digest})

        payload = {"head": head, "files": files}
        canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()
        payload["aggregate_sha256"] = hashlib.sha256(canonical).hexdigest()
        rendered = json.dumps(payload, indent=2, sort_keys=True) + "\n"
        if args.output:
            args.output.write_text(rendered, encoding="utf-8")
        else:
            sys.stdout.write(rendered)
    except (subprocess.CalledProcessError, OSError) as exc:
        print(f"hash_e5_workspace: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
