# FS27 — Integración de mediocampistas híbridos (6 MDI + 6 MDD = 12)

> **Estado: COMPLETA — 12 jugadores (6 MDI zurdos + 6 MDD diestros), 84 jugadores en la biblioteca** (20 porteros + 40 centrales + 12 laterales + 12 híbridos). Datos, importación, correcciones y validación probados fuera de Unity (`HybridMidfieldersDatabaseTests`, 13 tests). Sin push, sin PR.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md` | Conversión de `FS27md.docx` sin cambiar ningún dato: 5 tablas globales + 12 fichas. **Nunca se edita.** |
| `FS27_HYBRID_MIDFIELDERS_CORRECTIONS_V1.md` | 8 correcciones (6 verificadas, 2 por inferencia) y los hallazgos que NO se cambian. |
| `FS27_HYBRID_MIDFIELDERS_RECORDS_V1.json` | Registros definitivos (fuente + correcciones); un test comprueba que son exactamente eso. |

## 2. Qué se reutilizó y qué se añadió

Reutilizado sin cambios de contrato: `FieldPlayerDataRecord`, `FieldPlayerRecordImporter`, `FieldPlayerLibraryBuilder`, `AuthoringCorrections`, `WingPlayVocabulary`, `AuthoringIds` (ids `md-nombre`).
Añadido (genérico, solo autoría): `FieldPlayerCardMarkdown` (lector del diseño de "fichas": tablas globales cruzadas contra cada ficha), `FieldPlayerImportOptions.HybridMidfielder()` (rol `Midfielder`, zona primaria Midfield, una zona secundaria Defense o Wing, los 12 roles), `MidfieldPlayVocabulary` (etiquetas de acciones; ninguna se fuerza a un comportamiento existente), `ImportCardMarkdown`, y los códigos de nacionalidad UKR y HUN. No hay ningún sistema específico de MDI/MDD ni lógica por nombre.

## 3. Reglas aplicadas

- Zona primaria Midfield y una zona secundaria (Defense o Wing), tal como las escribe la fuente. Las posiciones reales (LB, LWB, RB...) se conservan como autoría; los laterales **no** se convierten en mediocampistas por esta fase y esta fase no añade ningún lateral.
- Jerarquía respetada: posición natural > posición real secundaria > versatilidad funcional (texto) > cobertura de emergencia (no existe aquí).
- Posiciones reales fuera de la zona secundaria (5 casos: LWB/RWB/RB) → aviso `PolyvalenceBeyondZone`; ninguna zona se inventa.
- Target OVR separado del Overall calculado; tolerancia 5 sin cambios; todos dentro de ella (p. ej. Zinchenko 82 calculado frente a 77 objetivo, justo en el límite).
- Atributos y afinidades de rol: tal como investigados. Solo Valverde (92) y Caicedo (90) llegan a 90 en un atributo; nadie llega a 95.
- Acciones de firma: etiquetas de autoría (aviso `SignatureActionsPending` ×12); `Progressive Pass`, `Pressing`, etc. no se asignan a comportamientos que solo se les parecen.

## 4. Hallazgos y datos no verificados

- **Nathan Brown no se pudo identificar** (ningún mediocampista inglés de ese nombre nacido en 2004; el nombre parecido es Nathaniel Brown, lateral alemán de 2003). Se conserva su ficha tal como la dio el documento; nacionalidad, fecha, medidas y dorsal son **NO VERIFICADOS**. El "club" era texto descriptivo y se vació.
- Correcciones verificadas: cumpleaños de Kimmich (8-feb-1995), club y dorsal de Barco (Chelsea, 4), club de Zinchenko (Ajax; su dorsal allí no se confirmó y se mantiene el 35 como no verificado), dorsal de O'Reilly (33), edad de Lewis-Skelly (20).
- Barco: la tabla resumen dice "LB / LWB" y su ficha "LB" con LM/LWB como secundarias; se usa la ficha y el lector lo informa (un test lo fija).

## 5. Cómo se verifica

`dotnet test Tests/Core.Tests --filter HybridMidfielders` (y la suite completa). El inventario de avisos está fijado: 5 `PolyvalenceBeyondZone` + 12 `SignatureActionsPending`, ningún error.
