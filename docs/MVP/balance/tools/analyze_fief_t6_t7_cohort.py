#!/usr/bin/env python3
"""Analyze raw output from run_fief_t6_t7_cohort.py using seed-level bootstrap resampling."""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import sys
from collections import Counter, defaultdict
from pathlib import Path

EXPECTED_MANIFEST_SHA256 = "a92dd049c1c56836dd2bffad3106985694be2eb755f15b4665c0937ae318f038"
TOOLS = Path(__file__).resolve().parent
DEFAULT_MANIFEST = TOOLS.parent / "m12-e5-9-f-t6-t7-natural-seeds-2026-11-14.tsv"
DEFAULT_TEMPLATE = TOOLS.parent / "m12-e5-9-f-t6-t7-natural-analysis-template-2026-11-15.md"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_seeds(path: Path) -> list[int]:
    return [int(value) for line in path.read_text(encoding="utf-8").splitlines()
            if (value := line.strip()) and not value.startswith("#") and value.lower() != "seed"]


def fields(line: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for token in line.split():
        if "=" in token:
            key, value = token.split("=", 1)
            result[key] = value
    return result


def percentile(sorted_values: list[float], probability: float) -> float:
    if not sorted_values:
        raise ValueError("percentile requires at least one value")
    position = (len(sorted_values) - 1) * probability
    lower = int(position)
    upper = min(lower + 1, len(sorted_values) - 1)
    fraction = position - lower
    return sorted_values[lower] * (1.0 - fraction) + sorted_values[upper] * fraction


def validate_cohort_protocol(metadata: dict[str, object], seed_count: int) -> bool:
    if seed_count < 20:
        raise ValueError(f"F-T6/F-T7 cohort requires at least 20 frozen seeds, found {seed_count}")
    required_flags = {
        "--fief-health-trace", "--fief-institution-trace", "--fief-wp7-paired",
        "--fief-sea10-trace", "--fief-territory-trace", "--fief-siege-trace",
        "--fief-siege-strategist-trace", "--fief-power-ledger",
    }
    combined = metadata.get("combinedNaturalTraceCohort") is True
    if combined:
        if not required_flags.issubset(set(metadata.get("traceFlags", []))):
            raise ValueError("combined natural-trace cohort is missing a required trace flag")
    elif seed_count != 20:
        raise ValueError(f"F-T6/F-T7 cohort requires exactly 20 seeds unless using the pinned combined protocol; found {seed_count}")
    return combined


def seeded_bootstrap_ratio(seed_totals: dict[int, tuple[float, float]], replicates: int, bootstrap_seed: int) -> tuple[float, float] | None:
    eligible = [(epi, total) for epi, total in seed_totals.values() if total > 0]
    if not eligible:
        return None
    rng = random.Random(bootstrap_seed)
    sampled_shares: list[float] = []
    for _ in range(replicates):
        sample = [eligible[rng.randrange(len(eligible))] for _ in eligible]
        total_epi = sum(epi for epi, _ in sample)
        total_hazard = sum(total for _, total in sample)
        sampled_shares.append(total_epi / total_hazard if total_hazard else 0.0)
    sampled_shares.sort()
    return percentile(sampled_shares, 0.025), percentile(sampled_shares, 0.975)


def analyze(raw_dir: Path, manifest_path: Path, template_path: Path, output_path: Path,
            replicates: int = 20_000, bootstrap_seed: int = 20261115) -> dict[str, object]:
    metadata_path = raw_dir / "run_metadata.json"
    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    if metadata.get("status") != "complete":
        raise ValueError(f"run metadata status is {metadata.get('status')!r}, expected 'complete'")
    seeds = load_seeds(manifest_path)
    manifest_hash = sha256(manifest_path)
    if manifest_path.resolve() == DEFAULT_MANIFEST.resolve() and manifest_hash != EXPECTED_MANIFEST_SHA256:
        raise ValueError("frozen seed manifest hash does not match the recorded cohort")
    if metadata.get("manifestSha256") != manifest_hash or metadata.get("seeds") != seeds:
        raise ValueError("raw run metadata does not match the frozen manifest")
    combined_cohort = validate_cohort_protocol(metadata, len(seeds))
    if metadata.get("combinedNaturalTraceCohort"):
        freeze_path = raw_dir / "run-freeze.json"
        if not freeze_path.is_file():
            raise ValueError("combined natural-trace cohort is missing run-freeze.json")
        freeze = json.loads(freeze_path.read_text(encoding="utf-8"))
        if (freeze.get("cohort_role") != "held_out" or freeze.get("seeds") != seeds or
                freeze.get("seed_file_sha256") != manifest_hash or freeze.get("traces") != metadata.get("traceFlags")):
            raise ValueError("combined run freeze does not match the held-out manifest or trace metadata")
        calibration_path = Path(freeze.get("calibration_seed_file", ""))
        if (not calibration_path.is_file() or sha256(calibration_path) != freeze.get("calibration_seed_file_sha256") or
                set(seeds) & set(freeze.get("calibration_seeds", []))):
            raise ValueError("combined held-out cohort has a missing, changed, or overlapping calibration manifest")
        expected_runner_sha = freeze.get("expected_runner_sha256")
        runner_path = Path(freeze.get("runner", ""))
        if not runner_path.is_file() or sha256(runner_path) != expected_runner_sha or freeze.get("runner_sha256") != expected_runner_sha:
            raise ValueError("combined runner path or SHA-256 does not match the frozen pin")
        test_path = Path(freeze.get("test_assembly", ""))
        if not test_path.is_file() or sha256(test_path) != freeze.get("test_assembly_sha256") or metadata.get("testAssemblySha256") != freeze.get("test_assembly_sha256"):
            raise ValueError("combined test-assembly path or SHA-256 does not match the frozen pin")
        for path_key, hash_key in (("packPath", "packSha256"), ("harnessDllPath", "harnessDllSha256"),
                                   ("simDllPath", "simDllSha256")):
            artifact = Path(metadata.get(path_key, ""))
            if not artifact.is_file() or sha256(artifact) != metadata.get(hash_key):
                raise ValueError(f"combined {path_key} does not match its frozen SHA-256")
    run_rows = metadata.get("runs", [])
    by_seed_runs = {int(row["seed"]): row for row in run_rows}
    if len(by_seed_runs) != len(run_rows):
        raise ValueError("raw run metadata contains duplicate seed records")
    if set(by_seed_runs) != set(seeds):
        raise ValueError("raw run metadata does not contain exactly the frozen seed set")

    health_by_seed: dict[int, list[dict[str, str]]] = {seed: [] for seed in seeds}
    institution_rows: list[dict[str, str]] = []
    institution_summary_rows: list[dict[str, str]] = []
    for seed in seeds:
        run = by_seed_runs[seed]
        if run.get("exitCode") != 0:
            raise ValueError(f"run metadata reports nonzero exit code for seed {seed}")
        expected_raw_name = f"seed-{seed}.txt"
        if run.get("rawFile") != expected_raw_name:
            raise ValueError(f"unexpected raw filename for seed {seed}: {run.get('rawFile')!r}")
        raw_path = raw_dir / expected_raw_name
        if sha256(raw_path) != run.get("rawSha256"):
            raise ValueError(f"raw output hash mismatch: {raw_path}")
        for line in raw_path.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("fief-health-trace "):
                health_by_seed[seed].append(fields(line))
            elif line.startswith("fief-institution-trace "):
                row = fields(line)
                row["_seed"] = str(seed)
                institution_rows.append(row)
            elif line.startswith("fief-institution-summary "):
                row = fields(line)
                row["_seed"] = str(seed)
                institution_summary_rows.append(row)
        if not health_by_seed[seed]:
            raise ValueError(f"no F-T7 health rows found for seed {seed}")
    if not institution_summary_rows:
        raise ValueError("no F-T6 institution summary rows were found")
    for seed in seeds:
        if not any(row["_seed"] == str(seed) for row in institution_rows):
            raise ValueError(f"no F-T6 institution rows found for seed {seed}")
        if not any(row["_seed"] == str(seed) for row in institution_summary_rows):
            raise ValueError(f"no F-T6 institution summary rows found for seed {seed}")

    seed_hazards: dict[int, tuple[float, float]] = {}
    health_seed_detail: list[str] = []
    total_epi_hazard = total_expected_hazard = 0.0
    total_adult_equiv = total_epi_deaths = total_deaths = 0.0
    total_pox_deaths = total_plague_deaths = 0
    quarantined_civ_seasons = import_attempts = import_blocked = import_successes = 0
    censored_no_key5: list[int] = []
    censored_no_post_season: list[int] = []
    post_key5_row_count = 0
    for seed in seeds:
        rows = health_by_seed[seed]
        post_rows = [row for row in rows if int(row.get("seasonOffsetFromOwnKey5", "-1")) >= 0]
        if not any(int(row.get("ownKey5Season", "-1")) >= 0 for row in rows):
            censored_no_key5.append(seed)
        elif not post_rows:
            censored_no_post_season.append(seed)
        epi_hazard = sum(float(row["expectedEpidemic"]) for row in post_rows)
        expected_hazard = sum(float(row["expectedTotal"]) for row in post_rows)
        adult_equiv = sum(float(row["adultEquivalentPersonSeasons"]) for row in post_rows)
        epi_deaths = sum(int(row["observedEpidemicDeaths"]) for row in post_rows)
        deaths = sum(int(row["observedTotalDeaths"]) for row in post_rows)
        pox_deaths = sum(int(row.get("observedPoxDeaths", "0")) for row in post_rows)
        plague_deaths = sum(int(row.get("observedPlagueDeaths", "0")) for row in post_rows)
        attempts = sum(int(row.get("importedOutbreakAttempts", "0")) for row in post_rows)
        blocked = sum(int(row.get("importedOutbreakBlocked", "0")) for row in post_rows)
        successes = sum(int(row.get("importedOutbreakSuccesses", "0")) for row in post_rows)
        if attempts != blocked + successes:
            raise ValueError(f"imported-outbreak counters do not reconcile for seed {seed}")
        if pox_deaths + plague_deaths > epi_deaths:
            raise ValueError(f"class-attributed deaths exceed epidemic deaths for seed {seed}")
        quarantined_civ_seasons += sum(row.get("quarantine", "false").lower() == "true" for row in post_rows)
        total_pox_deaths += pox_deaths
        total_plague_deaths += plague_deaths
        import_attempts += attempts
        import_blocked += blocked
        import_successes += successes
        seed_hazards[seed] = (epi_hazard, expected_hazard)
        total_epi_hazard += epi_hazard
        total_expected_hazard += expected_hazard
        total_adult_equiv += adult_equiv
        total_epi_deaths += epi_deaths
        total_deaths += deaths
        post_key5_row_count += len(post_rows)
        seed_share = f"{epi_hazard / expected_hazard:.6%}" if expected_hazard > 0 else "n/a"
        health_seed_detail.append(
            f"| {seed} | {len(rows)} | {len(post_rows)} | {epi_hazard:.6f} | {expected_hazard:.6f} | {seed_share} | "
            f"{epi_deaths} | {deaths} | {adult_equiv:.2f} |"
        )

    pooled_share = total_epi_hazard / total_expected_hazard if total_expected_hazard > 0 else None
    interval = seeded_bootstrap_ratio(seed_hazards, replicates, bootstrap_seed)
    epi_death_rate = total_epi_deaths / total_adult_equiv if total_adult_equiv > 0 else None
    total_death_rate = total_deaths / total_adult_equiv if total_adult_equiv > 0 else None

    states_by_institution: Counter[tuple[str, str]] = Counter()
    institution_row_count = 0
    staffed_working = staffed_dormant = staffed_unresolved = queued = 0
    unresolved_reasons: Counter[str] = Counter()
    staffed_by_seed_civ_season: dict[tuple[int, int, int], Counter[str]] = defaultdict(Counter)
    row_keys: set[tuple[int, int, int, int, str]] = set()
    represented_types: set[str] = set()
    for row in institution_rows:
        institution = row.get("institution", "unknown")
        state = row.get("state", "unknown")
        count = int(row.get("builtCount", "0"))
        reason = row.get("reason", "").strip()
        if not reason:
            raise ValueError(f"F-T6 row has no reason: seed={row.get('_seed')} institution={institution}")
        if institution not in {"monastery", "university", "hospital", "gothic_cathedral", "clock_tower", "watermill", "windmill"}:
            raise ValueError(f"unknown F-T6 institution type {institution!r}")
        if state not in {"working", "dormant", "queued", "unresolved"}:
            raise ValueError(f"unknown F-T6 institution state {state!r}")
        if state == "queued" and count != 0:
            raise ValueError(f"queued F-T6 row has nonzero builtCount: {row}")
        if state != "queued" and count < 1:
            raise ValueError(f"built F-T6 row has no built institution: {row}")
        row_key = (int(row["_seed"]), int(row["civ"]), int(row["season"]), int(row["settlement"]), institution)
        if row_key in row_keys:
            raise ValueError(f"duplicate F-T6 institution row: {row_key}")
        row_keys.add(row_key)
        institution_row_count += 1
        represented_types.add("mill" if institution in {"watermill", "windmill"} else institution)
        states_by_institution[(institution, state)] += count if state != "queued" else 1
        if state == "queued":
            queued += 1
        elif institution in {"monastery", "university", "hospital"}:
            key = (int(row["_seed"]), int(row["civ"]), int(row["season"]))
            if state == "working": staffed_working += count
            elif state == "dormant": staffed_dormant += count
            elif state == "unresolved":
                staffed_unresolved += count
                unresolved_reasons[row.get("reason", "unspecified")] += count
            staffed_by_seed_civ_season[key][state] += count
    staffed_denominator = staffed_working + staffed_dormant
    dormant_rate = staffed_dormant / staffed_denominator if staffed_denominator > 0 else None
    summary_mismatches = 0
    summary_keys: set[tuple[int, int, int]] = set()
    for row in institution_summary_rows:
        key = (int(row["_seed"]), int(row["civ"]), int(row["season"]))
        if key in summary_keys:
            raise ValueError(f"duplicate F-T6 summary row: {key}")
        summary_keys.add(key)
        counts = staffed_by_seed_civ_season[key]
        local_working = counts["working"]
        local_dormant = counts["dormant"]
        local_unresolved = counts["unresolved"]
        if (local_working != int(row["workingStaffed"]) or local_dormant != int(row["dormantStaffed"])
                or local_unresolved != int(row["unresolvedStaffing"])):
            summary_mismatches += 1
    # Every summary row must reconcile, including zero-institution civ-seasons.
    for row in institution_summary_rows:
        key = (int(row["_seed"]), int(row["civ"]), int(row["season"]))
        if key not in staffed_by_seed_civ_season and any(
                int(row[field]) != 0 for field in ("workingStaffed", "dormantStaffed", "unresolvedStaffing")):
            summary_mismatches += 1
    expected_summary_keys = {
        (seed, int(row["civ"]), int(row["season"]))
        for seed, seed_rows in health_by_seed.items() for row in seed_rows
    }
    summary_mismatches += len((expected_summary_keys | set(staffed_by_seed_civ_season)) ^ summary_keys)
    expected_types = {"monastery", "university", "hospital", "gothic_cathedral", "clock_tower", "mill"}
    missing_types = sorted(expected_types - represented_types)
    if missing_types:
        raise ValueError(f"F-T6 cohort did not represent all six institution types; missing: {', '.join(missing_types)}")
    institution_coverage_status = "PASS" if staffed_unresolved == 0 and summary_mismatches == 0 else "FAIL"

    def show_fraction(value: float | None) -> str:
        return "n/a" if value is None else f"{value:.6f}"

    def show_percent(value: float | None) -> str:
        return "n/a" if value is None else f"{value:.2%}"

    institution_detail = [f"| {institution} | {state} | {count} |" for (institution, state), count in sorted(states_by_institution.items())]
    unresolved_detail = [f"| `{reason}` | {count} |" for reason, count in sorted(unresolved_reasons.items())]
    ci = "n/a" if interval is None else f"[{interval[0]:.6%}, {interval[1]:.6%}]"
    replacements = {
        "{{MANIFEST_SHA256}}": str(metadata["manifestSha256"]),
        "{{PACK_SHA256}}": str(metadata["packSha256"]),
        "{{HARNESS_SHA256}}": str(metadata["harnessDllSha256"]),
        "{{SEED_COUNT}}": str(len(seeds)),
        "{{POST_KEY5_SEEDS}}": str(sum(1 for values in seed_hazards.values() if values[1] > 0)),
        "{{CENSORED_NO_KEY5}}": ", ".join(map(str, censored_no_key5)) or "none",
        "{{CENSORED_NO_POST_SEASON}}": ", ".join(map(str, censored_no_post_season)) or "none",
        "{{POST_KEY5_ROWS}}": str(post_key5_row_count),
        "{{EXPECTED_SHARE}}": show_percent(pooled_share),
        "{{EXPECTED_SHARE_CI}}": ci,
        "{{BOOTSTRAP_REPLICATES}}": str(replicates),
        "{{BOOTSTRAP_SEED}}": str(bootstrap_seed),
        "{{BOOTSTRAP_ELIGIBLE_SEEDS}}": str(sum(1 for values in seed_hazards.values() if values[1] > 0)),
        "{{EPIDEMIC_DEATH_RATE}}": show_fraction(epi_death_rate),
        "{{TOTAL_DEATH_RATE}}": show_fraction(total_death_rate),
        "{{TOTAL_ADULT_EQ}}": f"{total_adult_equiv:.2f}",
        "{{TOTAL_EPIDEMIC_DEATHS}}": str(int(total_epi_deaths)),
        "{{TOTAL_DEATHS}}": str(int(total_deaths)),
        "{{POX_DEATHS}}": str(total_pox_deaths),
        "{{PLAGUE_DEATHS}}": str(total_plague_deaths),
        "{{UNCLASSIFIED_EPIDEMIC_DEATHS}}": str(int(total_epi_deaths) - total_pox_deaths - total_plague_deaths),
        "{{QUARANTINED_CIV_SEASONS}}": str(quarantined_civ_seasons),
        "{{IMPORT_ATTEMPTS}}": str(import_attempts),
        "{{IMPORT_BLOCKED}}": str(import_blocked),
        "{{IMPORT_SUCCESSES}}": str(import_successes),
        "{{STAFFED_WORKING}}": str(staffed_working),
        "{{STAFFED_DORMANT}}": str(staffed_dormant),
        "{{STAFFED_UNRESOLVED}}": str(staffed_unresolved),
        "{{STAFFED_DENOMINATOR}}": str(staffed_denominator),
        "{{DORMANT_RATE}}": show_percent(dormant_rate),
        "{{QUEUED_ROWS}}": str(queued),
        "{{INSTITUTION_ROWS}}": str(institution_row_count),
        "{{INSTITUTION_TYPES}}": ", ".join(sorted(represented_types)),
        "{{SUMMARY_ROW_COUNT}}": str(len(institution_summary_rows)),
        "{{INSTITUTION_COVERAGE_STATUS}}": institution_coverage_status,
        "{{SUMMARY_MISMATCHES}}": str(summary_mismatches),
        "{{INSTITUTION_DETAIL}}": "\n".join(institution_detail) or "| (none) | (none) | 0 |",
        "{{UNRESOLVED_DETAIL}}": "\n".join(unresolved_detail) or "| (none) | 0 |",
        "{{HEALTH_SEED_DETAIL}}": "\n".join(health_seed_detail),
    }
    report = template_path.read_text(encoding="utf-8")
    for placeholder, value in replacements.items():
        report = report.replace(placeholder, value)
    unresolved_tokens = [token for token in report.split() if token.startswith("{{") and token.endswith("}}")]
    if unresolved_tokens:
        raise ValueError(f"unfilled report template placeholders: {unresolved_tokens}")
    output_path.write_text(report, encoding="utf-8")
    return {"seedCount": len(seeds), "postKey5Rows": post_key5_row_count, "expectedShare": pooled_share,
            "expectedShareCI": interval, "dormantRate": dormant_rate, "summaryMismatches": summary_mismatches,
            "institutionTypes": ", ".join(sorted(represented_types)), "institutionRows": institution_row_count,
            "unresolvedStaffing": staffed_unresolved, "queuedRows": queued,
            "institutionCoverageStatus": institution_coverage_status}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--raw-dir", required=True, type=Path)
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--template", type=Path, default=DEFAULT_TEMPLATE)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--bootstrap-replicates", type=int, default=20_000)
    parser.add_argument("--bootstrap-seed", type=int, default=20261115)
    args = parser.parse_args(argv)
    if args.bootstrap_replicates < 1:
        parser.error("--bootstrap-replicates must be positive")
    try:
        result = analyze(args.raw_dir, args.manifest, args.template, args.output,
                         args.bootstrap_replicates, args.bootstrap_seed)
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
