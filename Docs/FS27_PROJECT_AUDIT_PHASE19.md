# FS27 — Auditoría del estado del proyecto (Fase 19)

Rama `phase-19-project-audit`, partiendo de `phase-18-goal-line-detection` (`421a50c`). Fecha de los datos: estado del repositorio al inicio de la Fase 19.
**Esta fase no cambia ninguna regla, IA, balance, arquitectura ni gráfico.** Solo documenta, y añade dos sondas de medición (explícitas, no forman parte de la batería normal).

## 0. Método y límites
* Lectura directa del código actual (`Assets/_Project/Scripts`), búsquedas dirigidas, la lista de tests y los datos de `Docs/authoring`. No se reconstruyeron las fases anteriores; se consultó su documentación solo para confirmar dependencias.
* **Medido de verdad** (con código ejecutado): (a) rendimiento de un partido completo por modo (sección 8); (b) la calidad que el Core da a cada tipo de pase (hallazgo 1 de abajo); (c) los recuentos de tests y de datos. Todo lo demás es lectura de código: dice qué hay, no que funcione bien.
* **Nada de la capa Unity se ha ejecutado nunca** en este trabajo (`Tests/UnityLayerCheck` solo la compila contra stubs). Todo lo que dependa de Unity está marcado "no verificado".
* Escala de estados: **Implementado** (existe, está en el Core, tiene tests), **Parcial** (existe una parte o solo el contrato/evento), **No implementado**.

### Estructura y tamaño
| Parte | Archivos `.cs` | Líneas | Tests |
|---|---|---|---|
| Core (sin Unity) | 247 | ~38 800 | ver abajo |
| de las cuales Creator | 48 | ~12 600 | 459 |
| Capa Unity (`Gameplay`, `Cameras`, `Input`, `UI`, `Editor`, `Infrastructure`) | 25 | ~1 900 | 0 (solo compila contra stubs) |
| Tests (`Tests/EditMode`) | 87 archivos | – | 2387 casos (2315 atributos `[Test]`, más los `[TestCase]`) |
Reparto aproximado de los `[Test]`: datos, jugadores, equipos, árbitros y entrenadores 714; IA y dificultad 681; Creator 459; balón, acciones, reglas, locomoción e input 350; habilidades 94; cámara 17.
Contenido de Unity: **`Scenes`, `Prefabs`, `Data`, `Audio`, `Art/*` están vacíos** (solo `.gitkeep`). No hay escenas, prefabs, arte ni audio.
Datos del juego (se leen desde `Docs/authoring/*.json` y `.md` mediante una función del anfitrión, `GameDatabaseLoader`): 160 jugadores (24 porteros, 136 de campo), 12 equipos (11 oficiales y 1 de usuario), 20 entrenadores, 5 árbitros, 37 habilidades, 5 dificultades, 4 formaciones base, 185 identidades.

## 1. Estado del Core: balón y acciones
Todas las acciones pasan por `MatchController.Perform` → `Resolve` (`Match/MatchController.Actions.cs`) con un `ActionRequest`; la enumeración es `FootballActionKind` (`FootballActionKind.cs`) y el mapa botón/gesto → acción es `ActionMapper` (`Actions/ActionTypes.cs`).

