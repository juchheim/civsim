# B5-13 golden-diff audit — candidate 2fce6aa

**Status: delta inventory complete; integration gate remains open** pending the full Release suite and strict content validation.

Compared with base commit `053bf4c91e39916dda03a8bfd085ea94ea94ad19`, candidate `2fce6aa29eab5d75e0cd6018c44d6ea6d5a65b78` changes exactly three files under `tests/golden/`:

| Golden | Base | Candidate | Recorded effect |
|---|---|---|---|
| `bronze-v1.txt` | 43,650 ticks; civ 1 ends by Rival Countdown at season 266; checksum `a3d8468a78c8e6b9` | 43,800 ticks; civ 3 ends by Rival Countdown at season 267; checksum `4d3716f8ab7e89e9` | Refreshed during E5 integration and later Legend-effect changes. The match result changes only after explicitly implemented simulation rules changed. |
| `politics-v1.txt` | final `5d3ebe260a0f55b5`; digest `36a42d944dde196c` | final `8dff024561450efe`; digest `8002412cf628ef95` | Refreshed with the Legend-effect and E5 technical-regression changes. |
| `research-v1.txt` | final `cb6fd7b2b6ebfd06`; digest `4f81921d3d755818` | final `8243a9f72aba5291`; digest `69da0f91a8b8eb66` | Refreshed with the Legend-effect/research changes. |

The history attributes the rebaselines to code changes rather than unexplained fixture edits: E5 integration (`fb2d5ce`), refresh after economy/research fitting (`3caf5eb`), Legend effects (`fdd8756`), remembered dead Legends (`c18c642`), and E5 technical regression checks (`bd0a7dc`). No other `tests/golden/` file differs from this base. The content-pack golden is unchanged in this comparison.

**Scope note:** the frozen candidate descends from the current recorded main base and contains 72 changed files; its history includes E6 Legend/gameplay work as well as E5. Acceptance evidence is for the exact integrated candidate, not an E5-only source diff. Any final integration must review that complete source scope.

This audit explains the candidate's golden deltas; it does not claim B5-13 passed. The full Release test suite and `dotnet run --project src/CivSim.Cli -c Release -- validate --strict` must still pass on the frozen candidate before that gate can close.
