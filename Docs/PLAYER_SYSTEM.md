# FS27 — Player System V2

Estado: **implementado y probado fuera de Unity** (C# puro en `Core`, `dotnet test Tests/Core.Tests`). No se ha probado nada dentro del editor de Unity.
**FS27 es 6v6: cada equipo = 1 portero + 5 jugadores de campo (6 en total).** Este documento describe a *un jugador*; el equipo y las formaciones están en [DATA_CORE.md](DATA_CORE.md).
Extiende lo que ya existía (`PlayerDefinition`, `PlayerAttributes`, `PlayerPlayingProfile`, `RoleExecutionModel`…); no hay un segundo sistema de jugadores.

```
PlayerDefinition ──► PlayerPlayingProfile ──► PlayerRatingCalculator ──► PlayerCardData ──► (futuro) PlayerCardView
 identidad + atributos     zonas, roles, afinidad,      overall, ratings e              solo LEE; sin copias      la UI consume
 (fuente de verdad)        comportamiento               idoneidad (0 estado propio)     ni lógica de juego        la carta
          └──────────────► Gameplay / futura IA (leen la definición y el perfil; NUNCA la carta)
```

## 1. Filosofía del jugador

FS27 usa un universo **ficticio**: sin clubes, ligas, jugadores, escudos ni datos reales o con licencia. Todo es editable por datos.
Un jugador **no es una posición** (EI, DC…). Es: **identidad + atributos + zona + roles + polivalencia + perfil de juego**.
Dos reglas mandan: **una sola fuente de verdad** para lo que un jugador *puede hacer*, y **nada derivado se guarda** (ratings y carta se calculan al consultarlos).

## 2. `PlayerDefinition` (identidad y datos básicos)

Fuente de verdad de identidad, datos básicos y atributos. Campos nuevos (todos opcionales, editables y con valores por defecto válidos):

| Campo | Notas |
|---|---|
| `Id` | único en todo el juego (la `PlayerLibrary` rechaza repetidos) |
| `Name` / `DisplayName` | `DisplayName` es otro nombre del **mismo** almacenamiento (alias, no copia) |
| `ShortName` | ≤ 12 caracteres; vacío = se deriva de `Name` (`ResolveShortName`) |
| `Number` / `ShirtNumber` | 1..99; alias, igual que arriba |
| `NationalityCode` | 2–3 mayúsculas (admite códigos ficticios) o vacío |
| `TeamId` | id del equipo o vacío (jugador libre) |
| `Age`, `HeightCm`, `WeightKg` | 15–45, 140–220, 40–140 |
| `PreferredFoot`, `WeakFootQuality` | `Left`/`Right`; 0–100 |
| `BodyType` | `Light`, `Athletic`, `Strong`, `Tall`, `Compact` |
| `Role` | el `PlayerRole` grueso de siempre (portero/defensa/medio/delantero), conservado para formaciones y compatibilidad; **no** describe el comportamiento |

`DataValidator.ValidatePlayer` valida todo esto (rangos, ids, enums) y `ValidateTeam` comprueba que el `TeamId` del jugador coincida con el equipo que lo lista.

## 3. `PlayerPlayingProfile` (perfil de juego)

Vive aparte de la definición, enlazado por `PlayerId` (como ya estaba). Contiene lo que **no** son atributos: zona principal, zonas secundarias, hasta 3 **roles con afinidad (0–100)** en **una única lista** (`Roles`: cada elemento es un `RoleAffinity` = rol + afinidad; no hay dos listas paralelas) y el **perfil de comportamiento** (`RiskPreference`, `Creativity`, `Aggression`). No guarda ningún atributo (hay un test que lo impide).
Si un jugador no tiene perfil explícito se deriva de su `PlayerRole` (`PlayingProfileDefaults`), así que todo lo anterior sigue funcionando.

## 4. Atributos

Los **12 atributos** del sistema, escala 1–99 (`PlayerAttributeId` los nombra; el valor vive en `PlayerAttributes`):

| Grupo | Atributos |
|---|---|
| Físicos | Speed, Acceleration, Agility, Strength, Stamina |
| Ataque | Shooting, Finishing, Passing |
| Control | Control, Dribbling, Technique |
| Defensa | Defense |

Cómo encaja con lo que ya existía (**decisiones de integración**, ver §21):
- **Stamina**: es el campo `Stamina` de siempre. Sigue alimentando sprint, recuperación y fatiga a través de `PlayerStats`/`StaminaSystem` (sin tocar). No existe una segunda stamina (un test lo vigila).
- **Control** = el campo existente `BallControl`. `PlayerAttributes.Control` es un alias de propiedad (una sola variable, un solo valor).
- **Reaction NO es uno de los 12 atributos.** Es un campo heredado que pertenece al sistema de IA/dificultad (`ReactionModel`, anticipación, portero), que lo lee. **No participa** en overall, rating de rol, rating de zona ni en las estadísticas de la carta (un test lo vigila).
- **Agility, Finishing, Dribbling y Technique** son nuevos (se añadieron al final de la estructura). Aún no cambian el movimiento: el movimiento no se tocó.

## 5. Zonas

`Goal`, `Defense`, `Midfield`, `Wing`, `Attack`. Un jugador tiene una `PrimaryZone` y **cualquier número** de `SecondaryZones` (sin duplicados ni repetir la principal). No son posiciones tradicionales: son sitios donde puede rendir.

## 6. Roles

Son **datos**, no código por jugador. Un jugador tiene hasta 3, cada uno con afinidad 0–100. Se conservó la terminología inglesa existente (`PlayerArchetype`); `RoleInfo` aporta el nombre en español y el grupo. **Los 12 roles oficiales:**

| Grupo | Rol (español) | `PlayerArchetype` |
|---|---|---|
| Defensa | Muro · Guardián · Ancla | `Wall` · `Guardian` · `Anchor` |
| Creación | Constructor · Creador · Arquitecto | `Builder` · `Creator` · `Architect` |
| Movilidad | Motor · Ala · Explosivo | `Engine` · `Winger` · `Explosive` |
| Ataque | Finalizador · Cazagoles · Objetivo | `Finisher` · `GoalHunter` · `Target` |

El enum `PlayerArchetype` contiene **exactamente estos 12** (un test lo comprueba). `Explosive`, `Creator`, `Finisher`, `Anchor` y `Engine` ya existían y conservan nombre y número; se añadieron `Guardian`, `Wall`, `Builder`, `Architect`, `Winger`, `GoalHunter` y `Target`.
**Migración de roles antiguos** (sin alias permanentes): `Destroyer` duplicaba a Muro/Guardián y se **eliminó** (el perfil por defecto de un defensa pasa a `Wall`). `ShotStopper` y `Sweeper` eran roles de portero: se **eliminaron** y los roles de portero se diseñarán con el futuro sistema de portero (el perfil por defecto de un portero usa `Guardian` como marcador provisional). Los números 3, 6 y 7 quedan retirados y no se reutilizan.

## 7. Polivalencia

Un jugador no es "solo extremo". La polivalencia sale de dos cosas, sin guardar nada extra:
1. **Lo que declara su perfil**: zona principal y zonas secundarias.
2. **Lo que dicen sus atributos** para cada zona (un rating por zona calculado con pesos).

Así se responde `Player + Zone → Suitability` con una API clara y **sin cuatro números manuales por jugador**. La polivalencia **no cambia los atributos**: dice cómo de bien puede usarlos en cada zona.

## 8. Zone Suitability

`PlayerRatingCalculator.GetZoneSuitability(player, profile, zone)` → **0–100**:

```
suitability(zona) = ratingDeZona(atributos) × factor
ratingDeZona      = media ponderada de los atributos con los pesos de esa zona (1..99)
factor            = 1.00 si es la zona principal · 0.97 si es secundaria · 0.80 si no la declara   (configurable)
```

`GetZoneRating(player, zone)` da solo el rating por atributos (sin perfil). Una zona incompatible rinde menos: un defensa colocado de delantero baja mucho.

## 9. Role Suitability

`GetRoleSuitability(player, profile, role)` → **0–100**:

```
suitability(rol) = ratingDeRol(atributos) × factor
factor           = lerp(0.90, 1.00, afinidad/100) si el jugador tiene el rol · 0.80 si no lo tiene   (configurable)
```

**No recibe dificultad**: es una propiedad del jugador. La dificultad solo entra en la ejecución (§17).

## 10. Overall

`GetOverall(player, profile)` → **1–99**. No es la media de los atributos: depende del perfil.

```
pesos del overall = (1 − β) × [ pesos de cada rol del jugador, ponderados por su afinidad ]  +  β × [ pesos de la zona principal ]      (β = 0.30, configurable)
overall           = media ponderada de los atributos con esos pesos
```

- Un Cazagoles y un Constructor con los mismos atributos tienen overall distinto, porque sus pesos son distintos.
- Sin roles se usa solo la zona principal; sin perfil, el derivado del `PlayerRole`.
- No depende de nombre, equipo, pie, físico, comportamiento ni dificultad (tests).
- Un jugador con todos los atributos iguales a *v* tiene overall *v* en cualquier perfil (comprobación de cordura).

Todos los pesos viven en **un solo sitio**: `RatingTuning` (editable; valores iniciales en `DefaultRatingTuning`). Para cada rol/zona, pesos sobre 100 en el orden
Speed, Acceleration, Agility, Strength, Stamina, Shooting, Finishing, Passing, Control, Dribbling, Technique, Defense (cabeceras abreviadas):

#### Pesos por rol
| Rol | Spee | Acce | Agil | Stre | Stam | Shoo | Fini | Pass | Cont | Drib | Tech | Defe |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| Guardián (`Guardian`) | 12 | 8 | 14 | 10 | 8 | 0 | 0 | 4 | 6 | 2 | 2 | 34 |
| Muro (`Wall`) | 4 | 2 | 6 | 28 | 10 | 0 | 0 | 6 | 6 | 2 | 2 | 34 |
| Ancla (`Anchor`) | 2 | 2 | 4 | 16 | 10 | 0 | 0 | 16 | 14 | 2 | 8 | 26 |
| Constructor (`Builder`) | 2 | 2 | 6 | 3 | 8 | 1 | 0 | 30 | 20 | 6 | 18 | 4 |
| Creador (`Creator`) | 4 | 4 | 8 | 2 | 4 | 4 | 4 | 20 | 14 | 14 | 20 | 2 |
| Arquitecto (`Architect`) | 2 | 2 | 6 | 0 | 4 | 4 | 4 | 26 | 16 | 10 | 26 | 0 |
| Motor (`Engine`) | 12 | 10 | 6 | 10 | 28 | 0 | 0 | 8 | 8 | 4 | 4 | 10 |
| Ala (`Winger`) | 18 | 14 | 12 | 0 | 8 | 2 | 2 | 10 | 10 | 16 | 8 | 0 |
| Explosivo (`Explosive`) | 20 | 20 | 16 | 4 | 6 | 3 | 2 | 1 | 6 | 16 | 6 | 0 |
| Finalizador (`Finisher`) | 8 | 6 | 6 | 4 | 0 | 20 | 24 | 2 | 12 | 8 | 10 | 0 |
| Cazagoles (`GoalHunter`) | 12 | 8 | 6 | 2 | 0 | 20 | 30 | 1 | 10 | 4 | 6 | 1 |
| Objetivo (`Target`) | 3 | 2 | 1 | 28 | 6 | 12 | 16 | 6 | 14 | 0 | 8 | 4 |

#### Pesos por zona
| Zona | Spee | Acce | Agil | Stre | Stam | Shoo | Fini | Pass | Cont | Drib | Tech | Defe |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| `Goal` | 3 | 3 | 22 | 10 | 4 | 0 | 0 | 8 | 12 | 0 | 8 | 30 |
| `Defense` | 6 | 4 | 8 | 15 | 6 | 0 | 0 | 6 | 5 | 2 | 3 | 45 |
| `Midfield` | 4 | 2 | 6 | 6 | 14 | 2 | 0 | 22 | 14 | 6 | 10 | 14 |
| `Attack` | 10 | 8 | 6 | 5 | 1 | 18 | 20 | 4 | 12 | 8 | 8 | 0 |
| `Wing` | 16 | 14 | 14 | 1 | 6 | 4 | 3 | 8 | 10 | 16 | 8 | 0 |

La zona `Goal` se aproxima con Defense, Agility y Control porque **aún no existen atributos específicos de portero**: los 12 atributos son el sistema base de *jugador*, y el portero tendrá un **GK System** propio en una fase posterior (no se han inventado todavía Diving, Handling, Kicking, Positioning, Reflexes, etc.).

### Ratings transparentes (tests)
- Con +10 en **Shooting**, el rating de Cazagoles sube +2,0 y el de Constructor +0,1.
- Con +10 en **Passing**, el rating de Constructor sube +3,0 y el de Cazagoles +0,1.
- Para cada rol oficial, su atributo clave mueve el rating al menos 5 veces más que uno irrelevante; subir cualquier atributo nunca baja ningún rating.

## 11. Perfil de juego

`RiskPreference` (0 conservador … 100 arriesgado), `Creativity` (0 directo … 100 creativo) y `Aggression` (0 pasivo … 100 agresivo). Son **comportamiento, no atributos**: no entran en ningún rating ni cambian lo que el jugador puede hacer. Son datos para la IA futura.

## 12. Pie dominante

`PreferredFoot` y `WeakFootQuality` (0–100) están modelados y validados. **No se tocó pase ni tiro**: la información queda lista para que pase, tiro, centro, control y orientación del cuerpo la usen después.

## 13. Perfil físico

`HeightCm`, `WeightKg` y `BodyType` (5 tipos). Son datos que el futuro sistema de presentación usará para elegir variantes de personaje; el jugador no guarda ninguna elección visual.

## 14. Player Card

`PlayerCardData` es una **vista derivada**: guarda *referencias* al `PlayerDefinition`, al perfil, al equipo y a la calculadora, y lo lee todo al consultarlo. **No copia ninguna estadística**: si un atributo pasa de 82 a 86, la carta devuelve 86 sin reconstruirse.
Muestra: nombre, nombre corto, **overall**, **zona principal**, **rol principal** (el de mayor afinidad) y etiqueta de estilo, **zonas secundarias**, los **12 atributos**, nacionalidad, dorsal, equipo (id y nombre), tipo de carta, físico, pie y **polivalencia** (suitability en las 5 zonas, de mejor a peor, indicando si es principal/secundaria/no declarada).
No tiene setters. Nada del núcleo depende de ella (test por reflexión): el flujo es siempre `datos → carta → vista`, nunca `carta → gameplay`.
`PlayerCardBuilder` la arma a partir de un jugador, el catálogo de perfiles, el catálogo de tipos y (opcional) una búsqueda de equipo.

## 15. Card Types

`Standard`, `Rare`, `Special`, `Legend`, `Event`, `Custom`. Es **solo una etiqueta de presentación** (`PlayerCardCatalog`, por defecto `Standard`) y no cambia ningún rating (test). No hay mercado, sobres, economía, monedas, tienda ni online.

## 16. Relación con `TeamDefinition`

Un equipo FS27 son **6 jugadores: 1 portero + 5 de campo**. `TeamDefinition` ya **no contiene jugadores, solo sus ids**:

```
TeamDefinition → PlayerIds[6] → PlayerLibrary → PlayerDefinition   (una sola instancia lógica por jugador)
```

- `PlayerLibrary` es el único registro (ids únicos) y la forma de resolver un id (`IPlayerLookup`).
- `TeamRoster.TryBuild` crea un equipo a partir de ids existentes; `TeamRoster.TryResolve` devuelve los jugadores de la biblioteca (las mismas instancias, nunca copias). Editar un jugador lo cambia para todos los equipos, cartas y ratings.
- Mover un jugador es cambiar su `TeamId` (`TryAssignToTeam`) y reconstruir las plantillas; el validador detecta si un equipo lista a un jugador que dice pertenecer a otro.
- `DataValidator.ValidateTeam(team, players, formations)` exige 6 ids distintos y existentes, jugadores válidos, exactamente 1 portero (y por tanto 5 de campo) y una formación de 6 posiciones. La regla de "exactamente 5 jugadores" **ya no existe**.
Esto prepara cambiar jugadores de equipo, editar plantillas, crear equipos y torneos ficticios sin duplicar jugadores.

## 17. Relación con Difficulty

La dificultad **no modifica** atributos, overall, zone/role suitability ni identidad. Solo aparece en dos métodos separados, cuyo nombre acaba en `Execution01`:
`GetRoleExecution01` = suitability × disciplina de rol del nivel, y `GetZoneExecution01` = rating de zona × `RoleExecutionModel.Quality` (existente). Ambos devuelven **0–1 y solo pueden bajar** lo que el jugador tiene.

Ejemplo (jugador de ejemplo, rol Explosivo, suitability 91): ejecución esperada Novato 36 · Amateur 50 · Profesional 64 · Experto 75 · **Élite 84**. Élite ejecuta mejor, pero **Explosivo sigue siendo 95 de afinidad y 91 de suitability**.
(Estos valores son un factor de uso para la IA, no un rating, y dependen de números iniciales por ajustar.) Un test comprueba que, ejecutando toda la tubería con las cinco dificultades, atributos, overall, suitability e identidad quedan idénticos.

## 18. Relación con la futura AI

La IA leerá `PlayerDefinition` (qué puede hacer) y `PlayerPlayingProfile` (cómo suele jugar) y usará:
`GetZoneSuitability`/`GetRoleSuitability` (dónde y como rinde), `RiskPreference`/`Creativity`/`Aggression` (cómo decide), `PreferredFoot`/`WeakFootQuality` (con qué pie), los atributos (calidad de ejecución, con `AiErrorModel`) y las funciones `*Execution01` (cuánto del rol sabe aprovechar según la dificultad).
Nada de esto implementa IA todavía.

## 19. Relación con PlayerPresentation

La presentación (futura, ver `PLAYER_PRESENTATION_ARCHITECTURE.md`) leerá `BodyType`, `HeightCm`, `WeightKg`, `ShirtNumber` y los colores del equipo; la elección concreta de piel, cabello, rostro, botas, accesorios y uniforme vivirá en su propio catálogo por `PlayerId`.
`PlayerDefinition` no guarda ningún dato visual (test) y la presentación nunca lo modifica.

## 20. Ejemplo ficticio: "Sak"

> Personaje **inventado** para pruebas. No representa a ninguna persona real. Los datos de prueba solo existen en los tests.

- **Identidad:** Sak (corto: Sak), dorsal 7, nacionalidad ficticia `ZQ`, 24 años, 176 cm, 68 kg, `Light`, pie derecho, pie débil 68.
- **Perfil:** zona principal `Wing`; secundarias `Attack` y `Midfield`; roles Explosivo 95 · Creador 84 · Finalizador 82; riesgo 75 · creatividad 82 · agresividad 55.
- **Atributos:** Speed 94 · Acceleration 96 · Agility 92 · Strength 70 · Stamina 88 · Shooting 82 · Finishing 84 · Passing 79 · Control 91 · Dribbling 94 · Technique 89 · Defense 42 (Reaction 80, heredado).

Resultados con los pesos iniciales (calculados por el código):

| | Resultado |
|---|---|
| **Overall** | **89** (la media simple de sus 12 atributos es 83,4: el perfil lo hace distinto) |
| Zonas: rating → suitability | Wing 91 → **91** · Attack 87 → **85** · Midfield 80 → **78** · Defense 65 → 52 · Goal 73 → 59 |
| Roles: rating → suitability | Explosivo 92 → **91** · Creador 87 → **85** · Finalizador 87 → **86** · Ala 91 → 73 · Constructor 85 → 68 · Muro 68 → 54 |

Son cifras de la misma familia que las del ejemplo pedido (Wing 92 · Attack 87 · Midfield 73 · Defense 42; Explosivo 91 · Creador 84 · Finalizador 82; Overall 87) pero **no idénticas**: dependen de los pesos iniciales, que son editables. No se forzaron para que coincidieran.

## 21. Decisiones tomadas y pendientes

Resueltas por la corrección estructural 6v6:
1. **Reaction** queda fuera de los 12 atributos (pertenece a IA/dificultad) y fuera de overall, ratings y carta.
2. **12 roles oficiales** (Muro, Guardián, Ancla / Constructor, Creador, Arquitecto / Motor, Ala, Explosivo / Finalizador, Cazagoles, Objetivo); `Destroyer`, `ShotStopper` y `Sweeper` eliminados, sin alias.
3. **Una sola lista** `Roles` (rol + afinidad), máximo 3 roles, afinidad 0–100.
4. **Equipos por ids**: `TeamDefinition.PlayerIds` + `PlayerLibrary`.
5. **Control** sigue llamándose `BallControl` internamente (`Control` es un alias de propiedad, sin renombrado masivo).

Pendientes:
- **Portero:** sin atributos propios; su rating se aproxima con los 12 actuales y su rol por defecto (`Guardian`) es provisional hasta el GK System.
- **`PlayerDefinition.TeamId`** coexiste con `TeamDefinition.PlayerIds` (es la pertenencia vista desde el jugador); el validador comprueba que no se contradigan. Alternativa: eliminar `TeamId` y derivarlo siempre de los equipos.
- **Pesos iniciales:** todos los pesos y factores son estimaciones por ajustar jugando.

## 22. Cambios en lo existente (mínimos y justificados)

- `PlayerAttributes`: +4 campos, alias `Control`, `GetValue`/`With`. `PlayerDefinition`: campos de identidad. `PlayerArchetype`: roles oficiales (12). `PlayerPlayingProfile`: lista única `Roles` y comportamiento.
- `DataValidator`, `PlayingProfileValidator` y los códigos de error: validan lo nuevo (los códigos se **añaden al final**).
- Corrección 6v6: `TeamDefinition` pasa a ids, `DataRules` fija 1 + 5 = 6 jugadores, las formaciones por defecto tienen 6 posiciones con zona.
- Tests existentes tocados: el helper `TestData` (ahora rellena los atributos y crea equipos de 6 con una `PlayerLibrary`) y los tests que fijaban 5 jugadores, 9 atributos o los roles antiguos.
- No se tocaron movimiento, stamina, input, balón, cámara, partido ni dificultad (datos y modelos).

## 23. Qué NO se implementó (a propósito)

Pase, tiro, IA completa, portero completo, UI, integración con Unity, modelos 3D, mercado, sobres, economía, monedas, tienda, online, cuentas, backend, jugadores/clubes/ligas reales y cualquier botón de sprint (el sprint sigue dependiendo solo de la intensidad del joystick).
