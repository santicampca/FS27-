# FS27 — Skills reales (Fase 6)

Estado: **implementado en el Core, probado, sin Unity.** Rama `phase-6-real-skills`, partiendo exactamente de DATA LOCK (`37e4c81`, 1951/1951 tests).
Esta fase convierte el catálogo de 37 skills (datos y permiso en la Fase 5) en **comportamiento real dentro del sistema de acciones de la Fase 4**. No existe un segundo sistema: se extienden `SkillDefinition`, `PlayerSkillSet`, `SkillCatalog`, `ActionRequest → MatchController → BallSimulator → ActionResult`.

Leyenda de decisiones: **(DECISIÓN FASE 6)** = valor o criterio mío, no especificado; está en `SkillDefaults`/`SkillTuning` y se revisa en el pase de balance.

---

## 1. Arquitectura

```
FS27_SKILLS_V1.json ──► SkillCatalog / SkillDefinition       (CONFIGURACIÓN: id, requisitos, roles, acción que desbloquea)
GameDatabase ─────────► PlayerSkillSet (por jugador)          (LO QUE POSEE el jugador; no está en PlayerDefinition)
SkillBindings (tabla) ► SkillBinding por skill                (estado REAL/PERMISSION_ONLY/DEFERRED, efecto, acciones, de qué atributos se compone la maestría)
SkillResolver.Resolve ► ResolvedSkills (por jugador, 1 vez al crear el partido)
                         │  Has(efecto) · Strength(efecto) 0..1 · Ignored (lista, nunca silencio)
MatchController ──────► CanUseAction(...)  ◄── ÚNICA fuente de verdad del permiso
Perform(request) ─────► MatchController.Actions.cs / MatchController.Skills.cs
                         (la skill modifica CÓMO se resuelve la acción) ─► BallSimulator ─► MatchState ─► ActionResult
```

| Pieza | Archivo | Qué es |
|---|---|---|
| Contratos (`SkillDefinition`, `PlayerSkillSet`, `SkillKind`, `SkillCategory`) | `Core/Skills/SkillContracts.cs` | Sin cambios de forma: la skill sigue siendo configuración. |
| Catálogo y asignación | `Core/Skills/SkillCatalog.cs` | Sin cambios (`SkillCatalog`, `PlayerSkillCatalog`, `SkillAssigner`). |
| Tabla de comportamiento | `Core/Skills/SkillBindings.cs` | **Nuevo.** `SkillImplementation`, `SkillEffect` (26), `SkillBinding`, `SkillBindings`. Una fila por skill del catálogo (test lo exige). |
| Resolutor | `Core/Skills/SkillResolver.cs` | **Nuevo.** `ResolvedSkills`, `SkillPermission`, `LocomotionModifiers`, `SkillResolver.Resolve / CanUseAction / MasteryOf / MeetsRequirements / LocomotionOf`. Funciones puras, sin azar, sin Unity. |
| Números | `Core/Skills/SkillDefaults.cs` | **Nuevo.** `SkillDefaults` (constantes) + `SkillTuning` (copia editable); un test garantiza gemelos 1:1. |
| Integración en el motor | `Core/Match/MatchController.Skills.cs` | **Nuevo** (parcial). Aplica las skills dentro de cada acción. Sin números sueltos (test). |
| Puntos de enganche | `MatchController.Actions.cs`, `MatchController.cs`, `MatchRoster.cs`, `LocomotionStepper.cs` | Cambios mínimos: pasar por el resolutor, sumar términos que valen 0 sin skill. |

El test `SkillCatalogue_UsesTheExistingContracts_NoSecondSystem` sigue vigente: la carpeta `Skills/` pasa de 2 a 5 archivos, todos del **mismo** sistema (un test comprueba que no existen `SkillSystem`, segundo `SkillCatalog`, segundo `BallSimulator` ni `ActionModel`).

