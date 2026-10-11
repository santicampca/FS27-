# Phase 16 — Goalkeeper AI (report)

Branch `phase-16-goalkeeper-ai` (from F15 `784757b`). No push, no PR, no merge. Unity not run. Graphics and Creator untouched.
**The AI is NOT finished.** This is a first, measurable keeper AI, not a final one.

## What was built
- `Core/Goalkeeper/GoalkeeperPositioning` (stand on the goal→ball arc, depth by danger, near-post lean, blocker-aware lateral shift, slow positional drift error), `GoalkeeperReaction` (stateless reaction gating from `WorldBall.SecondsSinceTouch`, shot reading, deterministic misreads), `GoalkeeperExit` (come out / stay, bad-exit mistakes), `ShotQualityEvaluator` (distance, angle, pressure, keeper position, foot, speed, aim → value → logistic xG; stats only, never converts into goals).
- `ArenaGoalkeeperDefaults` (permissive: ReactionScale 1.1, ReadErrorScale 1.25, PositionErrorScale 0.85) and `ClassicGoalkeeperDefaults` (0.95 / 0.8 / 0.6). All numbers live in `GoalkeeperAiDefaults.cs`.
- Difficulty only through `DecisionProfile.Goalkeeper` (ReactionSeconds, Positioning, Anticipation, DecisionMaking, SaveTiming, ShotReading, ReboundResponse) combined with the keeper's own capabilities in `GoalkeeperSkillModel`. No `if level`, no attribute multipliers, no clairvoyance/teleport/size cheats. Mistakes use `DecisionDice` (never Core RNG). No LINQ/allocations in decision paths.
- Observer: saves (counted once per shot), shot log with xG, keeper misreads and bad exits. Core still resolves saves by touch; the AI only decides where/when the keeper moves (`GoalkeeperRush` still has no mechanical effect).
- Tests: `AiGoalkeeperTests` (21). Before 2337 (F15), after 2358 at the step-1 commit.

## Simulations (real length: Arena 720 s, Classic 960 s; 100 matches per cell, seeds 3001–3050 x both sides; validation seeds 4001–4050)
Keeper of the listed level faces a Professional attack; "score" is the level's own team vs Professional.

| Mode | Level | Shots faced | Goals | Conv % | Save % | Score (W/D/L, rate) | Goals own/against per match |
|---|---|---|---|---|---|---|---|
| Arena | Novice | 2990 | 675 | 22.6 | 76.7 | 11/6/83, 0.140 | 3.68 / 6.75 |
| Arena | Amateur | 3013 | 515 | 17.1 | 82.0 | 32/9/59, 0.365 | 3.87 / 5.15 |
| Arena | Professional | 3014 | 373 | 12.4 | 86.4 | 46/8/46, 0.500 | 3.73 / 3.73 |
| Arena | Expert | 3078 | 294 | 9.6 | 87.9 | 57/17/26, 0.655 | 3.98 / 2.94 |
| Arena | Elite | 3079 | 248 | 8.1 | 91.3 | 68/9/23, 0.725 | 4.19 / 2.48 |
| Classic | Novice | 4280 | 496 | 11.6 | 85.6 | 21/7/72, 0.245 | 2.84 / 4.96 |
| Classic | Amateur | 3963 | 411 | 10.4 | 87.6 | 30/16/54, 0.380 | 3.24 / 4.11 |
| Classic | Professional | 3983 | 332 | 8.3 | 90.1 | 46/8/46, 0.500 | 3.32 / 3.32 |
| Classic | Expert | 4044 | 253 | 6.3 | 91.5 | 50/16/34, 0.580 | 3.19 / 2.53 |
| Classic | Elite | 3746 | 197 | 5.3 | 92.8 | 67/18/15, 0.760 | 3.46 / 1.97 |

Keeper quality is monotone Novice < Amateur < Professional < Expert < Elite in both modes. Elite loses 23% (Arena) / 15% (Classic) of matches against Professional; Novice still wins 11% / 21%. Professional vs Professional is 0.500 by construction (validation seeds: 46/8/46 Arena, 43/14/43 Classic).

Goals per match (Professional vs Professional, both teams): old keeper (F15 basefull, 150 matches) Arena 6.47, Classic 7.03; new keeper Arena 7.46 (f_new) / 8.24 (f_val), Classic 6.64 / 6.18. So the new keeper makes Arena clearly more goal-heavy than Classic (intended direction) and Classic slightly lower than before. Note the Arena rise (+15–27 %) is larger than "modest"; see problems.

## xG vs real goals
- Calibration constants (`ShotQualityDefaults`: intercept −5.576, slope 5.685, Classic offset −0.695) were kept. On fresh seeds (f_val, Professional vs Professional) xG over logged shots is 576.0 vs 620 goals in Arena (−7 %) and 362.6 vs 394 in Classic (−8 %): usable, slightly low. Refitting on the full battery (`Docs/AI_GOALKEEPER_PHASE16_data/xg_recalibration.txt`) gave Arena almost the same constants (−5.53 / 5.76) and a Classic refit that over-predicts on validation (451.7 vs 394), so no change was made.
- 20–36 % of goals are NOT preceded by a logged, rated shot (f_new: Arena 4050 goals vs 3230 from logged shots; Classic 3294 vs 2259; f_val 824 vs 620 and 618 vs 394). Goals from rebounds, deflections and other non-shot origins are invisible to the shot log; hence the "goals/xG" column in `analysis_*.txt` (1.0–2.5) overstates xG error. It should be read as goals-per-xG including unlogged goals.

## Problems found
1. Reaction time and shot reading barely move results: shots travel ~28 m/s from short range (flight 0.2–0.3 s), so the dominant difficulty lever is the slow positional drift error, not reaction.
2. Keepers get tired (stamina issue already noted in F15), which inflates conversion in full-length matches; xG calibrated on short slices under-predicts.
3. Arena goal volume is high (7.5–8.2 per match at Pro vs Pro), above "modest" over Classic and above the old keeper. Cause: one combined recalibration step (PositionErrorMax 2.4→1.6, Base 1.2→1.5, Arena scale 1.0→0.85) fixed the level separation but did not get Arena down. Not re-tuned further this phase.
4. The shot log misses a fifth to a third of goals (above).
5. Old tests shifted by the new keeper and were adjusted with documented reasons (legacy test uses `Keeper.Enabled=0`; keeper-hands bound 2→4; pass-into-space completion 0.30→0.25; pressed/tired tolerance 0.004→0.01).
6. The old-keeper cells were not re-run; F15 data is the baseline (different sample size: 150 vs 100).

## Recommendations for F17
- Log every goal with its origin (shot, rebound, deflection, cross) so xG can be validated on all goals.
- Decide an Arena goals-per-match target and retune `ArenaGoalkeeperDefaults.PositionErrorScale` against it.
- Address outfield/keeper stamina (see `AI_STAMINA_PROPOSAL_PHASE15.md`) before recalibrating xG.
- Give reaction/reading a real effect (e.g. shot speed/placement variety) or accept positioning as the single difficulty lever.
- Rush/exit has no mechanical effect in Core; keeper come-out is only positional.
- Not done (per scope): penalties, free kicks, cards, replay, final stats, animations.

Data: `Docs/AI_GOALKEEPER_PHASE16_data/` (battery CSVs, shot logs, analysis scripts and outputs).
