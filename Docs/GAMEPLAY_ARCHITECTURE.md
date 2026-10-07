# FS27 — Arquitectura de gameplay V1 (CERRADA)

Estado: **arquitectura cerrada y FASE 1 (núcleo determinista) implementada** — ver sección N para lo que existe de verdad y las decisiones de implementación. Todas las decisiones de este documento son definitivas.
Esta fase de cierre solo modificó documentación y movió `FootballActionKind` a un archivo neutro de Core (con acciones nuevas añadidas al final). No se implementó gameplay, ni se tocaron los contratos de jugadores, porteros, entrenadores ni árbitros.
Las ampliaciones que se mencionan (banquillo, periodos, estados de partido, tipo de competición) son **propuestas aditivas para la fase de implementación**.

Leyenda: ✅ resuelto y cerrado · 🔧 resuelto y ya aplicado en código (cierre o Fase 1) · ⏳ pendiente de implementar (decisión tomada, falta construir) · ⚙ valor de configuración sin cifra decidida.

**Principio rector:** `GameMode` (cómo se juega: Arena | Classic) y `CompetitionMatchType` (cómo se resuelve el resultado: Normal | Knockout) son conceptos **independientes**. Ninguna regla de desempate depende del modo.

---

## A. AUDITORÍA

### A.1 Qué existe hoy (base sobre la que se construye)

| Sistema | Dónde | Estado |
|---|---|---|
| 12 atributos oficiales, Overall, tarjeta | `Core/Players/*`, `Core/PlayerAttributes.cs` | Hecho. `Control` es alias de `BallControl`. |
| 12 roles (`PlayerArchetype`), 5 zonas (`PitchZone`), `PlayerPlayingProfile` (Risk/Creativity/Aggression, afinidades, zonas) | `Core/Ai/PlayingProfile.cs` | Hecho (datos + `RoleExecutionModel.Fitness`). |
| Portero (8 capacidades, GRL = media, 4 estilos) | `Core/Goalkeepers/*` | Hecho, solo datos + `GoalkeeperSkillModel`. |
| Entrenadores (grado, formaciones, compatibilidad, boost +1) | `Core/Coaches/*` | Hecho. `EffectiveRating` es contextual (nunca guardado). |
| Árbitros (7 valores, estilos como datos) | `Core/Referees/*` | Hecho, **nadie los lee**. |
| Dificultad (calidad de decisión, reacción, errores, 6 grupos de parámetros) | `Core/Difficulty/*` | Hecho; ya separa "cómo juega" de "qué tan bien". Coherente con la sección 35. |
| IA: infraestructura (posicionamiento, presión, anticipación, estilo de equipo) | `Core/Ai/*` | Modelos de apoyo; **no hay Player AI ni Team AI** que decida. |
| Balón: simulador puro (`BallSimulator`, spin, predictor, toque de conducción) | `Core/Ball/*` | Hecho en Core. |
| Locomoción + stamina | `Core/PlayerLocomotion.cs`, `StaminaSystem.cs`, `InputCurve.cs` | Hecho (joystick → trote/carrera/sprint, sin botón de sprint). |
| `PlayerIntent` (movimiento + acción + estilo + detalles dribble/shot/pass) | `Core/PlayerIntent.cs` | Hecho. Un único tipo de intención. |
| Reglas de balón fuera/gol, controlador de partido | `Core/Match/*` | Mínimo (ver A.2). |
| Joystick virtual, teclado/gamepad | `Core/Input/*`, `Input/*` | Solo movimiento. |
| Formaciones | `Core/Data/FormationDefinition.cs` | Solo posiciones/slots. |
| Creator Engine (comportamientos, DNA, animación) | `Core/Creator/*` | Hecho; identidad/animación, no reglas. `FootballActionKind` ya no vive aquí (`Core/FootballActionKind.cs`). |

### A.2 Conflictos entre la especificación y el proyecto — todos cerrados

**C1 🔧 Dos físicas de balón (Fase 1).** `Gameplay/BallPhysics.cs` mueve el balón con `Rigidbody`; `Core/Ball/BallSimulator` (puro) no se usa en Gameplay.
Cierre: **`Core/Ball/BallSimulator` es la fuente autoritativa.** Flujo: `Input → Core Simulation → BallSimulator → MatchState → Presentación Unity / Rigidbody`. El `Rigidbody` representa visualmente (y puede participar en la presentación física donde corresponda) pero **nunca decide**: copia el `BallState`. Objetivos: determinismo, tests, depuración, replays; online fuera de alcance. 🔧 Fase 1: `MatchController.Advance` mueve el balón con `BallSimulator`; `BallController` tiene modo "core-driven" (cinemático, solo refleja) y `MatchBridge` lo alimenta. Ver N.

**C2 🔧 `MatchRules` genera córners/bandas/saques de puerta siempre (Fase 1).** Cierre: reglas **por modo, en datos** (`MatchModeDefinition` + `IModeRules`). Classic = comportamiento actual; Arena no usa la regla de líneas. 🔧 `IGameModeRules`/`ArenaRules`/`ClassicRules`.

**C3 ✅ Arena y los límites.** Cierre: campo cerrado; el balón **rebota** y continúa en juego. Sin saque de banda, córner ni saque de puerta. El rebote conserva de forma coherente velocidad, dirección, ángulo y energía (la física exacta se define al implementar; coeficiente de restitución configurable). El gol cuenta solo por la boca de la portería; el resto de la línea de fondo rebota. Tras una parada el portero juega el balón sin reanudación formal.

**C4 🔧 `MatchState` (Fase 1).** Cierre: **se amplía el existente** hasta ser la fuente autoritativa; no hay `GameState` ni segundo estado. `MatchController` evoluciona a `MatchEngine`, único escritor. Nuevos valores de `MatchPhase` solo al final (no se renumeran). Contenido en B.4. 🔧 Hecho; ver N.1 para el mapa entre `MatchPhase` y `MatchPeriod`.

**C5 ✅ Atributo legado `Reaction`.** Cierre: se mantiene; se marca como **legacy / internal derived value**; **no es el 13.º atributo oficial**; no entra en Overall, tarjetas ni datos nuevos; la IA aún lo consume. Deuda M-1.

**C6 ✅ Stamina.** Cierre: sistema continuo (no solo sprint) sobre `Stamina01` con una `FatigueCurve` que afecta de forma progresiva a velocidad, sprint, aceleración, recuperación, acciones físicas, presión, precisión bajo fatiga y repetición de esfuerzos. Referencias conceptuales 100 % / ~70 % / ~40 % / ~20 % / 0 % (a 0 no se puede hacer sprint hasta recuperar); se prefiere curva continua a estados rígidos. ⏳ fase 3.

**C7 ✅ Nombre `Capability` ocupado** (`GoalkeeperCapability`). Cierre: habilidades técnicas = **`SkillDefinition` y `PlayerSkillSet`**, solo contrato conceptual. **No se asigna ninguna habilidad a los 156 jugadores** ni se inventan; catálogo y asignación = fase posterior y separada.

**C8 🔧 `FootballActionKind` dentro del Creator.** Cierre aplicado: el enum vive ahora en `Core/FootballActionKind.cs` (mismo namespace `FS27.Core`, sin romper referencias). Los 16 valores originales conservan su número (ahora explícito, 0–15) y no se reordenan; ninguna serialización usa el número (se comprobó: las especificaciones guardan nombres/ids, no el entero del enum). Añadidos **al final** (16–22), solo los siete pedidos: `CurvedShot`, `Chip`, `WallPass`, `ThroughPass`, `SkillMove`, `SkillLobPass`, `GoalkeeperRush`. Referencias "C8/C9" de la auditoría: son identificadores de **conflicto** de este documento (C8 = esta migración, C9 = el pad de acciones), **no** valores del enum.
Consecuencias: el test que exige que toda acción tenga un perfil de animación se mantuvo y se añadieron siete perfiles **Planned** (sin clips, `RootMotionWeight = 0`) al catálogo por defecto; no hay arte ni animación final. Test nuevo `FootballActionKindTests` fija los números y el desacoplamiento.
Quedan sin enum las acciones defensivas de H.2 que no están en la lista mínima (presión/contención, entrada deslizante, disputa física, despeje): `Tackle`, `Intercept`, `Block` y `Shield` cubren parte; se decidirá al implementar el pad (fase 5) y solo si hace falta un valor real, añadido al final. No se añaden valores de relleno.

