# Phase 17 — match stability, stamina, goal traceability and quick tests (report)

Base: Phase 16 `f7c4551` (2358 tests). Branch `phase-17-match-balance-validation`. Code commit `30d5e67` (2375 tests, all green). No push, no PR, no merge. Unity not run, graphics and Creator untouched.
**Nothing here says the game is balanced or the AI finished.** What was measured is listed with its sample size; what was not measured is said.

## 1. What was measured (and how long it took)
All simulations are full-length matches (Arena 720 s, Classic 960 s), the same seeds for every comparison, Core deterministic (running the same code and configuration again reproduces every number exactly — checked: the adopted configuration on seeds 6001–6040 reproduces the experiment run bit for bit).

| Run | Content | Different matches | Real time |
|---|---|---|---|
| Baseline (f7c4551) | Professional / Novice / Elite against themselves, both modes, seeds 5001–5010 | 10 per cell (6 cells) | 276 s for all 6 (4 at a time), longest cell 219 s |
| Stamina probe | where the sprint goes by kind of decision, 3 seeds, 5 levels, 2 modes | 30 two-period matches | 177 s |
| Experiments E1–E4 (quick battery of the time) | one lever each, same 6 cells | 10 per cell | 275–286 s each |
| Extended validation (level 3) | Professional against Professional, base / walking 0.6 / walking 1.0, seeds 6001–6040 | 40 per cell (6 cells, one run per cell) | 371–485 s per cell, 864 s for the whole batch |
| Adopted code, level 3 | seeds 6001–6040 and fresh seeds 7001–7040 | 40 per cell (4 cells) | 491–496 s per batch of two cells |
| Quick battery, before / after | 6 cells, seeds 5001–5020 | 20 per cell | 276 s before, 287 s after |
| Reference of the quick battery | same, at commit `30d5e67` | 20 per cell | 287 s |
| Whole test suite | 2375 tests | – | 4 min 43 s (one earlier run 3 min 52 s; build 24 s) |

**Longest single run: 485 s. No run reached the limit (1200 s; the scripts stop a run at 1100 s and record it INCOMPLETE).** Incomplete or failed runs: none. Everything was run through `timeout`.

Timing comparison with the habit of Phase 16: a final Phase 16 battery (12 cells of 100 matches plus a validation) needed roughly an hour of continuous running (an estimate, it was not timed as one number); the quick battery now covers six cells in ~5 minutes and a level-3 cell in ~8 minutes, each run by itself. The speed-up comes from smaller, split runs and from not playing identical matches twice (see 5), **not** from shorter matches or fewer checks: matches stay full length and the test suite is untouched apart from the adjustments listed in 6.

## 2. Objective A — stamina
**Diagnosis (measured, not assumed).**
* The stamina of the keepers is not a problem: mean 99.2–99.6 of 100 in every cell. *Phase 16's report said the keepers tire: that was wrong and is withdrawn.*
* Field players: before the change 77–88 % of the stamina samples were below 20 and 33–46 % below 5; mean 15 (Arena) / 12 (Classic) of 100 over the match, quarter 1 averaging 35 (Arena) / 26 (Classic), quarters 2–4 6–11.
* Where the sprint goes (stamina probe, Professional): going for the loose ball 55 % (Arena) / 58 % (Classic) of the sprint decisions, interceptions 22 %, dribbling 10–11 %, repositioning 5–7 %.
* **Cause.** The locomotion model (`LocomotionStamina`) drains `3.0 · input² · sprint multiplier` per second and recovers only standing still (2 per second) or walking (a quarter of that = 0.5 per second, minus the cost of walking); running and sprinting recover nothing. A player who runs at 0.6 of the stick loses about 1.1 per second and never gains it back unless he stands or walks: the equilibrium is "empty".

**Experiments (one lever each; same seeds; Professional against Professional unless stated; 10 different matches per cell for E1–E4, 40 for the level-3 comparison).**

| # | Lever | Result | Decision |
|---|---|---|---|
| E1 | Chase of a loose ball at 0.65 of the stick (a run, no drain) when the ball is further than 8 m (decision layer) | mean stamina 15.31 → 15.42 (Arena Professional): no effect (the balls chased are within 8 m) | **reverted**, code removed |
| E2 | `WalkRecoveryFactor` 0.25 → 0.6 | mean 15.3 → 21.8, quarters 2–4 8–11 → 16–17, share below 5: 37 % → 9 % | **adopted** (see level 3) |
| E3 | `WalkRecoveryFactor` 1.0 | mean 26, share below 5: 4 %, but pass completion falls further | not adopted |
| E4 | `BaseStaminaDrain` 3.0 → 2.0 | quarter 1 rises (52) but quarters 2–4 stay 10–17, share below 5: 15–19 % | not adopted (it raises the first minutes, not the equilibrium) |

