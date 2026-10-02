# FS27 — Arquitectura de gameplay V1 (6v6)

Estado: **diseño. No hay código nuevo.** Este documento audita la especificación del Superprompt Maestro contra lo que ya existe en el repositorio, resuelve los conflictos con una propuesta explícita y deja el contrato y la hoja de ruta para implementar.
Nada aquí cambia contratos existentes de jugadores, porteros, entrenadores o árbitros; cuando algo tendría que cambiar, está en la sección A como conflicto con decisión pendiente.

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

### A.2 Conflictos entre la especificación y el proyecto (no se resuelven en silencio)

Formato: **conflicto · sistema afectado · propuesta · impacto · decisión recomendada.**

**C1 — Dos físicas de balón.**
- Sistema: `Gameplay/BallPhysics.cs` + `BallController.cs` mueven el balón con `Rigidbody` de Unity (PhysX); `Core/Ball/BallSimulator.cs` es un simulador puro que Gameplay **no usa**. `docs/ARCHITECTURE.md` dice "solo el balón es dinámico".
- Spec: física determinista/reproducible, tests, futuro online (secciones 10, 41).
- Propuesta: el estado real del balón vive en `BallState` y lo avanza `BallSimulator` con paso fijo. El `Rigidbody` deja de ser la verdad: pasa a ser, como mucho, un seguidor visual (o desaparece). Colisión balón–jugador la resuelve Core (esfera/cápsula).
- Impacto: cambia `BallPhysics/BallController/BallInteractor`; las pruebas de Core ya cubren el simulador.
- **Decisión recomendada: sí, Core es la fuente de verdad.** Sin esto no hay determinismo.

**C2 — `MatchRules` genera córners, saques de banda y saques de puerta siempre.**
- Sistema: `Core/Match/MatchRules.cs`, `MatchTypes.cs` (`RestartKind`). No existe el concepto de modo.
- Spec: Arena nunca genera córner/saque de banda/fuera de juego/falta (secciones 2, 42).
- Propuesta: `MatchRules` pasa a ser una regla por modo (`IModeRules`, ver B.4). Arena no usa la regla de líneas.
- Impacto: refactor compatible (Classic = comportamiento actual).
- **Decisión recomendada: sí.**

**C3 — Ambigüedad: ¿qué pasa en Arena cuando el balón llega a una línea?** La spec dice qué NO existe, no qué existe.
- Opciones: (a) vallas/paredes perimetrales con rebote (estilo arcade, balón siempre vivo, mantiene "partido continuo"); (b) reposición inmediata sin interrupción visible (teletransporte); (c) balón libre infinito, imposible.
- Gol: solo cuenta dentro de la boca de la portería; fuera de ella (línea de fondo) también rebota.
- **Decisión recomendada: (a) paredes con rebote.** Requiere pared en `IBallCollider` (ya existe `PlaneCollider`). **Necesito tu confirmación** (ver sección L).

**C4 — `MatchPhase` y `MatchState`.**
- Sistema: `MatchPhase` (NotStarted, WaitingForKickoff, Playing, GoalCelebration, WaitingForRestart, Paused, Finished); `MatchState` es solo "vista de solo lectura" de reloj, marcador y último toque. `MatchSettings` = un periodo de 180 s.
- Spec: MatchState = fuente única de verdad con jugadores, balón, stamina, sustituciones, tarjetas, formación, entrenador, árbitro, modo; estados HalfTime, Restart, DeadBall, etc. (secciones 36–37).
- Propuesta: **ampliar `MatchState`, no crear un segundo estado** (evita doble fuente de verdad). `MatchPhase` gana valores y cada modo define cuáles son alcanzables (tabla en G).
- Impacto: añadir valores al final del enum (los actuales no se renumeran).
- **Decisión recomendada: sí.**

**C5 — Atributo legado `Reaction`.**
- Sistema: `PlayerAttributes.Reaction` (nº 13) lo usan `ReactionModel`, `AnticipationModel`, `AiErrorModel`. No forma parte de los 12 oficiales ni del Overall.
- Spec: "usar los 12 atributos; no crear nuevos".
- Propuesta: no se añade ningún atributo. Mantener `Reaction` como campo **derivado e interno** de la IA (calculado desde Agility/Acceleration/Technique o fijado por la dificultad), nunca en tarjetas ni en datos nuevos.
- Impacto: ninguno ahora; deuda de limpieza en la fase de IA.
- **Decisión recomendada: mantenerlo oculto como derivado; no retirarlo todavía.**

