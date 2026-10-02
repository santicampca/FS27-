# FS27 — Goalkeeper System V1

Estado: **datos, rating, perfil y arquitectura implementados y probados fuera de Unity** (C# puro en `Core`, `dotnet test Tests/Core.Tests`). No se ha probado nada dentro del editor de Unity.
FS27 es **6v6**: cada equipo = 1 portero + 5 jugadores de campo. Este documento describe al portero; el jugador en general está en [PLAYER_SYSTEM.md](PLAYER_SYSTEM.md), los equipos en [DATA_CORE.md](DATA_CORE.md) y la dificultad en [DIFFICULTY_SYSTEM.md](DIFFICULTY_SYSTEM.md).

```
PlayerDefinition ──► GoalkeeperProfile ──► GoalkeeperRatingCalculator ──► PlayerCardData
 identidad + 12 atributos   8 capacidades + estilos     GK Rating (1–99)           lee, no copia
        │ (la misma definición, la misma stamina)
        └────────────► [futuro] Goalkeeper AI ► Goalkeeper Gameplay   (leen definición + perfil; NUNCA la carta)
```

## 1. Filosofía

El portero es **un jugador especial, no un sistema aparte**. Comparte con el resto identidad, equipo, número, físico, stamina, pie dominante y datos básicos, **porque es un `PlayerDefinition`**.
Añade solo lo que únicamente un portero tiene: 8 capacidades y un estilo. Dos reglas mandan:
- **No se duplica nada** del Player System (ni identidad, ni atributos, ni stamina, ni pie, ni equipo, ni biblioteca).
- **Capacidades ≠ comportamiento ≠ dificultad.** Las capacidades son datos; la IA futura las *usa*; la dificultad solo cambia *cómo de bien* las usa, nunca su valor.

## 2. Relación con `PlayerDefinition`

- Un portero es un `PlayerDefinition` con `Role = Goalkeeper`, y sigue en la `PlayerLibrary` y en `TeamDefinition.PlayerIds` por su `PlayerId`, como cualquier jugador.
- Conserva sus **12 atributos generales** (incluida su Stamina, la única que existe). Los 8 de portero **no** son los atributos 13–20 de todos los jugadores: un jugador de campo simplemente **no tiene** `GoalkeeperProfile` (no hay `Reflexes = 0`).
- `PlayerDefinition` no guarda ningún dato de portero (ni el perfil ni las capacidades): un test lo comprueba. El perfil vive aparte, enlazado por `PlayerId`, igual que el `PlayerPlayingProfile`.
- No existen `GKPlayerDefinition`, `GoalkeeperLibrary`, `GoalkeeperAttributes` ni segunda stamina (tests-guarda por reflexión).
- El rol de jugador por defecto de un portero sigue siendo `Guardian` (provisional); los 12 roles oficiales no cambian y **no** se añadió ningún rol de portero.

## 3. `GoalkeeperProfile`

`[Serializable]` con campos públicos (envoltorio futuro en ScriptableObject). Contiene:

| Campo | Contenido |
|---|---|
| `PlayerId` | el jugador al que pertenece |
| 8 capacidades | `Reflexes`, `Handling`, `Positioning`, `Diving`, `Kicking`, `Distribution`, `Command`, `Recovery` (1–99) |
| `PrimaryStyle` | estilo principal (`GoalkeeperStyle`) |
| `SecondaryStyles` | estilos secundarios (sin repetir, sin incluir el principal) |

**No** contiene Reaction (el tiempo de reacción sale de la dificultad y los Reflexes solo lo modulan), ni stamina, ni ningún atributo general.
`GoalkeeperProfileValidator` comprueba id, las 8 capacidades en 1–99, estilos definidos y sin repetir y, si se da el jugador, que el id coincida y que el jugador sea de verdad un portero. Informa de **todos** los problemas.

## 4. Las 8 capacidades

Los nombres se mantienen tal como se pidieron; cada una se define por lo que representa, no por un porcentaje de parada:

| Capacidad | Representa | Se usa hoy para |
|---|---|---|
| `Reflexes` | capacidad de respuesta | tiempo de reacción y lectura del tiro |
| `Handling` | atrapar, asegurar, controlar, reducir rebotes | respuesta al rebote |
| `Positioning` | colocarse bien (ángulo, distancia, lectura del juego) | posicionamiento y anticipación |
| `Diving` | capacidad física/técnica para intervenciones laterales | tiempo de parada |
| `Kicking` | golpeo largo | *futuro sistema de pase/saque* |
| `Distribution` | calidad de decisiones y entrega al iniciar un ataque (distinto de Kicking) | *futuro sistema de pase/saque* |
| `Command` | organizar y controlar la zona defensiva | toma de decisiones (y futura IA de equipo: línea, presión, salidas) |
| `Recovery` | levantarse y volver a estar preparado tras una acción | *futuro gameplay* |

Ninguna capacidad equivale directamente a "salvada": la futura parada dependerá de Reflexes, Diving, Positioning, Handling, distancia, velocidad y dirección del balón, tiempo disponible, dificultad y contexto. En esta fase solo está preparada la arquitectura.

## 5. Goalkeeper Rating

`GoalkeeperRatingCalculator` (1–99). **No** es una media simple: es una media ponderada cuyos pesos salen de los estilos del portero y que mezcla **capacidades de portero con unos pocos atributos generales** (Agility, Speed, Acceleration, Strength, Control, Passing, Technique, Defense). Todos los números están en un único `GoalkeeperRatingTuning` (editable; valores iniciales en `DefaultGoalkeeperTuning`); la calculadora solo los lee.

```
pesos del rating = estilo principal (70 %) + estilos secundarios (30 % repartido a partes iguales)   (si no hay secundarios: 100 % el principal)
rating           = media ponderada de las 8 capacidades y los atributos generales con esos pesos
```

- Un portero con todo igual a *N* tiene rating *N* (comprobación de cordura).
- `Stamina` y `Reaction` no pesan nada; Shooting, Finishing y Dribbling tampoco.
- **Es independiente del Overall del jugador.** El Overall sigue usando solo los 12 atributos y no sabe que existen las capacidades de portero (un test comprueba que `PlayerRatingCalculator` ni siquiera recibe un `GoalkeeperProfile`). Ejemplo válido: Overall 76 y GK 87.
- Cambiar Reflexes mueve el rating; cambiar Passing lo mueve mucho menos (nada en un Parador o un Comandante), salvo en un Distribuidor, donde sí importa.

### Pesos iniciales por estilo (sobre ~100; normalizados al usarse)

Orden de capacidades: Reflexes, Handling, Positioning, Diving, Kicking, Distribution, Command, Recovery; después los atributos generales que pesan.

| Estilo | Re | Ha | Po | Di | Ki | Ds | Co | Rec | Generales |
|---|--:|--:|--:|--:|--:|--:|--:|--:|---|
| Parador (`ShotStopper`) | 24 | 20 | 16 | 18 | 0 | 0 | 2 | 6 | Speed 1 · Accel 2 · Agility 4 · Control 1 |
| Distribuidor (`Distributor`) | 8 | 10 | 12 | 6 | 22 | 26 | 4 | 2 | Agility 2 · Passing 8 · Control 2 · Technique 6 |
| Portero líbero (`Sweeper`) | 6 | 6 | 22 | 6 | 6 | 10 | 8 | 20 | Speed 8 · Accel 8 · Agility 4 · Passing 2 |
| Comandante (`Commander`) | 8 | 16 | 20 | 4 | 2 | 8 | 28 | 4 | Agility 2 · Strength 4 · Defense 4 |

Otros factores: `PrimaryStyleShare 0.70`, factor de idoneidad de estilo primario 1.00 / secundario 0.97 / no declarado 0.85.

## 6. Perfiles (estilos) de portero

`GoalkeeperStyle`: `ShotStopper`, `Distributor`, `Sweeper`, `Commander`. **No son roles de jugador** (`PlayerArchetype` sigue teniendo exactamente los 12 oficiales y `PlayerPlayingProfile` no sabe de ellos).
Un portero tiene un estilo principal y puede tener secundarios; **no hay cuatro sistemas**: hay una calculadora y una tabla de pesos con cuatro filas.

- `GetStyleRating(...)`: lo bien que encajan sus capacidades en un estilo (1–99), declare o no ese estilo.
- `GetStyleSuitability(...)`: ese rating × el factor de declaración (0–100), igual que la idoneidad de rol del jugador de campo.

Ejemplo con el portero ficticio de abajo: Parador 89 → idoneidad 89 (principal) · Comandante 86 → 83 (secundario) · Líbero 83 → 81 (secundario) · Distribuidor 83 → 70 (no declarado).

### Áreas de actuación (comportamiento, no zonas)

El portero sigue perteneciendo a la zona `Goal`; **no se añadió ninguna zona** al sistema de jugadores (siguen siendo 5). Como concepto de *comportamiento* hay cuatro áreas, `GoalkeeperActionArea`: `GoalLine`, `Box`, `Distribution`, `SweeperArea`. `GetAreaRating(...)` dice qué área le va mejor con sus capacidades (misma estructura de pesos): para el portero de ejemplo, GoalLine 89 · Box 87 · SweeperArea 81 · Distribution 78. Es información preparada para la IA futura; nadie la usa todavía.

## 7. Relación con el Player System

- El Overall del portero se sigue calculando como el de cualquier jugador (con su perfil de juego por defecto: zona `Goal`, rol `Guardian`). Para un portero el dato importante de la carta es el **GK Rating**; el Overall queda como referencia secundaria.
- `PlayerAttributes`, los 12 atributos, los roles y las zonas **no cambiaron**. Reaction sigue fuera de los 12.

## 8. Relación con el Team System

- Un equipo es **1 portero + 5 de campo** y esa regla se mantiene. `DataValidator.ValidateTeam` ahora comprueba además:
  - el portero **tiene** `GoalkeeperProfile` (`GoalkeeperProfileMissing`);
  - ningún jugador de campo lo tiene (`GoalkeeperProfileOnFieldPlayer`);
  - el perfil del portero es válido (capacidades 1–99, estilos).
- 2 porteros o 0 porteros siguen siendo inválidos (`TeamGoalkeeperCountInvalid`).
- **`PlayerLibrary`** guarda también los perfiles (uno por portero, por `PlayerId`) y devuelve el jugador y su perfil: `TryGet`, `TryGetGoalkeeperProfile`, `TryGetGoalkeeper`. `IPlayerLookup` (lo que usa el validador) expone `TryGetGoalkeeperProfile`. `PlayerSystemValidator.ValidateAll` también exige perfil a todo portero de la biblioteca.

## 9. Relación con el Formation System

**No cambió.** El portero sigue en el slot 0, zona `Goal`; no hay formaciones especiales de portero.

## 10. Relación con Difficulty

**La dificultad no cambia ninguna capacidad, ningún atributo ni el rating.** Un portero con Reflexes 92 sigue con Reflexes 92 en Novato y en Élite; solo cambia cómo los *ejecuta*.
Se reutilizó el sistema existente (`GoalkeeperParameters` por nivel + `GoalkeeperSkillModel`), que ahora combina el valor del nivel con las capacidades del portero:

| Valor de ejecución | Nivel × … |
|---|---|
| `ReactionSeconds` | tiempo base del nivel modulado por **Reflexes** (nunca bajo el límite humano ni el mínimo configurado) |
| `Positioning` y `Anticipation` | **capacidad de posicionamiento**: Positioning (60 %) + Agility (20 %) + Speed (10 %) + Acceleration (10 %) |
| `DecisionMaking` | **Command** |
| `SaveTiming` | **Diving** |
| `ShotReading` | **Reflexes** |
| `ReboundResponse` | **Handling** |

`GoalkeeperTuning` (configurable) fija cuánto influye cada capacidad (factor 0,80 … 1,10, tope 1) y que Reflexes modula el tiempo de reacción. Kicking, Distribution y Recovery no intervienen aquí (son para el futuro sistema de distribución y de recuperación).
**Con el portero de ejemplo** (Reflexes 92), tiempo de reacción: Novato 0,37 s · Amateur 0,32 · Profesional 0,26 · Experto 0,22 · Élite 0,18; calidad de lectura de tiro: 0,27 · 0,49 · 0,67 · 0,84 · 0,97. El rating GK es 87 en los cinco niveles (tests).

## 11. Relación con la IA

Es una **extensión del AI Core, no un segundo AI Core**: no hay `GoalkeeperReactionModel`, ni error, ni posicionamiento, ni anticipación, ni buffer de observación propios del portero. La IA futura del portero usará las piezas existentes (`ReactionModel`, `AiErrorModel`, `ObservationDelayBuffer`, `PositioningModel`, `AnticipationModel`) con estos valores.
`GoalkeeperSkillModel.PositioningAbility01` es la base de la futura *GoalkeeperPositioningQuality*; la situación (balón, atacantes, ángulo de tiro) se aplicará encima más adelante. **No hay IA de portero.**

## 12. Relación con el Ball System

Preparada, no implementada. Handling quedará listo para la interacción balón–portero (atrapar, asegurar, rebotes); Diving y Reflexes para la resolución de paradas; Kicking y Distribution para el saque y el pase. No se tocó el Ball Core.

## 13. Relación con Player Card

`PlayerCardData` se extendió **sin copiar nada**: guarda una referencia al `GoalkeeperProfile` (y a la calculadora) y lo lee al consultarlo. Para un portero muestra `GoalkeeperRating`, las 8 capacidades (`GetGoalkeeperCapability`), el estilo principal y los secundarios, y `HeadlineRating` (el GK Rating para un portero; el Overall para los demás). El Overall sigue disponible como referencia. `PlayerCardBuilder` encuentra el perfil en el `PlayerLibrary`. Para un jugador de campo, `IsGoalkeeper` es falso y los datos de portero valen 0/vacío.
`CardType` no se mezcla con el perfil: un portero puede ser Standard, Rare, Special, etc., y eso no cambia ningún rating (test).

## 14. Portero de ejemplo (ficticio)

**Nico Valmar GK** (datos de prueba, solo en los tests; no representa a nadie real): dorsal 1, 192 cm, 88 kg, `Tall`, pie derecho.
- **Atributos generales:** Speed 68 · Acceleration 64 · Agility 77 · Strength 84 · Stamina 76 · Shooting 41 · Finishing 25 · Passing 73 · Control 70 · Dribbling 39 · Technique 75 · Defense 82 (Reaction 70, que no es de los 12).
- **GoalkeeperProfile:** Reflexes 92 · Handling 88 · Positioning 94 · Diving 90 · Kicking 76 · Distribution 83 · Command 79 · Recovery 86; estilo principal Parador, secundarios Comandante y Portero líbero.
- **Resultado:** Overall **76** · GK Rating **87**.

## 15. Qué NO está implementado todavía

Movimiento del portero, animaciones (estiradas, root motion, ragdoll, IK), paradas, física de atrapar, desvíos, colisiones, IA completa del portero, pase y saque, tiros, penaltis, UI, integración con Unity, multijugador, base de datos y online. Esta fase es **datos + rating + perfil + arquitectura + tests**.
