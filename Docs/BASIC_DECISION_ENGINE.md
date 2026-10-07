# FS27 — Motor de decisión básico (Fase 8)

Estado: **primer nivel funcional de decisión, en el Core, determinista y sin Unity.** Rama `phase-8-basic-decision-engine`, desde `932c897` (2131/2131).
No es "la IA": es la cadena mínima **observar → interpretar → decidir → intención → Core**, probada de punta a punta. Todo lo táctico queda para fases siguientes.

**(DECISIÓN FASE 8)** = valor o criterio mío, no especificado; los números están en `BasicDecisionDefaults` / `BasicDecisionTuning`, a balancear más adelante.

## 1. Arquitectura

```
MatchState ─► WorldState ─► WorldView ─► BasicDecisionEngine.Decide(view, playerId) ─► PlayerDecision
                                                                                          │
                                       MovementIntent ─► MatchController.ApplyMovementIntent ─► LocomotionInput ─► LocomotionStepper
                                       ActionIntent   ─► MatchController.PerformIntent ─► ActionRequest ─► Perform ─► resolutor ─► BallSimulator
```

| Pieza | Archivo | Qué es |
|---|---|---|
| `PlayerDecision`, `DecisionKind`, `DecisionReason` | `Core/Decision/PlayerDecision.cs` | La respuesta: jugador, tipo, `MovementIntent` y/o `ActionIntent`, receptor, motivo (solo depuración; nada ramifica por él, un test lo comprueba). |
| `BasicDecisionEngine` | `Core/Decision/BasicDecisionEngine.cs` | Función pura `(WorldView, playerId) → PlayerDecision`. No lee definiciones, atributos, `MatchState` ni balón; no escribe nada; sin azar ni tiempo. |
| `BasicDecisionDriver` | `Core/Decision/BasicDecisionDriver.cs` | Decide cada `DecisionIntervalSeconds` (escalonado por jugador) y aplica **solo** por `ApplyMovementIntent` y `PerformIntent`. Cuenta pasos, no tiempo. No maneja a jugadores controlados por una persona. |
| `AutoSelection` | `Core/Decision/AutoSelection.cs` | Elección mínima del jugador controlado. |
| `BasicDecisionDefaults` / `BasicDecisionTuning` | `Core/Decision/BasicDecisionDefaults.cs` | Todos los números; un test exige gemelos 1:1 y prohíbe números sueltos en el motor. |

Ampliaciones **explícitas** del contrato de la Fase 7 (el motor no accede a nada más): `WorldPlayer.Home/HasHome` (la posición de su formación en juego abierto, calculada por `MatchController.RefreshHomePositions` y cacheada), `WorldGeometry.IsLaneClear(..., ignoreGoalkeepers)` y `WorldGeometry.NearestOpponentDistanceTo`.

## 2. Qué decide (la jerarquía)

`DecisionKind`: `Idle, MoveToPoint, MoveToBall, Support, Pass, Shoot, Dribble, Protect` (8; lista cerrada).

### Sin balón (jugador de campo)
| Situación | Decisión |
|---|---|
| Balón libre | El jugador de campo **más cercano** de su equipo → `MoveToBall`. Los demás → `MoveToPoint` hacia su *home* atraído hacia el balón (`PositionBallPull`). El portero nunca cuenta. |
| Un compañero tiene el balón | Los `SupportCount` (2) compañeros más cercanos al portador → `Support`: un punto entre su home y el portador, `SupportAdvance` más adelante, entre `SupportMinDistance` y `SupportMaxDistance` del portador. Los demás → `MoveToPoint` (home). |
| El rival tiene el balón | El más cercano al balón → `MoveToBall` (se acerca). Los demás → `MoveToPoint` (home atraído `DefendBallPull`). **No hay marcaje, cobertura ni presión coordinada.** |
| Lejos de su home (`FarFromHomeDistance`) | Vuelve (salvo el que persigue el balón). |
| Llegó al punto (`IdleRadius`) | `Idle`. |
| Fuera de pista / partido sin juego / bloqueado por una acción | `Idle` con su motivo. |