### Reglas que se cumplen (y están en tests)
- **Una skill nunca cambia atributos, BaseOverall, EffectiveOverall, GRL ni Coach Boost.** Solo cambia cómo se resuelve una acción. `ResolvedSkills` no guarda atributos (test por reflexión) y un partido real con muchas acciones deja atributos y Overall idénticos.
- **Un requisito es una puerta, no un bonus.** Si faltan atributos, la skill no está activa (`Ignored: RequirementNotMet`). Superar el mínimo no da nada por sí mismo: el efecto depende de la **maestría**.
- **Maestría (0..1)** = media ponderada de atributos oficiales (pesos en `SkillBindings`, suman 1, solo los 12 atributos). **(DECISIÓN FASE 6: los pesos.)**
- **Determinismo.** Solo el generador del partido (`SeedMixer.Create(seed, 4)`), en el orden en que ocurren las acciones. El regate con skill consume **un** número aleatorio; el resto de skills no consumen ninguno (modifican la fórmula que ya tiraba el suyo). Sin skills, el resultado es **exactamente** el de la Fase 4 (test `ThePhase4Contracts_AreIntact…`).
- **Stamina.** Solo por `SpendStamina` (el coste centralizado existente). Nunca < 0, se recupera como siempre. Skill actions cuestan algo más que su acción normal (`CurvedShotExtraStamina`, `LobExtraStamina`, `MoveStaminaCost`).
- **Nunca un tirador perfecto.** (a) El bonus de calidad de una skill se detiene en `QualityCeiling` (0.95); (b) el tiro con efecto tiene un error residual (`CurveResidualErrorDegrees`) que ninguna maestría elimina; (c) el ruido sigue ahí.

---

## 2. Permiso: `CanUseAction` (una sola fuente)

`MatchController.CanUseAction(side, playerId, action, skillId = null, callerGrant = false)` → `SkillPermission { Allowed, Rejection, Effect, SkillId }`, implementado por `SkillResolver.CanUseAction`. **`Perform` usa exactamente esa función**; nadie más devuelve `SkillNotGranted` (test que escanea todo el Core).

- Acciones normales: siempre permitidas aquí.
- `CurvedShot`, `SkillMove`, `SkillLobPass`: necesitan una skill cuyo efecto esté en la lista de esa acción (`SkillBindings.EffectsUnlocking`).
- `SkillMove` además necesita el balón (`NotInPossession`).
- `SkillId` pide un estilo concreto (`close_control_dribble`, `flair_dribble`, `long_switch`…). Vacío = el de mayor maestría; empate = el primero en orden de catálogo (determinista). Un estilo que no posee → `SkillNotGranted`.
- **`ActionRequest.SkillGranted` (legado de la Fase 4)**: solo lo respeta un partido creado **sin datos de skills** (`IsAuthoritative = false`). Un partido que conoce las skills de sus jugadores lo **ignora**: las skills son el único permiso. Así se elimina la duplicación sin romper los 100 tests de acciones.
- `PlayerSkillCatalog.UnlockedActions` / `GameDatabase.UnlockedActions` (datos) y `CanUseAction` (motor) dan el mismo resultado para los 136 jugadores reales (test).
- **Botón C** (skill) **(DECISIÓN FASE 6)**: con balón, C toque = `SkillMove`, C deslizar arriba = `SkillLobPass`, C deslizar abajo = `CurvedShot`. Sin balón o portero en portería: `None`. `ActionInput.Curve` (−1..1) da el efecto; si es 0, `SkillDefaults.DefaultCurve` con el signo del pie preferido.

---

## 3. Catálogo de 37 skills (REAL / PERMISSION_ONLY / DEFERRED)

Totales: **25 REAL · 0 PERMISSION_ONLY · 12 DEFERRED.**
Ninguna skill es solo-permiso: las 6 que desbloquean una acción (3 de `SkillMove`, 2 de `SkillLobPass`, 1 de `CurvedShot`) cambian además cómo se resuelve. "Dueños reales" = jugadores de campo (de 136) que la tienen activa tras resolver.

