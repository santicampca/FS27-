# Balance targets (Phase 17)

**Status: working proposals, to be confirmed by the game designer.** They are derived from the measurements of Phases 15–17 (listed under each metric), not from an outside reference, and nothing here says the game *is* balanced.
Where a metric has no defensible range yet it says so. "Investigate" is the level at which a change of code or numbers has to be looked at before anything else is built on it.

All numbers are for **full-length matches** (Arena 720 s, Classic 960 s), Core as of commit `30d5e67`, both teams of the same level unless a row says otherwise.

## How much data it takes (to conclude, not to guess)
* Goals per match: standard deviation 2.6–3.5 per match. The 95 % half-width is `1.96 · sd / √n`: 26 distinct matches give ±1.0, 40 give ±0.8, 100 give ±0.5.
* A win rate: 100 matches give about ±0.09 (95 %), 50 give ±0.13.
* Shares over thousands of events (pass completion, stamina samples, goal origin shares per goal) need far fewer matches than goals do.
* **Two teams of the same level play each seed ONCE.** Until Phase 17 the battery played every such seed twice with identical results, so every equal-level cell of Phases 15 and 16 (and of the first Phase 17 experiments) had half as many different matches as rows. Cross-level cells (Elite against Novice, level against Professional) were not affected.
* The quick battery (20 different matches per cell) is a **regression alarm**, not evidence: its bands (`Tools/Balance/compare_battery.py`) are about two standard errors of 20 matches.

## Targets
| Metric | Desired | Tolerable | Investigate | Sample to conclude | Observed (Phase 17, adopted code) |
|---|---|---|---|---|---|
| Goals per match, Arena (both teams, Professional vs Professional) | 6.0–8.0 | 5.0–9.0 | < 4.5 or > 10 | ≥ 40 different matches (±0.8) | 7.6 (seeds 6001–6040), 7.8 (7001–7040) |
| Goals per match, Classic (same) | 4.5–6.5 | 3.5–7.5 | < 3.0 or > 8.5 | same | 6.7, 6.8 (inside "tolerable", above "desired") |
| Arena goals above Classic goals | modest (0.5–2) | 0–3 | Classic above Arena by more than the noise | ≥ 100 matches per mode (difference ±1.0 at 40) | +0.9, +1.0: **not demonstrable at 40 matches** (±1.2) |
| Score of Elite against Professional | 0.60–0.75 | 0.55–0.80 | < 0.52 or > 0.85; Elite losing fewer than 10 % of matches | 100 matches (±0.09) | Phase 16 (unchanged keeper and attack, old stamina): 0.725 Arena, 0.76 Classic, Elite lost 23 % / 15 % |
| Score of Novice against Professional | 0.10–0.30 | 0.05–0.35 | wins + half the draws below 0.05, or above 0.40 | 100 matches | Phase 16: 0.14 Arena, 0.245 Classic |
| Monotone ladder Novice < Amateur < Professional < Expert < Elite (own score against Professional) | strictly increasing | one adjacent pair within the noise | any pair reversed | 100 matches per level | Phase 16: increasing in both modes |
| Share of field-player stamina samples below 5 | ≤ 10 % | ≤ 20 % | > 30 % | 20 matches are enough (tens of thousands of samples) | before 33–45 %, after **9–10 %** |
| Mean field stamina in quarters 2–4 of the match | ≥ 20 | ≥ 12 | < 10 | 20 matches | before 6–11, after **14–17** (tolerable, **not yet desired**) |
| Mean goalkeeper stamina | ≥ 90 | ≥ 80 | < 70 | 20 matches | 99.2–99.6 (Phase 16's statement that the keepers tire was wrong) |
| Goalkeeper save share (shots on target stopped) | no target before the "save" counter is corrected (see below) | – | – | 40 matches | – |
| Goals with a known origin (Shot / Rebound / Deflection / Cross) | ≥ 90 % | ≥ 75 % | < 60 % | 40 matches (several hundred goals) | Arena 73–74 %, Classic 60–65 % (**below tolerable in Classic**) |
| Goals preceded by a logged shot | ≥ 85 % | ≥ 70 % | < 60 % | 40 matches | Arena 73–74 %, Classic 60–65 % |
| Pass completion (monitored, no target yet) | – | – | a fall of more than 0.03 after a change | 20 matches (thousands of passes) | before 0.46–0.48, after 0.42–0.43 (see the report) |
| Stability between independent samples | two 40-match samples differ by less than 2·SE of the difference (about 1.2 goals) | – | more | 2 x 40 matches | 7.62 against 7.80 (Arena), 6.70 against 6.83 (Classic): inside |

## What would change these numbers
* "Desired" ranges for goals per match are about the feel of each mode; they are the designer's to set. Changing them does not need new measurements, only the table.
* The stamina targets assume the rule "running and sprinting never recover" stays. A different rule (for example some recovery while jogging) would need its own experiment.
* The goal-origin targets assume the Core's goal-attribution is left as it is; see the open problem about goals the keeper touched last in `AI_BALANCE_PHASE17.md`.