**C9 ⏳ Controles A/B/C.** Cierre del diseño en H; falta `ActionPadModel` (fase 5).

**C10 ✅ C pulsación = sacar al portero.** Cierre: contextual, con prioridad cuando el usuario **no tiene la posesión** o el balón está **en su tercio**. La IA del portero **puede rechazarla** si la salida es claramente absurda, el balón está demasiado lejos, el riesgo es excesivo o la posición no permite una salida razonable; un toque accidental no provoca siempre una salida irracional (resultado `Accepted` / `Refused(motivo)`). C swipe ↑/↓ conserva las acciones especiales de ataque. C no tiene otra función defensiva principal.

**C11 ✅ Sin datos de habilidades.** Cierre: fase posterior (catálogo + asignación validada). Tests de skills con datos de prueba.

**C12 ✅ Formaciones.** Cierre: data-driven; ninguna definitiva ahora ni en el motor; las crea Claude después desde jugadores, roles, zonas y entrenadores. Deben poder expresar posiciones iniciales, zonas, roles, comportamiento con balón, sin balón y en transición, anchura, profundidad y responsabilidades (`FormationBehaviorProfile`, fase 9). `DefaultFormations` = esqueleto de tests.

**C13 ✅ Coach Boost.** Cierre: **no modifica atributos ni `BaseOverall`**. `CompatibilityScore ≥ 80 → CoachBoost +1`; `< 80 → 0`; nunca negativo. `EffectiveOverall = BaseOverall + CoachBoost`. **El motor de gameplay NO consume todavía `EffectiveOverall` como modificador adicional**: sin efecto oculto ni doble aplicación. La influencia real en simulación queda para una fase posterior de balance.

**C14 ✅ Plantilla.** Cierre: **6 titulares + 4 suplentes = 10 disponibles; mínimo 1 portero suplente.** Propuesta aditiva: `BenchIds` junto a la alineación actual de `TeamDefinition`. ⏳ fase 2.

**C15 ✅ Duración.** Cierre: Arena 2 × 6 min (sin descanso tradicional, cambio de lado al acabar el 1.º); Classic 2 × 8 min (con descanso). **Configurables** en `MatchModeDefinition`. `MatchSettings` actual (180 s, un periodo) no cambia hasta la fase 1.

**C16 ✅ Expulsiones (Classic).** Cierre: ver sección F.3.

**C17 ✅ Sustitución del portero.** Consume 1 de los 3 cambios como cualquier otra.

**C18 ✅ Frecuencia de simulación.** Paso fijo de física 60 Hz; decisión de IA ~10–15 Hz con escalonado determinista; percepción ~5 Hz (valores iniciales configurables; se afinan al medir en móvil).

**C19 ✅ Estadísticas y eventos.** Nuevos, filtrados por modo. No se inventan estadísticas permanentes nuevas más allá de las ya enumeradas en la spec original (sección 39); las de reglas solo existen en Classic.

**C20 ✅ Modo ≠ tipo de competición.** Nuevo en este cierre. `GameMode` y `CompetitionMatchType` son ejes independientes; el desempate depende solo del segundo (ver F.2).

**C21 ✅ Jugador de campo como portero.** Cierre: se deriva en runtime, sin atributos nuevos (ver F.4).

**C22 ✅ Segunda amarilla.** Cierre: Classic, 2 amarillas = roja (ver F.3).

### A.3 Sistemas que faltan por completo
Player AI, Team AI, transiciones, sistema de habilidades (solo contrato por ahora), resolución de duelos, detección de contacto/falta/fuera de juego, `ActionPadModel`, motor de partido con máquina de estados por modo, bus de eventos, estadísticas, sustituciones, tarjetas, reglas por modo, árbitro operativo, selector de jugador activo, fatiga continua, banquillo, formaciones con comportamiento (fase posterior), replay/log determinista.

### A.4 Ambigüedades de la spec original
Todas resueltas por la Fase 0 y por este cierre. Los valores de configuración de la Fase 1 quedaron decididos en N.3; lo que sigue pendiente de fases posteriores está en L.2.

---

## B. ARQUITECTURA FINAL

### B.1 Capas y dependencias (hacia abajo; Core no conoce Unity)

```
DATA            jugadores, porteros, entrenadores, árbitros, formaciones, habilidades, modos, dificultad
  ▲
RULES           IModeRules (Arena/Classic), contacto→infracción, fuera de juego, reanudaciones, tarjetas, sustituciones
  ▲
SIMULATION      MatchEngine, BallSimulator, locomoción, duelos, acciones (pase, tiro, regate, entrada)
  ▲      ▲
AI              PlayerAI, TeamAI, GoalkeeperAI   INPUT  ActionPadModel + VirtualStickModel + selector de jugador
  ▲
PRESENTATION    animación, cámara, audio, física visual de Unity (refleja Core)     UI   HUD, menús     NETWORK  (reservado, vacío)
```

Reglas de dependencia (verificables con tests de guarda como los existentes):
- `Simulation` no conoce `Presentation`, `UI`, `Audio` ni `Input` concretos: recibe `PlayerIntent`.
- `AI` e `Input` producen **el mismo** `PlayerIntent`.
- `Rules` no mueve a nadie: devuelve veredictos (`RuleVerdict`).
- `Presentation` solo lee `MatchState` y eventos; nunca escribe estado de juego. El `Rigidbody` visual copia el `BallState` de Core.
- Cero comparaciones con nombres de jugador/entrenador/árbitro: solo ids, perfiles, habilidades y datos.
- Ninguna formación vive en código del motor.

### B.2 Módulos y responsabilidades

| Módulo (Core) | Responsabilidad | Reutiliza |
|---|---|---|
| `Match/MatchEngine` | Dueño del paso de simulación y de la máquina de estados; único escritor de `MatchState` | `MatchController` (evolución) |
| `Match/MatchState` | **Estado autoritativo único** (ampliado) | `MatchState`, `MatchScore`, `MatchClock` |
| `Match/Mode/MatchModeDefinition`, `IModeRules`, `ArenaRules`, `ClassicRules` | Qué existe en cada modo, en datos | `MatchRules` (pasa a ser la parte de Classic) |
| `Match/Events/GameplayEvent` + `IEventSink` | Bus de eventos tipados; rechaza los que el modo no declara | nuevo |
| `Match/Stats/MatchStatistics` | Estadísticas aplicables por modo | nuevo |
| `Simulation/PlayerRuntimeState` (ampliado) | Estado dinámico por jugador | `PlayerRuntimeState` |
| `Simulation/FatigueCurve` | Stamina → multiplicadores | `StaminaSystem` |
| `Simulation/Actions/*` | Pase, tiro, regate, skill, entradas, pared, pase al hueco | `PlayerIntent`, `DribbleTouchModel` |
| `Simulation/DuelResolver`, `ContactModel` | Duelos con atributos y timing; el hecho físico del contacto, no la regla | nuevo |
| `Skills/SkillDefinition`, `PlayerSkillSet` | **Solo contrato** (sin catálogo, sin asignaciones) | nuevo (C7) |
| `Ai/PlayerAI`, `TeamAI`, `GoalkeeperAI`, `TransitionModel` | Decidir intenciones; el portero evalúa y puede rechazar la orden de salida | `PositioningModel`, `PressureModel`, `AnticipationModel`, `AiSkillResolver`, `TeamStyle`, `GoalkeeperSkillModel` |
| `Tactics/` (fase posterior) | `FormationBehaviorProfile` data-driven | `FormationDefinition` (sin cambiarla) |
| `Rules/ContactJudge`, `OffsideJudge`, `RestartRules`, `DisciplineRules`, `SubstitutionRules`, `RefereeDecisionModel` | Veredictos de Classic | `RefereeDefinition` |
| `Input/ActionPadModel`, `PlayerSelector` | Gestos → `ActionRequest`; cambio de jugador | `VirtualStickModel` |

### B.3 Contratos principales (firmas conceptuales)

