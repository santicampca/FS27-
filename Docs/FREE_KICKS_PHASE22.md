# Phase 22 — free kicks in Classic (report)

Branch `phase-22-free-kicks` (from `phase-21-referee-cards`, `6a7ff63`). No push, no PR, no merge. Unity, graphics, scenes, models, UI and the Creator untouched. **The AI is not finished and the balance was not tuned in this phase. Nothing here says the project is ready for a phone: there is no client, no touch input and no screen; the aiming described in section 4 is a Core contract, tested without Unity.**

## 1. What existed before, and the problem

* Phase 21 gave the referee (`RefereeDecisionSystem`), `MatchController.CallFoul`, the disciplinary history (`MatchState.Discipline`, with the position of the foul) and the event `FreeKickAwarded` (team = fouled team, player = victim, other = offender). **Nobody consumed the event**: a called foul only locked the offender and the fouled team kept playing (section 7 of the Phase 21 report, problem 5).
* The restart machinery already existed and was used by kick-offs, throw-ins, goal kicks and corners: `RestartRequest {Kind, Team, Position, TakerId, Status}`, `RestartKind.FreeKick` (a contract only), `RequestRestart`, `PrepareRestart` (taker next to the ball, everybody else out of a clearance circle, no overlaps), `ConfirmRestart` (resumes play and gives the ball to the taker). While the phase is `WaitingForKickoff/WaitingForRestart` the ball is not live, the clock does not run, the decision engine answers `Idle(NotPlaying)` and `Perform` rejects every action (`MatchNotLive`).
* Authorities (unchanged): the **Core** decides every fact (`MatchController`); the referee decides *whether* a contact is a foul; the AI only proposes intents; the presentation (future) only calls `ConfirmRestart`/`ExecuteFreeKick`.
* Consumers of `FreeKickAwarded` found: none (only the producer in `CallFoul` and the Arena refusal in `GameModeRules`). No other restart code was touched except the three hooks below.

## 2. The cycle and the design decisions

```
live play ──CallFoul──► BeginFreeKick ──► WaitingForRestart (RestartKind.FreeKick, Stage Preparing)
                                              │ PrepareRestart → PrepareFreeKick  (Stage Ready, event FreeKickPrepared)
                                              │   whistle: ConfirmRestart | ExecuteFreeKick(aim) | the Core's timer
                                              ▼
                                  Playing (Stage Protected, event FreeKickTaken) ──ball played──► Stage Done
```

