# FS27 — Integración de mediocentros defensivos (10 MCD)

> **Estado: COMPLETA — 10 MCD, 94 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 12 híbridos MDI/MDD + 10 MCD). Datos, importación, correcciones y validación probados fuera de Unity (`MidfieldersMcdDatabaseTests`, 13 tests). Sin push, sin PR. El documento (`FS27mcd.docx`) trae exactamente 10 jugadores (lista cerrada): Rodri, Vitinha, Frenkie de Jong, Joao Neves, Hakan Çalhanoğlu, Elliot Anderson, Ryan Gravenberch, Bruno Guimarães, Casemiro y Leandro Paredes.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_MCD_DATABASE_V1.md` | Conversión de `FS27mcd.docx` sin cambiar ningún dato: 3 tablas globales + 10 fichas. **Nunca se edita.** |
| `FS27_MCD_CORRECTIONS_V1.md` | 6 correcciones (3 verificadas, 2 por inferencia, 1 de diseño) y los hallazgos que NO se cambian. |
| `FS27_MCD_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado: `FieldPlayerDataRecord`, `FieldPlayerRecordImporter`, `FieldPlayerLibraryBuilder`, `AuthoringCorrections`, `MidfieldPlayVocabulary`, `AuthoringIds` (ids `mc-nombre`).
Añadido (genérico, solo autoría): `FieldPlayerPropertyCardMarkdown` (lector de fichas con tabla de roles y de perfil "propiedad/valor"; ignora etiquetas de cita sueltas y cruza las 3 tablas globales con cada ficha), `ImportPropertyCardMarkdown`, `FieldPlayerImportOptions.DefensiveMidfielder()` (mismo contrato que el híbrido: Midfield primaria, Defense o Wing secundaria, los 12 roles), nuevas etiquetas de acciones (DeepBuildUp, Interception, DefensiveScreen, LineBreakingPass, BallRecovery, TacticalFoul y alias CounterPress, LongSwitch) y el código de nacionalidad TUR. No hay ningún sistema específico de MCD ni lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Midfield; una zona secundaria (Defense; Wing para Vitinha y Anderson), tal como las escribe la fuente. La "cobertura de emergencia" (CB, AM, LM) es solo información de autoría: nadie se convierte en central ni extremo.
- Rol de Casemiro: Wall (94) como primer rol, por la fuente; el resto de roles y todos los atributos tal como investigados. Nadie llega a 95 en un atributo (máximo 92; 13 valores ≥ 90).
- Target OVR separado del Overall calculado; tolerancia 5 sin cambios; todos dentro de ella (p. ej. Frenkie de Jong 87 calculado frente a 83 objetivo).
- Acciones de firma: etiquetas de autoría (aviso `SignatureActionsPending` ×10); ninguna se fuerza a un comportamiento que solo se le parece.

## 4. Correcciones y hallazgos

Corregidos: nombre corto de Bruno Guimarães (15 caracteres, el contrato permite 12 → "Guimarães"), edad de Joao Neves (22), etiqueta de cuerpo "Agile / Light" → Light, nacionalidad y dorsal de Elliot Anderson (Inglaterra, 5 en el City), club de Paredes (Boca Juniors). Verificados sin cambios: Casemiro (Inter Miami, 5), Çalhanoğlu (Inter), fechas de nacimiento. El lector informa 5 incoherencias internas del documento (ficha de Anderson: código "SCO / ENG", fila de pie mal formada y nación distinta a la tabla; clubes de Gravenberch y Paredes distintos entre tabla y ficha); un test las fija.

## 5. Cómo se verifica

`dotnet test Tests/Core.Tests --filter MidfieldersMcd` (y la suite completa). Inventario de avisos fijado: 10 `SignatureActionsPending`, ningún error.
