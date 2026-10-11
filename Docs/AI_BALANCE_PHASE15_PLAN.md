# Phase 15 — Validation and balance of the AI difficulty: PLAN

Written BEFORE any measurement of Phase 15 and before any change. Branch `phase-15-ai-validation-balance`, from `63a723e` (2326 tests, 0 failed, 0 skipped, clean tree).
The questions are the 9 of the brief; the criteria below are fixed now so that the data cannot move them afterwards.

## 1. Baseline (F14, read, not assumed)

* `DecisionProfile` is built once per team from a `DifficultyDefinition` (`DefaultDifficulties`, the table of the infrastructure phase). No attribute, speed, stamina, RNG or score is touched (tests: `TheProfile_HoldsNoAttributeAndNoBonus…`, `TheDifficultyNumbers_LiveInOnePlace…`).
* What each level controls (values read from the code, `Docs/AI_BALANCE_PHASE15_data/profiles.txt`):

| Control | Novato | Amateur | Profesional | Experto | Élite | Mechanism |
|---|---|---|---|---|---|---|
| decision interval (× base 0.15 s) | 1.56 | 1.25 | 1.00 | 0.81 | 0.63 | `BasicDecisionDriver` per team |
| perception delay (s) | 0.25 | 0.20 | 0.16 | 0.13 | 0.105 | turnover noticed late |
| contest delay (s) | 0.175 | 0.14 | 0.112 | 0.091 | 0.073 | tackle/slide/challenge committed late |
| base mistake rate | 0.292 | 0.225 | 0.158 | 0.090 | 0.036 | second-best option, only inside a small score gap |
| decision margin (cannot tell apart under, pts) | 0.87 | 0.73 | 0.58 | 0.44 | 0.33 | scales the chance by closeness |
| pass / shot / skill-use / awareness quality | .35/.35/.35/.35 | .50 | .65 | .80 | .92/.90/.91/.92 | biases, habits, urgency |
| positioning quality / drift (m) | .30 / 3.0 | .50 / 2.0 | .65 / 1.3 | .80 / .8 | .92 / .4 | slow drift from the ideal place |
| anticipation (intercept horizon, s) | 1.11 | 1.32 | 1.53 | 1.72 | 1.85 | defence tuning copy |
| pressing quality (goal-side weight; min stamina to press) | 2.1; 15 | 2.7; 18 | 3.3; 22 | 3.8; 24 | 4.3; 27 | defence tuning copy |
| Classic challenge risk accepted | 1.75 | 1.51 | 1.26 | 1.04 | 0.87 | Classic only (Arena stays 1000: never held back) |
| stamina management (effort cap at stamina 50 / 25) | 1.00 / .68 | 1.00 / .57 | .95 / .51 | .84 / .47 | .78 / .45 | non-essential movements only |

* Known facts that bound the work: the F14 batteries were 120-second slices; a real match is 2 × 360 s (Arena) / 2 × 480 s (Classic). So F14 never exercised the clock/score urgency (window 90 s) nor the long-run stamina. F14 results: the process metrics were monotone, goals were not ranked, Amateur and Professional were not separable by goals.

## 2. What will be measured

* Battery tool `Phase15Battery_WriteTheCsv` (explicit): per match, both sides, 176 columns; reproducible from (mode, A, B, first seed, number of seeds, seconds). Each seed is played twice, A at home and A away, with the same two teams.
* Per side: goals, shots (available/chosen/actions/goal/stopped/out), clear chances (created), danger and final-third entries, passes, losses, recoveries, carrier decision mix, tackles/slides/challenges/contacts/interceptions/blocks/fouls/cards, distance from home, mistakes by kind and by situation (pressed, little space, tired), reaction delays, decisions (defensive/offensive/sprint-range), stamina by quarter.
* `DifficultyQualityScore` (diagnostic, five separate groups, never fed back): attack, defence, decision, movement. Game management is measured by a separate probe (score × clock), not by the match batteries.
* Statistics: mean, sd, median, range, W/D/L, a score rate (win = 1, draw = ½) and bootstrap 95 % confidence intervals (fixed seed) of every paired difference.
* **Not available in the Core, so not invented:** a "correct decision" label (there is no ground truth); shots on target (the Core does not tell blocked from saved: "stopped" = saved or blocked); occupation of space (no clean measure exists).

## 3. Protocol

* Seeds S1 = 1001–1075 (baseline), S2 = 2001–2075 (validation, never used for tuning), S3 = 1001–1020 re-run after any change (regression). F14 used 301–344 and 401+: disjoint.
* 150 matches per cell (75 seeds × 2 sides). Cells (both modes): Novato / Amateur / Profesional / Experto / Élite vs Profesional, and Novato vs Élite. Slices of 120 s for the full matrix; real-length matches for the cells where the clock and the legs matter.
* One change per experiment, recorded as BASELINE → CHANGE → RESULT → DECISION in `Docs/AI_BALANCE_PHASE15_EXPERIMENTS.md`. A change that does not survive S2 + S3 is reverted.

## 4. Not touched
Locomotion/stamina costs, BallSimulator, SkillResolver, TeamRoster, RolePositionAssigner, the F10/F11 architecture, `ShootMinScore` (stays 2.0 unless the data show a problem), graphics, Unity, Creator. No `if level`. Any stamina change would be a separate proposal.

## 5. Success criteria (fixed now)
1. **Decision quality ordered:** for `ErrorRate`, `PassCompletion`, `ClearChancesPer100Possessions` and `ChancesConcededPer100OpponentPossessions`, the five level means vs the same opponent are ordered (Spearman ≥ 0.9), in both modes, and each adjacent pair differs with a 95 % CI that excludes zero for `ErrorRate`.
2. **Amateur vs Profesional distinguishable:** the score-rate CI excludes 0.5, or at least three DQS ratios (one attack, one defence, one decision) have a difference CI that excludes zero. Otherwise it is documented as not separable and why.
3. **Experto harder than Profesional:** same test against Profesional, in the direction "better".
4. **Élite beatable:** against Profesional it loses at least 10 % of matches in each mode, and against Novato it does not win more than 95 %. **Novato beatable:** its score rate vs Profesional has a CI upper bound under 0.5, and it still wins or draws a meaningful share (> 15 %).
5. **Stable:** the signs and the ordering of 1–4 hold in S2 and S3.
6. **Identity:** Arena 0 fouls at every level; Classic fouls ≤ 20 % of contacts; teams keep different decision mixes at every level (checked against the unprofiled baseline).
7. **No cheating** (tests) and **determinism** (same arguments → same CSV).
8. Goals are reported, with CIs, but are NOT a criterion by themselves.