| Capacidad | Estado | Dónde está y notas |
|---|---|---|
| Pase corto | **Implementado, con un defecto** | `ShortPass` → `ResolveKick` (`MatchController.Actions.cs` ~204). **Hallazgo 1:** `ShortPass` no tiene `case` propio en el `switch` interno y cae en la rama `default` (la de la distribución del portero): su calidad base sale de `KeeperShortDistributionShare·Distribution + …·Kicking` del perfil de portero *de emergencia* (`EmergencyKeeperProfile.Derive`, factor 0,75) en vez de `PassQuality`. Medido (`Phase19Probe_PassQualityByKind`): para el mismo jugador, el pase corto tiene el 75 % de la calidad de cualquier otro pase (atributos 80: 0,602 frente a 0,806; 95: 0,714 frente a 0,959). Afecta al botón B (toque) del jugador humano y a los pases normales de la IA. **No se corrigió en la Fase 19** (no cambiaba reglas); corregido en la Fase 20 (`SHORT_PASS_PHASE20.md`). |
| Pase largo | Implementado | `LongPass` y `SkillLobPass` (balón por alto, `LongPassLoft`); la IA usa `LongPass`. |
| Pase al espacio | Implementado | `ThroughPass` (más velocidad y error) + `Decision/BallCarrier/DribbleSpace.cs`, candidato `ThroughPass` en `BallCarrierEvaluator`; la IA lo elige (razón `SpacePass`). |
| Pase de pared | Implementado en el Core, **no lo usa la IA** | `WallPass`. |
| Tiro | Implementado (parcial) | `Shot`, `CurvedShot` (habilidad), `Chip`; `PlacedShot` **no implementado** (devuelve `NotSupported`). La IA usa `Shot` y `CurvedShot`; no usa `Chip`. |
| Regate | Implementado | Llevar el balón: `StepDribble` + `DribbleControlQuality` (presión, escudo); `SkillMove` (`ResolveSkillMove`); `Ball/DribbleTouchModel.cs`. IA: candidato `Dribble` (`DribbleSpace`). |
| Protección | Implementado | `Shield` (`ResolveShield`); la IA lo usa (`Protect`) y el escudo mejora el control del regate. |
| Centros | **No implementado** | `Cross` existe en la enumeración, pero `Resolve` lo rechaza (`NotSupported`). No hay candidato de centro en la IA. |
| Cabezazos | **No implementado** | `Header` en la enumeración, rechazado. Además `MaxTouchHeight` impide a los jugadores de campo tocar balones altos. |
| Voleas | **No implementado** | No existe ni en la enumeración. |
| Rebotes | **Parcial** | Modos de balón `Rebound` y `Deflected` (`MatchBall`, paredes de Arena, paradas del portero con desvío `ParryAngle…`); `ReboundResponse` en la IA del portero. No hay lógica específica de segundo balón. |
| Segundas jugadas | **Parcial / no específico** | Solo el comportamiento general: el más cercano persigue el balón suelto (`IsNearestFieldPlayerToBall`), los delanteros corren tras recuperar (`AttackSupport`). Medido en la Fase 17: rebotes y desvíos son el 0–2 % de los goles. |
| Otras defensivas | Implementado | `Tackle`, `SlidingTackle`, `PhysicalChallenge`, `Intercept`, `Block`, `Pressure`, `Clearance`. La IA usa `Tackle`, `SlidingTackle`, `PhysicalChallenge` e `Intercept`; **no aparecen en su código** `Clearance`, `Block`, `Pressure`. |
| Portero | Implementado | `GoalkeeperSave/Catch` (por contacto, no como acción), `GoalkeeperDistribution` (la IA reparte con la mano/pie), `GoalkeeperRush` (**sin efecto mecánico**: solo contrato). |

## 2. Reglas del partido
### Gol — Implementado (corregido en la Fase 18)
* Autoridad única: `MatchController.TickPlaying` → `rules.JudgeBall` → `MatchRules.Evaluate` / `HasCrossedGoalLine` (`Match/MatchRules.cs`). Radio del balón 0,2 (`BallParameters`); campo 40 × 25; portería 5 × 2.
* **Línea:** se exige el balón completo al otro lado (centro a más de 20,2). **Balón completo dentro:** sí. **Propias puertas:** `RegisterGoal` marca `OwnGoal` si el último toque es del equipo que recibe el gol.
* **Postes y larguero — Parcial:** la anchura se juzga por el *centro* del balón (|z| ≤ 2,5) y la altura por la parte baja del balón (y − radio ≤ 2); **no hay colisión física con postes ni travesaño** (no existe código de rebote en ellos): el balón atraviesa la geometría de la portería sin chocar.
* Un portero con el balón no puede llevarlo por encima de su línea y un balón ya cruzado no se puede jugar (Fase 18, interruptor `KeeperHoldsBallOnThePitch`).

