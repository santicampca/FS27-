# FootballDNA 2.0

Cómo **tiende a jugar** un jugador: tendencias, preferencias y comportamientos de firma. Complementa lo que ya existe y **no lo duplica**:

| Pregunta | Dónde vive |
|---|---|
| ¿Qué *puede* hacer? | los 12 atributos (`PlayerDefinition`; el DNA **no** los guarda) |
| ¿Cuánto arriesga, cuánto crea, cuánta agresividad? | `PlayerPlayingProfile` (`RiskPreference`, `Creativity`, `Aggression`) |
| ¿Dónde y con qué rol juega? | `PlayerPlayingProfile` (zonas, roles) |
| ¿Cómo *suele* hacerlo (cuándo regatea, cuándo corta hacia dentro, cuándo ataca el área)? | **FootballDNA** |

Pruebas por reflexión garantizan que el DNA no tiene campos enteros, atributos ni nombres como `risk` o `creativity`.

## 1. Qué añade la versión 2 (`FS27.FootballDNA.v2`)

| Novedad | Detalle |
|---|---|
| **Entrada de comportamiento rica** (`BehaviorEntry`, ahora clase) | `Weight` (probabilidad/preferencia 0..1), `Priority` (0..10), `Risk`, `CooldownSeconds`, `Confidence`, `Origin` (manual/prompt/observed/derived), `Condition` |
| **Condición propia** (`BehaviorCondition`) | situaciones que exige, que prohíbe, que prefiere y umbral mínimo de puntuación: "solo corta hacia dentro si no está presionado" |
| **Valores por defecto en la definición** | cada `SignatureBehaviorDefinition` trae `DefaultPriority`, `DefaultRisk`, `DefaultCooldownSeconds`, `Forbids`, `Action`, `Style`; el jugador solo guarda lo que **cambia** (`Unset` = usar el de la definición) |
| **Secuencias** (`BehaviorSequence`) | cadenas con ventana de tiempo (`stop_go_burst`, `feint_cut_inside`, `one_two_late_arrival` como plantillas del catálogo) |
| **Confianza por tendencia** (`ParamConfidence`) | cuánto nos fiamos de un valor inferido de observaciones (ausente = 1) |
| **Semillas** en `CharacterSpecification` v2 | `GenerationSeed`, `AppearanceSeed`, `BehaviorSeed` |

Todo lo nuevo es **opcional y no se serializa si es el valor por defecto**: un comportamiento simple sigue siendo `{"id":"StopAndGo","weight":0.8}`.

## 2. Migración

`CharacterSpecification` pasa de `v1` a `v2`. Los ficheros `v1` **siguen cargando**: el `SchemaMigrator` trae registrado el paso v1→v2 (sellar la versión de la especificación y del DNA; todo lo nuevo es opcional, no hay nada que reescribir). Un esquema **más nuevo** que el que entiende el motor se rechaza, nunca se adivina. Pruebas: carga de un v1, ida y vuelta exacta de un v2 rico, v2 sin campos extra, copia de runtime sin autoría.

## 3. Validación (`FootballDNAValidator`)
Comportamiento desconocido o duplicado; peso, prioridad, riesgo, enfriamiento, confianza y umbral dentro de rango; condición **no contradictoria** (una situación no puede ser requerida y prohibida); secuencias de 2 a 6 pasos con comportamientos existentes y ventana/peso válidos; confianza solo sobre parámetros de DNA. Los comportamientos aún no ejecutables dan un aviso, no un error.

## 4. Quién lo produce y quién lo usa
- **Produce:** el compilador semántico (por prompt), `FootballDnaComposer` (roles + perfil + atributos + observaciones + preferencias manuales), `CharacterVariationGenerator` (pequeñas variaciones con semilla).
- **Usa:** `BehaviorDecisionEngine` (ver [BEHAVIOR_ENGINE](BEHAVIOR_ENGINE.md)) y `MovementPersonality` (solo presentación). **Nada del DNA toca la simulación de movimiento** ni la dificultad.

