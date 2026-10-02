# FootballDNA

Cómo **tiende a jugar** un jugador: tendencias, preferencias y comportamientos de firma. Complementa lo que ya existe y **no lo duplica**:

| Pregunta | Dónde vive |
|---|---|
| ¿Qué *puede* hacer? | los 12 atributos (`PlayerDefinition`) |
| ¿Cuánto arriesga, cuánto crea, cuánta agresividad? | `PlayerPlayingProfile` (`RiskPreference`, `Creativity`, `Aggression`) |
| ¿Dónde y con qué rol juega? | `PlayerPlayingProfile` (zonas, 12 roles) |
| ¿Cómo *suele* hacerlo (cuándo regatea, cuándo corta hacia dentro, cuándo ataca el área)? | **FootballDNA** |

Tests por reflexión garantizan que el DNA no tiene campos enteros, atributos ni nombres como `risk` o `creativity`.

## 1. Datos

`FootballDNA` = `schemaVersion` + parámetros 0–1 (neutro 0,5) + lista de comportamientos con peso. Solo se guarda lo que difiere del neutro. Un DNA típico son unos cientos de bytes.

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

## 3. Comportamientos de firma (15)

`SignatureBehaviorDefinition` es **datos**, no código: no hay un `if` por comportamiento ni por jugador.

| Id | Categoría | Se necesita | Lo impulsa |
|---|---|---|---|
| `StopAndGo` | Dribbling | balón + defensor delante | `stopAndGo`, `takeOn`, `decelerationTendency` |
| `BodyFeint` | Dribbling | balón + defensor delante | `bodyFeint`, `takeOn`, `directionChange` |
| `ExplosiveExit` | Dribbling | balón + espacio delante | `changeOfPace`, `accelerationTendency` |
| `InsideCut` | Dribbling | balón + banda | `insideCut`, `halfSpace`, `directionChange` |
| `OutsideCut` | Dribbling | balón + banda | `outsideCut`, `width`, `changeOfPace` |
| `DelayedRun` | Movement | compañero con balón | `delayedRuns`, `runTiming` |
| `BlindSideRun` | Movement | compañero con balón | `blindSideRuns`, `spaceSeeking` |
| `LateBoxArrival` | Positioning | compañero con balón | `delayedRuns`, `boxPresence`, `attackingRuns` |
| `HoldUpPlay` | Possession | balón + de espaldas | `holdUp`, `shielding` |
| `FirstTimeFinish` | Shooting | recibiendo + rango de tiro | `firstTime`, `insideBox`, `frequency` |
| `LongRangeShot` | Shooting | balón + larga distancia | `longShot`, `power`, `frequency` |
| `CreativePass` | Passing | balón | `progressive`, `risky`, `directness` |
| `RiskyThroughBall` | Passing | balón | `throughBall`, `risky` |
| `OneTouchCombination` | Passing | recibiendo | `oneTouch`, `short` |
| `AggressivePress` | Defending | el rival tiene el balón | `pressing`, `defending.aggression`, `movement.aggression` |

Cada definición también lleva atributos "suficientes" (`Needs`), situaciones preferidas (`Prefers`) y etiquetas de animación que haría falta (`AnimationTags`).

**Estado de todos: `Planned`.** Nada puede ejecutarse todavía; el validador lo avisa (aviso, no error). Cuando un comportamiento pase a `Implemented`, el aviso desaparece sin más cambios.

## 4. BehaviorResolver: DNA + atributos + contexto → candidatos

Puro y determinista. Para cada comportamiento cuyo contexto obligatorio se cumple:

```
impulso    = media ponderada de sus parámetros del DNA
fuerte     = peso del comportamiento si el jugador lo declara como firma (0 si no)
preferido  = fracción de sus situaciones preferidas presentes
base       = 0,45 × impulso + 0,40 × fuerte + 0,15 × preferido
encaje     = media de min(1, atributo / "suficiente")
puntuación = base × (0,5 + 0,5 × encaje)               → 0..1, ordenada de mayor a menor
```

- El **DNA decide qué se quiere**; los **atributos, cómo de bien se puede hacer**; el **contexto, si es posible**.
- Fuera de contexto, el comportamiento ni aparece.
- No conoce nombres ni ids de jugador (un test escanea las fuentes).
- Convertir un candidato en `PlayerIntent` y ejecutarlo es trabajo de la **IA futura**; la dificultad decidirá con qué calidad. **No está implementado.**

## 5. Referencias de jugadores reales (futuro, solo autoría)

Una `ReferenceProfile` (descripción de la fuente, notas de análisis, procedencia, confianza, fecha) puede acompañar a un personaje **durante la creación**. No se copia identidad ni apariencia, no hay vídeos ni páginas y **no entra en el build** (`ForRuntime()`). El resultado es solo el DNA estructurado. Aún no existe ninguna herramienta de análisis: el contrato está listo.