```
// Datos existentes (SIN cambios)
PlayerDefinition / PlayerPlayingProfile / PlayerAttributes(12 oficiales) / GoalkeeperProfile / CoachDefinition / RefereeDefinition
// Valor legado, no oficial: PlayerAttributes.Reaction (legacy / internal derived; lo consume la IA)

// Ejes independientes del partido (conceptuales, nuevos)
enum GameMode            { Arena, Classic }                 // cómo se juega
enum CompetitionMatchType{ Normal, Knockout }               // cómo se resuelve el resultado (Normal = liga/amistoso)
enum MatchTerminationReason { Regulation, ExtraTime, PenaltyShootout, InsufficientPlayers }  // (valores finales al implementar)

MatchModeDefinition { Mode, flags de reglas, Periods[ {Seconds} ], Boundaries(Walls|Lines), SideSwitch, RestKind,
                      RestStaminaRecovery (Classic 0.25, Arena 0.10), MaxSubstitutions = 3 }       // todo configurable
CompetitionRules    { Type, ExtraTime{ Periods, Seconds }, Shootout{ KicksPerTeam, SuddenDeath } } // solo Knockout los usa; configurable
SkillDefinition { SkillId, Kind, RequiredAttributes, Conditions, EnergyCost }   // contrato; sin catálogo
PlayerSkillSet  { PlayerId, SkillIds[] }                                          // contrato; sin asignaciones
TeamDefinition (ampliación aditiva futura): StarterIds[6] (= PlayerIds actual) + BenchIds[4], ≥1 GK en BenchIds

IMatchSimulation.Step(MatchState, IntentFrame, dt)        // determinista, paso fijo
IntentFrame { PlayerIntent[] }                             // origen irrelevante
GoalkeeperOrderResult { Accepted | Refused(reason) }       // respuesta a C tap
EmergencyKeeperProfile.Derive(PlayerAttributes) -> 8 capacidades en runtime (F.4; no se guarda)
RuleVerdict { Kind, Team, Position, Severity, Advantage }
IPlayerBrain.Decide(PerceptionSnapshot, TeamPlan, dt) -> PlayerIntent
ITeamBrain.Plan(PerceptionSnapshot, MatchContext) -> TeamPlan
IModeRules.Judge(MatchState, SimulationEvents) -> RuleVerdict[]
IResultResolver.Resolve(MatchState, CompetitionRules) -> Finished | ExtraTime | PenaltyShootout    // depende SOLO de CompetitionMatchType
```
Regla de contrato: `IResultResolver` no recibe `GameMode`; `IModeRules` no recibe `CompetitionMatchType`. Así los dos conceptos no se pueden mezclar.

### B.4 Estado único

```
MatchState (autoritativo; no existe ningún otro estado paralelo)
  GameMode, CompetitionMatchType, Settings/ModeDefinition, CompetitionRules
  Clock, Period, Phase (MatchPhase), Score, Result, TerminationReason
  Ball (BallState de Core + Possession + LastTouch)
  Teams[2]: FormationId, CoachId, Squad(Starters[6], Bench[4]), Subs { Used, Max=3 },
            Cards (amarillas por jugador), Expulsados, Stats, Referee-ref (solo Classic)
  Players en pista: id, RuntimeState(pos, vel, heading, Stamina01, acción, flags), Active/Expulsado, ocupa-portería
  ExtraTime (periodo actual), PenaltyShootout (tiradas, marcador de tanda)
  PendingRestart, ActiveFoul/Advantage (Classic)
```
`CoachBoost` y `EffectiveOverall` no son estado de simulación: se consultan con `CoachBoostRules` desde vistas de rating (tarjeta, HUD) y el motor no los lee.

---

## C. GAMEPLAY LOOP

```
Input/AI → PlayerIntent
  → ActionResolver (intención + contexto → GameplayAction; comprueba habilidad/stamina/modo)
  → Simulation (movimiento, balón, duelos, contactos)
  → Rules (solo el modo activo: veredicto; en Arena no se invoca la regla de infracciones)
  → MatchState (único escritor: el MatchEngine)
  → Events (según modo) + Stats
  → Presentation (lee estado/eventos: animación, cámara, audio, HUD)
```
Un paso = una pasada completa; el orden es fijo, para que sea reproducible.

## D. PLAYER LOOP
`Joystick/ActionPad → PlayerIntent{Move, Action, Style, detalle} → ActionResolver (¿tiene skill? ¿hay balón? ¿stamina?) → GameplayAction → Locomotion/ContactModel/BallSimulator → resultado (acción completada/fallida/modificada, coste de energía) → PlayerRuntimeState`.
La velocidad la define **solo la longitud del vector de movimiento** (ya implementado; no existe botón de sprint) multiplicada por Speed/Acceleration/Agility/Strength/BodyType y la curva de fatiga.

## E. AI LOOP
`Perception (snapshot con retardo/ruido de la dificultad) → Context (zona, marcador, tiempo, stamina, formación, entrenador, plan del equipo) → Decision (PlayingProfile, RiskPreference, Creativity, Aggression, roles, calidad de decisión) → Action (PlayerIntent) → Evaluation (resultado → memoria corta, p. ej. pase fallido reduce riesgo un rato)`.
- Determinista: el RNG por jugador sale de `SeededRandom` con semilla de partido + id.
- Frecuencia: percepción ~5 Hz, decisión ~10–15 Hz escalonada, movimiento cada paso.
- TeamAI emite un **plan** (bloque, altura de presión, anchura, tempo, quién presiona: máx. `MaxSimultaneousPressers`), PlayerAI lo interpreta según su perfil. Esto evita que todos corran al balón.

## F. MATCH LOOP

### F.1 Máquina de estados
Estados conceptuales: `PreMatch, Kickoff, InPlay, DeadBall, Goal, Restart, HalfTime, ExtraTime, PenaltyShootout, FullTime` (más los ya existentes `GoalCelebration`, `Paused`). No todos existen en los dos modos; el `MatchEngine` es el único que cambia de fase y rechaza los que el modo no declara.

**Arena:** `PreMatch → Kickoff → InPlay (6 min) → SideSwitch → Kickoff → InPlay (6 min) → resolución`. `Goal → Kickoff`. `SideSwitch` es un cambio de lado + recolocación, no un descanso. Sin `DeadBall` por reglas.
**Classic:** `PreMatch → Kickoff → InPlay ⇄ DeadBall(Foul|Offside|OutOfBounds…) → Restart(ThrowIn|Corner|FreeKick|Penalty|GoalKick) → InPlay … → HalfTime → Kickoff → … (8 min por tiempo) → resolución`. `Goal → Kickoff`.
**Resolución (ambos modos igual):** al terminar el tiempo reglamentario se consulta a `IResultResolver` (F.2).

### F.2 Empates, prórroga y penales (independientes del modo)

| Modo | Tipo | Empate tras tiempo reglamentario |
|---|---|---|
| Arena | Normal (liga/amistoso) | **Termina empatado.** Sin prórroga ni penales |
| Classic | Normal | **Termina empatado.** Sin prórroga ni penales |
| Arena | Knockout | Prórroga → si sigue empatado, tanda de penales |
| Classic | Knockout | Prórroga → si sigue empatado, tanda de penales |

Reglas:
- Un empate es un empate: nunca activa prórroga ni penales por sí solo.
- La prórroga depende **exclusivamente** de `CompetitionMatchType = Knockout` (no de Classic ni de Arena). **Dos periodos, cada uno de un tercio de un periodo reglamentario** (Arena 2 min, Classic 2 min 40 s), configurable en `MatchSettings`.
- La tanda de penales solo se activa si es Knockout **y** el empate persiste tras la prórroga; **determina al ganador**. **Formato: 3 lanzamientos por equipo; si siguen empatados, muerte súbita** (una tirada cada uno; decide la primera ronda completa con diferencia). Termina antes si un equipo queda matemáticamente inalcanzable. El 3 vive en un solo sitio (`MatchDefaults.ShootoutKicksPerTeam` → `MatchSettings.ShootoutKicksPerTeam`).
- Un partido Normal jamás entra en `ExtraTime` ni en `PenaltyShootout`; un Knockout con ganador al finalizar el tiempo reglamentario tampoco.
- En Arena la prórroga se juega con las reglas de Arena (campo cerrado, sin reglas de infracción); en Classic con las de Classic. El stamina de la prórroga usa la misma curva continua; la recuperación antes de la prórroga y en su descanso es la **del propio intervalo del modo** (Classic 25 %, Arena 10 %), configurable.
- Marcador: los goles de la tanda no se suman al marcador de juego; `Result` registra ganador y `TerminationReason`.

