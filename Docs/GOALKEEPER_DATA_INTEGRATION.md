# FS27 — Integración futura de la base de datos de porteros

Estado: **solo contratos y validación** (C# puro, probado con `dotnet test Tests/Core.Tests`; nada probado en Unity). **No hay ningún portero importado.** Este documento explica cómo se recibirá la base de datos investigada y qué decisiones quedan abiertas.

## Cadena

```
investigación externa ─► GoalkeeperDataRecord (autoría) ─► GoalkeeperRecordImporter ─► PlayerDefinition + GoalkeeperProfile + ids de comportamiento
                                                                                          │
                       PlayerLibrary / TeamDefinition.PlayerIds ◄─────────────────────────┘ ─► GoalkeeperRatingCalculator ─► PlayerCardData
```

No hay un segundo tipo de jugador: el record **no es** un jugador, es la entrada de un importador. Lo que sobrevive es un `PlayerDefinition` normal y un `GoalkeeperProfile`.

## Autoría frente a runtime

| Campo del record | Destino |
|---|---|
| `Id`, `DisplayName` | `PlayerDefinition.Id` / `Name` |
| 8 `Capabilities` (1–99) | `GoalkeeperProfile` |
| `PrimaryStyle`, `SecondaryStyles` | `GoalkeeperProfile` (solo los 4 estilos de portero; "Wall", "Guardian"… se rechazan) |
| `General` (12 atributos) | `PlayerDefinition.Attributes` (opcional en el record, **obligatorio** para ser jugable) |
| `SignatureActions` | ids del catálogo de comportamientos, por **datos** (mapa externo → id); lo que no coincide se lista como `ActionUnmapped`, nunca se inventa |
| `ReportedGrl` | **solo comparación**: se calcula el GRL del juego y, si difiere más de la tolerancia, aviso `GrlDiffers`. No se guarda ni sustituye al calculado |
| `Identity`, `Strengths`, `Weaknesses`, `Notes`, `Sources` | se quedan en autoría; no llegan a ningún tipo de runtime (una prueba lo comprueba) |

El importador **nunca rellena**: una capacidad ausente es error, un atributo general ausente es aviso `GeneralMissing` (no jugable, sin GRL calculado), una acción desconocida se lista. El número de camiseta queda a 0 a propósito (la fuente no lo da) y no es válido hasta el paso de plantillas.

## Formato

`GoalkeeperRecordJson` (`FS27.GoalkeeperRecord.v1`) lee y escribe el record de forma estricta (números enteros, tipos exactos, errores con motivo). Es **un** lector posible: el núcleo no depende de él, así que otro formato (CSV, hoja de cálculo) solo necesita producir `GoalkeeperDataRecord`.

## Validación disponible

Ya cubierta por el sistema existente: 1 portero + 5 de campo (6 en total), portero válido con perfil, capacidades 1–99, perfiles de estilo válidos, ids de jugador únicos (`PlayerLibrary.TryAdd`), números únicos por equipo, datos del jugador. Nuevo: `RosterValidator` (jugador en dos equipos; `TeamId` no confirmado por el roster) y `BehaviorScopeRules` (acciones de portero fuera de jugadores de campo y al revés).

## Decisiones que necesito de ti antes de la fase de importación

1. **GRL externo vs. GRL del juego.** El juego calcula el GRL a partir de capacidades y de algunos atributos generales (Agility, Speed, Passing, Technique…); la base trae GRL y solo 8 capacidades. Opciones: (a) ella aporta también los 12 generales; (b) los 12 generales se derivan de otra fuente en una fase explícita (no se inventan aquí); (c) el GRL externo se usa solo para calibrar. Hoy el contrato soporta (a) y (c) y avisa de las diferencias.
2. **Acciones características.** Hay que decidir qué comportamientos genéricos de portero se crean en el catálogo (con `Scope = Goalkeeper`) y el mapa nombre externo → id. Hasta entonces las acciones llegan como `ActionUnmapped`.
3. **`PlayerDefinition.TeamId`.** Migración propuesta en [GOALKEEPER_SYSTEM.md](GOALKEEPER_SYSTEM.md) §16; los datos importados no deben traerlo.
4. **Tolerancia del aviso de GRL** (`DefaultGrlTolerance = 5`, valor de partida sin calibrar).

## Qué NO existe todavía

Ningún portero real o de ejemplo, ningún comportamiento específico de portero, ninguna animación, ninguna base de datos completa, ninguna lectura de internet ni de LLM, ni conexión del importador con una carga masiva de la `PlayerLibrary`.

## Rendimiento

El record se descarta tras importar; el runtime guarda solo ids, 8 enteros, estilos y un `PlayerDefinition`. Cientos o miles de porteros no cargan texto de investigación.
