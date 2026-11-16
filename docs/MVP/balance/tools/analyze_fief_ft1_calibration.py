#!/usr/bin/env python3
"""Analyze the frozen F-T1 calibration raw outputs without imputing censors."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_fief_ft1_calibration as pinned  # noqa: E402

DEFAULT_MANIFEST = Path(__file__).resolve().parents[1] / "m12-wp5-fief-ft1-calibration-r2-seeds-2026-11-16.txt"
DEFAULT_RAW_DIR = Path(__file__).resolve().parents[1] / "m12-wp5-fief-ft1-calibration-output-2026-11-15"
DEFAULT_OUTPUT = Path(__file__).resolve().parents[1] / "m12-wp5-fief-ft1-calibration-analysis-2026-11-15.md"
EXPECTED_PACKS = {
    6000: "994fb7801aca466b0da3d9dabfab3cc90fb024a029100bcb50f76393fbe9812f",
    8600: "0be94d6257593bf6ede6af63d9785858d312df8dfb47c186d62d79bf6e359885",
}
BASE_COST_RECORDS = {
    6000: "85180312495f72ce543a2087b76219bacb96c5ed9ae4e1424a8281bfef7ff92a",
    8600: "2a7d34569481f78ee9d9d1d5075a2d6a7a09df0aa868d673b14245bf23030f05",
}
EXPECTED_SIM_DLL_SHA256 = "fc7959501299122543905e790b2294d5306f7b3d4d3f723f0e40b9bf13010063"
EXPECTED_WORKSPACE_SHA256 = "87996d7a3751c48ecdf40be72c83f903dae45c1370ec383f33cb46d160476857"
ARCHETYPES = {0: "Builder", 1: "Grower", 2: "Warlord", 3: "Sage"}
MATCH_RE = re.compile(
    r"^== fief-match seed (?P<seed>\d+): (?P<minutes>[\d.]+) minutes simulated, "
    r"chapter end (?P<end>.*?) at season (?P<end_season>.*?) \(ended: (?P<ended>True|False)\)$"
)
PROGRESS_RE = re.compile(r"^\s*c(?P<civ>[0-3])\s+(?P<arch>[^:]+):\s+(?P<projects>\d+) E5 projects, KEY-5→KEY-6 (?P<detail>.*)$")
COMPLETE_RE = re.compile(r"(?P<interval>[\d.]+) min from KEY-5 \(at (?P<key6>[\d.]+)\)")
CENSORED_RE = re.compile(r"not complete; observed (?P<observed>[\d.]+) min after KEY-5 \(right-censored\)")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_manifest(path: Path) -> list[int]:
    seeds: list[int] = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.lower() == "seed":
            continue
        try:
            seeds.append(int(line))
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: expected integer seed, got {line!r}") from exc
    if len(seeds) != 20 or len(set(seeds)) != 20:
        raise ValueError(f"F-T1 calibration manifest must contain 20 unique seeds; found {len(seeds)}")
    return seeds


def parse_raw_output(text: str, expected_seed: int, source: str = "<raw output>") -> dict[str, Any]:
    match = next((m for line in text.splitlines() if (m := MATCH_RE.match(line.strip()))), None)
    if match is None:
        raise ValueError(f"{source}: missing fief-match run header")
    seed = int(match.group("seed"))
    if seed != expected_seed:
        raise ValueError(f"{source}: header seed {seed} does not match expected seed {expected_seed}")
    minutes = float(match.group("minutes"))
    if minutes != 130.0:
        raise ValueError(f"{source}: expected 130 simulated minutes, got {minutes}")

    key5_col: int | None = None
    key5_by_civ: dict[int, float | None] = {}
    for line in text.splitlines():
        if line.startswith("civ arch"):
            columns = line.split()
            if "KEY-5" not in columns:
                raise ValueError(f"{source}: civilization table has no KEY-5 column")
            key5_col = columns.index("KEY-5")
            continue
        if key5_col is not None and re.match(r"^c[0-3]\s", line):
            fields = line.split()
            civ = int(fields[0][1:])
            if len(fields) <= key5_col:
                raise ValueError(f"{source}: truncated civilization row for c{civ}")
            key5_raw = fields[key5_col]
            key5_by_civ[civ] = None if key5_raw == "-" else float(key5_raw)
    if key5_col is None or set(key5_by_civ) != set(ARCHETYPES):
        raise ValueError(f"{source}: expected one KEY-5 table row for each of four civilizations")

    progress: dict[int, dict[str, Any]] = {}
    for line in text.splitlines():
        row = PROGRESS_RE.match(line)
        if not row:
            continue
        civ = int(row.group("civ"))
        if civ in progress:
            raise ValueError(f"{source}: duplicate E5 progress row for c{civ}")
        arch = row.group("arch").strip()
        if arch != ARCHETYPES[civ]:
            raise ValueError(f"{source}: c{civ} expected {ARCHETYPES[civ]}, got {arch}")
        detail = row.group("detail")
        projects = int(row.group("projects"))
        key5_minute = key5_by_civ[civ]
        completed = COMPLETE_RE.search(detail)
        censored = CENSORED_RE.search(detail)
        if completed:
            if key5_minute is None:
                raise ValueError(f"{source}: c{civ} completed KEY-6 but has no KEY-5 time")
            interval = float(completed.group("interval"))
            key6_minute = float(completed.group("key6"))
            if abs((key6_minute - key5_minute) - interval) > 0.16:
                raise ValueError(f"{source}: c{civ} KEY-5/KEY-6 times disagree with reported interval")
            state = "complete"
            observed_after_key5 = interval
        elif censored:
            if key5_minute is None:
                raise ValueError(f"{source}: c{civ} is right-censored but has no KEY-5 time")
            state = "right_censored_after_KEY5"
            observed_after_key5 = float(censored.group("observed"))
            interval = None
            key6_minute = None
        elif "KEY-5 not reached" in detail:
            if key5_minute is not None:
                raise ValueError(f"{source}: c{civ} reports KEY-5 not reached but table has a time")
            state = "KEY5_not_reached"
            observed_after_key5 = None
            interval = None
            key6_minute = None
        else:
            raise ValueError(f"{source}: unrecognized E5 progress state for c{civ}: {detail!r}")
        progress[civ] = {
            "seed": seed, "civ": civ, "archetype": arch, "key5_minute": key5_minute,
            "key6_minute": key6_minute, "interval_minutes": interval,
            "observed_after_key5_minutes": observed_after_key5, "state": state,
            "e5_projects": projects,
        }
    if set(progress) != set(ARCHETYPES):
        raise ValueError(f"{source}: expected four E5 progress rows; found {len(progress)}")
    return {
        "seed": seed, "simulated_minutes": minutes, "chapter_end": match.group("end"),
        "chapter_end_season": match.group("end_season"), "ended": match.group("ended") == "True",
        "seats": [progress[civ] for civ in range(4)],
    }


def parse_combined_output(text: str, expected_seeds: list[int], source: str = "<combined output>") -> list[dict[str, Any]]:
    """Parse concatenated per-seed fief-match outputs in manifest order."""
    chunks: list[str] = []
    current: list[str] = []
    for line in text.splitlines():
        if MATCH_RE.match(line.strip()):
            if current:
                chunks.append("\n".join(current))
            current = [line]
        elif current:
            current.append(line)
        elif line.strip():
            raise ValueError(f"{source}: unexpected content before first fief-match header")
    if current:
        chunks.append("\n".join(current))
    headers = []
    for chunk in chunks:
        header = next((m for line in chunk.splitlines() if (m := MATCH_RE.match(line.strip()))), None)
        if header is None:
            raise ValueError(f"{source}: malformed match chunk")
        headers.append(int(header.group("seed")))
    if headers != expected_seeds:
        raise ValueError(f"{source}: seed header sequence does not match frozen manifest; "
                         f"expected {expected_seeds}, got {headers}")
    return [parse_raw_output(chunk, seed, f"{source} (seed {seed})")
            for chunk, seed in zip(chunks, expected_seeds, strict=True)]


def validate_run(raw_dir: Path, manifest_path: Path) -> tuple[list[int], dict[str, Any]]:
    raw_dir, manifest_path = raw_dir.resolve(), manifest_path.resolve()
    if not raw_dir.is_dir():
        raise FileNotFoundError(f"raw output directory does not exist: {raw_dir}")
    if not manifest_path.is_file():
        raise FileNotFoundError(f"manifest does not exist: {manifest_path}")
    manifest_hash = sha256(manifest_path)
    if manifest_hash != pinned.EXPECTED_MANIFEST_SHA256:
        raise ValueError(f"frozen manifest hash mismatch: expected {pinned.EXPECTED_MANIFEST_SHA256}, got {manifest_hash}")
    seeds = load_manifest(manifest_path)
    metadata_path = raw_dir / "run_metadata.json"
    if not metadata_path.is_file():
        raise FileNotFoundError(f"runner metadata is missing: {metadata_path}")
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    if metadata.get("status") != "complete":
        raise ValueError(f"runner metadata status must be 'complete', got {metadata.get('status')!r}")
    if metadata.get("minutes") != 130 or metadata.get("cohortRole") != "calibration_only_not_acceptance":
        raise ValueError("runner metadata does not identify the pinned 130-minute calibration cohort")
    provenance_before = metadata.get("provenanceBefore")
    provenance_after = metadata.get("provenanceAfter")
    if not isinstance(provenance_before, dict) or not isinstance(provenance_after, dict):
        raise ValueError("runner metadata is missing pre/post provenance")
    if provenance_before.get("manifestSha256") != manifest_hash or metadata.get("seeds") != seeds:
        raise ValueError("runner pre-run provenance manifest hash or seed list does not match the frozen manifest")
    manifest_copy_name = metadata.get("manifestCopy")
    if not isinstance(manifest_copy_name, str) or not manifest_copy_name or Path(manifest_copy_name).name != manifest_copy_name:
        raise ValueError("runner metadata has no safe manifest-copy filename")
    manifest_copy = raw_dir / manifest_copy_name
    if not manifest_copy.is_file() or sha256(manifest_copy) != manifest_hash:
        raise ValueError("runner manifest copy is missing or does not match the frozen manifest")
    recorded_manifest_path = provenance_before.get("manifestPath")
    if recorded_manifest_path and Path(recorded_manifest_path).resolve() != manifest_path:
        raise ValueError("runner pre-run provenance manifest path does not match the supplied manifest")
    provenance_keys = ("headCommit", "sourceAggregateSha256", "packSha256", "harnessDllSha256",
                       "simDllSha256", "runnerSha256", "erasSha256")
    if any(provenance_before.get(key) != provenance_after.get(key) for key in provenance_keys):
        raise ValueError("runner pre/post provenance differs")
    for key, value in provenance_before.items():
        metadata.setdefault(key, value)
    if provenance_before.get("e5BaseCost") != pinned.EXPECTED_BASE_COST or \
            provenance_before.get("packE5BaseCost") != pinned.EXPECTED_BASE_COST:
        raise ValueError("runner provenance does not identify the frozen E5 base-cost candidate")
    if provenance_before.get("e5BaseCostRecordSha256") != pinned.EXPECTED_BASE_COST_RECORD_SHA256 or \
            provenance_before.get("packE5BaseCostRecordSha256") != pinned.EXPECTED_BASE_COST_RECORD_SHA256:
        raise ValueError("runner provenance E5 base-cost record hash mismatch")
    if provenance_before.get("runnerSha256") != provenance_after.get("runnerSha256") or \
            provenance_before.get("erasSha256") != provenance_after.get("erasSha256"):
        raise ValueError("runner or era-registry provenance differs pre/post")
    runs = metadata.get("runs", [])
    by_seed: dict[int, dict[str, Any]] = {}
    for row in runs:
        seed = int(row["seed"])
        if seed in by_seed:
            raise ValueError(f"runner metadata has duplicate run for seed {seed}")
        by_seed[seed] = row
    if set(by_seed) != set(seeds):
        raise ValueError("runner metadata does not contain exactly the frozen calibration seeds")

    matches = []
    for seed in seeds:
        row = by_seed[seed]
        if row.get("status") != "complete" or row.get("exitCode") != 0 or row.get("rawFile") != f"seed-{seed}.txt":
            raise ValueError(f"runner record for seed {seed} is incomplete or has an unexpected raw filename")
        command = row.get("command")
        if not isinstance(command, list) or "fief-match" not in command or "--grant-e5" in command or \
                command[command.index("--minutes") + 1:command.index("--minutes") + 2] != ["130"]:
            raise ValueError(f"runner record for seed {seed} is not the natural 130-minute Fief command")
        if any(row.get("provenanceBefore", {}).get(key) != provenance_before.get(key) or
               row.get("provenanceAfter", {}).get(key) != provenance_after.get(key)
               for key in provenance_keys):
            raise ValueError(f"runner record for seed {seed} has inconsistent pre/post provenance")
        raw_path = raw_dir / row["rawFile"]
        if not raw_path.is_file():
            raise FileNotFoundError(f"raw output missing for seed {seed}: {raw_path}")
        if sha256(raw_path) != row.get("rawSha256"):
            raise ValueError(f"raw output hash mismatch for seed {seed}: {raw_path}")
        matches.append(parse_raw_output(raw_path.read_text(encoding="utf-8", errors="replace"), seed, str(raw_path)))
    return seeds, {"metadata": metadata, "matches": matches}


def percentile(values: list[float], p: float) -> float:
    ordered = sorted(values)
    if not ordered:
        raise ValueError("percentile requires completed-seat intervals")
    position = (len(ordered) - 1) * p
    lower = int(position)
    upper = min(lower + 1, len(ordered) - 1)
    fraction = position - lower
    return ordered[lower] * (1 - fraction) + ordered[upper] * fraction


def analyze(raw_dir: Path, manifest_path: Path, output_path: Path) -> dict[str, Any]:
    seeds, validated = validate_run(raw_dir, manifest_path)
    return write_analysis(seeds, validated["matches"], validated["metadata"],
                          sha256(manifest_path.resolve()), output_path, None)


def write_analysis(seeds: list[int], matches: list[dict[str, Any]], metadata: dict[str, Any],
                   manifest_hash: str, output_path: Path,
                   combined_raw_sha256: str | None) -> dict[str, Any]:
    seats = [seat for match in matches for seat in match["seats"]]
    completed = [seat["interval_minutes"] for seat in seats if seat["state"] == "complete"]
    completed_count = len(completed)
    right_censored = sum(seat["state"] == "right_censored_after_KEY5" for seat in seats)
    no_key5 = sum(seat["state"] == "KEY5_not_reached" for seat in seats)
    if len(seats) != len(seeds) * 4 or completed_count + right_censored + no_key5 != len(seats):
        raise ValueError("seat counts do not reconcile to four seats per manifest seed")
    stats = {
        "seed_count": len(seeds), "seat_count": len(seats), "completed": completed_count,
        "right_censored_after_KEY5": right_censored, "KEY5_not_reached": no_key5,
        "median": percentile(completed, 0.5), "p10": percentile(completed, 0.1),
        "p25": percentile(completed, 0.25), "p75": percentile(completed, 0.75),
        "p90": percentile(completed, 0.9),
    }
    source_base_cost = metadata.get("e5BaseCost", pinned.EXPECTED_BASE_COST)
    pack_base_cost = metadata.get("packE5BaseCost", metadata.get("fiefEraBaseCost", source_base_cost))
    source_record_hash = metadata.get("e5BaseCostRecordSha256", BASE_COST_RECORDS[source_base_cost])
    pack_record_hash = metadata.get("packE5BaseCostRecordSha256", BASE_COST_RECORDS[pack_base_cost])
    eras_hash = metadata["erasSha256"]
    lines = [
        "# F-T1 calibration cohort analysis",
        "",
        "> Calibration only; not the frozen acceptance holdout and not F-T1 closure. Incomplete KEY-6 intervals are not imputed.",
        "",
        "## Provenance",
        "",
        f"- Frozen manifest SHA-256: `{manifest_hash}` ({len(seeds)} seeds).",
        f"- Pack SHA-256: `{metadata['packSha256']}`; E5 base cost source/pack `{source_base_cost}/{pack_base_cost}` "
        f"(canonical record SHA-256 `{source_record_hash}` / `{pack_record_hash}`).",
        f"- Release harness SHA-256: `{metadata['harnessDllSha256']}`; era registry SHA-256: `{eras_hash}`.",
        f"- Runner completed `{len(metadata.get('runs', seeds))}/{len(seeds)}` seeds, each exit code 0, at the 130-minute horizon with natural Fief progression.",
        "",
        "## Summary",
        "",
        f"- Seats: {stats['seat_count']} total; {completed_count} completed KEY-6; {right_censored} right-censored after KEY-5; {no_key5} did not reach KEY-5.",
        f"- Completed-seat KEY-5→KEY-6 interval (minutes): median {stats['median']:.2f}; p10/p25/p75/p90 {stats['p10']:.2f}/{stats['p25']:.2f}/{stats['p75']:.2f}/{stats['p90']:.2f}.",
        "- Quantiles use completed seats only. Censored and no-KEY-5 seats remain in the completion counts and are not assigned interval values.",
        "",
        "## Every seat",
        "",
        "| Seed | Civ | Archetype | KEY-5 minute | KEY-6 minute | KEY-5→KEY-6 minutes | State | Observed after KEY-5 | E5 projects |",
        "| ---: | ---: | --- | ---: | ---: | ---: | --- | ---: | ---: |",
    ]
    if combined_raw_sha256 is not None:
        lines.insert(10, f"- Combined raw-output SHA-256: `{combined_raw_sha256}`.")
    for seat in seats:
        def fmt(value: float | None) -> str:
            return "—" if value is None else f"{value:.1f}"
        lines.append(
            f"| {seat['seed']} | {seat['civ']} | {seat['archetype']} | {fmt(seat['key5_minute'])} | "
            f"{fmt(seat['key6_minute'])} | {fmt(seat['interval_minutes'])} | {seat['state']} | "
            f"{fmt(seat['observed_after_key5_minutes'])} | {seat['e5_projects']} |"
        )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return stats


def validate_combined_run(raw_path: Path, metadata_path: Path,
                          manifest_path: Path) -> tuple[list[int], dict[str, Any]]:
    raw_path, metadata_path, manifest_path = (p.resolve() for p in (raw_path, metadata_path, manifest_path))
    for path, label in ((raw_path, "combined raw output"), (metadata_path, "run metadata"),
                        (manifest_path, "frozen manifest")):
        if not path.is_file():
            raise FileNotFoundError(f"{label} does not exist: {path}")
    manifest_hash = sha256(manifest_path)
    if manifest_hash != pinned.EXPECTED_MANIFEST_SHA256:
        raise ValueError(f"frozen manifest hash mismatch: expected {pinned.EXPECTED_MANIFEST_SHA256}, got {manifest_hash}")
    seeds = load_manifest(manifest_path)
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    if metadata.get("status") != "complete":
        raise ValueError(f"runner metadata status must be 'complete', got {metadata.get('status')!r}")
    if metadata.get("minutes") != 130 or metadata.get("grantE5") is not False:
        raise ValueError("metadata must describe natural Fief progression at 130 minutes without --grant-e5")
    if metadata.get("seeds") != seeds or metadata.get("seedCount") != len(seeds):
        raise ValueError("metadata seed coverage/order does not match the frozen manifest")
    if metadata.get("sourceManifestSha256") != manifest_hash:
        raise ValueError("metadata source-manifest hash does not match the frozen manifest")
    try:
        base_cost = int(metadata["fiefEraBaseCost"])
        pack_hash = metadata["packSha256"]
        harness_hash = metadata["harnessDllSha256"]
        sim_hash = metadata["simDllSha256"]
        workspace_hash = metadata["workspaceAggregateSha256"]
    except (KeyError, TypeError, ValueError) as exc:
        raise ValueError("metadata is missing expected pack/harness/base-cost provenance") from exc
    if EXPECTED_PACKS.get(base_cost) != pack_hash:
        raise ValueError(f"unexpected pack/base-cost pair: base_cost={base_cost}, packSha256={pack_hash}")
    if harness_hash != pinned.EXPECTED_HARNESS_SHA256:
        raise ValueError(f"harness hash mismatch: expected {pinned.EXPECTED_HARNESS_SHA256}, got {harness_hash}")
    if sim_hash != EXPECTED_SIM_DLL_SHA256:
        raise ValueError(f"simulation DLL hash mismatch: expected {EXPECTED_SIM_DLL_SHA256}, got {sim_hash}")
    if workspace_hash != EXPECTED_WORKSPACE_SHA256:
        raise ValueError(f"workspace aggregate hash mismatch: expected {EXPECTED_WORKSPACE_SHA256}, got {workspace_hash}")
    if metadata.get("rawOutput") and Path(metadata["rawOutput"]).resolve() != raw_path:
        raise ValueError("metadata rawOutput path does not match the combined output path")
    for path_key, hash_key in (("packPath", "packSha256"), ("harnessDllPath", "harnessDllSha256"),
                               ("simDllPath", "simDllSha256")):
        source_path = Path(metadata[path_key])
        if source_path.is_file() and sha256(source_path) != metadata[hash_key]:
            raise ValueError(f"file hash no longer matches metadata: {source_path}")
    matches = parse_combined_output(raw_path.read_text(encoding="utf-8", errors="replace"), seeds, str(raw_path))
    return seeds, {
        "metadata": metadata, "matches": matches, "manifestSha256": manifest_hash,
        "combinedRawSha256": sha256(raw_path),
    }


def analyze_combined(raw_path: Path, metadata_path: Path, manifest_path: Path,
                     output_path: Path) -> dict[str, Any]:
    seeds, validated = validate_combined_run(raw_path, metadata_path, manifest_path)
    return write_analysis(seeds, validated["matches"], validated["metadata"],
                          validated["manifestSha256"], output_path,
                          validated["combinedRawSha256"])


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-dir", type=Path)
    parser.add_argument("--combined-output", type=Path)
    parser.add_argument("--metadata", type=Path)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args(argv)
    try:
        if args.combined_output:
            if not args.metadata:
                raise ValueError("--metadata is required with --combined-output")
            stats = analyze_combined(args.combined_output, args.metadata, args.manifest, args.output)
        else:
            if args.metadata:
                raise ValueError("--metadata is only accepted with --combined-output")
            stats = analyze(args.raw_dir or DEFAULT_RAW_DIR, args.manifest, args.output)
        print(json.dumps(stats, sort_keys=True))
        return 0
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
