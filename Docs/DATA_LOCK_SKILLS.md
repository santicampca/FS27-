# FS27 — Fase 5: catálogo de skills y asignación (detalle)

Detalle auditable de las skills. Reglas y decisiones en [DATA_LOCK.md](DATA_LOCK.md). Fuente de verdad: `Docs/authoring/FS27_SKILLS_V1.json` (catálogo) + las etiquetas de firma de las bases de jugadores (authoring). La asignación se calcula al cargar, de forma determinista (`SkillAssigner`).

## 1. Catálogo
| Skill | Familia | Tipo | Desbloquea | Roles | Requisitos | Etiquetas de authoring que significan esta skill |
|---|---|---|---|---|---|---|
| `close_control_dribble` — Regate de control cerrado | Ball | Dribble | SkillMove | cualquiera | Control ≥ 74, Dribbling ≥ 74 | CloseControlDribble, CloseControlSlalom, CloseControlPocketTurn, SlalomBoxDribble, ZonalSlalomDribble, ElusivePocketDribble, CloseQuarterSkill, SkillfulDribble |
| `elusive_dribble` — Regate escurridizo | Ball | Dribble | SkillMove | cualquiera | Agility ≥ 74, Dribbling ≥ 74 | OneVsOneDribble, OneVsOneIsolation, ExplosiveDribble, ChangeOfPaceDribble |
| `flair_dribble` — Regate de fantasía | Ball | Dribble | SkillMove | cualquiera | Dribbling ≥ 76, Technique ≥ 76 | RainbowFlickDribble, DragBack |
| `progressive_pass` — Pase progresivo | Ball | Passing | — | cualquiera | Passing ≥ 74 | ProgressivePass, VerticalPass, HalfSpacePass, WideProgression, InvertedProgression, InvertedCutInsidePass |
| `line_breaking_pass` — Pase rompe-líneas | Ball | Passing | — | cualquiera | Passing ≥ 76, Technique ≥ 74 | LineBreakingPass, PrecisionThroughBall, VisionaryThroughBall |
| `long_switch` — Cambio de orientación | Ball | SpecialPass | SkillLobPass | cualquiera | Passing ≥ 76, Technique ≥ 74 | SwitchOfPlay, LongDiagonalPass, MidfieldDistribution |
| `creative_pass` — Pase creativo | Ball | SpecialPass | SkillLobPass | cualquiera | Passing ≥ 72, Technique ≥ 74 | CreativePass, NoLookPass, OutsideOfFootPass, DirectAssistance, CreativeAssistance, FinalThirdAssistance, Cutback, HalfSpaceOrchestration |
| `build_up_play` — Salida y juego asociativo | Ball | Passing | — | cualquiera | Control ≥ 72, Passing ≥ 72 | DeepBuildUp, TempoControl, LinkUpPlay, CombinationPlay, DropDeepToLink |
| `precision_cross` — Centro de precisión | Ball | Passing | — | cualquiera | Passing ≥ 72, Technique ≥ 72 | PrecisionCross, EarlyCross, LineBreakingCross |
| `clinical_finishing` — Definición | Ball | Shooting | — | Finisher, GoalHunter, Target, Creator, Winger, Explosive, Engine | Finishing ≥ 76 | ClinicalFinishing, PrecisionShot, FinessePlacement, NearPostShot, OneVsOneFinishing, DirectShot |
| `first_time_finish` — Remate a primer toque | Ball | Shooting | — | Finisher, GoalHunter, Target, Creator, Winger, Explosive, Engine | Finishing ≥ 74 | FirstTouchShot, FirstTimeFinishing |
| `long_range_strike` — Disparo de media distancia | Ball | Shooting | — | cualquiera | Shooting ≥ 76 | LongRangeStrike, PowerShot |
| `curve_shot` — Disparo con efecto | Ball | SpecialShot | CurvedShot | cualquiera | Shooting ≥ 74, Technique ≥ 76 | CurlFinesseShot |
| `header_finish` — Remate de cabeza | Ball | Shooting | — | Target, Finisher, GoalHunter, Wall, Guardian, Anchor, Engine | Strength ≥ 70 | SetPieceHeader, BackPostHeader, CloseRangeHeader, AerialBoxThreat, AerialThreat, AerialHeaderExpert |
| `dead_ball_specialist` — Especialista a balón parado | Ball | SpecialShot | — | cualquiera | Shooting ≥ 74, Technique ≥ 76 | PenaltySpecialist, DeadBallSpecialist |
| `pocket_receive` — Recepción entre líneas | Ball | FirstTouch | — | cualquiera | Control ≥ 76, Technique ≥ 74 | PocketReceive, IntelligentPocketReading, BetweenLinesTurn |
| `hold_up_play` — Pivote y protección | Ball | Technique | — | cualquiera | Control ≥ 72, Strength ≥ 72 | HoldUpPlay, PhysicalShielding, CounterAttackHoldUp, CentreBackDominance, CentreBackShielding, CentreBackHolding, PhysicalFriction |
| `explosive_burst` — Arranque explosivo | Movement | Acceleration | — | cualquiera | Acceleration ≥ 80 | ExplosiveBurst, WingBurst, AmbidextrousBurst, ExplosivePowerRun |
| `counter_sprint` — Sprint de contraataque | Movement | Acceleration | — | cualquiera | Speed ≥ 80 | CounterAttackPace, CounterAttackRun, TransitionSprint, ExplosiveTransition, LongStridePace, HighSpeedSpaceAttack, DirectRunning, HyperSpeedBreakaway |
| `turn_and_accelerate` — Giro y aceleración | Movement | Agility | — | cualquiera | Acceleration ≥ 74, Agility ≥ 76 | TurnAndAccelerate, ElusiveRun, SuddenChangeOfPace |
| `progressive_carry` — Conducción progresiva | Movement | BallCarrying | — | cualquiera | Dribbling ≥ 70 | ProgressiveCarry, PowerProgressiveCarry, PowerCarryThroughCenter, ProgressiveDrive, DrivingRun, InsideDrive, PowerDrive, HalfSpaceDrive, OutsideWingerCarry, DirectGoalDrive |
| `press_resistance` — Resistencia a la presión | Movement | BallCarrying | — | cualquiera | Agility ≥ 72, Control ≥ 76 | PressResistance, PressureEscape |
| `cut_inside` — Diagonal hacia dentro | Movement | BallCarrying | — | cualquiera | Dribbling ≥ 72 | CutInside, WingCutInside, CutInsideAndShoot, CutInsideAndFinish, InsideSlash, FlankToCenterCut |
| `support_run` — Apoyo sin balón | Movement | OffBall | — | cualquiera | Stamina ≥ 70 | SupportRun, OffBallMovement, OffBallSpaceAttack |
| `overlapping_run` — Desdoblamiento | Movement | OffBall | — | cualquiera | Speed ≥ 70, Stamina ≥ 70 | OverlappingRun |
| `half_space_movement` — Movimiento en el carril interior | Movement | OffBall | — | cualquiera | Agility ≥ 70 | InsideMovement, HalfSpaceMovement, HalfSpaceOrbit, DiagonalRun, InsideDiagonalRun, DiagonalPenetration, OffBallPenetration, PocketPenetration, DeepRun |
| `late_box_arrival` — Llegada al área | Movement | OffBall | — | cualquiera | Stamina ≥ 70 | LateBoxArrival, LateRunsIntoBox, LateBoxCrash, BoxRuns, BoxAttacking, GoalAreaArrival, BoxToBoxRun, CoreBoxPresence, WeakSideAttack |
| `interception_reading` — Lectura e intercepción | Defensive | Interception | — | Wall, Guardian, Anchor, Engine, Builder, Winger, Explosive | Defense ≥ 70 | Interception, CleanInterception, AggressiveInterception, Anticipation, PassingLaneBlock |
| `aggressive_tackle` — Entrada agresiva | Defensive | Tackle | — | Wall, Guardian, Anchor, Engine, Builder, Winger, Explosive | Defense ≥ 70 | AggressiveTackle, FrontFootDefending |
| `one_v_one_defending` — Defensa del uno contra uno | Defensive | Tackle | — | Wall, Guardian, Anchor, Engine, Builder, Winger, Explosive | Agility ≥ 68, Defense ≥ 72 | OneVsOneContainment |
| `defensive_block` — Bloqueo | Defensive | Block | — | Wall, Guardian, Anchor, Engine, Builder, Winger, Explosive | Defense ≥ 70 | PhysicalBlock |
| `defensive_screen` — Cobertura y pantalla | Defensive | Block | — | Wall, Guardian, Anchor, Engine, Builder, Winger, Explosive | Defense ≥ 68 | DefensiveScreen, WideCover, DefensiveCover, BackLineCommand, DefensiveTracking |
| `high_press` — Presión alta | Defensive | Pressure | — | cualquiera | Stamina ≥ 72 | Pressing, HighPressing, HighIntensityPress, HighPressSurge, PressingDuels, DefendingFromFront |
| `counter_press` — Contrapresión | Defensive | Pressure | — | cualquiera | Stamina ≥ 72 | Counterpressing |
| `ball_recovery` — Recuperación de balón | Defensive | Pressure | — | cualquiera | Stamina ≥ 72 | BallRecovery, DefensiveWorkrate, SidelineWorkrate |
| `recovery_sprint` — Carrera de recuperación | Defensive | Recovery | — | Wall, Guardian, Anchor, Engine, Builder, Winger, Explosive | Speed ≥ 74 | RecoveryRun, WideRecovery, DefensiveRecovery |
| `aerial_duel` — Juego aéreo | Defensive | Aerial | — | cualquiera | Strength ≥ 72 | AerialClearance, AerialDominance, AerialDuels, AerialShielding, DefensiveSetPieceClearance |