### F.3 Disciplina y expulsiones (solo Classic)
- **Segunda amarilla:** dos amarillas = roja. Secuencia: primera amarilla → segunda amarilla (se registra la tarjeta y el evento) → expulsión. Usa el **mismo estado de expulsión** que una roja directa. Arena no usa este sistema.
- Roja (directa o por doble amarilla): el jugador queda fuera, no vuelve y no se repone; el equipo continúa con uno menos.
- **Mínimo de jugadores:** con **3 activos** el partido continúa. Con **menos de 3** el partido **termina inmediatamente** con `MatchTerminationReason = InsufficientPlayers`; el rival obtiene la **victoria técnica** y **se conserva el marcador existente**; no se sigue simulando. Esto aplica también a un Knockout: no hay prórroga ni penales tras una terminación por jugadores insuficientes.
- **Portero expulsado:**
  1. Hay portero suplente **y** queda una sustitución → entra el suplente y **consume 1 sustitución**.
  2. No queda ninguna sustitución → **un jugador de campo puede ocupar la portería** (F.4); no se crea ningún cambio.
  3. El equipo está en el mínimo permitido → **no se crea ninguna sustitución fuera del límite**.
- **Nunca** hay una sustitución fuera del límite de 3 (la expulsión no concede cambios).

### F.4 Jugador de campo como portero (solo Classic, excepcional)
No se crean atributos nuevos ni se modifica `PlayerDefinition`; no se guarda nada ni se convierte al jugador en un portero real. Las 8 capacidades se **derivan en runtime**, cada una como combinación de los atributos oficiales indicados:

| Capacidad de portero | Derivada de |
|---|---|
| Reflexes | Agility + Control |
| Handling | Control + Technique |
| Positioning | Defense + Agility |
| Diving | Agility + Speed |
| Kicking | Passing + Technique |
| Distribution | Passing + Control |
| Command | Comportamiento contextual / IA (no es un valor derivado de atributos) |
| Recovery | Stamina + Agility |

Debe rendir **claramente peor** que un portero especializado: la media de cada par se multiplica por un factor de falta de especialización de **0,75**, calibrado con la base de datos (ver N.3, fila 10). El GRL de un portero real sigue siendo la media exacta de sus 8 atributos; el portero de emergencia no tiene GRL.

### F.5 Sustituciones y tiempos
Contador único por equipo (máx. 3) en `MatchState`; solicitud → `SubstitutionRules.Validate` → `PlayerSubstituted`.
- **Classic:** respeta las interrupciones/reanudaciones del modo (balón muerto, descanso).
- **Arena:** las sustituciones **no detienen artificialmente un ataque**; solo se ejecutan en un **momento seguro / interrupción válida del Match Engine** (p. ej. tras un gol, en el cambio de lado o cuando el engine declare una ventana segura). No hay una ventana tradicional obligatoria.
- Un sustituido o expulsado no vuelve.

### F.6 Stamina entre periodos
- **Classic, descanso:** +25 % de la stamina máxima.
- **Arena, cambio de lado:** +10 % de la stamina máxima.
- Nunca supera el 100 %. Ambos valores son configurables en `MatchModeDefinition`.

---

## G. REGLAS POR MODO (ARENA vs CLASSIC)

| Aspecto | ARENA | CLASSIC |
|---|---|---|
| Campo | **Cerrado, el balón rebota en paredes/límites** | Líneas con reanudación |
| Continuidad | Partido continuo | Se interrumpe por infracciones y salidas |
| Duración | **2 × 6 min (configurable)** | **2 × 8 min (configurable)** |
| Descanso | Sin descanso tradicional; **cambio de lado** al acabar el 1.º tiempo (**+10 % stamina**) | Con descanso, HalfTime (**+25 % stamina**) |
| Faltas / tarjetas / penaltis / tiros libres | **No existen** | Sí |
| Fuera de juego | **No** | Sí (2.º último defensor; el portero cuenta como defensor) |
| Córner / saque de banda / saque de puerta | **No** | Sí |
| Contacto físico | Agresivo; afecta a balón/equilibrio/posesión; nunca infracción | Contacto legal vs infracción (`ContactJudge`) |
| Árbitro | No existe como sistema | Sí; perfil por parámetros |
| Ventaja | No | Sí |
| Lesiones | **No** | **No** |
| Disciplina | No hay tarjetas | Amarilla; **2 amarillas = roja**; roja = expulsión |
| Expulsiones | No | Equipo con uno menos si queda ≥3 activos; con <3 el partido termina (`InsufficientPlayers`, victoria técnica, marcador conservado) |
| Empate | Liga/amistoso: empate. Knockout: prórroga → penales | Liga/amistoso: empate. Knockout: prórroga → penales (**igual que Arena**: depende del tipo de competición, no del modo) |
| Portero de emergencia | No aplica | Capacidades derivadas en runtime (F.4) |
| Plantilla | 6 + 4 (≥1 portero suplente) | 6 + 4 (≥1 portero suplente) |
| Sustituciones | Máx. 3; solo en momento seguro del engine, sin parar un ataque | Máx. 3; en las interrupciones/reanudaciones de Classic |
| Eventos emitidos | Gol, pase, tiro, entrada, recuperación, posesión, sustitución | + falta, fuera de juego, córner, banda, penalti, tarjeta |
| Estadísticas | Sin faltas/tarjetas/córners/fueras de juego | Todas |
| Reanudaciones | Saque inicial (inicio de tiempo y tras gol) | Todas |

Mecanismo: `MatchModeDefinition` (datos) + `IModeRules`. Un evento que el modo no declara **no puede emitirse**.

---

## H. CONTROLES DEFINITIVOS

Joystick izquierdo: dirección + intensidad (0–30 % trote, 30–70 % carrera, 70–100 % sprint progresivo). **No hay botón de sprint.**

### H.1 Ataque

| Botón | Gesto | Acción | Condiciones | Atributos |
|---|---|---|---|---|
| A | tap | Tiro normal / potente (según carga) | Balón controlado o disponible | Shooting, Finishing, Control |
| A | swipe ↓ | Tiro con efecto (`CurvedShot`) | Posesión; rango razonable | Shooting, Finishing, Technique, pie, presión, equilibrio |
| A | swipe ↑ | Vaselina (`Chip`) | Espacio sobre el portero; ángulo | Finishing, Technique, Control, distancia |
| B | tap | Pase corto | Receptor válido | Passing, Technique, Control |
| B | swipe ↓ | Pedir pared (`WallPass`) | Compañero + línea + presión evaluados; devolución viable | Passing, Technique, Control, Creativity |
| B | swipe ↑ | Pase al hueco (`ThroughPass`) | Receptor con carrera + espacio | Passing, Technique, Creativity, RiskPreference |
| C | swipe ↑ | Regate / filigrana (`SkillMove`) | **Solo si el jugador tiene la skill disponible** y hay espacio | Technique, Dribbling, Control, Agility, Stamina |
| C | swipe ↓ | Pase elevado con habilidad (`SkillLobPass`) | **Solo con skill de pase especial** | Technique, Passing, Control, pie |
| C | tap | **Sacar al portero** (`GoalkeeperRush`; contextual) | Prioridad sin posesión o balón en tu tercio; la IA del portero puede rechazarla | Positioning, Command, Diving del portero; contexto |

### H.2 Defensa (sin balón) — definitiva

| Botón | Gesto | Acción | Condiciones | Atributos |
|---|---|---|---|---|
| A | tap | Presión / contener | Rival con balón en radio de presión | Defense, Agility, Acceleration, Stamina |
| A | swipe ↓ | Entrada de pie | Rival a alcance | Defense, Strength, Agility, timing |
| A | swipe ↑ | Entrada deslizante | Mayor riesgo y contacto | Defense, Strength, Acceleration |
| B | tap | Disputa física | Duelo cuerpo a cuerpo | Strength, Defense, BodyType |
| B | swipe ↓ | Interceptar / bloquear | Línea de pase o tiro abierta | Defense, Agility, anticipación |
| B | swipe ↑ | Despejar | Balón cerca / en peligro | Defense, Strength |
| C | tap | **Sacar al portero** | Prioridad sin posesión o balón en tu tercio; el portero puede rechazar | ver arriba |

