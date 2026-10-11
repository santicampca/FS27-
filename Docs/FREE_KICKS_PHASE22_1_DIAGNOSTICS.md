# Phase 22.1 — why did Classic Professional score fewer goals after the free kicks? (diagnostic report)

Branch `phase-22-free-kicks`, on top of `3882b61`. Diagnostic only: **no production code, no balance parameter and no football rule was changed** (`git diff 3882b61 -- Assets/_Project/Scripts` is empty). Only the test harness (reading-only counters), one script and the data were added. No push, no PR, no merge. Unity, graphics and the Creator untouched. The AI is not finished.

## 1. Answer in one paragraph

The drop reported in Phase 22 (Professional 5.88 → 4.70 goals per match) **was mostly sampling noise**: it appeared in the 40 matches the Phase 22 battery used and shrank to −0.13 and +0.05 on two fresh samples of 100 matches each. Over all 540 paired matches (three levels) goals fall by **−0.23 ± 0.14 per match (−3.6 %, z = −1.7): not significant, but a small real loss of a few percent cannot be ruled out.** If it exists, the cause is **not isolated**: the goals scored in the 10 s after a foul explain only −0.09 of it, removing the wall does not separate from the noise, and nothing else I measured moves. What the free kick demonstrably does is: fewer fouls and duels, more (low-value) shots after a foul, and a placement flaw in 2.1 % of the set-ups (section 7). Nothing I found justifies changing the balance now (recommendation, section 9).

## 2. What makes the two phases comparable (verified, not assumed)

| check | result |
|---|---|
| free kick **OFF** with the Phase 22 code (`FS27_EXP=C.FreeKick.Enabled=0`) against the stored **Phase 21** battery, Professional, seeds 5001–5120 | **214 shared columns × 40 matches, 0 differing values** (bit for bit) |
| free kick **ON** now (commit `3882b61` + diagnostics) against the stored **Phase 22** battery (commit `cd033aa`, before the manual taker), Professional, Novice and Elite, seeds 5001–5120 each | **228 shared columns × 20 matches × 6 cells, 0 differing values** (`Docs/FREE_KICKS_PHASE22_1_DATA/manual_taker_regression_check.txt`) |
| Arena (control), Professional, seeds 5001–5120, against the stored Phase 21 battery | **214 shared columns × 40 matches, 0 differing values; 0 fouls, 0 free kicks, 0 stopped steps** |

Consequences: (a) the only difference between "Phase 21" and "Phase 22" is the free-kick mechanism itself, nothing else in the AI or the Core; (b) the manual choice of taker introduced **no regression** in any level; (c) the diagnostic counters changed nothing in the simulation (the instrumented harness reproduces the old numbers exactly). Conditions: full-length matches (960 s), the same seed = the same pair of teams (`seed % 12`, `(seed*5+3) % 12`), same difficulty profiles, 1/60 s step, two teams of the same level play each seed once, Core deterministic.

## 3. Method and statistical limits

* **Design:** arm A = free kick off, arm B = free kick on, **same seeds, paired by seed**; the per-seed difference is averaged and its standard error computed from the 40–100 differences. Blocks of seeds: 5001–5120 (the Phase 22 sample, Professional 40, Novice 40, Elite 40), 6001–6500 (Professional 100, Novice 60, Elite 60), 7001–7500 (Professional 100, Novice 100). About 1,400 full matches were simulated for this report.
* **Noise is large.** Once the free kick changes the first foul, the rest of the match is a different match: of 100 paired matches only 3 came out identical (goals and shots), and the standard deviation of the per-match goal difference between two arms is about 3. Hence ±0.3 goals is the 1-sigma precision of a 100-match arm.
* **Placebo (noise calibration).** Free kick ON in all three arms, Professional, seeds 6001–6500, changing only an unrelated distance by a few decimetres: 5.27 (reference), 5.25 (taker 1.1 m behind the ball instead of 1.0), **5.83 (wall at 4.3 m instead of 4.0)**. Moving a wall by 30 cm "changes" the goals by +0.56, more than the effect under study: single comparisons of 40–100 matches cannot be read as causes.
* **Correlation versus causation:** the arm contrast is causal for the *package* "free kick on" (the arms differ in that switch and nothing else), but every statement about *which part* of the package acts is only as good as the measurement behind it; where I could not isolate it I say so.

## 4. Goals, per match (both teams), A = free kick off → B = on

