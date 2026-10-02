# FS27 — Integración de delanteros centro (20 DC, dos bloques de 10)

> **Estado: COMPLETA — 20 DC, 156 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 42 mediocampistas + 12 extremos, 10 SD y 20 DC). Datos, importación, correcciones y validación probados fuera de Unity (`StrikersDatabaseTests`, 13 tests). Sin push, sin PR. El documento (`FS27dc.docx`) trae **dos bloques de 10**: Bloque 1 (Kane, Havertz, Julián Álvarez, Lautaro Martínez, Mbappé, Haaland, João Pedro, Gyökeres, Isak, Malen) y Bloque 2 (Cristiano Ronaldo, Troy Parrott, Pio Esposito, Oyarzabal, Osimhen, Lukaku, Chris Wood, Aubameyang, Adeyemi, Muriqi). Ids `dc-nombre`.

> **Mismo aviso que extremos y SD:** la fuente no trae afinidades de rol, fecha de nacimiento, altura, peso ni dorsal. Las afinidades son los valores por defecto del proyecto por posición (90 / 75 / 60), **no investigados** (el orden de los roles sí es el de la fuente). Fecha, altura, dorsal y club actualizado los completé con búsquedas web (67 correcciones verificadas). Falta el **peso** en los 20, el **dorsal** de Lukaku, Aubameyang y Adeyemi, y la **altura** de Adeyemi.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_DC_DATABASE_V1.md` | Conversión de `FS27dc.docx` sin cambiar ningún dato. Las cuatro tablas globales de cada bloque se concatenan (20 filas: bloque 1 y luego bloque 2) y los párrafos siguen el mismo orden. **Nunca se edita.** |
| `FS27_DC_CORRECTIONS_V1.md` | 67 correcciones/complementos (todos verificados) y los hallazgos que NO se cambian. |
| `FS27_DC_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado: el lector de lista (`FieldPlayerListMarkdown`), importador, correcciones, opción de afinidades por defecto y `AttackPlayVocabulary`. Añadido (genérico, solo autoría): el lector acepta "Secondary: None" (zona secundaria vacía; 9 DC no la tienen) y la línea "Tipo Funcional" (texto de autoría que se guarda en la nota); `FieldPlayerImportOptions.Striker()` (mismo contrato que `SecondStriker()`: Attack primaria; Midfield, Wing o ninguna secundaria; los 12 roles); unas 60 etiquetas de acciones nuevas (solo se unen ortografías exactas de la misma acción); códigos de nacionalidad SWE, IRL, NZL, GAB y KVX. Sin lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Attack; secundaria la que dice la fuente (Midfield ×5, Wing ×6, ninguna ×9). Las posiciones reales SD/MCO/EI/ED son autoría: nadie se reclasifica.
- Atributos y Target OVR tal como investigados. Cuatro valores de 95+ en tres jugadores (Mbappé 96 y 97, Haaland 96, Adeyemi 95). Mbappé sale con Overall 92, Kane 88.
- Malen (79 vs 85), Aubameyang (80 vs 86) y Adeyemi (80 vs 87) pasan de la tolerancia de 5: avisan (`TargetOvrDiffers`), la tolerancia no se toca.
- El apellido de Gyökeres está mal escrito una vez en la tabla de la fuente: el lector lo informa (4 avisos fijados por un test) y una corrección arregla nombre e id (`dc-viktor-gyokeres`).
- Clubs actualizados: João Pedro (Chelsea, 9), Gyökeres (Arsenal, 14), Isak (Liverpool, 9), Malen (Roma, cedido, 14), Parrott (Real Betis, 19), Osimhen (Galatasaray, 45), Adeyemi (FC Barcelona), Muriqi (Fenerbahçe, 19). **Sin confirmar, se dejan como la fuente:** Julián Álvarez (Atlético), Lukaku (Napoli) y Aubameyang (Al-Qadsiah).

## 4. Cómo se verifica

`dotnet test Tests/Core.Tests --filter StrikersDatabase` (y la suite completa). Inventario de avisos fijado: 60 `AffinityDefaulted`, 20 `WeightDefaulted`, 3 `ShirtNumberDefaulted`, 1 `HeightDefaulted`, 20 `ShortNameDerived`, 20 `SignatureActionsPending`, 3 `TargetOvrDiffers`; ningún error.