Level 3 (40 different matches per cell, seeds 6001–6040; half-widths are 95 % intervals):

| Config | Arena goals | Classic goals | Arena stamina Q1-Q4 | Classic stamina Q1-Q4 | share < 5 (A / C) | pass completion (A / C) |
|---|---|---|---|---|---|---|
| base (0.25) | 8.05 ± 0.92 | 5.72 ± 0.69 | 35.9 9.4 9.9 9.0 | 26.0 6.6 10.7 6.8 | 0.33 / 0.43 | 0.482 / 0.463 |
| 0.6 (adopted) | 7.62 ± 0.87 | 6.70 ± 0.91 | 39.2 15.7 16.6 15.4 | 30.6 14.0 17.4 13.6 | 0.09 / 0.10 | 0.435 / 0.415 |
| 1.0 | 7.55 ± 0.94 | 7.50 ± 1.00 | 41.8 19.7 20.7 19.0 | 33.8 17.7 21.2 17.4 | 0.04 / 0.04 | 0.400 / 0.391 |

Fresh seeds 7001–7040, adopted code: Arena 7.80 ± 0.97 goals, Classic 6.83 ± 1.07, stamina quarters 39.5 15.4 16.3 15.2 / 31.0 13.8 17.1 13.9, share below 5: 0.09 / 0.09, pass completion 0.422 / 0.416: the same pattern on independent seeds.

**Applied.** `LocomotionDefaults.WalkRecoveryFactor` 0.25 → 0.6 (one number). It is a locomotion number: it also applies to a human-controlled player who walks (a design decision to confirm).

**Costs and what is not shown.**
* Pass completion falls about 4–5 points (Arena 0.482 → 0.435, Classic 0.463 → 0.415): the sample is thousands of passes, so this is real. Likely cause (not tested): fresher defenders close down and intercept more. It is a regression of the change; it is accepted because the stamina profile was the defect, and it is flagged for Phase 18.
* Goals per match: Arena unchanged within the noise (8.05 → 7.62). Classic +1.0 ± 1.1 (not significant at 40 matches; E3 reaches +1.8 ± 1.2).
* Stamina is **better, not good**: quarters 2–4 average 14–17 (the target table calls ≥ 20 desired, ≥ 12 tolerable). Reaching "desired" would need another lever (running recovering a little, or a smaller run cost) — a separate experiment.
* The difficulty effort cap (Phase 14) was not touched. The level-dependent difference in stamina remains small.

## 3. Objective B — where the goals come from
**Instrumentation.** `GoalOriginTracker` (diagnostics only) keeps the last 16 ball events (shots, passes, clearances, touches, deflections, blocks, tackles, interceptions, wall bounces) and, for each `GoalScored`, `AiDiagnostics` writes a `GoalOriginRecord`: match id, time, team, scorer, own-goal flag, origin (`Shot`, `Rebound`, `Deflection`, `Cross`, `Unknown`), evidence (`Direct`, `Inferred`, `None`), the event just before the goal, the shot it is read from (and whether that shot is in the shot log), the last kick, and the ball positions. The Core's own goal log (`MatchState` goals) is still the authority: the observer only annotates it; it does not change when a goal is given, adds no random numbers and does not touch the match (tested: same fingerprint with and without it). The battery writes `*_goals.csv`.
Rules: `Shot` direct = the last kick was a shot of the scoring team and nothing touched the ball after it. `Shot` inferred = the opposing keeper touched it afterwards (see below). `Rebound` inferred = shot, then a stop or deflection, then a new touch of the scoring team. `Deflection` direct = shot, then a block/deflection or a touch of an opponent's defender, and no new touch of the scoring team. `Cross`: the Core has no cross action yet (`FootballActionKind.Cross` exists but is not implemented in `MatchController.Actions`), so it stays at zero by construction. Everything else is `Unknown`, with the previous event kept so that it can be examined.

**Result (adopted code, 40 different matches per mode and sample).**

| Sample | Goals / match | Shot | Rebound | Deflection | Cross | Unknown | Direct / Inferred / None | Core-flagged own goals |
|---|---|---|---|---|---|---|---|---|
| Arena 6001–6040 | 7.62 | 222 (73 %) | 1 | 1 | 0 | 81 (27 %) | 223 / 1 / 81 | 59 (19 %) |
| Classic 6001–6040 | 6.70 | 170 (63 %) | 5 | 0 | 0 | 93 (35 %) | 170 / 5 / 93 | 69 (26 %) |
| Arena 7001–7040 | 7.80 | 229 (73 %) | 2 | 1 | 0 | 80 (26 %) | 230 / 2 / 80 | 63 (20 %) |
| Classic 7001–7040 | 6.83 | 161 (59 %) | 0 | 2 | 0 | 110 (40 %) | 163 / 0 / 110 | 83 (30 %) |

