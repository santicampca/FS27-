# FS27 — Infraestructura para IA (Fase 7)

Estado: **implementado en el Core, probado, sin Unity y SIN inteligencia.** Rama `phase-7-ai-infrastructure`, desde `67ad3d0` (2045/2045).
Esta fase no decide nada. Prepara el terreno para que una IA futura pueda **observar** el partido sin poder modificarlo, **devolver intenciones** y que esas intenciones lleguen al **mismo Core** que usa una persona:

```
MatchState ──► WorldState (copia derivada) ──► WorldView (solo lectura) ──► [IA futura: Decision]
                                                                                  │
                                                  MovementIntent / ActionIntent ◄─┘
                                                          │
                       LocomotionInput ◄──────────────────┤                    ┌── Persona: Touch → NormalizedInput
                       ActionRequest ──► MatchController.Perform ──► ActionResolver ──► BallSimulator ──► ActionResult
```

**(DECISIÓN FASE 7)** = valor o criterio mío, no especificado; los números están en `InfrastructureDefaults` / `InfrastructureTuning`.

---

## 0. Auditoría: qué se extendió y qué se creó

| Contrato existente | Qué hice |
|---|---|
| `MatchState`, `MatchBall`, `MatchPlayer`, `TeamMatchState` | **Extendidos**: `MatchPlayer.Role`, `StartedMatch`; `MatchBall.IntendedReceiverId`; `TeamMatchState.GoalkeeperSlot`. No se creó ningún segundo estado de partido. |
| `FormationDefinition`, `FormationPosition.ToFieldPosition` | **Reutilizados tal cual** (ya orientan y reflejan por equipo). |
| `RestartRequest`, `RestartKind`, `ConfirmRestartReady` | **Extendidos**: `TakerId`, `Status`, `RestartKind.FreeKick/Penalty` (contrato). `ConfirmRestartReady` queda intacto. |
| `ActionRequest`, `ActionResult`, `ActionMapper`, `ActionInput` | **Extendidos**: `Target`, `Source`; `TargetKind`, `TargetPlayerId`, `Source`. No existe un segundo request. |
| `LocomotionStepper`, `SetPlayerInput`, `PlacePlayer` | **Reutilizados** (`MovementIntent` termina en `SetPlayerInput`; `PlacePlayer` gana una sobrecarga). |
| `SkillResolver`, `ResolvedSkills`, `BallPredictor` | **Reutilizados** (el `WorldView` los expone, no los copia). |
| `Core/Ai/*` (`PositioningModel`, `PressureModel`, `AnticipationModel`, `TeamStyle`, `PlayingProfile`…) | **Conservados sin tocar.** Son modelos de apoyo sin consumidores de partido; la Fase 8+ los usará. No existe ni Perception, ni AttackIntent, ni Defense/Transition/GK. |
| `PlayerIntent` / `IIntentSource` | Conservados (consumidos por `PlayerIntentProvider` y la locomoción antigua de Unity). Ver §Legado. |
| **Nuevo** | `Core/World/*` (`WorldState`, `WorldView`, `PlayerObservation`, `WorldGeometry`), `Core/Intents/*` (`PassTarget`, `ControlSource`, `MovementIntent`, `LocomotionInput`, `ActionIntent`, `NormalizedInput`), `Core/Match/{PitchBounds, PlayerSeparation, FormationPlacement, GoalkeeperSlot, InfrastructureDefaults, MatchController.Infrastructure}`. |

---

## 1. WorldState
`WorldState.Capture(controller)` / `Refresh(controller)`. Reducción de `MatchState` a lo que necesita el gameplay:
- **`WorldMatch`**: modo, tipo de competición, fase, periodo, tiempo total y del periodo, marcador, dirección de ataque, balón vivo, posesión, reinicio pendiente (tipo, equipo, ejecutor, estado, posición).
- **`WorldBall`**: posición, velocidad, posición en el plano, velocidad horizontal, **dirección** (cero si está casi parado), altura, modo, si está en el suelo, poseedor, último toque, receptor previsto.
- **`WorldPlayer`** (los 20 jugadores de las dos plantillas, con su disponibilidad): id, equipo, posición en la plantilla, posición, velocidad, orientación, rumbo, velocidad, velocidad máxima, stamina, rol, **zona actual** (de `PlayingProfileDefaults.ZoneOfRelativePosition`), disponibilidad (`OnPitch/Bench/SubstitutedOff/Dismissed`, espejo exacto de `MatchPlayerStatus`), portero (también suplente o de emergencia), posesión, **controlado por una persona**, estado de acción (bloqueado, cooldown, deslizándose, presionando, protegiendo, esquivando), pie preferido, `ActionProfile` (cualidades derivadas de los atributos al crear el partido) y `ResolvedSkills`.
- También: `GoalkeeperSlot` de cada equipo y el jugador controlado de cada lado.