Notas:
- C **no tiene otra función defensiva principal**; no existe C+swipe defensivo.
- Orden de portero: la acción nunca se fuerza; devuelve `Accepted` o `Refused(motivo)` (la IA del portero valora distancia, rival, espacio y riesgo; en Classic también fuera de juego y posición).
- Arena: todo contacto es legal. Classic: `ContactJudge` separa contacto legal de infracción (velocidad, ángulo, balón, timing, intensidad, zona, árbitro).

---

## I. SUSTITUCIONES Y PLANTILLA

- **6 titulares + 4 suplentes = 10 disponibles, con al menos 1 portero suplente.**
- **Máximo 3 sustituciones por equipo y partido.** Un único contador, sin ventanas, sin cupo por tiempo, sin lesiones. 4 suplentes **no** son 4 cambios; la 4.ª solicitud se rechaza, incluido el cambio de portero.
- Arena: solo en momento seguro del engine (F.5). Classic: en las interrupciones del modo.
- Portero expulsado: F.3. Un expulsado o sustituido no vuelve. Nada concede cambios extra.

---

## J. TEST PLAN (39 casos obligatorios + guardas)

Tests puros (NUnit, `dotnet test`), sin nombres reales en identificadores. Estado: 🔧 = ya existe (cierre); ⏳ = se escribe con el sistema.

| # | Caso | Estado |
|---|---|---|
| 1 | Arena puede terminar empatado | 🔧 `MatchEngineTests` |
| 2 | Classic puede terminar empatado | 🔧 `MatchEngineTests` |
| 3 | Arena Normal no tiene prórroga | 🔧 `MatchEngineTests` |
| 4 | Classic Normal no tiene prórroga | 🔧 `MatchEngineTests` |
| 5 | Arena Knockout empatado → prórroga | 🔧 `MatchEngineTests` |
| 6 | Classic Knockout empatado → prórroga | 🔧 `MatchEngineTests` |
| 7 | Knockout empatado tras prórroga → penales | 🔧 `MatchEngineTests` |
| 8 | League/Friendly nunca activa penales | 🔧 `MatchEngineTests` |
| 9 | Classic con <3 activos → terminado (`InsufficientPlayers`, victoria técnica, marcador conservado) | 🔧 `MatchEngineTests` |
| 10–13 | Arena no genera faltas / fuera de juego / córners / saques de banda | 🔧 `MatchEngineTests` |
| 14 | Arena rebota en las paredes | 🔧 `MatchEngineTests` |
| 15 | Máximo 3 sustituciones | 🔧 `MatchEngineTests` |
| 16 | El banquillo contiene portero suplente | 🔧 `MatchEngineTests` |
| 17 | Segunda amarilla → roja (mismo estado de expulsión) | 🔧 `MatchEngineTests` |
| 18 | Portero expulsado con suplente consume una sustitución | 🔧 `MatchEngineTests` |
| 19 | Sin sustituciones, un jugador de campo puede ocupar la portería | 🔧 `MatchEngineTests` |
| 20 | C tap ordena la salida del portero (y la IA puede rechazarla) | ⏳ f5/f7 |
| 21–24 | A↓ tiro con efecto · A↑ vaselina · B↓ pared · B↑ pase al hueco | ⏳ f5 |
| 25–26 | C↑ skill · C↓ pase especial (solo con skill disponible) | ⏳ f5 |
| 27 | Un jugador sin skill no puede ejecutarla | ⏳ f5 |
| 28 | No existe botón de sprint (guarda estructural; ya existe para `PlayerIntent`/`MovementStyle`/`FootballActionKind`) | 🔧 parcial |
| 29 | La stamina afecta progresivamente al rendimiento | ⏳ f3 |
| 30 | Descanso Classic recupera 25 % (tope 100 %) | 🔧 `MatchEngineTests` |
| 31 | Cambio de lado Arena recupera 10 % (tope 100 %) | 🔧 `MatchEngineTests` |
| 32 | `CoachBoost` no modifica atributos | 🔧 `MatchEngineTests` |
| 33 | `CoachBoost` no se aplica dos veces / el motor no consume `EffectiveOverall` | 🔧 `MatchEngineTests` |
| 34 | `EffectiveOverall = BaseOverall + CoachBoost` | 🔧 ya cubierto (sistema de entrenadores) |
| 35 | `Reaction` no es atributo oficial (no está en `PlayerAttributeId`, Overall ni tarjeta) | 🔧 ya cubierto por las guardas de atributos; marca legacy ⏳ f2 |
| 36 | `FootballActionKind` está desacoplado del Creator | 🔧 `FootballActionKindTests` |
| 37 | `FootballActionKind` conserva compatibilidad con los valores existentes | 🔧 `FootballActionKindTests` |
| 38 | `BallSimulator` es la fuente de verdad (el Rigidbody no altera `BallState`) | 🔧 `MatchEngineTests` |
| 39 | `MatchState` es la única fuente autoritativa | 🔧 `MatchEngineTests` |

Guardas adicionales (⏳ con su fase): `IResultResolver` no depende de `GameMode` ni `IModeRules` de `CompetitionMatchType`; ninguna comparación por nombre de jugador/entrenador/árbitro; Simulation no depende de Presentation/UI; Core sin `UnityEngine`; el motor no contiene formaciones; skills solo contrato; portero de emergencia no guarda atributos ni toca `PlayerDefinition`; determinismo por semilla y replay; sin allocations en el paso caliente.

---

## K. HOJA DE RUTA DE IMPLEMENTACIÓN

Cada fase: rama propia, tests verdes, sin push/PR salvo que se pida.

0. ✅ **Decisiones y cierre de arquitectura** (este documento) + `FootballActionKind` movido a Core.
1. ✅ **Núcleo determinista (HECHO, ver N):** paso fijo; `MatchState` ampliado (modo, tipo de competición, fases `HalfTime/ExtraTime/PenaltyShootout/FullTime`, motivo de finalización); `MatchModeDefinition` + `IModeRules`; `CompetitionRules` + `IResultResolver` (empate/prórroga/penales, tests 1–8); bus de eventos filtrado por modo; `BallSimulator` autoritativo con Rigidbody visual reflejando Core (tests 38–39, 32–33).
2. **Datos mínimos:** banquillo 6+4 (≥1 GK suplente), periodos por modo (Arena 2×6, Classic 2×8), contratos `SkillDefinition`/`PlayerSkillSet` sin catálogo ni asignaciones, marca `legacy/internal derived` de `Reaction`.
3. **Locomoción y stamina:** `FatigueCurve` continua, recuperación 25 %/10 % configurable, BodyType físico, selector de jugador.
4. **Acciones base:** control, pase, tiro (normal/efecto/vaselina), regate, protección; contactos y duelos.
5. **Input:** `ActionPadModel` (A/B/C + gestos, ataque y defensa de H) sobre `PlayerIntent`; decidir si hacen falta valores nuevos del enum para las acciones defensivas.
6. **Arena completo:** campo cerrado con rebote, 2×6, cambio de lado, sustituciones en momento seguro, estadísticas.
7. **Portero:** IA, orden C con aceptación/rechazo, portero de emergencia derivado (F.4).
8. **IA de jugador y de equipo** + transiciones.
9. **Formaciones** (fase propia): `FormationBehaviorProfile` y formaciones creadas por Claude desde jugadores, roles, zonas y entrenadores.
10. **Habilidades** (fase propia): catálogo y asignación validadas por ti.
11. **Classic:** `ContactJudge`, fuera de juego, reanudaciones, tarjetas (2 amarillas = roja), expulsiones y mínimo de 3 activos, penaltis y tanda, árbitro por parámetros, ventaja, descanso.
12. **Estadísticas, replay/log determinista, perfilado móvil.**
13. **Presentación** sobre estado y eventos. Online fuera de alcance.
14. **Balance del Coach Boost** en simulación (solo cuando se defina su influencia; hasta entonces el motor no lo consume).

