#!/bin/bash
# usage: q.sh QUEUE gate:args ... ; each item "id|harness args"
SCR=/private/tmp/claude-501/-Users-ejuchheim--claude-spares-spare-CuLIlA/5b70c709-f986-4985-b1f7-c35ad1083c59/scratchpad
export DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec
P=$SCR/frozen/numeric-e5.pack
duel() { # id A B
  python3 $SCR/run_gate.py "$1" $SCR/runs/$1 --seeds-file $SCR/runs/seeds-a1-40.tsv -- ai --pack $P --mode fief --duel $2 $3 --seeds 40 --first-seed 20261019 --minutes 64 --match-details
}
case "$1" in
  q1) duel a2-grower warlord grower; duel a2-builder warlord builder; duel a2-wanderer warlord wanderer ;;
  q2) duel a3-rusher warlord rusher; duel a4-chieftain chieftain warlord; duel b5-10-legend legend warlord ;;
  q3) python3 $SCR/run_gate.py a5-ffa $SCR/runs/a5-ffa --seeds-file $SCR/runs/seeds-a1-40.tsv -- ai --pack $P --mode fief --ffa warlord --seeds 120 --first-seed 20261019 --minutes 64 --match-details ;;
esac