**No contiene `PlayerDefinition` ni `PlayerAttributes`** (test por reflexión). "TeamId": en un partido el equipo es `TeamSide`; el id del equipo del juego vive en la base de datos, no en el partido.

**No mutabilidad / garbage:** es una *copia derivada*; su API pública es de solo lectura. `Refresh` rellena el mismo objeto (arrays creados una vez) y `Clone()` guarda una copia congelada. Capturar es una lectura pura (test: el estado del partido es idéntico antes y después).

## 2. WorldView
`readonly struct WorldView` sobre un `WorldState`: no guarda `MatchState`, `MatchController`, `MatchPlayer`, `MatchBall` ni `PlayerDefinition` (test). Ofrece: partido, balón, jugadores por índice, ejecutor, portero de cada lado, `TryGetBallCarrier`, `OnPitchCount`, **`PredictBallPosition(seconds)`** (modelo de referencia del balón sobre una copia; no simula paredes), `SkillsOf`, `CanUse(playerId, action)` (= `SkillResolver.CanUseAction`, no una copia de las reglas) y `Observe(...)`. Sin Unity, sin `MonoBehaviour`, determinista.

## 3. PlayerObservation
Objeto **reutilizable** (`view.Observe(id, obs)` no reserva memoria): el propio jugador, distancia/ángulo al balón, quién tiene el balón (propio / rival / libre), hasta 6 compañeros y 6 rivales **en pista** ordenados por distancia (empate: el de la plantilla antes), espacio libre básico (rival más cercano, rivales en `OpenSpaceRadius`), distancia a las dos porterías, fase, zona, `IsControlled`, `CanAct`. **Solo observación**: ni decisiones, ni puntuación, ni marcaje, ni apoyo.

## 4. Distancias y relaciones (`WorldGeometry`)
Distancia, distancia al balón, ángulo relativo (+ derecha, − izquierda), ángulo entre direcciones, distancia punto-segmento, porterías según el sentido de ataque (cambia al cambiar de lado), rival/compañero más cercano, carril de pase libre (`PassLaneClearance`), **compañero disponible** (en pista, dentro de `MaxPassDistance`, carril libre; el portero cuenta: un pase atrás es un pase), rivales dentro de un radio. Todo en un único sitio; un test impide repetir esas fórmulas en `World/` e `Intents/`.

## 5. Formación → posiciones iniciales
`FormationPlacement.Compute` + `MatchController.PlaceFormation(side)`:
1. lee la `FormationDefinition` del equipo (`SetFormation`, con la formación real de `GameDatabase.TryGetFormation`);
2. el portero **en portería** ocupa el hueco de portero y los 5 jugadores de campo los huecos de campo **por orden de plantilla** (la IA decidirá después quién juega dónde);
3. convierte con `FormationPosition.ToFieldPosition` (un equipo que ataca −X es el mismo dibujo girado 180°: dos equipos con la misma formación son **espejo exacto**, test sobre las 12 formaciones reales);
4. portero dentro de la zona de portería (`GoalZoneDepth`, `GoalZoneHalfWidth`); todos dentro del campo;
5. en un saque de centro, todos en su mitad (`KickoffHalfMargin`);
6. separa al equipo (empuja y vuelve a colocar hasta asentarse). Los dos equipos juntos se separan en `PrepareRestart`.
Orientación = hacia la portería que ataca *ahora* (por eso es correcta tras el cambio de campo). **(DECISIÓN FASE 7)** El reparto de jugadores a huecos por orden de plantilla.
Las 12 formaciones reales (una por equipo): 6 jugadores por equipo, 12 en total, sin solapamiento, dentro del campo, portero en su zona, en su mitad.

Nota de datos: las plantillas reales no se leen de `TeamDefinition.PlayerIds` (está vacío); salen de `RosterGenerator` / `TeamRoster.TryToMatchSquad`.