### Etiquetas diferidas a propósito
| Etiqueta diferida | Motivo |
|---|---|
| `OffsideTrapExploitation` | Depends on the offside rule, which is a later Classic-rules phase. |
| `TacticalFoul` | Depends on fouls and the referee, which are a later Classic-rules phase. |

## 2. Cada etiqueta original → etiqueta normalizada → skill final
Las 258 etiquetas de texto libre de las bases de jugadores pasan primero por los vocabularios que ya existían en los importadores (258 → 160 etiquetas conceptuales) y después por el catálogo (160 → 37 skills + 2 diferidas). Ninguna etiqueta queda sin resolver.

| Etiqueta original (texto libre del authoring) | Veces | Etiqueta normalizada | Skill final |
|---|---|---|---|
| 1v1Dribble | 1 | OneVsOneDribble | `elusive_dribble` |
| 1v1Isolation | 2 | OneVsOneIsolation | `elusive_dribble` |
| Accurate Long Diagonal | 3 | LongDiagonalPass | `long_switch` |
| Aerial Clearance | 3 | AerialClearance | `aerial_duel` |
| Aerial Clearances | 1 | AerialClearance | `aerial_duel` |
| Aerial Dominance | 4 | AerialDominance | `aerial_duel` |
| Aerial Power Clearance | 1 | AerialClearance | `aerial_duel` |
| AerialBoxThreat | 1 | AerialBoxThreat | `header_finish` |
| AerialClearance | 1 | AerialClearance | `aerial_duel` |
| AerialDuelDomination | 1 | AerialDuels | `aerial_duel` |
| AerialDuels | 1 | AerialDuels | `aerial_duel` |
| AerialDuelsAggressive | 1 | AerialDuels | `aerial_duel` |
| AerialHeaderExpert | 1 | AerialHeaderExpert | `header_finish` |
| AerialShielding | 1 | AerialShielding | `aerial_duel` |
| AerialThreat | 1 | AerialThreat | `header_finish` |
| Aggressive 1v1 Duel | 1 | AggressiveTackle | `aggressive_tackle` |
| Aggressive 1v1 containment | 1 | OneVsOneContainment | `one_v_one_defending` |
| Aggressive Forward Charge | 1 | AggressiveTackle | `aggressive_tackle` |
| Aggressive Forward Interception | 1 | AggressiveInterception | `interception_reading` |
| Aggressive Interception | 1 | AggressiveInterception | `interception_reading` |
| Aggressive Tackle | 3 | AggressiveTackle | `aggressive_tackle` |
| AggressivePress | 1 | HighIntensityPress | `high_press` |
| AmbidextrousBurst | 1 | AmbidextrousBurst | `explosive_burst` |
| Back-Line Command | 1 | BackLineCommand | `defensive_screen` |
| Back-post header | 1 | BackPostHeader | `header_finish` |
| BackToGoalHoldUp | 1 | HoldUpPlay | `hold_up_play` |
| BackToGoalPlay | 1 | HoldUpPlay | `hold_up_play` |
| BallRecovery | 8 | BallRecovery | `ball_recovery` |
| BetweenLinesTurn | 1 | BetweenLinesTurn | `pocket_receive` |
| BlindsideAttack | 1 | WeakSideAttack | `late_box_arrival` |
| Box-to-Box Run | 1 | BoxToBoxRun | `late_box_arrival` |
| BoxAERIALThreat | 1 | AerialBoxThreat | `header_finish` |
| BoxAttacking | 1 | BoxAttacking | `late_box_arrival` |
| BoxRuns | 1 | BoxRuns | `late_box_arrival` |
| BurstAcceleration | 1 | ExplosiveBurst | `explosive_burst` |
| Calm Pressure Escape | 2 | PressureEscape | `press_resistance` |
| Calm Progressive Pass | 1 | ProgressivePass | `progressive_pass` |
| CentreBackBully | 1 | CentreBackDominance | `hold_up_play` |
| CentreBackDominance | 1 | CentreBackDominance | `hold_up_play` |
| CentreBackDomination | 1 | CentreBackDominance | `hold_up_play` |
| CentreBackHolding | 1 | CentreBackHolding | `hold_up_play` |
| CentreBackShielding | 1 | CentreBackShielding | `hold_up_play` |
| ChangeOfPaceDribble | 1 | ChangeOfPaceDribble | `elusive_dribble` |
| Clean 1v1 Interception | 1 | CleanInterception | `interception_reading` |
| Clean Anticipation | 2 | Anticipation | `interception_reading` |
| Clean Anticipation Tackle | 1 | CleanInterception | `interception_reading` |
| Clean Positional Interception | 3 | CleanInterception | `interception_reading` |
| Clean Progressive Pass | 4 | ProgressivePass | `progressive_pass` |
| Clinical1v1Finishing | 2 | OneVsOneFinishing | `clinical_finishing` |
| ClinicalFinish | 1 | ClinicalFinishing | `clinical_finishing` |
| ClinicalFinishing | 3 | ClinicalFinishing | `clinical_finishing` |
| CloseControlDribble | 3 | CloseControlDribble | `close_control_dribble` |
| CloseControlPocketTurn | 1 | CloseControlPocketTurn | `close_control_dribble` |
| CloseControlSlalom | 1 | CloseControlSlalom | `close_control_dribble` |
| CloseQuarterSkill | 1 | CloseQuarterSkill | `close_control_dribble` |
| CloseRangeHeader | 1 | CloseRangeHeader | `header_finish` |
| CombinationPlay | 1 | CombinationPlay | `build_up_play` |
| Composed Pressure Escape | 1 | PressureEscape | `press_resistance` |
| CoreBoxPresence | 1 | CoreBoxPresence | `late_box_arrival` |
| CounterAttackBurst | 1 | CounterAttackRun | `counter_sprint` |
| CounterAttackHoldUp | 1 | CounterAttackHoldUp | `hold_up_play` |
| CounterAttackPace | 1 | CounterAttackPace | `counter_sprint` |
| CounterAttackRun | 1 | CounterAttackRun | `counter_sprint` |
| CounterAttackSprint | 1 | CounterAttackPace | `counter_sprint` |
| CounterPaceBurst | 1 | CounterAttackPace | `counter_sprint` |
| CounterPress | 16 | Counterpressing | `counter_press` |
| Counterpressing | 5 | Counterpressing | `counter_press` |
| CreativeAssistance | 1 | CreativeAssistance | `creative_pass` |
| CreativePass | 1 | CreativePass | `creative_pass` |
| CurlFinesseShot | 1 | CurlFinesseShot | `curve_shot` |
| CurvedShot | 1 | CurlFinesseShot | `curve_shot` |
| CutInside | 2 | CutInside | `cut_inside` |
| CutInsideAndFinish | 1 | CutInsideAndFinish | `cut_inside` |
| CutInsideAndShoot | 2 | CutInsideAndShoot | `cut_inside` |
| CutInsideAndStrike | 1 | CutInsideAndShoot | `cut_inside` |
| Cutback | 1 | Cutback | `creative_pass` |
| Cutback assist | 2 | Cutback | `creative_pass` |
| DeadBallSpecialist | 1 | DeadBallSpecialist | `dead_ball_specialist` |
| DeepBuildUp | 9 | DeepBuildUp | `build_up_play` |
| DeepRun | 1 | DeepRun | `half_space_movement` |
| DefendingFromFront | 1 | DefendingFromFront | `high_press` |
| Defensive Block | 2 | PhysicalBlock | `defensive_block` |
| Defensive Cover | 4 | DefensiveCover | `defensive_screen` |
| Defensive recovery sprint | 1 | RecoveryRun | `recovery_sprint` |
| DefensivePressing | 1 | HighIntensityPress | `high_press` |
| DefensiveRecovery | 1 | DefensiveRecovery | `recovery_sprint` |
| DefensiveScreen | 8 | DefensiveScreen | `defensive_screen` |
| DefensiveSetPieceClearance | 1 | DefensiveSetPieceClearance | `aerial_duel` |
| DefensiveTracking | 1 | DefensiveTracking | `defensive_screen` |
| DefensiveWorkrate | 1 | DefensiveWorkrate | `ball_recovery` |
| DiagonalPenetration | 1 | DiagonalPenetration | `half_space_movement` |
| DiagonalRun | 2 | DiagonalRun | `half_space_movement` |
| DirectAssistance | 1 | DirectAssistance | `creative_pass` |
| DirectGoalDrive | 2 | DirectGoalDrive | `progressive_carry` |
| DirectRunning | 1 | DirectRunning | `counter_sprint` |
| DirectShot | 1 | DirectShot | `clinical_finishing` |
| Dominant 1v1 Lockdown | 2 | OneVsOneContainment | `one_v_one_defending` |
| Dominant Aerial Clearance | 2 | AerialClearance | `aerial_duel` |
| DrivingRun | 1 | DrivingRun | `progressive_carry` |
| DropDeepToLink | 1 | DropDeepToLink | `build_up_play` |
| Early cross | 3 | EarlyCross | `precision_cross` |
| Electrifying1v1 | 1 | OneVsOneDribble | `elusive_dribble` |
| EliteFinishing | 1 | ClinicalFinishing | `clinical_finishing` |
| ElusiveDribble | 1 | CloseControlDribble | `close_control_dribble` |
| ElusivePocketDribble | 1 | ElusivePocketDribble | `close_control_dribble` |
| ElusiveRun | 1 | ElusiveRun | `turn_and_accelerate` |
| Explosive dribble | 1 | ExplosiveDribble | `elusive_dribble` |
| ExplosiveAcceleration | 1 | ExplosiveBurst | `explosive_burst` |
| ExplosiveBurst | 2 | ExplosiveBurst | `explosive_burst` |
| ExplosivePowerRun | 1 | ExplosivePowerRun | `explosive_burst` |
| ExplosiveTransition | 1 | ExplosiveTransition | `counter_sprint` |
| Fierce 1v1 Shielding | 1 | PhysicalShielding | `hold_up_play` |
| FinalThirdAssistance | 1 | FinalThirdAssistance | `creative_pass` |
| FinessePlacement | 2 | FinessePlacement | `clinical_finishing` |
| FirstTimeFinishing | 1 | FirstTimeFinishing | `first_time_finish` |
| FirstTouchBoxShot | 1 | FirstTouchShot | `first_time_finish` |
| FirstTouchFinishing | 1 | FirstTimeFinishing | `first_time_finish` |
| FirstTouchShot | 1 | FirstTouchShot | `first_time_finish` |
| FlankToCenterCut | 1 | FlankToCenterCut | `cut_inside` |
| Front-Foot Aggressive Tackle | 1 | AggressiveTackle | `aggressive_tackle` |
| Front-Foot Anticipation | 1 | Anticipation | `interception_reading` |
| Front-Foot Defending | 3 | FrontFootDefending | `aggressive_tackle` |
| GoalAreaArrival | 1 | GoalAreaArrival | `late_box_arrival` |
| Half-Space Movement | 4 | HalfSpaceMovement | `half_space_movement` |
| HalfSpaceDrive | 1 | HalfSpaceDrive | `progressive_carry` |
| HalfSpaceMovement | 2 | HalfSpaceMovement | `half_space_movement` |
| HalfSpaceOrbit | 1 | HalfSpaceOrbit | `half_space_movement` |
| HalfSpaceOrchestration | 1 | HalfSpaceOrchestration | `creative_pass` |
| High Pressing | 2 | HighPressing | `high_press` |
| High-press ball win | 1 | HighPressing | `high_press` |
| High-press tackle | 1 | HighPressing | `high_press` |
| HighIntensityPress | 1 | HighIntensityPress | `high_press` |
| HighIntensityPressRun | 1 | HighIntensityPress | `high_press` |
| HighPressSurge | 2 | HighPressSurge | `high_press` |
| HighPressuring | 1 | HighIntensityPress | `high_press` |
| HighSpeedSpaceAttack | 1 | HighSpeedSpaceAttack | `counter_sprint` |
| HighWorkratePress | 1 | HighIntensityPress | `high_press` |
| HoldUpPlay | 1 | HoldUpPlay | `hold_up_play` |
| HyperSpeedBreakaway | 1 | HyperSpeedBreakaway | `counter_sprint` |
| Immaculate 1v1 Containment | 1 | OneVsOneContainment | `one_v_one_defending` |
| Inside Movement | 7 | InsideMovement | `half_space_movement` |
| InsideBoxMovement | 1 | BoxRuns | `late_box_arrival` |
| InsideCutAndFinish | 1 | CutInsideAndFinish | `cut_inside` |
| InsideCutAndShoot | 1 | CutInsideAndShoot | `cut_inside` |
| InsideDiagonalRun | 1 | InsideDiagonalRun | `half_space_movement` |
| InsideDrive | 1 | InsideDrive | `progressive_carry` |
| InsideSlash | 1 | InsideSlash | `cut_inside` |
| IntelligentPocketReading | 1 | IntelligentPocketReading | `pocket_receive` |
| Interception | 6 | Interception | `interception_reading` |
| Inverted Play Progression | 1 | InvertedProgression | `progressive_pass` |
| Inverted half-space pass | 1 | HalfSpacePass | `progressive_pass` |
| Inverted passing lane block | 1 | PassingLaneBlock | `interception_reading` |
| Inverted play progression | 1 | InvertedProgression | `progressive_pass` |
| InvertedCutInsidePass | 1 | InvertedCutInsidePass | `progressive_pass` |
| InvertedDiagonalRun | 1 | InsideDiagonalRun | `half_space_movement` |
| Late box arrival | 2 | LateBoxArrival | `late_box_arrival` |
| LateBoxArrival | 9 | LateBoxArrival | `late_box_arrival` |
| LateBoxCrash | 1 | LateBoxCrash | `late_box_arrival` |
| LateRunsIntoBox | 1 | LateRunsIntoBox | `late_box_arrival` |
| LightningBurst | 1 | ExplosiveBurst | `explosive_burst` |
| LineBreakingCross | 1 | LineBreakingCross | `precision_cross` |
| LineBreakingPass | 17 | LineBreakingPass | `line_breaking_pass` |
| LinkUpPassing | 2 | LinkUpPlay | `build_up_play` |
| LinkUpPlay | 1 | LinkUpPlay | `build_up_play` |
| Long-range screamer | 1 | LongRangeStrike | `long_range_strike` |
| Long-range strike | 2 | LongRangeStrike | `long_range_strike` |
| LongRangeStrike | 2 | LongRangeStrike | `long_range_strike` |
| LongStridePace | 1 | LongStridePace | `counter_sprint` |
| LongSwitch | 7 | SwitchOfPlay | `long_switch` |
| Masterclass 1v1 Containment | 1 | OneVsOneContainment | `one_v_one_defending` |
| Midfield Distribution (CDM) | 1 | MidfieldDistribution | `long_switch` |
| MilimeterCross | 1 | PrecisionCross | `precision_cross` |
| NearPostShot | 1 | NearPostShot | `clinical_finishing` |
| NoLookPass | 1 | NoLookPass | `creative_pass` |
| OffBallMovement | 1 | OffBallMovement | `support_run` |
| OffBallPenetration | 1 | OffBallPenetration | `half_space_movement` |
| OffBallSpaceAttack | 1 | OffBallSpaceAttack | `support_run` |
| OffsideTrapExploitation | 1 | OffsideTrapExploitation | (diferida) |
| Open-Field Recovery Sprint | 5 | RecoveryRun | `recovery_sprint` |
| Outside-of-boot cross | 1 | PrecisionCross | `precision_cross` |
| OutsideOfFootPass | 1 | OutsideOfFootPass | `creative_pass` |
| OutsideWingerBurst | 1 | WingBurst | `explosive_burst` |
| OutsideWingerCarry | 1 | OutsideWingerCarry | `progressive_carry` |
| Overlap | 1 | OverlappingRun | `overlapping_run` |
| Overlapping power run | 1 | OverlappingRun | `overlapping_run` |
| Overlapping run | 3 | OverlappingRun | `overlapping_run` |
| PenaltyMastery | 1 | PenaltySpecialist | `dead_ball_specialist` |
| PenaltySpecialist | 1 | PenaltySpecialist | `dead_ball_specialist` |
| Physical Block | 1 | PhysicalBlock | `defensive_block` |
| Physical Blocking | 1 | PhysicalBlock | `defensive_block` |
| Physical Shielding | 3 | PhysicalShielding | `hold_up_play` |
| Physical shielding | 1 | PhysicalShielding | `hold_up_play` |
| PhysicalFriction | 1 | PhysicalFriction | `hold_up_play` |
| PhysicalShielding | 2 | PhysicalShielding | `hold_up_play` |
| PhysicalShieldingOfBall | 1 | PhysicalShielding | `hold_up_play` |
| Pinpoint cross | 1 | PrecisionCross | `precision_cross` |
| PocketPenetration | 1 | PocketPenetration | `half_space_movement` |
| PocketReceive | 1 | PocketReceive | `pocket_receive` |
| Power Header Goal | 1 | SetPieceHeader | `header_finish` |
| PowerCarryThroughCenter | 1 | PowerCarryThroughCenter | `progressive_carry` |
| PowerDrive | 1 | PowerDrive | `progressive_carry` |
| PowerProgressiveCarry | 1 | PowerProgressiveCarry | `progressive_carry` |
| PowerShot | 1 | PowerShot | `long_range_strike` |
| PreciseFinishing | 1 | ClinicalFinishing | `clinical_finishing` |
| Precision cross | 1 | PrecisionCross | `precision_cross` |
| PrecisionCurler | 1 | CurlFinesseShot | `curve_shot` |
| PrecisionShot | 4 | PrecisionShot | `clinical_finishing` |
| PrecisionThroughBall | 1 | PrecisionThroughBall | `line_breaking_pass` |
| Press Resistance | 3 | PressResistance | `press_resistance` |
| PressResistance | 18 | PressResistance | `press_resistance` |
| Pressing | 5 | Pressing | `high_press` |
| PressingDuels | 1 | PressingDuels | `high_press` |
| Progressive Carry | 8 | ProgressiveCarry | `progressive_carry` |
| Progressive Carry / Pass | 1 | ProgressiveCarry | `progressive_carry` |
| Progressive Pass | 6 | ProgressivePass | `progressive_pass` |
| Progressive Power Carry | 2 | ProgressiveCarry | `progressive_carry` |
| ProgressiveCarry | 17 | ProgressiveCarry | `progressive_carry` |
| ProgressiveDrive | 1 | ProgressiveDrive | `progressive_carry` |
| ProgressivePass | 12 | ProgressivePass | `progressive_pass` |
| ProgressivePassing | 1 | ProgressivePass | `progressive_pass` |
| RainbowFlickDribble | 1 | RainbowFlickDribble | `flair_dribble` |
| Recovery Run | 6 | RecoveryRun | `recovery_sprint` |
| Recovery Run Sprint | 1 | RecoveryRun | `recovery_sprint` |
| Recovery defensive sprint | 1 | RecoveryRun | `recovery_sprint` |
| Recovery sprint | 4 | RecoveryRun | `recovery_sprint` |
| Set-Piece Header | 2 | SetPieceHeader | `header_finish` |
| SidelineWorkrate | 1 | SidelineWorkrate | `ball_recovery` |
| SkillfulDribble | 1 | SkillfulDribble | `close_control_dribble` |
| SlalomBoxDribble | 1 | SlalomBoxDribble | `close_control_dribble` |
| SpeedBurst | 1 | ExplosiveBurst | `explosive_burst` |
| StrikingFromDistance | 1 | LongRangeStrike | `long_range_strike` |
| SuddenChangeOfPace | 1 | SuddenChangeOfPace | `turn_and_accelerate` |
| Support Run | 9 | SupportRun | `support_run` |
| SupportRun | 2 | SupportRun | `support_run` |
| Surgery Long Pass | 1 | LongDiagonalPass | `long_switch` |
| Surgical Long Diagonal | 1 | LongDiagonalPass | `long_switch` |
| Switch of Play | 4 | SwitchOfPlay | `long_switch` |
| Switch of play | 2 | SwitchOfPlay | `long_switch` |
| SwitchOfPlay | 1 | SwitchOfPlay | `long_switch` |
| Tactical Interception | 1 | CleanInterception | `interception_reading` |
| TacticalFoul | 2 | TacticalFoul | (diferida) |
| Technical drag-back | 1 | DragBack | `flair_dribble` |
| TempoControl | 3 | TempoControl | `build_up_play` |
| TightSpaceDribble | 1 | CloseControlDribble | `close_control_dribble` |
| TransitionSprint | 1 | TransitionSprint | `counter_sprint` |
| Turbo-carry | 1 | ProgressiveCarry | `progressive_carry` |
| TurnAndAccelerate | 8 | TurnAndAccelerate | `turn_and_accelerate` |
| Vertical Aerial Leap | 1 | AerialDominance | `aerial_duel` |
| VerticalPass | 1 | VerticalPass | `progressive_pass` |
| VisionaryThroughBall | 1 | VisionaryThroughBall | `line_breaking_pass` |
| Wide Cover | 6 | WideCover | `defensive_screen` |
| Wide Defensive Cover | 1 | WideCover | `defensive_screen` |
| Wide Defensive Recovery | 1 | WideRecovery | `recovery_sprint` |
| Wide Progression | 2 | WideProgression | `progressive_pass` |
| Wide Recovery Run | 1 | WideRecovery | `recovery_sprint` |
| WingBurst | 1 | WingBurst | `explosive_burst` |
| WingCutInside | 1 | WingCutInside | `cut_inside` |
| ZonalSlalomDribble | 1 | ZonalSlalomDribble | `close_control_dribble` |

