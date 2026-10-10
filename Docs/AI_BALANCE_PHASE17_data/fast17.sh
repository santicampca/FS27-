#!/bin/bash
# usage: fast17.sh LABEL [FS27_EXP]  -> level-2 battery: 6 cells x 20 full-length matches (seeds 5001-5010 both sides), 4 at a time, every run limited to 1100 s
S=/tmp/claude-0/-home-user-junquifornikeichon/06a55549-4c51-54d5-a2f9-7aeebfc296b2/scratchpad
out=$S/p17/$1; mkdir -p $out; export FS27_EXP="$2"; start=$(date +%s)
printf '%s\n' "Arena Professional Professional" "Classic Professional Professional" "Arena Novice Novice" "Classic Novice Novice" "Arena Elite Elite" "Classic Elite Elite" | xargs -P 4 -L 1 bash -c '$0 $1 $2 $3 5001 10 '$out'/$1_$2_vs_$3.csv' $S/q17.sh
echo $(( $(date +%s) - start )) > $out/TOTAL.time; echo "$2" > $out/EXP.txt
cat $out/*.status | sort | uniq -c
