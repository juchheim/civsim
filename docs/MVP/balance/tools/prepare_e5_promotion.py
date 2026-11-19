#!/usr/bin/env python3
"""Prepare (but never apply) an all-50 E5 status promotion review artifact.

The source content remains untouched. A new output directory receives a complete
content-tree copy with only the 50 E5 `status: draft` fields changed to
`status: numeric`, a unified patch, and provenance. The manifest is an attestation
from reviewers; this tool does not infer or create acceptance.

Evidence entries use a safe POSIX-relative path below --evidence-root (default:
the manifest's parent). Each referenced file must exist and is hash-pinned into
report.json. Hashing verifies identity/existence, not whether reviewers accepted it.
"""
from __future__ import annotations
import argparse, difflib, hashlib, json, math, os, re, shutil, subprocess, sys, tempfile
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any

ID_LINE = re.compile(r"^  - id: ([A-Za-z0-9_-]+)\s*$")
ERA_LINE = re.compile(r"^    era: (\d+)\s*$")
STATUS_LINE = re.compile(r"^(    status: )(draft|numeric|tuned)(\s*)$")
HEX64 = re.compile(r"^[0-9a-fA-F]{64}$")


def catalog_rows(content_root: Path) -> dict[str, tuple[Path, int, str]]:
    found: dict[str, tuple[Path, int, str]] = {}
    for path in sorted((content_root / "catalog").glob("*.yaml")):
        lines = path.read_text(encoding="utf-8").splitlines(keepends=True)
        starts = [i for i, line in enumerate(lines) if ID_LINE.match(line.rstrip("\r\n"))]
        for pos, start in enumerate(starts):
            stop = starts[pos + 1] if pos + 1 < len(starts) else len(lines)
            block = lines[start:stop]
            id_match = ID_LINE.match(lines[start].rstrip("\r\n"))
            assert id_match
            if not any((m := ERA_LINE.match(line.rstrip("\r\n"))) and m.group(1) == "5" for line in block):
                continue
            project_id = id_match.group(1)
            statuses = [(start + off, STATUS_LINE.match(line.rstrip("\r\n")))
                        for off, line in enumerate(block) if line.startswith("    status:")]
            if len(statuses) != 1 or statuses[0][1] is None:
                raise ValueError(f"{path}:{start + 1}: {project_id} must have exactly one recognized status")
            if project_id in found:
                raise ValueError(f"duplicate E5 project ID: {project_id}")
            found[project_id] = (path, statuses[0][0], statuses[0][1].group(2))
    return found


def _valid_number(value: Any) -> bool:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return False
    try:
        return math.isfinite(value)
    except OverflowError:
        return False


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def content_tree_sha256(root: Path) -> str:
    records = [(p.relative_to(root).as_posix(), sha256(p)) for p in sorted(root.rglob("*")) if p.is_file()]
    return hashlib.sha256(json.dumps(records, separators=(",", ":"), ensure_ascii=False).encode()).hexdigest()


def _git(repo: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(repo), *args], check=True, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    return result.stdout.strip()


def source_git_snapshot(repo_root: Path, content_root: Path, expected_head: str) -> dict[str, str]:
    repo_root = repo_root.resolve()
    content_root = content_root.resolve()
    if _git(repo_root, "rev-parse", "--show-toplevel") != str(repo_root):
        raise ValueError("repo root is not the Git worktree root")
    if content_root != (repo_root / "content").resolve():
        raise ValueError("content root must be this worktree's content/ directory")
    actual_head = _git(repo_root, "rev-parse", "HEAD")
    if not expected_head or actual_head != expected_head:
        raise ValueError(f"--head mismatch: expected {expected_head!r}, actual {actual_head!r}")
    dirty = _git(repo_root, "status", "--porcelain=v1", "--untracked-files=all")
    if dirty:
        raise ValueError("source worktree must be clean for promotion preparation")
    return {"head": actual_head, "git_tree": _git(repo_root, "rev-parse", "HEAD^{tree}"),
            "content_tree_sha256": content_tree_sha256(content_root)}


