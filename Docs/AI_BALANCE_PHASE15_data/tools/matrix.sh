#!/bin/bash
# usage: matrix.sh OUTDIR FIRST SEEDS SECONDS [cellsfile]  -> runs every cell in both modes, 4 at a time
S=/tmp/claude-0/-home-user-junquifornikeichon/06a55549-4c51-54d5-a2f9-7aeebfc296b2/scratchpad
out=$1; first=$2; seeds=$3; secs=$4; cells=${5:-$S/cells.txt}
mkdir -p $out
while read a b; do for m in Arena Classic; do echo "$m $a $b"; done; done < $cells | \
  xargs -P 4 -L 1 bash -c 'S='$S'; $S/run15.sh $0 $1 $2 '$first' '$seeds' '$secs' '$out'/$0_$1_vs_$2.csv > '$out'/$0_$1_vs_$2.log 2>&1'
echo done > $out/DONE