**C6 — La stamina actual no es la de la spec.**
- Sistema: `StaminaSystem` solo gasta al correr en sprint, recarga por segundos, bloqueo al llegar a 0 con umbral de recuperación.
- Spec: la stamina afecta velocidad, aceleración, recuperación, acciones físicas, precisión, presión y repetición de esfuerzos; curva continua (sección 8).
- Propuesta: **mantener** `Stamina01` como único valor; añadir una `FatigueCurve` (0..1 → multiplicadores de pico de velocidad, aceleración, precisión, intensidad de presión) y costes de energía por acción (entrada, regate, tiro). El bloqueo de sprint a 0 ya cumple "no puede sprintar hasta recuperar".
- Impacto: ampliación compatible con `PlayerLocomotion`.
- **Decisión recomendada: sí; los umbrales 70/40/20 de la spec son orientativos, la curva continua los reproduce.**

**C7 — Nombre `Capability` ya está ocupado.**
- Sistema: `GoalkeeperCapability` son los 8 atributos del portero.
- Spec: sección 45 propone `PlayerCapability`/`PlayerSkillSet`.
- Propuesta: usar **`SkillDefinition` + `PlayerSkillSet`** (habilidades técnicas) para evitar confundir con las capacidades del portero. No existe un sistema equivalente: `BehaviorId` (Creator) es identidad/animación, no regla.
- **Decisión recomendada: sí; `BehaviorId` queda para presentación y se enlaza a una habilidad por `SkillId`.**

**C8 — `FootballActionKind` vive dentro del Creator.**
- Sistema: `Core/Creator/CharacterRuntimeContracts.cs`. Tiene Shot/PlacedShot/ShortPass/LongPass/Cross/Tackle/Block/Intercept/Shield, pero **no** tiro con efecto, vaselina, pared, pase al hueco (hay `PassIntent.Through`), regate/skill, pase especial, entrada deslizante, orden al portero, presión/contención.
- Propuesta: dos capas, sin duplicar tipos: (1) `PlayerIntent` sigue siendo el único tipo de intención; (2) se añade `GameplayActionId` (datos) y el enum solo recibe valores nuevos **al final**. Mover el enum a un archivo neutro de Core (mismo namespace) para que Gameplay no dependa conceptualmente del Creator.
- **Decisión recomendada: sí.**

**C9 — Faltan los controles A/B/C.** Solo existe `VirtualStickModel`.
- Propuesta: `ActionPadModel` puro en `Core/Input` (botón + gesto + contexto → `ActionRequest`), análogo a `VirtualStickModel`. Ver tabla H.

**C10 — "Sacar al portero" con C está bajo "ataque" pero es defensiva.**
- La orden hace salir al portero (reacciona a un balón/rival cerca de tu área). Se lista en controles de ataque porque C es el botón de skills ofensivas.
- Propuesta: C pulsación **solo significa "portero, sal"** cuando el equipo **no tiene la posesión** o el balón está en tu tercio defensivo; con tu portero ya con balón no hace nada. En ataque, C+swipe son skills.
- **Decisión recomendada: sí; confírmalo (sección L).**

**C11 — Datos de habilidades inexistentes.** La spec exige que "no todos los jugadores tengan todas las skills" y depende de datos por jugador. Los 156 jugadores no traen habilidades.
- Propuesta: no inventar. Dos pasos: (1) catálogo de habilidades (tú lo apruebas); (2) asignación por jugador: reglas por umbral de Technique/Dribbling/Control **como candidatas** + lista cerrada por jugador que tú validas.
- **Decisión recomendada: fase de datos de habilidades antes del gameplay de skills.** Sin ella, C swipe UP/DOWN queda sin contenido (tests 8, 9, 14 solo con datos de prueba).