### Composición (`FootballDnaComposer`), determinista y explicable
1. Los **atributos empujan** (un jugador rápido no es por eso uno que regatea): como mucho ±0,25 sobre el 0,5.
2. El **perfil y los roles tiran** hacia lo que hace ese tipo de jugador (extremo: ancho, recorte por fuera, centros; guardián: posicionamiento defensivo...).
3. Las **observaciones tiran** hacia lo visto, en proporción a la confianza (máx. 0,85).
4. Las **preferencias manuales** fijan valores exactos y ganan siempre.
5. Se **derivan** hasta 4 comportamientos de firma si el impulso es ≥ 0,72 y los atributos los sostienen (≥ 70 % de lo que exigen); se añaden las secuencias cuyos pasos están todos presentes.
Con una `VariationSeed` se añade una variación reproducible (±0,04) solo a lo *inferido* (no a lo observado ni a lo manual). La explicación paso a paso de cada valor queda en `ComposerResult.Explanation`.

## 2. Parámetros (52)

| Grupo | Id | Tendencia |
|---|---|---|
| movement | `movement.accelerationTendency` | Likes to burst away. |
| movement | `movement.decelerationTendency` | Likes to stop sharply. |
| movement | `movement.turningTendency` | Likes to turn. |
| movement | `movement.aggression` | Attacks space with intent. |
| movement | `movement.runTiming` | Times runs carefully. |
| movement | `movement.supportMovement` | Moves to support the ball carrier. |
| movement | `movement.spaceSeeking` | Looks for open space. |
| movement | `movement.diagonal` | Prefers diagonal runs. |
| movement | `movement.delayedRuns` | Starts runs late. |
| movement | `movement.blindSideRuns` | Runs behind defenders' view. |
| dribbling | `dribbling.takeOn` | Tries to beat defenders. |
| dribbling | `dribbling.closeControl` | Keeps the ball close. |
| dribbling | `dribbling.changeOfPace` | Changes speed to beat a defender. |
| dribbling | `dribbling.stopAndGo` | Stops and restarts. |
| dribbling | `dribbling.bodyFeint` | Uses body feints. |
| dribbling | `dribbling.directionChange` | Changes direction sharply. |
| dribbling | `dribbling.insideCut` | Cuts inside. |
| dribbling | `dribbling.outsideCut` | Goes outside. |
| dribbling | `dribbling.takeOnRisk` | Accepts losing the ball when dribbling. |
| possession | `possession.holdUp` | Holds the ball with back to goal. |
| possession | `possession.shielding` | Protects the ball with the body. |
| passing | `passing.short` | Short passes. |
| passing | `passing.progressive` | Forward passes. |
| passing | `passing.throughBall` | Through balls. |
| passing | `passing.cross` | Crosses. |
| passing | `passing.safe` | Safe passes. |
| passing | `passing.risky` | Risky passes. |
| passing | `passing.oneTouch` | One-touch passing. |
| shooting | `shooting.frequency` | Shoots often. |
| shooting | `shooting.longShot` | Shoots from distance. |
| shooting | `shooting.finesse` | Placed shots. |
| shooting | `shooting.power` | Powerful shots. |
| shooting | `shooting.firstTime` | First-time shots. |
| shooting | `shooting.weakFootUsage` | Uses the weaker foot. |
| shooting | `shooting.insideBox` | Prefers finishing inside the box. |
| positioning | `positioning.boxPresence` | Stays in the box. |
| positioning | `positioning.halfSpace` | Occupies the half-spaces. |
| positioning | `positioning.width` | Stays wide. |
| positioning | `positioning.depth` | Stays high up the pitch. |
| positioning | `positioning.dropping` | Drops to receive. |
| positioning | `positioning.attackingRuns` | Makes attacking runs. |
| positioning | `positioning.defensive` | Positions to defend. |
| decision | `decision.patience` | Waits for the right moment. |
| decision | `decision.directness` | Plays direct. |
| decision | `decision.pressureResponse` | Stays composed under pressure. |
| decision | `decision.scanning` | Checks surroundings before receiving. |
| defending | `defending.pressing` | Presses the ball. |
| defending | `defending.marking` | Marks opponents. |
| defending | `defending.interception` | Intercepts passes. |
| defending | `defending.aggression` | Goes into tackles. |
| defending | `defending.retreat` | Drops back quickly. |
| defending | `defending.laneBlocking` | Blocks passing lanes. |

Añadir una tendencia nueva es **una fila** en `DefaultParameters`: funciona de inmediato en JSON, validación y modificación incremental.

## 5. Estado honesto
Los 15 comportamientos son datos con estado `Planned`: ninguno se ejecuta en partido todavía. Los pesos de composición, umbrales y valores de prioridad/riesgo/enfriamiento son **valores de partida** a calibrar con juego real.
