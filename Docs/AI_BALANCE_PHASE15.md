# Phase 15 — Validation and balance of the AI difficulty: REPORT

Branch `phase-15-ai-validation-balance`, from `63a723e` (F14). Plan (written before measuring): `AI_BALANCE_PHASE15_PLAN.md`. Experiments: `AI_BALANCE_PHASE15_EXPERIMENTS.md`. Stamina proposal: `AI_STAMINA_PROPOSAL_PHASE15.md`. All data: `AI_BALANCE_PHASE15_data/` (see its README).

## 0. Verdict, in four layers
* **Architecture: correct.** One engine, one Core; the level is read once into `DecisionProfile`; no `if level`; no attribute, speed, stamina, score or RNG is touched (tests).
* **Behaviour: demonstrated.** The five levels play measurably differently, in the direction of the brief, in both modes, with confidence intervals that exclude zero for the decision metrics and for most attack/defence metrics.
* **Balance: partly demonstrated.** Against the same opponent (Profesional) the order Novato < Amateur < Profesional < Experto ≈ Élite holds in results in Arena and, with n = 300 for the extremes, in Classic. Neighbouring levels are mostly NOT separable by result with 150 matches (Experto vs Élite, Novato vs Amateur), and Amateur vs Profesional is separable by play quality in both modes but by result only in Classic.
* **Pending:** the stamina of a whole match (everyone plays most of it at 5–12 %), the top two levels, and the lack of a user test.

## 1. Baseline
F14 (`63a723e`): 2326 tests; F14 batteries were 120-second slices of matches that last 720 s (Arena) / 960 s (Classic). Repeating the whole matrix with 150 matches per cell on 120-second slices (`base120`) reproduced the F14 finding: process metrics ordered, goals/results not (0.6 goals per team per slice). The same matrix with REAL-length matches (`basefull`) separates the levels in results as well, and has ~10× the events per match: it is the reference of this phase. (The 120-second slices also never reached the last-90-seconds window of the clock/score logic.)

## 2. Changes (code)
* `RiskBias` 0.6 → 1.5 (Experiment A; the only balance change).
* Instruments (no behaviour): `DifficultyQualityScore` (five separate groups of ratios); observer counters for cards, possessions lost and mistakes/decisions by situation (pressed, little space, tired); `Phase15Battery` (reproducible CSV per match, 176 columns, both sides), `Phase15Probe_GameManagement`, `Phase15Probe_AmateurVsProfessional`, `Phase15Probe_Stamina`, `Phase15Tool_WriteTheProfiles`; the experiment hook (`FS27_EXP="*.Profile.<Number>=<value>"`) now reaches the profile numbers; the synthetic human-like profiles (tools only).
* 11 new tests (`AiBalance15Tests`); 2337 in total.

## 3. Experiments
Experiment A, see the log. No other number was changed: the data did not show a single parameter to blame for the remaining gaps.

## 4. Number of matches and seeds
Matrix real length: 6 comparisons × 2 modes × 150 matches = 1800 (+ 600 for Novato–Amateur and Élite–Experto). Validation S2: 4 cells × 150 baseline + 4 × 150 experiment. S3: 4 × 40. Slices: 12 × 150. Human-like: 16 × 60. Probes: 2 modes × 5 levels × 3 situations × 40 seeds, three times; Amateur probe 2 × 20 matches; stamina probe 2 × 5 × 6. Seeds: 1001–1075 (baseline), 2001–2075 (validation), 1001–1020 (repeat), 3001+/3201+ (probes), 4001+ (Amateur), 5001+ (stamina), 6001–6030 (human-like). Each seed is played twice, the level under test once at home and once away (the two teams of the seed are swapped with the levels, so no level plays only one of the teams).

## 5. Results by mode (real length, level under test vs Profesional, 150 matches per cell unless said)

Score rate = (wins + ½ draws) / matches, bootstrap 95 % CI.

