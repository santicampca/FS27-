# Phase 21 — fouls, referee and cards (report)

Branch `phase-21-referee-cards` (from `phase-20-short-pass`, `f68ba85`). No push, no PR, no merge. Unity, graphics and the Creator untouched. **The AI is not finished and the balance was not tuned to targets: the numbers below are measured.**

## 1. Audit (what existed)
* A "foul" was one line in `MatchController.Actions.cs`/`ResolveDuel`: a **failed** duel (margin under a foul margin) **from behind** published `FoulCommitted`. Nothing else: no referee, no count per player, no card, no history.
* `MatchController.IssueCard` (yellow, red, second yellow = dismissal, `Dismiss`, the keeper replacement, a team with too few players loses) existed and was tested, but **nobody called it** during a match; only `YellowCards` was stored on the player.
* The 5 referees of the data (`RefereeProfile`: Strictness, Cards, Advantage, ...) were read by no code (two guard tests forbid the gameplay folders to know the referee data types).
* Contacts: `ResolveTackle` (standing, slide) and `ResolveChallenge` (shoulder) end in `ResolveDuel`; the carrier only keeps or loses the ball. Contact between players otherwise exists as the separation of the bodies.

## 2. Architecture created
* **`RefereeDecisionSystem`** (`Match/RefereeDecisionSystem.cs`): a **pure static function** `Decide(ContactEvidence, RefereeStyle, RefereeTuning) -> RefereeDecision {IsFoul, Card, Reasons, Severity}`. No random number, no state: the same evidence is the same decision. `RefereeDefaults`/`RefereeTuning` hold every number (twins, one copy per match: `MatchController.RefereeTuning`).
* **`ContactEvidence`** (built by the Core from the match, in `MatchController.EvidenceOf`): did he play the ball first (`TouchedBallFirst`: the duel was won or the ball knocked away), did the bodies meet (`Contact`, within `ContactDistance` 1.3 m), slide or not, from behind, closing speed relative to his top speed, the engine's own "clumsy" mark (the margin under the action's foul margin, shifted by the aggressive-tackle skill), stopping a counter-attack, a clear goal-scoring chance denied (close to goal, facing it, no outfield defender between), fouls already committed.
* **`RefereeStyle`** (`Referees/RefereeStyle.cs`): `Strictness` and `Cards` in 0..1, built by the host from a referee's `RefereeProfile` (`RefereeStyle.From(profile)`, 0..100 -> 0..1); `MatchSettings.Referee` holds it (neutral by default). The match layer only knows the style, never the referee data, so the data-layer guards still hold. A strict referee calls a foul and shows the yellow for less (thresholds scaled between 1.25 and 0.75); the other profile values (Advantage, GameControl, PenaltySensitivity, Consistency...) are for later phases.
* **`MatchController.CallFoul`** (public): counts the foul against the player (`FoulsCommitted`) and his team (`TeamMatchState.Fouls`), writes a **`DisciplineRecord`** into `MatchState.Discipline` (time, period, team, offender, victim, position, reasons, severity, card, what showing it did), publishes `FoulCommitted` and `FreeKickAwarded`, and shows the card with the existing `IssueCard`. Only where the mode has fouls (Classic); in Arena it does nothing and publishes nothing.
* **Player discipline in the match:** `MatchPlayer.YellowCards`, `RedCards` (a second yellow counts as a red, the player is dismissed), `FoulsCommitted`.
* **Events prepared for Phase 22:** `GameplayEventKind.FreeKickAwarded` (appended; team = the fouled team, player = the victim, other = the offender, detail = reasons, position of the foul), refused in Arena. **Nothing consumes it and the play is not stopped**: the fouled team keeps the ball. The foul position is in the record for the free kick.
* **`ResolveDuel`** now asks the referee first (Classic): a foul takes the ball from nobody, locks the offender like a failed tackle and may carry a card; if not a foul, the duel ends as before (won / knocked away / lost).
* **AI** (`BasicDecisionEngine`, `DefenseTuning`, `WorldPlayer.YellowCards/FoulsCommitted`): `DisciplineRiskScale`: a defender with a yellow accepts `YellowRiskScale` (0.5) of the usual risk of a shoulder challenge and never slides (`CardedNeverSlide`); with a yellow and `NearRedFouls` (2) fouls he accepts `NearRedRiskScale` (0.1) of it, i.e. only the contacts from the front or the side. A clean sheet changes nothing. Where the mode has no cards nobody has one.