Dependencias duras: 1 → todo; 2 → 4/5/6; 4 → 6/11; 8 y 9 → pulir 6/11.

---

## L. DECISIONES

### L.1 Decisiones cerradas

| # | Decisión | Dónde |
|---|---|---|
| 1 | Arena: campo cerrado con rebote; sin córners, bandas, saques de puerta, faltas, fuera de juego, tiros libres por falta ni tarjetas; sin lesiones | C3, G |
| 2 | **`GameMode` ≠ `CompetitionMatchType`**; el desempate depende solo del tipo de competición | C20, F.2, B.3 |
| 3 | Un empate es un empate (Arena y Classic no eliminatorios); Knockout: prórroga → penales | F.2 |
| 4 | Prórroga y penales solo con `Knockout`, nunca por modo ni por un simple empate | F.2 |
| 5 | `BallSimulator` autoritativo; Rigidbody solo presenta | C1 |
| 6 | `MatchState` existente ampliado, única fuente de verdad | C4, B.4 |
| 7 | Stamina continua; descanso Classic +25 %, cambio de lado Arena +10 %, tope 100 %, configurable | C6, F.6 |
| 8 | `Reaction` legado/interno/derivado; no es atributo oficial; no se elimina aún | C5, M |
| 9 | 12 atributos oficiales; ninguno nuevo | B.3 |
| 10 | Portero de emergencia con 8 capacidades derivadas en runtime, sin atributos ni cambios en `PlayerDefinition` | F.4 |
| 11 | Segunda amarilla = roja con el mismo estado de expulsión | F.3 |
| 12 | <3 activos → fin inmediato, `InsufficientPlayers`, victoria técnica, marcador conservado | F.3 |
| 13 | Portero expulsado: suplente + cambio disponible → consume 1; sin cambios → jugador de campo; nunca cambio extra | F.3 |
| 14 | 6 titulares + 4 suplentes (≥1 GK); máx. 3 sustituciones; Arena en momento seguro, Classic en interrupciones | C14, F.5, I |
| 15 | Coach Boost: ≥80 → +1, <80 → 0, nunca −1; sin tocar atributos ni `BaseOverall`; **el motor no consume `EffectiveOverall` todavía** | C13 |
| 16 | Controles de ataque y defensa definitivos; C tap = sacar portero; C sin otra función defensiva | H, C10 |
| 17 | C tap contextual; la IA del portero puede rechazar | C10 |
| 18 | `FootballActionKind` movido a Core con valores existentes intactos y 7 nuevos al final | C8 🔧 |
| 19 | Skills: solo contrato `SkillDefinition`/`PlayerSkillSet`; sin catálogo ni asignaciones | C7 |
| 20 | Formaciones data-driven; ninguna definitiva ni en el motor | C12 |
| 21 | Duración Arena 2×6 / Classic 2×8, configurable | C15 |
| 22 | Árbitros: preparados para los 7 parámetros, sin lógica por nombre, relevantes en Classic | B.2 |
| 23 | **Tanda: 3 lanzamientos por equipo y muerte súbita si siguen empatados** (configuración centralizada) | F.2, N.3 |
| 24 | Convenciones de la Fase 1 cerradas: cambio de lado, saque tras el descanso, prórroga, recuperaciones, descanso, rebote, portero expulsado, portero de emergencia | N.3 |

### L.2 Qué queda abierto
No queda **ninguna decisión de diseño ni de convención de la Fase 1** abierta: la tanda (3 + muerte súbita), la prórroga, las recuperaciones, el descanso, el rebote, el portero de emergencia y las convenciones de N.3 están **cerradas** (con su valor, configurable). Lo único pendiente es de **fases posteriores**:
1. Valores de la `FatigueCurve` (fase 3).
2. Si hacen falta valores nuevos del enum para acciones defensivas (presión/contención, entrada deslizante, disputa física, despeje): fase 5.
3. Re-afinar en la fase de balance (no bloquea nada): rebote de Arena, factor del portero de emergencia, duración de prórroga.

---

## M. DEUDA CONOCIDA (solo lo que sigue pendiente)

1. **`Reaction` (legado):** `PlayerAttributes.Reaction` sigue consumido por `ReactionModel`, `AnticipationModel` y `AiErrorModel`. Está decidido que es *legacy / internal derived value*, pero la marca en código aún no se escribió (la Fase 1 no tocó `PlayerAttributes`). Migrar la IA y eliminarlo: fase de IA (8).
2. **Jugadores y balón en Unity:** con el balón dirigido por el Core (`MatchBridge`) el rigidbody es cinemático, así que **ya no colisiona físicamente con los jugadores**. Los toques se reportan por `IBallCoreLink`/`ApplyBallImpulse`; el contacto jugador–balón real (control, pase, tiro) es la fase 4. Mientras tanto la escena Sandbox sigue con la física antigua porque el modo "core-driven" es opt-in (hay que añadir `MatchBridge`). `Docs/ARCHITECTURE.md` conserva "solo el balón es dinámico" para el sandbox.
3. **Código no verificado en Unity:** `MatchBridge`, `IBallCoreLink` y los cambios de `BallController`/`BallPhysics` se comprobaron con un compilador contra stubs de UnityEngine escritos a mano (solo detecta errores de tipos), **no se ejecutaron en el editor**.
4. **`MatchController.Tick(dt, BallObservation)` (camino antiguo):** se mantiene para compatibilidad (los 40 tests previos lo usan); el camino autoritativo es `Advance`. Cuando ningún host use `Tick`, retirarlo.
5. **Re-afinado en balance (no bloquea):** rebote de Arena (0,75), factor del portero de emergencia (0,75), duración de prórroga (un tercio del periodo) y descanso de Classic (10 s). Son valores decididos y configurables, no pendientes.
6. **Convenciones de N.3:** cerradas. La única consecuencia pendiente es que `MatchBridge` no confirma los saques por sí solo (no hay jugadores que colocar todavía).
7. **Entrenadores, árbitro, formaciones y habilidades siguen sin conectarse al motor** (a propósito): `CoachBoost` aislado, árbitro sin lógica, ninguna formación, ninguna habilidad.
8. **Sin movimiento de jugadores, IA, pases, tiros, regates ni entradas:** `GiveBallTo`/`ApplyBallImpulse` son las puertas explícitas para esos sistemas; el pad de acciones (fase 5) y las acciones defensivas del enum siguen pendientes.
9. **Perfiles de animación de las 7 acciones nuevas:** solo `Planned`, sin clips ni arte.
10. **Árbitros:** los 5 son datos que nadie lee todavía (fase 11); `IssueCard` es la API que usará el árbitro.
11. **Tanda de penales:** solo estado y conteo; la ejecución física del penalti, sus controles y animaciones son una fase posterior. Los penaltis por infracción (`PenaltyAwarded`) existen como tipo de evento permitido en Classic pero nada los genera todavía.
12. **`Break` y `Shootout`** se añadieron a `MatchPhase` (al final); los hosts que hagan `switch` exhaustivo sobre `MatchPhase` deben tratarlos (no existe ninguno en el repositorio fuera de los tests).

---

## N. IMPLEMENTACIÓN DE LA FASE 1 (estado real)

### N.1 Estados: `MatchPhase` + `MatchPeriod` (contradicción detectada y resuelta)
**Problema:** la lista pedida (`PreMatch, Kickoff, FirstHalf, HalfTime, SecondHalf, ExtraTime*, PenaltyShootout, FullTime`) mezcla dos preguntas distintas — *¿está el balón vivo o el partido esperando?* y *¿en qué parte del partido estamos?* — y el `MatchPhase` existente (`NotStarted, WaitingForKickoff, Playing, GoalCelebration, WaitingForRestart, Paused, Finished`) ya contesta la primera y lo usan ~40 tests y `MatchController`.
**Causa:** meter "primer tiempo" y "segundo tiempo" en `MatchPhase` obligaría a duplicar `Playing`/`WaitingFor*`/`GoalCelebration` por cada periodo.
**Solución:** dos ejes ortogonales **dentro del mismo `MatchState`** (una sola fuente de verdad): `MatchPhase` (existente, + `Break` y `Shootout` añadidos al final) y `MatchPeriod` (nuevo: `PreMatch, FirstHalf, HalfTime, SecondHalf, ExtraTimeFirstHalf, ExtraTimeBreak, ExtraTimeSecondHalf, PenaltyShootout, FullTime`). `Kickoff` = `Phase WaitingForKickoff`. Invariantes (comprobadas por tests tras cada transición): `NotStarted ⇔ PreMatch`, `Break ⇔ HalfTime | ExtraTimeBreak`, `Shootout ⇔ PenaltyShootout`, `Finished ⇔ FullTime`.
**Impacto:** ninguno en el código existente; los consumidores leen `State.Period` para "qué parte" y `State.Phase` para "qué está pasando".

