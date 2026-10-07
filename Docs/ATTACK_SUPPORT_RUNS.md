# FS27 — Ataque sin balón: apoyo, desmarques y amplitud (Fase 10)

Estado: **capa ofensiva básica sin balón, en el Core, determinista y sin Unity.** Rama `phase-10-attack-support-runs`, desde `5ee7722` (2204/2204).
No es una táctica de ataque: es la cadena **un compañero tiene el balón → cada jugador de campo recibe un trabajo (apoyar, correr al espacio, dar amplitud, o desmarcarse cerca de su sitio) → un punto objetivo → `MovementIntent`**, más una puntuación de receptor mejor para el portador.
La IA sigue sin ser autoridad: solo produce intents. Nada de esta fase mueve jugadores, balón, stamina ni `MatchState`; el Core (locomoción, separación, resolutores) decide lo que ocurre.

**(DECISIÓN FASE 10)** = valor o criterio mío, no especificado. Todos los números viven en `AttackPositioningDefaults` / `AttackPositioningTuning` (cada constante tiene su gemela editable; un test lo exige).

## 1. Qué hay y dónde
| Pieza | Archivo | Qué hace |
|---|---|---|
| `AttackPositioningDefaults/Tuning` | `Decision/AttackPositioningDefaults.cs` | Todos los números de la fase, con interruptor `Enabled`. |
| `AttackSupport` | `Decision/AttackSupport.cs` | `Assign` (quién hace qué) y `Plan` (dónde se coloca). Función pura de la vista. |
| `ReceiverScore` | `Decision/ReceiverScore.cs` | Puntuación de un compañero como receptor de un pase. |
| `WorldGeometry.PassLaneRoom / IsPassLaneClear / FindBestOpenPoint` | `World/WorldGeometry.cs` | Geometría centralizada: línea de pase y búsqueda de punto libre. |
| `WorldPlayer.Roles` / `AffinityOf` | `World/WorldTypes.cs` | Los roles del jugador con su afinidad, leídos del perfil (ampliación de `WorldView`; el motor no lee `PlayerDefinition`). |
| `MatchController.SetPlayingProfiles` | `Match/MatchController.Infrastructure.cs` | El anfitrión da el catálogo de perfiles; opcional (sin él, ningún jugador tiene rol y todo sigue funcionando). |
| `DecisionKind` | `Decision/PlayerDecision.cs` | Se añaden `GetOpen`, `ForwardRun`, `HoldWidth` (16 en total). |

**Reutilización:** `SupportRun` **es** el `DecisionKind.Support` de la Fase 8 («ofrecerse al compañero que tiene el balón»); `LateralSupport` y `HoldWidth` son lo mismo → solo `HoldWidth`. Se añadieron 3 tipos, no 5. No se creó una segunda arquitectura: `BasicDecisionEngine.WithoutBall` delega en `AttackSupport` cuando hay un compañero con balón y `Enabled` es verdadero; si no, queda el comportamiento de la Fase 9 (usado solo para comparar).

## 2. Los cuatro trabajos
Para cada jugador de campo del equipo con balón (no el portador, no el portero, no el que controla una persona, en pista) `Assign` calcula tres puntuaciones y reparte, **por este orden de prioridad**, un apoyo, un corredor y un jugador de amplitud (el mejor de cada una; empate: el de la plantilla antes). Quien queda, se desmarca cerca de su sitio. Los máximos (`SupportRunners`, `ForwardRunners`, `WidthHolders` = 1/1/1) son configurables.

- **SupportRun** (`Support`): puntuación = cercanía a la distancia ideal del portador (`SupportIdealDistance` 8 m) − lejanía a su Home + preferencia de rol. Solo candidatos a menos de `SupportRadius` (16 m). Punto: entre su Home y el portador, algo más adelantado (el de la Fase 8), refinado por la búsqueda para estar a entre `SupportMinDistance` y `SupportMaxDistance` del portador, con línea de pase libre y espacio. No corre al balón (mínimo 5 m) y no se aleja más de `JobMaxHomeDeviation` (9 m) de su Home.
- **ForwardRun** (`ForwardRun`): puntuación = cuánto más adelantado que el portador (normalizado) + espacio delante (a `ForwardProbeDistance`) + rol. Exige `ForwardMinStamina` (45): un jugador cansado nunca es el corredor. Punto: a `ForwardRunDistance` por delante de él, refinado; **debe ganar al menos `ForwardMinGain` (1,5 m)**, si no, ese jugador solo se desmarca (`GetOpen`). Dentro de `ForwardMaxHomeDeviation` (11 m) de su Home: no abandona su zona. En los primeros `RecoverySeconds` (2,5 s) tras recuperar el balón corre a velocidad máxima y las puntuaciones de cercanía/avance se multiplican por `RecoveryBoost` (el más cercano se ofrece, el más adelantado corre).
- **HoldWidth** (`HoldWidth`): puntuación = lateralidad de su Home + bono si está en la banda + rol. Punto: junto a su banda (`WidthMargin` 2 m de la línea), entre su Home y el portador en X, a entre 6 y 18 m del portador, con línea libre. Mantiene el lado de su Home.
- **GetOpen** (`GetOpen`): el punto con más espacio en un radio pequeño alrededor del objetivo de Home de la Fase 9, a menos de `GetOpenMaxHomeDeviation` (5 m) de su Home.

