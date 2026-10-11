#!/bin/bash
# Phase 22.1 diagnostic cells. Reads lines "MODE LEVEL FIRST" from stdin: one cell = 20 DIFFERENT full-length matches of LEVEL against LEVEL, seeds FIRST..FIRST+19 (the same pairing of teams as the quick battery).
# Each cell is its own run limited to 1100 s (the absolute limit of any test run is 1200 s). Four cells at a time. Needs `dotnet build Tests/Core.Tests` first. Do NOT rebuild while it runs.
# usage: printf 'Classic Professional 6001\n...' | Tools/Balance/diag_cells.sh OUTDIR [FS27_EXP]      e.g. FS27_EXP="C.FreeKick.Enabled=0"
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$(mkdir -p "$1" && cd "$1" && pwd)"; EXP="$2"
LIMIT=1100
cell() { # MODE LEVEL FIRST
  start=$(date +%s); name="$1_$2_$3"
  ( cd "$REPO" && BAT_MODE=$1 BAT_A=$2 BAT_B=$2 BAT_FIRST=$3 BAT_SEEDS=20 BAT_SECONDS=full BAT_OUT="$OUT/$name.csv" FS27_EXP="$EXP" \
    timeout $LIMIT dotnet test Tests/Core.Tests --no-build --filter "Name=Phase15Battery_WriteTheCsv" > "$OUT/$name.log" 2>&1 )
  rc=$?
  echo $(( $(date +%s) - start )) > "$OUT/$name.time"
  if [ $rc -eq 124 ]; then echo INCOMPLETE > "$OUT/$name.status"; elif grep -q "Passed!" "$OUT/$name.log"; then echo OK > "$OUT/$name.status"; else echo FAILED > "$OUT/$name.status"; fi
}
export -f cell; export REPO OUT EXP LIMIT
start=$(date +%s)
xargs -P 4 -L 1 bash -c 'cell $0 $1 $2'
echo $(( $(date +%s) - start )) > "$OUT/TOTAL.time"; echo "$EXP" > "$OUT/EXP.txt"; (cd "$REPO" && git rev-parse HEAD > "$OUT/COMMIT.txt" 2>/dev/null)
cat "$OUT"/*.status | sort | uniq -c