| # | Skill | Familia | Acción | Estado | Requisitos (puerta) | Dueños reales | Tests |
|---|---|---|---|---|---|---|---|
| 1 | `close_control_dribble` | Balón | SkillMove | **REAL** | Control ≥ 74, Dribbling ≥ 74 | 11 | SkillMove_* (14) |
| 2 | `elusive_dribble` | Balón | SkillMove | **REAL** | Agility ≥ 74, Dribbling ≥ 74 | 6 | SkillMove_* (14) |
| 3 | `flair_dribble` | Balón | SkillMove | **REAL** | Dribbling ≥ 76, Technique ≥ 76 | 2 | SkillMove_* (14) |
| 4 | `progressive_pass` | Balón | ShortPass, LongPass, ThroughPass | **REAL** | Passing ≥ 74 | 31 | ProgressivePass_HelpsForwardPassesOnly |
| 5 | `line_breaking_pass` | Balón | ThroughPass | **REAL** | Passing ≥ 76, Technique ≥ 74 | 19 | LineBreakingPass_ImprovesTheThroughBall… |
| 6 | `long_switch` | Balón | SkillLobPass | **REAL** | Passing ≥ 76, Technique ≥ 74 | 19 | LobPass_* (6) |
| 7 | `creative_pass` | Balón | SkillLobPass | **REAL** | Passing ≥ 72, Technique ≥ 74 | 10 | LobPass_* (6) |
| 8 | `build_up_play` | Balón | ShortPass, WallPass | **REAL** | Control ≥ 72, Passing ≥ 72 | 15 | BuildUpPlay_HelpsTheShortGame… |
| 9 | `precision_cross` | Balón | — | **DEFERRED** | Passing ≥ 72, Technique ≥ 72 | 7 | DEFERRED: EveryDeferredSkill_SaysWhy…, ADeferredSkill_ChangesNothing… |
| 10 | `clinical_finishing` | Balón | Shot, CurvedShot | **REAL** | Finishing ≥ 76 | 16 | ClinicalFinishing_HelpsCloseShots… |
| 11 | `first_time_finish` | Balón | Shot, CurvedShot | **REAL** | Finishing ≥ 74 | 4 | FirstTimeFinish_OnlyHelps… |
| 12 | `long_range_strike` | Balón | Shot, CurvedShot | **REAL** | Shooting ≥ 76 | 7 | ClinicalFinishing_HelpsCloseShots_And… (long range) |
| 13 | `curve_shot` | Balón | CurvedShot | **REAL** | Shooting ≥ 74, Technique ≥ 76 | 3 | CurvedShot_* (11) |
| 14 | `header_finish` | Balón | — | **DEFERRED** | Strength ≥ 70 | 10 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 15 | `dead_ball_specialist` | Balón | — | **DEFERRED** | Shooting ≥ 74, Technique ≥ 76 | 3 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 16 | `pocket_receive` | Balón | Control | **REAL** | Control ≥ 76, Technique ≥ 74 | 2 | PocketReceive_HelpsTheFirstTouch… |
| 17 | `hold_up_play` | Balón | Shield, Tackle, PhysicalChallenge | **REAL** | Control ≥ 72, Strength ≥ 72 | 14 | HoldUpPlay_MakesAShieldingCarrier… |
| 18 | `explosive_burst` | Movimiento | locomoción | **REAL** | Acceleration ≥ 80 | 10 | ExplosiveBurst_GetsHimGoingFaster… |
| 19 | `counter_sprint` | Movimiento | — | **DEFERRED** | Speed ≥ 80 | 11 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 20 | `turn_and_accelerate` | Movimiento | locomoción | **REAL** | Acceleration ≥ 74, Agility ≥ 76 | 9 | TurnAndAccelerate_TurnsFaster… |
| 21 | `progressive_carry` | Movimiento | Dribble | **REAL** | Dribbling ≥ 70 | 34 | ProgressiveCarry_KeepsTheBallBetter… |
| 22 | `press_resistance` | Movimiento | Dribble | **REAL** | Agility ≥ 72, Control ≥ 76 | 19 | PressResistance_RemovesPartOfThePressure… |
| 23 | `cut_inside` | Movimiento | — | **DEFERRED** | Dribbling ≥ 72 | 11 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 24 | `support_run` | Movimiento | — | **DEFERRED** | Stamina ≥ 70 | 8 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 25 | `overlapping_run` | Movimiento | — | **DEFERRED** | Speed ≥ 70, Stamina ≥ 70 | 5 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 26 | `half_space_movement` | Movimiento | — | **DEFERRED** | Agility ≥ 70 | 18 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 27 | `late_box_arrival` | Movimiento | — | **DEFERRED** | Stamina ≥ 70 | 14 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 28 | `interception_reading` | Defensiva | Intercept, Block | **REAL** | Defense ≥ 70 | 18 | InterceptionReading_ReachesAFurtherBall… |
| 29 | `aggressive_tackle` | Defensiva | Tackle, SlidingTackle | **REAL** | Defense ≥ 70 | 9 | AggressiveTackle_WinsMoreDuels… |
| 30 | `one_v_one_defending` | Defensiva | Tackle | **REAL** | Agility ≥ 68, Defense ≥ 72 | 5 | OneVOneDefending_OnlyHelps… |
| 31 | `defensive_block` | Defensiva | Block | **REAL** | Defense ≥ 70 | 4 | DefensiveBlock_BlocksBetter… |
| 32 | `defensive_screen` | Defensiva | Pressure | **REAL** | Defense ≥ 68 | 18 | DefensiveScreen_MakesThePressure… |
| 33 | `high_press` | Defensiva | Pressure | **REAL** | Stamina ≥ 72 | 19 | HighPress_LastsLonger… |
| 34 | `counter_press` | Defensiva | — | **DEFERRED** | Stamina ≥ 72 | 13 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 35 | `ball_recovery` | Defensiva | Control | **REAL** | Stamina ≥ 72 | 8 | BallRecovery_HelpsTheTouchOnALooseBall |
| 36 | `recovery_sprint` | Defensiva | — | **DEFERRED** | Speed ≥ 74 | 19 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |
| 37 | `aerial_duel` | Defensiva | — | **DEFERRED** | Strength ≥ 72 | 17 | DEFERRED: ADeferredSkill_ChangesNothing_ItIsListedAndIgnored, EveryDeferredSkill_SaysWhy… |