**C12 — Formaciones: faltan comportamientos y hoy hay formaciones "genéricas".**
- Sistema: `FormationDefinition` solo tiene slots (`PlayerRole` amplio + zona + posición relativa). `DefaultFormations` contiene bases genéricas; los entrenadores generan 53 variantes (2-1-2, 1-2-2, 2-2-1, 3-1-1).
- Spec: comportamiento con balón/sin balón/transición, responsabilidades, y "no formaciones genéricas".
- Propuesta: `FormationBehaviorProfile` **separado** (datos por formación y por slot) en lugar de engordar `FormationDefinition`. Lo creo yo a partir de zonas, roles y entrenadores en una **fase propia** (no en esta).
- **Decisión recomendada: sí; las bases de `DefaultFormations` se quedan como esqueleto técnico para tests, no como contenido de juego.**

**C13 — ¿Qué hace el motor con `EffectiveOverall`?**
- El Overall se calcula desde atributos; el boost +1 no es un atributo. La spec no dice cómo llega al partido.
- Opciones: (a) modificador de atributos efectivos calculado al inicio (+Δ repartido para que el Overall del calculador suba ≈ +1, tope 99, jamás se escribe en `PlayerAttributes`); (b) bonus de decisión/colocación; (c) solo etiqueta de tarjeta (no afecta).
- **Decisión recomendada: (a)** vía capa `EffectiveAttributes`, calculada una vez por partido (no por frame). Test 18/19 vigilan que `BaseOverall` no cambie. **Confirmar (sección L).**

**C14 — Plantilla y banquillo no definidos.**
- `TeamDefinition.PlayerIds` es la alineación (6). No hay banquillo, y 3 sustituciones exigen suplentes.
- Propuesta: `TeamDefinition` gana `BenchIds` (≥3, máx. por definir; mínimo recomendado 4 con un GK suplente). Sin banquillo, el sistema de cambios no es utilizable.
- **Decisión recomendada: añadir banquillo de datos (compatible).**

**C15 — Reglas de formato sin definir en la spec** (decisiones recomendadas, marcadas como supuestos):
- Duración: Arena y Classic con dos tiempos? Hoy 180 s en un periodo. **Propuesta:** `MatchSettings` por modo; Classic con descanso (HalfTime); Arena sin descanso.
- Empate: hoy `IsDraw`. **Propuesta:** empate permitido en liga; prórroga/penaltis de desempate = futuro.
- Expulsión (Classic): el equipo queda con un jugador menos, sin reposición; mínimo de jugadores para continuar = 3 (portero incluido). Supuesto a confirmar.
- Portero expulsado: un jugador de campo toma el puesto del portero o entra el GK suplente consumiendo una sustitución. Supuesto a confirmar.
- Acumulación de tarjetas: 2 amarillas = roja (solo Classic).
- Fuera de juego en 6v6: segundo último defensor, **el portero cuenta como defensor**, solo en campo rival.
- Tiros libres/penaltis: ejecución con el mismo sistema de tiro; barrera con número reducido (1–2) en 6v6.

**C16 — Sustitución del portero.** Spec: 3 cambios totales. No dice si el portero es sustituible. Supuesto: sí (GK por GK).

**C17 — Frecuencia de simulación.** Spec: "no ejecutar lógica innecesaria cada frame". **Propuesta:** paso fijo de física a 60 Hz, decisión de IA a ~10–15 Hz con escalonado por jugador (offset determinista), percepción a ~5 Hz. (`DecisionIntervalSeconds` de dificultad ya existe: 0,32 s.)

**C18 — Estadísticas y eventos.** No existe nada. Se crean nuevos (sin conflicto).

### A.3 Sistemas que faltan por completo
Player AI, Team AI, transiciones, sistema de habilidades, resolución de duelos, detección de contacto/falta/fuera de juego, `ActionPadModel`, motor de partido con máquina de estados por modo, bus de eventos, estadísticas, sustituciones, tarjetas, reglas por modo, árbitro operativo, selector de jugador activo, fatiga continua, `EffectiveAttributes`, banquillo, formaciones con comportamiento, replay/log determinista.

### A.4 Decisiones ambiguas de la spec (resueltas arriba)
Arena y las líneas (C3) · significado de C-tap (C10) · uso real del boost (C13) · banquillo (C14) · tiempos/empate/expulsión (C15) · sustitución del portero (C16) · estilo de entradas defensivas A/B (sección H).

---

## B. ARQUITECTURA FINAL

### B.1 Capas y dependencias (hacia abajo; Core no conoce Unity)

