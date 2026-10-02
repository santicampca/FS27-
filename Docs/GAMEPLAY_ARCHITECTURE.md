# FS27 — Arquitectura de gameplay V1 (6v6)

Estado: **diseño, con las decisiones de la Fase 0 aplicadas. No hay código nuevo.** Este documento audita la especificación del Superprompt Maestro contra lo que ya existe en el repositorio y fija el contrato y la hoja de ruta para implementar.
Nada aquí cambia contratos existentes de jugadores, porteros, entrenadores o árbitros. Las ampliaciones que se mencionan (banquillo, periodos, estados de partido) son **propuestas aditivas para la fase de implementación**, no cambios hechos.

Leyenda: ✅ resuelto por decisión de Fase 0 · ⚠ sigue abierto (ver L.2).

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
| Creator Engine (comportamientos, DNA, `FootballActionKind`) | `Core/Creator/*` | Hecho; identidad/animación, no reglas. |

### A.2 Conflictos entre la especificación y el proyecto

Formato: conflicto · sistema afectado · **resolución**.

**C1 ✅ Dos físicas de balón.** `Gameplay/BallPhysics.cs` y `BallController.cs` mueven el balón con el `Rigidbody` de Unity; `Core/Ball/BallSimulator.cs` (puro) no se usa en Gameplay.
Resolución (decisión 8): **`BallSimulator` es la fuente autoritativa.** El `Rigidbody` deja de ser la verdad; la presentación física de Unity refleja el estado de Core (seguidor visual, sin decidir nada). Paso fijo, determinista siempre que sea razonable. Impacto: `BallPhysics/BallController/BallInteractor` se adaptan en la fase de implementación; `docs/ARCHITECTURE.md` ("solo el balón es dinámico") queda obsoleto entonces.

**C2 ✅ `MatchRules` genera córners, saques de banda y saques de puerta siempre.** No existe el concepto de modo.
Resolución: reglas **por modo, en datos** (`MatchModeDefinition` + `IModeRules`). Classic = comportamiento actual de `MatchRules`; Arena no usa la regla de líneas.

**C3 ✅ Arena y los límites del campo.**
Resolución (decisión 1): **campo cerrado, el balón rebota en paredes/límites.** Sin córners, saques de banda, saques de puerta, faltas ni fuera de juego; el juego continúa. El gol cuenta solo dentro de la boca de la portería; el resto de la línea de fondo rebota como una pared. Tras una parada el portero juega el balón sin reanudación formal. Solo hay saque inicial tras gol y al empezar cada tiempo.

**C4 ✅ `MatchState` es una vista de solo lectura.**
Resolución (decisión 9): **se amplía el `MatchState` existente** y termina siendo el estado autoritativo del partido; no se crea un segundo estado. `MatchController` evoluciona a `MatchEngine` como único escritor. Los valores nuevos de `MatchPhase` se añaden **al final** (los actuales no se renumeran). Detalle de fases por modo en F y G.

**C5 ✅ Atributo legado `Reaction`.**
Resolución (decisión 10): se **mantiene temporalmente**, documentado como **valor legado, interno y derivado**; **no es uno de los 12 atributos oficiales**, no entra en Overall, tarjetas ni datos nuevos. No se elimina porque lo consumen `ReactionModel`, `AnticipationModel` y `AiErrorModel`. Marcado en código (comentario/`Obsolete`) y retirada: fase posterior (deuda M-1).

**C6 ✅ La stamina actual no es la de la spec.** `StaminaSystem` solo gasta en sprint.
Resolución: se **mantiene** `Stamina01` como único valor y se añade una `FatigueCurve` continua (multiplicadores de velocidad pico, aceleración, precisión, presión) y costes de energía por acción. El bloqueo de sprint a 0 ya cumple la spec. Los umbrales 70/40/20 son orientativos.

**C7 ✅ `Capability` ya está ocupado** (`GoalkeeperCapability`).
Resolución (decisión 11): las habilidades técnicas se llaman **`SkillDefinition` y `PlayerSkillSet`**. En esta fase solo existe el **contrato conceptual**. **No se crea catálogo y no se asigna ninguna habilidad a los 156 jugadores** (fase posterior y separada). `BehaviorId` (Creator) sigue siendo identidad/animación.

