# Phase 18 — goal-line detection (report)

Base: `phase-17-match-balance-validation` at `6db74c1` (checked at the start: clean tree, 2375 tests in the Phase 17 report). Branch `phase-18-goal-line-detection`. Code commit `e7d972f` (2387 tests, all green). No push, no PR, no merge. Unity not run.

## 1. Root cause
**The geometry of the goal line was already right; the false goals did not come from ignoring the ball's radius.**
* There is one authority for goals: `MatchController.TickPlaying` → `rules.JudgeBall` → `MatchRules.Evaluate`. A goal needs `|x| − radius > half the length` (the whole ball beyond the line), `|z| ≤ half the goal width` and `y − radius ≤ goal height`. The radius comes from `BallParameters.Radius` (0.2) through `BallObservation`. Field 40 x 25, goal 5 x 2, ball radius 0.2 (the plane is crossed completely when the centre is beyond 20.2). Players may stand up to 1 m behind the line (`PitchBounds`).
* **What actually happens** (reproduced and traced on Arena seeds 6001–6003, `Docs/AI_GOAL_LINE_PHASE18_data/trace_goals_before_fix_seeds6001-6003_arena.csv`, 1.5 s before each goal, one row per step): a long pass reaches the keeper, he catches it **in front of the line** (ball centre at x = 17.9, 18.7 or 19.7; the line is at 20) and the ball becomes `Controlled` (possession, carried like a dribble: `StepDribble` keeps it at his feet). He is still moving backwards (the run to his stand point plus the momentum), so he walks into the goal and **takes the ball with him**: 2.5 m in 0.3 s in one traced case, the keeper ending at x = 21 (the limit behind the line). The ball is then completely over the line, `JudgeBall` correctly says "goal", and because the keeper was the last toucher the Core flags it as an **own goal**.
* So the judgement was correct for the ball it was given; what was wrong is that a held ball could be carried over the line. The second path, in the same place, is a ball that crosses the line completely in a physics step and is "caught" by the keeper in the same step (the touch is processed before the judgement): the goal would be credited to the right team but flagged as an own goal of the keeper. It was not seen in the traces; it is closed by the same rule and tested.
* Size of the problem (Phase 17 quick-battery reference, 120 matches): **144 of 948 goals (15 %)** were a keeper catch followed by an own goal; in the Phase 17 level-3 samples 16–27 % of the goals. It already existed with the Phase 15 keeper.

