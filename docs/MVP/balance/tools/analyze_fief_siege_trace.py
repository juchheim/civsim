#!/usr/bin/env python3
"""Validate and summarize a frozen natural Fief siege trace cohort.

This is an exposure and conservation report, not a balance acceptance gate. It
uses the generic runner's run-freeze/status sidecars and checks every raw log
hash before interpreting its trace rows.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

DEFAULT_MANIFEST = Path(__file__).resolve().parents[1] / "m12-e5-8-f-t5-natural-seeds-2026-11-16.txt"
EXPECTED_MANIFEST_SHA256 = "d30825f653a1a39c851a7a5f1d64a640ce0c69fee8da70c95c619d0a51edc29b"
EXPECTED_SEED_COUNT = 20
EXPECTED_TRACE = "--fief-siege-trace"
EXPECTED_MINUTES = 130
KV = re.compile(r"([A-Za-z][A-Za-z0-9]*)=([^\s]+)")
HEADER = re.compile(r"^== fief-match seed (\d+): ([\d.]+) minutes simulated, ")
EVENTS = {"investmentrelieved", "investmentcapitulated", "investmentsally", "assaultresolved", "captured"}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_sha(value: Any) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def load_manifest(path: Path) -> list[int]:
    values: list[int] = []
    for number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        item = raw.strip()
        if not item or item.startswith("#") or item.lower() == "seed":
            continue
        try:
            value = int(item)
        except ValueError as error:
            raise ValueError(f"{path}:{number}: invalid seed {item!r}") from error
        if value < 0:
            raise ValueError(f"{path}:{number}: seed must be nonnegative")
        values.append(value)
    if len(values) != EXPECTED_SEED_COUNT or len(set(values)) != EXPECTED_SEED_COUNT:
        raise ValueError(f"expected {EXPECTED_SEED_COUNT} unique siege seeds, got {len(values)}")
    if sha256(path) != EXPECTED_MANIFEST_SHA256:
        raise ValueError("siege seed manifest SHA-256 does not match the frozen 2026-11-16 block")
    return values


def load_json(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path}: expected JSON object")
    return value


def validate_provenance(freeze: dict[str, Any], manifest_path: Path, seeds: list[int]) -> dict[str, Any]:
    traces = freeze.get("traces")
    if freeze.get("trace") != EXPECTED_TRACE and not (isinstance(traces, list) and EXPECTED_TRACE in traces):
        raise ValueError(f"run freeze trace must be {EXPECTED_TRACE!r}")
    if freeze.get("minutes") != EXPECTED_MINUTES:
        raise ValueError(f"run freeze must use {EXPECTED_MINUTES} minutes")
    if freeze.get("seeds") != seeds:
        raise ValueError("run freeze seed list differs from the supplied frozen manifest")
    if freeze.get("seed_file_sha256") != sha256(manifest_path):
        raise ValueError("run freeze seed manifest hash differs from the supplied manifest")

    provenance = freeze.get("provenance")
    if not isinstance(provenance, dict):
        raise ValueError("run freeze has no provenance object")
    required = ("source_tree_sha256", "pack_sha256", "harness_sha256", "sim_sha256",
                "source_manifest_sha256", "source_manifest")
    if any(not isinstance(provenance.get(key), str) or not provenance[key] for key in required):
        raise ValueError("run freeze provenance is incomplete")
    for key in required[:-1]:
        if key.endswith("sha256") and not re.fullmatch(r"[0-9a-f]{64}", provenance[key]):
            raise ValueError(f"invalid SHA-256 field in provenance: {key}")

    source_manifest_path = Path(freeze["pack"]).resolve().parent / provenance["source_manifest"]
    if not source_manifest_path.is_file() or sha256(source_manifest_path) != provenance["source_manifest_sha256"]:
        raise ValueError("frozen source manifest is missing or its SHA-256 differs from provenance")
    source_manifest = load_json(source_manifest_path)
    files = source_manifest.get("files")
    head = source_manifest.get("head")
    if not isinstance(files, list) or not files or not isinstance(head, str) or not head:
        raise ValueError("source manifest does not have the canonical file list and HEAD")
    if source_manifest.get("aggregate_sha256") != provenance["source_tree_sha256"]:
        raise ValueError("source manifest aggregate differs from the run provenance")
    if canonical_sha({"head": head, "files": files}) != provenance["source_tree_sha256"]:
        raise ValueError("source aggregate is not the canonical hash of its frozen source manifest")

    for label, path_key, digest_key in (("pack", "pack", "pack_sha256"),
                                         ("Harness", "harness", "harness_sha256"),
                                         ("Sim", "sim", "sim_sha256")):
        path = Path(str(freeze.get(path_key, "")))
        if not path.is_file() or sha256(path) != provenance[digest_key]:
            raise ValueError(f"frozen {label} artifact is missing or its SHA-256 differs from provenance")
    harness = Path(freeze["harness"]).resolve()
    sim = Path(freeze["sim"]).resolve()
    if sim != (harness.parent / "CivSim.Sim.dll").resolve():
        raise ValueError("the frozen Sim path is not the adjacent assembly loaded by the Harness")
    return provenance


def jsonl(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not raw.strip():
            continue
        value = json.loads(raw)
        if not isinstance(value, dict):
            raise ValueError(f"{path}:{number}: expected JSON object")
        rows.append(value)
    return rows


def validate_run(raw_dir: Path, manifest_path: Path) -> tuple[list[int], dict[str, Any], dict[int, Path]]:
    raw_dir = raw_dir.resolve()
    manifest_path = manifest_path.resolve()
    if not raw_dir.is_dir():
        raise FileNotFoundError(f"run directory does not exist: {raw_dir}")
    seeds = load_manifest(manifest_path)
    freeze = load_json(raw_dir / "run-freeze.json")
    if Path(str(freeze.get("output", ""))).resolve() != raw_dir:
        raise ValueError("run freeze output directory differs from the supplied run directory")
    provenance = validate_provenance(freeze, manifest_path, seeds)
    run_status_path = raw_dir / "run-status.jsonl"
    status_rows = jsonl(run_status_path)
    if not status_rows or status_rows[0].get("status") != "running":
        raise ValueError("run status has no initial running event")
    if status_rows[-1].get("status") != "completed" or status_rows[-1].get("seed_count") != len(seeds):
        raise ValueError("run status is not a completed full cohort")
    if any(row.get("status") in ("failed", "interrupted") for row in status_rows):
        raise ValueError("run status contains a failed or interrupted event")
    status_traces = status_rows[0].get("traces")
    if (status_rows[0].get("trace") != EXPECTED_TRACE and
            not (isinstance(status_traces, list) and EXPECTED_TRACE in status_traces)) or \
            sum(row.get("status") == "running" for row in status_rows) != 1:
        raise ValueError("run status trace protocol or event sequence is invalid")

    raw_files: dict[int, Path] = {}
    for seed in seeds:
        output = raw_dir / f"seed-{seed}.txt"
        status_path = raw_dir / f"seed-{seed}.status.jsonl"
        if not output.is_file() or not status_path.is_file():
            raise FileNotFoundError(f"missing raw log or seed status for seed {seed}")
        events = jsonl(status_path)
        completed = [row for row in events if row.get("status") == "completed"]
        if len(completed) != 1 or completed[0].get("exit_code") != 0:
            raise ValueError(f"seed {seed} does not have exactly one successful completion record")
        starts = [row for row in events if row.get("status") == "running"]
        command = starts[0].get("command") if len(starts) == 1 else None
        selected_traces = freeze.get("traces")
        expected_flags = selected_traces if isinstance(selected_traces, list) else [EXPECTED_TRACE]
        command_valid = (isinstance(command, list) and len(command) == 9 + len(expected_flags) and bool(command[0]) and
                         Path(str(command[1])).resolve() == Path(freeze["harness"]).resolve() and
                         command[2:] == ["fief-match", "--pack", str(Path(freeze["pack"]).resolve()),
                                         "--seed", str(seed), "--minutes", str(EXPECTED_MINUTES), *expected_flags])
        command_valid = command_valid and EXPECTED_TRACE in expected_flags
        if not command_valid:
            raise ValueError(f"seed {seed} command does not match the frozen natural siege protocol")
        if completed[0].get("output_sha256") != sha256(output):
            raise ValueError(f"raw output hash mismatch for seed {seed}")
        run_headers = [HEADER.match(line.strip()) for line in output.read_text(encoding="utf-8", errors="replace").splitlines()]
        run_headers = [match for match in run_headers if match]
        if len(run_headers) != 1 or int(run_headers[0].group(1)) != seed or float(run_headers[0].group(2)) != EXPECTED_MINUTES:
            raise ValueError(f"seed {seed} output must contain exactly one {EXPECTED_MINUTES}-minute match header")
        raw_files[seed] = output

    unexpected = []
    for path in raw_dir.glob("seed-*.txt"):
        suffix = path.stem.removeprefix("seed-")
        if suffix.isdigit() and int(suffix) not in set(seeds):
            unexpected.append(path.name)
    if unexpected:
        raise ValueError(f"run directory contains files outside the manifest: {sorted(unexpected)}")
    return seeds, {"freeze": freeze, "provenance": provenance, "status": status_rows}, raw_files


def parse_rows(path: Path, expected_seed: int) -> list[dict[str, str]]:
    rows = []
    for number, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        stripped = line.strip()
        if not (stripped.startswith("fief-siege-trace ") or stripped.startswith("fief-siege-terminal")):
            continue
        marker, _, payload = stripped.partition(" ")
        row = {"recordType": marker, **dict(KV.findall(payload))}
        if "seed" not in row:
            raise ValueError(f"{path}:{number}: siege trace row has no seed")
        try:
            seed = int(row["seed"])
        except ValueError as error:
            raise ValueError(f"{path}:{number}: invalid trace seed {row['seed']!r}") from error
        if seed != expected_seed:
            raise ValueError(f"{path}:{number}: row seed {seed} differs from manifest seed {expected_seed}")
        rows.append(row)
    return rows


def int_field(row: dict[str, str], key: str, *, optional: bool = False) -> int | None:
    value = row.get(key, "")
    if optional and (not value or value.lower() == "unavailable"):
        return None
    if not re.fullmatch(r"-?\d+", value):
        raise ValueError(f"invalid integer {key}={value!r}")
    return int(value)


def float_field(row: dict[str, str], key: str, *, optional: bool = False) -> float | None:
    value = row.get(key, "")
    if optional and (not value or value.lower() == "unavailable"):
        return None
    try:
        number = float(value)
    except ValueError as error:
        raise ValueError(f"invalid number {key}={value!r}") from error
    if not math.isfinite(number):
        raise ValueError(f"non-finite number {key}={value!r}")
    return number


def bool_field(row: dict[str, str], key: str, *, optional: bool = False) -> bool | None:
    value = row.get(key, "").lower()
    if optional and value == "unavailable":
        return None
    if value not in ("true", "false"):
        raise ValueError(f"invalid boolean {key}={row.get(key)!r}")
    return value == "true"


def bootstrap_rate(seed_totals: dict[int, tuple[int, int]], manifest_sha: str, label: str) -> tuple[float, float, int]:
    """Deterministic match-cluster bootstrap CI for deaths / assigned adult-turns."""
    eligible = [seed for seed, (trials, _) in seed_totals.items() if trials > 0]
    if len(eligible) < 2:
        return math.nan, math.nan, 0
    rng = random.Random(int(hashlib.sha256(f"{manifest_sha}:{label}".encode()).hexdigest()[:16], 16))
    rates: list[float] = []
    for _ in range(10_000):
        chosen = [rng.choice(eligible) for _ in eligible]
        trials = sum(seed_totals[seed][0] for seed in chosen)
        deaths = sum(seed_totals[seed][1] for seed in chosen)
        if trials:
            rates.append(deaths / trials)
    if not rates:
        return math.nan, math.nan, 0
    rates.sort()
    return rates[int(0.025 * (len(rates) - 1))], rates[int(0.975 * (len(rates) - 1))], len(rates)


def percentile(values: list[float], p: float) -> float:
    if not values:
        raise ValueError("percentile of an empty sample")
    x = (len(values) - 1) * p
    lo = int(x)
    hi = min(lo + 1, len(values) - 1)
    return values[lo] * (hi - x) + values[hi] * (x - lo)


def attrition_report(seeds: list[int], seasonal: list[dict[str, str]], manifest_hash: str) -> list[str]:
    groups: dict[str, dict[int, tuple[int, int]]] = {"false": defaultdict(lambda: (0, 0)),
                                                    "true": defaultdict(lambda: (0, 0))}
    excluded = Counter()
    included_rows = Counter()
    for row in seasonal:
        flag = bool_field(row, "def09Active", optional=True)
        assigned = int_field(row, "attritionAssignedCrews", optional=True)
        deaths = int_field(row, "attritionDeaths", optional=True)
        seed = int_field(row, "seed")
        if flag is None or assigned is None or deaths is None:
            excluded["missing_attrition_or_DEF09_fields"] += 1
            continue
        if assigned < 0 or deaths < 0 or deaths > assigned:
            raise ValueError(f"impossible attrition exposure/death counts in seed {seed}")
        key = str(flag).lower()
        trials, total_deaths = groups[key][seed]
        groups[key][seed] = (trials + assigned, total_deaths + deaths)
        included_rows[key] += 1

    lines = [
        "",
        "## Siege crew attrition exposure",
        "",
        "Each `attritionAssignedCrews` value is the count of adults exposed for one siege turn; summing it across seasonal rows gives assigned adult-turns. Confidence intervals are a deterministic 10,000-draw bootstrap clustered by match seed. These are descriptive estimates, not a pass/fail gate.",
        "",
        "| Stratum | Matches with exposure | Valid seasonal rows | Assigned adult-turns | Deaths | Observed rate | 95% match-cluster bootstrap CI | Draft reference rate | Interpretation |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---|",
    ]
    for flag, label, target in (("false", "DEF-09 inactive", 0.01), ("true", "DEF-09 active", 0.0125)):
        by_seed = groups[flag]
        trials = sum(n for n, _ in by_seed.values())
        deaths = sum(d for _, d in by_seed.values())
        exposed_seeds = sum(n > 0 for n, _ in by_seed.values())
        ci_low, ci_high, draws = bootstrap_rate(by_seed, manifest_hash, flag)
        if trials:
            rate = f"{deaths / trials:.4%}"
            ci = f"[{ci_low:.4%}, {ci_high:.4%}]" if draws else "not estimable"
            interpretation = "low exposure; descriptive" if exposed_seeds < 5 or trials < 100 else "descriptive only"
        else:
            rate, ci, interpretation = "—", "—", "no exposure observed"
        lines.append(f"| {label} | {exposed_seeds}/{len(seeds)} | {included_rows[flag]} | {trials} | {deaths} | {rate} | {ci} ({draws:,} draws) | {target:.2%} per adult-turn | {interpretation} |")
    lines.append(f"\nRows excluded from attrition-rate estimates because fields were missing: {sum(excluded.values())}.")
    return lines


def analyze(raw_dir: Path, manifest_path: Path, *, verify_current: bool = False) -> str:
    seeds, metadata, raw_files = validate_run(raw_dir, manifest_path)
    freeze = metadata["freeze"]
    provenance = metadata["provenance"]
    manifest_hash = sha256(manifest_path.resolve())
    rows_by_seed: dict[int, list[dict[str, str]]] = {}
    for seed in seeds:
        rows_by_seed[seed] = parse_rows(raw_files[seed], seed)

    seasonal: list[dict[str, str]] = []
    censored: list[dict[str, str]] = []
    terminal: dict[int, dict[str, str]] = {}
    inventory: list[dict[str, str]] = []
    terminal_settlements: list[dict[str, str]] = []
    events: Counter[str] = Counter()
    unique_events: set[tuple[int, int | None, int | None, str]] = set()
    for seed in seeds:
        terminals = [row for row in rows_by_seed[seed] if row["recordType"] == "fief-siege-terminal"]
        if len(terminals) != 1:
            raise ValueError(f"seed {seed}: expected exactly one terminal exposure row, got {len(terminals)}")
        if terminals[0].get("exposure") not in ("none", "observed"):
            raise ValueError(f"seed {seed}: unknown terminal exposure state {terminals[0].get('exposure')!r}")
        terminal[seed] = terminals[0]
        for row in rows_by_seed[seed]:
            record = row["recordType"]
            outcome = row.get("outcome", "").lower()
            if record == "fief-siege-terminal-inventory":
                for field in ("cannonStock", "powderStock"):
                    value = float_field(row, field)
                    if value is not None and value < 0:
                        raise ValueError(f"seed {seed}: negative terminal {field}")
                inventory.append(row)
            elif record == "fief-siege-terminal-settlement":
                terminal_settlements.append(row)
            elif record == "fief-siege-trace":
                if row.get("siegeTurn") == "terminal":
                    if outcome not in EVENTS:
                        raise ValueError(f"seed {seed}: unrecognized siege terminal event {outcome!r}")
                    events[outcome] += 1
                    unique_events.add((seed, int_field(row, "season", optional=True),
                                       int_field(row, "settlement", optional=True), outcome))
                elif outcome == "right_censored":
                    censored.append(row)
                elif outcome in ("active", "ended"):
                    seasonal.append(row)
                else:
                    raise ValueError(f"seed {seed}: unrecognized siege trace outcome {outcome!r}")

    # Validate seasonal rows and distinguish engine/cannon assets, staffed crews,
    # shots, powder, and actual wall integrity. PowderBreach is a persistent
    # mechanic state, not the same thing as integrity reaching zero.
    shots = 0
    powder_paid = 0.0
    fired_rows = conservation_rows = conservation_mismatches = 0
    breach_flag_rows = actual_wall_breach_rows = breach_without_shot = 0
    actual_wall_breach_sieges: set[tuple[int, int]] = set()
    engine_asset_turns = cannon_asset_turns = engine_crews = cannon_crews = 0
    attrition_adult_turns = 0
    by_siege: dict[tuple[int, int], list[dict[str, str]]] = defaultdict(list)
    for row in seasonal:
        seed = int_field(row, "seed")
        settlement = int_field(row, "settlement")
        season = int_field(row, "season")
        engines = int_field(row, "engines", optional=True)
        cannons = int_field(row, "cannons", optional=True)
        fire_attempted = bool_field(row, "fireAttempted", optional=True)
        if engines is None or cannons is None or engines < 0 or cannons < 0:
            raise ValueError(f"seed {seed}: invalid/unknown engine or cannon investment count")
        engine_asset_turns += engines
        cannon_asset_turns += cannons
        ec = int_field(row, "engineCrews", optional=True)
        cc = int_field(row, "cannonCrews", optional=True)
        if ec is not None:
            engine_crews += ec
        if cc is not None:
            cannon_crews += cc
        if fire_attempted:
            if ec is None or cc is None:
                raise ValueError(f"seed {seed}: fire was attempted but staffed engine/cannon crews are unavailable")
            if ec > engines or cc > cannons:
                raise ValueError(f"seed {seed}: more staffed crews than invested engines/cannons")
            row_shots = int_field(row, "cannonShots")
            row_powder = float_field(row, "powderPaid")
            assert row_powder is not None
            if row_shots < 0 or row_powder < 0:
                raise ValueError(f"seed {seed}: negative shots or powder payment")
            fired_rows += 1
            shots += row_shots
            powder_paid += row_powder
            conservation_rows += 1
            if abs(row_powder - row_shots * 0.25) > 0.011:
                conservation_mismatches += 1
        else:
            row_shots = int_field(row, "cannonShots")
            row_powder = float_field(row, "powderPaid")
            if row_shots not in (0, None) or row_powder not in (0.0, None):
                raise ValueError(f"seed {seed}: no-fire row reports positive cannon fire/powder")

        assigned = int_field(row, "attritionAssignedCrews", optional=True)
        deaths = int_field(row, "attritionDeaths", optional=True)
        def09 = bool_field(row, "def09Active", optional=True)
        if assigned is not None and deaths is not None and def09 is not None:
            attrition_adult_turns += assigned
            if assigned < 0 or deaths < 0 or deaths > assigned:
                raise ValueError(f"seed {seed}: impossible crew attrition counts")
            if assigned > 4 * (engines + cannons):
                raise ValueError(f"seed {seed}: assigned adult count exceeds four per engine/cannon")
            if def09 and assigned == 0:
                raise ValueError(f"seed {seed}: DEF-09 active without assigned crew exposure")

        powder_breach = bool_field(row, "powderBreach", optional=True)
        integrity = float_field(row, "integrity", optional=True)
        if powder_breach:
            breach_flag_rows += 1
            if not any(int_field(prior, "cannonShots", optional=True) and
                       float_field(prior, "powderPaid", optional=True) and
                       float_field(prior, "powderPaid", optional=True) > 0
                       for prior in by_siege[(seed, settlement)]) and not (row_shots and row_shots > 0):
                breach_without_shot += 1
        if integrity is not None and integrity <= 0:
            actual_wall_breach_rows += 1
            actual_wall_breach_sieges.add((seed, settlement))
        by_siege[(seed, settlement)].append(row)
        if season is None:
            raise ValueError(f"seed {seed}: seasonal row lacks season")

    # A DEF-09 attrition observation is only meaningful for the garrisoned
    # concentric-castle stratum. Ensure any such observed stratum is visible at
    # the match's terminal settlement record; a missing row is reported below.
    terminal_by_key = {(int_field(row, "seed"), int_field(row, "settlement")): row for row in terminal_settlements}
    if len(terminal_by_key) != len(terminal_settlements):
        raise ValueError("duplicate terminal settlement rows in the run")
    def09_rows = [row for row in seasonal if bool_field(row, "def09Active", optional=True) is True]
    def09_missing_terminal = 0
    for row in def09_rows:
        key = (int_field(row, "seed"), int_field(row, "settlement"))
        terminal_row = terminal_by_key.get(key)
        if terminal_row is None or terminal_row.get("castleGarrisoned") != "true" or terminal_row.get("concentricCastle") != "true":
            def09_missing_terminal += 1

    terminal_outcomes = Counter(row.get("outcome", "unknown").lower() for row in terminal_settlements)
    for seed in seeds:
        terminal_row = terminal[seed]
        settlement_rows = [row for row in terminal_settlements if int_field(row, "seed") == seed]
        inventory_rows = [row for row in inventory if int_field(row, "seed") == seed]
        if terminal_row.get("exposure") == "none":
            if settlement_rows or inventory_rows or any(int_field(row, "seed") == seed for row in seasonal):
                raise ValueError(f"seed {seed}: terminal says no exposure but siege records are present")
        else:
            expected_settlements = int_field(terminal_row, "settlements")
            if expected_settlements != len(settlement_rows):
                raise ValueError(f"seed {seed}: terminal exposure settlement count does not match terminal rows")
    no_investment_seeds = [seed for seed in seeds if terminal[seed].get("exposure") == "none"]
    observed_seeds = [seed for seed in seeds if terminal[seed].get("exposure") == "observed"]
    censor_by_seed = Counter(int_field(row, "seed") for row in censored)
    terminal_censor = sum(outcome == "right_censored" for outcome in terminal_outcomes.elements())
    concentric = sum(row.get("concentricCastle") == "true" for row in terminal_settlements)
    garrisoned_concentric = sum(row.get("castleGarrisoned") == "true" and row.get("concentricCastle") == "true"
                                for row in terminal_settlements)
    inventory_cannon = sum(float_field(row, "cannonStock") or 0.0 for row in inventory)
    inventory_powder = sum(float_field(row, "powderStock") or 0.0 for row in inventory)
    seeds_with_inventory = len({int_field(row, "seed") for row in inventory})

    source = provenance["source_tree_sha256"]
    if verify_current:
        tools_dir = Path(__file__).resolve().parents[4] / "tools"
        sys.path.insert(0, str(tools_dir))
        from fief_trace_runner import source_tree_hash
        source_manifest = load_json(Path(freeze["pack"]).resolve().parent / provenance["source_manifest"])
        current_hash = source_tree_hash(Path(freeze["source_root"]), source_manifest)
        if current_hash != source:
            raise ValueError(f"live source tree differs from the frozen run: {current_hash} != {source}")

    lines = [
        "# WP-E5-8 natural siege trace report",
        "",
        f"- Frozen manifest: `{manifest_path.name}`; {len(seeds)} seeds; SHA-256 `{manifest_hash}`.",
        f"- Cohort tuple: source `{source}`, pack `{provenance['pack_sha256']}`, Harness `{provenance['harness_sha256']}`, Sim `{provenance['sim_sha256']}`.",
        f"- Run: {freeze['minutes']} minutes; trace `{freeze['trace']}`; runner SHA `{freeze.get('runner_sha256', 'unavailable')}`.",
        f"- Terminal exposure rows: {len(terminal)}/{len(seeds)}; natural siege investment observed in {len(observed_seeds)}/{len(seeds)} seeds; no investment in {len(no_investment_seeds)}/{len(seeds)}.",
        f"- Seasonal siege turns: {len(seasonal)}; right-censored investment snapshots: {len(censored)} ({len(censor_by_seed)} seeds); terminal attacked-settlement rows: {len(terminal_settlements)} ({terminal_censor} right-censored).",
        f"- Engine/cannon asset-turn observations: {engine_asset_turns}/{cannon_asset_turns}; staffed engine/cannon crews across seasons: {engine_crews}/{cannon_crews}; valid assigned adult-turns: {attrition_adult_turns}.",
        f"- Fire-attempt seasons: {fired_rows}; cannon shots: {shots}; powder paid: {powder_paid:.2f} L; shot/powder checks: {conservation_rows}, mismatches: {conservation_mismatches} (tolerance 0.011 L after 2-decimal serialization).",
        f"- Persistent powder-backed breach-flag rows: {breach_flag_rows}; flags without any preceding/current paid shot: {breach_without_shot}; actual wall breaches (unique siege pairs with reported integrity <= 0): {len(actual_wall_breach_sieges)} across {actual_wall_breach_rows} rows.",
        f"- Terminal cannon/powder inventory totals: {inventory_cannon:.2f} / {inventory_powder:.2f} across {seeds_with_inventory}/{len(seeds)} seeds that had investment exposure.",
        f"- Terminal defenses: concentric castles={concentric}; garrisoned concentric castles={garrisoned_concentric}; rows with DEF-09 active but no matching terminal garrisoned concentric state={def09_missing_terminal}.",
        "",
        "This report is descriptive exposure/conservation evidence only. It does not accept or fit the 1%/1.25% attrition hypotheses, prove siege balance, or infer engine/cannon purchases. The trace records investment inventory and final stocks but no acquisition/purchase ledger.",
        "",
        "## Terminal investment outcomes",
        "",
        "| Outcome | Attacked settlement rows |",
        "|---|---:|",
    ]
    for outcome, count in sorted(terminal_outcomes.items()):
        lines.append(f"| {outcome} | {count} |")
    if not terminal_outcomes:
        lines.append("| — | 0 |")
    lines += ["", "## Two-sided siege event rows", "", "| Event | Raw rows | Unique (seed, season, settlement, event) keys |", "|---|---:|---:|"]
    for event, count in sorted(events.items()):
        unique = sum(1 for row in unique_events if row[3] == event)
        lines.append(f"| {event} | {count} | {unique} |")
    if not events:
        lines.append("| — | 0 | 0 |")
    lines += ["", *attrition_report(seeds, seasonal, manifest_hash),
              "", "## No-investment and censored records", "",
              f"No-investment seeds: {', '.join(map(str, no_investment_seeds)) if no_investment_seeds else 'none'}.",
              f"Observed-investment seeds: {', '.join(map(str, observed_seeds)) if observed_seeds else 'none'}.",
              "",
              "A zero-exposure match remains in the manifest denominator. `fief-siege-terminal exposure=none` is a recorded natural non-exposure, not missing data. `right_censored` means an investment remained active at the match horizon; it is not a failure or success outcome.",
              "",
              "## Interpretation limits", "",
              "`engines` and `cannons` are invested assets; `engineCrews` and `cannonCrews` count active machine crews on fire-attempt observations; `attritionAssignedCrews` counts assigned adults per siege turn. The trace does not identify purchase events, so acquisition cannot be reconstructed from ending stocks or investment counts.",
              "`powderBreach=true` is the persistent cannon-fired/breach state used by the combat code. It is distinct from an actual wall breach; the latter is reported only when trace `integrity` is at or below zero. Terminal inventory is emitted only for seeds with at least one observed siege investment.",
              "This trace does not emit the applied defense multiplier, so the natural cohort cannot independently verify the -70% wall-defense calculation; retain the focused mechanics tests/source audit as separate evidence.",
              "Attrition rates use adult-turns as the denominator, stratified by the trace's `def09Active` value. Confidence intervals resample whole match seeds, but the small 20-match cohort can still have sparse exposure; low exposure remains inconclusive. No acceptance threshold is evaluated.",
              ""]
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-dir", required=True, type=Path, help="Completed output directory from fief_trace_runner.py")
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--output", type=Path, help="Markdown report path; defaults to stdout")
    parser.add_argument("--verify-current", action="store_true", help="Also require the live source tree to match the frozen source manifest")
    args = parser.parse_args(argv)
    try:
        report = analyze(args.run_dir, args.manifest, verify_current=args.verify_current)
        if args.output:
            if args.output.exists():
                raise FileExistsError(f"refusing to overwrite existing analysis report: {args.output}")
            args.output.write_text(report, encoding="utf-8")
        else:
            sys.stdout.write(report)
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(f"Fief siege analyzer: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
