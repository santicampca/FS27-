# Phase 20 — the short pass (report)

Branch `phase-20-short-pass` (from `phase-19-project-audit`, `2cd85ed`). No push, no PR, no merge. Unity, graphics and the Creator untouched. **The AI is not finished and the balance was not tuned: this phase measured first.**

## 1. Cause (confirmed in the code)
* `MatchController.Actions.cs`, `ResolveKick`: the `switch` that rates a kick had a `case` for `WallPass`, `ThroughPass`, `LongPass`/`SkillLobPass`, `Chip`, `Shot`/`CurvedShot` and `Clearance`, and a `default` for the goalkeeper's distribution. **`ShortPass` had no case and landed in the `default`.** There its quality was `KeeperShortDistributionShare x Distribution + ... x Kicking` of the *emergency-keeper* profile (`EmergencyKeeperProfile.Derive`, every capability multiplied by 0.75), not the player's `PassQuality`.
* Measured before the fix (`Phase19Probe_PassQualityByKind`, same player, no rivals): attributes 80 -> short pass 0.602, any other pass 0.806; 95 -> 0.714 against 0.959; 40 -> 0.296 against 0.398. A fixed 25 % penalty, for the human's button B (tap) and for every ordinary pass of the AI (both reach the same `Resolve`).
* The skill code already treated the short pass as an ordinary pass (`KickSkillsOf` gives `BuildUpPass` and `ProgressivePass` bonuses to it): the missing `case` was an oversight, not a design.

## 2. Change
1. **`ShortPass` has its own case** with the same numbers as the other passes: `PassQuality`, `PassMinSpeed/PassMaxSpeed`, no loft, `PassMaxError`, `PassStaminaCost`. After the fix every kind of pass of the same player has the same quality (80 -> 0.806, 95 -> 0.959, 40 -> 0.398); the `default` is now only the goalkeeper's distribution.
2. **A pass loses quality when rivals are close** (new, requested by the design rules; it did not exist for any kind of pass). `ActionDefaults.PassPressureEffect` = 0.125: `quality x (1 - effect x pressure)`, where the pressure (0..2) is the sum of the two nearest opponents inside the existing `PressureRadius` (3 m), each counting 1 at his feet and 0 at the radius, reduced by the player's own press-resistance skill. Half of the dribble's `PressureEffect`. It applies to short, wall, through, long and lob passes, **not** to shots or to the goalkeeper's distribution. Plain proximity (not "the rival asked for the Pressure action"), because the AI closes down by moving. 0 switches it off.
3. **One model for everybody:** the person's pass and the AI's both go through `Resolve`; there is no formula for the CPU. Orientation is the existing foot rule (a pass across the body to the weak side uses the weak foot, `WeakFootAngleDegrees`/`WeakFootFloor`); the receiver is the decision layer's `ReceiverScore` (rivals near him and in the lane lower his value), unchanged.
Files: `Scripts/Core/Match/MatchController.Actions.cs`, `Scripts/Core/Actions/ActionDefaults.cs` (the fix); tests and tools below; `Tools/Balance/fast_battery.sh` (a `FAST_FIRST` variable to choose the block of seeds).

## 3. Tests
New suite **`AiShortPassTests`** (15 tests, `Tests/EditMode/AiShortPassTests.cs`, its own scenery): higher passing -> better short pass; no fixed penalty (equal quality to wall/through/long at four levels of attributes, and well above 0.9 of the long pass); a passer with 85 is above 0.8 and below 1 (high quality, not automatic perfection); the short pass keeps its speed and no loft; a free passer passes better than a pressed one, a rival beyond the radius changes nothing, two rivals take more than one, a closer rival more than a farther one, it never makes the pass hopeless; pressure applies to every pass kind and not to shots; a bad orientation (a pass across the body to the weak side with a poor weak foot) lowers the quality, a better weak foot recovers it; a poorer passer's direction error is larger; a marked receiver is worth less than a free one (`ReceiverScore`); the person's pass and the AI's are rated the same and leave the same ball; the button B mapping gives a short pass rated like a wall pass; the long pass, through ball, wall pass and shot keep their ratings; the goalkeeper's distribution is unchanged; a short pass plays in Arena and in Classic.
Run against the code *before* the fix: 6 of the 15 fail (no fixed penalty, the 85 case, the three pressure tests, the button B), the other 9 pass: the suite detects the defect.
**Existing tests that the change moved (changed on purpose; reasons are in the assertions):**
* `TheRefactoredScores_ReproduceThePhase11Matches_BitForBit…`: re-pinned (`2212894415`/`3998311449` -> `4242571587`/`34388847`). The short pass and the pressure on passes are a correction of the kick itself, not a decision-layer switch, so the Phase 11 matches can no longer be reproduced; the test now anchors the refactor on the Phase 20 matches.
* `ThePassesIntoSpace_ReachTheirRunnerFarMoreOften…`: it read the share from the eight short matches (about 26 attempts: 5 of 26 = 0.19 here, and wide swings on neighbouring runs). It now reads 24 seeds per mode (about 48 matches, 70 s more in the suite) with a floor of 0.18 (Phase 12: 0.14). Measured over seeds 301..340: Arena 0.33 (25 of 75; 0.25 before the fix), Classic 0.15 (10 of 65; 0.12 before), together 0.25 (0.20 before). The share of passes into space improves a little; the old criterion was noise around its threshold.
Whole suite: 2387 -> **2402 tests** (+15 new; the probes are explicit and not counted), **all passed, 0 failed, 0 skipped**.