### N.2 Qué existe ahora
| Pieza | Dónde | Notas |
|---|---|---|
| `GameMode`, `CompetitionMatchType`, `MatchPeriod`, `MatchTerminationReason`, `BallMode`, `MatchDefaults` | `Core/Match/MatchModes.cs` | Todos los números por defecto de la arquitectura viven en `MatchDefaults`. |
| `MatchSettings` (ampliada) | `Core/Match/MatchTypes.cs` | Modo, competición, periodos, descanso, recuperaciones, prórroga, tanda, sustituciones, mínimo de jugadores, rebote de Arena, paso de simulación. `CreateArena()`/`CreateClassic()` dan los valores de la arquitectura (periodos, descanso, recuperaciones, prórroga, tanda de 3); `new MatchSettings()` conserva el comportamiento antiguo (1 periodo de 180 s, Classic, Normal). `IsValid` rechaza valores imposibles (p. ej. más de 3 sustituciones). |
| `MatchState` (ampliado) | `Core/Match/MatchState.cs` | Configuración (`Settings`) separada del estado mutable. Contiene id, modo, competición, `Phase`, `Period`, marcador, reloj del periodo, `MatchTimeSeconds`, balón (`MatchBall`), `Home/Away` (`TeamMatchState`), dirección de ataque actual, prórroga, tanda, goles, resultado y motivo. |
| `MatchBall` | `Core/Match/MatchBall.cs` | Envuelve el `BallState` **existente** (no se duplicó) + `BallMode`, posesión (equipo/jugador), último y penúltimo toque (para la asistencia). |
| `TeamMatchState`, `MatchPlayer`, `MatchSquad`, `SquadMember` | `Core/Match/MatchRoster.cs` | 6+4 validado (1 portero titular, ≥1 portero suplente); estado por jugador (`OnPitch/Bench/SubstitutedOff/Dismissed`), amarillas, `InGoal`, `IsEmergencyKeeper` y el `PlayerRuntimeState` **existente**. |
| `IGameModeRules`, `ArenaRules`, `ClassicRules` | `Core/Match/GameModeRules.cs` | Qué tiene cada modo, qué eventos permite, cómo se juzga el balón, cuándo se puede sustituir y qué colisionadores crea. El motor no compara el modo (un test lo vigila). |
| `CompetitionResolver` | `Core/Match/CompetitionResolver.cs` | Empate/prórroga/penales. No recibe `GameMode`. |
| `PenaltyShootoutState` | `Core/Match/PenaltyShootout.cs` | Estado y conteo de la tanda (alternancia, victoria anticipada, muerte súbita). Sin ejecución física. |
| `GameplayEventBus`, `GameplayEvent`, `GameplayEventKind` | `Core/Match/GameplayEvents.cs` | 26 tipos; el bus rechaza (y cuenta) los que el modo no tiene; numeración determinista. |
| `ArenaWalls` | `Core/Ball/ArenaWalls.cs` | 4 paredes con rebote (mismo cálculo que `PlaneCollider`), bocas de portería abiertas, cuenta rebotes para el evento. |
| `MatchController` (evolucionado) | `Core/Match/MatchController.cs` | Motor: periodos, descanso/cambio de lado, recuperación de stamina, prórroga, tanda, fin, goles con goleador/asistente, posesión, sustituciones, tarjetas, expulsiones, insuficiencia de jugadores, `Advance` (autoritativo) y `Tick` (antiguo). |
| `EmergencyKeeperProfile` | `Core/Match/EmergencyKeeperProfile.cs` | Las 7 capacidades derivadas en runtime con las fórmulas de F.4; `Command` contextual; nunca se guardan. |
| `SimulationClock` | `Core/SimulationClock.cs` | Convierte frames en pasos fijos (60 Hz por defecto, con tope por llamada). |
| `MatchRules.Evaluate` (sobrecarga) | `Core/Match/MatchRules.cs` | Misma regla con la dirección de ataque explícita (cambia con el cambio de lado). La firma antigua sigue igual. |
| Puente Unity | `Gameplay/MatchBridge.cs`, `IBallCoreLink.cs`, cambios en `BallController.cs` y `BallPhysics.cs` | Ver N.4. |
| `FootballActionKind` | `Core/FootballActionKind.cs` | Sin cambios en esta fase (0–22). |

### N.3 Convenciones de la Fase 1 — CERRADAS
Estas convenciones empezaron como provisionales; quedan **definitivas**, cada una con su razón. Todas son configurables en `MatchSettings` (salvo las reglas de comportamiento) y sus valores viven en `MatchDefaults`.

| # | Convención | Valor / regla | Razón |
|---|---|---|---|
| 1 | **Tanda de penales** | 3 lanzamientos por equipo; si siguen empatados, muerte súbita; termina antes si un equipo es inalcanzable; empieza `ShootoutFirstTeam` (Home por defecto; el sorteo es cosa de la capa de competición, no del Core determinista) | Decisión del diseño |
| 2 | **Quién saca** | 1.er tiempo `KickoffTeam`; 2.º tiempo el otro; prórroga: 1.ª parte `KickoffTeam`, 2.ª el otro; tras un gol saca quien lo encajó | Alternancia habitual del fútbol, determinista |
| 3 | **Cambio de lado** | En Arena (decidido) y también en Classic, al descanso y al descanso de prórroga (`SwitchSidesAtBreak`). No hay cambio entre el reglamentario y la prórroga | Fútbol tradicional; Arena lo conserva como único "descanso" |
| 4 | **Duración de la prórroga** | 2 periodos de **un tercio** de un periodo reglamentario: Arena 120 s, Classic 160 s | Como 15 min son un tercio de 45: proporcional en cada modo |
| 5 | **Recuperación de stamina en prórroga** | Antes de la prórroga y en su descanso: **la del intervalo del modo** (Classic 25 %, Arena 10 %) | Sin curvas nuevas: reutiliza lo decidido y mantiene Classic más "descansado" que Arena |
| 6 | **Descanso de Classic** | `HalftimeSeconds` = 10 s de tiempo de Core (fase `Break`); Arena no tiene `Break` | Pausa breve para sustituciones y presentación; cualquier mayor duración es de UI |
| 7 | **Rebote de Arena** | Restitución 0,75 y fricción 0,2; paredes **sin techo** y porterías abiertas | Algo más vivo que el bote del suelo (0,7) para el ritmo arcade; el balón nunca sale |
| 8 | **Portero expulsado** | `NeedsGoalkeeper` bloquea el siguiente saque hasta que entre el suplente (`TryReplaceDismissedKeeper`, consume 1 cambio y sale un jugador de campo) o un jugador de campo tome los guantes (`TryAssignEmergencyKeeper`, solo sin cambio o sin portero suplente; no es sustitución). Nunca más de 3 cambios | Un equipo no puede reanudar sin portero; respeta el límite |
| 9 | **Sustituciones** | El hueco de portería solo pasa a un portero y un portero solo entra por ese hueco (`GoalkeeperMismatch`); un sustituido o expulsado no vuelve. Momentos seguros: Arena = esperando saque/celebración/cambio de lado; Classic = además reanudaciones y descanso; la pausa no cuenta | Coherente con "no parar un ataque" y "respetar las interrupciones" |
| 10 | **Portero de emergencia** | Las 7 capacidades derivadas (F.4) × **0,75**. Calibración con la base de datos: los 136 jugadores de campo dan de media 82,5 con las fórmulas sin factor (máximo 89,2) y los 20 porteros 82,0 de media en esas 7 capacidades (mínimo 74,6). Con 0,75 el jugador de campo medio queda en ≈ 61,9 y el mejor en ≈ 66,9, por debajo del peor portero real (74,6) | Sin factor un jugador de campo igualaría a un portero; con 0,75 es claramente peor sin ser absurdo |
| 11 | **Asistencia / gol en propia** | Asistencia = el toque anterior al último si es de otro jugador del mismo equipo que marca; gol en propia = último toque del rival | Usa el historial de toques del balón |
| 12 | **Terminación técnica** | El rival gana, el marcador se conserva (puede ser empate), sin prórroga ni penales, ni siquiera en eliminatoria; se comprueba en cada expulsión y al final de cada periodo | Decisión de la Fase 0 |
| 13 | **Tanda sin muerte súbita** (`ShootoutSuddenDeath = false`) | Puede acabar sin ganador (`Reason = PenaltyShootout`, empate); por defecto la muerte súbita está activa | Opción de configuración; no se usa por defecto |

