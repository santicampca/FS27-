# FS27 — Integración de laterales (fase PARCIAL: 10 de 12)

> **Estado: INCOMPLETA.** La fase pedía 6 laterales derechos + 6 izquierdos (12, 72 jugadores en total). **La fuente (`fs27la.docx`) trae solo 10: 5 derechos + 5 izquierdos.** No se eligió ni se añadió ningún jugador para llegar a 12. La biblioteca tiene **70 jugadores, no 72**. Faltan **un lateral derecho y un lateral izquierdo**; cuando existan en la fuente, esta integración los acepta sin cambios de código (una prueba marcada como *pendiente* lo recuerda).

Estado técnico: datos, importación, correcciones documentadas y validación implementados y probados fuera de Unity. Reutiliza el sistema genérico de jugadores de campo ([DEFENDERS_DATA_INTEGRATION.md](DEFENDERS_DATA_INTEGRATION.md), [LEFT_DEFENDERS_DATA_INTEGRATION.md](LEFT_DEFENDERS_DATA_INTEGRATION.md)): **no hay ningún sistema específico de laterales** (ni `FullbackImporter`, ni `LateralDataRecord`, ni roles o posiciones nuevas).

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_FULLBACKS_DATABASE_V1.md` | Conversión de `fs27la.docx` (sin cambiar ningún dato; las tablas pasan a tablas markdown). **Nunca se edita.** |
| `FS27_FULLBACKS_CORRECTIONS_V1.md` | 13 correcciones documentadas (jugador, campo, antes, después, clase, fuente, razón). Cada una se aplica solo si el "antes" coincide. |
| `FS27_FULLBACKS_RECORDS_V1.json` | Registros definitivos (fuente + correcciones). Un test comprueba que son exactamente eso y que se importan por sí solos. |

## 2. Qué se reutilizó y qué se añadió (todo genérico y compatible hacia atrás)

Reutilizado sin cambios de contrato: `FieldPlayerDataRecord`, `FieldPlayerRecordImporter`, `FieldPlayerLibraryBuilder`, `AuthoringCorrections`, `DefenderVocabulary`, `NationalityCodes`, `AuthoringIds`, `RosterValidator`, `PlayerRatingCalculator`.

Añadido (sirve a cualquier jugador de campo futuro):
- **`FieldPlayerSectionedMarkdown`:** lector del *layout seccionado* de esta fuente (bloque por jugador con tablas y tres tablas globales). Produce los mismos `FieldPlayerDataRecord`; contrasta las tablas globales con los bloques e informa de cada discrepancia.
- **`FieldPlayerImportOptions.Fullback()` y `AllowedSecondaryZones`:** Wing como zona primaria, Defense o Midfield como única secundaria opcional, cualquiera de los 12 roles oficiales. Código nuevo `SecondaryZoneNotAllowed` (error). Los defensas siguen sin restricción de zona secundaria.
- **`BodyTypeNormalizer`:** una etiqueta que ya es uno de los 5 tipos oficiales ("Light", "Athletic", "Strong"…) se usa tal cual. Las etiquetas combinadas y la rama sin etiqueta no cambian; los umbrales de IMC **no se tocan**.
- **`WingPlayVocabulary`:** vocabulario de acciones = el de defensas + 12 etiquetas conceptuales de juego por banda (carrera de apoyo, pase atrás, centro, cambio de orientación...). Siguen siendo **solo de autoría**; únicamente `LateBoxArrival` (nuevo mapeo exacto) y `HighPressing` equivalen a un comportamiento existente.
- `FieldPlayerDataRecord`: `FunctionalPositions` y `GameplayBehavior` (autoría). `AuthoringCorrections`: campos `ageAsWritten`, `dateOfBirth` y `shortName`.
- `NationalityCodes`: Marruecos, Colombia y Canadá. Posición real `RM`/`LM` → zona Midfield en la comprobación de polivalencia.

## 3. Correcciones (13; ningún atributo, afinidad de rol ni Target OVR)

| Jugador | Campo | Antes → Después | Clase y confianza | Evidencia |
|---|---|---|---|---|
| Alexander-Arnold | nombre corto | Alexander-Arnold → Trent | Verified, alta | Su camiseta en el Real Madrid dice "Trent"; el contrato limita el nombre corto a 12 caracteres (16 → no se podía importar) |
| Alexander-Arnold | dorsal | "66 / 2" → 12 | Verified, alta | Lleva el 12 en el Real Madrid (La Liga limita los dorsales a 1–25; el 66 era el de Liverpool) |
| Alexander-Arnold | club | Real Madrid / Liverpool → Real Madrid | Verified, alta | Fichaje confirmado |
| Dumfries | club | Inter Milan → Real Madrid | Verified, alta | Comunicado oficial del Real Madrid (5 de julio de 2026). Su dorsal actual no se encontró: se conserva el 2 de la fuente (sin verificar) |
| Dumfries | fecha de nacimiento | 26 → 18 de abril de 1996 | Verified, alta | Wikipedia y perfiles |
| Dani Muñoz | fecha de nacimiento | 24 → 26 de mayo de 1996 | Verified, media-alta | Una ficha de perfil (búsqueda web) |
| Porro | edad escrita | 26 → 27 | Verified, alta | Nace el 13 de septiembre de 1999; la propia fuente da esa fecha y a 2026-10-02 tiene 27 |
| Cancelo | pie | frase larga → Right | Verified, alta | Es diestro (Barcelona, FotMob): la fuente da tres pies distintos en sus tres tablas |
| Cancelo | club | Al Hilal / Club de élite → Barcelona | Verified, alta | Ficha oficial del Barcelona |
| Cancelo | dorsal | 27 → 2 | Verified, alta | Lleva el 2 en el Barcelona |
| Dumfries, Mendes, Davies | zona secundaria | Attack → Midfield | Design | El contrato del lateral permite Defense o Midfield; su propia polivalencia cita RM/LM (Wing ya es la zona primaria) |

**No se corrigió** (decisión documentada): la **posición natural de Cancelo** sigue siendo `LB`, como lo registra la fuente ("opción principal en el flanco zurdo según solicitud"), aunque en la realidad es lateral derecho que juega por dentro por la izquierda. No tiene efecto en runtime (zona primaria Wing en ambos casos) y su pie diestro queda correcto. Si más adelante se quiere la posición real, es una corrección de una línea.

## 4. Normalización

- **Zonas:** todos Wing como primaria; secundaria Defense (Hakimi, Dani Muñoz, Cucurella) o Midfield (los otros 7). Nunca más de una, nunca una inventada.
- **Roles:** los 12 oficiales y como máximo 3 (aquí: Winger, Explosive, Engine, Creator, Builder, Guardian, Wall), con su afinidad. No se creó ningún rol de lateral. Las afinidades se conservan: las diferencias con los ratings de rol oficiales son de 1–2 puntos, sin contradicción clara.
- **Posiciones:** natural RB/LB; posiciones secundarias reales (RWB, LWB, CM, RM, LM, RB) y versatilidad funcional (RM, RW, LW, CM) se conservan **separadas en el registro de autoría**. En runtime todos son `PlayerRole.Defender` con la zona Wing: nadie se vuelve mediocampista ni central. Aviso `PolyvalenceBeyondZone` cuando una posición real no cabe en la zona secundaria (Dani Muñoz: RM; Cucurella: LM).
- **Pie:** 6 diestros (5 laterales derechos + Cancelo) y 4 zurdos. El pie débil es independiente (Cancelo 85).
- **BodyType:** la fuente usa tipos oficiales (Athletic, Light, Strong): sin derivación por medidas.
- **Etiquetas descriptivas** (lateral defensivo/ofensivo/híbrido): no existen en la fuente como dato y **no se crearon enums**.
- **PlayerId:** `fb-` + nombre en ASCII (`fb-achraf-hakimi`), único y determinista, sin choques con los 20 porteros ni los 40 centrales.

## 5. Auditoría de atributos

Los 12 atributos oficiales, 1–99, sin cambios. Hay **17 valores de 90 o más** (de 120) y **6 de 95 o más** (Hakimi Speed 95, Alexander-Arnold Passing 95, Dani Muñoz Stamina 95, Davies Speed 98 y Acceleration 95, Cancelo Technique 95); solo Davies es 95+ en velocidad y aceleración a la vez. Se **conservan tal cual** (fidelidad al jugador > balance global): los registros de velocidad punta (Davies 37,1 km/h) respaldan los extremos de velocidad y la técnica de Cancelo/Alexander-Arnold es su rasgo. Los perfiles no se homogeneizan: Davies es rapidísimo y defensivamente medio (Defense 77), Cucurella defensivamente fuerte (85) y más lento, Dimarco/Alexander-Arnold muy creativos. Deuda: la calibración de extremos entre bases (Davies 98 frente al 97 de van de Ven, que registra una velocidad mayor) queda para una revisión global.

## 6. Target OVR frente a Overall calculado

El Target OVR sigue siendo referencia de autoría (`OverallCheck`), nunca runtime. La tolerancia sigue en 5. Calculado − objetivo: de −2 a +6; **Davies (81 → 87) y Cancelo (81 → 87)** superan la tolerancia y generan `TargetOvrDiffers`; el resto está dentro. No se movió ningún atributo.

## 7. Avisos reales y errores

0 errores. Avisos: `TargetOvrDiffers` ×2, `PolyvalenceBeyondZone` ×2, `SignatureActionsPending` ×10 (etiquetas de autoría sin comportamiento). **No hay ningún valor por defecto**: la fuente trae dorsal, pie débil, nacionalidad, nombre corto y BodyType. Problemas de lectura (3, de la propia fuente): el dorsal "66 / 2" de Alexander-Arnold y dos tablas que dan un pie distinto para Cancelo.

## 8. Integración

Los 10 conviven con los 20 porteros y los 40 centrales en un mismo `PlayerLibrary` (70 jugadores, 20 perfiles de portero, `PlayerSystemValidator.ValidateAll` válido, GRL de portero intacto). Un equipo de 1 portero + 5 laterales valida; `TeamDefinition.PlayerIds` sigue siendo la única autoridad del roster (`TeamId` queda vacío; el club es solo autoría). Reimportar no crea duplicados.

## 9. Deuda

- **Faltan 2 jugadores** (un lateral derecho y un lateral izquierdo) para llegar a 6 + 6 y 72. Hay que completar la fuente; no se eligieron.
- Posición natural real de Cancelo (RB) frente a la registrada (LB).
- Dorsal actual de Dumfries no verificado; clubes de Hakimi, Porro, Dani Muñoz, Mendes, Dimarco, Cucurella y Davies (confirmado en Bayern) solo parcialmente contrastados.
- Calibración global de extremos de atributos y de umbrales de BodyType (no se tocaron).
- `PlayerDefinition.TeamId` sigue como pista legacy.