| level | block | n | A | B | B−A | SE | z |
|---|---|---|---|---|---|---|---|
| Professional | 5001–5120 | 40 | 5.88 | 4.70 | −1.18 | 0.55 | −2.1 |
| Professional | 6001–6500 | 100 | 5.40 | 5.27 | −0.13 | 0.32 | −0.4 |
| Professional | 7001–7500 | 100 | 5.44 | 5.49 | +0.05 | 0.30 | +0.2 |
| Novice | 5001–5120 | 40 | 8.43 | 8.35 | −0.07 | 0.56 | −0.1 |
| Novice | 6001–6220 | 60 | 8.32 | 7.43 | −0.88 | 0.43 | −2.1 |
| Novice | 7001–7500 | 100 | 7.99 | 7.85 | −0.14 | 0.35 | −0.4 |
| Elite | 5001–5120 | 40 | 4.17 | 4.00 | −0.17 | 0.34 | −0.5 |
| Elite | 6001–6220 | 60 | 4.40 | 4.55 | +0.15 | 0.36 | +0.4 |
| **Professional, pooled** | | 240 | 5.50 | 5.27 | −0.23 | 0.20 | −1.1 |
| **Novice, pooled** | | 200 | 8.18 | 7.83 | −0.35 | 0.24 | −1.4 |
| **Elite, pooled** | | 100 | 4.31 | 4.33 | +0.02 | 0.25 | +0.1 |
| **All, pooled** | | 540 | 6.27 | 6.04 | **−0.23** | 0.14 | −1.7 (−3.6 %) |

Reading: two of eight blocks reach |z| ≈ 2 (as expected by chance among eight comparisons); the pooled estimate shrinks as samples are added (block 5001: −0.47, block 6001: −0.26, block 7001: −0.04). Elite shows no effect at all. The headline "−20 % for Professional" is the first block alone.

## 5. The mechanisms asked about (Professional, 240 paired matches with the diagnostic counters)

| metric per match (both teams) | A off | B on | B−A | SE | z |
|---|---|---|---|---|---|
| goals | 5.50 | 5.27 | −0.23 | 0.20 | −1.1 |
| shots rated | 73.4 | 75.3 | +1.8 | 0.9 | +2.1 |
| expected goals (xG sum) | 3.82 | 3.82 | +0.01 | 0.06 | +0.1 |
| mean shot quality | 1.06 | 1.05 | −0.01 | 0.01 | −1.8 |
| danger-zone entries / final-third entries | 77.2 / 120.2 | 77.2 / 118.9 | 0.0 / −1.4 | 0.8 / 0.8 | 0 / −1.7 |
| clear chances | 58.5 | 56.7 | −1.8 | 1.2 | −1.5 |
| possessions (pace) / passes attempted | 370.6 / 514.8 | 372.4 / 512.1 | +1.8 / −2.7 | 1.5 / 2.6 | +1.2 / −1.0 |
| fouls | 5.05 | 4.69 | −0.36 | 0.18 | −2.0 |
| yellow / red cards | 0.85 / 0.11 | 0.85 / 0.11 | 0 / 0 | | |
| tackle contacts / challenge contacts / all contacts | 4.4 / 21.0 / 25.4 | 4.1 / 20.0 / 24.2 | −0.2 / −1.0 / −1.2 | | −1.2 / −2.2 / −2.3 |
| fouls on a team that had the ball within 16 m of goal | 2.07 | 1.75 | −0.32 | 0.11 | −2.8 |
| shots by the fouled team within 10 s of a foul | 1.34 | 2.27 | **+0.93** | 0.11 | +8.2 |
| goals by the fouled team within 10 s of a foul | 0.32 | 0.23 | −0.09 | 0.06 | −1.5 |
| …only fouls in a chance (carrier within 16 m of goal): shots / goals | 0.89 / 0.23 | 1.63 / 0.12 | +0.75 / −0.11 | | +7.6 / −2.2 |
| mean time until the fouled team loses the ball (sum over fouls, s) | 10.2 | 7.3 | −2.9 | 0.6 | −5.0 |
| goals OUTSIDE the 10 s after a foul | 5.18 | 5.04 | −0.14 | | |
| match seconds actually played (of 960 steps) | 949.71 | 949.64 | −0.07 | | |

Per free kick (1,125 taken): **34 % are a shot, 55 % a pass, 11 % other (clearance forward); the taker kicks it in 100 %; goals within 8 s of the kick: 3.7 % of the free kicks (10.9 % of the shots).** Every foul gave exactly one free kick, every free kick was prepared once and whistled once, and the clock was stopped for one harness step per free kick (the harness whistles at once).

