# FS27 — Integración de la base de datos de 20 porteros

Estado: **datos, importación, validación y GRL implementados y probados fuera de Unity** (C# puro, `dotnet test Tests/Core.Tests`). No se ha abierto Unity ni se ha tocado gameplay. No hay física de paradas, animaciones ni controlador de portero.

## 1. Fuente de autoría

`Docs/authoring/FS27_GOALKEEPER_DATABASE_V2.md` es la fuente de autoría de los 20 porteros. Se generó a partir del documento Word entregado (`FS27p.docx`) con una conversión mecánica del texto (cabeceras, tabla de atributos y listas); **ningún dato se reconstruyó de memoria ni se modificó**. Es texto de autoría: el runtime nunca lo lee.

## 2. Cadena e importación

```
FS27_GOALKEEPER_DATABASE_V2.md  (AUTORÍA)
   │  GoalkeeperDatabaseMarkdown.Parse(texto)        lector de autoría: solo convierte texto a registros (+ problemas de lectura)
   ▼
GoalkeeperDataRecord                                 contrato de autoría (FS27.GoalkeeperRecord.v2)
   │  GoalkeeperRecordImporter.Import                validación + conversión, sin inventar
   ▼
─────────────────────────── frontera autoría | runtime ───────────────────────────
   ▼
PlayerDefinition  +  GoalkeeperProfile  +  FootballDNA (referencias a comportamientos)     un único PlayerLibrary
   ▼
GoalkeeperRatingCalculator.Calculate  →  GRL        PlayerCardData lee de las dos fuentes
                                                    GoalkeeperSkillModel recibe el perfil
```

### El record (autoría)
`PlayerId`, `Name`, `Nationality`, `HeightCm?`, `PreferredFoot` (texto tal cual) · datos generales solo si la fuente los tiene: `ShirtNumber?`, `Age?`, `WeightKg?`, `WeakFootQuality?`, `General` (12 atributos) · datos de portero: `Capabilities` (exactamente los 8 atributos, 1–99), `PrimaryProfile`, `SecondaryProfiles` (texto tal cual), `SignatureActions` · `ExternalGrl?` (solo verificación) · solo autoría: `Identity`, `Strengths`, `Weaknesses`, `Notes`, `Sources`. Los opcionales son `int?`: **ausente ≠ 0**. Nada de esto llega al runtime.

### El resultado de cada registro (`GoalkeeperImportResult`)
`Player`, `Profile`, `Dna` (null si hay errores), `Rating` (`GoalkeeperRatingCheck`), `BehaviorIds`, `UnmappedActions`, `NonOfficialProfiles` (texto original), `Defaulted` (cada valor puesto por defecto: jugador, campo, valor y motivo) e `Issues` (cada uno con `PlayerId`, `Field`, `Value` y mensaje). `GoalkeeperLibraryBuilder.Import`/`ImportMarkdown` hace el lote: cada portero completo entra en el `PlayerLibrary`; un `PlayerId` repetido es **error** y se rechaza el registro **posterior** (el primero se conserva, nunca se sobrescribe, aunque el primero haya fallado); los problemas del lector (`ReaderProblems`) se devuelven también.

## 3. GRL: autoría ≠ runtime

`GRL = RoundHalfUp((Reflexes + Handling + Positioning + Diving + Kicking + Distribution + Command + Recovery) / 8)`. Un único cálculo (`GoalkeeperRatingCalculator`, estático, solo recibe el perfil; aritmética entera `(suma + 4) / 8`). El importador **lo consume**, no lo reimplementa (una prueba lo vigila). No usa pesos, perfiles, estilos, atributos generales, altura, Overall, dificultad, reputación, nombre, nacionalidad ni potencial.

`GoalkeeperRatingCheck { PlayerId, HasExternal, ExternalGrl, CalculatedGrl, Difference (calculado − externo), Matches }`. El GRL **externo** (de autoría) y el **calculado** (oficial, el del runtime) nunca se mezclan: el externo solo vive en el record y en este check; no existe ningún campo de rating en `PlayerDefinition`, `GoalkeeperProfile` ni `FootballDNA`. Si difieren: aviso `ExternalGrlDiffers`, sin tocar atributos ni objetivo y sin bloquear.

## 4. Perfiles

Exactamente cuatro: Shot Stopper, Distributor, Sweeper, Commander. No afectan al GRL. Principal inválido = **error** (no se puede construir un perfil). Etiqueta secundaria que no es perfil ("Diving", "Clutch Player", "Acrobat"…) = **aviso** `ProfileNotOfficial`: no se convierte en perfil y el texto original queda en `NonOfficialProfiles` y en el `Value` del aviso.

## 5. Signature Actions

Son ids de comportamiento, no atributos ni perfiles. `GoalkeeperBehaviors.CreateCatalog()` tiene 12 comportamientos **genéricos** con `Scope = Goalkeeper` y estado `Planned` (`GoalkeeperReflexSave`, `…DivingSave`, `…OneOnOneStopper`, `…PenaltyStopper`, `…CloseShotReaction`, `…AggressiveClaim`, `…QuickDistribution`, `…PressureBreakPass`, `…LongBallSpecialist`, `…SweeperRun`, `…VocalLeader`, `…CommandBackline`) y un mapa nombre de la fuente → id. Cada acción se busca ahí; si existe se mapea y si no es `ActionUnmapped` (aviso, no se crea nada). El `FootballDNA` del portero contiene **solo** referencias (peso 1) a comportamientos existentes: sin tendencias inventadas, sin lógica por nombre ni id de jugador. El catálogo no está fusionado con el de campo (15 comportamientos, `Scope = Any`, intacto): faltan antes los contextos de portero.

## 6. Errores y avisos

Regla: **ERROR = no puedo construir un registro válido; WARNING = puedo, pero hay información incompleta, ambigua o no soportada.** Cada código está clasificado (una prueba exige que todos lo estén).

| ERROR (el portero no se importa) | WARNING (se importa y se avisa) |
|---|---|
| `RecordMissing`, `PlayerIdInvalid`, `NameInvalid` | `ExternalGrlDiffers` |
| `AttributeMissing`, `AttributeUnknown` (un 9.º o nombre inexacto), `AttributeOutOfRange` | `ProfileNotOfficial` |
| `PrimaryProfileInvalid`, `ProfileDuplicate` | `ActionUnmapped` |
| `GeneralAttributeUnknown`, `GeneralAttributeOutOfRange` | `MissingGeneralAttributeDefaulted` (uno por atributo, con el valor usado) |
| `ExternalGrlOutOfRange` (fuera de 1–99, incluido 0) | `ShirtNumberDefaulted`, `AgeDefaulted`, `WeightDefaulted`, `HeightDefaulted`, `WeakFootDefaulted`, `PreferredFootDefaulted` |
| `ActionWrongScope` | `PreferredFootAmbiguous` (con el texto original) |
| `PlayerInvalid` (p. ej. un dorsal **dado** de 0) | `NationalityUnknown` |
| `DuplicatePlayerId` (lote) | |

Un valor **dado** pero inválido es siempre error (no se sustituye por un default); un valor **ausente** se rellena con un default explícito y se avisa. Roster: `RosterValidator` + `DataValidator.ValidateTeam` (ver §7).

## 7. Roster

`TeamDefinition.PlayerIds` es la autoridad. `PlayerDefinition.TeamId` es una pista (legacy): los datos importados la dejan vacía y el validador detecta cualquier contradicción o jugador en dos rosters. **Deuda:** `PlayerCardData.TeamId` y `PlayerLibrary.GetByTeam` aún la leen; la migración queda para una fase propia.

## 8. Relaciones

- **`PlayerDefinition`:** identidad, altura, pie, nacionalidad, atributos generales. Nada de portero.
- **`GoalkeeperProfile`:** solo las 8 capacidades y los perfiles.
- **`FootballDNA`:** referencias a comportamientos; los atributos GK no se duplican.
- **`PlayerCardData`:** lee jugador + perfil en vivo (no copia nada) y calcula el GRL al consultarlo.
- **`GoalkeeperSkillModel`:** sin estado; recibe perfil + dificultad; probado con los 20 porteros en los 5 niveles.
- **Dificultad:** no cambia atributos ni GRL (`DifficultyDefinition` no se modificó).

## 9. Hallazgos honestos sobre la base de autoría (no se editó el archivo)

1. **10 de los 20 `Target GRL` no coinciden con la media redondeada de sus propios 8 atributos**, aunque el archivo marca `Match: YES`. El importador los avisa (`ExternalGrlDiffers`) y el juego usa siempre la media. Tabla (autoría → media de las 8): Maignan 81→82 · Pickford 83→84 · Sommer 81→82 · Oblak 82→83 · Donnarumma 83→82 · Neuer 84→86 · Kobel 83→82 · Onana 79→81 · Alisson 84→85 · Ramsdale 80→81. Además algunas sumas/promedios escritos en la sección "GRL Verification" son aritméticamente incorrectos (p. ej. Maignan: la tabla suma 656, no 646); esa sección se ignora por ser derivada. Decisión tuya: corregir los objetivos, corregir los atributos o aceptar el GRL calculado (hoy es lo que usa el juego). Una prueba fija esta lista (y el inventario completo de avisos de la base) para que cualquier cambio de datos se note. Ramsdale es un empate exacto (80,5): el redondeo hacia arriba da 81.
2. **Perfil "Diving"** como secundario/terciario de Suzuki y de Gea: no es un perfil oficial; se deja fuera y se avisa (`ProfileNotOfficial`; el rasgo ya vive en el atributo Diving y en las acciones).
3. **Acciones sin comportamiento:** "Shot Stopper" (Henderson, Ramsdale: es un nombre de perfil), "Tapadas Imposibles (Miracle Save)" (Ryan) y "Vocal Energizer" (Ramsdale). Se avisan (`ActionUnmapped`); no se inventó nada.
4. **Un portero con pie "Izquierdo / Derecho (Ambidiestro técnico)":** no es un único pie (es Pickford); se mantiene el valor por defecto y se avisa (`PreferredFootAmbiguous`).

## 10. Valores que NO vienen de la base (decisiones pendientes)

- **12 atributos generales:** la base no los trae. El importador usa un valor explícito y común (`GoalkeeperImportDefaults.GeneralAttributeValue = 70`) y lo avisa uno a uno (`MissingGeneralAttributeDefaulted`). No afectan al GRL; sí al Overall, que para un portero no es relevante, y a una parte del posicionamiento en `GoalkeeperSkillModel` (Agility/Speed/Acceleration). **No son datos de autoría.**
- **Número de camiseta:** no viene; se usa 1 y se avisa (`ShirtNumberDefaulted`). Solo debe ser único dentro de un equipo (lo comprueba la validación de plantillas).
- **Edad, peso, pie débil:** valores por defecto de un `PlayerDefinition` nuevo, cada uno con su aviso (`AgeDefaulted`, `WeightDefaulted`, `WeakFootDefaulted`). Tipo de cuerpo y nombre corto no forman parte del contrato y quedan por defecto (el nombre corto se deriva del último apellido, p. ej. "Gea" para "David de Gea").
- **Nacionalidad:** se traduce a un código (FIFA) con una tabla de datos; un país no incluido se avisa.

## 11. Siguiente fase

Aportar los 12 atributos generales, números y datos reales de plantilla; decidir los puntos de la sección 9; definir los contextos de portero y fusionar su catálogo con el de campo; conectar el `FootballDNA` de cada portero a `CharacterSpecification`; y migrar `TeamId`. Luego, el gameplay de portero (paradas, animaciones, controlador) y Unity.

## 12. Qué NO existe todavía

Paradas, física, animaciones, IK, colliders, controlador de portero, integración con el partido, presentación visual y cualquier prueba en Unity.