* Rebounds and deflections are rare (0–2 % each): the earlier suspicion that rebounds explained the missing shots was not right.
* **What the Unknown goals are.** In 62–72 % of them the last event before the goal is the opposing goalkeeper's `GoalkeeperCatch`, the goal is flagged as an own goal by the Core (the keeper touched it last), and the last kick was a **long pass of the scoring team** about two seconds earlier (traced on seeds 6001–6004: the ball was at the goal line or inside the goal when the keeper caught it, ball height 0.2 m). That is 1.25–1.45 (Arena) and 1.45–1.85 (Classic) such goals per match: roughly 16–27 % of all goals. The remaining Unknown goals are passes that went in, controls and interceptions at the line.
* They are not new: in the Phase 15 data (old keeper) 27–31 % of the goals were not preceded by a followed shot; in Phase 16 20–34 %.
* **Consequence not yet handled:** the keeper's "save" is counted when he catches or parries while a shot is in flight, so a catch on the line that is then given as a goal is also counted as a save — the save shares of Phase 16 (77–93 % by level) are slightly overstated (about one per match); the target table therefore has no save target yet.
* Per the instruction, the goal logic was **not** changed, and the xG was **not** recalibrated: the data quality (known origin 59–74 %) is not enough yet.

## 4. Objective C — targets
`Docs/AI_BALANCE_TARGETS_PHASE17.md`: desired / tolerable / investigate ranges, sample sizes and the observed value for goals per match (Arena, Classic), the Arena–Classic gap, Elite against Professional, Novice against Professional, level ladder, stamina (below-5 share, mean quarters 2–4, keepers), known goal origin, goals with a logged shot, pass completion (monitored), seed stability. They are working proposals, to be confirmed by the designer.

## 5. Objective D — quick, reproducible battery
`Tools/Balance/fast_battery.sh` + `compare_battery.py` + `README.md`; reference `Docs/AI_BALANCE_PHASE17_data/fast_reference.json` (commit `30d5e67`, configuration "none"). Fixed seeds 5001–5020, six cells (Professional, Novice, Elite against themselves, Arena and Classic), full-length matches, each cell its own run limited to 1100 s, summary with goals, stamina by quarter, share below 5, keeper stamina, shots, pass completion, mistakes per carrier decision and known-origin share, status and real time per cell; flags what leaves the bands. It was checked to catch what it should: compared against a reference taken with walking recovery 0.25 it flags every stamina metric and the pass completion, and compared with itself it flags nothing.
Three levels (`Tools/Balance/README.md`): 1 quick tests (the suite, 4 min 43 s, or a filter), 2 this battery (~5 min), 3 extended validation cell by cell with `Phase15Battery_WriteTheCsv` (~8 min per cell of 40 different matches).
**Fix found on the way.** For two teams of the same level the battery played every seed twice, with identical results (levels swapped, same teams): `Phase15Battery_WriteTheCsv` now plays it once (`TheBattery_PlaysEachSeedOnce_WhenBothTeamsAreTheSameLevel_AndTwiceWhenTheyDiffer`). Consequences: every equal-level cell of Phases 15/16 and of E1–E4 has half as many different matches as rows (cross-level cells and the Phase 16 conclusions on the level ladder are not affected, because they come from cross-level cells); the half-widths in this report use the different matches. Stamina and pass-completion shares are unaffected in value.
**Limit.** 20 different matches per cell: goals per match ±1.2. The quick battery detects a stamina or pass-completion change; it cannot establish a change of a goal or win-rate difference smaller than that.