132 de los 136 jugadores de campo poseen al menos una skill real. Los otros posesores de skills diferidas las conservan en su `PlayerSkillSet` (nada se pierde), y el resolutor las **lista como ignoradas** (`Deferred`), nunca las aplica a medias.

### 3.1 Clasificación A / B / C
- **A — solo permiso:** ninguna (ver arriba).
- **B — cambian parámetros de una acción existente:** `progressive_pass`, `line_breaking_pass`, `build_up_play`, `clinical_finishing`, `first_time_finish`, `long_range_strike`, `pocket_receive`, `ball_recovery`, `hold_up_play`, `progressive_carry`, `press_resistance`, `interception_reading`, `defensive_block`, `defensive_screen`, `high_press`, `aggressive_tackle`, `one_v_one_defending`, `explosive_burst`, `turn_and_accelerate` (estos dos cambian el paso de locomoción, no un atributo).
- **C — cambian el comportamiento de la acción:** `curve_shot` (apunta contra la curva, error residual), `close_control_dribble` / `elusive_dribble` / `flair_dribble` (acción nueva con 3 desenlaces), `long_switch` / `creative_pass` (pase por alto con estilo).

### 3.2 De qué está hecha la maestría de cada skill real
| Skill | Efecto | Atributos |
|---|---|---|
| `close_control_dribble` | CloseControlMove | Control 35%, Dribbling 30%, Agility 20%, Technique 15% |
| `elusive_dribble` | ElusiveMove | Agility 35%, Dribbling 30%, Speed 20%, Control 15% |
| `flair_dribble` | FlairMove | Technique 35%, Dribbling 30%, Control 20%, Agility 15% |
| `progressive_pass` | ProgressivePass | Passing 70%, Technique 15%, Control 15% |
| `line_breaking_pass` | LineBreakingPass | Passing 60%, Technique 25%, Control 15% |
| `long_switch` | SwitchLob | Passing 55%, Technique 25%, Control 10%, Strength 10% |
| `creative_pass` | CreativeLob | Passing 45%, Technique 35%, Control 20% |
| `build_up_play` | BuildUpPass | Passing 55%, Control 35%, Technique 10% |
| `clinical_finishing` | ClinicalFinish | Finishing 70%, Technique 15%, Shooting 15% |
| `first_time_finish` | FirstTimeFinish | Finishing 55%, Technique 25%, Control 20% |
| `long_range_strike` | LongRangeStrike | Shooting 70%, Technique 20%, Strength 10% |
| `curve_shot` | CurvedShot | Technique 40%, Shooting 25%, Finishing 20%, Control 15% |
| `pocket_receive` | PocketReceive | Control 50%, Technique 30%, Agility 20% |
| `hold_up_play` | HoldUp | Strength 50%, Control 35%, Technique 15% |
| `explosive_burst` | ExplosiveBurst | Acceleration 60%, Agility 20%, Speed 20% |
| `turn_and_accelerate` | TurnAndAccelerate | Agility 55%, Acceleration 45% |
| `progressive_carry` | ProgressiveCarry | Dribbling 45%, Control 30%, Speed 25% |
| `press_resistance` | PressResistance | Control 40%, Agility 25%, Strength 20%, Dribbling 15% |
| `interception_reading` | InterceptionReading | Defense 55%, Agility 20%, Speed 15%, Control 10% |
| `aggressive_tackle` | AggressiveTackle | Defense 55%, Strength 30%, Speed 15% |
| `one_v_one_defending` | OneVOneDefending | Defense 50%, Agility 35%, Speed 15% |
| `defensive_block` | DefensiveBlock | Defense 60%, Strength 20%, Agility 20% |
| `defensive_screen` | DefensiveScreen | Defense 50%, Strength 30%, Agility 20% |
| `high_press` | HighPress | Stamina 50%, Defense 30%, Speed 20% |
| `ball_recovery` | BallRecovery | Defense 40%, Stamina 25%, Agility 20%, Speed 15% |

