#!/usr/bin/env bash
# Builds the Discovery playtest pack: the repository content with the 50 E5 rows flipped from draft to numeric in a scratch copy
# (E6 is already numeric in the repository), so a human can research through Faith & Fief into the Discovery age.
# The repository content is never changed. Usage: playtest/make-discovery-pack.sh   (run from the repository root)
set -euo pipefail
export DOTNET_ROOT="${DOTNET_ROOT:-/opt/homebrew/opt/dotnet/libexec}"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
cp -R content "$scratch/content"
python3 - "$scratch/content/catalog" <<'PY'
import glob, re, sys
flipped = 0
for f in glob.glob(sys.argv[1] + '/*.yaml'):
    lines = open(f).read().split('\n')
    starts = [i for i, l in enumerate(lines) if re.match(r'  - id: ', l)] + [len(lines)]
    for a, b in zip(starts, starts[1:]):
        if any(re.match(r'    era: 5\s*$', l) for l in lines[a:b]):
            for i in range(a, b):
                if re.match(r'    status: draft\s*$', lines[i]):
                    lines[i] = '    status: numeric'; flipped += 1
    open(f, 'w').write('\n'.join(lines))
print(f'flipped {flipped} E5 rows to numeric')
PY
# Tuning aid: E6_BASE_COST=<n> sets the Discovery era's base cost in the scratch copy (never in the repository); PACK_OUT names the pack.
if [ -n "${E6_BASE_COST:-}" ]; then
  sed -i.bak -E "s/(name: Discovery, base_cost: )[0-9]+/\1${E6_BASE_COST}/" "$scratch/content/registries/eras.yaml"
  # the eight late E6 projects cost exactly twice the era base x their type multiplier (docs/MVP/38 §6 D9): keep them in step
  python3 - "$scratch/content/catalog" "$E6_BASE_COST" <<'PY'
import glob, re, sys
base = int(sys.argv[2]); mult = {'M': 1.0, 'I': 1.5}
for f in glob.glob(sys.argv[1] + '/*.yaml'):
    text = open(f).read()
    def fix(block):
        if 'docs/MVP/38 §6 D9' not in block: return block
        m = re.search(r'    type: (\w)', block)
        cost = int(round(2 * base * mult[m.group(1)]))
        block = re.sub(r'    cost: \d+', f'    cost: {cost}', block)
        return re.sub(r'EraBase \d+', f'EraBase {base}', block)
    parts = re.split(r'(?m)^(?=  - id: )', text)
    open(f, 'w').write(''.join(fix(b) for b in parts))
PY
fi
dotnet run --project src/CivSim.Cli -c Release -- validate --content "$scratch/content"
dotnet run --project src/CivSim.Cli -c Release -- build-pack --content "$scratch/content" --out "${PACK_OUT:-playtest/discovery-numeric-e5.pack}"
