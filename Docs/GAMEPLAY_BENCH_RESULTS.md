# GameplayBench

20 scenes x 12 paired seeds. Improved metrics: 8, worsened: 3.

## ref vs cur (improved 8, worsened 3)

### A1_BuildUp — a centre-back builds from his own half against a mid block (12 seeds, 25 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| progress | higher is better | 22.03 | 22.59 | +0.56 ± 2.93 | flat |
| h_passOkRate | higher is better | 0.54 | 0.48 | -0.05 ± 0.03 | flat |
| h_passLostRate | lower is better | 0.44 | 0.51 | +0.07 ± 0.03 | **WORSE** |
| h_xg | higher is better | 0.06 | 0.05 | -0.01 ± 0.03 | flat |
| a_xg | lower is better | 0.05 | 0.08 | +0.03 ± 0.05 | flat |

### A2_WingPlay — a winger with the ball on the flank in the final third: do crosses come, arrive, and become shots (12 seeds, 20 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_crosses | higher is better | 1.50 | 1.08 | -0.42 ± 0.34 | flat |
| h_crossReach | higher is better | 0.50 | 0.17 | -0.33 ± 0.14 | **WORSE** |
| h_crossShots | higher is better | 0.08 | 0.00 | -0.08 ± 0.08 | flat |
| h_xg | higher is better | 0.23 | 0.23 | 0.00 ± 0.05 | flat |
| h_passLostRate | lower is better | 0.63 | 0.54 | -0.10 ± 0.07 | flat |
| a_xg | lower is better | 0.00 | 0.04 | +0.04 ± 0.02 | flat |

### A3_Combination — a carrier with a team-mate close and a defender pressing: give-and-return or loss (12 seeds, 15 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_giveAndReturn | higher is better | 1.25 | 0.50 | -0.75 ± 0.41 | flat |
| h_passLostRate | lower is better | 0.59 | 0.59 | 0.00 ± 0.08 | flat |
| progress | higher is better | 9.36 | 9.99 | +0.64 ± 2.16 | flat |
| h_xg | higher is better | 0.00 | 0.03 | +0.03 ± 0.03 | flat |

### A4_RunsOffTheBall — a carrier in midfield with two team-mates who can run in behind a line of five (12 seeds, 20 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| progress | higher is better | 14.70 | 10.13 | -4.57 ± 3.23 | flat |
| h_forwardPasses | higher is better | 5.25 | 5.67 | +0.42 ± 0.90 | flat |
| h_passOkRate | higher is better | 0.58 | 0.53 | -0.05 ± 0.08 | flat |
| h_xg | higher is better | 0.01 | 0.01 | 0.00 ± 0.01 | flat |
| a_xg | lower is better | 0.04 | 0.02 | -0.02 ± 0.03 | flat |

### A5_FinalThird — the carrier at the edge of the box with options: does he create and take a good chance (12 seeds, 15 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_shots | higher is better | 0.42 | 0.33 | -0.08 ± 0.26 | flat |
| h_xg | higher is better | 0.09 | 0.05 | -0.04 ± 0.06 | flat |
| h_bigChances | higher is better | 0.08 | 0.00 | -0.08 ± 0.08 | flat |
| h_firstShotAt | lower is better | 12.14 | 11.21 | -0.93 ± 2.13 | flat |
| h_passLostRate | lower is better | 0.58 | 0.64 | +0.06 ± 0.10 | flat |

### B1_UnderPressure — the carrier is closed down at 1.5 m and 3.8 m with outlets behind and beside him (12 seeds, 12 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_passLostRate | lower is better | 0.65 | 0.57 | -0.08 ± 0.04 | flat |
| firstLossAt | higher is better | 1.81 | 1.57 | -0.24 ± 0.27 | flat |
| h_passOkRate | higher is better | 0.35 | 0.42 | +0.07 ± 0.04 | flat |
| a_xg | lower is better | 0.01 | 0.03 | +0.02 ± 0.01 | flat |

### B2_TransitionDefence — the rival has just won the ball in the home half while the home team is up the pitch: how fast it recovers and how much it concedes (12 seeds, 10 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| a_xg | lower is better | 0.03 | 0.07 | +0.04 ± 0.03 | flat |
| a_shots | lower is better | 0.33 | 0.50 | +0.17 ± 0.21 | flat |
| a_firstShotAt | higher is better | 8.24 | 7.41 | -0.83 ± 1.08 | flat |
| regainSeconds | lower is better | 1.96 | 2.05 | +0.09 ± 0.10 | flat |
| h_interceptions | higher is better | 2.08 | 1.83 | -0.25 ± 0.71 | flat |

### B3_TransitionAttack — the home team wins the ball with the rival pushed up and two runners already beyond its line: does it use the space (12 seeds, 12 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_xg | higher is better | 0.02 | 0.00 | -0.02 ± 0.02 | flat |
| h_shots | higher is better | 0.08 | 0.00 | -0.08 ± 0.08 | flat |
| progress | higher is better | 10.24 | 6.61 | -3.64 ± 4.04 | flat |
| a_xg | lower is better | 0.10 | 0.05 | -0.05 ± 0.07 | flat |
| h_firstShotAt | lower is better | 11.87 | 12.00 | +0.13 ± 0.13 | flat |

### C1_PressingBuildUp — the rival builds from its own goal against a pressing home team (12 seeds, 15 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_interceptions | higher is better | 3.83 | 3.67 | -0.17 ± 0.51 | flat |
| a_passLostRate | higher is better | 0.38 | 0.41 | +0.02 ± 0.07 | flat |
| progressAway | lower is better | 18.77 | 20.51 | +1.74 ± 3.04 | flat |
| a_xg | lower is better | 0.05 | 0.03 | -0.03 ± 0.05 | flat |
| regainSeconds | lower is better | 1.35 | 1.52 | +0.17 ± 0.15 | flat |