Decisions (all deducible from the existing code; none needed a question):
1. **The producer is the consumer.** `CallFoul` is the only place a foul is born, so it starts the free kick itself, after the card (a sent-off player is never in the wall) and after the disciplinary record. There is no event subscriber that could run twice: `FreeKickAwarded` stays published once per foul as in Phase 21, and the restart is requested once. `BeginFreeKick` only acts in live play and only when no free kick is already open (a second foul during the stoppage is still counted and recorded, but starts nothing).
2. **State is kept in a Core record**, `MatchState.FreeKick` (`FreeKickState`): beneficiary, offender, victim, reasons, index of the foul in `Discipline` (the originating infraction is not copied or changed), spot, taker, wall, stage, timers. It survives the kick (Stage `Done`) so a test or report can read it.
3. **Possession.** The fouled team is the beneficiary; the ball goes to the spot; at the whistle the ball is given to the taker of the same team, so the possession never changes team (no `PossessionChanged` is invented; tested).
4. **Spot.** The place of the foul, brought inside the lines with a margin (`SpotFieldMargin`). A foul in the goal zone of the fouling team would be a penalty (a later phase, not allowed here): the spot is moved out to `SpotMinGoalLineDistance` from the goal line, so it is an ordinary free kick, never a tap-in.
5. **Taker** (`SelectFreeKickTaker`, public, deterministic): an active field player of the fouled team (the goalkeeper only if nobody else). Near the goal (up to `TakerShotDistance`) the best shot quality (close/far blend, bonus for owning the curve skill); further away the best passer; every metre between the player and the spot costs a little. A host can still force a taker with `AssignRestartTaker`. If the taker is sent off during the wait, another is chosen.
6. **Wall** (`BuildWall`): only the defending team's field players (never the goalkeeper), the nearest to the wall line, shoulder to shoulder, **exactly `WallDistance` from the ball on the line to the NEAR post** (`WallPostCoverage` of the half goal, on the side of the ball), looking at the ball. Size by distance to the goal: 3 up to `WallThreeDistance`, 2 up to `WallTwoDistance`, 1 up to `WallMaxGoalDistance`, none further; and at least `WallMinFieldDefendersOutside` defenders always stay out. So the wall never closes the goal: the lane to the far post stays open (tested) and the goalkeeper covers the rest. Everybody else stays out of the circle (opponents `WallDistance`, the taker's team-mates the usual clearance radius); goalkeepers stay in their zone.
7. **Setup is idempotent and repeated at the whistle.** `ConfirmRestart` runs the free-kick set-up again, so a card, a substitution or a formation change during the wait never leaves a stale wall or a dismissed taker (tested: taker and a wall player sent off during the wait).
8. **Nobody touches the ball early.** The phase is not `Playing` while preparing: no ball physics, no interactions, no actions (`Perform` rejects), the clock stops. The AI answers `Idle(NotPlaying)` for every player (tested for all twelve).
9. **Resume exactly once.** The whistle is `ConfirmRestartReady` (one place); `FreeKickTaken` is published there once; a second `ConfirmRestart` returns false; a restart request of another kind closes a free kick that was still open.
10. **Never blocked.** `TickRestartWait` (Core time): the free kick prepares itself, whistles by itself after `ReadySeconds` (2 s), or `HumanMaxWaitSeconds` (12 s) when the beneficiary is human-controlled so a person can aim; and a team that still has nobody in goal (the goalkeeper was sent off on the foul) gets a goalkeeper after `KeeperVacancySeconds`: a substitute keeper if a substitution is left, otherwise the field player with the best reading of the game. Tested: goalkeeper sent off on the foul → the match resumes.
11. **Protection after the whistle.** Until the ball is played (it leaves the taker, moves `PlayedDistance`, or `ProtectionSeconds`) the opponents (goalkeeper excepted) cannot enter the wall distance (`StepFreeKickProtection`).
12. **Arena is untouched by construction**: `GameModeRules.HasFouls` is false in Arena, so `CallFoul` returns before anything; the new events are refused by the Arena rules too. Proved by a test and by the quick battery (section 8).
13. **`FreeKickTuning.Enabled`** (default true) switches the stoppage off (Phase 21 behaviour). It exists for experiments and for the referee tests that are about the call and not about the restart.

## 3. Prepare, execute, resume (files)

| piece | where |
|---|---|
| numbers: `FreeKickDefaults` + `FreeKickTuning` (const + field twin), `FreeKickState`, `FreeKickStage`, `FreeKickAim`, `FreeKickIntent`, `GoalZone`, `FreeKickExecuteResult`, `FreeKickAiming` | `Match/FreeKicks.cs` (new) |
| `BeginFreeKick`, spot, taker, `PrepareFreeKick`, wall, clearance, `TickRestartWait`, `FillVacantGoals`, `OnRestartWhistle`, `StepFreeKickProtection`, `ExecuteFreeKick` | `Match/MatchController.FreeKicks.cs` (new) |
| hooks: `CallFoul` starts it; `PrepareRestart` routes `FreeKick`; `ConfirmRestart` sets up again; `ConfirmRestartReady` calls the whistle; `Tick` runs the wait; `Advance` runs the protection; `RequestRestart` closes an open free kick | `MatchController.cs`, `MatchController.Infrastructure.cs` |
| events `FreeKickPrepared`, `FreeKickTaken` (appended), refused in Arena | `GameplayEvents.cs`, `GameModeRules.cs` |
| `MatchState.FreeKick`, `FreeKicksAwarded` | `MatchState.cs` |
| the AI's view: `WorldMatch.FreeKickTakerId` | `World/WorldTypes.cs`, `World/WorldState.cs` |
| the AI taker: `TakeFreeKick`, `DecisionReason.FreeKickShot/FreeKickPass/FreeKickClear`, `FreeKickShootRange`, `FreeKickLobPower` | `Decision/BasicDecisionEngine.cs`, `PlayerDecision.cs`, `BasicDecisionDefaults.cs` |

