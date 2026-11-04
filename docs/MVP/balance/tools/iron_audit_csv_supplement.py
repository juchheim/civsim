"""Supplementary cuts of the iron-audit --csv output (stdlib only). Usage: python3 -I iron_audit_csv_supplement.py audit.csv  (audit.csv from: civsim-harness iron-audit --csv audit.csv)"""
import csv, sys
rows = list(csv.DictReader(open(sys.argv[1])))
by = {(int(r['seed']), int(r['civ'])): r for r in rows}
n = len(rows)
step = lambda r: int(r['step'])
non = [r for r in rows if step(r) >= 4]
print('starts', n, 'non-local (steps 4-6)', len(non))
# partner capability variants for the non-local starts: mate or either other-continent start, by max step allowed
for maxstep in (2, 3):
    ok = 0
    for r in non:
        s, c = int(r['seed']), int(r['civ'])
        partners = [by[(s, p)] for p in range(4) if p != c]
        if any(step(p) <= maxstep for p in partners):
            ok += 1
    print(f'  non-local starts with a partner at step <= {maxstep}: {ok}; without: {len(non) - ok}')
mate_steps = {}
for r in non:
    m = by[(int(r['seed']), int(r['civ']) ^ 1)]
    mate_steps[step(m)] = mate_steps.get(step(m), 0) + 1
print('  mate step among non-local starts:', dict(sorted(mate_steps.items())))
# pairs where both members are outside steps 1-3
pairs = {(int(r['seed']), int(r['civ']) // 2) for r in non}
both = 0
for (s, p) in pairs:
    if step(by[(s, 2 * p)]) >= 4 and step(by[(s, 2 * p + 1)]) >= 4:
        both += 1
print('  pairs (seed, pair) with a non-local start:', len(pairs), '; with both members non-local:', both)
# eligible-by-hex but unreachable on foot
un = [r for r in rows if step(r) <= 2 and r['siteWalkTenths'] == str(2**31 - 1)]
print('steps 1-2 with no walking route to any eligible site:', len(un), 'by step', {k: sum(1 for r in un if step(r) == k) for k in (1, 2)})
long_ = [r for r in rows if step(r) <= 2 and r['siteWalkTenths'] != str(2**31 - 1) and int(r['siteWalkTenths']) > 400]
print('steps 1-2 with a walking route > 40 tiles:', len(long_))
# the same cuts, distinct starts only
d = [r for r in rows if int(r['civ']) < 2]
print('distinct starts', len(d), 'steps 1-2', sum(1 for r in d if step(r) <= 2), 'steps 1-3', sum(1 for r in d if step(r) <= 3))
