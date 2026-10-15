# Goalkeeper ScenarioBench (Phase 29)

What it is: the keeper with the ball in his hands, measured on the **real Core**. Each run is a real `MatchController` (Classic, real teams 3 vs 8 and their attributes, the real `BasicDecisionDriver` deciding for all twelve players, the controller's physics and rules). The bench only places the twelve players, gives the ball to the Home keeper and lets the Core play up to 9 s. Nothing decides for the keeper: the action, its instant and its reason are read from the controller's `ActionPerformed` event and the driver's last decision (`TryGetLast`). A guard test forbids the bench from calling the keeper's rule itself.

Run: `SCEN_OUT=/tmp/bench.csv SCEN_SEEDS=8 dotnet test Tests/Core.Tests --no-build --filter "Name=ScenarioBench_Goalkeeper_WritesTheCsv"` (about 6 s). Automatic tests (`ScenarioBench_Goalkeeper_*`): every scenario x configuration x seed is present with every column; the CSV is identical when played twice; rows agree with the real Core (switched off he never waits, a release is a real `KeeperDistribute` decision, instants are the controller's clock, he never holds the ball for the whole run).

Configurations: `DistributionEnabled` on / off (off = Phase 28 behaviour: play at once to the best receiver, else clear). Same seeds, same starting positions in both (the seed moves the receiver sideways by a fixed amount, -1..+1 m, and seeds the controller's noise). Difficulty of the Away team is always Professional.

CSV columns: scenario, variant, seed, distributionEnabled, homeLevel, levelDecisionMaking (difficulty parameter), keeperId, keeperCommand/Kicking/Distribution (his attributes), skillDecisionMaking (`GoalkeeperSkillModel`, the Core function the engine uses), nearestRivalToKeeper / ToReceiver (m, at the start), released, releaseSeconds, action, isLong, targetId, reason (`DecisionReason` of the real decision that released the ball), waitDecisions (driver decisions that were `KeeperWaitForLane`), ballOwnerAt3s and retainedAt3s (3 s after the start), ballOwnerAfterRelease3s and retainedAfterRelease3s (3 s after the release), finalPhase.
`retained` is derived from Core state: Home owns the ball, or nobody does and the last touch was Home's (it is on its way to a team-mate); `no` = Away owns it or Away touched it last; `stopped` = dead ball (goal, out of play). `NA` = the horizon was not reached or the ball was not released.

## Scenarios: expected and suspect results (nothing is forced; the logic does not guarantee these)

| Scenario | Scene | Expected to observe | Would point to a defect |
|---|---|---|---|
| S1 FreeReceiver | one team-mate in range, nobody near | on and off release at the first decision with a short pass to him; few or no wait decisions | on waits long with a free team-mate; clears instead of passing |
| S2 CoveredReceiver | the same team-mate with a rival at 0.5 m; nobody near the keeper | off: long blind kick at once. on: waits for a lane, then clears; the wait is the patience of his decision making | on releases at once into the covered receiver; waits past the patience; keeper loses the ball while waiting with no rival near |
| S3 Rival3m | covered receiver + a rival 3 m in front of the keeper | on and off the same: hurried, he clears at the first decision (no waiting) | on waits with a rival at 3 m |
| S4 Rival7m | covered receiver + a rival 7 m in front | on: waits while the rival is far, then clears when he comes within 5 m; off: clears at once | on waits until he is tackled; clears with the rival still far and no wait |
| S5 DecisionMaking | S2 with difficulty goalkeeper decision making 0.9 vs 0.2 | on: the high one waits longer (more wait decisions, later release) than the low one; off: no difference | high waits less than low (inverted); off differs between them |
| S6 Difficulty | S2 with Novice vs Elite | on: Elite waits at least as long as Novice (decision making 0.9 vs 0.3); off: release at the first decision of each level (a Novice decides less often) | Novice waits longer than Elite in the same scene |

## What the first run showed (8 seeds, Classic, commit with this file; compare with the CSV, not with these words)

| Group | mean release s | mean wait decisions | retained / lost / dead ball at 3 s |
|---|---|---|---|
| S1 on / off | 0.283 / 0.283 | 15.8 / 0 | 8-0-0 / 8-0-0 |
| S2 on / off | 1.577 / 0.283 | 77.6 / 0 | 8-0-0 / 3-5-0 |
| S3 on / off | 0.283 / 0.283 | 0 / 0 | 3-0-5 / 3-0-5 |
| S4 on / off | 0.883 / 0.283 | 36.0 / 0 | 1-2-5 / 4-0-4 |
| S5 high on / low on | 1.577 / 1.333 | 77.6 / 63.0 | 8-0-0 / 5-3-0 |
| S6 Novice on / Elite on | 1.617 / 1.558 | 56.0 / 82.5 | 6-2-0 / 8-0-0 |
| S6 Novice off / Elite off | 0.683 / 0.183 | 0 / 0 | 5-3-0 / 2-5-1 |

Findings to review (not fixed here, the task forbids changing the rule):
- S1: with the distribution on he still lets 15.8 decisions pass (0.28 s) before releasing a pass to a free team-mate; the release instant is the same as off.
- S2/S5: waiting for a lane kept the ball at 3 s in 8/8 against 3/8 playing at once; high decision making waits longer than low, as expected. Eight seeds of a deterministic AI give little independent variation: this is a direction, not a proof.
- S4: with the distribution on, 4/8 runs end in a goal within 3 s (1/8 off): the blind clearance (receiver -1, straight along the pitch) reaches the far goal. Possible defect of the clearance (direction/power), outside Stage 1.
- S6: Novice is NOT more impatient than Elite (release 1.617 s vs 1.558 s): the release instant is dominated by the level's decision interval and by the rival's approach, not only by patience. Possible defect or intended; to review in Stage 2.

## Limitations
- One pair of teams, one keeper (`gk-emiliano-martinez`, Command 0.867, Kicking 0.796, Distribution 0.786) and Classic only; Arena is not measured.
- Few seeds, and the seed only moves a receiver by a fixed lateral amount plus the controller's noise.
- A 3 s horizon mixes the pass in flight, the clearance and dead balls (`stopped`); `retained` is a coarse derived label.
- The keeper's reason is a `DecisionReason` (the Core gives no numeric chance or utility for the choice): the chance `GoalkeeperDistribution` computed is not exposed and is not invented here.
- The rest of the players move with the real AI, so after the first second the scene is no longer controlled; the early columns (release, action, reason, waits) are the controlled ones.
