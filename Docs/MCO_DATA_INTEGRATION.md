# FS27 — Integración de mediocampistas ofensivos (10 MCO)

> **Estado: COMPLETA — 10 MCO, 114 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 12 híbridos + 10 MCD + 10 MC + 10 MCO). Datos, importación, correcciones y validación probados fuera de Unity (`MidfieldersMcoDatabaseTests`, 14 tests). Sin push, sin PR. El documento (`FS27mco.docx`) trae exactamente 10 (lista cerrada): Bellingham, Palmer, Ødegaard, Bruno Fernandes, Musiala, Isco, Reus, De Bruyne, Wirtz y Gilberto Mora. Ids `mo-nombre`. No se ha tocado nada de extremos ni de bandas.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_MCO_DATABASE_V1.md` | Conversión de `FS27mco.docx` sin cambiar ningún dato. **Nunca se edita.** |
| `FS27_MCO_CORRECTIONS_V1.md` | 9 correcciones (6 verificadas, 1 por inferencia, 2 de diseño) y los hallazgos que NO se cambian. |
| `FS27_MCO_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado: el lector de fichas propiedad/valor (`FieldPlayerPropertyCardMarkdown`), importador, correcciones y `MidfieldPlayVocabulary`. Cambios genéricos, compatibles hacia atrás: el lector acepta la sección "MCO Type", tolera una nota entre paréntesis tras una altura, peso o dorsal (se queda con el número que va delante) y lee "Reus (Marco Reus)" como el nombre completo; `AuthoringIds.Slug` convierte letras sin descomposición (Ø, Æ, Ł, ß...) a su ortografía ASCII (sin esto el id de Ødegaard perdía la letra); códigos de nacionalidad NOR y MEX; cinco etiquetas de acciones nuevas y el alias "MilimeterCross" (la fuente lo escribe así) hacia el cruce de precisión existente; `FieldPlayerImportOptions.OffensiveMidfielder()` (mismo contrato que el MC: Midfield primaria, Defense/Wing/Attack secundaria). Sin lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Midfield y secundaria Attack en los diez, tal como la fuente. SD (segundo delantero), RW, LW y LM son solo autoría: nadie se convierte en extremo ni delantero (6 avisos `PolyvalenceBeyondZone`).
- Atributos, roles y Target OVR tal como investigados; nadie llega a 95 (máximo 94).
- **Tres Target OVR están 6 por debajo del calculado** (Isco, De Bruyne, Wirtz): fuera de la tolerancia de 5, el importador avisa y la tolerancia no se toca.
- Acciones de firma: etiquetas de autoría; solo "Late box arrival" (4 jugadores) equivale a un comportamiento existente.

## 4. Correcciones y hallazgos

Clubes y dorsales desactualizados: De Bruyne (Napoli, 11), Wirtz (Liverpool, 7). Edad de Bruno Fernandes (32). Nombres cortos demasiado largos (Fernandes, Mora). Gilberto Mora (17 años): dorsal 10 y altura 168 cm (el documento daba estimados); su peso sigue sin verificar. El lector informa 5 incoherencias del documento (filas de pie mal formadas de Ødegaard, Musiala y Wirtz; club sin "FC" para Palmer y Ødegaard); un test las fija.

## 5. Cómo se verifica

`dotnet test Tests/Core.Tests --filter MidfieldersMco` (y la suite completa). Inventario fijado: 10 `SignatureActionsPending`, 6 `PolyvalenceBeyondZone`, 3 `TargetOvrDiffers`, ningún error.