### Faltas — Parcial
* **Detección:** solo en los duelos: `ResolveDuel` emite `FoulCommitted` cuando una entrada falla por un margen bajo y el infractor llega por detrás (`FromBehind`), en el modo que tiene faltas (Classic). **Entradas:** `Tackle` implementado. **Barridas:** `SlidingTackle` implementado. **Contactos:** hay contactos de duelo (`PhysicalChallenge`) y separación entre jugadores (`PlayerSeparation`), pero no una detección de contactos genérica.
* **Consecuencias: ninguna.** Solo se publica el evento (`FoulCommitted`): no se detiene el juego, no hay tiro libre, ni se reparte tarjeta, ni hay ventaja (el propio comentario del código lo dice). Solo lo consumen los diagnósticos.

### Tarjetas — Parcial
* `MatchController.IssueCard` (`Match/MatchController.cs` ~400): amarilla y roja directa, **segunda amarilla = expulsión**, `Dismiss` (el jugador sale, el portero expulsado obliga a poner otro, `TryReplaceDismissedKeeper`), equipos con pocos jugadores pierden por incomparecencia (`CheckInsufficientPlayers`). Solo en Classic.
* **Nadie llama a `IssueCard` en el juego** (solo los tests): no hay árbitro en ejecución que decida. `Referees/` y los 5 árbitros son solo datos (estilo, identidad). Acumulación entre partidos: no existe.

### Penales — Parcial (casi solo contratos)
* **Detección:** no existe (`PenaltyAwarded` nunca se publica). **Ejecución:** no existe como acción (`RestartKind.Penalty` es un contrato). **IA:** no existe.
* Sí existe la **tanda de penaltis como contador**: `PenaltyShootoutState` (orden, muertes súbita, decisión anticipada), fases del partido y `CompetitionResolver`. Los lanzamientos no se simulan: el anfitrión llama a `RecordShootoutKick(scored)`.

### Tiros libres — No implementado
* `RestartKind.FreeKick` es un contrato. No hay falta normal que lo dé, ni barrera, ni ejecución, ni IA.

### Fuera de juego — No implementado (no estaba en la lista, pero falta)
* `HasOffside` es un interruptor de modo; `OffsideDetected` nunca se publica. Dos habilidades del catálogo están aplazadas por esto y por las faltas (`OffsideTrapExploitation`, `TacticalFoul`).

### Otras reglas que sí existen
Saques de banda, córners, saques de puerta y saques de inicio (`MatchRules.Evaluate`, `RestartRequest`); prórroga y tanda como estados; sustituciones (`TrySubstitute`); portero de emergencia (`EmergencyKeeperProfile`); pausa (`MatchController.Pause`); descansos y recuperación de resistencia; dos modos con reglas distintas (`ArenaRules`, `ClassicRules`; Arena con paredes y sin faltas, fuera, córners ni tarjetas).

### VAR — solo diseño (no implementar)
**Estado actual:** no existe nada: no hay estado "decisión provisional", ni grabación de pruebas, ni moneda. Lo siguiente es un diseño para fases posteriores.

