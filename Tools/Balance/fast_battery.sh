#!/bin/bash
# Phase 17 quick balance battery (level 2). Six cells of 20 DIFFERENT full-length matches each (Professional, Novice and Elite against themselves, Arena and Classic; seeds 5001-5020, one match per seed: two teams of the same level play each seed once),
# every cell is its own run limited to 1100 s (the absolute limit of any test run is 1200 s). Four cells at a time. Needs `dotnet build Tests/Core.Tests` first.
# usage: Tools/Balance/fast_battery.sh OUTDIR [FS27_EXP]      e.g. FS27_EXP="*.Locomotion.WalkRecoveryFactor=0.25"
# then: python3 -I Tools/Balance/compare_battery.py OUTDIR [--reference Docs/AI_BALANCE_PHASE17_data/fast_reference.json]
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$(mkdir -p "$1" && cd "$1" && pwd)"; EXP="$2"
LIMIT=1100
cell() { # MODE A B
  start=$(date +%s)
  ( cd "$REPO" && BAT_MODE=$1 BAT_A=$2 BAT_B=$3 BAT_FIRST=5001 BAT_SEEDS=20 BAT_SECONDS=full BAT_OUT="$OUT/$1_$2_vs_$3.csv" FS27_EXP="$EXP" \
    timeout $LIMIT dotnet test Tests/Core.Tests --no-build --filter "Name=Phase15Battery_WriteTheCsv" > "$OUT/$1_$2_vs_$3.log" 2>&1 )
  rc=$?
  echo $(( $(date +%s) - start )) > "$OUT/$1_$2_vs_$3.time"
  if [ $rc -eq 124 ]; then echo INCOMPLETE > "$OUT/$1_$2_vs_$3.status"; elif grep -q "Passed!" "$OUT/$1_$2_vs_$3.log"; then echo OK > "$OUT/$1_$2_vs_$3.status"; else echo FAILED > "$OUT/$1_$2_vs_$3.status"; fi
}
export -f cell; export REPO OUT EXP LIMIT
start=$(date +%s)
printf '%s\n' "Arena Professional Professional" "Classic Professional Professional" "Arena Novice Novice" "Classic Novice Novice" "Arena Elite Elite" "Classic Elite Elite" | xargs -P 4 -L 1 bash -c 'cell $0 $1 $2'
echo $(( $(date +%s) - start )) > "$OUT/TOTAL.time"; echo "$EXP" > "$OUT/EXP.txt"; (cd "$REPO" && git rev-parse HEAD > "$OUT/COMMIT.txt" 2>/dev/null)
cat "$OUT"/*.status | sort | uniq -c