def _evidence_ref(root: Path, reference: str, project_id: str, index: int) -> dict[str, Any]:
    if not isinstance(reference, str) or not reference.strip() or "\\" in reference:
        raise ValueError(f"{project_id}: evidence[{index}] reference must be a relative POSIX file path")
    rel = PurePosixPath(reference)
    if rel.is_absolute() or PureWindowsPath(reference).drive or PureWindowsPath(reference).is_absolute() or any(part in ("", ".", "..") for part in rel.parts):
        raise ValueError(f"{project_id}: evidence[{index}] reference is not a normalized relative path")
    path = (root / Path(*rel.parts)).resolve()
    try:
        path.relative_to(root.resolve())
    except ValueError as exc:
        raise ValueError(f"{project_id}: evidence[{index}] escapes evidence root") from exc
    if not path.is_file():
        raise ValueError(f"{project_id}: evidence[{index}] file does not exist: {reference}")
    size = path.stat().st_size
    if size == 0:
        raise ValueError(f"{project_id}: evidence[{index}] file is empty: {reference}")
    return {"id": project_id, "reference": rel.as_posix(), "sha256": sha256(path), "bytes": size}


def validate_manifest(path: Path, expected_ids: set[str], raw_bytes: bytes | None = None,
                      evidence_root: Path | None = None) -> tuple[dict[str, dict[str, Any]], list[dict[str, Any]]]:
    try:
        source = raw_bytes if raw_bytes is not None else path.read_bytes()
        data = json.loads(source, parse_constant=lambda x: (_ for _ in ()).throw(ValueError(f"invalid JSON number {x}")))
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        raise ValueError(f"cannot read valid manifest JSON: {exc}") from exc
    if not isinstance(data, dict) or data.get("schema") != 1 or not isinstance(data.get("rows"), list):
        raise ValueError("manifest must have schema: 1 and a rows array")
    root = (evidence_root or path.parent).resolve()
    rows: dict[str, dict[str, Any]] = {}
    evidence_hashes: list[dict[str, Any]] = []
    for i, row in enumerate(data["rows"]):
        if not isinstance(row, dict) or not isinstance(row.get("id"), str):
            raise ValueError(f"manifest rows[{i}] must have a string id")
        project_id = row["id"]
        if project_id in rows:
            raise ValueError(f"duplicate manifest row: {project_id}")
        rows[project_id] = row
        if row.get("accepted") is not True:
            raise ValueError(f"{project_id}: row is not marked accepted")
        values = row.get("numeric_values")
        if not isinstance(values, dict) or not values:
            raise ValueError(f"{project_id}: numeric_values must be a non-empty object")
        for key, value in values.items():
            if not isinstance(key, str) or not key.strip() or not _valid_number(value):
                raise ValueError(f"{project_id}: invalid numeric value for {key!r}")
        evidence = row.get("evidence")
        if not isinstance(evidence, list) or not evidence:
            raise ValueError(f"{project_id}: evidence must be a non-empty accepted evidence list")
        for j, item in enumerate(evidence):
            if not isinstance(item, dict) or item.get("accepted") is not True:
                raise ValueError(f"{project_id}: evidence[{j}] is not marked accepted")
            evidence_hashes.append(_evidence_ref(root, item.get("reference"), project_id, j))
    missing, extra = sorted(expected_ids - rows.keys()), sorted(rows.keys() - expected_ids)
    if missing or extra:
        raise ValueError(f"manifest ID set mismatch; missing={missing}; extra={extra}")
    return rows, evidence_hashes


def _assert_external_fresh_output(raw_output: Path, repo_root: Path) -> Path:
    raw = Path(os.path.abspath(raw_output))
    if os.path.lexists(raw):
        raise ValueError(f"output directory already exists or is a symlink: {raw}")
    resolved = raw.resolve()
    repo = repo_root.resolve()
    if resolved == repo or repo in resolved.parents or resolved in repo.parents:
        raise ValueError("output directory must be completely separate from the repository worktree")
    return resolved


