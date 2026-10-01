# Data Core: jugadores, equipos y formaciones (5v5)

Capa de datos pura en `Assets/_Project/Scripts/Core/Data`. Sin UnityEngine, sin IA, sin comportamiento:
solo **qué es** un jugador, un equipo y una formación, y la validación de que son utilizables en un 5 contra 5.
Todo es aditivo: reutiliza `PlayerAttributes` y `FieldDimensions` sin modificarlos.

> **Actualizado por Player System V2:** `PlayerAttributes` pasó de 9 a 12 atributos + `Reaction` heredado y `PlayerDefinition` ganó identidad y datos físicos. Ver [PLAYER_SYSTEM.md](PLAYER_SYSTEM.md). Donde este documento dice "los 9 atributos", léase "los atributos (12 + Reaction)".

## Modelo

```
TeamDefinition ──► Players: List<PlayerDefinition>   (el orden = la alineación; índice 0..4)
      │                         └─► Attributes: PlayerAttributes (los 9 atributos 1..99, ya existentes)
      └─► FormationId ──(se resuelve en)──► FormationLibrary ──► FormationDefinition
                                                                   └─► Positions: List<FormationPosition> (5)
```

| Tipo | Contenido |
|---|---|
| `PlayerDefinition` | `Id` estable, `Name`, `Number`, `Role`, `Attributes` (speed, acceleration, stamina, ballControl, passing, shooting, defense, strength, reaction) |
| `PlayerRole` | `Goalkeeper`, `Defender`, `Midfielder`, `Forward` (solo dato, sin comportamiento) |
| `TeamDefinition` | `Id`, `Name`, `Colors` (`TeamColors`: primario/secundario en `ColorRgb`), `Players`, `FormationId` |
| `FormationDefinition` | `Id`, `Name`, `Positions` |
| `FormationPosition` | `PlayerIndex` (índice en la alineación), `Role`, `Relative` |
| `FormationLibrary` | busca formaciones por id; `TryAdd` rechaza duplicados/ids inválidos |
| `DefaultFormations` | `2-2`, `1-2-1` (rombo), `2-1-1`: ejemplos, el portero siempre en el slot 0 |
| `DataRules` | las constantes: 5 jugadores, 1 portero, números 1..99, longitudes de id/nombre, rango relativo 0..1 |

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
| `ValidatePlayer` | id válido (1-64, sin espacios), nombre (1-32, no vacío), número 1..99, rol definido, los 9 atributos en 1..99 |
| `ValidateFormation` | exactamente 5 posiciones, índices 0..4 sin repetir, exactamente 1 portero, roles definidos, coordenadas en 0..1 (NaN/infinito rechazados) |
| `ValidateTeam(team, library?)` | exactamente 5 jugadores, exactamente 1 portero, ids y números de camiseta sin repetir, cada jugador válido, `FormationId` presente. Con biblioteca: la formación existe, es válida y su slot de portero coincide con el portero del equipo |
| `ValidateMatchTeams(home, away, library?)` | ambos equipos válidos, ids de equipo distintos, ningún id de jugador compartido |

Ejemplo de uso (pseudocódigo): `var r = DataValidator.ValidateTeam(team, library); if (!r.IsValid) log(r.ToString());`

## Qué NO hay (a propósito)

IA, tácticas, comportamiento por rol, menús, torneos, carrera, transferencias, estadísticas, jugadores reales o con licencia
(los tests usan datos genéricos), adaptadores de Unity (ScriptableObjects). Y, como en todo el proyecto, **ningún botón o flag de sprint**:
el sprint sale solo de la intensidad del joystick (un test-guarda lo vigila también en estos tipos).

## Cableado futuro en Unity

`PlayerDefinitionAsset`, `TeamDefinitionAsset` y `FormationDefinitionAsset` (ScriptableObjects) envolverán estos tipos y
pasarán por `DataValidator` en `OnValidate`. `PlayerEntity` leerá sus atributos desde un `PlayerDefinition`.

## Tests

`Assets/_Project/Tests/EditMode/DataCoreTests.cs` (153 casos): cada regla en su límite (valores válidos e inválidos),
formaciones por defecto, conversión a campo, biblioteca, equipos, partidos, mensajes con el id del culpable y tests-guarda
de arquitectura (Core sin Unity, tipos serializables, sin sprint). Ejecutar: `dotnet test Tests/Core.Tests`.
