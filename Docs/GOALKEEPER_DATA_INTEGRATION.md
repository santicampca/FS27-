# FS27 — Integración de la base de datos de 20 porteros

Estado: **datos, importación, validación y GRL implementados y probados fuera de Unity** (C# puro, `dotnet test Tests/Core.Tests`). No se ha abierto Unity ni se ha tocado gameplay. No hay física de paradas, animaciones ni controlador de portero.

## 1. Fuente de autoría

`Docs/authoring/FS27_GOALKEEPER_DATABASE_V2.md` es la fuente de autoría de los 20 porteros. Se generó a partir del documento Word entregado (`FS27p.docx`) con una conversión mecánica del texto (cabeceras, tabla de atributos y listas); **ningún dato se reconstruyó de memoria ni se modificó**. Es texto de autoría: el runtime nunca lo lee.

## 2. Cadena e importación

```
FS27_GOALKEEPER_DATABASE_V2.md  (AUTORÍA)
   │  GoalkeeperDatabaseMarkdown.Parse(texto)        lector de autoría: solo convierte texto a registros
   ▼
GoalkeeperDataRecord                                 contrato de autoría (el único; reutilizado, no hay un segundo)
   │  GoalkeeperRecordImporter.Import                validación + conversión, sin inventar
   ▼
─────────────────────────── frontera autoría | runtime ───────────────────────────
   ▼
PlayerDefinition  +  GoalkeeperProfile  +  FootballDNA (ids de acciones)      un único PlayerLibrary
   ▼
GoalkeeperRatingCalculator.Calculate  →  GRL        PlayerCardData lee de las dos fuentes
                                                    GoalkeeperSkillModel recibe el perfil
```

`GoalkeeperLibraryBuilder.Import` hace el lote: cada portero completo entra en el `PlayerLibrary` (jugador + perfil), un `PlayerId` repetido se rechaza con `DuplicateId`, y **todos** los resultados (errores y avisos) se devuelven. Notas, fuentes, fortalezas, debilidades y texto de identidad se quedan en el record: no llegan a ningún tipo de runtime (una prueba lo comprueba). No hay clase "GoalkeeperPlayer": son `PlayerDefinition` normales con un `GoalkeeperProfile` aparte.

Esquema del record (`FS27.GoalkeeperRecord.v1`, también legible como JSON estricto con `GoalkeeperRecordJson`): `Id`, `DisplayName`, `Nationality`, `HeightCm`, `PreferredFoot`, `ReportedGrl` (referencia), 8 `Capabilities`, `PrimaryStyle`, `SecondaryStyles`, `General` (12 atributos, opcional), `SignatureActions`, y solo de autoría: `Identity`, `Strengths`, `Weaknesses`, `Notes`, `Sources`. El `Id` es `gk-` + el nombre en ASCII (p. ej. `gk-mike-maignan`).

## 3. GRL

`GRL = media aritmética de los 8 atributos GK, redondeada al entero más cercano (un empate .5 sube)`. Un único cálculo (`GoalkeeperRatingCalculator`, estático, solo recibe el perfil). No usa pesos, perfiles, estilos, altura, Overall, dificultad, reputación, nombre, nacionalidad ni potencial. **Se eliminó** la fórmula anterior (media ponderada por estilo con atributos generales, `GoalkeeperRatingTuning`, rating/idoneidad por estilo y por área). El GRL de autoría **no se guarda**: el sistema lo recalcula y solo lo compara.

## 4. Perfiles

Exactamente cuatro: Shot Stopper, Distributor, Sweeper, Commander. No afectan al GRL. El perfil principal inválido es un **error** (el portero no se importa). Una etiqueta secundaria/terciaria que no es perfil se **deja fuera y se avisa** (`StyleDropped`).

## 5. Signature Actions

Son ids de comportamiento, no atributos ni perfiles. `GoalkeeperBehaviors.CreateCatalog()` registra 12 comportamientos **genéricos** (`GoalkeeperReflexSave`, `…DivingSave`, `…OneOnOneStopper`, `…PenaltyStopper`, `…CloseShotReaction`, `…AggressiveClaim`, `…QuickDistribution`, `…PressureBreakPass`, `…LongBallSpecialist`, `…SweeperRun`, `…VocalLeader`, `…CommandBackline`) con `Scope = Goalkeeper`, estado `Planned` (no ejecutables), y un mapa de nombres externos → id. Los 20 porteros comparten los 12 (ninguno es exclusivo de un jugador). Cada portero importado obtiene un `FootballDNA` que solo lista esos ids (peso 1); no se inventa ningún otro dato de DNA. Una acción sin comportamiento se avisa (`ActionUnmapped`) y no se crea nada para ella.

Importante: este catálogo **no está fusionado** con el de jugadores de campo. Hace falta antes definir los contextos de portero (tiro entrante, centro, pase atrás…) en `BehaviorContext`. Mientras tanto el catálogo de campo sigue idéntico (15 comportamientos, `Scope = Any`).

## 6. Validación del importador

Errores (el portero no se importa): record nulo, `Id`/nombre inválidos, atributo GK ausente, desconocido o fuera de 1–99, perfil principal inválido, estilo repetido, atributo general desconocido o fuera de rango, GRL de autoría fuera de rango, acción con ámbito de jugador de campo, `PlayerDefinition` resultante inválido, `PlayerId` duplicado en el lote. Avisos: `GrlDiffers`, `StyleDropped`, `ActionUnmapped`, `GeneralDefaulted`, `NumberDefaulted`, `NationalityUnknown`, `FootUnclear`. Roster: `RosterValidator` (jugador en dos equipos, `TeamId` que el roster no confirma) + `DataValidator.ValidateTeam` (1 portero + 5 de campo, números únicos, `TeamPlayerTeamIdMismatch`).

## 7. Roster

`TeamDefinition.PlayerIds` es la autoridad. `PlayerDefinition.TeamId` es una pista desnormalizada (comentado en el código): los datos importados la dejan vacía y el validador detecta cualquier contradicción. **Deuda:** `PlayerCardData.TeamId` y `PlayerLibrary.GetByTeam` aún la leen; la migración (derivar del roster y eliminar `TeamId`) queda para una fase propia.

## 8. Relaciones

- **`PlayerDefinition`:** identidad, altura, pie, nacionalidad y atributos generales. No contiene datos de portero.
- **`GoalkeeperProfile`:** solo las 8 capacidades y los estilos.
- **`GoalkeeperSkillModel`:** sin estado; recibe perfil + dificultad; una prueba lo ejecuta con los 20 porteros en los 5 niveles sin cambiar atributos ni GRL. Sin nombres hardcodeados (una prueba lee las fuentes).
- **`FootballDNA`:** tendencias y comportamientos; los atributos GK no se duplican en él.
- **`PlayerCardData`:** lee jugador + perfil (no copia nada) y muestra nombre, GRL, 8 atributos, perfiles, nacionalidad, equipo.
- **Dificultad:** no cambia atributos ni GRL (`DifficultyDefinition` no se modificó).

## 9. Hallazgos honestos sobre la base de autoría (no se editó el archivo)

1. **10 de los 20 `Target GRL` no coinciden con la media redondeada de sus propios 8 atributos**, aunque el archivo marca `Match: YES`. El importador los avisa (`GrlDiffers`) y el juego usa siempre la media. Tabla (autoría → media de las 8): Maignan 81→82 · Pickford 83→84 · Sommer 81→82 · Oblak 82→83 · Donnarumma 83→82 · Neuer 84→86 · Kobel 83→82 · Onana 79→81 · Alisson 84→85 · Ramsdale 80→81. Además algunas sumas/promedios escritos en la sección "GRL Verification" son aritméticamente incorrectos (p. ej. Maignan: la tabla suma 656, no 646); esa sección se ignora por ser derivada. Decisión tuya: corregir los objetivos, corregir los atributos o aceptar el GRL calculado (hoy es lo que usa el juego). Una prueba fija esta lista para que cualquier cambio de datos se note.
2. **Perfil "Diving"** como secundario/terciario de Suzuki y de Gea: no es un perfil oficial; se deja fuera y se avisa (el rasgo ya vive en el atributo Diving y en las acciones).
3. **Acciones sin comportamiento:** "Shot Stopper" (Henderson, Ramsdale: es un nombre de perfil), "Tapadas Imposibles (Miracle Save)" (Ryan) y "Vocal Energizer" (Ramsdale). Se avisan; no se inventó nada.
4. **Un portero con pie "Izquierdo / Derecho (Ambidiestro técnico)":** no es un único pie; se mantiene el valor por defecto y se avisa (`FootUnclear`).

## 10. Valores que NO vienen de la base (decisiones pendientes)

- **12 atributos generales:** la base no los trae. El importador usa un valor explícito y común (`GoalkeeperImportDefaults.GeneralAttributeValue = 70`) y lo avisa (`GeneralDefaulted`). No afectan al GRL; sí al Overall, que para un portero no es relevante, y a una parte del posicionamiento en `GoalkeeperSkillModel` (Agility/Speed/Acceleration). **No son datos de autoría.**
- **Número de camiseta:** no viene; se usa 1 y se avisa (`NumberDefaulted`). Solo debe ser único dentro de un equipo (lo comprueba la validación de plantillas).
- **Edad, peso, tipo de cuerpo, pie débil, nombre corto:** quedan con los valores por defecto de `PlayerDefinition` (el nombre corto se deriva del último apellido, p. ej. "Gea" para "David de Gea").
- **Nacionalidad:** se traduce a un código (FIFA) con una tabla de datos; un país no incluido se avisa.

## 11. Siguiente fase

Aportar los 12 atributos generales, números y datos reales de plantilla; decidir los puntos de la sección 9; definir los contextos de portero y fusionar su catálogo con el de campo; conectar el `FootballDNA` de cada portero a `CharacterSpecification`; y migrar `TeamId`. Luego, el gameplay de portero (paradas, animaciones, controlador) y Unity.

## 12. Qué NO existe todavía

Paradas, física, animaciones, IK, colliders, controlador de portero, integración con el partido, presentación visual y cualquier prueba en Unity.