**Piezas propuestas** (todas en el Core, deterministas, sin Unity):
1. `ReviewableDecision`: una decisión que se puede revisar (`Goal`, `Penalty`, `RedCard`), con id, tiempo, equipo, veredicto original y un **margen de duda** calculado por la misma función que decidió (por ejemplo, cuánto sobrepasó el balón la línea, la gravedad del contacto frente al umbral de falta/roja y el estilo del árbitro). Un caso "dudoso" es el de margen pequeño.
2. `EvidenceRecorder`: un anillo de unos segundos con posiciones del balón y de los jugadores por paso (hoy **no existe**; el registro de eventos no basta). La revisión nunca mira el futuro ni inventa datos: reevalúa con las pruebas grabadas.
3. `ReviewRequest { Requester (usuario o IA), Team, DecisionId }` y `ReviewOutcome { Upheld | Overturned, nuevo veredicto, motivo }`.
4. `IReviewResolver` por tipo de decisión: reevalúa con las funciones autoritativas ya existentes (`MatchRules.HasCrossedGoalLine` para el gol; las del penal y de la roja cuando existan) con información completa.
5. `ReviewPolicy` (datos): coste en Zenit Points por tipo, ventana de tiempo para pedirla, límite por partido, modos donde está permitida.
6. **Zenit Points:** el Core no guarda saldos; define `IZenitWallet` (del anfitrión) y emite el cargo `ZenitCharged { Team, Amount, DecisionId }` **en el momento de pedir la revisión, siempre, también si la decisión se mantiene** (requisito: "siempre se cobran").
7. **IA:** un `AiReviewPolicy` dentro del `DecisionProfile` (umbral de duda, presupuesto), con `DecisionDice` para ser determinista; por dificultad.
8. **Flujo:** decisión (gol/penal/roja) → estado provisional con ventana de revisión (hoy el gol cierra el marcador y arranca la celebración de inmediato) → petición del usuario o de la IA → cargo → resolución → aplicar (anular/mantener gol y marcador, penal, roja con vuelta del jugador) con eventos `ReviewRequested` y `ReviewResolved` → reanudar con el reinicio correcto.
**Bloqueos antes de poder construirlo:** faltas con consecuencias, penales y roja decididas por un árbitro (secciones 2 y 9), un estado provisional en `MatchController`, el grabador de pruebas y la economía de Zenit Points (no existe en ningún sitio del código).

## 3. Inteligencia artificial
Tubería: `WorldState`/`WorldView` → `BasicDecisionEngine.Decide` (`Decision/BasicDecisionEngine.cs`) → `PlayerDecision` → intención de movimiento y acción → `MatchController`. Un perfil de decisión (`DecisionProfile`) por equipo, construido una vez desde la dificultad.

### Ataque
| Parte | Estado | Dónde |
|---|---|---|
| Portador | Implementado | `BallCarrierEvaluator`: candidatos `Pass`, `ForwardPass`, `ThroughPass`, `Shoot`, `Dribble`, `Protect`, `Hold` con puntuaciones por ejes (espacio, presión, línea de pase, distancia a gol). |
| Decisiones de pase | Implementado | Elección de receptor (`ReceiverScore`, `BestReceiver`), pase corto, adelantado, al espacio; no usa pared, ni centro. Defecto del pase corto (hallazgo 1) afecta a su resultado. |
| Tiro | Implementado | Candidato `Shoot` con umbral `ShootMinScore`; tiros con efecto por habilidad. Evaluación de calidad del tiro (`ShotQualityEvaluator`, solo estadística). |
| Regate | Implementado | `Dribble` con `DribbleSpace`; regates de habilidad (`SkillMove`). |
| Movimientos sin balón | Implementado | `AttackSupport`: trabajos `Support`, `Forward`, `Width`, `GetOpen`; posicionamiento por rol/formación (`Positioning/RolePositionAssigner.cs`). Sin centros ni remates de cabeza. |