Una decisión de trabajo conserva su tipo aunque el jugador ya haya llegado (se queda quieto), para poder leer qué hace cada uno.

## 3. Búsqueda de espacio (`WorldGeometry.FindBestOpenPoint`)
Pocos candidatos deterministas: el punto base y dos anillos de `SearchDirections` (8) puntos, siempre en el mismo orden. Se descartan los que quedan fuera del campo (con margen) o fuera de la correa de su Home. Puntuación de un candidato: espacio respecto al rival más cercano (tope `OpenCap`) + holgura de la línea de pase desde el portador − falta de distancia a la banda deseada + avance (si se pide) + lateralidad (si se pide) − lejanía a su Home + separación del compañero más cercano (evita el agrupamiento) − un pequeño coste por moverse (evita parpadeo). Empates: el primer candidato. Sin LINQ, sin listas, sin asignaciones. Si ningún candidato cabe en la correa, el punto base se acerca a ella.

## 4. Línea de pase
`PassLaneRoom(view, equipo, de, a)`: distancia del rival más cercano a la recta **contando solo a los que están entre ambos extremos** (uno detrás del que pasa o más allá del receptor no puede parar el balón; esto incorpora el ángulo que pedía la especificación). `IsPassLaneClear(..., clearance, maxDistance, out room)`: dentro de alcance y con holgura ≥ `PassLaneClearance`. Geometría solamente: sin xAssists ni probabilidades.

## 5. `ReceiverScore`
Los términos de la Fase 8 (avance, espacio, distancia preferida, giro del portador) más: **holgura de la línea** (`ReceiverLaneWeight`), **presión sobre el receptor** (cada rival a menos de `ReceiverPressureRadius` resta) y **compatibilidad con su movimiento** (si corre alejándose del que pasa, suma; si vuelve hacia él, resta). Devuelve −∞ para quien no es candidato (rival, fuera de pista, demasiado cerca o lejos, línea bloqueada, sin espacio, sin avance suficiente, portero salvo petición). Con `Enabled = false` es exactamente la puntuación de la Fase 8/9 (incluida su prueba de línea).

## 6. Roles: una preferencia, no una regla
`AttackSupport.RoleBias` = `RoleBiasWeight` × (valor de tabla del rol para el trabajo: 1,0 preferido / 0,5 aceptable) × afinidad/100, tomando el mejor de los (hasta 3) roles. Tabla: Creator/Architect → apoyo (y correr, aceptable); Builder/Anchor → apoyo; Winger → amplitud; Explosive/Finisher/GoalHunter/Target → correr; Engine → apoyo o amplitud; Wall/Guardian → apoyo (aceptable). Sin perfiles, la posición gruesa (`PlayerRole`) da una inclinación pequeña. **Por qué no es rígido:** la preferencia máxima suma 1,0 y la parte contextual (distancia, espacio, avance, lateralidad de su Home) suma hasta ~3; el rol desempata entre jugadores en situaciones parecidas (test: dos jugadores simétricos, ganan los roles en las dos direcciones), pero un Winger que es el único bien situado apoya, y un Finisher cansado no corre (tests).

## 7. Coordinación y agrupamiento
Un apoyo, un corredor, un jugador de amplitud como máximo; el resto, cerca de su sitio. El agrupamiento se evita con el término de separación de la búsqueda (decide **dónde quiere ir**; la separación física de la Fase 7 sigue siendo la autoridad del solapamiento) y porque cada trabajo tiene su propia banda y su lado. No hay coordinación de objetivos entre compañeros más allá de la posición actual de los demás (**límite conocido**).

## 8. Otras transiciones
- **Ataque → defensa:** intacta (Fase 9): principal, secundario, el resto a Home. Un test comprueba que nadie conserva un trabajo ofensivo cuando el rival tiene el balón.
- **Defensa → ataque:** regla simple (`RecoverySeconds`/`RecoveryBoost`), sin FSM.
- **Portero con balón:** sin cambios en su lógica; un jugador de campo se ofrece cerca del portero (dentro de su correa) como apoyo.

## 9. Simulación: 120 s, 12 jugadores reales, rosters y formaciones reales, asignación por rol, defensa y ataque
Medido, no balanceado. «P9» = el mismo partido con `Enabled = false` (el equivalente de la Fase 9).

