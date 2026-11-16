#!/usr/bin/env python3
"""Validate and summarize frozen natural Fief SEA-10 trace runs.

This is an exposure/throughput diagnostic. It intentionally does not produce a
pass/fail acceptance decision or promote numeric catalog values.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from decimal import Decimal
from pathlib import Path
from typing import Any

sys.path.insert(0, str(Path(__file__).resolve().parent))
from cohort_provenance import sha256  # noqa: E402

FIX64_ONE = 1 << 32
SEA_PROJECTS = ("KEY-5", "KEY-6", "SEA-04", "SEA-10")
ROUTES = {"land", "water_locked", "water", "unreachable"}
TRACE_KINDS = {
    "route_rejected", "shipment_dispatched", "voyage_turnback", "voyage_survived",
    "shipment_delivered", "shipment_raid_loss", "shipment_raid_survived",
}
MATCH_RE = re.compile(
    r"^== fief-match seed (?P<seed>\d+): (?P<minutes>[\d.]+) minutes simulated, "
    r"chapter end (?P<end>.*?) at season (?P<season>.*?) \(ended: (?P<ended>True|False)\)$"
)
KV_RE = re.compile(r"^([A-Za-z][A-Za-z0-9]*)=([^\s]+)$")
EVENT_REQUIRED = {
    "seed", "season", "shipment", "compact", "priorDeliveries", "sellerCiv", "buyerCiv",
    "seller", "buyer", "good", "route", "physicalWaterTiles", "eligibleWaterTiles",
    "SEA04", "KEY6", "SEA10", "reason", "requested", "capped", "taken", "carried",
    "delivered", "paid", "returned", "lost", "arrivalSeason",
}
MILESTONE_REQUIRED = {"seed", "season", "civ", "project", "complete", "completedSeason", "canSail"}
BOOL_FIELDS = {"SEA04", "KEY6", "SEA10", "complete", "canSail"}
INT_FIELDS = {
    "seed", "season", "shipment", "compact", "priorDeliveries", "sellerCiv", "buyerCiv",
    "seller", "buyer", "physicalWaterTiles", "eligibleWaterTiles", "arrivalSeason",
    "civ", "completedSeason", "requested", "capped", "taken", "carried", "delivered",
    "paid", "returned", "lost",
}


def load_seeds(path: Path) -> list[int]:
    seeds: list[int] = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.lower() == "seed":
            continue
        try:
            seed = int(line)
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: expected integer seed, got {line!r}") from exc
        if seed < 0:
            raise ValueError(f"{path}:{line_number}: seed must be nonnegative")
        seeds.append(seed)
    if not seeds or len(seeds) != len(set(seeds)):
        raise ValueError(f"seed manifest must contain unique nonnegative seeds; found {len(seeds)}")
    return seeds


def canonical_source_hash(source_manifest: dict[str, Any]) -> str:
    files = source_manifest.get("files")
    head = source_manifest.get("head")
    if not isinstance(files, list) or not files or not isinstance(head, str) or not head:
        raise ValueError("source manifest must contain a nonempty sorted file list and HEAD")
    paths = [entry.get("path") for entry in files if isinstance(entry, dict)]
    if len(paths) != len(files) or paths != sorted(paths) or len(paths) != len(set(paths)):
        raise ValueError("source manifest paths must be unique and sorted")
    for entry in files:
        if not isinstance(entry.get("sha256"), str) or not re.fullmatch(r"[0-9a-f]{64}", entry["sha256"]):
            raise ValueError(f"invalid source-file SHA-256 for {entry.get('path')!r}")
    payload = {"head": head, "files": files}
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def load_provenance(path: Path) -> tuple[dict[str, Any], str]:
    provenance = json.loads(path.read_text(encoding="utf-8"))
    required = {
        "source_tree_sha256", "source_manifest", "source_manifest_sha256",
        "pack_sha256", "harness_sha256", "sim_sha256",
    }
    missing = sorted(required - provenance.keys())
    if missing:
        raise ValueError(f"provenance is missing required fields: {', '.join(missing)}")
    for key in required - {"source_manifest"}:
        if not isinstance(provenance[key], str) or not re.fullmatch(r"[0-9a-f]{64}", provenance[key]):
            raise ValueError(f"provenance {key} must be a SHA-256 hex string")
    source_path = (path.resolve().parent / provenance["source_manifest"]).resolve()
    if path.resolve().parent not in source_path.parents:
        raise ValueError("source manifest path escapes the provenance directory")
    if not source_path.is_file() or sha256(source_path) != provenance["source_manifest_sha256"]:
        raise ValueError("frozen source-manifest file is missing or has a hash mismatch")
    source_manifest = json.loads(source_path.read_text(encoding="utf-8"))
    if canonical_source_hash(source_manifest) != provenance["source_tree_sha256"]:
        raise ValueError("source-manifest aggregate does not match frozen provenance")
    return provenance, sha256(path)


def read_jsonl(path: Path) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        try:
            row = json.loads(line)
        except json.JSONDecodeError as exc:
            raise ValueError(f"{path}:{line_number}: invalid JSON: {exc}") from exc
        if not isinstance(row, dict):
            raise ValueError(f"{path}:{line_number}: expected JSON object")
        rows.append(row)
    return rows


def validate_frozen_run(run_dir: Path, seeds_path: Path, provenance_path: Path) -> tuple[list[int], dict[str, Any]]:
    run_dir = run_dir.resolve()
    seeds_path = seeds_path.resolve()
    provenance_path = provenance_path.resolve()
    freeze_path = run_dir / "run-freeze.json"
    status_path = run_dir / "run-status.jsonl"
    if not freeze_path.is_file() or not status_path.is_file():
        raise FileNotFoundError("run-freeze.json and run-status.jsonl are required")
    seeds = load_seeds(seeds_path)
    freeze = json.loads(freeze_path.read_text(encoding="utf-8"))
    provenance, provenance_hash = load_provenance(provenance_path)
    if freeze.get("provenance") != provenance:
        raise ValueError("run freeze provenance does not match the supplied frozen provenance file")
    if freeze.get("minutes") != 130 or freeze.get("trace") != "--fief-sea10-trace":
        raise ValueError("run freeze must be the 130-minute --fief-sea10-trace protocol")
    if freeze.get("seeds") != seeds or freeze.get("seed_file_sha256") != sha256(seeds_path):
        raise ValueError("run freeze seed list or seed-manifest SHA does not match the supplied manifest")
    if Path(freeze.get("output", "")).resolve() != run_dir:
        raise ValueError("run freeze output path does not match the analyzed directory")
    for label, key, path_key in (
        ("pack", "pack_sha256", "pack"), ("Harness", "harness_sha256", "harness"),
        ("Sim", "sim_sha256", "sim"),
    ):
        path = Path(freeze.get(path_key, ""))
        if not path.is_file() or sha256(path) != provenance[key]:
            raise ValueError(f"frozen {label} is missing or has a hash mismatch")
    if Path(freeze.get("sim", "")).resolve() != Path(freeze.get("harness", "")).resolve().with_name("CivSim.Sim.dll"):
        raise ValueError("frozen Sim must be adjacent to the frozen Harness")
    runner = Path(freeze.get("runner", ""))
    if not runner.is_file() or sha256(runner) != freeze.get("runner_sha256"):
        raise ValueError("frozen trace-runner is missing or has a hash mismatch")
    source_manifest_path = (provenance_path.parent / provenance["source_manifest"]).resolve()
    metadata = {
        "freeze": freeze,
        "freeze_sha256": sha256(freeze_path),
        "provenance_sha256": provenance_hash,
        "source_manifest_sha256": provenance["source_manifest_sha256"],
        "seeds_sha256": sha256(seeds_path),
        "source_manifest_path": str(source_manifest_path),
    }

    run_status = read_jsonl(status_path)
    if not run_status or run_status[0].get("status") != "running" or run_status[-1].get("status") != "completed":
        raise ValueError("run status must begin with running and end with completed")
    terminal_states = [row for row in run_status if row.get("status") in {"failed", "interrupted"}]
    if terminal_states:
        raise ValueError("run status contains a failed or interrupted event")
    if run_status[-1].get("seed_count") != len(seeds):
        raise ValueError("completed run-status seed_count does not match manifest")

    expected_files = {f"seed-{seed}.txt" for seed in seeds}
    raw_files = {p.name for p in run_dir.glob("seed-*.txt")}
    if raw_files != expected_files:
        raise ValueError(f"raw seed files differ from frozen manifest; missing={sorted(expected_files-raw_files)}, extra={sorted(raw_files-expected_files)}")
    expected_status = {f"seed-{seed}.status.jsonl" for seed in seeds}
    actual_status = {p.name for p in run_dir.glob("seed-*.status.jsonl")}
    if actual_status != expected_status:
        raise ValueError(f"per-seed status files differ from frozen manifest; missing={sorted(expected_status-actual_status)}, extra={sorted(actual_status-expected_status)}")
    match_records: list[dict[str, Any]] = []
    for seed in seeds:
        status_file = run_dir / f"seed-{seed}.status.jsonl"
        raw_file = run_dir / f"seed-{seed}.txt"
        if not status_file.is_file():
            raise FileNotFoundError(f"per-seed status is missing for seed {seed}")
        seed_status = read_jsonl(status_file)
        final = seed_status[-1] if seed_status else {}
        if (not seed_status or seed_status[0].get("status") != "running" or final.get("seed") != seed or
                final.get("status") != "completed" or final.get("exit_code") != 0 or
                any(row.get("status") in {"failed", "interrupted"} for row in seed_status)):
            raise ValueError(f"seed {seed} is not recorded as a successful completed run")
        if final.get("output_sha256") != sha256(raw_file):
            raise ValueError(f"raw output SHA mismatch for seed {seed}")
        match_headers = [MATCH_RE.match(line.strip()) for line in raw_file.read_text(encoding="utf-8").splitlines()
                         if MATCH_RE.match(line.strip())]
        if (len(match_headers) != 1 or int(match_headers[0].group("seed")) != seed or
                not 0.0 <= float(match_headers[0].group("minutes")) <= 130.0):
            raise ValueError(f"seed {seed} must have exactly one fief-match header with matching seed and no more than 130 simulated minutes")
        match_records.append({
            "seed": seed,
            "simulated_minutes": float(match_headers[0].group("minutes")),
            "chapter_end": match_headers[0].group("end"),
            "end_season": match_headers[0].group("season"),
            "ended": match_headers[0].group("ended") == "True",
        })
    metadata["match_records"] = match_records
    metadata["run_status_sha256"] = sha256(status_path)
    return seeds, metadata


def parse_trace_row(line: str, source: str) -> dict[str, Any] | None:
    stripped = line.strip()
    if not stripped.startswith("sea10-trace "):
        return None
    values: dict[str, str] = {}
    for token in stripped.split()[1:]:
        match = KV_RE.match(token)
        if not match:
            raise ValueError(f"{source}: malformed trace token {token!r}")
        key, value = match.groups()
        if key in values:
            raise ValueError(f"{source}: duplicate trace field {key!r}")
        values[key] = value
    kind = values.get("kind")
    required = MILESTONE_REQUIRED if kind == "milestone" else EVENT_REQUIRED
    missing = sorted(required - values.keys())
    if missing:
        raise ValueError(f"{source}: {kind!r} row missing fields {missing}")
    if kind != "milestone" and kind not in TRACE_KINDS:
        raise ValueError(f"{source}: unknown SEA-10 trace kind {kind!r}")
    if kind != "milestone" and values.get("route") not in ROUTES:
        raise ValueError(f"{source}: unknown route state {values.get('route')!r}")
    for field in INT_FIELDS & values.keys():
        try:
            values[field] = int(values[field])
        except ValueError as exc:
            raise ValueError(f"{source}: {field} must be an integer") from exc
    for field in BOOL_FIELDS & values.keys():
        if values[field] not in {"true", "false"}:
            raise ValueError(f"{source}: {field} must be true or false")
        values[field] = values[field] == "true"
    for field in ("requested", "capped", "taken", "carried", "delivered", "paid", "returned", "lost"):
        if field in values and values[field] < 0:
            raise ValueError(f"{source}: {field} must be a nonnegative Fix64 raw value")
    if kind == "milestone":
        if values["project"] not in SEA_PROJECTS:
            raise ValueError(f"{source}: unexpected SEA milestone {values['project']!r}")
        if values["complete"] != (values["completedSeason"] >= 0):
            raise ValueError(f"{source}: milestone completion and completedSeason disagree")
    return values


def parse_raw(path: Path, expected_seed: int) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        try:
            row = parse_trace_row(line, f"{path}:{line_number}")
        except ValueError as exc:
            raise ValueError(str(exc)) from exc
        if row is None:
            continue
        if row["seed"] != expected_seed:
            raise ValueError(f"{path}:{line_number}: trace seed {row['seed']} does not match manifest seed {expected_seed}")
        rows.append(row)
    return rows


def fix64(raw: int) -> Decimal:
    return Decimal(raw) / Decimal(FIX64_ONE)


def fmt(value: Decimal) -> str:
    return f"{value.quantize(Decimal('0.0001')):,.4f}"


def shipment_summary(rows: list[dict[str, Any]]) -> tuple[dict[tuple[int, int], dict[str, Any]], list[tuple[int, int]]]:
    shipments: dict[tuple[int, int], dict[str, Any]] = {}
    for row in rows:
        if row["kind"] == "milestone" or row["shipment"] < 0:
            continue
        key = (row["seed"], row["shipment"])
        record = shipments.setdefault(key, {"events": [], "dispatch": None, "terminal": None})
        record["events"].append(row)
        if row["kind"] == "shipment_dispatched":
            if record["dispatch"] is not None:
                raise ValueError(f"duplicate shipment dispatch for seed={key[0]} shipment={key[1]}")
            record["dispatch"] = row
        if row["kind"] in {"voyage_turnback", "shipment_delivered", "shipment_raid_loss"}:
            if row["kind"] == "shipment_raid_loss" and row["lost"] == 0:
                continue
            if record["terminal"] is not None:
                raise ValueError(f"multiple terminal shipment events for seed={key[0]} shipment={key[1]}")
            record["terminal"] = row
    pending: list[tuple[int, int]] = []
    for key, record in shipments.items():
        if record["dispatch"] is None:
            raise ValueError(f"shipment event lacks dispatch record for seed={key[0]} shipment={key[1]}")
        dispatch = record["dispatch"]
        for event in record["events"]:
            if event["good"] != dispatch["good"]:
                raise ValueError(f"shipment good changes for seed={key[0]} shipment={key[1]}")
            if event["kind"] != "shipment_dispatched" and event["carried"] != dispatch["carried"]:
                raise ValueError(f"shipment carried amount changes for seed={key[0]} shipment={key[1]}")
        cargo = dispatch["carried"]
        if dispatch["carried"] != dispatch["taken"] or dispatch["taken"] > dispatch["capped"] or dispatch["capped"] > dispatch["requested"]:
            raise ValueError(
                f"dispatch quantities do not reconcile for seed={key[0]} shipment={key[1]}: "
                "require 0 <= taken = carried <= capped <= requested"
            )
        terminal = record["terminal"]
        if terminal is None:
            pending.append(key)
            continue
        if terminal["kind"] == "shipment_delivered":
            reconciled = terminal["delivered"] + terminal["returned"]
        elif terminal["kind"] == "voyage_turnback":
            reconciled = terminal["returned"]
        else:
            reconciled = terminal["lost"]
        if reconciled != cargo:
            raise ValueError(
                f"shipment cargo does not reconcile for seed={key[0]} shipment={key[1]}: "
                f"dispatch carried={cargo}, terminal sum={reconciled}"
            )
    return shipments, pending


def markdown_report(seeds: list[int], all_rows: list[dict[str, Any]], metadata: dict[str, Any]) -> str:
    milestones = [row for row in all_rows if row["kind"] == "milestone"]
    events = [row for row in all_rows if row["kind"] != "milestone"]
    shipments, pending = shipment_summary(all_rows)
    route_counts = Counter((row["kind"], row["route"]) for row in events)
    rejection_reasons = Counter(row["reason"] for row in events if row["kind"] == "route_rejected")
    water_dispatches = [record["dispatch"] for record in shipments.values()
                        if record["dispatch"]["route"] == "water"]
    water_dispatch_keys = {(row["seed"], row["shipment"]) for row in water_dispatches}
    water_delivery_events = [record["terminal"] for key, record in shipments.items()
                             if key in water_dispatch_keys and record["terminal"] is not None and
                             record["terminal"]["kind"] == "shipment_delivered"]
    water_positive_deliveries = sum(row["delivered"] > 0 for row in water_delivery_events)
    water_turned_back = sum(1 for key, record in shipments.items()
                            if key in water_dispatch_keys and record["terminal"] is not None and
                            record["terminal"]["kind"] == "voyage_turnback")
    water_lost = sum(1 for key, record in shipments.items()
                     if key in water_dispatch_keys and record["terminal"] is not None and
                     record["terminal"]["kind"] == "shipment_raid_loss")
    water_pending = sum(1 for key, record in shipments.items()
                        if key in water_dispatch_keys and record["terminal"] is None)
    pending_water_keys = sorted(f"{seed}:{shipment}" for seed, shipment in pending
                                if shipments[(seed, shipment)]["dispatch"]["route"] == "water")
    no_trace_seeds = [seed for seed in seeds if not any(row["seed"] == seed for row in all_rows)]
    no_route_rows = [seed for seed in seeds if not any(row["seed"] == seed and row["kind"] != "milestone" for row in all_rows)]
    water_locked_seeds = sorted({row["seed"] for row in events if row["route"] == "water_locked"})
    water_route_seeds = sorted({row["seed"] for row in water_dispatches})
    all_milestones: list[str] = []
    for project in SEA_PROJECTS:
        project_rows = [row for row in milestones if row["project"] == project]
        # A project can repeat as complete in each later season; retain its first observed completion season.
        first: dict[tuple[int, int], int] = {}
        for row in project_rows:
            if row["complete"]:
                key = (row["seed"], row["civ"])
                first[key] = min(first.get(key, row["completedSeason"]), row["completedSeason"])
        seasons = list(first.values())
        all_milestones.append(
            f"| {project} | {len(first)} / {len(seeds) * 4} seats | "
            f"{statistics.median(seasons):.1f}" if seasons else f"| {project} | 0 / {len(seeds) * 4} seats | —"
        )
        if seasons:
            all_milestones[-1] += f" | {min(seasons)}–{max(seasons)} |"
        else:
            all_milestones[-1] += " | — |"

    by_good: dict[str, list[tuple[tuple[int, int], dict[str, Any]]]] = defaultdict(list)
    for key, record in shipments.items():
        dispatch = record["dispatch"]
        by_good[str(dispatch["good"])].append((key, record))
    cargo_lines: list[str] = []
    for good in sorted(by_good):
        records = by_good[good]
        dispatch_rows = [record["dispatch"] for _, record in records]
        terminal_rows = [record["terminal"] for _, record in records if record["terminal"] is not None]
        # Do not add lifecycle snapshots together: each cargo aggregate uses one event class only.
        requested = sum((fix64(row["requested"]) for row in dispatch_rows), Decimal(0))
        capped = sum((fix64(row["capped"]) for row in dispatch_rows), Decimal(0))
        taken = sum((fix64(row["taken"]) for row in dispatch_rows), Decimal(0))
        carried = sum((fix64(row["carried"]) for row in dispatch_rows), Decimal(0))
        delivered = sum((fix64(row["delivered"]) for row in terminal_rows if row["kind"] == "shipment_delivered"), Decimal(0))
        voyage_returned = sum((fix64(row["returned"]) for row in terminal_rows if row["kind"] == "voyage_turnback"), Decimal(0))
        settlement_returned = sum((fix64(row["returned"]) for row in terminal_rows if row["kind"] == "shipment_delivered"), Decimal(0))
        lost = sum((fix64(row["lost"]) for row in terminal_rows if row["kind"] == "shipment_raid_loss"), Decimal(0))
        units = "R (food units)" if good == "Food" else "good-specific cargo units"
        liters_note = (f"delivered {fmt(delivered / 2)} L; returned {fmt((voyage_returned + settlement_returned) / 2)} L"
                       if good == "Food" else "—")
        cargo_lines.append(
            f"| {good} | {len(dispatch_rows)} | {units} | {fmt(requested)} | {fmt(capped)} | "
            f"{fmt(taken)} | {fmt(carried)} | {fmt(delivered)} | {fmt(voyage_returned)} | "
            f"{fmt(settlement_returned)} | {fmt(lost)} | {liters_note} |"
        )

    cap_groups: dict[tuple[str, bool], list[dict[str, Any]]] = defaultdict(list)
    for row in water_dispatches:
        cap_groups[(str(row["good"]), row["SEA10"])].append(row)
    cap_lines: list[str] = []
    for (good, sea10), dispatch_rows in sorted(cap_groups.items()):
        capacity_l = Decimal(24 if sea10 else 12)
        capacity = capacity_l * 2 if good == "Food" else capacity_l
        eligible = [row for row in dispatch_rows if fix64(row["requested"]) >= capacity]
        full = sum(fix64(row["capped"]) == capacity for row in eligible)
        limited = [row for row in dispatch_rows if fix64(row["requested"]) < capacity]
        cap_mismatch = sum(fix64(row["capped"]) != capacity for row in eligible)
        cap_mismatch += sum(fix64(row["capped"]) != fix64(row["requested"]) for row in limited)
        unit = "R (food units)" if good == "Food" else "good-specific cargo units"
        cap_text = f"{fmt(capacity)} {unit} (= {fmt(capacity_l)} L approx.)" if good == "Food" else f"{fmt(capacity)} {unit} (12/24 L rule; no density conversion)"
        cap_lines.append(f"| {good} | {str(sea10).lower()} | {len(dispatch_rows)} | {len(eligible)} | {full} | {len(limited)} | {cap_mismatch} | {cap_text} |")

    route_table = [f"| {kind} | {route} | {count} |" for (kind, route), count in sorted(route_counts.items())]
    reason_table = [f"| {reason} | {count} |" for reason, count in sorted(rejection_reasons.items())]
    freeze = metadata["freeze"]
    provenance = freeze["provenance"]
    lines = [
        "# SEA-10 natural-route trace summary",
        "",
        "**Diagnostic only.** This report describes exposure and cargo flow; it does not accept a balance threshold or promote a catalog row.",
        "",
        "## Frozen run identity",
        "",
        f"- Seeds: {len(seeds)}; manifest SHA-256 `{metadata['seeds_sha256']}`; 130 minutes per match.",
        f"- Source aggregate `{provenance['source_tree_sha256']}`; pack `{provenance['pack_sha256']}`; Harness `{provenance['harness_sha256']}`; Sim `{provenance['sim_sha256']}`.",
        f"- Run freeze SHA-256 `{metadata['freeze_sha256']}`; run status SHA-256 `{metadata['run_status_sha256']}`.",
        f"- Verified completed match logs: {len(seeds)}/{len(seeds)}; trace rows: {len(all_rows)} ({len(milestones)} milestone, {len(events)} route/shipment).",
        f"- Matches with less than 130 simulated minutes: {sum(row['simulated_minutes'] < 130.0 for row in metadata['match_records'])}/{len(seeds)}; chapter-ended flags: {sum(bool(row['ended']) for row in metadata['match_records'])}/{len(seeds)}.",
        "| Seed | Simulated minutes | Chapter end | End season | Ended flag |",
        "|---:|---:|---|---|---|",
        *(f"| {row['seed']} | {row['simulated_minutes']:.1f} | {row['chapter_end']} | {row['end_season']} | {str(row['ended']).lower()} |"
          for row in metadata["match_records"]),
        "",
        "## Route exposure",
        "",
        f"- Distinct actual water-route dispatches: {len(water_dispatches)} across {len(water_route_seeds)} seeds ({', '.join(map(str, water_route_seeds)) if water_route_seeds else 'none'}).",
        f"- Water shipment terminal outcomes: {len(water_delivery_events)} delivery/settlement events ({water_positive_deliveries} with positive delivered cargo), {water_turned_back} voyage returns, {water_lost} raid losses, {water_pending} still without a traced terminal shipment event at the match horizon.",
        f"- Censored water shipments (seed:shipment): {', '.join(pending_water_keys) if pending_water_keys else 'none'}.",
        f"- Seeds with water-locked route observations: {len(water_locked_seeds)} ({', '.join(map(str, water_locked_seeds)) if water_locked_seeds else 'none'}).",
        f"- Seeds with no SEA-10 trace rows: {len(no_trace_seeds)} ({', '.join(map(str, no_trace_seeds)) if no_trace_seeds else 'none'}); seeds with milestones but no route/shipment rows: {len(no_route_rows)} ({', '.join(map(str, no_route_rows)) if no_route_rows else 'none'}).",
        "",
        "| Trace event | Route state | Rows |",
        "|---|---|---:|",
        *(route_table or ["| — | — | 0 |"]),
        "",
        "Route rejection reasons:",
        "",
        "| Reason | Rows |",
        "|---|---:|",
        *(reason_table or ["| — | 0 |"]),
        "",
        "## Research state",
        "",
        "Milestone season is the first observed completion season for each seed/civilization. The table counts seats, not independent worlds.",
        "",
        "| Project | Completed seats | Median completion season | Range |",
        "|---|---:|---:|---:|",
        *all_milestones,
        "",
        "## Cargo accounting",
        "",
        "Values below use dispatch rows once for requested/capped/taken/carried and terminal rows once for delivery/return/loss; lifecycle snapshots are not summed. The dispatch relation must satisfy `0 <= taken = carried <= capped <= requested`, and each observed terminal cargo amount must reconcile to carried. Fix64 raw values are divided by 2³². `paid` is an economic settlement measure across goods and is not added to cargo quantities.",
        "Food quantities are reported in R units, with the documented approximation 2 R = 1 L shown separately. Other goods remain in good-specific cargo units; they are not labeled liters.",
        "",
        "| Good | Water/land dispatches | Cargo unit | Requested | Capped | Taken | Carried at dispatch | Delivered | Voyage returned | Settlement returned | Lost | Food volume equivalents |",
        "|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---|",
        *(cargo_lines or ["| — | 0 | — | — | — | — | — | — | — | — | — | — |"]),
        "",
        "| Good | SEA-10 at dispatch | Water dispatches | Requests ≥ capacity | Capped at capacity | Request-limited | Cap/request mismatches | Capacity interpretation |",
        "|---|---|---:|---:|---:|---:|---:|---|",
        *(cap_lines or ["| — | — | 0 | 0 | 0 | 0 | 0 | no water dispatch observed |"]),
        "",
        "The cargo table has a separate food-volume column for 2 R ≈ 1 L. The cap table compares `capped` with the 12/24 capacity rule only for water-route dispatches where requested cargo reaches capacity; a smaller capped amount can simply reflect a smaller request. Other goods are reported in their good-specific cargo units without an assumed density conversion. Report cap observations separately from throughput and from shipments that were delivered, returned, lost, or still pending.",
        "",
        "## Interpretation limits",
        "",
        "- A `water_locked` rejection before KEY-6 does not establish a post-KEY-6 water shipment; only `shipment_dispatched` rows with `route=water` count as actual water exposure.",
        "- `carried` is an event-time snapshot and is not a flow to add to requested/taken/delivered. `voyage_turnback.returned` is a route return; `shipment_delivered.returned` is the settlement remainder returned to the seller; keep them separate.",
        "- A shipment without a terminal event is right-censored at the match horizon. No trace row is not evidence of zero route eligibility or zero capacity.",
        "- These natural runs reveal route access and observed flow only. They do not by themselves close WP-E5-8 or establish acceptance limits.",
        "",
    ]
    return "\n".join(lines)


def analyze(run_dir: Path, seeds_path: Path, provenance_path: Path) -> str:
    seeds, metadata = validate_frozen_run(run_dir, seeds_path, provenance_path)
    rows: list[dict[str, Any]] = []
    for seed in seeds:
        rows.extend(parse_raw(Path(run_dir) / f"seed-{seed}.txt", seed))
    return markdown_report(seeds, rows, metadata)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--run-dir", type=Path, required=True)
    parser.add_argument("--seeds", type=Path, required=True)
    parser.add_argument("--provenance", type=Path, required=True)
    parser.add_argument("--output", type=Path, help="write Markdown report here (default: stdout)")
    args = parser.parse_args(argv)
    try:
        report = analyze(args.run_dir, args.seeds, args.provenance)
        if args.output:
            args.output.write_text(report, encoding="utf-8")
        else:
            print(report, end="")
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
        print(f"SEA-10 trace analysis failed: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