**C8 ⚠ `FootballActionKind` vive en el Creator y le faltan acciones.** (`Core/Creator/CharacterRuntimeContracts.cs`.) Faltan tiro con efecto, vaselina, pared, pase al hueco, regate/skill, pase especial, entrada de pie/deslizante, disputa física, despeje, orden al portero, presión/contención.
Propuesta (para implementación): un único `PlayerIntent`; los valores nuevos se añaden **al final** del enum y el enum se mueve a un archivo neutro de Core (mismo namespace), sin tocar contratos. Aún no se hace.

**C9 ⚠ Faltan los controles A/B/C.** Solo existe `VirtualStickModel`. Propuesta: `ActionPadModel` puro en `Core/Input`. Tabla definitiva en H.

**C10 ✅ C pulsación = sacar al portero.**
Resolución (decisión 2): C pulsación sin swipe **ordena la salida del portero**. La orden es **contextual**, con **prioridad cuando el usuario no tiene la posesión o el balón está en su tercio**. **La IA del portero puede rechazarla** si la salida no es razonable (la orden devuelve `Accepted` o `Refused(motivo)`; nunca se fuerza una salida absurda). C swipe ↑/↓ conserva las acciones especiales de ataque. C no tiene ninguna otra función defensiva principal.

**C11 ⚠ Sin datos de habilidades por jugador.** Sigue siendo así por decisión: fase posterior (catálogo + asignación validada por ti). Mientras tanto los tests 8, 9 y 14 usan datos de prueba, nunca jugadores reales.

**C12 ✅ Formaciones.**
Resolución (decisión 12): **no se diseñan formaciones definitivas en esta fase.** El motor es data-driven: lee `FormationDefinition` por id y nunca contiene una formación. Las formaciones y su comportamiento (`FormationBehaviorProfile`: con balón, sin balón, transición, responsabilidades) las creará Claude después, desde jugadores, roles, zonas y entrenadores. Las bases de `DefaultFormations` son esqueleto técnico de tests, no contenido de juego.

**C13 ✅ Coach Boost.**
Resolución (decisión 3): **no se modifica ningún atributo individual**; los 12 atributos no cambian y `BaseOverall` permanece intacto. `CompatibilityScore ≥ 80 → CoachBoost +1`; `< 80 → 0`; nunca negativo. `EffectiveOverall = BaseOverall + CoachBoost`. Por tanto **no existe capa `EffectiveAttributes`** (la propuesta anterior se retira). El boost es un valor de rating contextual; la simulación no lo convierte en atributos.
⚠ Queda abierto si el boost influye en la simulación por otra vía (p. ej. un parámetro de IA): hasta que se decida, **el motor no lo consume**; solo lo exponen tarjetas/HUD/rating de equipo.

**C14 ✅ Banquillo.**
Resolución (decisión 4): **6 titulares + 4 suplentes, con al menos 1 portero suplente.** Máximo **3 sustituciones por equipo y partido**; 4 suplentes **no** implican 4 cambios. Propuesta aditiva para implementación: `TeamDefinition.BenchIds` (hoy `TeamDefinition` solo tiene la alineación de 6). Validación: ≥1 portero entre los 4 suplentes.

**C15 ✅ Duración y formato.**
Resolución (decisión 6): **Arena: 2 tiempos de 6 min, sin descanso tradicional, cambio de lado al terminar el primero. Classic: 2 tiempos de 8 min, con descanso. Valores configurables** (`MatchModeDefinition`: periodos y segundos por periodo). El `MatchSettings` actual (180 s, un periodo) no cambia hasta la implementación; los valores 6/8 min son los que entrarán como datos de modo.

**C16 ✅ Expulsiones (Classic).**
Resolución (decisión 7): la roja expulsa al jugador, que **no puede volver**; el equipo sigue con uno menos mientras tenga **al menos 3 jugadores activos** en el campo (cuentan todos, sea cual sea su puesto). Portero expulsado: con portero suplente y **una sustitución disponible**, entra el suplente y **consume 1 cambio**; **sin cambios disponibles**, un jugador de campo ocupa la portería. **Nunca** hay sustitución fuera del límite de 3 (el expulsado tampoco se repone).
⚠ Abiertos: resultado si un equipo cae por debajo de 3 (L.2-1), cómo se rinde un jugador de campo en portería sin crear atributos (L.2-2), acumulación de amarillas (L.2-3).