## 3. Skills por jugador de campo
| Jugador (id) | Nombre publicado | Roles oficiales | Skills |
|---|---|---|---|
| `cb-pierre-kalulu` | Bastien Jouvet | Guardian/Anchor/Wall | recovery_sprint, aggressive_tackle, defensive_screen |
| `cb-jonathan-tah` | Gerrit Thalmann | Wall/Anchor/Guardian | aerial_duel, defensive_block, hold_up_play |
| `cb-jules-kounde` | Florian Dupuis | Guardian/Wall/Anchor | interception_reading, aerial_duel, progressive_carry |
| `cb-marc-pubill` | Damián Toledano | Wall/Guardian/Anchor | aerial_duel, defensive_screen, hold_up_play |
| `cb-harry-maguire` | Lachlan Garrick | Wall/Anchor/Guardian | aerial_duel, defensive_block |
| `cb-dayot-upamecano` | Rémi Gerbier | Guardian/Wall/Anchor | interception_reading, recovery_sprint, progressive_carry |
| `cb-matthijs-de-ligt` | Olaf Peeters | Wall/Anchor/Guardian | defensive_block, aerial_duel, aggressive_tackle |
| `cb-jon-aramburu` | Dante Naranjo | Guardian/Anchor/Wall | aggressive_tackle, recovery_sprint, high_press |
| `cb-kalidou-koulibaly` | Kalidou Ndiaye | Wall/Anchor/Guardian | aerial_duel, progressive_pass, hold_up_play |
| `cb-ruben-dias` | Ulisses Eusébio | Anchor/Wall/Guardian | defensive_screen, interception_reading, progressive_pass |
| `cb-pau-cubarsi` | Nicanor Ferreiro | Anchor/Guardian/Wall | long_switch, interception_reading, press_resistance |
| `cb-cristian-romero` | Camilo Funes | Guardian/Wall/Anchor | aggressive_tackle, hold_up_play, long_switch |
| `cb-ronald-araujo` | Ramón Soria | Guardian/Wall/Anchor | recovery_sprint, one_v_one_defending, aerial_duel |
| `cb-marquinhos` | Telmo Evangelista | Anchor/Guardian/Wall | interception_reading, long_switch, header_finish |
| `cb-ezri-konsa` | Isaac Ashworth | Guardian/Anchor/Wall | interception_reading, defensive_screen, press_resistance |
| `cb-virgil-van-dijk` | Koen Everts | Wall/Anchor/Guardian | one_v_one_defending, long_switch, aerial_duel |
| `cb-jurrien-timber` | Arjen Faber | Guardian/Anchor/Wall | interception_reading, progressive_pass, recovery_sprint |
| `cb-antonio-rudiger` | Florian Hellwig | Guardian/Wall/Anchor | aggressive_tackle, recovery_sprint, header_finish |
| `cb-william-saliba` | Yann Salmon | Guardian/Anchor/Wall | one_v_one_defending, press_resistance, recovery_sprint |
| `cb-kim-min-jae` | Changmin Hong | Guardian/Wall/Anchor | interception_reading, recovery_sprint, aerial_duel |
| `cb-riccardo-calafiori` | Piero Faccini | Guardian/Anchor/Wall | progressive_carry |
| `cb-willian-pacho` | Diego Quiroga | Guardian/Wall/Anchor | one_v_one_defending, recovery_sprint |
| `cb-dean-huijsen` | Rodrigo Narváez | Wall/Anchor/Guardian | aerial_duel, progressive_pass |
| `cb-sven-botman` | Maarten Eijkelboom | Wall/Anchor/Guardian | aerial_duel, defensive_block |
| `cb-lucas-hernandez` | Fabrice Lemaire | Guardian/Anchor/Wall | aggressive_tackle |
| `cb-aymeric-laporte` | Eloy Hontoria | Anchor/Wall/Guardian | long_switch, interception_reading |
| `cb-daley-blind` | Rutger Nijhuis | Anchor/Guardian/Wall | progressive_pass, interception_reading |
| `cb-david-hancko` | Ilija Ilić | Guardian/Anchor/Wall | defensive_screen |
| `cb-micky-van-de-ven` | Lodewijk Jonker | Guardian/Wall/Anchor | recovery_sprint |
| `cb-piero-hincapie` | Wilmer Ramos | Guardian/Anchor/Wall | aggressive_tackle, defensive_screen |
| `cb-lisandro-martinez` | Patricio Vallejos | Guardian/Anchor/Wall | aggressive_tackle |
| `cb-josko-gvardiol` | Hamza Vlahović | Guardian/Anchor/Wall | progressive_carry |
| `cb-alessandro-bastoni` | Bruno Tagliaferri | Anchor/Guardian/Wall | progressive_pass |
| `cb-renato-veiga` | Hugo Aragão | Wall/Guardian/Anchor | header_finish |
| `cb-calvin-bassey` | Tetteh Jegede | Guardian/Wall/Anchor | defensive_screen |
| `cb-marc-guehi` | Jonty Nuttall | Guardian/Anchor/Wall | interception_reading |
| `cb-gerard-martin` | Ramiro Maldonado | Anchor/Guardian/Wall | defensive_screen |
| `cb-gabriel-magalhaes` | Duarte Zagalo | Wall/Guardian/Anchor | aerial_duel, header_finish |
| `cb-nico-schlotterbeck` | Torsten Detmers | Anchor/Wall/Guardian | long_switch |
| `cb-castello-lukeba` | Gaspard Aubry | Guardian/Anchor/Wall | high_press, aggressive_tackle |
| `fb-achraf-hakimi` | Ghali Ouazzani | Winger/Explosive/Engine | overlapping_run, creative_pass, recovery_sprint |
| `fb-trent-alexander-arnold` | Nolan Holloway | Creator/Builder/Engine | long_switch, precision_cross, progressive_pass |
| `fb-denzel-dumfries` | Luuk Brouwers | Winger/Wall/Engine | header_finish, hold_up_play, overlapping_run |
| `fb-pedro-porro` | Mauro Salcedo | Creator/Builder/Winger | precision_cross, long_range_strike, overlapping_run |
| `fb-dani-munoz` | Ismael Escalante | Engine/Guardian/Winger | high_press, late_box_arrival, recovery_sprint |
| `fb-nuno-mendes` | Nuno Teles | Explosive/Winger/Guardian | elusive_dribble, creative_pass, recovery_sprint |
| `fb-federico-dimarco` | Pellegrino Mazzanti | Creator/Builder/Winger | precision_cross, long_range_strike |
| `fb-marc-cucurella` | Leandro Carrasco | Engine/Guardian/Builder | high_press, one_v_one_defending, interception_reading |
| `fb-alphonso-davies` | Kit Kelsall | Explosive/Winger/Engine | progressive_carry, recovery_sprint, creative_pass |
| `fb-joao-cancelo` | Quirino Valadares | Builder/Creator/Winger | progressive_pass, precision_cross, flair_dribble, long_switch |
| `fb-andy-robertson` | Reuben Redfern | Engine/Winger/Builder | overlapping_run, precision_cross, recovery_sprint |
| `fb-marcos-llorente` | Quico Dorado | Engine/Explosive/Guardian | late_box_arrival, long_range_strike, recovery_sprint |
| `md-oleksandr-zinchenko` | Kamil Beridze | Architect/Builder/Engine | half_space_movement, progressive_pass, long_switch, counter_press |
| `md-valentin-barco` | Lucio Cabrera | Winger/Creator/Engine | progressive_carry, half_space_movement, press_resistance, overlapping_run |
| `md-nathaniel-brown` | Sven Freudenberg | Engine/Builder/Winger | progressive_carry, support_run, progressive_pass, high_press |
| `md-myles-lewis-skelly` | Tristan Northcott | Anchor/Engine/Builder | half_space_movement, defensive_screen, progressive_pass, counter_press |
| `md-lewis-hall` | Idris Strickland | Builder/Anchor/Engine | progressive_pass, half_space_movement, defensive_screen, support_run |
| `md-nico-o-reilly` | Fergus Wetherby | Creator/Builder/Engine | progressive_carry, half_space_movement, support_run, press_resistance |
| `md-reece-james` | Kieran Aldous | Anchor/Engine/Builder | half_space_movement, progressive_pass, long_switch, defensive_screen |
| `md-joshua-kimmich` | Rainer Lindner | Architect/Builder/Engine | long_switch, progressive_pass, counter_press, support_run |
| `md-dominik-szoboszlai` | Gheorghe Ionescu | Creator/Engine/Winger | progressive_carry, long_switch, high_press, half_space_movement |
| `md-moises-caicedo` | Nicolás Bordón | Anchor/Engine/Builder | defensive_screen, high_press, recovery_sprint, progressive_pass |
| `md-warren-zaire-emery` | Étienne Delacroix | Engine/Builder/Creator | progressive_carry, support_run, counter_press, half_space_movement |
| `md-federico-valverde` | Agustín Jofré | Engine/Winger/Creator | progressive_carry, progressive_pass, recovery_sprint, high_press |
| `mc-rodri` | Marcelo Landeira | Anchor/Builder/Guardian | build_up_play, interception_reading, progressive_pass, defensive_screen |
| `mc-vitinha` | Tomé Guedes | Builder/Creator/Engine | press_resistance, progressive_pass, counter_press, line_breaking_pass |
| `mc-frenkie-de-jong` | Eelco Kok | Builder/Architect/Engine | progressive_carry, press_resistance, build_up_play, line_breaking_pass |
| `mc-joao-neves` | Aurélio Gouveia | Engine/Builder/Anchor | ball_recovery, counter_press, interception_reading, progressive_pass |
| `mc-hakan-calhanoglu` | Hakan Gökçe | Architect/Builder/Creator | long_switch, progressive_pass, build_up_play, interception_reading |
| `mc-elliot-anderson` | Declan Ellerby | Engine/Builder/Creator | progressive_carry, progressive_pass, counter_press, support_run |
| `mc-ryan-gravenberch` | Jelle Hoekstra | Builder/Engine/Anchor | progressive_carry, interception_reading, build_up_play, defensive_screen |
| `mc-bruno-guimaraes` | Plácido Ferrão | Creator/Builder/Engine | progressive_pass, ball_recovery, press_resistance, counter_press |
| `mc-casemiro` | Leonel Ilharco | Wall/Anchor/Guardian | interception_reading, ball_recovery, defensive_screen, aerial_duel |
| `mc-leandro-paredes` | Elías Olivera | Anchor/Builder/Architect | long_switch, progressive_pass, build_up_play, press_resistance |
| `mq-fabian-ruiz` | Gael Yáñez | Builder/Creator/Engine | build_up_play, progressive_pass, long_switch, press_resistance |
| `mq-pedri` | Teodoro Tejedor | Creator/Builder/Architect | line_breaking_pass, press_resistance, turn_and_accelerate, progressive_carry |
| `mq-enzo-fernandez` | Uriel Fierro | Architect/Builder/Creator | long_switch, progressive_pass, line_breaking_pass, build_up_play |
| `mq-declan-rice` | Nigel Blackwood | Anchor/Engine/Builder | ball_recovery, progressive_carry, defensive_screen, interception_reading |
| `mq-alexis-mac-allister` | Genaro Campos | Builder/Engine/Creator | progressive_pass, counter_press, ball_recovery, press_resistance |
| `mq-sandro-tonali` | Ippolito Vitale | Engine/Builder/Anchor | counter_press, long_switch, progressive_pass, ball_recovery |
| `mq-luka-modric` | Nikola Oprea | Architect/Creator/Builder | creative_pass, press_resistance, build_up_play, line_breaking_pass |
| `mq-nicolo-barella` | Vittorio Lombardi | Engine/Builder/Creator | counter_press, late_box_arrival, progressive_carry, line_breaking_pass |
| `mq-kobbie-mainoo` | Barnaby Denholm | Builder/Creator/Engine | press_resistance, progressive_carry, line_breaking_pass, build_up_play |
| `mq-scott-mctominay` | Finlay Whitcombe | Finisher/Engine/Wall | late_box_arrival, header_finish, progressive_carry, defensive_screen |
| `mo-jude-bellingham` | Wesley Egerton | Creator/Engine/Finisher | late_box_arrival, progressive_carry, press_resistance, line_breaking_pass |
| `mo-cole-palmer` | Orrin Irvine | Creator/Builder/Architect | line_breaking_pass, clinical_finishing, press_resistance, turn_and_accelerate |
| `mo-martin-odegaard` | Ivar Sandvik | Architect/Creator/Builder | line_breaking_pass, high_press, build_up_play, half_space_movement |
| `mo-bruno-fernandes` | Joel Amaral | Creator/Architect/Engine | progressive_pass, long_switch, counter_press, clinical_finishing |
| `mo-jamal-musiala` | Jens Zimmerer | Explosive/Creator/Builder | progressive_carry, close_control_dribble, line_breaking_pass, turn_and_accelerate |
| `mo-isco` | Fabián Bermejo | Creator/Architect/Builder | close_control_dribble, press_resistance, line_breaking_pass, turn_and_accelerate |
| `mo-marco-reus` | Dietmar Immel | Creator/Finisher/Builder | clinical_finishing, line_breaking_pass, late_box_arrival, dead_ball_specialist |
| `mo-kevin-de-bruyne` | Noé Besnard | Architect/Creator/Builder | precision_cross, progressive_pass, long_switch, clinical_finishing |
| `mo-florian-wirtz` | Philipp Nowack | Creator/Architect/Explosive | line_breaking_pass, progressive_carry, press_resistance, turn_and_accelerate |
| `mo-gilberto-mora` | Nahuel Troncoso | Creator/Builder/Explosive | progressive_carry, line_breaking_pass, press_resistance, turn_and_accelerate |
| `wg-bukayo-saka` | Percy Ashcroft | Winger/Creator/Engine | progressive_carry, cut_inside, line_breaking_pass, defensive_screen |
| `wg-giuliano-simeone` | Orlando Ibáñez | Engine/Winger/Finisher | high_press, half_space_movement, ball_recovery, counter_press |
| `wg-desire-doue` | Gautier Lacombe | Explosive/Creator/Builder | progressive_carry, close_control_dribble, turn_and_accelerate |
| `wg-estevao` | Rolando Faria | Explosive/Creator/Winger | close_control_dribble, cut_inside, progressive_carry, creative_pass |
| `wg-antoine-semenyo` | Ebuka Osei | Explosive/Finisher/Engine | progressive_carry, cut_inside, hold_up_play, counter_sprint |
| `wg-mohamed-salah` | Elias Taher | Finisher/GoalHunter/Winger | half_space_movement, clinical_finishing, cut_inside, late_box_arrival |
| `wg-christian-pulisic` | Rhys Yardley | Winger/Explosive/Finisher | explosive_burst, progressive_carry, late_box_arrival, elusive_dribble |
| `wg-kaoru-mitoma` | Kenta Fujimoto | Winger/Explosive/Creator | explosive_burst, elusive_dribble, precision_cross, cut_inside |
| `wg-anthony-gordon` | Dermot Eastwood | Winger/Engine/Explosive | high_press, explosive_burst, progressive_carry, counter_sprint |
| `wg-vinicius-junior` | Ivo Furtado | Explosive/Winger/Finisher | elusive_dribble, explosive_burst, cut_inside, progressive_carry |
| `wg-bradley-barcola` | Zacharie Ollivier | Winger/Explosive/Finisher | explosive_burst, counter_sprint, cut_inside, elusive_dribble |
| `wg-luis-diaz` | Franco Medina | Explosive/Winger/Engine | close_control_dribble, high_press, counter_sprint, half_space_movement |
| `sd-paulo-dybala` | Thiago Altamirano | Creator/Architect/Finisher | pocket_receive, curve_shot, progressive_pass |
| `sd-lionel-messi` | Valentino Gallardo | Creator/Architect/Finisher | line_breaking_pass, close_control_dribble, clinical_finishing, half_space_movement |
| `sd-michael-olise` | Jules Thibault | Creator/Architect/Explosive | progressive_pass, curve_shot, creative_pass, elusive_dribble |
| `sd-lamine-yamal` | Pascual Cordero | Creator/Winger/Explosive | close_control_dribble, late_box_arrival, curve_shot, line_breaking_pass |
| `sd-morgan-rogers` | Alistair Gilchrist | Engine/Explosive/Creator | progressive_carry, hold_up_play, creative_pass |
| `sd-fermin-lopez` | Xabier Pellicer | Engine/Finisher/GoalHunter | high_press, late_box_arrival, long_range_strike, half_space_movement |
| `sd-raphinha` | Sérgio Lobato | Explosive/Winger/Finisher | high_press, half_space_movement, counter_sprint, long_range_strike |
| `sd-neymar-jr` | Mário Rangel | Creator/Explosive/Architect | flair_dribble, creative_pass, close_control_dribble, half_space_movement |
| `sd-khvicha-kvaratskhelia` | Vasyl Andrić | Explosive/Winger/Creator | close_control_dribble, cut_inside, progressive_carry, creative_pass |
| `sd-ousmane-dembele` | Dorian Navarre | Explosive/Winger/Creator | explosive_burst, turn_and_accelerate, cut_inside, creative_pass |
| `dc-harry-kane` | Oakley Gresham | Finisher/Architect/Target | build_up_play, clinical_finishing, long_range_strike, line_breaking_pass |
| `dc-kai-havertz` | Hannes Neumeier | Finisher/Target/Builder | aerial_duel, support_run, high_press, build_up_play |
| `dc-julian-alvarez` | Lautaro Zalazar | Finisher/Engine/GoalHunter | high_press, clinical_finishing, recovery_sprint, late_box_arrival |
| `dc-lautaro-martinez` | Bruno Duarte | Finisher/GoalHunter/Engine | hold_up_play, first_time_finish, high_press, late_box_arrival |
| `dc-kylian-mbappe` | Quentin Rochefort | GoalHunter/Explosive/Finisher | explosive_burst, half_space_movement, clinical_finishing, counter_sprint |
| `dc-erling-haaland` | Gustav Wallin | GoalHunter/Finisher/Target | explosive_burst, first_time_finish, hold_up_play, header_finish |
| `dc-joao-pedro` | Edmundo Rebelo | Creator/Finisher/Builder | close_control_dribble, dead_ball_specialist, build_up_play |
| `dc-viktor-gyokeres` | Emil Holmgren | GoalHunter/Finisher/Target | progressive_carry, hold_up_play, clinical_finishing |
| `dc-alexander-isak` | Karl Ulvestad | Explosive/Finisher/GoalHunter | close_control_dribble, clinical_finishing, turn_and_accelerate, counter_sprint |
| `dc-donyell-malen` | Ruud Bosman | Explosive/Finisher/Winger | cut_inside, explosive_burst, clinical_finishing, counter_sprint |
| `dc-cristiano-ronaldo` | Gilberto Bettencourt | GoalHunter/Finisher/Target | dead_ball_specialist, header_finish, first_time_finish, support_run |
| `dc-troy-parrott` | Vaughn Cresswell | Finisher/Target/Builder | build_up_play, late_box_arrival, high_press, clinical_finishing |
| `dc-francesco-pio-esposito` | Matteo Lunardi | Target/Finisher/GoalHunter | aerial_duel, hold_up_play, header_finish |
| `dc-mikel-oyarzabal` | Severo Abellán | Creator/Finisher/Engine | pocket_receive, clinical_finishing, build_up_play, ball_recovery |
| `dc-victor-osimhen` | Dayo Eze | GoalHunter/Explosive/Finisher | counter_sprint, aerial_duel, progressive_carry, high_press |
| `dc-romelu-lukaku` | Loïc Deschamps | Target/Finisher/GoalHunter | hold_up_play, long_range_strike |
| `dc-chris-wood` | Warren Fairbanks | Target/Finisher/GoalHunter | header_finish, first_time_finish, hold_up_play, late_box_arrival |
| `dc-pierre-emerick-aubameyang` | Jean-Luc Samb | Explosive/GoalHunter/Finisher | counter_sprint, clinical_finishing, half_space_movement |
| `dc-karim-adeyemi` | Norbert Dorfmeister | Explosive/Winger/Finisher | counter_sprint, cut_inside, explosive_burst, clinical_finishing |
| `dc-vedat-muriqi` | Hristo Stanković | Target/Finisher/Engine | aerial_duel, hold_up_play |