### The hypotheses, one by one

| hypothesis | verdict | evidence |
|---|---|---|
| attacks interrupted by fouls | **real but tiny** | fouls on a team with a chance fall −0.32/match (fewer duels); goals in the 10 s after a foul −0.09 (−0.11 for chance fouls): about 40 % of the pooled −0.23 at most, and not significant |
| chances lost in the stoppages | not demonstrated | xG is unchanged (3.82 / 3.82); danger-zone entries unchanged; clear chances −1.8 (z −1.5) |
| free kicks that produce no chances | **partly true, small** | they create +0.93 shots per match but only 0.17 goals per match: a shot after a foul is worth less than the carried attack it replaced (0.89 → 1.63 shots, 0.23 → 0.12 goals in chance fouls) |
| possession lost at restarts | **false** | the taker has the ball in 100 % of the free kicks and the same team keeps it; possessions unchanged (+1.8); the faster loss after the foul (−2.9 s) is the free-kick *shot* leaving the team's feet |
| effective playing time | **false** | 949.71 vs 949.64 of 960 s; the clock stops during the stoppage by design |
| placement / selection / resumption errors | **one flaw, 2.1 %** | section 7; no duplicated or missing restart, taker always valid, ball always on the spot |
| AI behaviour changes | **false outside the taker** | free kick off = Phase 21 bit for bit (section 2); the only new AI code is the taker's decision |
| the wall closing the game | not separable from noise | wall removed (`WallMaxPlayers=0`): Professional 5.39 (off 5.40, on 5.27), Novice 8.20 (off 8.32, on 7.43); placebo spread is ±0.5 |

Verdict on causes: **demonstrated** effects are fewer fouls/duels and more, weaker shots after fouls; **not demonstrated** is any measurable net loss of goals beyond what the 10 s after fouls explains; the remaining −0.14 outside those windows is within noise (placebo range).

## 6. Novice, Elite and Arena

| level | goals A → B (pooled) | fouls (Novice and Elite: the 60 matches of seeds 6001–6220 with the counters; Professional: 240) | free kicks / match | shots within 10 s of a foul | goals within 10 s of a foul | notes |
|---|---|---|---|---|---|---|
| Novice (200) | 8.18 → 7.83 (−0.35, z −1.4) | 6.0 → 6.2 | 6.2 | 2.5 → 3.4 | 0.32 → 0.28 | free kicks per match highest; set-up anomalies 0.17 per match (they foul most near the lines) |
| Professional (240) | 5.50 → 5.27 (−0.23, z −1.1) | 5.1 → 4.7 | 4.7 | 1.3 → 2.3 | 0.32 → 0.23 | |
| Elite (100) | 4.31 → 4.33 (+0.02, z +0.1) | 5.7 → 5.1 | 5.1 | 1.4 → 2.6 | 0.27 → 0.22 | no effect on goals |
| **Arena (control)** | **identical to Phase 21** | 0 → 0 | 0 | | | 214 columns × 40 matches, 0 differences; no free kick, wall, card or pause |

(Raw tables: `Docs/FREE_KICKS_PHASE22_1_DATA/summary.txt`, regenerated by `summarize.py`; per-level comparisons with `analyze.py`.)

## 7. The placement flaw found (rivals left inside the circle)

In **24 of 1,125 free kicks (2.1 %)** an opponent who is not in the wall ends closer than the wall distance (1.4–3.0 m instead of ≥ 4 m). All 24 reproduced cases (`anomalies_reproduced.txt`) have the ball **1 m from the touchline (|z| = 11.5) or from the goal line (x = −19)**. Cause: `ClearFreeKickCircles` pushes a rival radially *away* from the ball; when "away" points out of the pitch the position is clamped to the lines (`InsideLines`/`ClampPlayer`) and he stays inside the radius. It does not block the match, the taker and ball are always valid, and the after-whistle protection (`StepFreeKickProtection`, which pushes the same way) shares the limitation. **Not corrected in this phase** (a rule/placement change). The test `FreeKickDiagnostics_APlacementFlawIsMeasured_NotHidden…` documents the *current* behaviour on purpose: when the flaw is fixed that assertion must be reversed.

## 8. Metrics and tools added (reading only)