### 3.3 Skills diferidas y por qué (no se simulan)
| Skill | Motivo |
|---|---|
| `precision_cross` | Crossing is not resolved by the Core yet (FootballActionKind.Cross is NotSupported since Phase 4). |
| `header_finish` | Headers are NotSupported by the Core: aerial play is a later phase. |
| `dead_ball_specialist` | Set pieces (free kicks, corners) do not exist in the Core yet: restarts are a later phase. |
| `counter_sprint` | Needs the possession-transition context (a team that has just won or lost the ball), which belongs to the later transitions phase. |
| `cut_inside` | Needs the tactical lane context (what is 'inside' relative to the touchline and the play), which is positioning AI. |
| `support_run` | Off-ball movement is decided by the AI, which does not exist yet: the Core has no player-controlled run for this skill to modify. |
| `overlapping_run` | Off-ball movement is decided by the AI, which does not exist yet: the Core has no player-controlled run for this skill to modify. |
| `half_space_movement` | Off-ball movement is decided by the AI, which does not exist yet: the Core has no player-controlled run for this skill to modify. |
| `late_box_arrival` | Off-ball movement is decided by the AI, which does not exist yet: the Core has no player-controlled run for this skill to modify. |
| `counter_press` | Needs the possession-transition context (a team that has just won or lost the ball), which belongs to the later transitions phase. |
| `recovery_sprint` | Needs the defensive recovery context (running back to the goal side), which is defensive AI. |
| `aerial_duel` | Aerial duels need headers and ball-in-the-air contests, which the Core does not resolve yet (Header is NotSupported). |

Las 2 etiquetas diferidas de la Fase 5 siguen igual (`OffsideTrapExploitation`, `TacticalFoul`). **Auditoría 258 → 160 → 37:** 158 etiquetas con skill + 2 diferidas = 160 conceptos; cada etiqueta pertenece a exactamente una skill (test); no se creó ninguna skill nueva para eliminar una etiqueta.

---

## 4. Cómo se resuelve cada familia

### 4.1 Tiro con efecto — `curve_shot` → `CurvedShot`
Dentro de `ResolveKick` (la misma ruta que `Shot`):
- Sin skill → `SkillNotGranted`. Un `Shot` normal sigue limitado a `BasicCurveLimit` (0.5); el tiro con skill puede curvar hasta 1.
- Calidad = (calidad de tiro + `CurveMasteryBonus × maestría`) × **pie** × **stamina**, con techo `QualityCeiling`.
- Error: el extra de error de la curva se reduce `CurveErrorRelief × maestría`; **más** un error residual `CurveResidualErrorDegrees × |curva|` que nada elimina.
- **Apunta contra la curva** (`CurveAimCompensationDegrees × curva × maestría`): el jugador con técnica apunta a donde el balón debe volver. El ángulo se calibró contra el Magnus real del `BallSimulator` (curva 1, ~16 m ≈ 0.7 m de deriva ≈ 2.5°).
- **`BallSimulator` sigue siendo la autoridad:** la acción solo da velocidad y `Spin.Y`; el test `…TheBallSimulatorStaysTheAuthority…` comprueba que el balón no se teletransporta y que con Magnus 0 en el simulador la misma skill no curva.
- Coste: tiro + `CurvedShotExtraStamina`.

### 4.2 Regate / skill move — `close_control_dribble`, `elusive_dribble`, `flair_dribble` → `SkillMove`
Acción nueva (`ResolveSkillMove`), solo con balón, un número aleatorio. Puntuación =
`maestría del estilo × pie × stamina + sesgo del estilo − 0.30 × rivales cerca (máx. 2, radio de presión) − 0.15 × (velocidad/velocidad máx.) − 0.25 × ángulo/180° + ruido × 0.12`.
- **Limpio** (≥ 0.55): el balón se empuja en la dirección pedida (sigue siendo suyo), el jugador queda **protegido** `MoveEvadeSeconds` (los placajes contra él tienen `MoveEvadeDefenceBonus × fuerza` menos de margen y la presión pesa `MoveEvadePressureRelief` menos en su control de balón).
- **Reducido** (0.30–0.55): `MoveReducedShare` (40 %) del desplazamiento y de la protección.
- **Fallo** (< 0.30): el balón se escapa libre, el jugador queda ocupado `MoveFailLockSeconds`, `Failed/Fumbled`.
- Estilos: *close control* (seguro y corto), *elusive* (equilibrado), *flair* (arriesgado y decisivo): distinto sesgo, desplazamiento, protección y coste.
- Enfriamiento `MoveCooldownSeconds`; coste de stamina centralizado, nunca < 0. **No hay IA defensiva:** el rival solo "ve" el efecto en sus propios duelos.