## 4. Measurements (quick battery, full-length matches, 20 different matches per cell, two independent blocks of seeds: 5001..5020 and 5101..5120, so 40 per cell)
Before = commit `2cd85ed` (the code of the Phase 18/19). After = this phase's code. Same seeds, same configuration; the Core is deterministic. Times: 361-375 s for the six cells of a block (4 at a time), longest cell 295 s; no run above the 1100 s limit; nothing incomplete.
Noise: goals per match have a standard deviation of 2.4-3.5, so with 40 matches the half-width is about 0.8-1.0 goals; shares over thousands of passes are much more stable (about 0.01).

### Arena_Elite_vs_Elite (n=40 matches each)
| metric | before (2cd85ed) | after (final) | change |
|---|---|---|---|
| goals/match | 4.425 | 4.850 | +0.425 |
| passes | 405.100 | 413.250 | +8.150 |
| passes completed | 168.600 | 169.400 | +0.800 |
| completion | 0.416 | 0.410 | -0.006 |
| clear chances | 97.675 | 93.000 | -4.675 |
| shots chosen | 61.625 | 60.350 | -1.275 |
| shots on target | 31.925 | 31.900 | -0.025 |
| possessions | 239.650 | 249.875 | +10.225 |
| losses | 238.650 | 248.875 | +10.225 |
| mistakes/carrier decision | 0.007 | 0.007 | +0.000 |
| fouls | 0.000 | 0.000 | +0.000 |
| tackles | 6.825 | 7.775 | +0.950 |

### Arena_Novice_vs_Novice (n=40 matches each)
| metric | before (2cd85ed) | after (final) | change |
|---|---|---|---|
| goals/match | 9.775 | 9.575 | -0.200 |
| passes | 277.725 | 275.550 | -2.175 |
| passes completed | 133.250 | 124.350 | -8.900 |
| completion | 0.480 | 0.451 | -0.028 |
| clear chances | 48.025 | 47.000 | -1.025 |
| shots chosen | 56.500 | 56.425 | -0.075 |
| shots on target | 29.200 | 27.775 | -1.425 |
| possessions | 180.625 | 188.550 | +7.925 |
| losses | 179.625 | 187.550 | +7.925 |
| mistakes/carrier decision | 0.159 | 0.158 | -0.001 |
| fouls | 0.000 | 0.000 | +0.000 |
| tackles | 3.450 | 3.725 | +0.275 |

### Arena_Professional_vs_Professional (n=40 matches each)
| metric | before (2cd85ed) | after (final) | change |
|---|---|---|---|
| goals/match | 6.625 | 5.950 | -0.675 |
| passes | 350.125 | 340.250 | -9.875 |
| passes completed | 149.875 | 143.700 | -6.175 |
| completion | 0.428 | 0.422 | -0.006 |
| clear chances | 62.350 | 63.700 | +1.350 |
| shots chosen | 58.550 | 59.900 | +1.350 |
| shots on target | 29.375 | 29.575 | +0.200 |
| possessions | 219.900 | 219.225 | -0.675 |
| losses | 218.900 | 218.225 | -0.675 |
| mistakes/carrier decision | 0.058 | 0.058 | +0.000 |
| fouls | 0.000 | 0.000 | +0.000 |
| tackles | 5.375 | 5.550 | +0.175 |

### Classic_Elite_vs_Elite (n=40 matches each)
| metric | before (2cd85ed) | after (final) | change |
|---|---|---|---|
| goals/match | 4.800 | 3.850 | -0.950 |
| passes | 600.625 | 594.650 | -5.975 |
| passes completed | 242.000 | 229.100 | -12.900 |
| completion | 0.403 | 0.385 | -0.018 |
| clear chances | 86.575 | 75.425 | -11.150 |
| shots chosen | 72.800 | 72.475 | -0.325 |
| shots on target | 26.500 | 26.400 | -0.100 |
| possessions | 379.625 | 380.350 | +0.725 |
| losses | 378.625 | 379.350 | +0.725 |
| mistakes/carrier decision | 0.006 | 0.006 | -0.000 |
| fouls | 0.675 | 0.525 | -0.150 |
| tackles | 7.625 | 8.575 | +0.950 |

