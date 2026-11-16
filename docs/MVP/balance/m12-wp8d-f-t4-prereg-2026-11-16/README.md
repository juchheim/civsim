# F-T4 held-out roster preregistration (2026-11-16)

Status: preregistered, **not run**. This is a fresh 80-world held-out paired cohort for the fail-closed 180-minute runner. It is diagnostic. It sets no acceptance tolerance and does not authorize catalog promotion.

## Frozen selection

- Seed pool: unsigned seeds `50,000,000..50,999,999`.
- Worlds: 80 distinct seeds sampled uniformly without replacement.
- RNG: Python `random.Random(20261116)`; call `sample(range(50_000_000, 51_000_000), 80)` and then, continuing the same RNG stream in the sample's draw order, assign each sampled seed one focus seat with `randrange(4)`. No world, geography, KEY-5 timing, outcome, or other screening was performed. Sort only for execution.
- Selected seeds and focus seats: [seeds-focus.tsv](seeds-focus.tsv).
- Exact runner roster: [manifest.json](manifest.json), 160 rows, exactly `agr11` and `control_no_agr11` once per seed, with the same focus seat in both arms.
- Manifest SHA-256 for this preregistered file, with pending pins below: `9c0d33206c2c44fc60c76514e02767a7f255905870d17cb90a49d2ecb907a825`.

The focus seat is selected uniformly from 0–3 per world. Both arms are paired on world seed and focus seat. The current runner fixes the horizon at 180 minutes and keeps the existing paired-match design unchanged.

## Final-freeze pins are pending

`manifest.json` currently contains zero-valued syntactic placeholders in all six required pin fields. These are not candidate identities and are intentionally not runnable against real artifacts. At final freeze, replace them with the exact `headCommit`, `sourceAggregateSha256`, `packSha256`, `harnessDllSha256`, `simDllSha256`, and `runnerSha256`; compute the SHA-256 of the completed manifest; pass that digest to the runner. Preserve the emitted launch manifest digest alongside the run output. Do not substitute the preregistration SHA above for the final pinned manifest SHA.

## Disjointness audit

The new pool is outside the known recorded cohort ranges, including all historical F-T4 blocks (`20,270,403..20,279,994`, `20,291,001..20,296,000`, `20,300,000..20,399,999`), F-T1/WP5 fit and validation (`20,400,000..20,699,999`, including earlier `20,610xx` probes), F-T5 (`20,500,000..20,599,999`), F-T6/T7 (`20,700,000..20,799,999`), WP-E5-12 (`33,000,000..36,000,000`), WP11 (`31,000,000..31,999,999`), B5-03 (`40,000,000..40,000,999`), and other manifests, seed lists and ledgers inspected in the canonical repository. A scan of 1,093 repository text files outside this preregistration found zero occurrences of any selected seed value and zero matching seed-field assignments. The generated 80 seeds are unique and do not overlap the historical F-T4 lists or any other seed assignment found in the repository scan.

## After final freeze

After code and pack are final and shared Release binaries are available, fill the six pins, compute the completed manifest digest, then use this single command to fill the pins and launch. Set `CIVSIM_REPO`, `CIVSIM_PACK`, `CIVSIM_HARNESS`, `CIVSIM_SIM`, and `CIVSIM_OUTPUT` to the frozen artifacts and a new absent output directory. This command computes the provenance tuple using the repository helper, hashes the runner, writes and hashes the pinned manifest, then invokes the guarded runner:

```bash
python3 -c 'import hashlib,json,os,sys; from pathlib import Path; repo,pack,harness,sim,out=map(Path,sys.argv[1:]); sys.path.insert(0,str(repo/"docs/MVP/balance/tools")); import cohort_provenance; m=repo/"docs/MVP/balance/m12-wp8d-f-t4-prereg-2026-11-16/manifest.json"; d=json.loads(m.read_text()); d["pins"].update(cohort_provenance.capture(repo,pack,harness,sim)); d["pins"]["runnerSha256"]=hashlib.sha256((repo/"docs/MVP/balance/tools/run_fief_land_long_horizon.py").read_bytes()).hexdigest(); m.write_text(json.dumps(d,indent=2)+"\n"); digest=hashlib.sha256(m.read_bytes()).hexdigest(); print("final manifest SHA-256:",digest,flush=True); from run_fief_land_long_horizon import main; raise SystemExit(main(["--repo",str(repo),"--pack",str(pack),"--harness",str(harness),"--sim",str(sim),"--manifest",str(m),"--manifest-sha256",digest,"--output",str(out)]))' "$CIVSIM_REPO" "$CIVSIM_PACK" "$CIVSIM_HARNESS" "$CIVSIM_SIM" "$CIVSIM_OUTPUT"
```

No matches are launched by this preregistration or by its fixture validation. After a completed cohort, analyze its +12-season focus-seat samples with `fief_land_population_weighted_fit.py`, using `seeds-focus.tsv` as the frozen selection, and report all fitted quantities as diagnostics until the user supplies tolerances.
