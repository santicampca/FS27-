# Phase 14 — AI competitiveness, attack diagnostics and difficulty

Branch `phase-14-ai-competitiveness` (from `ff43132`, baseline 2299/2299). Data: `Docs/AI_DIFFICULTY_PHASE14_data/`.
Nothing here says the AI is finished or the game balanced.

## 1. Diagnosis before changing anything

* **Classic shot threshold (`ShootMinScore` 2.0).** 60 Classic matches, 2.0 vs 1.0 vs 0: 340 / 348 / 347 shots. Shots scored below 2.0 converted 1 of 45; the threshold removes only bad shots. The F13 drop of Classic goals (10 → 2) was sample noise (62 goals in 60 matches). **Kept.**
* **Pass returned to the passer (Core, not AI).** The re-touch cooldown followed the *last toucher*, so any touch by someone else (a team-mate's control, an opponent's block, a goalkeeper catch) let the passer take his own pass back on the next step (80 of 782 Arena passes returned in < 0.1 s). Fixed with one condition in `StepBallInteractions` (a player whose kick is still recovering cannot touch the ball), switchable with `ActionTuning.KickerKeepsOffHisKick` (1 = on, 0 = the old behaviour). Returns within 0.25 s: 80 → 0.
* **Stamina.** Estimated drain ≈ 1.7 per player-second (30 % chasing loose balls, 29 % repositioning, 11 % interceptions) against a 2/s recovery that only happens idle or walking; every level ends a 2-minute match with 5–15 of 100. Locomotion/F3 untouched. The decision part is corrected (below); the essential sprints (loose ball, closing the carrier) are the rest and are proposed for F15.

## 2. Architecture

* `DecisionProfile` (+ `DecisionProfileDefaults/Tuning`, `DecisionDice`): the single reading of a `DifficultyDefinition` by the decision layer. Built once; **no `if level` anywhere** (a test forbids it); no attribute, speed, stamina or shot/pass bonus is stored or changed (a test checks players are identical before and after).
* One engine, one Core. `BasicDecisionEngine.SetProfile(team, profile)` gives each team its own set (profiled defence tuning); `BasicDecisionDriver.SetDifficulty(team, definition, players)` gives it its rhythm and gates. No profile = exactly Phase 13.
* Mechanisms (all deterministic, hash-rolled from clock + player, no stream, never the Core RNG):
  1. decision rhythm (interval scaled by level);
  2. **perception**: a player notices a turnover after his reaction time (attribute-aware) and keeps doing what he was doing meanwhile;
  3. **commitment**: a defender in range waits his reaction time before the tackle/slide/challenge;
  4. **controlled mistakes**: second-best option only if within a small score gap, more likely when options are close, under pressure, tired or when the player is poor at that action; classified (bad pass, wrong receiver, bad shot, unnecessary dribble, excessive risk, passive);
  5. **reading the game**: bias by the player's own quality for each action, level habits, and score/clock urgency (risk when chasing, safety when protecting a lead);
  6. **defence**: anticipation horizon, who presses, pressing stamina, and (Classic only) how risky a challenge he accepts;
  7. **positioning**: slow, small drift from the ideal place;
  8. **legs**: non-essential movements are capped as stamina falls (a better manager saves earlier).
* Observer: per-team filter (`OnlyOneTeam`), `Twin` observer, mistakes, final-third/danger entries, clear chances, distance from home.
* `Phase14Battery` (explicit tool): same teams and seed, tested side once home and once away.

## 3. Results (tested level vs an opponent; each seed played twice; per-row sample 24–48 matches)

Versus **Professional** (Arena 48 matches, Classic 48 matches):

| Level | Arena W/D/L GF-GA | Arena mistakes per carrier decision | Arena clear chances created / conceded | Classic W/D/L GF-GA | Classic mistakes | Classic clear chances created / conceded |
|---|---|---|---|---|---|---|
| Novato | 11/14/23, 30-46 | 14.8 % | 93 / 367 | 11/25/12, 38-33 | 13.3 % | 101 / 218 |
| Amateur | 15/17/16, 30-32 | 8.7 % | 159 / 242 | 16/25/7, 25-22 | 8.3 % | 143 / 133 |
| Profesional | 17/15/16, 29-33 | 4.4 % | 265 / 253 | 11/22/15, 20-23 | 3.7 % | 142 / 195 |
| Experto | 16/19/13, 33-27 | 2.4 % | 334 / 248 | 15/23/10, 32-24 | 1.9 % | 203 / 163 |
| Élite | 16/22/10, 30-24 | 0.7 % | 306 / 209 | 16/18/14, 35-32 | 0.5 % | 212 / 152 |

Versus **Novato** (24 matches each): Arena Élite W14/D7/L3, 21-5; Experto 12-10; Profesional 23-16; Amateur 18-16; Novato 10-17. Classic is flat (Élite 20-13, Experto 24-12, Novato 19-12).
Versus **Élite**: nobody dominates (Arena Experto 22-18, Élite 16-20; Classic Experto 23-12, Novato 21-13); Élite loses or draws about as often as it wins.

Other measured effects: pass completion rises with level (Arena 31 % → 34–35 %), clear chances created ×3, chances conceded −40 % (Arena), danger entries conceded 329 → 253, Classic fouls stay at 4–9 per 48 matches, Arena 0 fouls and contacts at every level. Stamina at 110 s: F13 5–10 → 12–15 (no level separates; the essential sprints dominate).

## 4. Honest reading

* **The levels differ clearly in HOW they play** (mistakes, chances created and conceded, rhythm, completion) and in a monotone way.
* **Results separate only partly.** Arena against weak opposition does (Élite 21-5 vs Novato); against Professional the order is right at the ends (Novato −16, Experto/Élite +6 goals in 48 matches) but Amateur and Professional are not separable. Classic goals do not follow the chances (low scoring, shot conversion luck): Novato even finishes positive. Samples of 24–48 two-minute matches are small; goals need ≥ 150 matches per row to rank levels, which is cheap enough to run in F15.
* "Novato beatable", "Élite difficult but not impossible" and "no cheating" are supported (Élite loses 7–10 of 24 against Élite; all numbers are in the profile; nothing touches attributes), "Experto difficult" and "Amateur competitive" are not demonstrated by goals.
* Not done: user-vs-AI playtest (needs Unity), team-style risk (`RiskTolerance` stays 0), a tactical machine.

## 5. Reverted / did not work
* First reaction stimulus (any change of possession state, including the ball in the air) froze every player after every pass; replaced by "the team that had the ball last" (turnovers only).
* First error curve (exponent 1.5, max 0.5) left Élite at 0.2 % mistakes; changed to exponent 1.0, max 0.45.
* Effort cap on non-essential moves raised end-of-match stamina (5–10 → 12–15) but does not separate levels.

## 6. For F15
1. A ≥ 150-match-per-row run to rank levels by goals; 2. essential-sprint policy (loose-ball chasing) as its own change; 3. link shot quality to conversion in Classic; 4. coach/team-style risk into `RiskTolerance`; 5. goalkeeper difficulty (the definition has parameters, no AI uses them yet); 6. user-vs-AI playtest in Unity.