### 4.3 Pase por alto — `long_switch`, `creative_pass` → `SkillLobPass`
Dentro de `ResolveKick` (base: pase largo). *Switch*: más velocidad (`SwitchSpeedFactor`) y vuelo más alto (`SwitchLoftFactor`). *Creative*: más plano (`CreativeLoftFactor`) y más preciso (`CreativeErrorFactor`). Ambos: `LobMasteryBonus × maestría` de calidad, pie, stamina, `LobExtraStamina`.

### 4.4 Pases y tiros (skills pasivas)
Cada una suma calidad (`× maestría`, antes de pie y stamina, con techo) **solo** en las acciones y contextos de su fila: `progressive_pass` (pase hacia delante), `line_breaking_pass` (pase al hueco + menos error extra), `build_up_play` (pase corto/pared), `clinical_finishing` (tiro cerca), `long_range_strike` (tiro lejos), `first_time_finish` (tiro sin control previo).

### 4.5 Recepción, protección y conducción
`pocket_receive` y `ball_recovery` suman al **primer toque** (con rival encima / balón suelto). `hold_up_play` suma a la protección (escudo) en el control del balón y en los duelos. `progressive_carry` (a ritmo) y `press_resistance` (quita parte de la penalización de la presión) actúan en `DribbleControlQuality`.

### 4.6 Movimiento
`explosive_burst` y `turn_and_accelerate` no tocan atributos ni la velocidad máxima: son `LocomotionModifiers` del paso de locomoción. *Burst*: más aceleración solo por debajo de `BurstSpeedFraction` de la velocidad máxima (arrancar). *Turn*: más aceleración mientras gira más de `TurnAngleThresholdDegrees` y menos penalización de giro con la velocidad. Sin skill, el paso es **idéntico** (test bit a bit). Mismo gasto de stamina.

### 4.7 Defensa
`aggressive_tackle` (+ ataque en placaje y deslizante; **más faltas** en Classic: `AggressiveFoulShift` > bonus; en Arena no hay faltas), `one_v_one_defending` (+ ataque solo si el portador corre hacia él; se recupera antes de un placaje fallado), `interception_reading` (+ calidad y alcance de intercepción), `defensive_block` (+ calidad solo en bloqueos), `defensive_screen` (su presión pesa más sobre el portador), `high_press` (presión más larga, más cara de mantener la mitad, radio mayor). Sin marcaje, cobertura ni zonas: eso es IA.

---

## 5. Pie dominante / débil / desconocido
- `ActionFoot` (izq/dcha) como antes; `ActionResult.WeakFoot` (**nuevo**) dice explícitamente si se usó el pie débil.
- **50 no es un positivo especial:** la calidad del pie débil es `WeakFootFloor + (1 − WeakFootFloor) × q`, lineal; 50 está en la misma recta que 40 y 60 (test).
- **Desconocido** = dato no verificado. Sigue siendo un dato de **procedencia** (`PlayerDataProvenance.WeakFoot(id)` → Authored / NeutralUnverified / NotApplicable; 22 jugadores de campo sin verificar) y no cambia el cálculo: el Core nunca trata un 50 sin verificar mejor ni peor que un 50 verificado.

---

## 6. Arena y Classic
Las skills se resuelven igual en ambos (tests comparan velocidad y calidad). La única diferencia viene de la regla del modo: las faltas solo existen en Classic (`rules.HasFouls`), así que el riesgo de falta de `aggressive_tackle` solo se nota allí.

---