## 3. Rules of the decision
Severity = late (did not play the ball first) 0.45 + from behind 0.20 + closing speed share x 0.30 + slide 0.10 + clumsy 0.25.
1. **No contact: no foul.** A challenge that **played the ball first is clean** (even at speed and from behind), unless it is a *dangerous slide* (a slide at >= 0.6 of his top speed, from behind or without the ball): that is a foul and a yellow whatever else.
2. **Foul** = contact, the ball not played first, and severity >= 0.55 (a light late brush from the front is let go).
3. **Yellow** = severity >= 0.95, or a dangerous slide, or stopping a counter-attack (the carrier's team won the ball <= 4 s ago and he runs at >= 0.7 of his top speed straight at goal), or persistent fouling (4+ fouls already).
4. **Red** = a clear goal-scoring chance denied with severity >= 0.80 (a brush of no weight is not a red), or severity >= 1.30, or a dangerous slide from behind at >= 0.85 of top speed. A second yellow is also a dismissal (existing rule). Violent conduct is not modelled.
5. The referee's style scales rules 2 and 3 (permissive: 1.25x the threshold; strict: 0.75x).
Typical cases: the clean tackle (ball first) = play on; a late tackle from behind = foul, no card; a late tackle from behind that is brisk and clumsy = yellow; the last man denying a chance with weight = red.

## 4. Tests
* **`RefereeDecisionTests`** (23, `Tests/EditMode/RefereeDecisionTests.cs`): contacts (clean tackle is not a foul, late tackle is, dangerous slide is, no contact no foul, a light late brush is let go); cards (an ordinary foul has no card, a strong foul is yellow, a counter-attack stopped and persistent fouling are yellow, the direct red for the last man with weight and for extreme danger, reds are rare: no combination without a last man or a slide gives one); the referee's style (strict calls and books sooner, built from the profile, neutral when there is none); consistency (the same evidence 1000 times is the same decision; severity grows with each aggravating factor); the match (a foul is counted, recorded, published and does not stop the play; a yellow; **the second yellow dismisses**; a direct red; Arena has no fouls; a no-foul decision is never recorded); duels in a real match (every foul is recorded once with its events and counters over 60 seeds; a tackle from behind is called more than from the front; the same match gives the same calls; Arena never calls).
* **`Discipline_*`** (7, in the AI suite): a clean-sheet defender challenges from behind as before; with a yellow he does not; close to a red he accepts only safe contacts (still challenges from the side); a booked player never slides, one without a card does; fouls without a card change nothing; no cards in Arena; the world view carries cards and fouls.
* Existing tests moved by the new definition of a foul (changed on purpose; the reason is in each assertion):
  * the fouls-as-a-minority-of-contacts tests (3): 20 % -> 50 % of the contacts (measured in full matches: about a quarter), and "under 12 fouls in 8 short matches" -> under 40 % of the contacts (it is 43; Phase 12 had 18); the diagnostics test no longer requires a foul to be from behind (about 80 % are);
  * the aggressive-tackle skill test now compares the fouls where only a clumsy challenge is a foul (the front), where he fouls more (from behind every lost duel is called for everybody);
  * the simulation harnesses count players on the pitch minus the sent-off (two harnesses);
  * `GoalOriginTracker` is public (the Phase 17 tests used an internal class: it would not compile in the Unity test assembly).
* Whole suite: see section 6.

## 5. Measurements
Quick battery (full-length matches, 20 different matches per cell, two independent blocks of seeds 5001..5020 and 5101..5120 = 40 per cell). Before = Phase 20 (`f68ba85`, the same seeds); after = this phase. A block takes about 6 minutes (4 cells at a time); no run above 300 s.
**Arena is bit for bit identical to Phase 20** (every metric of the three cells, both blocks): no fouls, no cards, nothing changed.

Classic (per match, both teams):
| metric | Professional before -> after | Elite before -> after | Novice before -> after |
|---|---|---|---|
| fouls | 0.70 -> **5.8** | 0.53 -> **5.9** | 0.78 -> **6.2** |
| yellow cards | 0 -> **1.03** | 0 -> **1.53** | 0 -> **0.45** |
| red cards (direct + second yellow) | 0 -> **0.13** | 0 -> **0.23** | 0 -> **0.10** |
| contacts (duels) | 30.0 -> 27.3 | 27.3 -> 24.6 | 29.0 -> 26.5 |
| goals | 5.43 -> 5.88 | 3.85 -> 4.18 | 8.43 -> 8.43 |
| passes attempted | 515 -> 510 | 595 -> 577 | 431 -> 430 |
| pass completion | 0.413 -> 0.413 | 0.385 -> 0.387 | 0.433 -> 0.432 |
| possessions (pace) | 372 -> 369 | 380 -> 376 | 352 -> 351 |
| shots chosen | 73.5 -> 73.0 | 72.5 -> 72.7 | 73.5 -> 74.2 |
| clear chances | 63.2 -> 62.1 | 75.4 -> 77.9 | 59.5 -> 60.5 |
The pace of the game (possessions, passes, shots, chances) does not move, because the play is not stopped yet; the contacts fall by about 2.7 per match (a called foul ends the duel with the ball kept by the fouled team).
Ten-match logs of every call (`Docs/REFEREE_CARDS_PHASE21_data/discipline_log_*.csv`): Professional 66 fouls, 54 without card, 11 yellow, 1 red; Elite 66 fouls, 43 without card, 21 yellow, 2 direct red and 1 second yellow. Reasons (Professional): late 66, from behind 52, reckless 17, stopping a counter 8, clear chance denied 6, dangerous slide 1, persistent 1.
**Does the AI understand the cards?** (Classic, seeds 5001..5020, with and without the caution rules): Professional: yellows 1.30 -> 1.00, reds 0.20 -> 0.05; Elite: yellows 1.65 -> 1.95, reds 0.20 -> 0.20, slides 0.75 -> 0.80. **The effect is visible for Professional and absent for Elite in 20 matches**: the caution only touches shoulder challenges and slides; fouls also come from standing tackles lost, and from players of any role.

## 6. Tests and time
Whole suite: **2432 passed, 0 failed, 0 skipped, 7 min 0 s** (2402 before; +30 new: 23 `RefereeDecisionTests` and 7 `Discipline_` tests; the explicit tools are not counted). The quick battery: 354 s and 350 s for the two blocks; the longest single run of the phase was under 300 s; nothing came near the 20-minute limit.

## 7. Problems found
1. **Elite expels a player every 4-5 matches** (0.23 reds per match; 0.13 for Professional; the real game is around 0.1 or less): Elite plays the most challenges and is the most aggressive. Not tuned (asked), listed for the balance.
2. **Fouls are concentrated early in a match** (about 2.7 per minute in the first two minutes of an unprofiled team, 0.4 per minute over a full match): fresh players challenge a lot. With the free kick in Phase 22 the first minutes would stop and start; to look at then.
3. **The caution of the AI is weak for Elite** (above). The AI does not yet reason about the clock, the score or a teammate's card.
4. The referee can be neither wrong nor lenient on a whim: the same action is always the same call (a requirement here). A "human error" model (consistency in the referee profile) is for later.
5. **A foul is not yet a stoppage**: the offender is locked as in a lost tackle and the fouled team keeps the ball. No advantage rule, no restart, no wall.
6. Violent conduct, handball and the penalty area are not modelled; the position of the foul is recorded for Phase 23.
7. The sent-off player simply leaves; the formation positions of the remaining four are produced by the existing placement; not looked at in detail.
8. The keeper never fouls in the simulated matches (the AI keeper does not challenge); the direct red for a keeper is only tested through `CallFoul`.

## 8. For Phase 22 (free kicks)
* Consume `FreeKickAwarded` (team = the fouled one, position = `DisciplineRecord.Position`, victim and offender in the event); stop the play (`WaitingForRestart`, `RestartKind.FreeKick` exists as a contract); place the ball, the taker, the wall (the Core already has the restart-request flow of throw-ins and corners).
* Decide the advantage rule (the `Advantage` value of the referee profile is unused) and what happens to the offender's `ActionLock`.
* Revisit the foul rate (problem 2) once the play stops, and the reds of Elite (problem 1).
* Fouls in the area become a penalty in Phase 23: the `DisciplineRecord` has the position; the area is not defined yet.