## 6. Colocar jugadores (`PlacePlayer`)
Sobrecarga `PlacePlayer(in PlayerPlacement)`: posición, orientación, stamina inicial opcional (limitada al depósito) y `TakesRestart` (lo hace ejecutor del reinicio pendiente de su equipo). La posesión no se regala al colocar: la da `ConfirmRestart` al ejecutor. Funciona en saque de centro, segunda parte, cambio de campo, prórroga y reinicios (tests).

## 7. Reinicios
`RestartRequest { Kind, Team, Position, TakerId, Status }`, `RestartKind` (`KickOff, ThrowIn, GoalKick, Corner` + **`FreeKick`, `Penalty`** añadidos como contrato: las reglas que los conceden son de una fase posterior) y `RestartStatus { Pending, Placed, Confirmed }`.
- `AssignRestartTaker(id)`: ejecutor **explícito** (solo un jugador en pista del equipo que saca).
- `DefaultTaker(request)`: si nadie lo asignó — portero en un saque de puerta; si no, el **jugador de campo más cercano al punto** (empate: el de la plantilla antes).
- `PrepareRestart()`: en un saque de centro coloca las dos formaciones; coloca al ejecutor junto al balón (`TakerOffset`); aleja a los demás del balón (`RestartClearRadius`) y los separa.
- `ConfirmRestart()`: prepara si hace falta, reanuda el juego (`ConfirmRestartReady`) y **da el balón al ejecutor**. No reanuda mientras un equipo necesite portero.
- Los reinicios existentes (saque de banda, de puerta, córner) funcionan por el mismo camino (tests). `ConfirmRestartReady` queda como estaba (no regala el balón a nadie).

## 8. Saque de centro
Equipo = `settings.KickoffTeam` (el contrario en la 2.ª parte y la 2.ª parte de la prórroga); ejecutor explícito o por defecto; balón en el centro; ejecutor mirando a la portería que ataca; resto de jugadores en su mitad y fuera del círculo; **ningún jugador recibe el balón por ser el primero de la lista** (test sobre los 12 equipos reales: el portero que abre la lista nunca saca) y nadie se lo quita por proximidad en los primeros instantes.

## 9. Límites del campo (`PitchBounds`)
Geometría pura del Core: largo, ancho, `Contains`, `ClampPlayer` (campo + `PlayerBoundsMargin`, para que un saque de banda se tome en la línea), zona de portería de cada equipo (sigue al cambio de campo), mitad propia, centro de las dos porterías. `BallLimit`: **Arena = `Walls`** (el balón rebota; lo resuelve `ArenaWalls` del `BallSimulator`) y **Classic = `LeavesPlay`** (sale y hay reinicio; lo resuelve `MatchRules`). Se pregunta a las reglas del modo (`rules.UsesWalls`), no al modo. La clase `FieldBounds` de `Gameplay/` es presentación Unity y no se toca.

## 10. Separación entre jugadores (`PlayerSeparation`)
Distancia mínima configurable (`MinSeparation`), empuja a los dos por igual en la línea que los une; dos puntos idénticos se separan por un eje fijo según sus índices (determinista; `axisSign` conserva el espejo). Sin física, sin velocidades. En el motor (`StepPlayerSpace`) también mantiene a los jugadores dentro del campo + margen. **(DECISIÓN FASE 7)** *Está apagada por defecto* (`InfrastructureTuning.EnforcePlayerSpace = false`) y se enciende sola con `PlaceFormation`/`PrepareRestart` o a mano: los tests y puentes anteriores colocan a todos los jugadores en el origen y esperan el comportamiento de las Fases 3–4. Sin garbage por paso (test).

## 11. Receptor de pase (`PassTarget`)
`None | Player | Position | Unknown`. `ActionRequest.Target` lo transporta; al patear, un objetivo **jugador** gira el disparo hacia donde está ese compañero ahora; un objetivo **posición**, hacia ese punto. Un jugador que no se puede resolver (desconocido, rival, banquillo, él mismo) deja la dirección pedida y se informa como `Unknown`. `ActionResult.TargetKind/TargetPlayerId`; `MatchBall.IntendedReceiverId` mientras el pase va en camino (cualquier toque lo borra). Solo lo usan las acciones de patada. No hay IA que elija al receptor.