### C2_CompactDefence — a rival carrier with three runners against four home defenders near their own box (12 seeds, 14 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| a_xg | lower is better | 0.04 | 0.07 | +0.02 ± 0.07 | flat |
| a_shots | lower is better | 0.08 | 0.17 | +0.08 ± 0.15 | flat |
| a_goals | lower is better | 0.08 | 0.08 | 0.00 ± 0.12 | flat |
| h_interceptions | higher is better | 4.33 | 4.17 | -0.17 ± 0.72 | flat |
| progressAway | lower is better | 5.94 | 8.03 | +2.09 ± 1.94 | flat |

### C3_WingDefence — a rival winger runs down the flank with a runner at the far post (12 seeds, 14 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| a_crosses | lower is better | 0.08 | 0.00 | -0.08 ± 0.08 | flat |
| a_crossReach | lower is better | 0.00 | 0.00 | 0.00 ± 0.00 | flat |
| a_xg | lower is better | 0.04 | 0.05 | +0.01 ± 0.03 | flat |
| h_interceptions | higher is better | 3.92 | 4.42 | +0.50 ± 0.87 | flat |

### C4_CoverSecond — a rival carrier against two defenders, primary and cover, with room behind (12 seeds, 12 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| a_xg | lower is better | 0.02 | 0.03 | +0.01 ± 0.04 | flat |
| a_shots | lower is better | 0.17 | 0.17 | 0.00 ± 0.21 | flat |
| progressAway | lower is better | 3.20 | 1.84 | -1.36 ± 1.09 | flat |
| h_interceptions | higher is better | 3.17 | 4.08 | +0.92 ± 0.62 | flat |

### K_S2_CoveredReceiver — the keeper holds the ball (S2_CoveredReceiver): distribution on or off (12 seeds, 9 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| a_goals | lower is better | 0.00 | 0.00 | 0.00 ± 0.00 | flat |
| h_passOkRate | higher is better | 0.50 | 1.00 | +0.50 ± 0.15 | **better** |
| firstLossAt | higher is better | 0.28 | 1.08 | +0.80 ± 0.02 | **better** |

### K_S4_Rival7m — the keeper holds the ball (S4_Rival7m): distribution on or off (12 seeds, 9 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| a_goals | lower is better | 0.08 | 0.00 | -0.08 ± 0.08 | flat |
| h_passOkRate | higher is better | 0.58 | 0.00 | -0.58 ± 0.15 | **WORSE** |
| firstLossAt | higher is better | 0.28 | 0.28 | 0.00 ± 0.00 | flat |

### K_GoalKick — a goal kick for the home team against a pressing rival (real restart procedure) (12 seeds, 12 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_passOkRate | higher is better | 0.49 | 0.50 | +0.01 ± 0.09 | flat |
| h_passLostRate | lower is better | 0.48 | 0.46 | -0.01 ± 0.08 | flat |
| a_xg | lower is better | 0.00 | 0.01 | +0.01 ± 0.01 | flat |
| a_shots | lower is better | 0.00 | 0.08 | +0.08 ± 0.08 | flat |

### H1_AssistPasses — a scripted person passes with a seeded aiming error of up to 25 degrees (the configurations differ only in the aim assistance) (12 seeds, 120 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_passToTargetRate | higher is better | 0.05 | 0.16 | +0.11 ± 0.02 | **better** |
| h_passLostRate | lower is better | 0.77 | 0.65 | -0.13 ± 0.04 | **better** |

### H2_AssistShots — a scripted person shoots from the edge of the box with an aiming error of up to 30 degrees (12 seeds, 5 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_xg | higher is better | 0.03 | 0.05 | +0.02 ± 0.01 | flat |
| h_goals | higher is better | 0.00 | 0.33 | +0.33 ± 0.14 | **better** |
| h_shots | higher is better | 1.00 | 1.00 | 0.00 ± 0.00 | flat |

### H3_AutoSelection — a person who touches nothing for five minutes: is the player he controls the one with the ball, or the nearest to it, and how often does the selection flicker (12 seeds, 300 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| selCarrier | higher is better | 0.95 | 0.93 | -0.02 ± 0.02 | flat |
| selNearestWhenDefending | higher is better | 0.90 | 0.93 | +0.04 ± 0.01 | flat |
| selDefSwitchesPerMinute | lower is better | 32.61 | 14.79 | -17.83 ± 3.99 | **better** |
| selFlickerPerMinute | lower is better | 10.04 | 0.08 | -9.95 ± 1.54 | **better** |
| selFar | lower is better | 0.04 | 0.01 | -0.04 ± 0.01 | flat |

### H4_ManualSwitch — a person presses the switch-player button while the rival has the ball: does the player he chose stay his (12 seeds, 300 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| manualKept | higher is better | 0.08 | 1.00 | +0.92 ± 0.01 | **better** |

### M1_FullMatch — a whole Classic match, Professional against Professional, real pairs of teams (12 seeds, 900 s)

| metric | judged | ref | cur | Δ ± SE | verdict |
|---|---|---|---|---|---|
| h_passOkRate | higher is better | 0.59 | 0.63 | +0.03 ± 0.02 | flat |
| h_xg | lower is better | 6.04 | 6.00 | -0.04 ± 0.74 | flat |
| a_xg | lower is better | 6.48 | 5.87 | -0.60 ± 0.59 | flat |