```
DATA            jugadores, porteros, entrenadores, árbitros, formaciones, habilidades, reglas por modo, dificultad
  ▲
RULES           IModeRules (Arena/Classic), contacto→infracción, fuera de juego, reanudaciones, tarjetas, sustituciones
  ▲
SIMULATION      MatchEngine, BallSimulator, movimiento/locomoción, duelos, acciones (pase, tiro, regate, entrada)
  ▲      ▲
AI              PlayerAI, TeamAI, GoalkeeperAI   INPUT  ActionPadModel + VirtualStickModel + selector de jugador
  ▲
PRESENTATION    animación, cámara, audio, efectos      UI   HUD, menús      NETWORK  (reservado, vacío)
```

Reglas de dependencia (verificables con tests de guarda, como los que ya existen):
- `Simulation` no conoce `Presentation`, `UI`, `Audio` ni `Input` concretos: recibe `PlayerIntent`.
- `AI` e `Input` producen **el mismo** `PlayerIntent` (la simulación no distingue origen).
- `Rules` no mueve a nadie: devuelve veredictos (`RuleVerdict`).
- `Presentation` solo lee `MatchState` y eventos; nunca escribe estado de juego.
- Cero comparaciones con nombres de jugador/entrenador/árbitro: solo ids, perfiles, habilidades y trazos.

### B.2 Módulos y responsabilidades

| Módulo (Core) | Responsabilidad | Reutiliza |
|---|---|---|
| `Match/MatchEngine` | Dueño del paso de simulación y de la máquina de estados | `MatchController` (evolución) |
| `Match/MatchState` | Única fuente de verdad (ampliada) | `MatchState`, `MatchScore`, `MatchClock` |
| `Match/Mode/IModeRules`, `ArenaRules`, `ClassicRules`, `MatchModeDefinition` | Qué existe en cada modo, en datos | `MatchRules` (se vuelve la parte de Classic) |
| `Match/Events/GameplayEvent` + `IEventSink` | Bus de eventos tipados por modo | nuevo |
| `Match/Stats/MatchStatistics` | Estadísticas aplicables por modo | nuevo |
| `Simulation/PlayerRuntimeState` (ampliado) | Estado dinámico por jugador (pos, vel, stamina, balón, acción en curso) | `PlayerRuntimeState` |
| `Simulation/FatigueCurve` | Stamina → multiplicadores | `StaminaSystem` |
| `Simulation/EffectiveAttributes` | Atributos + boost, una vez por partido | `CoachBoostRules` |
| `Simulation/Actions/*` (Pass, Shot, Dribble, Skill, Tackle, Wall, Through) | Ejecutan acciones con datos + atributos | `PlayerIntent`, `DribbleTouchModel` |
| `Simulation/DuelResolver` | Resolver duelos con atributos y timing | nuevo |
| `Simulation/ContactModel` | Contacto, equilibrio, magnitud, ángulo; el hecho físico, no la regla | nuevo |
| `Skills/SkillDefinition`, `PlayerSkillSet`, `SkillLibrary` | Habilidades como datos | nuevo (C7) |
| `Ai/PlayerAI`, `Ai/TeamAI`, `Ai/GoalkeeperAI` | Decidir intenciones | `PositioningModel`, `PressureModel`, `AnticipationModel`, `AiSkillResolver`, `TeamStyle`, `GoalkeeperSkillModel` |
| `Ai/TransitionModel` | Disparadores y prioridades de transición | nuevo |
| `Tactics/FormationBehaviorProfile` | Comportamiento por formación y slot | `FormationDefinition` (sin cambiarla) |
| `Rules/ContactJudge`, `OffsideJudge`, `RestartRules`, `DisciplineRules`, `SubstitutionRules`, `RefereeDecisionModel` | Veredictos de Classic | `RefereeDefinition` |
| `Input/ActionPadModel`, `PlayerSelector` | Gestos → `ActionRequest`; cambio de jugador | `VirtualStickModel` |

### B.3 Contratos principales (firmas conceptuales)