## 2. Solution (small, in the Core, one switch)
`ActionTuning.KeeperHoldsBallOnThePitch` (`ActionDefaults`, 1 = on, 0 = the behaviour before):
1. `MatchRules.HasCrossedGoalLine(field, ball)` — the crossing test, extracted from `Evaluate` without changing it, so that there is still ONE authority (the judgement and the keeper's hands use the same function).
2. `StepBallInteractions`: a loose ball that has already crossed the line completely cannot be touched by anybody (no catch, no deflection after the fact): it is a goal, credited to the last touch that was before the crossing.
3. `StepDribble` → `KeepHeldBallOnThePitch`: while a goalkeeper holds the ball, its centre is kept at most at `half length − radius` (touching the line, never beyond it) and the outward velocity is cancelled. The keeper's body may still go behind the line.
The goal rule itself (`Evaluate`), Arena and Classic rules, restarts, the dribbling of field players (a field player can still walk the ball in) and the shots/saves of the keeper are untouched.

**Interpretation to confirm.** In the laws of football a keeper who carries the ball over his own line concedes a goal. Rule 4 of the request ("a keeper who catches or controls the ball on the line, in front of it or without a complete crossing must not cause a false goal") and the finding that these were 15 % of the goals led to stopping the carry-over. If the designer prefers the real-law rule, setting `KeeperHoldsBallOnThePitch = 0` restores the previous behaviour (a test pins that behaviour as the documented cause).

## 3. Tests
New, `Assets/_Project/Tests/EditMode/GoalLineTests.cs` (12; partial class `ActionsAndBallTests`, whose helpers they reuse), deterministic:
* Rules: completely beyond = goal at both ends and for both directions of attack (side switch); touching the line / partly across / centre on the line = no goal at both ends; completely across but wide of either post or over the bar = no goal; the two modes judge the same crossing and keep their own rules for the rest (Arena has no "out", Classic gives out of play); the crossing function is the one the judgement uses.
* Match: a shot into the empty goal is exactly one goal in Arena and in Classic, not an own goal; the same at the other end for Away; an own goal still counts when the ball really crosses (defender's last touch); **a keeper who catches in front of the line and keeps going back cannot carry the ball over it (Arena and Classic, ground and lofted ball)**; a ball already completely over the line cannot be played by the keeper, so the goal is the shooter's (Arena and Classic, counted once); the catch of a slow shot inside the area works as before; with the rule off the keeper carries the ball over and the Core calls it an own goal (documents the cause).
Before the fix the two tests for the carry and for the already-crossed ball failed (carry: 1 goal instead of 0; already crossed: flagged as own goal); the other 9 passed (the geometry was right). After: 12 of 12.
Whole suite: **2387 passed, 0 failed, 0 skipped** (was 2375), 5 min 1 s (run twice, 4 min 3 s to 5 min 1 s); the focused run of the 12 new tests takes under a second. No test was changed in its expectations.
Test tooling added (no behaviour): a step hook in the attack simulation (`StepHook`), the explicit tool `Phase18Tool_TraceGoals` (writes the last 1.5 s before every goal), and the word `partial` on `ActionsAndBallTests`.

## 4. Effect in simulated matches (quick battery, 6 cells x 20 different matches, seeds 5001–5020, commit `e7d972f`, 294 s in total, longest cell 234 s)
| | Phase 17 reference (before) | Phase 18 (after) |
|---|---|---|
| goals in the 120 matches | 948 | 798 (−150, about −1.25 per match) |
| own goals right after a keeper catch | 144 | **0** |
| goals flagged as own goals | 179 | 31 |
| goals with a known origin (Shot / Rebound / Deflection) | 70 % | 87 % (Arena Professional 0.73 → 0.90, Classic Professional 0.65 → 0.80) |
| goals per match, Arena Professional | 8.35 | 6.70 |
| goals per match, Classic Professional | 7.10 | 5.60 |
| stamina, keeper stamina, pass completion, mistakes | – | unchanged (inside the bands) |
The goals fall because the false goals were being counted; **this is a consequence of the rule, not a balance change** (no AI, stamina, attribute, animation, control or duration number was touched). The 20-match samples are noisy (±1.2 goals), so the exact size is not established here. The new reference of the battery is `Docs/AI_GOAL_LINE_PHASE18_data/fast_reference_phase18.json` (commit `e7d972f`); the Phase 17 reference no longer applies to goals.
The remaining goals with an unknown origin (110 of 798) are mostly long passes that roll into the goal with no shot, controls and interceptions at the line.

## 5. Pending problems and limits
* The lateral test uses the centre of the ball (|z| ≤ half the goal width), as before; the radius is used for the plane of the goal line, not for the posts. A ball that is partly beyond a post is judged by its centre. This was not changed (it would be a new rule).
* Earlier statistics are not comparable: the Phase 16 save shares, the goals per match and the origin shares of Phases 16–17, and the "desired / observed" columns of `AI_BALANCE_TARGETS_PHASE17.md` (observed goals) were measured with these false goals. They should be re-measured (the xG was never recalibrated, as before).
* The keeper counter of saves in the diagnostics counted those catches as saves; after the fix that error no longer occurs.
* The interpretation in 2 needs the designer's confirmation.
* Unity, VAR, review requests, red cards, penalties and Zenit Points: not touched.

## 6. Git and scope
Base `6db74c1`; code commit `e7d972f`; this report and the data are in the following commit. Files changed in the code commit: `Scripts/Core/Match/MatchRules.cs`, `Scripts/Core/Match/MatchController.Actions.cs`, `Scripts/Core/Actions/ActionDefaults.cs` (the fix), `Tests/EditMode/GoalLineTests.cs`, `AiPhase18Tools.cs`, `AiAttackSimulationTests.cs`, `ActionsAndBallTests.cs` (tests and tools, plus their `.meta`). **Nothing outside the scope was modified:** no change to the AI, stamina, attributes, animations, controls, match durations, the rules of Arena and Classic apart from what is described, or the goal rule itself.
