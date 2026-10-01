# FS27 — Sistema de dificultad de la IA

Estado: **infraestructura implementada y probada fuera de Unity** (C# puro en `Core`). No hay todavía IA de ataque, defensa ni portero:
este sistema es la base sobre la que se construirán. Nada de esto se ha probado dentro del editor de Unity (no es necesario: no depende de Unity).
Todos los números son **valores iniciales** pensados para ajustarse jugando, no verdades medidas.

## 1. Filosofía

> **Una dificultad mayor hace que el rival juegue MEJOR, no que haga trampas.**

La dificultad cambia la **calidad de uso** de lo que un jugador ya tiene: decisiones, reacción, posicionamiento, anticipación,
presión, ejecución y coordinación. **Nunca** cambia lo que el jugador *es*: sus atributos y sus capacidades físicas.
Un jugador de nivel 87 tiene ese nivel en Novato y en Élite; en Élite lo *aprovecha* mejor.

No existen: bonus de velocidad, de estadísticas, "rubber banding", ni información que el jugador humano no tendría (la IA no conoce el futuro).

## 2. Dónde vive (mapa del código)

Todo es aditivo, en `Assets/_Project/Scripts/Core`, sin `UnityEngine`; no se modificó ningún sistema existente.

| Carpeta | Contenido |
|---|---|
| `Core/Difficulty` | `DifficultyLevel`, `DifficultyDefinition` y sus 8 grupos de parámetros, `DefaultDifficulties` (los 5 niveles), `DifficultyLibrary`, `DifficultyMetrics`, `DifficultyValidator`, `DifficultyRules` (límites humanos), `PlayerAssist` (asistencias), `ReactionModel`, `ObservationDelayBuffer`/`ReactionGate`, `AiErrorModel`, `SeededRandom` |
| `Core/Ai` | `PositioningModel`, `AnticipationModel`, `PressureModel`, `TeamStyle*` y `TeamAiProfile`, `PlayerPlayingProfile` (polivalencia), `AiSkillResolver`, `GoalkeeperSkillModel` |

Encaja con la arquitectura existente: `PlayerAttributes` (fuente de verdad de lo que un jugador puede hacer) se **lee**, nunca se escribe;
`PlayerDefinition`, `TeamDefinition` y `FormationDefinition` **no se tocan** (estilo y perfil de juego viven en tablas aparte enlazadas por id,
igual que se hizo con la apariencia); el balón se predice con `BallPredictor` ya existente. La configuración es `[Serializable]` con campos públicos,
así que un `ScriptableObject` futuro solo tendrá que envolverla.

## 3. Los cinco niveles

`DifficultyDefinition` agrupa **ocho categorías independientes** (no todas crecen igual ni a la vez):

| Cat. | Qué controla |
|---|---|
| A. Decisión | calidad de elección, nº de opciones que evalúa, cada cuánto decide, resistencia a la presión, disciplina de rol |
| B. Reacción | segundos hasta reaccionar, suelo, variación, influencia del atributo Reaction |
| C. Posicionamiento | error de posición, disciplina de zona, cobertura de líneas de pase, adaptabilidad de rol |
| D. Anticipación | calidad, horizonte máximo, campo de visión, alcance de percepción, lectura de rebotes |
| E. Presión | **intensidad** y **inteligencia** (separadas), distancia máxima, recuperación |
| F. Ejecución | frecuencia y magnitud de errores, sensibilidad a presión y fatiga |
| G. Coordinación | calidad de equipo, presionadores simultáneos, compacidad, apoyos, retardo de reorganización |
| H. Portero | reacción, posicionamiento, anticipación, decisión, tiempo de parada, lectura de tiro, rebotes |

Perfil cualitativo (lo que se *ve*): **Novato** decide simple, reacciona tarde, se posiciona mal, falla más y presiona descoordinado;
**Amateur** decide razonablemente y empieza a reconocer espacios; **Profesional** es equilibrado y consistente; **Experto** anticipa y elige mejor el pase,
aprovecha errores y cubre mejor; **Élite** lee el juego, se coloca con precisión, decide bien bajo presión y se coordina como equipo.
**Élite no significa "corre más rápido"**: no existe ningún parámetro físico en la dificultad.

### Valores iniciales (generados desde el código)

Cada fila es un parámetro; "Mejor si…" dice en qué sentido mejora al subir de nivel (`libre` = no tiene por qué crecer: es una cuestión de estilo,
p. ej. la intensidad de presión). El validador exige que los parámetros que deben mejorar nunca empeoren de un nivel al siguiente.

#### Decision
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `DecisionQuality` | 0.35 | 0.5 | 0.65 | 0.8 | 0.92 | sube |
| `OptionsConsidered` | 2 | 3 | 4 | 5 | 6 | sube |
| `DecisionIntervalSeconds` | 0.5 | 0.4 | 0.32 | 0.26 | 0.2 | baja |
| `PressureResistance` | 0.3 | 0.45 | 0.6 | 0.75 | 0.88 | sube |
| `RoleDiscipline` | 0.4 | 0.55 | 0.7 | 0.82 | 0.92 | sube |

#### Reaction
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `BaseReactionSeconds` | 0.5 | 0.4 | 0.32 | 0.26 | 0.21 | baja |
| `MinReactionSeconds` | 0.18 | 0.16 | 0.15 | 0.14 | 0.13 | baja |
| `ReactionVariance` | 0.3 | 0.25 | 0.2 | 0.15 | 0.1 | baja |
| `AttributeInfluence` | 0.4 | 0.4 | 0.4 | 0.4 | 0.4 | libre |

#### Positioning
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `PositioningQuality` | 0.3 | 0.5 | 0.65 | 0.8 | 0.92 | sube |
| `PositionErrorMeters` | 3 | 2 | 1.3 | 0.8 | 0.4 | baja |
| `ZoneDiscipline` | 0.3 | 0.5 | 0.65 | 0.8 | 0.92 | sube |
| `PassLaneCover` | 0.2 | 0.4 | 0.6 | 0.78 | 0.9 | sube |
| `RoleAdaptability` | 0.25 | 0.4 | 0.55 | 0.7 | 0.85 | sube |

#### Anticipation
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `AnticipationQuality` | 0.2 | 0.4 | 0.6 | 0.78 | 0.9 | sube |
| `MaxLookaheadSeconds` | 0.15 | 0.25 | 0.4 | 0.55 | 0.7 | sube |
| `VisionAngleDegrees` | 110 | 125 | 140 | 155 | 170 | sube |
| `PerceptionRangeMeters` | 12 | 15 | 18 | 21 | 24 | sube |
| `ReboundReading` | 0.2 | 0.4 | 0.6 | 0.78 | 0.9 | sube |

#### Pressure
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `PressureIntensity` | 0.5 | 0.5 | 0.55 | 0.6 | 0.6 | libre |
| `PressureIntelligence` | 0.2 | 0.4 | 0.6 | 0.78 | 0.92 | sube |
| `MaxPressDistanceMeters` | 10 | 10 | 10 | 10 | 10 | libre |
| `RecoveryRunQuality` | 0.3 | 0.5 | 0.65 | 0.8 | 0.9 | sube |

#### Execution
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `ExecutionQuality` | 0.35 | 0.5 | 0.65 | 0.8 | 0.9 | sube |
| `ErrorMagnitude` | 1 | 0.85 | 0.7 | 0.55 | 0.4 | baja |
| `PressureSensitivity` | 1.2 | 1 | 0.85 | 0.65 | 0.45 | baja |
| `FatigueSensitivity` | 1 | 0.9 | 0.8 | 0.7 | 0.6 | baja |

#### Coordination
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `CoordinationQuality` | 0.2 | 0.4 | 0.6 | 0.78 | 0.92 | sube |
| `MaxSimultaneousPressers` | 3 | 3 | 2 | 2 | 2 | libre |
| `LineCompactness` | 0.3 | 0.45 | 0.6 | 0.75 | 0.88 | sube |
| `SupportRunTiming` | 0.25 | 0.45 | 0.62 | 0.78 | 0.9 | sube |
| `CommunicationDelaySeconds` | 0.9 | 0.7 | 0.5 | 0.35 | 0.25 | baja |

#### Goalkeeper
| Parámetro | Novato | Amateur | Profesional | Experto | Élite | Mejor si… |
|---|---|---|---|---|---|---|
| `Goalkeeper.ReactionSeconds` | 0.45 | 0.38 | 0.31 | 0.26 | 0.22 | baja |
| `Goalkeeper.Positioning` | 0.3 | 0.5 | 0.65 | 0.8 | 0.92 | sube |
| `Goalkeeper.Anticipation` | 0.2 | 0.4 | 0.6 | 0.78 | 0.9 | sube |
| `Goalkeeper.DecisionMaking` | 0.3 | 0.5 | 0.65 | 0.8 | 0.9 | sube |
| `Goalkeeper.SaveTiming` | 0.3 | 0.5 | 0.65 | 0.8 | 0.92 | sube |
| `Goalkeeper.ShotReading` | 0.25 | 0.45 | 0.62 | 0.78 | 0.9 | sube |
| `Goalkeeper.ReboundResponse` | 0.2 | 0.4 | 0.6 | 0.78 | 0.9 | sube |


Asistencias recomendadas (pase / tiro / cambio de jugador): Novato High·High·High · Amateur High·Medium·Medium · Profesional Medium·Low·Manual · Experto Low·Low·Manual · Élite Low·Manual·Manual.

## 4. Atributos frente a dificultad (regla obligatoria)

Los atributos siguen siendo la única fuente de verdad de las capacidades de un jugador. Cómo se garantiza, **de forma estructural y no solo por convención**:

1. `DifficultyDefinition` y sus grupos **no contienen** ni atributos, ni estadísticas, ni multiplicadores de capacidades físicas o técnicas.
2. Ninguna función de dificultad/IA recibe `PlayerAttributes` por `ref`/`out` ni lo devuelve: solo `in` (lectura). `PlayerStats.Resolve(atributos, tuning)` **no tiene entrada de dificultad**.
3. La dificultad modifica **cómo se usan** las capacidades. Un jugador con Passing 75 elige mejor a quién pasar en Élite, pero sigue teniendo las limitaciones de un Passing 75
   (con la misma dificultad, un Passing 40 sigue fallando más que un Passing 90: está en los tests).
4. Tests-guarda: ningún miembro escribe atributos; ninguna clase de IA guarda `PlayerAttributes`/`PlayerStats`; ningún miembro se llama *boost/bonus/cheat/rubber/handicap/omniscient*;
   y un test ejecuta toda la tubería con los 5 niveles comprobando que atributos y estadísticas físicas quedan **idénticos** (el "87 sigue siendo 87").

## 5. Error de IA (frecuencia y magnitud controladas)

`AiErrorModel` calcula, para cada tipo de fallo, una **probabilidad** y una **magnitud**; el azar solo decide el desenlace final.

Tipos: `BadPassChoice`, `PoorControl`, `LatePress`, `PoorCover`, `OverAggressiveMove`, `RushedShot`, `MisreadTrajectory`, `LateReaction`.

```
probabilidad = tasaMáximaDelFallo
             × (1 − habilidadDeLaDificultad)^exponente        ← qué ajuste de la dificultad protege de ese fallo
             × factorDelAtributoRelevante                      ← p. ej. Passing para BadPassChoice, BallControl para PoorControl
             × (1 + sensibilidadAPresión × presión)
             × (1 + sensibilidadAFatiga × (1 − stamina))
             × factorDeDificultadDeLaAcción                    ← distancia, ángulo, tiempo
magnitud     = magnitudMáximaDelNivel × (más con presión) × factorDelAtributo
```

No es azar puro: depende de **dificultad, atributo, presión, situación y stamina**. `Resolve` consume siempre exactamente 2 muestras de un `IRandomSource` inyectado (reproducible con `SeededRandom`).
Todos los números (tasas por tipo, exponentes, pesos) están en `ErrorTuning`. Ejemplo con el tuning actual, jugador medio, sin presión → con presión 0,8 (`PoorControl`):

- Novato: reaction 0.50s, PoorControl calm 15.5% pressed 30.3%
- Amateur: reaction 0.40s, PoorControl calm 10.4% pressed 18.8%
- Profesional: reaction 0.32s, PoorControl calm 6.1% pressed 10.3%
- Experto: reaction 0.26s, PoorControl calm 2.6% pressed 4.0%
- Élite: reaction 0.21s, PoorControl calm 0.9% pressed 1.3%

Los fallos siguen existiendo en Élite (bajo presión máxima, cansada y con atributos flojos aún falla del orden del 5 %): la IA **puede** equivocarse a todos los niveles.

## 6. Reacción y límite de información

- `ReactionModel`: tiempo = base del nivel × factor del atributo **Reaction** × variación reproducible, con **suelo** del nivel y **límite humano** global (`DifficultyRules.AbsoluteMinReactionSeconds` = 0,10 s). Aunque la configuración sea absurda, el modelo no baja de ahí.
- **No se puede reaccionar antes de que exista la información.** `ObservationDelayBuffer<T>` guarda observaciones con marca de tiempo y solo devuelve muestras **anteriores o iguales** al instante consultado: la IA actúa sobre "el mundo de hace *reacción* segundos". Un test comprueba que un balón golpeado en t=1,0 s solo es "visto" por un defensor de 0,30 s de reacción a partir de t≈1,30 s.
- `ReactionGate`: "lo noté en *t*, puedo actuar en *t + reacción*"; un estímulo repetido no reinicia ni adelanta el temporizador.
- Aplica por igual a defensores, portero (`GoalkeeperParameters.ReactionSeconds`), cambios de dirección, balones divididos, rebotes, pases filtrados y tiros.

## 7. Posicionamiento

`PositioningModel` decide **cuánto se acerca la IA a la posición ideal**, no cuál es esa posición (la dará la IA táctica futura mediante `IIdealPositionProvider`):
error máximo en metros (menor en niveles altos; peor con presión y con Defense bajo), **persecución fuera de zona** (`MayChase`: un Novato sigue el balón fuera de su sitio; un Élite mantiene la zona) y eficacia al cerrar líneas de pase.
El ruido que desplaza al jugador lo aporta quien llama (reproducible).

## 8. Anticipación

`AnticipationModel` limita lo que la IA puede leer:
- **Percepción:** solo percibe lo que está dentro de su campo de visión (medido desde la orientación del cuerpo) y alcance. Fuera de eso, el horizonte es 0.
- **Horizonte:** máximo del nivel (techo duro), reducido por distancia, por dar la espalda y por un atributo Reaction bajo; nunca por encima de `AbsoluteMaxLookaheadSeconds` (1 s).
- **Predicción:** `PredictBallPosition` parte **solo del estado observado** (no del presente real ni del futuro) y le añade el error de lectura propio del nivel. Un test verifica que una observación vieja da una predicción vieja.

## 9. Presión: intensidad frente a inteligencia

Son dos botones distintos (`PressureModel`):
- **Intensidad:** cuánto está dispuesto a presionar → distancia a la que empieza (`TriggerDistance`). No tiene por qué crecer con el nivel (Novato y Amateur comparten el mismo valor) y **ningún nivel presiona siempre** (máx. 0,60).
- **Inteligencia:** con qué frecuencia elige la acción **correcta** entre `Press`, `Contain`, `Cover` y `Retreat` según la situación (¿soy el más cercano? ¿hay cobertura? ¿es peligroso? ¿ha recibido mal?).

Un defensor ingenuo carga siempre que está cerca; uno inteligente espera, contiene o cubre cuando conviene. Test: en una situación donde presionar es un error, **Élite presiona menos que Novato**; donde sí conviene, presiona casi siempre.
La probabilidad de elegir bien depende **solo** de la inteligencia (no de la intensidad ni del estilo). El **estilo** (§12) desplaza *dónde* empieza la presión.

## 10. Coordinación de equipo (solo configuración)

`CoordinationParameters`: calidad, presionadores simultáneos (más = menos organización), compacidad de líneas, sincronía de apoyos y retardo de reorganización tras perder/ganar el balón.
Se consumirá por la futura IA de equipo; aquí solo existen los datos y su ordenación validada.

## 11. Portero (solo configuración)

`GoalkeeperParameters` (reacción, posicionamiento, anticipación, toma de decisiones, tiempo de parada, lectura de tiro, respuesta al rebote) y `GoalkeeperSkillModel`, que los combina con los atributos del portero (Reaction → reflejos/lectura/tiempo; Defense → posición/decisión/rebote).
Respeta el límite humano de reacción y no modifica atributos. **No hay IA de portero**; solo la base de configuración.

## 12. Asistencias del jugador humano (separadas de la IA)

`PlayerAssistSettings`: **Pass**, **Shot** y **Player Switch**, cada una `High / Medium / Low / Manual`. Cada dificultad trae valores **recomendados**; el jugador puede fijar cada una libremente (`PlayerAssistOverrides`; lo no fijado cae en la recomendación).
`AssistTuning` traduce cada nivel a números (grados de ayuda de apuntado, radio de selección automática) para los futuros sistemas de pase/tiro/cambio.
Un test garantiza que los tipos de asistencia y los de IA **no se conocen entre sí**: cambiar una asistencia no altera ningún parámetro de IA y viceversa.

## 13. Dificultad frente a estilo

**Dificultad = qué tan bien juega. Estilo = cómo le gusta jugar.**

`TeamStyleDefinition` (id, nombre, `PressTriggerScale`, `DefensiveLineHeight`, `AttackWidth`, `Directness`, `Tempo`, `RiskTaking`) no comparte ningún parámetro con `DifficultyDefinition` (solo `Id`/`Name`) y ningún tipo de uno menciona al otro.
`TeamAiProfile { TeamId, DifficultyId, StyleId }` los combina por id (`AiProfileResolver`) sin modificar ninguno, y vive **fuera** de `TeamDefinition`.

Ejemplo (probado): dos equipos en **Profesional**, uno con estilo de presión alta y otro de contraataque → misma `DifficultyDefinition`, estilos distintos (el primero empieza a presionar más lejos).
El mismo estilo en Novato y en Élite escala igual el disparador, pero Élite elige mejor la acción.
Por ahora existe solo el estilo neutro **Equilibrado**; presión alta, contraataque, posesión, defensa profunda, juego por bandas y juego directo serán simplemente **datos nuevos** (los de los tests existen solo en tests).

## 14. Jugadores polivalentes

Un jugador no es "una posición". `PlayerPlayingProfile` (aparte de `PlayerDefinition`, enlazado por id) describe:
- **Zona principal** (`Goal`, `Defense`, `Midfield`, `Attack`, `Wing`), p. ej. *Banda*;
- **Zonas secundarias**, p. ej. *Ataque* y *Mediocampo*;
- hasta tres **arquetipos** (`Explosive`, `Creator`, `Finisher`, `Destroyer`, `Anchor`, `Engine`, `ShotStopper`, `Sweeper`; lista ampliable).

Sin perfil explícito se deriva del `PlayerRole` existente (`PlayingProfileDefaults`), así que **todo lo ya creado sigue funcionando**. `ZoneOfRelativePosition` traduce una posición de formación a zona.
`RoleExecutionModel`: **la dificultad no cambia el rol, cambia lo bien que se ejecuta**. En su zona principal, la calidad es la disciplina de rol del nivel; fuera de ella, la penalización por encaje (secundaria < principal) es menor en niveles con más adaptabilidad.
Validación: el portero usa la zona `Goal`, y solo él.

## 15. Cómo lo usará la IA (integración futura)

```
TeamAiProfile ──► DifficultyDefinition + TeamStyleDefinition
PlayerDefinition.Attributes + PlayingProfile + situación (presión, stamina)
        │
        ▼
AiSkillResolver ─► AiSkillProfile      (reacción, calidad de decisión, error de posición, horizonte…)
AiErrorModel / PositioningModel / AnticipationModel / PressureModel / GoalkeeperSkillModel
        │  (solo ven información observada con retardo: ObservationDelayBuffer)
        ▼
IA de ataque / defensa / portero / equipo (futuras) ─► PlayerIntent (el mismo contrato que el humano)
```

## 16. Dificultad dinámica (solo documentada; NO implementada)

Idea futura: el equipo ajusta su **comportamiento táctico** según el partido:
- **Perdiendo:** más presión, líneas adelantadas, más riesgo.
- **Ganando:** proteger el resultado, reducir riesgos, mantener el balón.

Diseño propuesto: un `TacticalAdjustment` (deltas de riesgo, altura de línea y disparador de presión) calculado a partir de `MatchState` (marcador y reloj, que ya existen)
y aplicado **al estilo**, nunca a atributos ni a la dificultad. Así es explicable, medible y no altera silenciosamente a ningún jugador.
Regla que se mantiene: **prohibido modificar atributos o aplicar ayudas ocultas** según el marcador.

## 17. Lo que queda para fases posteriores

IA de ataque, defensa, portero y equipo; estilos concretos (presión alta, contraataque…); ajuste táctico dinámico; cálculo y uso del *overall* de un jugador;
`ScriptableObject`s de dificultad/estilo/perfil y pantalla de ajustes; consumo real de las asistencias en pase, tiro y cambio de jugador; **ajuste fino de todos los números jugando** (los actuales son estimaciones).

## 18. Pruebas

174 tests nuevos (451 en total en `dotnet test Tests/Core.Tests`): datos y ordenación de los niveles, validación y límites humanos, reacción y buffer de información,
error de IA, posicionamiento, anticipación, presión, estilo, polivalencia, asistencias, portero y tests-guarda de arquitectura (sin escritura de atributos, sin trampas, sin sprint, sin nombres de clubes/ligas/jugadores/marcas reales).
Los tests-guarda se comprobaron con mutaciones (añadir un nombre real, un `ref PlayerAttributes` y un miembro con "sprint" hace fallar los tests).
No se ha probado nada dentro del editor de Unity: este sistema no depende de él.