| Una ejecución (equipos 3 y 8) | Arena (sem. 111) P9 → P10 | Classic (sem. 121) P9 → P10 |
|---|---|---|
| Pases completados | 87 → 116 | 119 → 114 |
| Tiros | 10 → 5 | 11 → 10 |
| Goles | 1 → 0 | 0 → 0 |
| Posesiones / pérdidas | 88 / 87 → 88 / 87 | 81 / 80 → 87 / 86 |
| Recuperaciones | 62 → 53 | 58 → 61 |
| Decisiones de apoyo / carrera / amplitud / desmarque | 567/0/0/0 → 227/147/243/339 | 544/0/0/0 → 298/152/300/476 |
| Líneas de pase libres por muestra | 1,56 → 1,89 | 1,49 → 1,99 |
| Distancia media entre compañeros | 9,6 → 10,0 m | 9,1 → 11,1 m |
| Distancia media del portador al receptor elegido | 7,1 → 8,1 m | 7,7 → 8,3 m |

Agregado sobre 8 partidos de 120 s por modo (semillas 201–208, equipos distintos): Arena P9 → P10: pases 691 → 777, tiros 88 → 71, goles 15 → 13, posesiones 587 → 511, recuperaciones 487 → 384, líneas por muestra 1,60 → 1,65, separación entre compañeros 10,5 → 11,1 m. Classic: pases 776 → 798, tiros 70 → 81, goles 4 → 14, posesiones 674 → 583, recuperaciones 450 → 310, faltas 6 → 5, líneas 1,64 → 1,80, separación 10,5 → 11,4 m. De los pases decididos con la capa activa, aproximadamente el 30 % va al apoyo, el 15 % al corredor, el 22 % al jugador de amplitud y el 28 % a un jugador que se desmarca.

**Lectura honesta:** la capa consigue lo que se midió directamente — hay apoyos, carreras, amplitud y desmarques en todos los partidos, más líneas de pase libres desde el portador, compañeros más separados (menos agrupamiento) y más pases en Arena; las posesiones son más largas. **No** produce un aumento claro de tiros (baja en Arena y sube en Classic en el agregado) ni se debe leer ningún efecto en goles (cifras demasiado pequeñas). La causa probable (no investigada en esta fase): el portador decide tirar solo dentro de `ShootRange` (11 m) con ángulo y línea libres, y su decisión de pase/avance de la Fase 8 no valora la carrera. Quedó para la fase siguiente.

## 10. Determinismo
Misma semilla, rosters y formaciones ⇒ mismas asignaciones, movimientos, decisiones, pases, tiros, goles y contadores (test: dos ejecuciones de 120 s en Arena y Classic con huella idéntica; otra semilla da otra). Sin `System.Random`, `UnityEngine.Random`, `DateTime`, `Guid` ni LINQ en los archivos nuevos (test). `Decide` es una función pura de la vista: no cambia nada del partido (test con huella del partido).

## 11. Rendimiento
`AttackSupport` reutiliza arrays del motor; la búsqueda mira 17 candidatos × ≤ 12 jugadores; `Assign` se recalcula en cada decisión del jugador (≈ 5 por ronda) sin asignar memoria. No se usa LINQ ni se crean listas por jugador.

## 12. Qué queda fuera (no implementado)
Utility AI, árbol de comportamiento, GOAP, FSM táctica, tácticas de equipo, marcaje, cobertura avanzada, presión coordinada, defensa zonal, línea defensiva, contrapresión, contra-sprint, overlapping avanzado, half-space, llegada tardía al área, IA de portero avanzada, Coach AI, dificultad, Classic completo, árbitros, competición, estadísticas, replay, Unity (animación, HUD, cámara, VFX, audio) y las 12 skills diferidas. Tampoco: valoración de pases por probabilidad, coordinación de destinos entre compañeros, carreras que el portador entienda (el portador no sabe qué compañero «corre»), cambios de trabajo con histéresis.

## 13. Legado (auditado; no se borra nada)
Sin cambios desde la Fase 9: `PlayerIntent` (10 archivos en Core, 3 en Unity), `IIntentSource` (3/3), `PlayerIntentProvider` (1/4), `PlayerLocomotion` (2/1), `StaminaSystem` (2/0), `DribbleTouchModel` (1/0), `BallInteractor` (1/2) siguen vivos por consumidores del Core antiguo, de Unity y de tests. Esta fase no los toca ni añade consumidores. Su limpieza queda para la fase de cierre Core/Unity.

## 14. Fase 11
El límite «el portador no sabe qué compañero corre» (§12) queda resuelto en lo básico: ver [BALL_CARRIER_INTELLIGENCE.md](BALL_CARRIER_INTELLIGENCE.md). `AttackSupport` implementa `ITeammateRuns` (`RunOf`) y el portador valora las carreras de apoyo, adelante, amplitud y desmarque, y pasa al espacio de un corredor. La asignación de trabajos no cambió.