### Defensa
| Parte | Estado | Dónde |
|---|---|---|
| Marcaje | **Parcial** | No hay marcaje al hombre: `DefenseRoles` elige un defensor **primario** y como mucho un **secundario**; el resto mantiene su sitio (posición base de formación). |
| Presión | Implementado | `CloseDown` con distancias/magnitudes (`DefenseDefaults`), umbral de resistencia (`PressMinStamina`). |
| Cobertura | Parcial | El secundario (`SecondaryDefender`) y el retorno a casa; sin esquemas de cobertura por zona. |
| Intercepciones | Implementado | `TryIntercept`: predicción del balón (`PredictBallPosition`), el mejor situado del equipo va; el resto no. |
| Entradas | Implementado | `Tackle`, `SlidingTackle`, `PhysicalChallenge` con riesgo máximo de falta (`ChallengeMaxRisk`). Sus faltas no tienen consecuencias (sección 2). |

### Portero (Fase 16)
Posicionamiento (`GoalkeeperPositioning`: arco gol→balón, profundidad por peligro, error de deriva), reacción y lectura (`GoalkeeperReaction`, sin ver el futuro), salidas (`GoalkeeperExit`), paradas por contacto del Core, dificultad por `DecisionProfile` con las capacidades propias del portero (`GoalkeeperSkillModel`). Reglas distintas por modo (`ArenaGoalkeeperDefaults`, `ClassicGoalkeeperDefaults`). **Límites:** el tiempo de reacción y la lectura casi no cambian el resultado (los tiros llegan en 0,2–0,3 s); la palanca dominante es el error de posición; `GoalkeeperRush` no tiene efecto físico; el contador de "paradas" de los diagnósticos cuenta también capturas que acabaron en gol (antes de la Fase 18).

### Dificultades — las cinco existen
Novato, Amateur, Profesional, Experto, Élite (`Difficulty/DefaultDifficulties.cs`). Cómo afectan hoy (`DifficultyDefinition` → `DecisionProfile`; sin multiplicar atributos y sin `if (nivel)`):
* **Reacción / retraso de observación** y **error de decisión** (probabilidad y tipo de error según presión, poco espacio y cansancio; `AiErrorModel`, `DecisionProfile.ChoosesSecond`).
* **Anticipación** (horizonte de intercepción), **posicionamiento** (deriva lenta de la posición ideal), **presión** (cuándo deja de presionar por cansancio) y **gestión de esfuerzo** (límite de esfuerzo en movimientos no esenciales, `EffortCap`).
* **Gestión del partido** (sesgo de riesgo según marcador y tiempo, Fase 15) y los parámetros del portero.
Estado medido (Fases 16–17): la escala entre niveles es monótona en celdas de niveles distintos; Élite sigue perdiendo partidos frente a Profesional. **Más de un 90 % de los errores de decisión ocurren con el jugador cansado**, así que hoy la dificultad depende mucho de la resistencia (con la mejora de la Fase 17 los niveles bajos se cansan menos). Los niveles Experto y Élite se distinguen peor que los otros. Nada de esto se ha probado con humanos.

## 4. Sistemas de jugador
| Sistema | Estado | Detalle |
|---|---|---|
| `PlayerDefinition` | Implementado | `Data/PlayerDefinition.cs`: id, nombre, dorsal, rol, 13 atributos (`PlayerAttributes`: velocidad, aceleración, resistencia, control, pase, tiro, defensa, fuerza, reacción, agilidad, definición, regate, técnica), edad, altura, peso, pie, pie débil, complexión, nacionalidad, equipo, nombre corto. |
| Estadísticas | Implementado (atributos) | Valoración global y por rol (`PlayerRatingCalculator`); perfil de acciones derivado (`ActionProfile`). **Estadísticas de partido del jugador (goles, pases, tiros…): no existen** fuera de los diagnósticos de test. |
| Roles | Implementado | 4 roles base (`PlayerRole`) y roles detallados de campo (defensa central/lateral, medios, extremos, segundo punta, delantero) con vocabularios de autoría (`Players/Authoring`); formaciones (`FormationDefinition`, 4 base) y asignación (`RolePositionAssigner`). |
| Skills | Implementado | 37 habilidades en el catálogo (`Docs/authoring/FS27_SKILLS_V1.json`), 25 efectos reales (`SkillEffect`), reglas que desbloquean acciones (`SkillResolver.CanUseAction`); 2 etiquetas aplazadas (fuera de juego y falta táctica). |
| Stamina | Implementado | Modelo continuo 0–100 (`Locomotion/LocomotionStamina.cs`); recuperación al andar 0,6 desde la Fase 17; los porteros no se cansan (99,5). Todavía por debajo del nivel "deseado" de `AI_BALANCE_TARGETS_PHASE17.md`. |
| Progresión | **No implementado** | No hay experiencia, niveles, mejora de atributos ni evolución entre partidos. |
| Personalización | Parcial (solo datos) | Creator (sección 7): descripción en lenguaje natural → ADN de fútbol y apariencia, todo como datos; sin pantalla ni modelo real. Entrenadores con compatibilidad y bonificaciones (`Coaches/`). |
| Cambio de jugador controlado | Implementado | `CycleSelection`, `AutoSelection`, `SubmitHumanInput`. |

