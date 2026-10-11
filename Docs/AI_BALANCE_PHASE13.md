# Phase 13 — Balance of decisions, passes and defence by mode

Branch `phase-13-ai-balance` (from `cd3337c`, baseline 2275/2275). Same sixteen matches as Phase 12 (Arena seeds 301–308, Classic 321–328, 120 s each).
Core remains the authority; the AI only produces intents. Every new number lives in a `*Defaults`/`*Tuning` pair (and, when it differs by mode, in a `*ModeDefaults` class named `Arena<Field>` / `Classic<Field>`), applied through `ForMode`. Arena and Classic share one engine; only parameters differ.

## What changed

| # | Change | Where | Arena | Classic |
|---|---|---|---|---|
| 2a | Dribble space graded over a longer look-ahead (`DribbleSpaceHorizon`) | BallCarrier | 3.5 m (as before) | 5.0 m |
| 2b | Safe pass gets a base value even without pressure (`SafePassBaseWeight`) | BallCarrier | 0.4 | 0.8 |
| 3 | The passer does not chase his own pass (`PasserStaysOffHisPass`, `DecisionReason.AfterPass`) | Basic | on | on |
| 4 | A through pass is not aimed into the opposing keeper's reach (`ThroughKeeperMargin`) | BallCarrier | 2.5 m | 2.5 m |
| 5 | Classic challenge risk: from behind, shielding carrier, fast closing (`ChallengeMaxRisk`, `ChallengeShieldRisk`, `ChallengeClosingRisk`) | Defense | off (1000) | 1.2 |
| 6 | Weak shots are not taken (`ShootMinScore`); range unchanged | BallCarrier | 0 (off) | 2.0 |

Reverted / not done, with the reason:
* Through-pass power (4b): no improvement in completion; removed completely, fingerprints equal to 4a.
* Lower `PositionMagnitude` for stamina (0.4/0.3): the stamina curve did not move; rejected. Stamina is left unchanged (see F14).
* Shoot range: shots from further convert worse; left at 11 m.

## Results (same 16 matches, Phase 12 → Phase 13)

| Metric | Arena F12 → F13 | Classic F12 → F13 |
|---|---|---|
| Pass share of carrier decisions (Pass+Forward+Through) | 22.8 % → 29.2 % | 22.6 % → 32.2 % |
| Dribble share | 49.5 % → 49.3 % | 58.1 % → 46.2 % |
| Passes attempted / completed | 675 / 204 → 782 / 278 | 669 / 224 → 811 / 243 |
| Passer chased his own pass (completed passes) | 150/204 → 34/278 | 179/224 → 40/243 |
| Passes into space: attempts / completed | 23 / 3 → 21 / 12 | 26 / 4 → 12 / 3 |
| Passes ending in the opposing keeper's hands (into space) | 11 → 0 | 4 → 1 |
| Shots chosen / goals | 64 / 4 → 37 / 6 | 54 / 10 → 40 / 2 |
| Contacts | 449 → 379 | 304 → 302 |
| Interceptions (events) | 254 → 311 | 199 → 241 |
| Sliding tackles | 13 → 23 | 13 → 16 |
| Fouls | 0 → 0 | 18 → 5 |
| Challenge contacts / fouls | 312 / 0 → 267 / 0 | 212 / 17 → 185 / 3 |

## Honest reading

Better: passes are completed more in Arena (30 → 36 %), the passer no longer runs after his own pass, through passes no longer end in the keeper's hands, Classic fouls fell from 18 to 5 while its physical challenge stays (185 contacts), and Classic shares between pass and dribble look less one-sided.

Worse or unresolved:
* Arena contacts fell 16 % (449 → 379) as a side effect of the passer staying back and fewer shield contacts; recoveries, interceptions and slides rose, so the defence is not softer, but it is not identical either.
* Classic goals 10 → 2 and shots 54 → 40: `ShootMinScore = 2.0` made Classic more selective. The samples are small (eight matches); it must be re-measured on a wider run before keeping or relaxing it.
* Classic pass completion is flat (33 % → 30 %).
* Passes returned to the passer within 0.1 s remain frequent: that comes from the Core's pass/first-touch resolution, not from the AI's choice.
* Stamina collapses over 120 s because of the sprint-range decisions; not changed here.

Nothing here makes the AI "finished" or the game "balanced".

## Tests

`AiBalanceTests` (24 new tests) cover mode-number isolation, Classic challenge scenarios (front, side, behind, fast, slow, shielding, not shielding), Arena never held back, after-pass, pass-vs-dribble by mode, dribble horizon, pass-to-space and keeper rejection, shot selectivity, matches by mode, determinism. Three Phase 11/12 tests were adapted on purpose: the old fingerprints are reproduced with the Phase 13 switches off; zero is a legal "off" value in the carrier defaults; two scenes moved their runner away from the keeper.

## F14 recommendation

Re-measure Classic shot selectivity on a wider sample; study the stamina curve (sprint decisions); investigate the Core's pass return to the passer; add explicit Arena aggression only if the contact drop proves harmful.