## 4. Cuántos jugadores tiene cada skill
| Skill | Jugadores que la tienen |
|---|---|
| `close_control_dribble` | 11 |
| `elusive_dribble` | 6 |
| `flair_dribble` | 2 |
| `progressive_pass` | 31 |
| `line_breaking_pass` | 19 |
| `long_switch` | 19 |
| `creative_pass` | 10 |
| `build_up_play` | 15 |
| `precision_cross` | 7 |
| `clinical_finishing` | 16 |
| `first_time_finish` | 4 |
| `long_range_strike` | 7 |
| `curve_shot` | 3 |
| `header_finish` | 10 |
| `dead_ball_specialist` | 3 |
| `pocket_receive` | 2 |
| `hold_up_play` | 14 |
| `explosive_burst` | 10 |
| `counter_sprint` | 11 |
| `turn_and_accelerate` | 9 |
| `progressive_carry` | 34 |
| `press_resistance` | 19 |
| `cut_inside` | 11 |
| `support_run` | 8 |
| `overlapping_run` | 5 |
| `half_space_movement` | 18 |
| `late_box_arrival` | 14 |
| `interception_reading` | 18 |
| `aggressive_tackle` | 9 |
| `one_v_one_defending` | 5 |
| `defensive_block` | 4 |
| `defensive_screen` | 18 |
| `high_press` | 19 |
| `counter_press` | 13 |
| `ball_recovery` | 8 |
| `recovery_sprint` | 19 |
| `aerial_duel` | 17 |