## 5. Sistemas necesarios para móvil
### Controles (Core: implementado; Unity: no verificado)
* **Joystick:** `VirtualStickModel`, `DigitalStickEmulator`, `IntentRouter`, `NormalizedInput` (`Input/`, `Intents/`); en Unity `Input/VirtualJoystick.cs` y `KeyboardGamepadIntentSource.cs`.
* **Botones A, B y C con toque y gestos arriba/abajo:** el Core está completo (`ActionButton`, `ActionGesture`, `ActionMapper`; el significado depende de si hay balón o si es el portero). **No existe en Unity la interfaz táctil de los botones** (solo el joystick y el teclado/mando).
* **Sprint por intensidad del joystick:** implementado (`InputCurve.SprintIntensity`: sprint desde 0,7, máximo a 0,95; sin botón de sprint).
### Cámaras
Core (`Camera/`): `CameraDirector.CreateDefault` registra **TV, TV cercana y Amplia** (`FollowCameraMode`); `Player`, `Broadcast` y `Replay` existen como identificadores pero **no tienen modo**. Unity: `Cameras/CameraManager.cs`, no verificado. Sin repetición (no hay grabador).
### Menús
| Pantalla | Estado |
|---|---|
| Menú principal | **No existe** |
| Selección de equipo | **No existe** (hay datos de equipos y reparto de plantillas) |
| Selección de dificultad | **No existe** (hay 5 dificultades como datos) |
| Pausa | Parcial: `MatchController.Pause`/`Resume` en el Core; sin pantalla |
| Final de partido | Parcial: `MatchResult` y la fase `Finished`; sin pantalla ni estadísticas |
Lo único en `UI/`: lectura de depuración, barra de resistencia y ajuste de área segura.

## 6. Datos y guardado
| Elemento | Estado |
|---|---|
| Equipos | Implementado: 12 (11 oficiales y 1 de usuario), `TeamDefinition`, validación (`DataValidator`, `RosterValidator`). |
| Jugadores | Implementado: 160 con identidades ficticias (`DataLock`), importadores desde bases de autoría. |
| Plantillas | Implementado: reparto de plantillas, formaciones, sustituciones (`MatchSquad`, `FormationPlacement`). |
| Estadísticas | **No implementado** como sistema de juego (marcador y goles en `MatchState`; el resto solo en diagnósticos de test). |
| Partidas guardadas | **No implementado**: ningún guardado de progreso, ajustes ni partida. |
| Progresión | **No implementado**. |
| Importación/exportación de datos | Implementado para equipos, entrenadores, árbitros, habilidades, identidades y paquetes del Creator (JSON). |
Los datos viven en `Docs/authoring` y se leen a través de una función del anfitrión; `Assets/_Project/Data` está vacío (no hay un cargador de recursos de Unity).

