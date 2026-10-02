# Data Core: jugadores, equipos y formaciones (6v6)

**FS27 es 6 contra 6: cada equipo tiene 1 portero + 5 jugadores de campo = 6 jugadores.** (Las constantes viven en un solo
sitio, `DataRules`: `GoalkeepersPerTeam = 1`, `FieldPlayersPerTeam = 5`, `PlayersPerTeam = 6`.)

Capa de datos pura en `Assets/_Project/Scripts/Core/Data`. Sin UnityEngine, sin IA, sin comportamiento:
solo **qué es** un jugador, un equipo y una formación, y la validación de que son utilizables en un 6 contra 6.
Todo es aditivo: reutiliza `PlayerAttributes` y `FieldDimensions` sin modificarlos.

> **Historia:** este sistema se escribió primero para 5 contra 5 (1 portero + 4 de campo). Esa regla **ya no existe**: el diseño definitivo es 6v6.
> Además, los equipos ya no contienen jugadores sino sus **ids**; la identidad y los 12 atributos (+ `Reaction`, que pertenece a la IA/dificultad y no es uno de los 12)
> están descritos en [PLAYER_SYSTEM.md](PLAYER_SYSTEM.md).

## Modelo

```
TeamDefinition ──► PlayerIds: List<string>   (el orden = la alineación; índice 0..5)
      │                         └─(se resuelve en)─► PlayerLibrary ──► PlayerDefinition (UNA instancia lógica por id)
      │                                                                   └─► Attributes: PlayerAttributes
      └─► FormationId ──(se resuelve en)──► FormationLibrary ──► FormationDefinition
                                                                   └─► Positions: List<FormationPosition> (6)
```

El equipo **no contiene copias** de `PlayerDefinition`: solo ids. Existe una única instancia por jugador (en la `PlayerLibrary`),
lo que permite cambiar un jugador de equipo, editar plantillas, crear equipos y reutilizar jugadores sin duplicar nada.
Para resolver los ids (y los perfiles de portero) se usa `IPlayerLookup` (la `PlayerLibrary` lo implementa), por lo que `Data` no depende de `Players`.

| Tipo | Contenido |
|---|---|
| `PlayerDefinition` | `Id` estable, `Name`, `Number`, `Role`, `Attributes` e identidad/datos físicos (ver PLAYER_SYSTEM.md) |
| `PlayerRole` | `Goalkeeper`, `Defender`, `Midfielder`, `Forward` (solo dato, sin comportamiento) |
| `TeamDefinition` | `Id`, `Name`, `Colors` (`TeamColors`), `PlayerIds` (6), `FormationId`; `GoalkeeperIndex(IPlayerLookup)` |
| `FormationDefinition` | `Id`, `Name`, `Positions` |
| `FormationPosition` | `PlayerIndex` (índice en la alineación), `Role` (categoría gruesa, el portero se identifica por ella), `Zone` (`PitchZone`: Goal/Defense/Midfield/Wing/Attack), `Relative` |
| `FormationLibrary` | busca formaciones por id; `TryAdd` rechaza duplicados/ids inválidos |
| `DefaultFormations` | `2-1-2`, `1-2-2`, `2-2-1`: ejemplos de 1 portero + 5 de campo, el portero siempre en el slot 0 |
| `DataRules` | las constantes: 1 portero, 5 de campo, 6 en total, números 1..99, longitudes de id/nombre, rango relativo 0..1 |

### Formaciones por defecto (6 jugadores)

Una formación define **posición inicial, distribución espacial, zona y rol grueso**; no ata al jugador a una posición tradicional
(los jugadores siguen siendo polivalentes, y su idoneidad por zona se calcula en el Player System).

| Id | Alineación (además del portero) |
|---|---|
| `2-1-2` | 2 defensas, 1 medio, 1 extremo (zona `Wing`), 1 atacante |
| `1-2-2` | 1 defensa, 2 medios, 1 extremo (zona `Wing`), 1 atacante |
| `2-2-1` | 2 defensas, 2 medios, 1 atacante |

