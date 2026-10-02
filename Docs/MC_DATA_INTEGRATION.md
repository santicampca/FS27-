# FS27 — Integración de mediocentros centrales (10 MC)

> **Estado: COMPLETA — 10 MC, 104 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 12 híbridos + 10 MCD + 10 MC). Datos, importación, correcciones y validación probados fuera de Unity (`MidfieldersMcDatabaseTests`, 12 tests). Sin push, sin PR. El documento (`FS27mc.docx`) trae exactamente 10 (lista cerrada): Fabián Ruiz, Pedri, Enzo Fernández, Declan Rice, Alexis Mac Allister, Sandro Tonali, Luka Modrić, Nicolò Barella, Kobbie Mainoo y Scott McTominay. Ids `mq-nombre` (el prefijo `mc-` ya lo usan los mediocentros defensivos).

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_MC_DATABASE_V1.md` | Conversión de `FS27mc.docx` sin cambiar ningún dato. **Nunca se edita.** |
| `FS27_MC_CORRECTIONS_V1.md` | 5 correcciones (3 verificadas, 1 por inferencia, 1 de diseño) y los hallazgos que NO se cambian. |
| `FS27_MC_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado: el lector de fichas propiedad/valor de los MCD (`FieldPlayerPropertyCardMarkdown`, solo se generalizó para aceptar la sección "MC Type" y localizar la tabla de nombres por su columna Nation), importador, correcciones y `MidfieldPlayVocabulary`. Añadido: `FieldPlayerImportOptions.CentralMidfielder()` (como el híbrido pero con **Attack** también permitida como zona secundaria, porque 6 de los 10 la tienen en la fuente; el contrato del híbrido y del MCD no cambia) y cuatro etiquetas de acciones (TurnAndAccelerate, OutsideOfFootPass, TempoControl, AerialThreat). Sin lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Midfield y una secundaria tal como la escribe la fuente (Attack ×6, Defense ×4). La cobertura de emergencia (AM, LW, CB, RM) es solo autoría.
- Atributos, roles y Target OVR tal como investigados. McTominay tiene Finisher (91) como primer rol. Barella tiene Stamina 95, el único 95 de la base.
- **Luka Modrić**: Target OVR 81 frente a 87 calculado (+6), fuera de la tolerancia de 5, así que el importador avisa (`TargetOvrDiffers`). La tolerancia no se toca y el juego usa el 87.
- Acciones de firma: etiquetas de autoría; solo "Late box arrival" (5 jugadores) equivale a un comportamiento existente.

## 4. Correcciones y hallazgos

Corregidos: Modrić (club AC Milan, dorsal 14, edad 41), McTominay (dorsal 8) y el nombre corto de Enzo Fernández (14 caracteres, el contrato permite 12 → "Fernández"). Verificados sin cambios: Mainoo (37), Fabián, Pedri, Enzo, Rice, Mac Allister, Tonali, Barella. El lector informa 4 diferencias menores entre tabla y ficha (club con/sin "FC"/"SSC"); un test las fija.

## 5. Cómo se verifica

`dotnet test Tests/Core.Tests --filter MidfieldersMc` (y la suite completa). Inventario de avisos fijado: 10 `SignatureActionsPending` + 1 `TargetOvrDiffers`, ningún error.