### Con balón (jerarquía)
1. **Tiro claro** → `Shoot`: dentro de `ShootRange`, mirando a portería (`ShootMaxAngleDegrees`), sin rival a menos de `ShootMinOpponentDistance` y sin rival que no sea portero en la línea de tiro. Apunta al lado contrario al portero rival. Si `WorldView.CanUse(CurvedShot)` lo permite y está a más de `CurveMinDistance` → `CurvedShot` con curva según el pie.
2. **Presionado y hay receptor razonable** → `Pass` (acepta un pase de hasta `PassMaxBackwardGain` hacia atrás).
3. **Libre y hay receptor razonable (más adelantado)** → `Pass`.
4. **Hay espacio** → `Dribble` (solo `MovementIntent`; rodea al rival a `DribbleAvoidDegrees`). Con un rival que bloquea de cerca y `CanUse(SkillMove)` → `Dribble` con `SkillMove`.
5. **Presionado sin pase ni espacio** → `Protect`: la acción `Shield` que el Core ya tiene, más retroceso lento. No se inventó ninguna mecánica.
6. **Nada aplica** → `Dribble` hacia portería (`NoActionFallback`). Si no puede patear (enfriamiento), solo se mueve.
No se usa el Overall ni ningún atributo.

### Elección del receptor
Candidatos: compañeros en pista (el portero solo si está presionado y no hay otro), a `PassMinDistance..PassMaxDistance`, con carril libre (`WorldGeometry.IsLaneClear`), con espacio (`ReceiverMinSpace`) y más adelantados (libre) o no demasiado atrás (presionado). Puntuación = peso del avance + espacio − distancia a la distancia cómoda + facilidad de giro. Empate: el de la plantilla antes. Resultado: `PassTarget.ToPlayer(id)` (o ninguno si no hay receptor; el portero con balón y sin receptor despeja largo).
Tipo de pase: `ShortPass`; `LongPass` desde `LongPassDistance`; `SkillLobPass` desde `LobPassDistance` **solo si** `CanUse` lo permite. Potencia según la distancia.

### Portero (mínimo)
Sin balón: se queda en su zona (`KeeperHold`: en la línea entre su portería y el balón) y responde al balón cercano (`KeeperReact`), siempre con el destino limitado a la zona (`PitchBounds.ClampToGoalZone`). Con balón: `GoalkeeperDistribution` a un jugador de campo libre o largo hacia delante. Sin GK AI.

## 3. Skills
El motor solo pregunta `WorldView.CanUse(...)` (que es `SkillResolver.CanUseAction`). No copia requisitos ni lee atributos (test de texto). La ejecución sigue por `SkillResolver`: si el motor pidiese una skill que no se posee, el Core la rechazaría (test).

## 4. Frecuencia
`DecisionIntervalSeconds` = 0.15 s (≈ 6,7 decisiones/s) → 9 pasos a 60 Hz. Cada jugador tiene su turno `(paso + lado·5 + hueco) mod 9`: como mucho 3 deciden en el mismo paso. Entre decisiones el stick sigue donde se puso y el Core sigue simulando.

## 5. Selección automática (`AutoSelection`)
Sin balón: el jugador de campo del equipo más cercano al balón. Con balón: se mantiene el controlado mientras siga en pista; si no, el más cercano. El `MatchController.ControlledPlayerId` (Fase 7) sigue siendo el respaldo si sale de pista. El driver la aplica solo a lados con control humano.

## 6. Determinismo
Misma vista + mismo jugador → misma decisión (test, también con otra instancia del motor). El motor no usa azar; el azar de los disparos y los duelos sigue siendo el del Core. Misma semilla → mismo partido de 30 s (Arena y Classic); otra semilla → otro.

## 7. Prueba de 12 jugadores autónomos
Equipos y formaciones reales, `WorldState → WorldView → BasicDecisionEngine → intents → Core`, 30 s en Arena y en Classic. Con la semilla 5 (equipos 3 contra 8): Arena 2400 decisiones, 19 acciones, 13 pases, 1 tiro; Classic 2399 decisiones, 24 acciones, 19 pases, 1 tiro, 4 reinicios, 1 gol. Invariantes comprobadas cada 6 pasos: 12 jugadores en pista, sin NaN, dentro del campo + margen, stamina 0..100, balón finito (en Arena, dentro de las paredes).

## 8. Límites de esta fase
No hay: Utility AI, Behavior Tree, GOAP, FSM táctica, táctica de equipo, marcaje, cobertura, presión coordinada, defensa zonal, transiciones, desmarques, overlapping, half-space, counter-sprint / counter-press, GK AI, Coach AI, dificultad, árbitro, competición, estadísticas, Unity. No se implementaron las 12 skills diferidas. **Limitaciones conocidas:**
- Nadie **entra a un duelo**: el que persigue al rival con balón solo se acerca (no hay decisión de placar/interceptar); por eso la defensa no recupera balones salvo por pérdidas del regate o balones sueltos.
- Los jugadores sin balón no saben "ocupar espacio" más allá de su home atraído hacia el balón.
- Tras un pase o un tiro el jugador se queda quieto hasta su siguiente decisión.
- Los reinicios (`ConfirmRestart`) los hace quien llama; el driver no los decide.
- Todos los valores son iniciales.