## 7. Creator (solo estado)
* **Existe y es la parte más grande del Core después de las bases de jugadores:** 48 archivos, ~12 600 líneas, 459 tests.
* **Hecho (probado con `dotnet test`, fuera de Unity):** capa semántica ES/EN, compilador a parches con conflictos y deshacer, ADN de fútbol 2.0 con migraciones, motor de comportamiento, apariencia 2.0 y estilos, plan de ensamblaje, malla procedural con exportación GLB, resolutores de animación y de personalidad de movimiento, tubería del Creator, validador de contenido, paquete de runtime, contratos de proveedor de lenguaje (sin transporte real).
* **Falta (según `Docs/CREATOR_ROADMAP.md`):** verificar `Gameplay/Creator` en Unity (es lo único escrito sin ejecutar), modelo base real, cuerpo/cara/pelo/ropa, animaciones, acciones de fútbol animadas, **conectar el motor de comportamiento a la IA de partido**, calibración, transporte real del modelo de lenguaje, interfaz visual, optimización móvil.
* **No se tocó en esta fase.**

## 8. Rendimiento futuro (riesgos; no se optimizó nada)
**Medido** (`Phase19Probe_Performance`, un partido completo por modo, Profesional contra Profesional, sin diagnósticos; contenedor en la nube Xeon 2,1 GHz de 4 núcleos, .NET 8 — **no es un móvil**):
| | Arena (720 s) | Classic (960 s) |
|---|---|---|
| Velocidad frente al tiempo real | 117× | 122× |
| Paso de IA (media / peor) | 0,131 ms / 10,96 ms | 0,126 ms / 3,97 ms |
| Paso del partido (media / peor) | 0,011 ms / 2,0 ms | 0,010 ms / 3,1 ms |
| Memoria asignada por partido (IA + partido) | 0,69 MB | 0,72 MB |
| Recolecciones de basura en el partido | 0 | 0 |
| Montón gestionado / pico del proceso | 6,4 MB / 114 MB (proceso de test) | 6,6 MB / 114 MB |
| Parte del cuadro de 60 Hz usada (media) | 0,85 % | 0,82 % |
Lectura: la simulación (12 jugadores, IA completa) es muy ligera y no genera basura apreciable en el camino caliente. **Riesgos reales:**
* **Móvil sin medir.** Un móvil puede ser de 5 a 10 veces más lento por núcleo y .NET en Unity (Mono o IL2CPP) no se comporta como este .NET: hay que medir en un dispositivo. Los **picos** (hasta 11 ms en un paso de IA en Arena, probablemente arranque en frío o varios jugadores decidiendo a la vez) pueden notarse como tirones.
* **Determinismo entre plataformas** (coma flotante, IL2CPP) no verificado: las semillas y las baterías solo se han ejecutado en una máquina.
* **Número de jugadores/IA simultánea:** 12 jugadores, un intervalo de decisión de 0,15 s; escala lineal con más jugadores; no hay presupuesto por cuadro ni reparto de decisiones entre cuadros.
* **Creator y gráficos:** malla procedural y personajes ensamblados (12 con cuerpo, cara, pelo y ropa): memoria, llamadas de dibujo y triángulos sin medir (paso 11 del roadmap del Creator). **Es probablemente el mayor riesgo de rendimiento del proyecto**, no el Core.
* **Memoria:** el Core es pequeño; los datos de 160 jugadores y los paquetes del Creator se cargan enteros (sin carga diferida).
* **Capa Unity sin ejecutar:** puede ocultar costes (actualización de objetos por cuadro, copias entre Core y Unity).