**C17 ✅ Sustitución del portero.** Un cambio de portero cuenta como cualquier otro: consume 1 de los 3.

**C18 ⚠ Frecuencia de simulación (propuesta, sin objeción).** Física a paso fijo 60 Hz; decisión de IA a ~10–15 Hz con escalonado determinista por jugador; percepción ~5 Hz (`DecisionIntervalSeconds` de dificultad ya existe: 0,32 s). Se confirma al medir en móvil.

**C19 ✅ Estadísticas y eventos.** No existe nada; se crean nuevos, filtrados por modo.

### A.3 Sistemas que faltan por completo
Player AI, Team AI, transiciones, sistema de habilidades (solo contrato por ahora), resolución de duelos, detección de contacto/falta/fuera de juego, `ActionPadModel`, motor de partido con máquina de estados por modo, bus de eventos, estadísticas, sustituciones, tarjetas, reglas por modo, árbitro operativo, selector de jugador activo, fatiga continua, banquillo, formaciones con comportamiento (fase posterior), replay/log determinista.

### A.4 Ambigüedades de la spec original
Todas resueltas por la Fase 0 salvo las listadas en L.2.

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
// Valor legado, no oficial: PlayerAttributes.Reaction (interno, derivado, consumido por la IA)

// Conceptuales nuevos
SkillDefinition { SkillId, Kind, RequiredAttributes, Conditions, EnergyCost }   // contrato; sin catálogo
PlayerSkillSet  { PlayerId, SkillIds[] }                                          // contrato; sin asignaciones
MatchModeDefinition { ModeId, flags de reglas, Periods[ {Seconds} ], Boundaries(Walls|Lines), SideSwitch, RestKind, MaxSubstitutions=3 }
TeamDefinition (ampliación aditiva futura): StarterIds[6] (= PlayerIds actual) + BenchIds[4], ≥1 GK en BenchIds

IMatchSimulation.Step(MatchState, IntentFrame, dt)        // determinista, paso fijo
IntentFrame { PlayerIntent[] }                             // origen irrelevante
GoalkeeperOrderResult { Accepted | Refused(reason) }       // respuesta a C tap
RuleVerdict { Kind, Team, Position, Severity, Advantage }
IPlayerBrain.Decide(PerceptionSnapshot, TeamPlan, dt) -> PlayerIntent
ITeamBrain.Plan(PerceptionSnapshot, MatchContext) -> TeamPlan
IModeRules.Judge(MatchState, SimulationEvents) -> RuleVerdict[]
```

### B.4 Estado único

```
MatchState (autoritativo)
  Mode, Settings, Clock, Period, Phase, Score
  Ball (BallState de Core + Possession + LastTouch)
  Teams[2]: FormationId, CoachId, Squad(Starters, Bench), Subs { Used, Max=3 }, Cards, Sent-off, Stats, Referee-ref (solo Classic)
  Players[12 en pista]: id, RuntimeState(pos, vel, heading, Stamina01, acción, flags), Active/Expulsado
  PendingRestart, ActiveFoul/Advantage (Classic)