* Battery CSV columns (`dgFouls, dgFoulsFinal, dgFoulsChance, dgClosed, dgLostTime, dgShot, dgGoal, dgLost, dgProgress, dgChanceClosed/Shot/Goal, dgCtlN/Shot/Goal/Lost` (control windows every 15 s on the team with the ball), `dgAwarded, dgPrepared, dgTaken, dgAnomalies, dgStopSteps, dgPlayed`) plus the Phase 22 `fk*` columns. Windows: 10 match seconds after each foul; "chance" = the fouled team had the ball within 16 m of the goal it attacks. Set-up anomalies are checked at every whistle: taker valid and of the right team and not in goal, ball on the spot, spot inside the field, no non-wall rival nearer than the wall distance − 0.8 m, wall made of active field players.
* `FS27_EXP` accepts `C.FreeKick.<Field>=<value>` (diagnostics only), e.g. `C.FreeKick.Enabled=0` plays the Phase 21 rules with the Phase 22 code; `BAT_ANOM=<file>` writes the anomaly details.
* `Tools/Balance/diag_cells.sh` (cells of 20 matches from stdin, 4 at a time, each ≤ 1100 s), `Docs/FREE_KICKS_PHASE22_1_DATA/analyze.py` (paired comparison of two arms), `summarize.py` (every table here).
* Tests (`FreeKickDiagnosticsTests.cs`, 4): the counters agree (awarded = fouls, prepared = taken, one stopped step per free kick); with the free kick off there are fouls and no free kicks and Arena has neither; the counters are deterministic; the known placement flaw is documented.
* **Reproduce:** `dotnet build Tests/Core.Tests`, then e.g. `printf 'Classic Professional 6001\n…' | Tools/Balance/diag_cells.sh OUT "C.FreeKick.Enabled=0"` (arm A) and with `""` (arm B), then `python3 -I Docs/FREE_KICKS_PHASE22_1_DATA/analyze.py A_DIR B_DIR Professional`. Directories used: `off`, `on` (5001/5101), `off_pro_6000`, `on_pro_6000`, `placebo1_pro_6000`, `placebo2_pro_6000`, `nowall_pro_6000`, `off_ne_6000`, `on_ne_6000`, `nowall_no_6000`, `off_pro_7000`, `on_pro_7000`, `off_no_7000`, `on_no_7000`, `on_ne_5000`, `arena`. The cell CSVs, logs and times are kept.

## 9. Possible corrections, ordered by risk, and the recommendation

1. **Nothing (keep the current behaviour).** Risk: none. The goal change is not significant (−3.6 % ± 2.3 %).
2. **Placement of rivals near a line (section 7):** push along the direction that stays inside the pitch (or to the nearest point at the radius that is inside). Risk: low, only the 2 % of set-ups near a line; does not touch the balance; add a test that reverses the documented one.
3. **Free-kick quality** (taker choice by the shot vs. the pass, lob power by distance, more passes to a runner): Risk: medium, changes the goals from free kicks (0.17 per match now) and needs a battery.
4. **Advantage rule** (play on when the fouled team keeps a chance): restores the advantage the carried attack had in Phase 21; Risk: medium-high (new rule, the Phase 21 report already listed it as deferred; it must not duplicate the free kick).
5. **Retuning goals/shots** to hide the difference: risk high and unjustified; **not recommended**: the difference is inside the noise.

**Recommendation: conserve the current behaviour now; do not correct anything for balance.** The −20 % was noise; the pooled −3.6 % is not significant. Correct only the cheap placement flaw (item 2) when convenient, not as a phase of its own. Do not spend more phases on this diagnosis: more matches would only narrow ±0.14 (to detect −0.23 at 3 sigma ≈ 1,300 more paired matches ≈ 1 hour of simulation), and the result would not change a design decision.

## 10. Tests run and regressions

* Whole suite after the diagnostics: **2481 passed, 0 failed, 0 skipped, 7 min 32 s** (2477 before + the 4 `FreeKickDiagnostics` tests; limit 1200 s respected). The 4 new tests alone: 4 passed in 24 s.
* No regression from the manual choice of the taker (section 2). No regression in Arena. The diagnostics did not alter the simulation (arms reproduce the earlier batteries bit for bit).

## 11. Limitations still open

* The goal effect is bounded to roughly −8 % … +1 % (95 %) pooled, not resolved; the cause of a possible small loss is not isolated.
* The harness whistles at once: the real dead-ball time of a client (≥ 2 s per free kick, about 9 s per match) is not simulated and does not count as match time.
* The set-up flaw of section 7 remains.
* Penalties, fouls in the area, advantage, offside, set-piece roles: out of scope, unchanged. Nothing was tried in Unity or on a touch screen.