```
// Datos (ya existen o se amplían sin romper)
PlayerDefinition / PlayerPlayingProfile / PlayerAttributes(12)     // sin cambios
GoalkeeperProfile                                                  // sin cambios
CoachDefinition / CoachBoostRules                                  // sin cambios
RefereeDefinition                                                  // sin cambios
SkillDefinition { SkillId, Kind(Dribble|SpecialPass|Shot), RequiredAttributes, MinAttribute*, FootRule, RequiredSpace, EnergyCost, Tags }
PlayerSkillSet  { PlayerId, SkillIds[] }                           // por id, nunca por nombre
MatchModeDefinition { ModeId, Rules flags (fouls, offside, corners, throwIns, cards, referee), Periods, Boundaries(Walls|Lines), Subs=3 }
FormationBehaviorProfile { FormationId, Slots[ {SlotIndex, WithBall, WithoutBall, Transition} ] }

// Simulación
IMatchSimulation.Step(MatchState, IntentFrame, dt)     // determinista, paso fijo
IntentFrame { PlayerIntent[] por jugador }             // origen irrelevante
GameplayAction { ActionId, Actor, Params }              // resultado de decidir/ejecutar
RuleVerdict { Kind, Team, Position, Severity, Advantage }

// IA
IPlayerBrain.Decide(PerceptionSnapshot, TeamPlan, dt) -> PlayerIntent
ITeamBrain.Plan(PerceptionSnapshot, MatchContext) -> TeamPlan

// Reglas
IModeRules.Judge(MatchState, SimulationEvents) -> RuleVerdict[]
```

Ninguno de estos tipos referencia a `Presentation`/`UI`.

### B.4 Mapa de estado único

```
MatchState
  Mode, Settings, Clock, Phase, Score
  Ball (BallState + Possession + LastTouch)
  Teams[2]: Formation, Coach, Referee-ref (solo Classic), Subs { Used, Max=3 }, Cards, Stats
  Players[12]: Definition-id, RuntimeState(pos, vel, heading, Stamina01, action, flags), EffectiveAttributes
  PendingRestart, ActiveFoul/Advantage (Classic)
```

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

**Arena:** `PreMatch → Kickoff → InPlay → (Goal → Kickoff) … → FullTime`. Pausa disponible. Sin DeadBall por reglas, sin HalfTime (a confirmar).
**Classic:** `PreMatch → Kickoff → InPlay ⇄ DeadBall(Foul/Offside/OutOfBounds…) → Restart(ThrowIn|Corner|FreeKick|Penalty|GoalKick) → InPlay … → HalfTime → … → FullTime`; `Goal → Kickoff`.
El `MatchEngine` es el único que cambia de fase; cada modo declara en datos qué fases/eventos admite y el motor rechaza los no declarados (eso hace verificables los tests 2–6).

Sustituciones: contador único por equipo (máx. 3) en `MatchState`; solicitud → `SubstitutionRules.Validate` → `PlayerSubstituted`. Solo con balón muerto en Classic; en Arena en cualquier momento por pausa táctica (a confirmar).

---

## G. REGLAS POR MODO (ARENA vs CLASSIC)

| Aspecto | ARENA | CLASSIC |
|---|---|---|
| Continuidad | Partido continuo | Se interrumpe por infracciones y salidas |
| Faltas / tarjetas / penaltis / tiros libres | **No existen** | Sí |
| Fuera de juego | **No** | Sí (2.º último defensor, GK cuenta) |
| Córner / saque de banda / saque de puerta | **No** | Sí |
| Qué pasa en las líneas | Paredes con rebote (C3, a confirmar) | Línea → reanudación |
| Contacto físico | Agresivo, afecta balón/equilibrio/posesión; nunca infracción | Contacto legal vs infracción (ContactJudge) |
| Árbitro | No existe como sistema | Sí; perfil por parámetros |
| Ventaja | No | Sí |
| Lesiones | **No** | **No** (no es mecánica) |
| Sustituciones | 3 por equipo, tácticas | 3 por equipo |
| Descanso | No (a confirmar) | Sí |
| Eventos emitidos | Gol, pase, tiro, entrada, recuperación, posesión, sustitución | + falta, fuera de juego, córner, banda, penalti, tarjeta |
| Estadísticas | Sin faltas/tarjetas/córners/fueras de juego | Todas |
| Reanudaciones | Solo saque inicial tras gol | Todas |

Mecanismo: `MatchModeDefinition` (datos) + `IModeRules`. Un evento que el modo no declara **no puede emitirse** (guardado en el bus).

---

## H. CONTROLES DEFINITIVOS

Joystick izquierdo: dirección + intensidad (0–30 % trote, 30–70 % carrera, 70–100 % sprint progresivo). **No hay botón de sprint.**