```
El `CoachBoost` no vive aquí como atributo: se consulta con `CoachBoostRules` cuando una vista de rating lo necesita.

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

**Arena:** `PreMatch → Kickoff → InPlay (tiempo 1, 6 min) → SideSwitch → Kickoff → InPlay (tiempo 2, 6 min) → FullTime`; `Goal → Kickoff` en ambos tiempos. `SideSwitch` es una transición breve (cambio de lado + recolocación), **sin descanso tradicional** ni interrupciones por reglas. Sin `DeadBall` por infracciones ni salidas; el balón rebota en las paredes.
**Classic:** `PreMatch → Kickoff → InPlay ⇄ DeadBall(Foul|Offside|OutOfBounds…) → Restart(ThrowIn|Corner|FreeKick|Penalty|GoalKick) → InPlay … → HalfTime (descanso) → Kickoff → … → FullTime` (2 × 8 min); `Goal → Kickoff`.
Duraciones, número de periodos, cambio de lado y tipo de descanso salen de `MatchModeDefinition` (configurable). El `MatchEngine` es el único que cambia de fase y rechaza fases/eventos que el modo no declara (eso hace verificables los tests de modo).

Sustituciones: contador único por equipo (máx. 3) en `MatchState`; solicitud → `SubstitutionRules.Validate` → `PlayerSubstituted`. Classic: con balón muerto o en el descanso. Arena: ⚠ momento permitido abierto (L.2-4).
Expulsión (Classic): jugador `Expulsado`, no vuelve; si quedan menos de 3 activos el partido no puede continuar (resultado: L.2-1). Portero expulsado: ver C16.

---

## G. REGLAS POR MODO (ARENA vs CLASSIC)

| Aspecto | ARENA | CLASSIC |
|---|---|---|
| Campo | **Cerrado, el balón rebota en paredes/límites** | Líneas con reanudación |
| Continuidad | Partido continuo | Se interrumpe por infracciones y salidas |
| Duración | **2 × 6 min (configurable)** | **2 × 8 min (configurable)** |
| Descanso | Sin descanso tradicional; **cambio de lado** al acabar el 1.º tiempo | Con descanso (HalfTime) |
| Faltas / tarjetas / penaltis / tiros libres | **No existen** | Sí |
| Fuera de juego | **No** | Sí (2.º último defensor; el portero cuenta como defensor) |
| Córner / saque de banda / saque de puerta | **No** | Sí |
| Contacto físico | Agresivo; afecta a balón/equilibrio/posesión; nunca infracción | Contacto legal vs infracción (`ContactJudge`) |
| Árbitro | No existe como sistema | Sí; perfil por parámetros |
| Ventaja | No | Sí |
| Lesiones | **No** | **No** |
| Expulsiones | No | Roja = fuera; juega con uno menos con ≥3 activos |
| Plantilla | 6 + 4 (≥1 portero suplente) | 6 + 4 (≥1 portero suplente) |
| Sustituciones | Máx. 3 por equipo | Máx. 3 por equipo |
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
| A | swipe ↓ | Tiro con efecto | Posesión; rango razonable | Shooting, Finishing, Technique, pie, presión, equilibrio |
| A | swipe ↑ | Vaselina | Espacio sobre el portero; ángulo | Finishing, Technique, Control, distancia |
| B | tap | Pase corto | Receptor válido | Passing, Technique, Control |
| B | swipe ↓ | Pedir pared | Compañero + línea + presión evaluados; devolución viable | Passing, Technique, Control, Creativity |
| B | swipe ↑ | Pase al hueco | Receptor con carrera + espacio | Passing, Technique, Creativity, RiskPreference |
| C | swipe ↑ | Regate / filigrana | **Solo si el jugador tiene la skill disponible** y hay espacio | Technique, Dribbling, Control, Agility, Stamina |
| C | swipe ↓ | Pase elevado con habilidad | **Solo con skill de pase especial** | Technique, Passing, Control, pie |
| C | tap | **Sacar al portero** (contextual; ver H.2) | Prioridad sin posesión o balón en tu tercio; el portero puede rechazar | Positioning, Command, Diving del portero; contexto |

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

- **6 titulares + 4 suplentes por equipo, con al menos 1 portero suplente.**
- **Máximo 3 sustituciones por equipo y partido** (un único contador, sin ventanas, sin cupo por tiempo, sin lesiones). Tener 4 suplentes **no** da 4 cambios; la 4.ª solicitud se rechaza, incluido el cambio de portero.
- Un jugador sustituido o expulsado no vuelve. El expulsado no se repone.
- Portero expulsado: ver C16 (consume 1 cambio si entra el suplente; sin cambios, un jugador de campo va a portería; nunca hay cambio extra).

---

## J. TEST PLAN

Tests puros (NUnit, `dotnet test`), sin nombres reales en identificadores:

| Área | Qué verifica |
|---|---|
| Modo | Arena no emite `FoulCommitted/OffsideDetected/CornerAwarded/ThrowInAwarded/PenaltyAwarded/CardIssued` ni saque de puerta; Classic sí puede |
| Arena | Campo cerrado: el balón rebota y nunca sale; solo cuenta gol por la boca; juego continuo |
| Formato | Arena 2×6 con cambio de lado y sin descanso; Classic 2×8 con descanso; valores configurables |
| Plantilla/Sustituciones | 6+4 con ≥1 GK suplente; 4.ª sustitución rechazada; 4 suplentes ≠ 4 cambios; contador por equipo; el cambio de portero cuenta |
| Expulsión | Roja: no vuelve; continúa con ≥3 activos; con <3 no continúa; GK expulsado: suplente consume 1 cambio, sin cambios → jugador de campo, nunca cambio extra |
| Input | A↓ efecto; A↑ vaselina; B↓ pared; B↑ pase al hueco; C tap → orden al portero; C↑/C↓ solo con skill disponible; sin skill → no se ejecuta; no existe sprint button; defensa: A tap/↓/↑ y B tap/↓/↑ según H.2 |
| Portero | C tap emite orden; la IA acepta si es razonable y rechaza si no; GRL = media exacta de 8 |
| Stamina | Baja stamina reduce sprint pico; 0 bloquea sprint; curva continua |
| Atributos | Los 12 no cambian; `Reaction` no aparece en tarjetas/Overall (guarda de legado) |
| Coach Boost | `BaseOverall` nunca cambia; ≥80 → +1; <80 → 0; nunca −1; ningún atributo se modifica |
| Balón | `BallSimulator` es la verdad; el Rigidbody visual no altera `BallState`; misma semilla → misma trayectoria |
| Pase/tiro/regate/duelo | Atributos importan; pie débil penaliza; mismo input → mismo resultado |
| IA | Decisión estable con semilla; no todos corren al balón; dificultad no cambia atributos |
| Árbitro | Veredicto por parámetros, no por nombres; Arena sin árbitro |
| Reglas Classic | Contacto legal vs infracción, ventaja, fuera de juego (GK cuenta), córner, banda, penalti, tarjetas |
| Transiciones | Disparadores, prioridades, recuperación de forma |
| MatchState | Única fuente de verdad; fases por modo; sin escritores externos |
| Skills | Solo contrato: sin comparación por nombre; sin catálogo ni asignaciones en datos |
| Formaciones | El motor no contiene formaciones; las toma de datos |
| Guardas | Sin comparaciones por nombre; Simulation no depende de Presentation/UI; Core sin `UnityEngine` |
| Rendimiento/Replay | Sin allocations en el paso caliente; entradas + semilla reproducen el mismo `MatchState` final |

---

## K. HOJA DE RUTA DE IMPLEMENTACIÓN

Cada fase: rama propia, tests verdes, sin push/PR salvo que lo pidas.

0. ✅ **Decisiones** (este documento).
1. **Núcleo determinista**: paso fijo, `MatchState` ampliado, `MatchModeDefinition`/`IModeRules` (Classic = lo actual), bus de eventos filtrado por modo, `BallSimulator` autoritativo y Rigidbody visual reflejando Core.
2. **Datos mínimos**: banquillo 6+4 (validación ≥1 GK suplente), periodos por modo (Arena 2×6, Classic 2×8), contratos `SkillDefinition`/`PlayerSkillSet` **sin catálogo ni asignaciones**, marca de legado de `Reaction`.
3. **Locomoción completa**: curva de fatiga, BodyType físico, selector de jugador.
4. **Acciones base**: control, pase, tiro (normal/efecto/vaselina), regate, protección; contactos y duelos.
5. **Input**: `ActionPadModel` (A/B/C + gestos, ataque y defensa de H) sobre `PlayerIntent`.
6. **Arena completo**: campo cerrado, 2×6, cambio de lado, sustituciones, estadísticas — modo más simple primero.
7. **Portero**: IA y orden C con aceptación/rechazo.
8. **IA de jugador y de equipo** + transiciones.
9. **Fase propia de formaciones**: `FormationBehaviorProfile` y formaciones generadas por Claude desde jugadores, roles, zonas y entrenadores.
10. **Fase propia de habilidades**: catálogo y asignación, validadas por ti.
11. **Classic**: `ContactJudge`, fuera de juego, reanudaciones, tarjetas/expulsiones, penaltis, árbitro por parámetros, ventaja, descanso.
12. **Estadísticas, replay/log determinista, perfilado móvil.**
13. **Presentación** enchufada a estado y eventos. Online queda fuera.

Dependencias duras: 1 → todo; 2 → 4/5/6; 4 → 6/11; 8 y 9 → pulir 6/11.

---

## L. DECISIONES

### L.1 Decididas en la Fase 0 (aplicadas arriba)

| # | Decisión | Dónde |
|---|---|---|
| 1 | Arena: campo cerrado con rebote; sin córners, bandas, saques de puerta, faltas ni fuera de juego; el juego continúa | C3, G, F |
| 2 | C tap = orden de salida del portero, contextual, prioridad sin posesión/tercio propio; la IA puede rechazar; C swipe ↑/↓ = especiales de ataque | C10, H |
| 3 | Coach Boost: no toca atributos; ≥80 → +1, <80 → 0; Effective = Base + Boost | C13 |
| 4 | Plantilla 6 + 4, ≥1 portero suplente, máx. 3 cambios; 4 suplentes ≠ 4 cambios | C14, I |
| 5 | Controles defensivos A/B definitivos; C solo "sacar portero" | H.2 |
| 6 | Arena 2×6 sin descanso y cambio de lado; Classic 2×8 con descanso; configurable | C15, F, G |
| 7 | Expulsiones Classic (≥3 activos; portero expulsado según cambios disponibles; nunca cambio extra) | C16, I |
| 8 | `BallSimulator` autoritativo; Rigidbody refleja Core | C1 |
| 9 | `MatchState` existente ampliado como estado autoritativo | C4, B.4 |
| 10 | `Reaction` legado/interno/derivado, mantenido; deuda documentada | C5, M |
| 11 | Skills: solo contrato `SkillDefinition`/`PlayerSkillSet`; sin catálogo ni asignaciones | C7 |
| 12 | Formaciones data-driven; ninguna definitiva ahora; ninguna en el motor | C12 |

### L.2 Siguen abiertas (necesitan decisión antes de la fase que las usa)
1. **Equipo con menos de 3 jugadores activos (Classic):** el partido termina; ¿qué resultado se registra (marcador actual, derrota por incomparecencia, 3-0)? Recomendación: terminar con victoria del rival conservando el marcador si ya es mayor. *(fase 11)*
2. **Jugador de campo en la portería:** el portero se evalúa con 8 capacidades propias; un jugador de campo no las tiene y **no se deben crear atributos nuevos**. Recomendación: perfil de portero de emergencia fijo y bajo, definido en datos, o derivado de los 12 atributos con regla documentada. *(fase 7)*
3. **Amarillas:** ¿2 amarillas = roja? Recomendación: sí, en Classic. *(fase 11)*
4. **Arena: momento permitido para sustituir** (pausa, tras gol, cambio de lado). Recomendación: tras gol, en el cambio de lado y mediante pausa. *(fase 6)*
5. **Coach Boost en simulación:** si debe influir en el partido por vía distinta a atributos. Hasta decidirlo, el motor no lo consume. *(antes de la fase 8)*
6. **Recuperación de stamina** en el descanso de Classic y en el cambio de lado de Arena (valor configurable; sin default decidido). *(fase 3)*
7. **Empate/prórroga:** hoy `IsDraw`; desempate = futuro.
8. **C8 y C9:** confirmar mover `FootballActionKind` a un archivo neutro de Core y añadir valores al final. *(fase 4/5)*

---

## M. DEUDA CONOCIDA

1. **`Reaction` (legado):** campo `PlayerAttributes.Reaction` consumido por `ReactionModel`, `AnticipationModel`, `AiErrorModel`. Marcarlo como legado/interno/derivado en código (aún no hecho: esta fase no toca código) y retirarlo o derivarlo en una fase posterior de IA.
2. **Dos físicas de balón:** hasta la fase 1, Gameplay usa `Rigidbody`; `Docs/ARCHITECTURE.md` conserva "solo el balón es dinámico" (obsoleto tras C1).
3. **`DefaultFormations` genéricas:** esqueleto de tests; sin comportamiento. Las formaciones reales y `FormationBehaviorProfile`: fase propia (9).
4. **Habilidades:** sin catálogo ni asignaciones; el contrato aún no existe en código.
5. **`TeamDefinition` sin banquillo** y `MatchSettings` de un solo periodo de 180 s: cambios aditivos pendientes de la fase 2.
6. **`MatchPhase`/`MatchState` mínimos** respecto a lo decidido: ampliación en fase 1.
7. **`FootballActionKind` dentro del Creator** y sin las acciones nuevas (L.2-8).
8. **Árbitros:** los 5 son datos que nadie lee; su uso llega en la fase 11.
9. **Coach Boost:** uso en simulación sin decidir (L.2-5).
