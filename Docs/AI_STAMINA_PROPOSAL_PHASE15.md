# Proposal (separate, NOT implemented in Phase 15): the stamina of a whole match

## Data (real-length matches, both teams at the same level, 6 matches per level and mode, seeds 5001–5006)
Mean stamina of the field players by quarter of the match (0–100):

| Mode / level | Q1 | Q2 | Q3 | Q4 |
|---|---|---|---|---|
| Arena Novato | 33.4 | 7.4 | 9.2 | 6.8 |
| Arena Profesional | 34.5 | 9.3 | 10.7 | 9.5 |
| Arena Élite | 34.6 | 10.8 | 11.9 | 9.7 |
| Classic Novato | 24.3 | 6.8 | 9.9 | 5.3 |
| Classic Profesional | 25.7 | 5.9 | 9.7 | 7.2 |
| Classic Élite | 25.6 | 7.7 | 11.6 | 8.5 |

Over all the battery (300 matches per cell) the mean is 11–16 at every level. The first quarter's mean is already low, and the rest of the match is played at 5–12 %.

## Where it goes (share of the relative drain, by decision kind; Professional)
Arena: chasing the ball (MoveToBall) 50 %, interception runs 18 %, repositioning (MoveToPoint) 13 %, dribbling 9 %, closing the carrier 3 %. Classic: 56 / 18 / 10 / 9 / 2 %. Élite spends 1.5× the Professional's sprint slots (it decides more often) and Novato 0.65×.

## What Phase 15 could and could not say
* Difficulty cannot be separated from stamina by the current numbers: the effort cap of Phase 14 only touches the non-essential movements (13 % + 2 %), so the whole saving is ≈ 1.5 points of mean stamina between Novato and Élite (Arena 14.2 → 15.8, Classic 11.3 → 12.8).
* The levels' behaviour is compressed by it: when everybody plays most of the match at 7–10 %, a better rhythm or better reading has less to work with. It is a plausible reason why the top two levels cannot be told apart by result, but it was not tested (it would need a change of the cost or of the chase policy).

## Proposal for a separate phase (to be measured before any change)
1. Chase policy: the nearest player sprints for the ball only when he can get there before the opponent or when the ball is within a short distance; otherwise he jogs. (Decision layer; no change to Locomotion.)
2. If after 1 the mean stays below ≈ 40 in the first half, review the cost and recovery constants of Locomotion as their own experiment, with the F3 tests as the guard.
3. Re-run the Phase 15 battery unchanged to see whether the separation between neighbouring levels grows.