### H.1 Ataque (equipo con balón o jugador activo en posesión)

| Botón | Gesto | Acción | Condiciones | Atributos |
|---|---|---|---|---|
| A | tap | Tiro normal / potente (según carga) | Balón controlado o disponible | Shooting, Finishing, Control |
| A | swipe ↓ | Tiro con efecto (curva) | Posesión; rango razonable | Shooting, Finishing, Technique, pie, presión, equilibrio |
| A | swipe ↑ | Vaselina | Portero adelantado o espacio sobre él; ángulo | Finishing, Technique, Control, distancia |
| B | tap | Pase corto (control de potencia) | Receptor válido | Passing, Technique, Control |
| B | swipe ↓ | Pedir pared | Compañero + línea + presión evaluados; devolución viable | Passing, Technique, Control, Creativity |
| B | swipe ↑ | Pase al hueco | Receptor con carrera + espacio | Passing, Technique, Creativity, RiskPreference |
| C | swipe ↑ | Regate / filigrana | **Solo si el jugador tiene la skill disponible** (datos) y espacio | Technique, Dribbling, Control, Agility, Stamina |
| C | swipe ↓ | Pase elevado con habilidad (trivela, etc.) | **Solo con skill de pase especial** | Technique, Passing, Control, pie |
| C | tap | Sacar al portero (orden defensiva, ver C10) | Sin posesión o balón en tu tercio; portero con espacio y riesgo asumible | GoalkeeperSkill (Positioning, Command, Diving), contexto |

### H.2 Defensa (sin balón) — propuesta, 2 botones (C reservado)

| Botón | Gesto | Acción | Condiciones | Atributos |
|---|---|---|---|---|
| A | mantener | Presionar/contener (jockey; velocidad y ángulo guiados) | Rival con balón en radio de presión | Defense, Agility, Acceleration, Stamina |
| A | tap | Entrada de pie / disputar | Rival a alcance | Defense, Strength, Agility, timing |
| A | swipe ↓ | Entrada deslizante | Mayor riesgo; abre contacto fuerte | Defense, Strength, Acceleration |
| B | tap | Interceptar / cubrir línea de pase | Línea de pase abierta | Defense, Agility, anticipación |
| B | swipe ↑ | Despejar / bloquear tiro | Balón cerca, tiro/centro en curso | Defense, Strength |
| B | swipe ↓ | Contacto físico (barrer con el cuerpo, disputa de hombro) | Duelo cuerpo a cuerpo | Strength, Defense, BodyType |
| C | tap | Sacar al portero | ver arriba | ver arriba |

Notas:
- El **mismo gesto** produce acciones distintas según el contexto (con/sin balón), por eso son pocos botones.
- En Arena todo contacto es legal; en Classic `ContactJudge` decide **legal vs infracción** (velocidad, ángulo, balón, timing, intensidad, zona, árbitro).
- La asignación defensiva (A mantener/tap/↓, B tap/↑/↓) es una **propuesta mía**: confirma o ajústala antes de la fase de input.
- No existe C+swipe defensivo.

---

## I. SUSTITUCIONES
**Máximo: 3 por equipo y partido.** Un único contador (`Subs.Used`, `Max = 3`) por equipo, sin ventanas, sin cupo por tiempo ni por lesión (no hay lesiones). La 4.ª solicitud se rechaza en `SubstitutionRules`, también cuando el cambio es del portero. Requiere banquillo (C14). Un expulsado no se repone.

---

## J. TEST PLAN

Nuevos tests puros (NUnit, en `Tests/EditMode`, ejecutables con `dotnet test`), sin nombres reales en identificadores:

| Área | Qué verifica |
|---|---|
| Modo | Arena no emite `FoulCommitted/OffsideDetected/CornerAwarded/ThrowInAwarded/PenaltyAwarded/CardIssued`; Classic sí puede |
| Sustituciones | 4.ª rechazada; contador por equipo; GK; expulsado no se repone |
| Input | A↓ → tiro con efecto; A↑ → vaselina; B↓ → pared; B↑ → pase al hueco; C tap → orden portero; C↑/C↓ solo con skill disponible; sin skill → no se ejecuta; no existe sprint button (guarda estructural) |
| Stamina | Stamina baja reduce sprint pico; 0 bloquea sprint hasta recuperar; continua, sin saltos |
| Atributos | Reutilizan los 12; no se añade ningún atributo oficial |
| Balón | Determinismo (misma semilla → misma trayectoria); estados; rebote; spin/curva |
| Pase/tiro/regate/duelo | Atributos importan; pie débil penaliza; sin aleatoriedad pura; mismo input → mismo resultado |
| IA | Decisión estable con semilla; no todos corren al balón; respeta rol/zona; dificultad no cambia atributos |
| Portero | GRL = media exacta de 8 (ya cubierto, mantener); salida con C |
| Árbitro | Veredicto depende de parámetros, no de nombres; Arena sin árbitro |
| Reglas | Falta (contacto legal vs infracción), ventaja, fuera de juego (GK cuenta), córner, banda, penalti, tarjetas (2 amarillas = roja) |
| Transiciones | Disparadores, prioridades, recuperación de forma |
| MatchState | Única fuente de verdad; fases por modo; sin escritores externos |
| Entrenador | `BaseOverall` nunca cambia; `EffectiveOverall` hasta +1; nunca −1 |
| Guardas | Sin comparaciones por nombre (jugador/entrenador/árbitro); Simulation no depende de Presentation/UI; Core sin `UnityEngine` |
| Rendimiento | Presupuesto de pasos (nº de decisiones por segundo) y cero allocations en el paso caliente (test de conteo) |
| Replay | Entradas grabadas + semilla reproducen el mismo `MatchState` final |

---

## K. HOJA DE RUTA DE IMPLEMENTACIÓN (orden recomendado)

Cada fase: rama propia, tests verdes, sin push/PR salvo que lo pidas.

0. **Decisiones** (sección L): confirmar C3, C10, C13, C14, defensa A/B, tiempos/expulsión.
1. **Núcleo determinista**: paso fijo, `MatchState` ampliado, `MatchModeDefinition`/`IModeRules` (Classic = lo actual), bus de eventos con filtro por modo; balón en Core como verdad (C1).
2. **Datos que faltan**: banquillo, catálogo de habilidades + asignación (tú validas), `EffectiveAttributes`.
3. **Locomoción completa**: curva de fatiga, BodyType físico, selector de jugador.
4. **Acciones base**: control, pase, tiro (normal/efecto/vaselina), regate, protección; contactos y duelos.
5. **Input**: `ActionPadModel` (A/B/C + gestos) sobre `PlayerIntent`.
6. **Arena completo** (paredes, continuidad, sustituciones tácticas, estadísticas) — el modo más simple primero para validar el bucle.
7. **Portero**: IA y orden C.
8. **IA de jugador y de equipo** + transiciones + formaciones con comportamiento (`FormationBehaviorProfile`, generadas por mí desde roles/zonas/entrenadores).
9. **Classic**: `ContactJudge`, fuera de juego, reanudaciones, tarjetas, penaltis, árbitro por parámetros, ventaja, descanso.
10. **Estadísticas, replay/log determinista, perfilado móvil.**
11. **Presentación** (animación/cámara/UI) enchufada a eventos y estado. Online queda fuera.

Dependencias duras: 1 → todo; 2 → 4/5; 4 → 6/9; 8 → pulir 6/9.

---

## L. PREGUNTAS PENDIENTES (necesito tu decisión; cada una tiene recomendación)
1. **Arena en las líneas:** paredes con rebote (recomendado) o reposición inmediata.
2. **C tap:** orden "portero, sal" solo sin posesión/en tu tercio (recomendado).
3. **Coach Boost en partido:** atributos efectivos +Δ (≈ +1 Overall), calculado una vez por partido (recomendado).
4. **Banquillo:** ≥4 suplentes con GK suplente (recomendado).
5. **Defensa A/B:** ¿aceptas el reparto de H.2?
6. **Formato:** Classic con 2 tiempos y descanso, Arena sin descanso; ¿duración de cada uno?
7. **Expulsión:** equipo con uno menos y mínimo 3 jugadores; ¿portero expulsado → entra GK suplente consumiendo cambio?

## M. Deuda conocida
- Atributo legado `Reaction` (C5).
- Bases de `DefaultFormations` genéricas (C12).
- Datos de habilidades por jugador inexistentes (C11).
- `Docs/ARCHITECTURE.md` conserva "solo el balón es dinámico" (Rigidbody); queda obsoleto al resolver C1.
