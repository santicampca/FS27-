# FS27 — Inteligencia del portador (Fase 11)

Estado: **el portador compara sus opciones en una escala común y elige la mejor**, en el Core, determinista y sin Unity. Rama `phase-11-ball-carrier-intelligence`, desde `ef802d0` (2238/2238).
No es una IA táctica ni un sistema de utilidad genérico: son siete tipos de candidato, una fórmula pequeña por tipo y un `max`. La IA sigue sin ser autoridad: el motor produce `MovementIntent`/`ActionIntent` y el Core resuelve todo (`Perform` → resolutor → `BallSimulator`, con `SkillResolver`).
**(DECISIÓN FASE 11)** = valor o criterio mío. Todos los números viven en `BallCarrierDefaults`/`BallCarrierTuning`; **no están balanceados**.

## 1. Arquitectura
| Pieza | Archivo | Qué hace |
|---|---|---|
| `BallCarrierEvaluator` | `Decision/BallCarrier/BallCarrierEvaluator.cs` | Puntúa todos los candidatos del portador y devuelve el mejor. Pura: lee solo la vista, no cambia nada, no asigna memoria (los candidatos viven en un array propio). |
| `ActionCandidate`, `CandidateType`, `RunKind`, `ITeammateRuns` | `Decision/BallCarrier/ActionCandidate.cs` | El candidato (tipo, puntuación, receptor, punto, carrera, razón solo para depurar) y la interfaz mínima con la que el portador «ve» las carreras. |
| `DribbleSpace` | `Decision/BallCarrier/DribbleSpace.cs` | La mirada hacia delante del portador (la regla de la Fase 8, movida aquí: **una sola** regla sirve para comparar y para ejecutar). |
| `BallCarrierDefaults/Tuning` | `Decision/BallCarrier/BallCarrierDefaults.cs` | Todos los números, con interruptor `Enabled`. |
| `BasicDecisionEngine.CarrierDecision` | `Decision/BasicDecisionEngine.cs` | Convierte el candidato ganador en el intent con los constructores que ya existían (`BuildShot`, `PassTo`, `TryDribble`, `Protect`) + `ThroughTo` y `Hold`. |

El evaluador **no conoce** `AttackSupport`, `PlayerDefinition`, `MatchState` ni `MatchController` (un test lo comprueba): recibe `ITeammateRuns`, que implementa `AttackSupport`. `DecisionKind` gana solo `Hold` (17 en total); pasar al espacio es un `Pass` con acción `ThroughPass`.

## 2. Candidatos y puntuación
Todos puntúan en una escala común (~0–5). Gana el de mayor puntuación; empate: el primero en este orden fijo (tiro, pases por orden de plantilla, pases al espacio, regate, proteger, esperar). Sin azar.

`presión` = 1 − distancia al rival más cercano / `PressureRadius`, más `PressureExtraOpponent` por cada rival adicional dentro del radio, acotada a 0..1.

| Candidato | Puntuación | Existe cuando |
|---|---|---|
| **Shoot** | `ShootScale × (0,40·cercanía + 0,25·ángulo + 0,20·línea libre + 0,15·portero lejos de la línea) − ShootPressureWeight × presión` | Dentro de `ShootRange`, ángulo ≤ máximo, línea a puerta sin rival (los porteros no cuentan para la línea) y puede golpear. |
| **Pass / ForwardPass** | `ReceiverScore` (Fase 8/10) + bono de carrera + `ForwardPassBonus` (si gana ≥ 10 % del campo) + `SafePassWeight × presión × seguridad` − penalización si el receptor está de espaldas con un rival al lado | `ReceiverScore` lo admite (alcance, línea libre, espacio, retroceso máximo). El portero solo desde `KeeperPassPressure`. |
| **ThroughPass** | `ThroughBase + peso·avance + peso·espacio + peso·margen de carrera + bono de carrera adelante − ThroughRisk` | Ver §4. |
| **Dribble** | `DribbleScale × avance × espacio libre − peso·presión − peso·cercanía a su portería − peso·cansancio` (o `SkillMoveScore` si ambos lados están cerrados y puede) | Hay camino libre; si no, un candidato de «seguir caminando» con `DribbleFallbackScore`. |
| **Protect** | `ProtectBase + ProtectPressureWeight × presión (+ ProtectWaitBonus si no hay pase y viene una carrera)` | Un rival a menos de `PressureRadius`. |
| **Hold** | `HoldBase (+ HoldRunBonus si hay una carrera adelante en marcha)` | Presión ≤ `HoldMaxPressure` y dentro de la ventana del reloj (§7). |

