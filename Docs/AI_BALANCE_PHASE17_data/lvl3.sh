#!/bin/bash
# usage: lvl3.sh OUTDIR FIRST SEEDS "label|EXP" ...   -> Professional vs Professional, both modes, SEEDS seeds (2 matches each), each cell a separate run limited to 1100 s, 4 at a time
S=/tmp/claude-0/-home-user-junquifornikeichon/06a55549-4c51-54d5-a2f9-7aeebfc296b2/scratchpad
out=$1; first=$2; seeds=$3; shift 3; mkdir -p $out; start=$(date +%s)
for cfg in "$@"; do for m in Arena Classic; do echo "$m|${cfg%%|*}|${cfg#*|}"; done; done | \
  xargs -P 4 -d '\n' -I{} bash -c 'IFS="|" read m label exp <<< "{}"; export FS27_EXP="$exp"; '$S'/q17.sh $m Professional Professional '$first' '$seeds' '$out'/${label}_${m}.csv'
echo $(( $(date +%s) - start )) > $out/TOTAL.time; cat $out/*.status | sort | uniq -c
