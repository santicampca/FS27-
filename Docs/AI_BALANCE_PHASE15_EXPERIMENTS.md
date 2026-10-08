# Phase 15 — Experiments log

Rule: one change per experiment; BASELINE → CHANGE → RESULT → DECISION. Seeds: S1 = 1001–1075 (baseline), S2 = 2001–2075 (validation, fresh), S3 = 1001–1020 (a part of S1 repeated), probe seeds 3001+ (tuning) and 3201+ (validation, fresh). Every cell = 150 matches of real length (75 seeds × the two sides), except S3 (40) and the probes. No number was changed in advance of seeing why.

## What was looked at and NOT changed

| Question | Evidence | Decision |
|---|---|---|
| Are the five levels different? | Error rate 15.6 → 10.2 → 5.5 → 2.3 → 0.6 % (Arena, full matches, CI width < 0.01); pass completion, clear chances created/conceded, recoveries ordered in both modes | No change: architecture and behaviour confirmed. |
| Amateur vs Profesional (the explicit F14 problem) | Same decision in the same situation 94.7 % (carrier type) / 97 % (off the ball): the engine differs by ≈ 5 % of decisions; the rest of the gap is the rhythm (×1.25), the delays and the mistake rate (×2). Process: error rate +0.047 [0.044, 0.050], clear chances −4.4 per 100 possessions [−6.2, −2.7], recoveries −0.055 (all CIs exclude 0); Classic score rate 0.397 [0.323, 0.473] | **Distinguishable** (by process in both modes, by result in Classic; in Arena the result CI 0.370–0.517 still contains 0.5). Not tuned: raising Amateur's errors would only make it a worse Novato. |
| Experto vs Élite | Process separates them (error rate 2.2 % vs 0.6 %, clear chances +3.3 / +2.7 per 100 possessions, CIs exclude 0), results do not in Classic (0.490 [0.417, 0.567]) and barely in Arena (0.543 [0.470, 0.617]) | Not tuned. Shots per match are flat across levels (28–31 Arena); the levels differ in conversion and chance creation, and the top two end up equal in conversion (9.2 % vs 9.4 %). Reported as a limit, see pending problems. |
| Novato vs Amateur in Classic | 0.447 [0.370, 0.520] head to head; vs Profesional 0.450 / 0.418 (n = 300) and Amateur 0.397: the intervals overlap | No real inversion; noise. Not tuned. |
| `ShootMinScore` 2.0 | 60 Classic matches: scores < 2.0 converted 1 of 45 (F14 evidence); here the shot economy (shots per possession ≈ 0.32 at every level in Arena) is flat across levels | Kept. |
| Shooting `ShotHabit` / `DribbleHabit` | Shots chosen per match are 28.3–31.0 (Arena) across levels: the habit is not what moves the results | Not tuned (no evidence of a problem). |
| Stamina | Mean stamina is 7–12 of 100 from the second quarter on at EVERY level (Arena q1 33 → q2 7–11); 74–76 % of the drain is chasing the ball (50–56 %) and intercepting (17–20 %), both essential | Not touched; separate proposal `AI_STAMINA_PROPOSAL_PHASE15.md`. |

## Experiment A — game management (`RiskBias`)

* **Baseline.** `RiskBias` = 0.6 (score added at full urgency to the forward pass, the pass into space and the shot; removed from them when protecting a lead). Probe: the last 45 s of a period with the score 0-2, 0-0 and 2-0, 40 seeds per level and mode, share of the carrier's decisions that are risky (forward pass, pass into space, shot). Swing = trailing − leading: Arena +1.8 / +0.8 / +1.4 / −0.4 / +0.7 points (Novato … Élite), Classic +1.7 / +1.8 / +0.6 / +0.7 / +2.1. The direction was right on 8 of 10 but the size was at the noise level (± 1 point): the adaptation existed on paper and was invisible in the play.
* **Change.** `RiskBias` 0.6 → 1.5 (only this number; through `FS27_EXP="*.Profile.RiskBias=1.5"` for the runs).
* **Result, same probe seeds.** Swing Arena +5.6 / +5.0 / +6.6 / +7.3 / +5.9, Classic +3.0 / +4.7 / +3.4 / +5.5 / +7.4 (all ten positive).
* **Validation 1 — fresh probe seeds (3201+).** Baseline swing Arena +2.3 / +2.3 / +3.3 / +0.9 / +4.7, Classic −0.5 / −2.3 / +1.6 / +4.4 / +4.3; with 1.5: Arena +5.6 / +5.5 / +6.0 / +4.2 / +8.6, Classic +1.2 / +3.0 / +7.0 / +5.8 / +5.0. Replicated (nine of ten ≥ +3; the weakest is Classic Novato, +1.2, the level with the lowest awareness: expected).
* **Validation 2 — does it hurt the game?** Fresh seeds S2, full matches, Élite and Novato vs Profesional: Arena Élite 0.567 → 0.607, Novato 0.393 → 0.410; Classic Élite 0.583 → 0.560, Novato 0.387 → 0.357 (all inside the ± 0.07 of the intervals, signs and order kept). Goals per match 6.56 → 6.50 (Arena), 7.09 → 7.09 (Classic); 0-0 and "a team with 8 or more goals" unchanged (0.3 % / 0 %; 4.3 → 4.0 % / 9.0 → 7.3 %).
* **Validation 3 — repeat of part of S1 (S3).** Arena Élite 0.613 → 0.637, Novato 0.375 → 0.463; Classic Élite 0.637 → 0.575, Novato 0.450 → 0.500 (n = 40 per cell, intervals ± 0.14: no sign of harm, no sign of benefit).
* **Decision: ADOPTED.** It does what it was meant to (the team that chases a game takes more risks, the team that protects a lead takes fewer), the effect holds on fresh seeds, and nothing else moved beyond noise. It is a change of behaviour, not of strength.

## Not run (and why)
No second experiment was justified by the data: the remaining gaps (Experto vs Élite, Amateur vs Novato in Classic) are inside the statistical noise of 150 matches or are not attributable to a single number; the one real bottleneck (stamina) belongs to a separate proposal.