### Classic_Novice_vs_Novice (n=40 matches each)
| metric | before (2cd85ed) | after (final) | change |
|---|---|---|---|
| goals/match | 7.725 | 8.425 | +0.700 |
| passes | 432.775 | 431.050 | -1.725 |
| passes completed | 184.100 | 186.600 | +2.500 |
| completion | 0.425 | 0.433 | +0.008 |
| clear chances | 58.325 | 59.475 | +1.150 |
| shots chosen | 72.775 | 73.450 | +0.675 |
| shots on target | 25.100 | 27.175 | +2.075 |
| possessions | 358.275 | 351.625 | -6.650 |
| losses | 357.275 | 350.625 | -6.650 |
| mistakes/carrier decision | 0.144 | 0.148 | +0.003 |
| fouls | 0.700 | 0.775 | +0.075 |
| tackles | 3.175 | 2.800 | -0.375 |

### Classic_Professional_vs_Professional (n=40 matches each)
| metric | before (2cd85ed) | after (final) | change |
|---|---|---|---|
| goals/match | 5.800 | 5.425 | -0.375 |
| passes | 523.850 | 515.225 | -8.625 |
| passes completed | 219.975 | 212.875 | -7.100 |
| completion | 0.420 | 0.413 | -0.007 |
| clear chances | 60.625 | 63.150 | +2.525 |
| shots chosen | 73.500 | 73.475 | -0.025 |
| shots on target | 27.650 | 25.675 | -1.975 |
| possessions | 370.575 | 371.925 | +1.350 |
| losses | 369.575 | 370.925 | +1.350 |
| mistakes/carrier decision | 0.051 | 0.052 | +0.002 |
| fouls | 0.675 | 0.700 | +0.025 |
| tackles | 5.225 | 5.275 | +0.050 |



### What the short-pass fix alone does (seeds 5001..5020, pass completion)
| cell | before | fix only | fix + pressure (final) |
|---|---|---|---|
| Arena Professional | 0.431 | 0.436 | 0.425 |
| Classic Professional | 0.420 | 0.409 | 0.416 |
| Arena Novice | 0.485 | 0.457 | 0.458 |
| Classic Novice | 0.426 | 0.434 | 0.428 |
| Arena Elite | 0.425 | 0.397 | 0.408 |
| Classic Elite | 0.403 | 0.398 | 0.388 |

## 5. Reading (honest)
* **The artificial penalty is gone.** The short pass is rated like the others (0.806 at 80, 0.959 at 95) and the button B no longer plays with 75 % of the player's passing.
* **It did not raise the completion of passes in the simulated matches.** Over 40 matches per cell the completion moves by -0.006 (Arena Professional), -0.007 (Classic Professional), -0.028 (Arena Novice), +0.008 (Classic Novice), -0.006 (Arena Elite), -0.018 (Classic Elite). The Professional changes are inside the noise; the Arena Novice fall (-0.028, present in both blocks and with the fix alone) is real and **not explained**. A reading, not verified: the angular error of a short pass is small compared with the receiver's reach, so improving it changes little; what decides a completion is the receiver, the rivals and the distance.
* **Goals per match** (Arena / Classic, Professional): 6.6 -> 5.95 and 5.8 -> 5.4; the changes of every cell are between -0.95 and +0.7, all inside the noise of 40 matches. **Chances** (clear chances, shots chosen, shots on target), **possessions**, **mistakes per carrier decision** and **fouls**: no change beyond noise. The passes into space complete a little more often (above).
* **Does it improve the balance?** It does not improve it, and it does not break it: Arena stays at about 6 goals (inside the desired 6-8 of `AI_BALANCE_TARGETS_PHASE17.md`), Classic at about 5.4 (inside 4.5-6.5); the pass completion stays at 0.41-0.43. The gain is correctness and feel (button B and AI use the real passing attribute; pressure now matters), not a better score on these metrics.
* **Cost of the new pressure rule:** with the fix alone the Professional completion was 0.436 (Arena) / 0.409 (Classic); with the pressure term 0.425 / 0.416: no clear effect either way.

## 6. Tests and time
Full suite after the change: **2402 passed, 0 failed, 0 skipped, 7 min 13 s** (it was 6 min 18 s: the larger sample of the passes-into-space test adds about 70 s; one earlier run of this phase took 6 min 12 s). The quick battery takes about 6 minutes for a block of 6 cells (4 at a time), one cell at most 5 minutes; the longest single run of the phase was 295 s, far from the 1200 s limit. Level-3 style validation (hundreds of matches) was not run: it was not needed to detect a regression and is pending if a decision on the balance is to be taken.

## 7. Pending and limits
* The Arena Novice completion fall is unexplained; the cause of "better quality, not better completion" is a reading, not a measurement.
* The pressure number (0.125) is a first value chosen as half of the dribble's; it was not tuned and its effect on the matches is small. If the designer wants a more visible "imperfect under pressure", it is the number to raise (with the quick battery to see the cost).
* The 20-match cells cannot decide goal differences below about one goal; completion and shares can be read.
* The `passIntercepted` counter of the one-team observer reads 0 and was left out of the tables (a diagnostics gap, not looked into).
* Not done, as asked: VAR, Zenit Points, penalties, free kicks, cards, offside, crosses, headers, graphics, Unity, mobile controls.
