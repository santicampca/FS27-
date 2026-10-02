# FS27 — Sistema de entrenadores V1

> **Estado: implementado y probado fuera de Unity** (`CoachSystemTests`, 24 tests; suite completa verde). Sin push, sin PR. No se tocó ningún jugador existente, ningún atributo, rol, zona, el cálculo del Overall ni el GRL de porteros.

## 1. Idea

Un entrenador **no cambia las estadísticas** del jugador. Cambia (1) qué estructuras tácticas tiene disponibles el equipo, (2) cómo juega (mentalidad, construcción, presión, transiciones) y (3) el **GRL efectivo** mediante un **Coach Boost** de +1 para los jugadores que encajan con su modelo. El boost es una **capa contextual** que se calcula al consultar; nunca se guarda.

```
BaseOverall + CoachBoost = EffectiveOverall        (CoachBoost ∈ {0, +1}, nunca negativo)
```

## 2. Arquitectura (`Assets/_Project/Scripts/Core/Coaches/`, C# puro, sin nombres propios)

| Pieza | Qué es |
|---|---|
| `CoachDefinition` | Identidad (`CoachId`, `Name`, `FullName`, `ShortName`, `NationalityCode`), `Grade`, ocho valoraciones de apoyo (0-100; en C# `AttackRating` y `DefenseRating` para no parecer una copia de un atributo), filosofía, identidad táctica, `CompatibilityProfile`, `AvailableFormations`, notas de autoría. `PreferredRoles` y `PreferredZones` se derivan del perfil (peso ≥ 70), sin duplicar datos. |
| `CoachGrade` / `CoachGradeInfo` | Escala oficial **A+ A A- B+ B B- C+ C** (no hay S ni D). El grado es una valoración **global**, no un promedio, y no determina el estilo. Número de formaciones: A+/A/A- = 3 · B+/B/B- = 2 · C+/C = 1. |
| `CoachFormationProfile` | Una variante táctica de una formación base: id, coach, `BaseFormation`, nombre, propósito, `BackLineType`, 5 slots (zona + rol oficial), mentalidad, construcción, presión, transición, ancho/ritmo/riesgo/línea defensiva (0-100) e instrucciones. |
| `CoachCompatibilityProfile` | Preferencias como DATOS: roles, zonas, pesos de los 12 atributos, riesgo/creatividad/agresividad (objetivo + importancia), tipos de cuerpo, e importancia de cada parte. |
| `CoachBoostRules` | Reglas configurables: umbral 80, boost +1, máximo +1 (más la escala de atributos y el umbral de "criterio que encaja"). Un boost negativo no es una regla válida. |
| `CoachCompatibilityCalculator` | `Evaluate(coach, PlayerDefinition, PlayerPlayingProfile)` → `CoachCompatibilityResult` (puntuación 0-100, `CoachBoost`, `MatchedCriteria`, componentes). También `GetEffectiveRating` y `ApplyToRating` (para otros ratings, p. ej. el GRL de un portero). |
| `EffectiveRating` | `BaseOverall`, `CoachBoost`, `EffectiveOverall` (tope 99). Una struct de solo lectura. |
| `PlayerCardCoachView` | Vista **del lado de la card** (como `CharacterCardView`): envuelve el `PlayerCardData` (que no cambia) y añade base/boost/efectivo; `PlayerCardCoachView.Create(card, calculador, entrenador, jugador, perfil)`. La card nunca conoce al entrenador y el gameplay nunca lee esta vista. |
| `CoachLibrary`, `CoachValidator`, `CoachJson` | Búsqueda por id, validación con códigos propios y lectura/escritura estricta en JSON (`FS27.Coaches.v1`). |

Se reutilizó: `FormationDefinition/Library` (+ `DataValidator.ValidateFormation`), `PlayerArchetype` (12 roles), `PitchZone`, `AttributeWeights`, `PlayerRatingCalculator`, `TeamStyleDefinition` (cada variante se lee como un estilo existente con `ToTeamStyle()`), `DataRules` (6 jugadores, 1 portero, 5 de campo) y `MiniJson`. **Único cambio en código existente:** `DefaultFormations.ThreeOneOne` / `CreateThreeOneOne()` / `CreateExtended()` (la estructura de 3 atrás); `CreateAll()` sigue devolviendo las tres formaciones de siempre.

## 3. Formaciones

- Una formación base (`2-1-2`, `1-2-2`, `2-2-1`, `3-1-1`) da la geometría; el entrenador define **cómo se interpreta**: los 5 slots siguen el orden de las posiciones de campo de la base. Los slots defensivos siguen siendo `Defense`; los demás pueden reinterpretarse entre `Midfield`, `Wing` y `Attack`. Nunca hay una zona nueva (no `Goal` en un slot de campo) ni un rol nuevo.
- **Línea de 3** = capacidad configurable (`BackLineType.Three`, formación base `3-1-1`), no una posición nueva. Se da solo a quien lo justifica la investigación: **Simeone, Mourinho, Allegri, Tuchel, Moriyasu y Van Gaal**. Los otros 14, incluidos otros A+, no la tienen (por ejemplo Luis Enrique: sin evidencia). `BackLineType` también admite `One` (para `1-2-2`) porque el motor tiene esa formación.
- El validador exige: formación base existente; 5 slots; defensas = línea declarada = defensas de la base; zonas y roles válidos; dials 0-100; que la variante construya una formación jugable (1 portero + 5 de campo); y el número de formaciones que pide el grado.

## 4. Compatibilidad entrenador-jugador

Cada parte vale 0-100 y la puntuación es la media ponderada por las importancias del perfil (una parte sin datos o con importancia 0 se omite):

- **Roles:** el mejor `peso del rol × afinidad del jugador` entre sus roles.
- **Zonas:** el mejor peso de su zona primaria (100 %) o secundaria (80 %).
- **Atributos:** media ponderada de los 12 atributos, escalada entre un suelo (62) y un techo (92) configurables.
- **Comportamiento:** cercanía de riesgo / creatividad / agresividad al objetivo del entrenador, ponderada.
- **Cuerpo:** 100 si coincide con los preferidos, 55 si no.

`MatchedCriteria` lista lo que encaja (`Role:Architect`, `Zone:Midfield`, `Attribute:Passing(91)`, `Behaviour:Creativity`...) para depurar. **Ningún cálculo usa nombres** (un test escanea el código en busca de los 20 nombres y de ramas por nombre o id).

## 5. Coach Boost

| Puntuación | Boost |
|---|---|
| 80-100 | +1 GRL |
| 0-79 | +0 |

Sin penalización: un jugador que no encaja conserva su GRL. Umbral, boost y máximo son configurables (`CoachBoostRules`). Ejemplo (test): base 84, compatibilidad 85 → +1 → 85; con otro entrenador, compatibilidad 70 → +0 → 84; cambiar de entrenador no toca el base. Con la biblioteca real, el mismo jugador puede recibir +1 de un entrenador y +0 de otro: no hay entrenadores "mejores", hay modelos a los que se ajusta. Entre 9 y 42 de los 136 jugadores de campo reciben boost según el entrenador (ninguno recibe a más de la mitad ni a nadie por debajo de 1).

## 6. Porteros

La compatibilidad se calcula igual desde `PlayerDefinition` + perfil; el boost se aplica al GRL con `ApplyToRating(grl, resultado)` sin tocar `GoalkeeperRatingCalculator` (+1 como máximo, nunca por debajo del GRL).

## 7. Datos

Los 20 entrenadores viven en `Docs/authoring/FS27_COACHES_V1.json` (53 formaciones: 14 entrenadores × 3 + 5 × 2 + 1), explicados en `Docs/authoring/FS27_COACHES_V1.md` con notas **FACT / INFERENCE / AUTHORING**. Cambiar un entrenador o añadir uno es un cambio de datos. Un test comprueba que el archivo es exactamente lo que escribe `CoachJson`.

## 8. Validaciones y pruebas (`CoachSystemTests`)

Escala (solo las 8 letras), formaciones por grado, reglas del boost (umbral, máximo, nunca negativo), caso de 84 → 85 / 84, jugador que no encaja, zona secundaria, puntuación data-driven con partes omitidas, los 20 entrenadores válidos y únicos, formaciones realmente distintas (53 formas distintas), línea de 3 solo donde está justificada, formaciones jugables 6v6 con zonas y roles oficiales, formaciones actuales intactas, validador con 13 casos de error, jugadores reales (nadie pierde rating, boost 0/+1, base intacta tras evaluar a los 20), contexto (mismo jugador, distinto entrenador), card sin número de entrenador, portero, estilo de equipo, JSON y ausencia de nombres.

## 9. Deuda y avisos

- Los grados, las valoraciones y los números de compatibilidad son **juicio de autoría**; los hechos están en las notas. La compatibilidad está calibrada para que cada entrenador favorezca a un grupo distinto, no contra datos de partidos.
- Entrenadores con perfiles cercanos (Guardiola / Fàbregas, Flick / Luis Enrique) comparten jugadores boosteados; difieren en estilo y formaciones.
- Yaya Touré: poca evidencia táctica propia; su formación y su perfil son un modelo de AUTORÍA (C+).
- El boost todavía **no se conecta a Unity ni a la IA de partido**: esta fase es el contrato de datos y el cálculo.
