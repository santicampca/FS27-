# FS27 — Integración de segundos delanteros (10 SD)

> **Estado: COMPLETA — 10 SD, 136 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 42 mediocampistas + 22 delanteros de banda y segundos delanteros). Datos, importación, correcciones y validación probados fuera de Unity (`SecondStrikersDatabaseTests`, 13 tests). Sin push, sin PR. El documento (`FS27sd.docx`) trae exactamente 10: Dybala, Messi, Olise, Lamine Yamal, Morgan Rogers, Fermín López, Raphinha, Neymar, Kvaratskhelia y Dembélé. Ids `sd-nombre`.

> **Mismo aviso que los extremos:** la fuente no trae afinidades de rol, fecha de nacimiento, altura, peso ni dorsal. Las afinidades son los valores por defecto del proyecto por posición (90 / 75 / 60), **no investigados** (el orden de los roles sí es el de la fuente). Fecha, altura, dorsal y club actualizado los completé con búsquedas web (32 correcciones verificadas). Falta el **peso** en los 10 y la **altura** de Olise: valor neutro con aviso.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_SD_DATABASE_V1.md` | Conversión de `FS27sd.docx` sin cambiar ningún dato: 4 tablas globales + 10 párrafos. **Nunca se edita.** |
| `FS27_SD_CORRECTIONS_V1.md` | 32 correcciones/complementos (todos verificados) y los hallazgos que NO se cambian. |
| `FS27_SD_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado: el lector de lista de los extremos (`FieldPlayerListMarkdown`), importador, correcciones, `AttackPlayVocabulary` y la opción de afinidades por defecto. Añadido (genérico, solo autoría): `FieldPlayerImportOptions.SecondStriker()` (rol Forward, zona primaria Attack, secundaria Midfield o Wing; el contrato de los extremos no cambia), unas 30 etiquetas de acciones (solo se unen ortografías exactas de la misma acción), códigos de posición MC, MCD y DC, y el código de nacionalidad GEO. Sin lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Attack; secundaria Midfield (4) o Wing (6), como dice la fuente. Las posiciones reales ED/EI/MCO son autoría: nadie se convierte en extremo ni mediocampista (4 avisos `PolyvalenceBeyondZone`).
- Atributos y Target OVR tal como investigados. Messi y Neymar tienen valores de 95-96 (cuatro en total) y la defensa más baja de la base (35 y 40); Messi sale con Overall 90.
- Messi (objetivo 83, calculado 90, +7) y Neymar (80 frente a 89, +9) pasan de la tolerancia de 5: avisan (`TargetOvrDiffers`), la tolerancia no se toca.
- Dembélé tiene el pie escrito como "Both": aviso `PreferredFootAmbiguous`, se conserva Right y nunca se convierte en Left.
- Clubs actualizados: Morgan Rogers (Chelsea, 17), Neymar (Santos), Kvaratskhelia (solo PSG).

## 4. Cómo se verifica

`dotnet test Tests/Core.Tests --filter SecondStrikersDatabase` (y la suite completa). Inventario de avisos fijado: 30 `AffinityDefaulted`, 10 `WeightDefaulted`, 1 `HeightDefaulted`, 10 `ShortNameDerived`, 10 `SignatureActionsPending`, 4 `PolyvalenceBeyondZone`, 2 `TargetOvrDiffers`, 1 `PreferredFootAmbiguous`; ningún error.
