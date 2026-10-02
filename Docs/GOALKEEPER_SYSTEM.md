# FS27 — Goalkeeper System V1

Estado: **datos, rating, perfil y arquitectura implementados y probados fuera de Unity** (C# puro en `Core`, `dotnet test Tests/Core.Tests`). No se ha probado nada dentro del editor de Unity.
FS27 es **6v6**: cada equipo = 1 portero + 5 jugadores de campo. Este documento describe al portero; el jugador en general está en [PLAYER_SYSTEM.md](PLAYER_SYSTEM.md), los equipos en [DATA_CORE.md](DATA_CORE.md) y la dificultad en [DIFFICULTY_SYSTEM.md](DIFFICULTY_SYSTEM.md).

```
PlayerDefinition ──► GoalkeeperProfile ──► GoalkeeperRatingCalculator ──► PlayerCardData
 identidad + 12 atributos   8 capacidades + estilos     media de las 8 (1–99)       lee, no copia
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

## 5. Goalkeeper Rating (GRL) — regla oficial actual

```
GRL = redondeo al entero más cercano de (Reflexes + Handling + Positioning + Diving + Kicking + Distribution + Command + Recovery) / 8
```

- Un único cálculo: `GoalkeeperRatingCalculator` (clase estática, sin estado, sin ajustes). Solo recibe un `GoalkeeperProfile`, así que no puede depender de estilos, atributos generales, Overall, altura, nombre, nacionalidad, potencial ni dificultad. Un empate .5 sube (aritmética entera exacta: `(suma + 4) / 8`).
- **Cambio de esta fase:** antes era una media ponderada por estilo que mezclaba capacidades con atributos generales (con `GoalkeeperRatingTuning`, `GoalkeeperWeights`, rating y idoneidad por estilo y por área). Se **eliminó entera** por decisión oficial; no quedan pesos en el código ni en las pruebas. Los cuatro estilos siguen existiendo como perfil/identidad, **no afectan al GRL**.
- El GRL **no se almacena**: se calcula siempre desde los 8 atributos. Un GRL que venga en datos de autoría es solo una referencia que se compara (aviso `GrlDiffers`).
- Independiente del Overall: el Overall sigue usando los 12 atributos generales y no sabe de las capacidades de portero (un test comprueba que `PlayerRatingCalculator` no recibe un `GoalkeeperProfile`).

## 6. Perfiles (estilos) de portero

`GoalkeeperStyle`: `ShotStopper`, `Distributor`, `Sweeper`, `Commander`. **No son roles de jugador** (`PlayerArchetype` sigue teniendo exactamente los 12 oficiales y `PlayerPlayingProfile` no sabe de ellos).
Un portero tiene un estilo principal y puede tener secundarios.


Los estilos son identidad de portero (y, en el futuro, entrada de la IA); no hay calificación por estilo ni por área en el código.

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

`PlayerCardData` se extendió **sin copiar nada**: guarda una referencia al `GoalkeeperProfile` (y a la calculadora) y lo lee al consultarlo. Para un portero muestra `GoalkeeperRating` (la media de sus 8 capacidades), las 8 capacidades (`GetGoalkeeperCapability`), el estilo principal y los secundarios, y `HeadlineRating` (el GK Rating para un portero; el Overall para los demás). El Overall sigue disponible como referencia. `PlayerCardBuilder` encuentra el perfil en el `PlayerLibrary`. Para un jugador de campo, `IsGoalkeeper` es falso y los datos de portero valen 0/vacío.
`CardType` no se mezcla con el perfil: un portero puede ser Standard, Rare, Special, etc., y eso no cambia ningún rating (test).

## 14. Porteros reales de la fase de datos

Los 20 porteros de la base de autoría se integran en [GOALKEEPER_DATA_INTEGRATION.md](GOALKEEPER_DATA_INTEGRATION.md). Los tests antiguos usan un portero de prueba (Nico Valmar GK) que no representa a nadie real; su rating es ahora la media de sus 8 capacidades.

## 15. Qué NO está implementado todavía

Movimiento del portero, animaciones (estiradas, root motion, ragdoll, IK), paradas, física de atrapar, desvíos, colisiones, IA completa del portero, pase y saque, tiros, penaltis, UI, integración con Unity, multijugador, base de datos y online. Esta fase es **datos + rating + perfil + arquitectura + tests**.

## 16. Auditoría de preparación para datos externos (fase de arquitectura)

Auditoría hecha sobre el repositorio real, antes de tocar nada. (Esta sección describe la fase de auditoría; los 20 porteros se integraron en la fase siguiente.)

### Fuente de verdad (tabla)

| Dato | Dueño único | Quien lo lee (sin copiar) |
|---|---|---|
| Identidad, 12 atributos generales, número, físico | `PlayerDefinition` | cartas, rating, IA |
| Perfil de juego (zona, rol) | `PlayerPlayingProfile` | IA, rating de campo |
| 8 capacidades + estilos de portero | `GoalkeeperProfile` (enlazado por `PlayerId`) | `GoalkeeperRatingCalculator`, `GoalkeeperSkillModel`, `PlayerCardData` |
| Pertenencia a un equipo (roster) | **`TeamDefinition.PlayerIds`** | `RosterValidator.TeamOf` |
| Tendencias y comportamientos | `FootballDNA` (ids de catálogo) | motor de comportamiento |
| Dificultad | `DifficultyDefinition` | solo cambia cómo se *usan* las capacidades |

### GRL: cómo funcionaba y cómo funciona
Antes de la fase de datos era una media ponderada por estilo (se auditó y documentó antes de cambiarla). Ahora es la media simple de las 8 capacidades (sección 5). El GRL de una fuente externa es solo una **referencia** (`ReportedGrl`) que se compara con el calculado.

### Hallazgos
1. **`PlayerDefinition.TeamId` + `TeamDefinition.PlayerIds` = dos fuentes de verdad de la plantilla (real).** Hoy `PlayerLibrary.TryAssignToTeam` cambia el `TeamId` del jugador pero no reconstruye ningún roster (el comentario de la clase lo promete), y el validador solo detectaba el desacuerdo desde el lado del equipo. Un jugador con `TeamId = "red"` que ningún roster lista, o listado en dos equipos, pasaba sin aviso. **Decisión (mantenida en la fase de datos):** no se elimina `TeamId` (lo usan `PlayerCardData`, `PlayerLibrary.GetByTeam` y ~40 pruebas; quitarlo sería destructivo). Se declara **`TeamDefinition.PlayerIds` la única verdad** y `TeamId` una pista desnormalizada, y se añade `RosterValidator` (solo lectura) que detecta jugador en dos equipos y `TeamId` que el roster no confirma. Migración segura pendiente (fase posterior, con tu visto bueno): derivar `PlayerCardData.TeamId` del roster, deprecar `TeamId` y, al final, eliminarlo; los datos importados **no** deben traer `TeamId`.
2. **`Reaction` sigue existiendo en `PlayerAttributes`** como campo legado (13.º valor, fuera de los 12; lo leen `AnticipationModel` y `AiErrorModel`, y `DataValidator` lo valida). No forma parte de los ratings ni de las 8 capacidades, pero contradice "Reaction pertenece a la IA/dificultad". **No se cambió** (es de la infraestructura de IA ya probada); queda como riesgo documentado: retirarlo exigiría reescribir esos dos modelos.
3. **`GoalkeeperProfileSuggester`** (Creator) puntúa estilos con medias simples de deseos 0..1 para **sugerir** un perfil antes de que existan atributos; no calcula ni define ningún rating (el GRL ya no tiene fórmula por estilo, así que no hay una segunda fórmula del GRL).
4. **Un portero se reconoce de dos maneras** (`Role == Goalkeeper` y tener `GoalkeeperProfile`); `ValidateTeam` ya exige que coincidan (`GoalkeeperProfileMissing` / `GoalkeeperProfileOnFieldPlayer`). Sin cambios.
5. **Comportamientos de acción característica:** el catálogo (`DefaultBehaviors`, 15 entradas, todas `Planned`) es genérico —datos, sin clase por jugador— y sirve para porteros, pero no distinguía a quién se aplican. Se añade `BehaviorScope` (`Any` por defecto, `FieldPlayer`, `Goalkeeper`) y `BehaviorScopeRules`. **No se creó ningún comportamiento de portero** (las acciones características reales llegarán con la base de datos).
6. **Sin duplicaciones** de atributos GK en `FootballDNA`, `GoalkeeperSkillModel` (estático y sin estado), `PlayerCardData` (referencia) ni en `Difficulty`. **Sin ramas por nombre o id de jugador** en `Core` (ahora lo comprueba una prueba que lee las fuentes). La dificultad no escribe atributos, capacidades ni rating (pruebas existentes + una nueva con un portero importado).
7. **Unity:** nada de esto se ejecutó en Unity; todo es C# puro probado con `dotnet test Tests/Core.Tests`.

### Qué se añadió (solo contratos)
- `RosterValidator` (Data): `BuildIndex`, `TeamOf`, `Validate` y los códigos `RosterPlayerInMultipleTeams`, `RosterPlayerTeamIdNotInRoster`.
- `BehaviorScope` en `SignatureBehaviorDefinition` y `BehaviorScopeRules`, código `BehaviorScopeMismatch`.
- `GoalkeeperDataRecord`, `GoalkeeperRecordImporter`, `GoalkeeperRecordJson` (Goalkeepers): contrato de **autoría** para la futura base de datos.

### Qué deliberadamente NO existe todavía
Ningún comportamiento específico por portero, ninguna novena capacidad, ninguna animación o IA de portero, y `TeamId` sigue en `PlayerDefinition` (migración pendiente).
