# M13 WP-E6-6 slice 6: TRD-10 joint-stock and TRD-11 insurance (2026-11-21)

**Status:** built and tested on `codex/e6-discovery`, uncommitted (on top of the uncommitted [WP-E6-7 slice 1](m13-wp7-mind-slice1.md)). Built after **decision D12** was settled on 2026-11-21 ("Accept D12": **pooled goods only, no debt and no money**). Discovery chapter only; the new structure kind and role are written only in Discovery snapshots, nothing else is stored, and Bronze, Iron and Fief bytes are unchanged. All numbers are provisional.

## What the slice does

| Part | How |
|---|---|
| **The rule (D12)** | Neither project creates money or records a debt. Both are about *who stands behind a voyage*, so both need **more than one purse**: a civilization with fewer than two harbor towns has nobody to pool with or to spread a loss across. |
| **TRD-10 joint-stock** (KEY-7 prerequisite) | With TRD-10 and **at least two harbor towns**, sea ventures sail **x1.5 faster** (the row's venture_speed; the sea leg of a route is two thirds as long), composed with WAY-09 and SEA-11 before rounding. Walked routes are untouched. The "pooled ledger" is the pair of purses; nothing is spent or stored. |
| **TRD-11 exchange house** | New structure `exchange_house` (TRD-11; 30 work-seasons, 14 block, 6 wood): a town with a harbor and 60 people raises one; **one to a civilization**. It is open only while **two brokers** (new role 14) are at their desks ("an unstaffed exchange has no effect"). |
| **TRD-11 insurance** | With a staffed exchange house and at least two harbor towns, **voyage loss is halved** (the row's voyage_loss x0.5), after WAY-08, WAY-10 and INQ-07. A loss is spread, not paid out of thin air: the risk the realm carries falls by half. |

## Design choices worth knowing

1. **Two harbor towns is the whole pool.** It makes the "many purses" literal (it takes two towns' stores to charter a ship) without a goods ledger the sim has no use for. A realm that cannot fill two harbors gets neither effect, so the AI has a reason to build a second port.
2. **Insurance halves the roll, not the wreck's cost.** A voyage that is lost still loses its cargo (as today); there are simply half as many losses. Paying a loss out of the pool would need a loss record the sim does not keep.
3. **Staffing is the existing role pass.** Brokers are drawn like engineers and operatives (keeping four other adults at home), so an exchange costs a harbor town two workers.
4. **TRD-08 (E5 credit) is untouched.** D12 settles the E6 projects; Fief's TRD-08 and TRD-09 stay as they are.

## Tests (4 in `DiscoveryVentureTests`, all pass)

Joint-stock needs TRD-10 and two harbor towns, speeds sea routes and never slows one, leaves walked routes and other civilizations alone; one harbor pools nothing and Fief has none; the exchange house needs TRD-11, a harbor and size 60, one to a town and to a civilization; a staffed house halves the loss and an empty one does nothing.

## What this unblocks

TRD-10 was a KEY-7 prerequisite and FIB-12 requires it, so the KEY-7 path (and the Factory behind MEC-10) is no longer closed by the credit decision. SOC-12 still requires TRD-08, which is an E5 row already in play.

## Not done

1. A natural-play measurement of how often the AI builds a second harbor and an exchange house (WP-E6-12).
2. FIB-12 shuttle and jenny (now unblocked), cottage cloth as a trade good, FOD-10 sugar and the cod and sugar goods.