Every new number is a constant with its field twin; the no-loose-literal guards of Decision/, Goalkeeper/, `MatchController.Actions.cs` and `ActionModel.cs` pass; `MatchController.Actions.cs` was not touched.

## 4. Aiming with a finger (the Core contract, implemented and tested)

The future touch client sends one neutral struct, `FreeKickAim`, when the person releases the shot button. Nothing in the Core knows about screens.

* **Direction indicator.** `HasZone/Zone` (a zone of the goal: `Centre`, `Left`, `Right`, as the attacker sees them; `FreeKickAiming.GoalZonePoint`), or `HasDestination/Destination` (the dragged point, in field space; a point outside the pitch is brought inside the lines), or `Direction` (a plain drag vector). Nothing given → the middle of the goal (the default, so nothing blocks).
* **A team-mate.** `Intent = Pass` with `TargetPlayerId`, or without it: the team-mate inside `PassAimConeDegrees` of the drag is found (`FreeKickAiming.FindPassTarget`, pure). A drag into space is a pass towards that direction.
* **Power.** `Power` (0..1) or `HoldSeconds` (the time the button was held; `FreeKickAiming.PowerFromHold`: a tap is `MinPower`, `FullChargeSeconds` is full power). It goes to the existing kick as `ActionRequest.Power`, so the initial speed comes from the existing formulas and limits (tested: more power → faster ball, for shots and passes).
* **Curve.** `Curve` (-1..1) is passed to `FootballActionKind.CurvedShot` **only if the taker owns the skill** (the existing `SkillResolver.CanUseAction`); otherwise the shot is straight. No ball physics were added.
* **Lob.** `Intent = Lob` uses the existing `Chip` (a ground shot is stopped by a wall on its lane: measured).
* **Execution.** `MatchController.ExecuteFreeKick(aim)` validates the aim (NaN, infinite, unknown intent, a named team-mate who is an opponent or not on the pitch → `InvalidAim`, **nothing is whistled**), whistles, builds an `ActionRequest` (with the destination as `PassTarget.ToPosition`/`ToPlayer`, which the action system already honours) and calls the usual `Perform`. A refusal of the curve falls back to a straight shot. Results: `Executed`, `NoFreeKick` (not in a free-kick wait, Arena included), `NotReady`, `InvalidAim`, `ActionRefused`.
* **Goalkeeper and wall** react to the ball, not to the decision: the goalkeeper's AI is the existing one; the wall stands where it stands.
* Tests: the destination changes the direction (zones, dragged point, drag direction), the power changes the speed, no aim → towards the goal, invalid inputs refused without whistling, out-of-field destination clamped and finite, pass by name and by drag, lob over the wall, curve request, pure maths.
* **Not done (needs the client):** the visual indicator, the touch gestures, converting screen to field space. The contract does not claim that a phone works.

## 5. The AI taker, wall and goalkeeper

* While preparing, the AI decides nothing (`Idle(NotPlaying)`).
* After the whistle the taker (`WorldMatch.FreeKickTakerId`) plays the ball, he does not carry it: a shot up to `FreeKickShootRange`; if something stands on the lane (the wall) the shot becomes a **lob** (`Chip`, `FreeKickLobPower`); further away, the best pass; with nobody, a clearance forward. Measured over 120 free kicks in the battery: every free kick was played by its taker.
* The wall and the goalkeeper use the existing defensive AI from the moment the whistle goes; the opponents are held out of the circle by the Core for at most 2.5 s.

## 6. Extra improvements (each with problem / solution / files / tests / impact)