Las prioridades salen de las puntuaciones, no de una cadena de `if`: un tiro claro (≈ 3–4,5) supera a pases mediocres (≈ 2–2,5); un pase a un corredor libre (≈ 3,3–3,7) supera a un tiro mediocre (≈ 2–2,5); el espacio libre favorece el regate (≈ 3,2); la presión con pocas alternativas favorece proteger; sin nada claro, esperar.

## 3. Pases y carreras
`ReceiverScore` (Fase 10) sigue siendo la base de calidad del receptor (línea, espacio, avance, presión sobre él, su movimiento). Encima, el portador lee la **carrera** del compañero (`RunKind`) y suma un bono: adelante `RunForwardBonus` 1,5 (escalado por lo bien que va: `RunBaseShare` al empezar, el resto según su velocidad hacia la portería), apoyo 0,6, amplitud 0,5, desmarque 0,3. Una carrera bloqueada no da bono: sin línea no hay candidato. El receptor de espaldas con un rival cerca pierde valor; el que obliga a retroceder más del máximo ni siquiera es candidato; el retroceso menor pierde por el término de avance del propio `ReceiverScore`.
Los roles **no** llegan al portador directamente: llegan, si acaso, a través de la carrera que su dueño hace (el trabajo ofensivo de la Fase 10 depende del rol como preferencia). Ningún rol fuerza un pase.
**Cómo «detecta» el portador la carrera:** `AttackSupport.Assign` es una función pura de la vista; el portador la evalúa y obtiene el mismo reparto que cada compañero obtiene para sí. Es equivalente a observar su intención sin tener memoria compartida; además, el bono adelante usa la velocidad **observada** del compañero. Límite: en un equipo con jugador humano la intención real del humano no se conoce (su trabajo es el que la función calcularía).

## 4. Pase al espacio
Solo para un compañero que corre adelante (por trabajo, o visto avanzando ≥ `ThroughMinRunnerSpeed`) y solo si el portador puede jugar un `ThroughPass` (`WorldView.CanUse`). El punto = su posición + su dirección × (`ThroughLeadDistance` + velocidad × `ThroughLeadSeconds`). El candidato existe solo si: el punto está dentro del campo, a alcance de pase, por delante del portador (≥ `ThroughMinGain`), con línea libre, con espacio (≥ `ThroughMinSpace`) y **el corredor y el balón llegan al punto al menos `ThroughRaceMargin` antes que el rival más cercano** (tiempos con la velocidad máxima de cada uno). Sin esa ventaja no hay pase peligroso. El intent usa `PassTarget.ToPosition(punto)`; `TargetPlayerId` se rellena solo como información (para medir). **Límite:** el Core solo registra «receptor previsto» para destinos de jugador; con un destino de punto, el corredor llega al balón porque va hacia allí y el compañero más cercano lo persigue, pero no hay reserva del receptor.

## 5. Tiro
Graduado en vez de una puerta: la presión resta valor en lugar de prohibir el tiro (la Fase 10 prohibía con un rival a < 1,5 m); la distancia, el ángulo, la limpieza de la línea y la distancia del portero a la línea suben o bajan la amenaza. Se conservan como filtros duros solo el alcance, el ángulo máximo y que no haya un rival (que no sea el portero) sobre la línea. La existencia de un pase mejor se resuelve por comparación. El constructor del tiro (lado del portero, tiro con efecto si la skill lo permite) es el de la Fase 8.

## 6. Dribble y Protect
**Dribble:** reutiliza `DribbleSpace` (cono, desvío hacia el lado contrario al bloqueador, regate con skill). Pierde valor con presión, con poco espacio, cerca de su propia portería (perder el balón allí es peligroso) y con poca stamina (`LocomotionStamina` del estado; no hay modelo nuevo). Hay una puntuación base pequeña de «seguir» para no quedarse sin opción.
**Protect:** es una opción más con su puntuación (más alta con más presión), no un reflejo: con un compañero libre o espacio para conducir, gana otro candidato (test). Gana cuando la presión es alta y no hay pase ni camino.

## 7. Hold
Esperar un instante (parado, sin acción) para que las carreras de la Fase 10 se desarrollen. Es la opción por defecto cuando nada supera `HoldBase` (1,6), sube con una carrera adelante en marcha, no existe con presión > `HoldMaxPressure`, y solo se permite durante los primeros `HoldWindowSeconds` (0,6 s) de cada `HoldCycleSeconds` (1,2 s) del **reloj del partido**. La ventana es una función pura del reloj (no hay memoria por jugador): nunca espera más de 0,6 s seguidos. Sale de la espera de forma natural cuando el pase a la carrera sube de valor o aparece presión.