| Level | Arena W/D/L | Arena score rate | Arena goals/match A–B | Classic W/D/L | Classic score rate | Classic goals/match A–B |
|---|---|---|---|---|---|---|
| Novato | 44/26/80 | 0.380 [0.310, 0.450] (n = 300 with S2: 0.387 [0.337, 0.437]) | 2.67–3.41 | 58/19/73 | 0.450 [0.377, 0.523] (n = 300: 0.418 [0.367, 0.472]) | 3.37–3.77 |
| Amateur | 53/26/71 | 0.440 [0.370, 0.517] | 3.17–3.44 | 49/21/80 | 0.397 [0.323, 0.473] | 3.39–3.99 |
| Profesional | 65/20/65 | 0.500 [0.427, 0.573] | 3.23–3.23 | 66/18/66 | 0.500 [0.420, 0.577] | 3.51–3.51 |
| Experto | 75/23/52 | 0.577 [0.507, 0.650] | 3.63–2.99 | 78/23/49 | 0.597 [0.527, 0.670] | 3.83–3.16 |
| Élite | 80/18/52 | 0.593 [0.523, 0.663] (n = 300: 0.580 [0.528, 0.633]) | 3.63–3.01 | 75/24/51 | 0.580 [0.503, 0.650] (n = 300: 0.582 [0.532, 0.633]) | 3.72–3.27 |
| Novato vs Élite | 36/16/98 | 0.293 [0.227, 0.360] | 2.61–3.93 | 44/13/93 | 0.337 [0.263, 0.410] | 3.10–4.32 |
| Élite vs Experto | 69/25/56 | 0.543 [0.470, 0.617] | 3.33–2.83 | 66/15/69 | 0.490 [0.417, 0.567] | 3.33–3.29 |
| Novato vs Amateur | 63/22/65 | 0.493 [0.420, 0.570] | 2.99–3.12 | 57/20/73 | 0.447 [0.370, 0.520] | 3.41–3.78 |

Goals: a whole match has 6.5 goals in Arena (median 6, 0-0 in 0.3 % of matches, max 15) and 7.2 in Classic (median 7, 0-0 never, max 17). A team scores 8 or more in 4 % (Arena) and 8 % (Classic) of the matches: neither 0-0 nor systematic 8-7, but the Classic total is high (see pending problems). Goals are reported, never used as a criterion.

## 6. Metrics by level (real length; A − B against Profesional; * the 95 % CI excludes 0)