1. **Taker chosen by quality, not by proximity.** Problem: the default taker was the nearest player. Solution: `SelectFreeKickTaker`. Files: `MatchController.FreeKicks.cs`. Test: `FreeKick_TheTakerIsAnActiveFieldPlayer...GoodShooterNearTheGoalIsPreferred`. Impact: none outside free kicks. Risk: low.
2. **Vacant goal never blocks a restart** (free kicks only). Problem: a goalkeeper sent off makes `NeedsGoalkeeper` true and every `ConfirmRestart` returns false; before Phase 22 a red on a keeper was almost harmless because the play never stopped, now it would block. Solution: `FillVacantGoals`. Test: `FreeKick_AGoalkeeperSentOffOnTheFoul_GetsReplaced...`. Risk: low; **it is not applied to the other restarts** (see section 9).
3. **A foul in the goal zone is not a tap-in.** Solution: spot moved out of the zone. Test: `FreeKick_AFoulInTheGoalZone...`. Risk: low; a penalty is Phase 23.
4. **The taker lobs over the wall.** Problem: with the wall on the lane a ground shot at 28 m/s was always controlled by the wall (measured); no free kick could score. Solution: `TakeFreeKick`. Tests: `FreeKickAi_ANearFreeKickBehindAWall...`, `FreeKick_AGoalAfterAFreeKick...`, `Aim_ALob_GoesOverTheWall...`. Impact: measured in section 8. Risk: medium (new behaviour, small sample).
5. **Opponents kept out of the circle after the whistle** (protection). Test: `FreeKick_TheOpponentsKeepTheirDistance...`.
6. **Quick-battery diagnostics for free kicks** (columns `fkTaken, fkKicked, fkShot, fkPass, fkOther, fkGoal, fkWall` in the cell CSVs; `Docs/FREE_KICKS_PHASE22_data/fk_summary.py`). Reading only.

## 7. Improvements found and NOT implemented (proposals)

* **Advantage rule.** The referee profile `Advantage` is still unused. A safe design needs the foul record kept but the free kick deferred for ~2 s while the fouled team keeps the ball, and a rule for *when the advantage ends* (lost the ball ⇒ the free kick comes back; goal chance ⇒ play on) without a duplicated free kick. That is a rule with its own tests and balance effect; **deferred** (the foul record is already safe: `DisciplineRecord` is written first).
* **Penalty / area.** No area is defined. Fouls in the goal zone become pulled-out free kicks (above). Phase 23.
* **Players' individual set-piece roles** (second taker, someone standing over the ball, blocking runners). Needs formations for set pieces.
* **Quick free kick** (the beneficiary takes it before the wall is set) and **referee visible stoppage time**: a foul in the last seconds of a half ends the period right after the whistle, because the period clock keeps its remaining time; added time is a later phase.
* **Keeper vacancy for every restart** (generalising `FillVacantGoals` to throw-ins, goal kicks, corners). Not done: it would move Phase 7-21 behaviours outside this phase's scope.
* **Wall jumping / curve around the wall as a decision with attributes.** Needs the skill data of the real players for the shot (today the curve only applies when the taker owns the skill).

## 8. Tests and measurements

