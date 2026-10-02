# FS27 — Integración de extremos y delanteros de banda (6 ED + 6 EI = 12)

> **Estado: COMPLETA — 12 jugadores (6 ED + 6 EI), 126 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 42 mediocampistas + 12 extremos). Datos, importación, correcciones y validación probados fuera de Unity (`WingersDatabaseTests`, 12 tests). Sin push, sin PR. El documento (`FS27ex.docx`) trae exactamente 12: ED Saka, Giuliano Simeone, Doué, Estêvão, Semenyo, Salah; EI Pulisic, Mitoma, Gordon, Vinícius Júnior, Barcola, Díaz. Ids `wg-nombre`.

> **Aviso importante:** esta fuente es la más escasa del proyecto. No trae **afinidades de rol**, ni fecha de nacimiento, altura, peso o dorsal. Las afinidades son los **valores por defecto del proyecto por posición (90 / 75 / 60)**, no investigados (el orden de los roles sí es el de la fuente). Fecha, altura, dorsal y club actualizados los completé con búsquedas web (39 correcciones verificadas). El **peso** falta en los 12 y la **altura** en Simeone y Semenyo: se usa el valor neutro con aviso.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_WINGERS_DATABASE_V1.md` | Conversión de `FS27ex.docx` sin cambiar ningún dato: 4 tablas globales + 12 párrafos. **Nunca se edita.** |
| `FS27_WINGERS_CORRECTIONS_V1.md` | 39 correcciones/complementos (todos verificados) y los hallazgos que NO se cambian. |
| `FS27_WINGERS_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado: `FieldPlayerDataRecord`, importador, correcciones, builder. Añadido (genérico, solo autoría, compatible hacia atrás): `FieldPlayerListMarkdown` (lector de este diseño: 4 tablas globales cruzadas contra un párrafo por jugador); `FieldPlayerImportOptions.Winger()` (rol Forward, zona primaria Wing, secundaria Attack o Midfield, los 12 roles); la opción `DefaultMissingAffinities` (solo activa aquí: afinidad por defecto por posición + aviso `AffinityDefaulted`; en el resto de bases una afinidad ausente sigue siendo error, y un test lo comprueba); `AttackPlayVocabulary` (etiquetas de acciones de banda; solo se unen ortografías exactas de la misma acción y ninguna se fuerza a un comportamiento existente); códigos de posición ED/EI (Wing), SD (Attack) y MCO (Midfield); códigos de nacionalidad USA, EGY y GHA. Sin lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Wing y secundaria Attack en los doce, tal como la fuente. Las posiciones reales ED/EI/SD/MCO son autoría: nadie se convierte en mediocampista ni delantero (2 avisos `PolyvalenceBeyondZone`, los que tienen MCO).
- Atributos y Target OVR tal como investigados. Vinícius Júnior tiene dos valores 95 (aceleración y regate) y Overall 91.
- Estêvão (objetivo 78, calculado 86) y Mitoma (77 frente a 87) pasan de la tolerancia de 5: avisan (`TargetOvrDiffers`), la tolerancia no se toca.
- Barcola: la fuente dice PSG, pero hay noticias de traspaso a Liverpool en agosto de 2026; se deja PSG y se marca como no verificado.

## 4. Cómo se verifica

`dotnet test Tests/Core.Tests --filter WingersDatabase` (y la suite completa). Inventario de avisos fijado: 36 `AffinityDefaulted`, 12 `WeightDefaulted`, 2 `HeightDefaulted`, 12 `ShortNameDerived`, 12 `SignatureActionsPending`, 2 `PolyvalenceBeyondZone`, 2 `TargetOvrDiffers`; ningún error.