## 9. Qué queda para las fases siguientes
- Decisiones defensivas reales (placar, interceptar, presionar), con coordinación mínima.
- Posicionamiento por rol y línea; desmarques; las 12 skills diferidas.
- Evaluación de receptores con más criterio (líneas de pase, riesgo); decisión de reinicios.
- IA de portero.
- Balance de `BasicDecisionDefaults`; conectar al puente de Unity.

## 10. Legado (auditado; no se borra nada en esta fase)
| Sistema | ¿Consumidores? | ¿Sustituido? | Cuándo podrá eliminarse |
|---|---|---|---|
| `PlayerIntent`, `IIntentSource` | Sí: `PlayerIntentProvider` y la locomoción/animación antigua de Unity | Funcionalmente sí: `NormalizedInput`/`MovementIntent`/`ActionIntent` → Core | Cuando Unity lea el Core (`MatchBridge`) en vez de `PlayerIntentProvider`. |
| `PlayerIntentProvider` (Unity) | Sí: `LocomotionPresenter` y otros | Sí | Misma condición. |
| `MatchController.Tick(dt, BallObservation)` | Sí: hosts y tests que dan su propio balón; `Advance` lo llama | No: es el paso de reglas dentro de `Advance` | No es legado; se mantiene como API de observación. |
| `PlayerLocomotion`, `StaminaSystem` | Aislados en Core (1–2 referencias) y 1 en Unity | Sí: Fase 3 | Con la limpieza de Unity de locomoción. |
| `DribbleTouchModel` | 1 referencia en Core | Sí: conducción de la Fase 4 | Tras comprobar que `BallInteractor` no lo necesita. |
| `BallInteractor` (Unity) | 1 consumidor en Unity | Sí: acciones del Core | Con la migración del balón de Unity al Core. |
| `Core/Ai/*` (`PositioningModel`, `PressureModel`, `AnticipationModel`, `TeamStyle`, `PlayingProfile`) | Sin consumidores de partido | **Todavía no**: el motor básico no los usa a propósito | Se usarán (o se reemplazarán) cuando haya posicionamiento por rol y presión. |

## 11. Fase 9 — lo que cambió aquí
Ver [DEFENSE_AND_ROLE_POSITIONING.md](DEFENSE_AND_ROLE_POSITIONING.md). `DecisionKind` gana (al final) `CloseDown, Tackle, SlidingTackle, Challenge, Intercept`; con el rival con balón ya no se persigue con `MoveToBall`: el defensor principal (y, con motivo, un segundo) cierra y disputa, el resto va a su Home con `HomeTarget`. Los límites de la §8 sobre "nadie entra a un duelo" quedan resueltos en lo básico; sigue sin haber marcaje, cobertura ni presión coordinada.

## 12. Fase 10 — lo que cambió aquí
Ver [ATTACK_SUPPORT_RUNS.md](ATTACK_SUPPORT_RUNS.md). Con un compañero con balón, `WithoutBall` ya no manda a «los dos más cercanos» a apoyar: delega en `AttackSupport` (un apoyo, un corredor, un jugador de amplitud, el resto se desmarca cerca de su sitio). `DecisionKind` gana `GetOpen`, `ForwardRun`, `HoldWidth` (16 en total; el apoyo sigue siendo `Support`). `BestReceiver` pasa a `ReceiverScore` (línea de pase, presión, movimiento del receptor). `AttackPositioningTuning.Enabled = false` devuelve el comportamiento de la Fase 9.

## 13. Fase 11 — lo que cambió aquí
Ver [BALL_CARRIER_INTELLIGENCE.md](BALL_CARRIER_INTELLIGENCE.md). `WithBall` ya no es la jerarquía fija tiro → pase → regate → proteger: el portador puntúa siete tipos de candidato en una escala común (`BallCarrierEvaluator`) y hace el mejor, construyendo el intent con los mismos constructores. `DecisionKind` gana `Hold` (17). `BallCarrierTuning.Enabled = false` devuelve la regla de la Fase 8–10 (los tests de la Fase 8 que la describen la usan).