## 6. Tests
Before (Phase 16) 2358 → after 2375, all passed, 0 failed, 0 skipped; none incomplete. New (`AiMatchStability17Tests`, 17): goal origin by case (shot, rebound, deflection, block, no shot, too old, other team's shot, own goal without shot, keeper touch on the line, opponent's touch), ring, one record per goal and counts consistent with the Core's score, deterministic log and same match with and without the observer, walking recovery value and relations, stamina over a whole Arena match (field players above 11 in quarters 2–4, keepers above 90), low-stamina counters, battery plays each seed once, reproducible battery text and columns.
**Existing tests that the stamina change moved (changed on purpose, each with the reason in the assertion):**
* `Recovery_IdleRecovers_WalkRecoversSlowly_RunAndSprintDoNot`: walking = `2 · WalkRecoveryFactor` (was a literal 0.5) and still below idle.
* `TheRefactoredScores_ReproduceThePhase11Matches_BitForBit…`: its experiment string now also sets `*.Locomotion.WalkRecoveryFactor=0.25` (the bit-for-bit reference is a Phase 11 match).
* `ThePassingGame_IsMoreBalanced…`: the Classic pass-share floor 0.28 → 0.26 (measured 0.275; Phase 12 had 0.21) and the Arena/Classic circulation comparison now `Classic > 0.85 · Arena`, because the eight short matches changed trajectories (520 passes against a bit over 540); over full matches Classic circulates more (437 against 315 per match, level-3 data).
* `TheMistakesPerDecision_FallWithTheLevel…`: "the best still errs" is now checked over 3 matches of 360 s instead of 90 s: over 90 % of the mistakes happen when tired, and with the new recovery nobody is tired in the first 90 s (full matches: Elite errs 0.6–0.7 % of the carrier decisions, unchanged).
Two new instrumentation columns in the harness (`LocomotionTuning` overrides through `FS27_EXP "*.Locomotion.X=…"`, keeper and low-stamina columns) change no Core behaviour.

## 7. Before / after (quick battery, 20 different matches per cell, seeds 5001–5020)
| Cell | goals before → after | stamina Q1-Q4 before | stamina Q1-Q4 after | share < 5 | pass completion | keeper stamina |
|---|---|---|---|---|---|---|
| Arena Professional | 7.15 → 8.35 | 34.9 8.1 10.3 8.0 | 39.6 15.7 16.4 15.6 | 0.36 → 0.09 | 0.476 → 0.434 | 99.5 → 99.4 |
| Classic Professional | 6.20 → 7.10 | 25.4 6.4 10.5 6.4 | 29.4 14.0 17.3 13.9 | 0.45 → 0.09 | 0.463 → 0.423 | 99.6 → 99.6 |
| Arena Novice | 12.60 → 11.55 | 35.0 8.4 8.6 8.1 | 37.4 13.3 14.3 13.9 | 0.32 → 0.10 | 0.501 → 0.476 | 99.3 → 99.2 |
| Classic Novice | 9.15 → 9.25 | 24.0 5.9 9.5 6.2 | 27.7 11.5 14.8 11.7 | 0.43 → 0.11 | 0.469 → 0.432 | 99.4 → 99.4 |
| Arena Elite | 4.95 → 5.85 | 34.9 8.2 10.8 9.7 | 39.3 16.8 17.8 16.7 | 0.36 → 0.09 | 0.470 → 0.424 | 99.6 → 99.6 |
| Classic Elite | 4.10 → 5.30 | 26.6 6.7 10.9 7.9 | 31.6 14.9 19.1 15.4 | 0.45 → 0.10 | 0.449 → 0.399 | 99.6 → 99.6 |

Goals: the quick battery's samples are too small to separate the movements of ±1 (it is consistently a bit higher after, which fits the Classic increase of the level-3 data but is not conclusive). Goal origin: Section 3. Level ladder (teams playing themselves: Novice 9–12.6 goals per match, Professional 6–8.4, Elite 4–6) is an indication that lower levels leak goals; it was not re-measured as an ordered ladder in this phase.

## 8. Open problems and limits
1. **Goals the keeper touched last (roughly 16–27 % of the goals).** A long pass reaches the keeper at the line or behind it, he catches it, and the Core gives the goal flagged as an own goal. Whether it is a Core rule (a ball that has crossed the line when he touches it), a keeper positioning problem from Phase 16 (the effect existed with the old keeper) or a ball-height issue is not known: it needs a Core review. It inflates goals, "saves" and the unknown-origin share.
2. Stamina is better but not at the "desired" row of the targets; pass completion fell 4–5 points; Classic goals may have risen by about one.
3. The `WalkRecoveryFactor` change also applies to the human player: confirm the feel.
4. Cross: no Core action; `Rebound` and `Deflection` have too few cases for a statistic.
5. The earlier equal-level samples (Phases 15/16) were counted twice; their intervals should be widened by √2 (equal-level cells only).
6. xG not recalibrated (data quality), as instructed.
7. Phase 14's "mistakes" model is almost entirely a "tired" model (over 90 %): with the better stamina the lower levels err slightly less when tired and the model will need a look if the difficulty is to rest on something other than fatigue.
8. The Level 2 battery cannot decide goal-level questions.

## 9. Git and state
Base `f7c4551`; code commit `30d5e67`; this documentation and the reference are in the commit that follows it. Work tree clean after that commit. Branch `phase-17-match-balance-validation`; no push, no PR, no merge.
