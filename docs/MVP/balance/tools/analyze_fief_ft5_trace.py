#!/usr/bin/env python3
"""Deterministically summarize F-T5 siege trace sidecars (key/value text, JSONL, or CSV)."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

EXPECTED_MANIFEST_SHA256 = "f787766662b46319f7f0807eacd27592aa8c2af4163f366d52ddec07a3e3a786"
DEFAULT_MANIFEST = Path(__file__).resolve().parents[1] / "m12-e5-8-f-t5-natural-seeds-2026-11-15.txt"
KV = re.compile(r"([A-Za-z][A-Za-z0-9]*)=([^\s]+)")
TERMINAL_EVENTS = {"investmentrelieved", "investmentcapitulated", "investmentsally", "assaultresolved", "captured"}
LOW_EXPOSURE_ADULT_SEASONS = 20


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_manifest(path: Path) -> list[int]:
    seeds = []
    for line_number, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = raw.strip()
        if not line or line.startswith("#") or line.lower() == "seed":
            continue
        try:
            seeds.append(int(line))
        except ValueError as exc:
            raise ValueError(f"{path}:{line_number}: invalid seed {line!r}") from exc
    if len(seeds) != 20 or len(set(seeds)) != 20:
        raise ValueError(f"expected 20 unique frozen F-T5 seeds; found {len(seeds)}")
    if sha256(path) != EXPECTED_MANIFEST_SHA256:
        raise ValueError("F-T5 seed manifest SHA-256 mismatch")
    return seeds


def normalize(row: dict) -> dict[str, str]:
    result = {str(k).strip(): str(v).strip() for k, v in row.items() if k is not None and v is not None}
    marker = result.get("recordType", result.get("type", result.get("kind", ""))).lower()
    if marker and marker not in ("fief-siege-trace", "siege-trace", "siege",
                                  "fief-siege-terminal", "fief-siege-terminal-inventory",
                                  "fief-siege-terminal-settlement"):
        return {}
    return result


def read_rows(path: Path) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    if path.suffix.lower() == ".csv":
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for raw in csv.DictReader(stream):
                row = normalize(raw)
                if row and ("seed" in row or "settlement" in row):
                    rows.append(row)
        return rows
    for line_number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith("fief-siege-trace ") or stripped.startswith("fief-siege-terminal"):
            marker, _, payload = stripped.partition(" ")
            row = normalize({"recordType": marker, **dict(KV.findall(payload))})
        elif stripped.startswith("{"):
            try:
                value = json.loads(stripped)
            except json.JSONDecodeError as exc:
                raise ValueError(f"{path}:{line_number}: invalid JSON: {exc}") from exc
            row = normalize(value) if isinstance(value, dict) else {}
        else:
            continue
        if row:
            rows.append(row)
    return rows


def int_value(row: dict[str, str], key: str) -> int | None:
    value = row.get(key, "")
    if not value or value.lower() == "unavailable":
        return None
    try:
        return int(value)
    except ValueError as exc:
        raise ValueError(f"invalid integer {key}={value!r}") from exc


def attrition_deaths(row: dict[str, str]) -> int | None:
    """Accept the explicit field and the earlier deadCrews sidecar spelling."""
    return int_value(row, "attritionDeaths") if "attritionDeaths" in row else int_value(row, "deadCrews")


def crew_exposure_rows(rows: list[dict[str, str]]) -> list[str]:
    groups: dict[str, list[dict[str, str]]] = {"false": [], "true": []}
    unavailable = 0
    for row in rows:
        flag = row.get("def09Active", "").lower()
        assigned = int_value(row, "attritionAssignedCrews")
        if flag not in groups or assigned is None:
            unavailable += 1
            continue
        if assigned > 0:
            groups[flag].append(row)

    lines = [
        "",
        "## Siege crew attrition exposure",
        "",
        f"Rates use observed deaths per assigned adult-season (four adults per engine/cannon crew). Fewer than {LOW_EXPOSURE_ADULT_SEASONS} assigned adult-seasons is labeled low exposure and inconclusive; even above that count, rates are descriptive only and are not a pass/fail gate.",
        "",
        "| Stratum | Siege-season exposures | Assigned adult-seasons | Observed deaths | Rate | Status |",
        "|---|---:|---:|---:|---:|---|",
    ]
    for flag, label in (("false", "Base (DEF-09 inactive)"), ("true", "DEF-09 active")):
        group = groups[flag]
        crew_seasons = sum(int_value(row, "attritionAssignedCrews") or 0 for row in group)
        available_deaths = [attrition_deaths(row) for row in group]
        known = [value for value in available_deaths if value is not None]
        missing = len(available_deaths) - len(known)
        deaths = sum(known)
        rate = f"{deaths / crew_seasons:.4f}" if crew_seasons and missing == 0 else (f"{deaths}/{crew_seasons} known" if crew_seasons else "—")
        if crew_seasons < LOW_EXPOSURE_ADULT_SEASONS:
            status = "INCONCLUSIVE (low/no exposure)"
        else:
            status = "DESCRIPTIVE ONLY; no pass/fail threshold"
        if missing:
            status += f"; {missing} death counts unavailable"
        lines.append(f"| {label} | {len(group)} | {crew_seasons} | {deaths}" +
                     (f" ({missing} unavailable)" if missing else "") + f" | {rate} | {status} |")
    lines.append(f"\nRows with missing/unknown DEF-09 flag or attrition assignment count: {unavailable}.")
    return lines


def analyze(raw_dir: Path, manifest_path: Path) -> str:
    seeds = load_manifest(manifest_path)
    rows_by_seed: dict[int, list[dict[str, str]]] = defaultdict(list)
    files_by_seed: dict[int, list[Path]] = defaultdict(list)
    for path in raw_dir.glob("seed-*.*"):
        suffix = path.stem[5:]
        if suffix.isdigit():
            files_by_seed[int(suffix)].append(path)
    missing = [seed for seed in seeds if not files_by_seed[seed]]
    if missing:
        raise FileNotFoundError(f"missing raw output for manifest seed(s): {missing}")
    for seed, paths in files_by_seed.items():
        for path in sorted(paths):
            for row in read_rows(path):
                try:
                    row_seed = int_value(row, "seed")
                except ValueError as exc:
                    raise ValueError(f"{path}: {exc}") from exc
                if row_seed != seed:
                    raise ValueError(f"{path}: trace row seed {row_seed!r} does not match filename/manifest seed {seed}")
                rows_by_seed[seed].append(row)
    extra = [p.name for p in raw_dir.glob("seed-*.*") if p.stem.startswith("seed-") and
             p.stem[5:].isdigit() and int(p.stem[5:]) not in set(seeds)]
    if extra:
        raise ValueError(f"raw directory contains seeds outside frozen manifest: {', '.join(sorted(extra))}")

    seasonal: list[dict[str, str]] = []
    event_rows: list[dict[str, str]] = []
    censored: list[dict[str, str]] = []
    terminal: list[dict[str, str]] = []
    terminal_inventory: list[dict[str, str]] = []
    terminal_settlements: list[dict[str, str]] = []
    for seed in seeds:
        for row in rows_by_seed[seed]:
            record_type = row.get("recordType", "").lower()
            if record_type == "fief-siege-terminal":
                terminal.append(row)
                continue
            if record_type == "fief-siege-terminal-inventory":
                terminal_inventory.append(row)
                continue
            if record_type == "fief-siege-terminal-settlement":
                terminal_settlements.append(row)
                continue
            outcome = row.get("outcome", "").lower()
            if outcome == "right_censored":
                censored.append(row)
            elif row.get("siegeTurn", "") == "terminal" or outcome in TERMINAL_EVENTS:
                event_rows.append(row)
            elif outcome in {"active", "ended"}:
                seasonal.append(row)

    exposure = Counter(row.get("exposed", "unavailable").lower() for row in seasonal)
    shots = sum(int_value(row, "cannonShots") or 0 for row in seasonal)
    powder = sum(float(row.get("powderPaid", "0")) for row in seasonal if row.get("powderPaid", "").lower() != "unavailable")
    conservation_rows = [row for row in seasonal if int_value(row, "cannonShots") is not None and
                         row.get("powderPaid", "").lower() != "unavailable" and row.get("powderPaid", "")]
    conservation_mismatches = sum(abs(float(row["powderPaid"]) - 0.25 * int_value(row, "cannonShots")) > 0.011
                                 for row in conservation_rows)
    breach_gate_rows = [row for row in seasonal if row.get("powderBreach", "").lower() == "true"]
    breach_without_shot = sum((int_value(row, "cannonShots") or 0) <= 0 or
                              float(row.get("powderPaid", "0")) <= 0 for row in breach_gate_rows)
    breached = {(int_value(row, "seed"), int_value(row, "settlement")) for row in seasonal
                if row.get("powderBreach", "").lower() == "true"}
    outcomes = Counter(row.get("outcome", "unknown").lower() for row in event_rows)
    unique_outcomes = {(int_value(row, "seed"), int_value(row, "season"), int_value(row, "settlement"),
                        row.get("outcome", "unknown").lower()) for row in event_rows}
    crew_unavailable = sum(attrition_deaths(row) is None for row in seasonal)
    seeds_with_rows = sum(bool(rows_by_seed[seed]) for seed in seeds)
    no_investment_seeds = [seed for seed in seeds if not any(int_value(row, "seed") == seed for row in seasonal)]
    terminal_no_exposure = sum(row.get("exposure", "").lower() == "none" for row in terminal)
    terminal_observed = sum(row.get("exposure", "").lower() == "observed" for row in terminal)
    terminal_censored = sum(row.get("outcome", "").lower() == "right_censored" for row in terminal_settlements)
    terminal_breaches = sum(row.get("powderBreach", "").lower() == "true" for row in terminal_settlements)
    defense_counts = {field: sum(row.get(field, "").lower() == "true" for row in terminal_settlements)
                      for field in ("stoneWalls", "gatehouse", "castle", "castleGarrisoned", "concentricCastle")}
    inventory_unavailable = sum(any(row.get(field, "").lower() == "unavailable" or not row.get(field)
                                    for field in ("cannonStock", "powderStock"))
                                for row in terminal_inventory)

    lines = [
        "# F-T5 siege trace summary",
        "",
        f"- Frozen manifest: `{manifest_path.name}` ({len(seeds)} seeds; SHA-256 `{sha256(manifest_path)}`).",
        f"- Raw files with trace rows: {seeds_with_rows}/{len(seeds)}; no seasonal investment rows: {len(no_investment_seeds)}/{len(seeds)}.",
        f"- Seasonal investment rows: {len(seasonal)}; exposed: {exposure['true']}; unexposed: {exposure['false']}; exposure unavailable: {sum(v for k, v in exposure.items() if k not in {'true', 'false'})}.",
        f"- Cannon shots observed: {shots}; powder paid observed: {powder:.2f}; siege/settlement pairs with a powder breach in observed rows: {len(breached)}.",
        f"- Shot/powder conservation (0.25 L per shot): {len(conservation_rows)} comparable seasonal rows; {conservation_mismatches} mismatches.",
        f"- Powder-breach gate: {len(breach_gate_rows)} seasonal breach rows; {breach_without_shot} breach rows without a paid shot.",
        f"- Terminal exposure: {terminal_observed} observed, {terminal_no_exposure} no-exposure, {len(terminal) - terminal_observed - terminal_no_exposure} unknown; final inventory rows: {len(terminal_inventory)} ({inventory_unavailable} incomplete); attacked settlements: {len(terminal_settlements)} ({terminal_censored} right-censored, {terminal_breaches} with terminal powder breach).",
        "- Attacked-settlement defenses (true): " + ", ".join(f"{field}={count}" for field, count in defense_counts.items()) + ".",
        f"- Unique terminal event keys (seed, season, settlement, event): {len(unique_outcomes)}; raw two-sided event rows: {len(event_rows)}; right-censored spells: {len(censored)}.",
        f"- Attrition-death values unavailable in seasonal rows: {crew_unavailable}/{len(seasonal)}.",
        "",
        "## Event rows",
        "",
        "| Event | Raw rows |",
        "|---|---:|",
    ]
    lines.extend(f"| {name} | {count} |" for name, count in sorted(outcomes.items()))
    lines.extend(crew_exposure_rows(seasonal))
    lines += [
        "",
        "## Censored seeds",
        "",
        "| Seed | Right-censored rows | Last censored season |",
        "|---:|---:|---:|",
    ]
    censor_by_seed: dict[int, list[dict[str, str]]] = defaultdict(list)
    for row in censored:
        censor_by_seed[int_value(row, "seed") or 0].append(row)
    for seed in seeds:
        values = censor_by_seed.get(seed, [])
        if values:
            last_season = max(int_value(row, "season") or 0 for row in values)
            lines.append(f"| {seed} | {len(values)} | {last_season} |")
    if not censored:
        lines.append("| — | 0 | — |")
    lines += [
        "",
        "## Terminal stocks and attacked settlements",
        "",
        "| Seed | Civ | Final cannon stock | Final powder stock |",
        "|---:|---:|---:|---:|",
    ]
    for row in terminal_inventory:
        lines.append(f"| {row.get('seed', '—')} | {row.get('civ', '—')} | {row.get('cannonStock', 'unavailable')} | {row.get('powderStock', 'unavailable')} |")
    if not terminal_inventory:
        lines.append("| — | — | — | — |")
    lines += [
        "",
        "| Seed | Settlement | Outcome | Censor reason | Integrity | Breach | Walls | Gatehouse | Castle | Garrisoned | Concentric |",
        "|---:|---:|---|---|---:|---|---|---|---|---|---|",
    ]
    for row in terminal_settlements:
        lines.append("| " + " | ".join(row.get(field, "unavailable") for field in (
            "seed", "settlement", "outcome", "censorReason", "integrity", "powderBreach",
            "stoneWalls", "gatehouse", "castle", "castleGarrisoned", "concentricCastle")) + " |")
    if not terminal_settlements:
        lines.append("| — | — | — | — | — | — | — | — | — | — | — |")
    lines += [
        "",
        "## Limits",
        "",
        "Terminal inventory is end-of-match stock, not a shot/powder ledger; shot/powder conservation compares seasonal paid powder with 0.25 L per observed shot and allows 0.01 L for two-decimal serialization. Terminal defense counts describe attacked settlements, not independent match outcomes. Right-censored rows mean an investment remained active at the run horizon, not that the siege eventually failed or succeeded. Event rows are two-sided and the raw row total therefore counts each side's event record; the unique-key count collapses those perspective duplicates where event fields permit.",
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--raw-dir", required=True, type=Path)
    cli.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    cli.add_argument("--output", type=Path, help="Write Markdown report here; default is stdout")
    args = cli.parse_args()
    try:
        report = analyze(args.raw_dir.resolve(), args.manifest.resolve())
        if args.output:
            args.output.write_text(report, encoding="utf-8")
        else:
            sys.stdout.write(report)
        return 0
    except (OSError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