## 9. Hallazgos adicionales de la auditoría
1. **Pase corto degradado** (sección 1): defecto de código, verificado por medición; ya afecta a todo el balance medido desde la Fase 15, a las comparaciones de pases completados (en la Fase 17 la tasa bajó 4–5 puntos al mejorar la resistencia) y al jugador humano. Es corto de arreglar, pero obliga a volver a medir el balance.
2. **Estadísticas de fases 16 y 17 contaminadas** por los goles falsos (corregidos en la Fase 18) y por el pase corto: volver a medir.
3. **Reglas de Classic sin consecuencias:** faltas, tarjetas, fuera de juego, penales y tiros libres existen solo como eventos, interruptores o contratos. Es el mayor hueco de reglas.
4. **Sin árbitro en ejecución.**
5. **El balón atraviesa postes y larguero.**
6. **Dos documentos describen el Creator** (uno histórico) y varios documentos de arquitectura anteriores: conviene mantener un índice.
7. **Ninguna escena, prefab, arte ni audio** en Unity; la capa Unity nunca se ha ejecutado.

## 10. Hoja de ruta recomendada: próximas 10 fases (por prioridad real)
El orden sale del estado encontrado: primero lo que invalida mediciones, luego las reglas de Classic en el orden en que dependen unas de otras, después las acciones que faltan, el VAR (que necesita lo anterior), los datos de partida y por último el cliente jugable y el balance con humanos.

| Fase | Contenido | Por qué en este lugar |
|---|---|---|
| **20** | **Pase corto + nueva línea base de medición.** Dar al `ShortPass` su calidad (`PassQuality`) y volver a medir pases completados, goles por partido y escala de dificultades con la batería rápida; actualizar `AI_BALANCE_TARGETS_PHASE17.md`. | Es un defecto pequeño que contamina todo lo medido; cualquier fase de reglas posterior se apoyaría en una base falsa. |
| **21** | **Faltas, árbitro y tarjetas (Classic).** Que las faltas detengan el juego con un reinicio de tiro libre, un árbitro en ejecución que decida amarillas y rojas (con `IssueCard` ya existente), ventaja y acumulación en el partido; estilos de árbitro como datos. | Es la base de las reglas siguientes y el mayor hueco de Classic. |
| **22** | **Tiros libres.** Falta normal, barrera, ejecución (humano y IA), y acciones de portero ante la barrera. | Depende de las faltas con consecuencias. |
| **23** | **Penales.** Detección en el área, ejecución, IA del lanzador y del portero y la **tanda de penaltis real** (hoy solo es un contador). | Reutiliza el reinicio y la ejecución del balón parado. |
| **24** | **Fuera de juego (Classic).** Detección, reinicio y las dos habilidades aplazadas. | Cierra las reglas de Classic. |
| **25** | **Centros, cabezazos, voleas y segundas jugadas.** Acciones `Cross`, `Header`, volea, centros y remates en la IA, lógica de segundo balón, y colisión con postes y larguero. | Gameplay que falta en ataque; mejor tras las reglas para medirlo con ellas. |
| **26** | **VAR y Zenit Points.** Estado provisional de decisión, grabador de pruebas, revisión por el usuario y por la IA, cobro siempre, resolución sobre gol, penal y roja. | Necesita faltas, penales y rojas decididas por un árbitro (fases 21–24). |
| **27** | **Datos de partida.** Estadísticas del partido y del jugador, pantalla final como datos, guardado y carga, y progresión básica. | Lo necesita la interfaz, el VAR (saldo) y la progresión. |
| **28** | **Cliente jugable mínimo.** Decisión del motor (visor 2D en navegador, Godot o Unity), repetición, controles táctiles A/B/C, menús principal, de selección y pausa, final de partido; verificar la capa Unity o su sustituto. | El Core está listo; hay que ver y tocar el juego. |
| **29** | **Balance integral, dificultad con humanos y rendimiento en dispositivo.** Pruebas con jugadores reales, objetivos de balance definitivos, resistencia, y mediciones de CPU, memoria y determinismo en móvil. | Solo tiene sentido con reglas cerradas y un cliente. |

Fuera de esta lista y en paralelo cuando haya assets: el Creator (modelo base, animaciones, conexión con la IA de partido, interfaz) sigue el orden de `CREATOR_ROADMAP.md`.