Un `PlayerDefinition.Attributes` se pasa tal cual a `PlayerStats.Resolve(...)`, así que un jugador definido en datos
llega a la locomoción/stamina existente sin conversiones. Los tipos son `[Serializable]` con campos públicos:
un `ScriptableObject` futuro solo tendrá que envolverlos.

## Posiciones relativas

`FormationPosition.Relative`: **X = profundidad** (0 = su propia línea de gol, 1 = la del rival), **Y = anchura**
(0..1, 0.5 = centro). No dependen del tamaño del campo ni del lado:
`ToFieldPosition(FieldDimensions, attacksPositiveX)` las convierte a espacio de campo. El equipo que ataca hacia -X es
el mismo diseño girado 180°, así que izquierda y derecha se mantienen desde su punto de vista.

## Validación (`DataValidator`)

Funciones puras que **no modifican** nada y devuelven **todos** los problemas, no solo el primero.
Cada problema es un `ValidationIssue` con `Code` (enum `ValidationCode`, lo que usan los tests), `Subject`
(p. ej. `team 'blue' > player 'blue-d1'`) y `Message` (qué está mal y qué rango se esperaba).

| Método | Comprueba |
|---|---|
| `ValidatePlayer` | id válido (1-64, sin espacios), nombre (1-32, no vacío), número 1..99, rol definido, los atributos en 1..99, identidad y datos físicos |
| `ValidateFormation` | exactamente 6 posiciones (1 portero + 5 de campo), índices 0..5 sin repetir, exactamente 1 portero, roles y zonas definidos (solo el portero en la zona `Goal`), coordenadas en 0..1 (NaN/infinito rechazados) |
| `ValidateTeam(team, players, formations?)` | exactamente 6 ids de jugador, ids válidos y sin repetir, todos existentes en la biblioteca de jugadores, cada jugador válido, exactamente 1 portero (y por tanto 5 de campo) **con `GoalkeeperProfile` válido** (y ningún jugador de campo con uno; ver [GOALKEEPER_SYSTEM.md](GOALKEEPER_SYSTEM.md)), números de camiseta sin repetir, `TeamId` del jugador coherente, `FormationId` presente. Con biblioteca de formaciones: la formación existe, es válida (6 posiciones) y su slot de portero coincide con el portero del equipo |
| `ValidateMatchTeams(home, away, players, formations?)` | ambos equipos válidos, ids de equipo distintos, ningún id de jugador compartido |

Ejemplo de uso (pseudocódigo): `var r = DataValidator.ValidateTeam(team, playerLibrary, formationLibrary); if (!r.IsValid) log(r.ToString());`

## Qué NO hay (a propósito)

IA, tácticas, comportamiento por rol, menús, torneos, carrera, transferencias, estadísticas, jugadores reales o con licencia
(los tests usan datos genéricos), adaptadores de Unity (ScriptableObjects). Y, como en todo el proyecto, **ningún botón o flag de sprint**:
el sprint sale solo de la intensidad del joystick (un test-guarda lo vigila también en estos tipos).

## Cableado futuro en Unity

`PlayerDefinitionAsset`, `TeamDefinitionAsset` y `FormationDefinitionAsset` (ScriptableObjects) envolverán estos tipos y
pasarán por `DataValidator` en `OnValidate`. `PlayerEntity` leerá sus atributos desde un `PlayerDefinition`.

## Tests

`Assets/_Project/Tests/EditMode/DataCoreTests.cs`: cada regla en su límite (equipos de 5, 6 y 7; 0, 1 y 2 porteros; formaciones de 5 y 6),
formaciones por defecto, conversión a campo, biblioteca, equipos por ids, partidos, mensajes con el id del culpable y tests-guarda
de arquitectura (Core sin Unity, tipos serializables, sin sprint). Ejecutar: `dotnet test Tests/Core.Tests`.