## 8. Integración con la Fase 10, transiciones y portero
- Ataque sin balón: sin cambios. `AttackSupport` sigue siendo el que decide qué hace cada compañero; el portador solo lo lee por `ITeammateRuns`.
- Ataque → defensa: intacta (Fase 9). Defensa → ataque: el nuevo portador evalúa en su primera decisión; no hay FSM.
- Portero con balón: **sin cambios** (la lógica mínima de la Fase 8: distribuir a un compañero libre o despejar).
- Skills: solo `WorldView.CanUse` (tiro con efecto, regate con skill, pase por alto, pase al espacio); la ejecución sigue en `SkillResolver`.

## 9. Determinismo, rendimiento y comparación
Sin `System.Random`, `UnityEngine.Random`, `DateTime`, `Guid` ni LINQ en el evaluador (test). Misma semilla ⇒ mismas decisiones, pases, pases al espacio, esperas, tiros y goles (test en 120 s Arena y Classic). Rendimiento: un recorrido por jugadores para los pases y otro para los pases al espacio (≤ 12 jugadores, cada uno con unos pocos bucles sobre 12), sin listas ni asignaciones.
`BallCarrierTuning.Enabled = false` restituye exactamente al portador de la Fase 10 (para comparar y para los tests de la Fase 8, que describen esa regla).

## 10. Simulación: 8 partidos de 120 s por modo, equipos y semillas distintos (Arena 301–308, Classic 321–328)
Medido, no balanceado. F10 = el mismo escenario con `Enabled = false`.

| | Arena F10 → F11 | Classic F10 → F11 |
|---|---|---|
| Pases completados | 848 → 675 | 798 → 669 |
| …a apoyo / corredor / amplitud / desmarque | 240/103/162/317 → 194/118/109/234 | 253/131/149/248 → 206/96/145/224 |
| Pases al espacio (intentos) | 0 → 23 | 0 → 26 |
| Tiros (oportunidades) | 75 (–) → 58 (239) | 63 (–) → 54 (191) |
| Goles | 18 → 6 | 10 → 12 |
| Decisiones de regate / proteger / esperar | 1210/100/0 → 1145/401/172 | 1094/72/0 → 1350/223/169 |
| Posesiones / recuperaciones | 589 / 403 → 469 / 301 | 575 / 365 → 508 / 253 |
| Faltas | 0 → 0 | 7 → 18 |
| Líneas de pase libres por muestra | 1,70 → 1,62 | 1,73 → 1,87 |
| Distancia portador–receptor | 9,0 → 9,6 m | 9,6 → 10,1 m |
| Entropía de las decisiones del portador | 1,37 → 1,85 bits | 1,34 → 1,69 bits |

**Lectura honesta.** Se ve lo que la fase pretendía de forma estructural: el portador toma decisiones **más variadas** (la entropía sube de 1,3–1,4 a 1,7–1,9 bits; aparecen esperas y pases al espacio), más pases van **al corredor** en Arena (103 → 118; en Classic bajan, 131 → 96) y las líneas de pase mejoran en Classic. **No** se puede decir que mejore el juego ofensivo en conjunto: hay **menos pases y menos tiros** (el portador conduce más y protege más), en Arena bajan los goles y en Classic suben, con cifras demasiado pequeñas para concluir nada; las **faltas en Classic suben** (7 → 18) probablemente por pasar más tiempo en contacto (proteger/esperar/conducir) y no se investigó. Un ajuste: tras la primera medición, la protección dominaba (el portador protegía 4 de cada 10 decisiones) y se bajaron `ProtectBase`, `ProtectPressureWeight` y `DribblePressureWeight` para que fuese una opción y no un reflejo; no se ajustó nada más ni se buscó ninguna cifra.

## 11. Qué queda fuera / límites
Utility AI general, BT, GOAP, ML, planificación a varios pasos, Monte Carlo, xG/xA, tácticas, marcaje, cobertura, presión coordinada, contrapresión, medio espacio, overlapping, llegada tardía, IA de portero avanzada, Coach AI, dificultad, árbitros, Classic completo, competición, estadísticas, replay, Unity, skills diferidas y balance. Límites conocidos: el pase al espacio no reserva receptor; la espera usa el reloj, no una intención sostenida; el portador no estima si un tiro supera al portero rival más allá de la distancia a la línea; no hay memoria (cada decisión es independiente); los pesos son iniciales.

## 12. Legado (auditado; sin cambios)
`PlayerIntent` (10 archivos en Core, 3 en Unity), `IIntentSource` (3/3), `PlayerIntentProvider` (1/4), `PlayerLocomotion` (2/1), `StaminaSystem` (2/0), `DribbleTouchModel` (1/0), `BallInteractor` (1/2): ningún consumidor nuevo en esta fase. Limpieza para la fase de cierre Core/Unity.