### N.4 El balón y Unity
- **Core:** `MatchController.Advance(dt)` llama a `BallSimulator.Step` (con `ArenaWalls` en Arena), luego juzga el balón con las reglas del modo. El estado vive en `MatchState.Ball.Physics`. Las únicas entradas externas son explícitas: `ApplyBallImpulse`, `GiveBallTo`, `ReleaseBall`, `RegisterTouch`, `SetBallMode`.
- **Unity:** `MatchBridge` posee el `MatchController`, avanza con `SimulationClock` (pasos fijos; el tiempo de frame solo decide cuántos pasos tocan) y llama a `BallController.ApplyCoreState`. `BallController.SetCoreDriven(true)` pone el rigidbody cinemático, redirige `ApplyTouch` hacia `IBallCoreLink.RequestTouch` y `BallPhysics` deja de aplicar fuerzas. El puente no lee el rigidbody de vuelta (lo vigila un test sobre el texto del código).
- **Determinismo:** `Advance` no usa tiempo de Unity, aleatoriedad ni reloj de pared (test de texto sobre Core/Match y Core/Ball). Mismas llamadas ⇒ mismo balón y mismo registro de eventos (test).

### N.5 Qué NO se hizo (a propósito)
Movimiento de jugadores, IA, pases/tiros/regates/entradas, habilidades, formaciones, `Coach Boost` en simulación, árbitro, animaciones, gráficos, UI y online. No se crearon atributos nuevos ni se modificaron los contratos de jugadores, porteros, entrenadores ni árbitros.

---

## O. FASE 2 — equipos, Player Library y sorteo (resumen)
Detalle en [TEAMS_AND_ROSTERS.md](TEAMS_AND_ROSTERS.md). Lo que afecta a este documento:
- **Plantilla de 10 = 2 porteros + 8 de campo** (6 activos con 1 portero; 4 suplentes con 1 portero suplente). La especificación de la Fase 2 decía "exactamente 1 portero" y "al menos 1 portero suplente" a la vez; el usuario eligió 2 porteros, que **conserva intacta la regla de la Fase 1** (`MatchSquad`).
- **Porteros:** 12 plantillas necesitan 24 y la base tenía 20; en la Fase 2.1 se añadieron 4 porteros reales (`FS27_GOALKEEPER_ADDITIONS_V1.md`) y la biblioteca real queda en **160 jugadores = 24 porteros + 136 de campo**. El sorteo real ya se ejecuta sin porteros de prueba.
- El Match Engine no conoce rosters ni sorteos; solo recibe un `MatchSquad` (`TeamRoster.TryToMatchSquad`).
- `PlayerDefinition.TeamId` y `PlayerAttributes.Reaction` están marcados `[Obsolete]` (legacy); ninguno es autoridad ni atributo oficial.

---

## P. FASE 3 — locomoción y stamina (resumen)
Detalle y fórmulas en [LOCOMOTION_STAMINA.md](LOCOMOTION_STAMINA.md). Lo que afecta a este documento:
- Cumple las fases 3 (locomoción) y la parte de stamina de la fase 3 de la hoja de ruta: curva de fatiga continua, `BodyType` y `Strength` sin usar todavía.
- El estado de movimiento vive en el `MatchState` (`MatchPlayer.Locomotion`); el motor lo avanza a 60 Hz solo con el balón vivo; Unity solo lo refleja.
- Recuperación de intervalos: las mismas fracciones de la Fase 1 (Classic 25 %, Arena 10 %), aplicadas también a la stamina 0–100.
- **No implementado (fases posteriores):** pases, tiros, regates, entradas, control del balón, skills, IA, formaciones, contacto entre jugadores, límites del campo, animaciones.

---

## Q. FASE 4 — acciones y balón (resumen)
Detalle y fórmulas en [ACTIONS_AND_BALL.md](ACTIONS_AND_BALL.md). Lo que afecta a este documento:
- Cumple la fase 4 de la hoja de ruta (acciones base: control, pase, tiro normal/efecto/vaselina, conducción, protección, contactos y duelos) y las acciones defensivas del enum de la fase 5 (se añadieron al final de `FootballActionKind`: `Pressure`, `SlidingTackle`, `PhysicalChallenge`, `Clearance`). El pad con gestos A/B se reduce a un contrato neutro (`ActionInput` + `ActionMapper`); la capa táctil de Unity sigue pendiente.
- El Core es la autoridad: `Input → MatchController → acción → balón (BallSimulator) → MatchState → Unity`. La deuda M.2 (jugadores y balón en Unity) queda resuelta a nivel de Core: el contacto jugador–balón es determinista y vive en el motor; Unity solo refleja.
- Posesión: una sola fuente en `MatchBall`; el balón nunca está pegado al jugador. El portero usa el mismo sistema (rush solo contrato). Arena y Classic respetan sus reglas.
- **No implementado (fases posteriores):** IA, tácticas, formaciones, entrenadores, catálogo de skills, árbitro y reanudaciones por falta, cabezazo/centro/tiro colocado, animaciones, cámara, UI.

---

## R. FASE 5 — Data lock (resumen)
Detalle en [DATA_LOCK.md](DATA_LOCK.md). Lo que afecta a este documento:
- La base está cerrada y validada (`GameDatabaseLoader` + `DataLockValidator`): 160 jugadores con identidad ficticia y `ShortName`, 37 skills y un `PlayerSkillSet` por jugador de campo, 12 equipos con formación, 20 coaches (53 variantes), 5 árbitros y 5 niveles de dificultad.
- Las skills usan los contratos de la Fase 2 (`SkillDefinition`, `PlayerSkillSet`); 3 tipos desbloquean las acciones de skill de la Fase 4 (`SkillMove`, `SkillLobPass`, `CurvedShot`).
- Sin cambios de comportamiento de partido. No implementado: IA, tácticas de gameplay, árbitro funcional, competición.

---

## S. FASE 6 — Skills reales (resumen)
Detalle en [SKILLS_REAL.md](SKILLS_REAL.md). Lo que afecta a este documento:
- Las 37 skills del catálogo ya **cambian cómo se resuelven las acciones** dentro del pipeline de la Fase 4 (`ActionRequest → MatchController → BallSimulator → ActionResult`): **25 REAL, 0 solo-permiso, 12 DEFERRED** (dependen de IA, transiciones, centros, cabezazos o balón parado, que no existen aún).
- Un solo permiso: `CanUseAction` (`SkillResolver`); `ActionRequest.SkillGranted` queda como legado solo para partidos sin datos de skills. Un solo catálogo, una sola tabla de comportamiento (`SkillBindings`), un solo resolutor, un solo `BallSimulator`.
- Nueva acción real: `SkillMove` (regate con 3 estilos y 3 desenlaces). `CurvedShot` y `SkillLobPass` resueltos con la maestría del jugador, el pie y la stamina. Botón **C** (skill) añadido al esquema de controles (H): con balón, toque = skill move, arriba = pase por alto, abajo = tiro con efecto.
- Las skills no cambian atributos, Overall, GRL ni Coach Boost; la stamina es la de locomoción; el azar sale solo del generador sembrado del partido.
- **No implementado (fases posteriores):** IA, tácticas, transiciones, centros, cabezazos, balón parado, árbitro funcional, competición, Unity (HUD del botón C, animaciones).