## 12. MovementIntent
`Direction`, `Magnitude`, `HasTarget/Target`, `Priority`, `Source`. `ToLocomotion()` → `LocomotionInput` (el stick: dirección × magnitud, nunca más de 1; NaN → quieto). `TowardsTarget(from, target, magnitude, arriveRadius)` frena al llegar (sin pasarse). `MatchController.ApplyMovementIntent` lo convierte en la entrada de la locomoción de la Fase 3 (idéntico, bit a bit, a `SetPlayerInput`). Si dos fuentes mueven al mismo jugador en el mismo paso gana la mayor prioridad (empate: la última); cada paso empieza limpio.

## 13. ActionIntent
`Team, PlayerId, Kind, Direction, Power, Curve, Target, SkillId, Long, Source`. `ToRequest()` produce **el** `ActionRequest` (potencia 0 = la de defecto; nunca lleva el permiso heredado `SkillGranted`) y `MatchController.PerformIntent` lo manda a `Perform`. Con skills se resuelve igual que cualquier acción (sin skill, `SkillNotGranted`: la intención no se salta nada).

## 14. Selección de jugador
`SetControl(side, Human|Ai)`, `SelectPlayer`, `ControlledPlayerId` (con **respaldo**: si el seleccionado ya no está en pista —expulsado, sustituido— el portador del balón del equipo; si no, el primer jugador de campo en pista; si no, el portero), `CycleSelection(±1)` (salta a quien no puede jugar), `ClearSelection`. El respaldo se guarda. **(DECISIÓN FASE 7)** Es una regla mínima y determinista; la selección automática inteligente es de una fase posterior. `WorldPlayer.IsControlled` solo es verdadero en un lado humano.

## 15. GoalkeeperSlot
Derivado de la plantilla (no se guarda dos veces): `StarterId`, `BenchKeeperId`, `OccupantId`, `Mode { Vacant, Starter, Substitute, Emergency }`, `NeedsGoalkeeper`. Sigue las reglas ya existentes: sustitución de portero, reemplazo del expulsado, portero de emergencia y expulsión (tests). Sin IA de portero.

## 16. Normalización de entrada (`NormalizedInput`)
Stick + botón (A/B/C) + gesto (toque / deslizar arriba / abajo) + dirección + potencia + efecto. `Sanitized()` (NaN, sticks largos, botones inválidos), `ToActionInput()`, `ToLocomotion()`. **Unity:** `Touch → NormalizedInput`. **Core:** `NormalizedInput → ActionInput → ActionMapper → ActionRequest`. El Core no sabe de píxeles, dedos ni Unity (test sobre los archivos).

## 17. Humano vs IA
- **Humano:** `SubmitHumanInput(side, NormalizedInput)` → el jugador controlado: stick → `ApplyMovementIntent`; botón → `PerformInput` → mapper → `ActionRequest` → `Perform`.
- **IA (futura):** `Decision` → `MovementIntent` / `ActionIntent` → `ApplyMovementIntent` / `PerformIntent` → `ActionRequest` → `Perform`.
- Las tres entradas que devuelven un `ActionResult` (`Perform`, `PerformInput`, `PerformIntent`) terminan en el mismo `Perform` (test por reflexión y por texto: nadie más llama al resolutor). Una acción equivalente de una persona y de la IA da el **mismo** resultado; solo difiere `Source`.

## 18. Skills
`WorldPlayer.Skills` (`ResolvedSkills`: activas, ignoradas con motivo), `WorldView.SkillsOf` y `CanUse`. No se duplica `SkillResolver`: el permiso es el del motor.

## 19. Determinismo
Sin tiempo real, sin `UnityEngine.Random`, sin `System.Random`, sin estado global (test de guarda sobre los archivos nuevos). Todo orden de desempate es el de la plantilla. Dos partidos de 20 s con 12 jugadores y el mismo semilla acaban en el mismo estado (Arena y Classic).

## 20. Rendimiento
- `WorldState.Refresh` + 20 observaciones + consultas, ×500: **0 bytes** (test, tolerancia 4 KB).
- `StepPlayerSpace`: sin garbage por paso (test).
- `WorldPlayer` es un struct de unos 200–250 bytes (incluye `ActionProfile`); el indexador devuelve copia. **Punto a optimizar más adelante** si hiciera falta: indexador `ref readonly` o separar el perfil. No se ha optimizado porque no cuesta garbage.
- `PlaceFormation`/`PrepareRestart` reservan listas pequeñas: solo ocurren en reinicios, no por paso.