Whole suite: **2464 passed, 0 failed, 0 skipped, 7 min 8 s** (2432 before; +32 new). Before the adaptations listed below it was 2457 passed / 4 failed (the 4 are the tests listed as changed on purpose). New tests: `FreeKickTests.cs` (31, plus 1 in `RefereeDecisionTests`: stopping and the single restart; the clock and the ball stand; possession; spot inside the field and out of the goal zone; taker; wall; no wall far away; clear circles; the whistle once and no duplicated events; the Core's own whistle; human wait; second foul; protection; sent-off taker/wall; vacant goal; a free kick can be a goal; the aim (direction, power, default, pass, invalid input, curve, lob, pure maths); Arena; a Classic match without fouls; determinism; the AI idle during the preparation, the AI taker's decisions, three 5-minute Classic simulations with free kicks: no broken invariant, no action while stopped, no free kick whistled twice, every free kick prepared once).
Existing tests changed on purpose (each with the reason in the assertion):
* `Match_AFoul_...WithoutStoppingThePlay` (Referee) → now asserts the stoppage; a new test keeps the Phase 21 behaviour with `FreeKick.Enabled = false`.
* `Discipline_*` helper `Book` records fouls with the free kick switched off (the tests are about the AI's caution, not about the restart): 2 tests.
* `Classic_KeepsItsPhysicalPlay_WithFarFewerFouls`: tackle contacts > 60 → > 45 and contacts > 200 → > 170, because every foul now stops the play: measured on the same eight matches, switched off / on: tackle contacts 61 / 55, contacts 255 / 206, challenges 177 / 140, fouls 43 / 51.

Quick battery (full-length matches, 20 per cell, seeds 5001..5020; reference = Phase 21 code):
* **Arena: bit for bit identical** to Phase 21 in every one of the 214 shared metrics of the three cells (60 matches, 0 different values): no fouls, no free kicks, no walls, no pauses.
* **Both blocks of seeds** (5001..5020 and 5101..5120 = 40 matches per cell): Arena is bit for bit identical in all six Arena cells (0 different values in 25,680 compared). Classic: see 8b.

### 8b. Classic, before (Phase 21) → after (Phase 22), per match (both teams), 40 matches per cell

| metric | Professional | Elite | Novice |
|---|---|---|---|
| goals | 5.88 → **4.70** | 4.17 → 4.00 | 8.43 → 8.35 |
| fouls | 5.80 → 4.80 | 5.92 → 5.00 | 6.17 → 6.42 |
| yellow / red | 1.02 → 0.97 / 0.12 → 0.12 | 1.52 → 1.27 / 0.23 → 0.12 | 0.45 → 0.55 / 0.10 → 0.12 |
| tackle / challenge contacts | 5.1 → 4.4 / 22.0 → 19.7 | 7.5 → 7.7 / 16.9 → 15.7 | 2.6 → 2.8 / 23.9 → 24.1 |
| passes attempted (completed) | 510 (210) → 505 (208) | 577 (223) → 585 (236) | 430 (185) → 429 (186) |
| possessions (pace) | 369 → 371 | 376 → 372 | 351 → 352 |
| shots chosen / on goal | 73.0 → 76.6 / 4.50 → 3.75 | 72.7 → 75.1 / 3.15 → 3.05 | 74.2 → 72.4 / 7.45 → 7.20 |
| **free kicks** per match | 4.80 | 5.00 | 6.42 |
| free kicks played by the taker | 100 % | 100 % | 100 % |
| of them: shots / passes | 1.62 / 2.58 | 1.73 / 2.45 | 2.80 / 2.55 |
| goals within 8 s of a free kick | 0.25 | 0.17 | 0.23 |

Reading (honest): the pace of the game (possessions, passes) does not move; Elite and Novice goals do not move. **Professional scores about 20 % fewer goals (5.88 → 4.70, 40 matches, the first block alone also exceeded the quick battery's band: 4.50 against 5.85)**; its shots on goal fall 4.50 → 3.75. The likely cause is that a foul no longer lets the fouled team carry on in the middle of a transition: the play stops, the defence is set (wall, clearance) and the attack restarts from a free kick, so fewer fouled-attack moves end in a shot. This explanation is a hypothesis, **it was not isolated and nothing was tuned**. The free kicks themselves score about 0.2 goals per match, 5 % of the free kicks (one shot in six), which is in the range of a real game. Raw numbers: `Docs/FREE_KICKS_PHASE22_data/classic_before_after.txt`, the cell CSVs of both blocks and `fk_summary.py`.

## 9. Pending limitations

* The free kick is instantaneous in the simulation harnesses (they call `ConfirmRestart` at once); the preparation time only exists with a host that waits, as a real client will.
* Penalties, offside, crosses, headers, VAR, Zenit Points, persistent stats, progression: out of scope, not touched.
* Fouls inside the area are free kicks pulled out of the goal zone (no penalty).
* The vacant-goal safety net exists only for free kicks.
* A foul at the very end of a half: the half ends at the first step after the whistle; the free kick is not "extra time".
* An action in progress (a slide) at the moment of the foul continues after the whistle; no case found in the matches simulated.
* The effect on the balance (section 8b) was **not** tuned.
* Not verified: any real client, touch input, frame-rate behaviour, the Unity test assembly (the dotnet harness compiles the Core and the tests in one assembly; new APIs are public so the Unity assembly can see them; not run in Unity).

## 10. Git

Branch `phase-22-free-kicks`, created from `phase-21-referee-cards` (`6a7ff63`). Nothing pushed, no PR, no merge. Commits: see `git log` (the code and tests; the quick-battery data and this report).

## 11. Final numbers

* Whole suite: 2464 passed, 0 failed, 0 skipped, 7 min 8 s (limit 1200 s). Before: 2432 / 0 / 0, 7 min 0 s.
* Quick battery: 6 cells, 354 s (block 5001) with the limit of 1100 s per cell respected; second block also 6 OK.
* Arena identical to Phase 21; Classic effects in section 8b.