| Metric | Mode | Novato | Amateur | Profesional | Experto | Élite |
|---|---|---|---|---|---|---|
| Mistakes per carrier decision (level's own) | Arena | 15.6 % | 10.2 % | 5.5 % | 2.3 % | 0.6 % |
| | Classic | 13.8 % | 8.9 % | 4.7 % | 1.9 % | 0.5 % |
| …under pressure / when tired (Arena) | | 20.9 / 15.8 % | 12.8 / 10.4 % | 6.3 / 5.6 % | 2.4 / 2.4 % | 0.5 / 0.6 % |
| Pass completion, A − B | Arena | −0.023* | 0.000 | – | +0.021* | +0.027* |
| | Classic | −0.011* | −0.001 | – | +0.008 | +0.018* |
| Passes given away per 100 passes | Arena | 41.6 | 39.5 | 38.6 | 37.7 | 36.6 |
| | Classic | 45.7 | 44.4 | 44.4 | 43.3 | 43.0 |
| Clear chances created per 100 possessions, A − B | Arena | −5.5* | −4.4* | – | +6.7* | +9.3* |
| | Classic | −0.3 | −0.6 | – | +2.2* | +4.1* |
| Clear chances conceded: the mirror of the row above | | | | | | |
| Recoveries per opponent possession, A − B | Arena | −0.114* | −0.055* | – | +0.051* | +0.082* |
| | Classic | −0.051* | −0.021* | – | +0.007 | +0.031* |
| Contest success (won / contacts), A − B | Arena | −0.102* | −0.024 | – | +0.023 | +0.004 |
| | Classic | −0.074* | −0.015 | – | −0.049 | −0.015 |
| Shots per match / conversion (Arena) | | 28.3 / 6.6 % | 28.6 / 7.8 % | 29.3 / 8.0 % | 31.0 / 9.2 % | 30.3 / 9.4 % |
| Shots per match / conversion (Classic) | | 37.0 / 6.7 % | 39.2 / 6.2 % | 39.0 / 6.2 % | 41.8 / 6.8 % | 42.0 / 6.8 % |
| Mean stamina of the field players (0–100) | Arena / Classic | 14.2 / 11.3 | 14.8 / 12.0 | 15.2 / 12.3 | 15.5 / 12.9 | 15.8 / 12.8 |
| Fouls per match (Classic) | | 0.27 | 0.23 | 0.15 | 0.20 | 0.10 |
| Yellow / red cards | both | 0 | 0 | 0 | 0 | 0 |

Arena had 0 fouls and 0 cards in every cell. (Per decision each level's rhythm is also different: Élite takes about 2.2× the carrier decisions of a Novato in the same match.)

## 7. Errors, chances, passes, shots, defence, stamina (summary)
* **Errors:** the controlled mistakes fall monotonically with the level in every situation, and fall faster under pressure for the better levels (Novato 20.9 % under pressure vs 15.6 % overall; Élite 0.5 % vs 0.6 %).
* **Chances:** created and conceded are ordered in both modes; in Classic the two lowest levels create about as many chances as Profesional (−0.3, −0.6, CIs include 0) and lose through recoveries and duels instead.
* **Passes:** completion and passes given away improve monotonically; the gain per level is small (≈ 1–3 points) because pass quality itself belongs to the players.
* **Shots:** the number of shots is flat across levels (28–31 in Arena); the levels differ in conversion (6.6 → 9.4 % Arena). `ShootMinScore` 2.0 untouched.
* **Defence:** recoveries and chances conceded improve with the level (the mirror of the attack rows); contest success separates only the extremes in Arena and does not separate in Classic.
* **Stamina:** see the proposal; the difference between levels is ≈ 1.5 points of mean stamina.
* **Movement:** distance from home with/without the ball (Arena ≈ 4.6–4.7 m defending at every level) does not separate the levels (differences < 0.1 m); the drift introduced by the level is far smaller than the natural movement. It is reported, not claimed.
* **Game management:** before Experiment A the share of risky choices at the end of a period moved by ≈ 1 point between trailing and leading (noise); after it, by 4–8 points in nine of ten level/mode pairs (Classic Novato +1.2, the lowest awareness).

## 8. Amateur vs Profesional
Distinguishable by play quality in both modes (error rate +0.047 [0.044, 0.050]; clear chances −4.4 [−6.2, −2.7] per 100 possessions in Arena, −0.6 (n.s.) in Classic; recoveries −0.055 [−0.075, −0.035] Arena, −0.021 [−0.031, −0.012] Classic; shots, pressure and tired errors all significant), and by result in Classic (0.397 [0.323, 0.473]); in Arena the result alone (0.440 [0.370, 0.517]) cannot exclude equality. Why only a modest gap: in the same situation the Amateur engine takes the same decision as the Professional 94.7 % of the time (carrier) and 97 % (off the ball); groups: mistakes 96.4 %, habits 97.1 %, positioning 97.8 %, anticipation + pressing 98.9 %, stamina 99 %. The rest of the gap is the rhythm (×1.25), the delays and the mistake rate (×1.4). Not tuned: see the experiments log.

## 9. Experto and Élite
Experto vs Profesional 0.577 / 0.597 (both excluded 0.5); Élite vs Profesional 0.593 / 0.580. Élite vs Experto: 0.543 [0.470, 0.617] Arena, 0.490 [0.417, 0.567] Classic. Élite is better than Experto in play (error rate, chances, final-third entries, recoveries: CIs exclude 0) but not demonstrably in result. **Élite is very difficult and beatable:** against Profesional it lost 52/150 (Arena) and 51/150 (Classic); against Novato it won 98/150 and 93/150 (65 %, 62 %), never 95 %.

## 10. Simulation of human-like profiles (a SIMULATION, not a user test)
Two synthetic whole-team profiles written in advance and never tuned: HumanSkilled (decision quality 0.85, rhythm 0.40, reaction 0.30 s, execution 0.75, anticipation 0.8, positioning 0.6) and HumanCasual (0.5, 0.50, 0.40 s, 0.5, 0.4, 0.4). 60 matches per cell. Score rate Arena / Classic: HumanSkilled vs Novato 0.542 / 0.592, vs Amateur 0.500 / 0.525, vs Profesional 0.408 / 0.500, vs Experto 0.300 / 0.425, vs Élite 0.392 / 0.442 (it won 17 and 22 of 60 against Élite); HumanCasual vs Novato 0.542 / 0.458, vs Profesional 0.383 / 0.467, vs Élite 0.325 / 0.283. Reading: a skilled player who decides well but slowly sits between Amateur and Profesional, can beat Élite in a fair share of matches, and a casual one is about a Novato's equal. **A real test needs a person controlling one player in Unity; the Core is ready for it (a team under human control is never driven, and `SetDifficulty` is per team), nothing was connected.**

## 11. Identity
Team identity survives every level: the order of the teams by forward-pass share, shots, hold, protect and defensive share is preserved (Spearman 0.78–0.99 against Profesional in the 8 shares, lowest 0.43/0.59 for Novato's dribble share) and the spread between teams is the same at every level (`summaries/basefull_team_identity.txt`). Arena: 0 fouls at every level, duels kept; Classic: fouls ≤ 0.45 per match and ≤ 20 % of contacts.

## 12. Success criteria (fixed in the plan) — evaluated
| # | Criterion | Result |
|---|---|---|
| 1 | Decision quality ordered (Spearman ≥ 0.9 for error rate, pass completion, chances created/conceded; adjacent error-rate CIs apart) | **Met** (paired differences vs the same opponent; Classic chances: 0.9 with the Novato/Amateur inversion inside the CI) |
| 2 | Amateur vs Profesional distinguishable | **Met** (process in both modes, result in Classic) |
| 3 | Experto harder than Profesional | **Met** (both modes) |
| 4 | Élite beatable; Novato beatable | **Met** (Élite loses ≥ 34 % vs Pro and does not win > 65 % vs Novato; Novato CI upper bound < 0.5 in Arena and, pooled, in Classic) |
| 5 | Stable on S2 and S3 | **Met for Élite/Novato** (S2: 0.567/0.393 Arena, 0.583/0.387 Classic); Amateur and Experto were not repeated on S2 |
| 6 | Identity | **Met** |
| 7 | No cheating, deterministic | **Met** (tests) |
| 8 | Goals not a criterion | Respected |

## 13. Parameters
* **Modified:** `DecisionProfileDefaults.RiskBias` 0.6 → 1.5.
* **Not modified:** every other number of the profile and of the definitions (the table is in `AI_BALANCE_PHASE15_data/profiles.txt`), `ShootMinScore` 2.0, all tunings of the engine, Locomotion/stamina, BallSimulator, SkillResolver, F10/F11.

## 14. Pending problems
1. **Stamina** of a whole match: mean 11–16 in every cell; a separate proposal (chase policy first).
2. **Experto vs Élite** and **Novato vs Amateur** (Classic) are not separable by result at 150 matches; they are by play quality except Classic chance creation at the bottom. A fair test of them would need ≥ 400 matches per cell.
3. **Total goals per match are high** (6.5 / 7.2; a team scores 8+ in 4 % / 8 %); not touched, not a target of this phase; Classic is the one to watch.
4. **Cards are not exercised** in Classic (0 per 150 matches): fouls are rare (≈ 0.15 per match), so the card flow of the Core is untested at the AI level.
5. **Metrics that the Core cannot produce** and were NOT invented: a "correct decision" label, shots on target (we report shot outcome: goal / stopped / out), occupation of space. `LossesPerPossession` in `DifficultyQualityScore` is degenerate (≈ 0.98 by construction): the table above uses the passes given away instead; the ratio should be replaced in F16.
6. Distance from home does not separate the levels (< 0.1 m).
7. Amateur and Experto were not re-run on fresh seeds.
8. The human-like profiles are guesses; the user test is pending.

## 15. Recommendation for F16
(1) the stamina proposal, with the Phase 15 battery re-run unchanged to see whether neighbouring levels separate; (2) 400+ match cells for the neighbouring pairs; (3) replace the degenerate ratio and add a real "avoidable loss" metric to the observer; (4) review the total goals of Classic as its own question; (5) the user test (Unity) with `SetDifficulty` on the AI team only; (6) only then reconsider the spread of the top two levels (the data point to conversion and chance creation, not to errors).