## 7. Números (todos en `SkillDefaults` / `SkillTuning`) — DECISIÓN FASE 6, balance pendiente
| Nombre | Valor | Para qué |
|---|---|---|
| `LineBreakingErrorRelief` | 0.50 | /// EVERY number of the Phase-6 skill model, in one place (the same rule as  and ): starting values copied into an editable /// . A skill never changes an attribute: it changes HOW an action resolves, by at most these amounts, scaled by the owner's mastery (0..1, derived from his /// attributes) and always on top of the stamina, foot and noise the action already has. DECISION MADE DURING PHASE 6: all of these values (the specification fixes the behaviour, not the /// numbers); balance is deferred and these are meant to be re-tuned in a balance pass. ///  public static class SkillDefaults { // ---- passing and shooting skills: extra quality (0..1 added to the quality of the kick, before foot and stamina) at mastery 1 public const float ProgressivePassBonus = 0.10f; public const float LineBreakingBonus = 0.10f; /// Share of the through-ball extra error a line-breaking passer removes at mastery 1. |
| `BuildUpBonus` | 0.10 |  |
| `ClinicalFinishBonus` | 0.10 |  |
| `FirstTimeFinishBonus` | 0.08 |  |
| `LongRangeBonus` | 0.10 |  |
| `ForwardCosine` | 0.5 | A pass counts as forward when its direction and the attack direction have at least this cosine. |
| `QualityCeiling` | 0.95 | A skill never makes a kick perfect: the quality it adds stops here (a player whose attributes alone already give more keeps what they give). |
| `CurveErrorRelief` | 0.60 | Share of the curve's extra error a master of the curve removes at mastery 1. |
| `CurveAimCompensationDegrees` | 2.5 | Degrees the aim is turned against the curve (per unit of curve) at mastery 1: the skilled curler aims for where the ball must come back to. |
| `CurveMasteryBonus` | 0.08 | Quality added by the mastery of the curve. |
| `CurveResidualErrorDegrees` | 1.0 | Degrees of error that no mastery removes (per unit of curve): a curved shot is never perfect, not even the best player's. |
| `CurvedShotExtraStamina` | 1.5 |  |
| `DefaultCurve` | 0.6 | The curve (0..1) the C-button curved shot uses when the input gives none; the sign follows the preferred foot. |
| `LobMasteryBonus` | 0.15 |  |
| `SwitchSpeedFactor` | 1.15 |  |
| `SwitchLoftFactor` | 1.25 |  |
| `CreativeLoftFactor` | 0.85 | The creative lob is flatter and more accurate: its loft and its maximum error are scaled by these. |
| `CreativeErrorFactor` | 0.80 |  |
| `LobExtraStamina` | 1.0 |  |
| `MoveCooldownSeconds` | 0.6 |  |
| `MoveStaminaCost` | 3.0 |  |
| `MoveEvadeSeconds` | 0.5 |  |
| `MoveEvadeDefenceBonus` | 0.30 | How much a successful move adds to the carrier's resistance in a tackle (before the style factor), and how much of the pressure penalty on his control it removes. |
| `MoveEvadePressureRelief` | 0.60 |  |
| `MoveCleanThreshold` | 0.55 | Success score at or above this: the move beats the man cleanly; between the two it only partly works; below it fails. |
| `MoveReducedThreshold` | 0.30 |  |
| `MoveReducedShare` | 0.40 | A partly-working move keeps this share of the evade and of the displacement. |
| `MoveBallSpeed` | 3.5 |  |
| `MoveFailBallSpeed` | 5.0 |  |
| `MoveFailLockSeconds` | 0.5 |  |
| `MovePressureWeight` | 0.30 |  |
| `MoveSpeedWeight` | 0.15 |  |
| `MoveAngleWeight` | 0.25 |  |
| `MoveAngleScaleDegrees` | 180.0 |  |
| `MoveNoise` | 0.12 |  |
| `CloseMoveBias` | 0.10 | Styles: close control is safe and short, elusive is the balanced one, flair is risky and decisive. Bias moves the score (negative = riskier); the factors scale the displacement, the evade and the stamina. |
| `CloseMoveDistance` | 0.60 |  |
| `CloseMoveEvade` | 0.70 |  |
| `CloseMoveCost` | 0.80 |  |
| `ElusiveMoveBias` | 0.0 |  |
| `ElusiveMoveDistance` | 1.0 |  |
| `ElusiveMoveEvade` | 1.0 |  |
| `ElusiveMoveCost` | 1.0 |  |
| `FlairMoveBias` | -0.12 |  |
| `FlairMoveDistance` | 1.20 |  |
| `FlairMoveEvade` | 1.40 |  |
| `FlairMoveCost` | 1.30 |  |
| `PocketReceiveBonus` | 0.15 |  |
| `BallRecoveryBonus` | 0.15 |  |
| `HoldUpBonus` | 0.20 |  |
| `ProgressiveCarryBonus` | 0.15 |  |
| `PressResistanceRelief` | 0.50 | Share of the pressure penalty a press-resistant carrier removes at mastery 1. |
| `BurstAccelerationGain` | 0.40 |  |
| `BurstSpeedFraction` | 0.60 | The burst only works while the player is below this share of his top speed: it is a way of getting going, not a higher top speed. |
| `TurnAccelerationGain` | 0.30 |  |
| `TurnAngleThresholdDegrees` | 60.0 |  |
| `TurnPenaltyRelief` | 0.35 |  |
| `InterceptReadingBonus` | 0.15 |  |
| `InterceptReadingReach` | 0.25 |  |
| `AggressiveTackleBonus` | 0.12 |  |
| `AggressiveFoulShift` | 0.20 | An aggressive tackler fouls more: the foul margin of his challenge is raised by this at mastery 1 (more than his tackle bonus, so the net risk grows) (only the modes with fouls feel it). |
| `OneVOneBonus` | 0.12 |  |
| `OneVOneLockRelief` | 0.50 | Share of the lost-tackle recovery time a one-on-one defender saves at mastery 1. |
| `DefensiveBlockBonus` | 0.15 |  |
| `ScreenPressureGain` | 0.50 | Extra weight of this presser in the pressure he puts on the carrier, at mastery 1. |
| `HighPressSecondsGain` | 0.50 |  |
| `HighPressDrainRelief` | 0.40 |  |
| `HighPressRadiusGain` | 0.25 |  |
| `FacingCosine` | 0.0 | A one-on-one only counts when the carrier runs towards the defender: the cosine of the carrier's facing against the direction to the defender. |