## 5. Lo que NO se dio, y por qué (nada es silencioso)
Un jugador que supera 4 skills conserva las primeras según el orden de su ficha; las demás quedan como `TrimmedByCap`. `RejectedRequirement` es un atributo por debajo del mínimo de la skill. `FallbackAssigned` es el único jugador que no tenía ninguna etiqueta utilizable.

| Jugador | Skill | Qué pasó | Motivo |
|---|---|---|---|
| `cb-harry-maguire` | `progressive_carry` | RejectedRequirement | Dribbling 68 is below 70. |
| `cb-renato-veiga` | `header_finish` | FallbackAssigned | No skill from his tags; derived from his attributes and roles. |
| `md-oleksandr-zinchenko` | `support_run` | TrimmedByCap | More than 4 skills; left out. |
| `md-valentin-barco` | `support_run` | TrimmedByCap | More than 4 skills; left out. |
| `md-nathaniel-brown` | `half_space_movement` | TrimmedByCap | More than 4 skills; left out. |
| `md-myles-lewis-skelly` | `support_run` | TrimmedByCap | More than 4 skills; left out. |
| `md-lewis-hall` | `recovery_sprint` | TrimmedByCap | More than 4 skills; left out. |
| `md-reece-james` | `press_resistance` | TrimmedByCap | More than 4 skills; left out. |
| `md-joshua-kimmich` | `half_space_movement` | TrimmedByCap | More than 4 skills; left out. |
| `md-dominik-szoboszlai` | `support_run` | TrimmedByCap | More than 4 skills; left out. |
| `md-moises-caicedo` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `md-warren-zaire-emery` | `recovery_sprint` | TrimmedByCap | More than 4 skills; left out. |
| `md-federico-valverde` | `late_box_arrival` | TrimmedByCap | More than 4 skills; left out. |
| `mc-rodri` | `press_resistance` | TrimmedByCap | More than 4 skills; left out. |
| `mc-vitinha` | `build_up_play` | TrimmedByCap | More than 4 skills; left out. |
| `mc-frenkie-de-jong` | `defensive_screen` | TrimmedByCap | More than 4 skills; left out. |
| `mc-joao-neves` | `defensive_screen` | TrimmedByCap | More than 4 skills; left out. |
| `mc-hakan-calhanoglu` | `press_resistance` | TrimmedByCap | More than 4 skills; left out. |
| `mc-elliot-anderson` | `press_resistance` | TrimmedByCap | More than 4 skills; left out. |
| `mc-ryan-gravenberch` | `long_switch` | TrimmedByCap | More than 4 skills; left out. |
| `mc-bruno-guimaraes` | `defensive_screen` | TrimmedByCap | More than 4 skills; left out. |
| `mq-fabian-ruiz` | `late_box_arrival` | TrimmedByCap | More than 4 skills; left out. |
| `mq-pedri` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mq-enzo-fernandez` | `press_resistance` | TrimmedByCap | More than 4 skills; left out. |
| `mq-declan-rice` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mq-alexis-mac-allister` | `late_box_arrival` | TrimmedByCap | More than 4 skills; left out. |
| `mq-sandro-tonali` | `late_box_arrival` | TrimmedByCap | More than 4 skills; left out. |
| `mq-luka-modric` | `progressive_carry` | TrimmedByCap | More than 4 skills; left out. |
| `mq-nicolo-barella` | `ball_recovery` | TrimmedByCap | More than 4 skills; left out. |
| `mq-kobbie-mainoo` | `turn_and_accelerate` | TrimmedByCap | More than 4 skills; left out. |
| `mq-scott-mctominay` | `ball_recovery` | TrimmedByCap | More than 4 skills; left out. |
| `mo-jude-bellingham` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mo-cole-palmer` | `half_space_movement` | TrimmedByCap | More than 4 skills; left out. |
| `mo-martin-odegaard` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mo-bruno-fernandes` | `late_box_arrival` | TrimmedByCap | More than 4 skills; left out. |
| `mo-jamal-musiala` | `late_box_arrival` | TrimmedByCap | More than 4 skills; left out. |
| `mo-isco` | `build_up_play` | TrimmedByCap | More than 4 skills; left out. |
| `mo-marco-reus` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mo-kevin-de-bruyne` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mo-florian-wirtz` | `counter_press` | TrimmedByCap | More than 4 skills; left out. |
| `mo-gilberto-mora` | `support_run` | TrimmedByCap | More than 4 skills; left out. |