def prepare(content_root: Path, manifest_path: Path, output_dir: Path, head: str,
            repo_root: Path | None = None, evidence_root: Path | None = None) -> dict[str, Any]:
    content_root = content_root.resolve()
    repo_root = (repo_root or content_root.parent).resolve()
    manifest_path = manifest_path.resolve()
    if not (content_root / "catalog").is_dir():
        raise ValueError("content root must contain catalog/")
    git_pre = source_git_snapshot(repo_root, content_root, head)
    output_dir = _assert_external_fresh_output(output_dir, repo_root)
    manifest_bytes = manifest_path.read_bytes()
    rows = catalog_rows(content_root)
    if len(rows) != 50:
        raise ValueError(f"expected exactly 50 E5 catalog rows, found {len(rows)}")
    manifest, evidence_hashes = validate_manifest(manifest_path, set(rows), manifest_bytes, evidence_root)
    non_draft = sorted(pid for pid, (_, _, status) in rows.items() if status != "draft")
    if non_draft:
        raise ValueError(f"source E5 rows must all be draft; found non-draft={non_draft}")

    originals = {path: path.read_bytes() for path, _, _ in rows.values()}
    edited_lines = {path: data.decode("utf-8").splitlines(keepends=True) for path, data in originals.items()}
    for pid, (path, line_index, _) in rows.items():
        line = edited_lines[path][line_index]
        match = STATUS_LINE.match(line.rstrip("\r\n"))
        if not match or match.group(2) != "draft":
            raise ValueError(f"{path}:{line_index + 1}: {pid} changed during preflight")
        ending = "\r\n" if line.endswith("\r\n") else "\n" if line.endswith("\n") else ""
        edited_lines[path][line_index] = match.group(1) + "numeric" + match.group(3) + ending
    file_edits = {path: (originals[path], "".join(edited_lines[path]).encode()) for path in originals}
    patch_parts: list[str] = []
    for path in sorted(file_edits):
        before, after = file_edits[path]
        rel = path.relative_to(content_root.parent).as_posix()
        patch_parts.extend(difflib.unified_diff(before.decode().splitlines(keepends=True),
                                                after.decode().splitlines(keepends=True),
                                                fromfile=f"a/{rel}", tofile=f"b/{rel}"))
    patch = "".join(patch_parts)
    if patch.count("-    status: draft") != 50 or patch.count("+    status: numeric") != 50:
        raise ValueError("generated patch did not contain exactly 50 status substitutions")
    if sum(len(before) != len(after) for before, after in file_edits.values()) != len(file_edits):
        raise ValueError("unexpected staged file edit shape")

    output_dir.parent.mkdir(parents=True, exist_ok=True)
    temp_dir = Path(tempfile.mkdtemp(prefix=f".{output_dir.name}.staging-", dir=output_dir.parent))
    try:
        staged = temp_dir / "content"
        shutil.copytree(content_root, staged)
        for path, (_, after) in file_edits.items():
            (staged / path.relative_to(content_root)).write_bytes(after)
        for source in content_root.rglob("*"):
            if source.is_file():
                target = staged / source.relative_to(content_root)
                expected = file_edits[source][1] if source in file_edits else source.read_bytes()
                if target.read_bytes() != expected:
                    raise ValueError(f"unexpected staged content difference: {source}")
        (temp_dir / "promotion.patch").write_text(patch, encoding="utf-8")
        report = {"schema": 2, "head": git_pre["head"], "git_tree": git_pre["git_tree"],
                  "source_content_root": str(content_root), "source_manifest": str(manifest_path),
                  "source_manifest_sha256": hashlib.sha256(manifest_bytes).hexdigest(),
                  "source_content_tree_sha256": git_pre["content_tree_sha256"],
                  "e5_row_count": len(rows), "promoted_ids": sorted(rows), "status_changes_only": True,
                  "changed_catalog_files": [p.relative_to(content_root.parent).as_posix() for p in sorted(file_edits)],
                  "evidence_files": evidence_hashes, "evidence_root": str((evidence_root or manifest_path.parent).resolve()),
                  "patch_sha256": sha256(temp_dir / "promotion.patch"),
                  "note": "Review artifact only; no numeric values written and source content was not modified."}
        (temp_dir / "report.json").write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        git_post = source_git_snapshot(repo_root, content_root, head)
        if git_post != git_pre:
            raise ValueError("source Git/content snapshot changed during staging; refusing publication")
        if hashlib.sha256(manifest_path.read_bytes()).digest() != hashlib.sha256(manifest_bytes).digest():
            raise ValueError("acceptance manifest changed during staging; refusing publication")
        for item in evidence_hashes:
            current = _evidence_ref((evidence_root or manifest_path.parent).resolve(), item["reference"], item["id"], 0)
            if current["sha256"] != item["sha256"] or current["bytes"] != item["bytes"]:
                raise ValueError(f"evidence changed during staging: {item['reference']}")
        if os.path.lexists(output_dir):
            raise ValueError(f"output directory appeared during staging: {output_dir}")
        os.rename(temp_dir, output_dir)
        return report
    except Exception:
        shutil.rmtree(temp_dir, ignore_errors=True)
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--content-root", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--evidence-root", type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--head", required=True, help="must equal clean source worktree HEAD")
    args = parser.parse_args()
    try:
        report = prepare(args.content_root, args.manifest, args.output_dir, args.head,
                         args.repo_root, args.evidence_root)
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(f"prepare_e5_promotion: refused: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(report, indent=2, sort_keys=True))
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