## 21. Legado (solo lo afectado)
| Elemento | Estado |
|---|---|
| `PlayerIntent`, `IIntentSource`, `PlayerIntentProvider` (Unity) | **Mantenidos**: los consume la locomoción/animación antigua de Unity. El camino nuevo es `NormalizedInput → ActionInput → ActionRequest` y `MovementIntent`. Sustituir esos consumidores es trabajo de Unity. |
| `ActionInput` | **Mantenido** como entrada del `ActionMapper`; `NormalizedInput` lo produce. |
| `MatchController.Tick(dt, BallObservation)` | **Mantenido** (hosts que dan su propio balón). `Advance` es el paso autoritativo. |
| `MatchController.ConfirmRestartReady` | **Mantenido** (el puente de Unity y la mayoría de los tests anteriores lo usan); no da el balón a nadie. Un host nuevo debería usar `ConfirmRestart`. |
| `MatchBridge` (Unity) | Sin tocar: con jugadores sigue esperando que el host los coloque y llame a `ConfirmRestartReady`. Migrarlo a `PlaceFormation`/`ConfirmRestart` es trabajo de Unity. |
| `Gameplay/FieldBounds` (Unity) | Presentación; la geometría es ahora `PitchBounds` (Core). |
| `PlayerLocomotion`, `StaminaSystem`, `DribbleTouchModel`, `BallInteractor` | Sustituidos funcionalmente por la locomoción de la Fase 3 y las acciones de la Fase 4, pero aún tienen consumidores (o están aislados): **no se borra nada**. |
| `MatchPlayer.Runtime` (stamina duplicada) | Sin cambios; las acciones y la IA usan la stamina de locomoción. |

## 22. Límites de esta fase
No hay: DecisionEngine, UtilityAI, BehaviorTree, GOAP, FSM táctica, scoring de decisiones, personalidad de IA, estilos de equipo, ataque, defensa, marcaje, cobertura, desmarques, presión inteligente, transiciones, IA de portero, tácticas, Coach AI, dificultad de gameplay, Classic completo, árbitro, competición, estadísticas, Unity gameplay, HUD, cámara, VFX ni audio. El selector de jugador y el ejecutor por defecto son reglas mínimas, no inteligencia. Los reinicios `FreeKick` y `Penalty` son solo contrato.

## 23. Qué queda para la Fase 8
- El **motor de decisión** mínimo (`WorldView → Decision → intents`) para un jugador con balón y uno sin balón, usando `PlayerObservation` y `WorldGeometry`.
- Asignación de jugadores a huecos de formación según su rol (hoy: por orden de plantilla).
- Selección automática de jugador (el más cercano al balón…).
- Pasar el jugador **controlado** a la IA cuando el humano no lo es, y un `ActionIntent` por tick de IA.
- Reglas que conceden `FreeKick`/`Penalty` (con el árbitro y las faltas de Classic) y su ejecución.
- Unity: `Touch → NormalizedInput`, `MatchBridge` con `PlaceFormation`/`ConfirmRestart`, HUD del botón C.
- Balance de `InfrastructureDefaults` (separación, radios de despeje, margen de campo).

## 24. Fase 8 — lo que el motor de decisión añadió a este contrato
Ver [BASIC_DECISION_ENGINE.md](BASIC_DECISION_ENGINE.md). Ampliaciones de esta infraestructura (todas explícitas, de solo lectura para la IA): `WorldPlayer.Home/HasHome` (posición de su formación en juego abierto; caché derivada `MatchController.RefreshHomePositions`, solo se recalcula si cambian la formación, el sentido de ataque o quién está en pista), `WorldGeometry.IsLaneClear(..., ignoreGoalkeepers)` y `WorldGeometry.NearestOpponentDistanceTo`. La sección "Qué queda para la Fase 8" de arriba se cumple solo en parte: existe un primer motor de decisión (con balón / sin balón, receptor, tiro, regate, protección, portero mínimo y selección automática mínima); siguen pendientes la asignación de huecos por rol, las decisiones defensivas reales y los reinicios decididos por la IA.