---

## 8. Tests (suite `SkillsReal*` — 94 nuevos)
Catálogo y tabla (7) · resolución de un jugador (5) · permiso único (11) · tiro con efecto y pie (12) · skill move (14) · pase por alto (6) · skills pasivas, movimiento y defensa (22) · jugadores reales de punta a punta, guardas y centralización (17). Además se actualizaron 3 tests de la Fase 4/5 que describían el contrato anterior (ver §10).

---

## 9. Qué queda para después
- **Para la IA (Fase de IA/táctica):** skills de movimiento sin balón (`support_run`, `overlapping_run`, `half_space_movement`, `late_box_arrival`), `cut_inside`, `recovery_sprint`; decidir *cuándo* usar una skill (el Core solo resuelve *qué pasa* si el jugador la usa).
- **Para transiciones:** `counter_sprint`, `counter_press`.
- **Para acciones/reglas futuras:** `precision_cross` (centro), `header_finish` y `aerial_duel` (cabezazo/duelo aéreo), `dead_ball_specialist` (jugadas a balón parado).
- **Para el pase de balance:** todos los valores de §7 y los pesos de maestría; umbrales 0.55 / 0.30 del regate; equilibrio entre estilos.
- **Para Unity:** mapear el botón C en `ActionBridge`/HUD (el puente ya reenvía cualquier `ActionButton`), pasar `db.PlayerSkills` y `db.Skills` al construir el `MatchController` en `MatchBridge`, animaciones y VFX de cada `ActionDetail` (`Evaded`, `PartlyEvaded`, `Fumbled`, `Curved`, `Lofted`).

## 10. Legado afectado (solo donde las skills lo tocan)
- **`ActionRequest.SkillGranted`**: legado de la Fase 4; ahora solo para partidos sin datos de skills. Puede eliminarse cuando todos los llamantes construyan el partido con skills.
- **Tests actualizados por cambio de contrato** (no se debilitó ningún test): `ContractOnlyActions_AreRefusedClearly…` (un `SkillMove` concedido ya no es `NotSupported`), `TheNeutralInput_CarriesButtonGestureDirectionAndMagnitude…` (botón C y `Curve`), `SkillCatalogue_UsesTheExistingContracts_NoSecondSystem` (la carpeta tiene 5 archivos del mismo sistema).
- **Sin tocar (documentado, no limpiado):** dos modelos de error de acciones (`ActionModel.KickError` vs `AiErrorModel`), `PlayerIntent` vs `ActionInput`, `MatchPlayer.Runtime` (stamina duplicada con la de locomoción). Las skills usan solo la stamina de locomoción vía `SpendStamina`.
- **Pendiente de validación del usuario:** los umbrales de atributos de las skills de la Fase 5 siguen siendo criterio mío.
