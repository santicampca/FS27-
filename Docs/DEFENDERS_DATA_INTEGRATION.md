# FS27 — Integración de los 20 defensas centrales

> Los 20 defensas centrales zurdos usan este mismo sistema: ver [LEFT_DEFENDERS_DATA_INTEGRATION.md](LEFT_DEFENDERS_DATA_INTEGRATION.md).

Estado: **datos, importación, correcciones documentadas y validación implementados y probados fuera de Unity** (C# puro, `dotnet test Tests/Core.Tests`). No se tocó gameplay, IA, animación ni Unity. La fuente de autoría es `fs27dd.docx`.

## 1. Archivos de autoría (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_DEFENDERS_DATABASE_V1.md` | Conversión línea a línea de `fs27dd.docx`. **Nunca se edita**: es la fuente. |
| `FS27_DEFENDERS_CORRECTIONS_V1.md` | Tabla de correcciones documentadas (jugador, campo, antes, después, clase, fuente, razón). Se aplican por código solo si el "antes" coincide. |
| `FS27_DEFENDERS_RECORDS_V1.json` | Registros definitivos (fuente + correcciones). Un test comprueba que es exactamente lo que se regenera, y que se importa solo. |

## 2. Cadena

```
FS27_DEFENDERS_DATABASE_V1.md ──FieldPlayerDatabaseMarkdown──► FieldPlayerDataRecord ──AuthoringCorrections──► registro corregido
        │ (AUTORÍA)                                                                                                   │
────────┴──────────────────────────────────────── frontera autoría | runtime ───────────────────────────────────────┴──
FieldPlayerRecordImporter ──► PlayerDefinition + PlayerPlayingProfile + FootballDNA ──► PlayerLibrary / PlayingProfileCatalog
                                    └─ OverallCheck (Target OVR frente a Overall calculado por PlayerRatingCalculator)
```

Es el mismo patrón que el de porteros, pero **genérico para jugadores de campo** (el siguiente puesto reutiliza `FieldPlayerRecordImporter` cambiando solo las `FieldPlayerImportOptions`: roles y zonas permitidos, vocabulario de acciones). El archivo de texto nunca lo lee el runtime; el lector solo recibe texto.

## 3. Contrato del jugador de campo

- **`PlayerDefinition`** = identidad + los **12 atributos oficiales** (Speed, Acceleration, Agility, Strength, Stamina, Shooting, Finishing, Passing, BallControl, Dribbling, Technique, Defense) + datos físicos. Un atributo ausente, fuera de 1–99 o **no oficial** (Tackling, Heading, Marking, Interceptions, Positioning, Anticipation, Jumping, Pace, Reaction, cualquiera de portero) es un **error**; un atributo nunca se rellena por defecto.
- **`PlayerPlayingProfile`** = zona primaria, **como máximo una** zona secundaria, 1–3 roles con afinidad 0–100 y Risk/Creativity/Aggression 0–100. Para estos defensas solo se aceptan los roles **Wall, Guardian, Anchor** y la zona primaria **Defense**.
- **`FootballDNA`** = solo referencias a comportamientos que ya existen. No se creó ningún comportamiento.
- **`PlayerCardData`** = proyección (muestra el Overall calculado). **`TeamDefinition.PlayerIds`** sigue siendo la única autoridad del roster; `TeamId` queda vacío y el club es solo información de autoría.

## 4. Target OVR frente a Overall calculado

El documento trae un "Target OVR". **No es el Overall del juego**: el Overall oficial lo calcula `PlayerRatingCalculator` desde los atributos y el perfil. El importador guarda ambos en `OverallCheck { TargetOvr, CalculatedOverall, Difference, Matches }` y nunca los mezcla; no se tocó ningún atributo para acercarlos y el target no existe en ningún tipo de runtime. Resultado en esta base (calculado − target): siempre dentro de ±5 (Cubarsí 86→82 es el caso mayor negativo; Kalulu, Rüdiger y Koulibaly están entre los mayores positivos). El aviso `TargetOvrDiffers` solo salta por encima de 5 (valor de partida, no una regla); en esta base no salta.

## 5. Zonas, polivalencia y posiciones reales

Jerarquía mantenida: posición natural > posición real secundaria > versatilidad funcional > cobertura de emergencia. **Todos siguen siendo centrales** (`NaturalPosition = CB`); nadie pasa a lateral. La zona secundaria es la principal extensión funcional y solo existe con evidencia en el propio documento:

| Zona secundaria | Jugadores |
|---|---|
| Wing | Kalulu, Koundé, Pubill, Aramburu, Araújo, Konsa, Timber (todos con RB como posición secundaria real) |
| Midfield | Marquinhos (CDM es su primera posición secundaria; el RB queda fuera de la zona) |
| ninguna | los otros 12 ("CB puro") |

La polivalencia completa (`SecondaryPositions`, `VersatilityLevel`, nota) **se conserva en el registro de autoría**; en runtime solo existe la zona secundaria (no se creó ningún campo ni arquitectura nueva). Cuando una posición real queda fuera de la zona secundaria el importador avisa (`PolyvalenceBeyondZone`): hoy solo Marquinhos (RB).

## 6. Normalización

**BodyType** (etiquetas combinadas → los 5 oficiales Light, Athletic, Strong, Tall, Compact, sin crear ninguno), con reglas visibles de `BodyTypeNormalizer` que usan la etiqueta y la altura/peso (el resultado y la regla quedan en el resultado de importación):

| Regla (en orden) | Ejemplo |
|---|---|
| "Tall" en la etiqueta → Tall | Saliba |
| "Compact" → Compact | Koundé, Aramburu |
| empieza por Heavy/Robust, o dice Tank → Strong | Tah, Pubill, Maguire, de Ligt, Koulibaly, van Dijk, Kim |
| Robust/Power con IMC ≥ 25,5 → Strong | Upamecano |
| Lean/Agile con IMC < 22 → Light | Kalulu |
| el resto reconocido → Athletic | Dias, Cubarsí, Romero, Araújo, Marquinhos, Konsa, Timber, Rüdiger |

Un ejemplo de por qué se usa el IMC: Timber ("Lean / Agile Technical", 1,79 m y 79 kg) y Cubarsí (78 kg) no son Light.

**PlayerId:** `cb-` + nombre en ASCII (`cb-pierre-kalulu`), igual que los porteros usan `gk-`: único, estable y determinista (mismo archivo, mismos ids). El nombre mostrado es el del índice sin apodo (`Cristian Romero`); el nombre completo queda en autoría.
**Edad:** sale de la fecha de nacimiento a 2026-10-02 y se contrasta con la edad escrita (coinciden las 20).
**Nacionalidad:** código de tres letras desde una tabla de datos (`NationalityCodes`, compartida con los porteros).
**Signature Actions:** las 50 etiquetas del documento se normalizan a 22 etiquetas conceptuales (`DefenderVocabulary`; p. ej. "Recovery Run", "Recovery Run Sprint" y "Open-Field Recovery Sprint" → `RecoveryRun`; "Surgery Long Pass" es una errata de "Surgical Long Diagonal" → `LongDiagonalPass`). Cada jugador tiene 3 (máximo 5). Solo una etiqueta equivale a un comportamiento que ya existe (`HighPressing` → `AggressivePress`, Aramburu); las demás quedan como etiquetas de autoría y generan un aviso `SignatureActionsPending` por jugador. No son código, atributos ni mecánicas.

## 7. Correcciones realizadas (15)

Detalle completo, con fuente y razón, en `FS27_DEFENDERS_CORRECTIONS_V1.md`. **No se cambió ningún atributo ni ningún Target OVR.**

| Jugador | Campo | Antes → Después | Clase | Razón |
|---|---|---|---|---|
| Koulibaly | altura | 196 → 186 cm | Verified (Wikipedia 1,86 m; el sitio de la liga saudí da 1,95: las fuentes discrepan) | 1,96 m lo haría más alto que van Dijk |
| Koulibaly | dorsal | 26 → 3 | Verified (perfil actual en Al-Hilal) | el archivo da el número de un club anterior |
| Marquinhos | zona secundaria | Midfield / Wing → Midfield | Design | el contrato permite una sola zona secundaria |
| Tah | afinidad Guardian / Anchor | 84/78 → 78/84 | Inference | su debilidad es "agilidad y giro lentos" (Agilidad 64); Guardian es el rol de agilidad/velocidad |
| de Ligt | Guardian / Anchor | 80/76 → 76/80 | Inference | debilidad de agilidad (68) |
| Koulibaly | Guardian / Anchor | 82/78 → 78/82 | Inference | velocidad 74, agilidad 67 |
| Pubill | Guardian / Wall | 84/82 → 82/84 | Inference | fortaleza y juego aéreo (Fuerza 85, 86 kg), debilidad de agilidad: su rol principal es Wall |
| Dias | Guardian / Wall | 86/82 → 82/86 | Inference | Fuerza 89, "velocidad punta justa"; Anchor 91 sigue de principal |
| Cubarsí | Guardian / Anchor | 92/85 → 85/92 | Inference | pase, visión y técnica (Passing 92, Technique 90) sin ventaja de velocidad: es un Ancla |

Los cambios de afinidad se apoyan en dos cosas, no en una fórmula nueva: el texto del propio archivo y los ratings de rol que ya calcula el juego (Wall/Guardian/Anchor). Las demás afinidades se conservan tal cual (las diferencias entre roles en el cálculo oficial son de 1–3 puntos, así que no hay contradicción clara). Se comprobó el resto de datos: edades coherentes con las fechas de nacimiento; clubes verificados por búsqueda web para Romero (Atlético de Madrid), Pubill (Atlético de Madrid) y Aramburu (Real Sociedad, 1,76 m, nacionalidad venezolana); el resto del archivo se conserva como fuente.

## 8. Validación

Las 20 importaciones cumplen (pruebas): 20 jugadores con ids únicos; 12 atributos completos y en 1–99; solo atributos oficiales; ningún atributo de portero ni defensivo no oficial; BodyType oficial; pie derecho; pie débil, Risk, Creativity y Aggression en 0–100; 1–3 roles oficiales de defensa ordenados por afinidad; zona primaria Defense y como máximo una secundaria; acciones normalizadas; `PlayerSystemValidator.ValidateAll` válido; los 20 defensas conviven con los 20 porteros en un mismo `PlayerLibrary` sin romperse (40 jugadores, 20 perfiles de portero, GRL de portero intacto, un equipo 1 portero + 5 de campo válido); un id repetido se rechaza. Los 90+ son el ~5 % de los 240 valores (Fuerza sobre todo); solo Cubarsí y Saliba tienen dos.

## 9. Errores y avisos del importador

Error = no se puede construir un jugador válido; aviso = se puede, pero hay algo incompleto, normalizado o no soportado. Cada código está clasificado (una prueba lo exige). Avisos reales de la base: `SignatureActionsPending` (20) y `PolyvalenceBeyondZone` (1, Marquinhos). Ningún valor se rellena por defecto.

## 10. Deuda restante

- 21 etiquetas de acción sin comportamiento de campo: son etiquetas conceptuales hasta que exista el sistema de comportamientos defensivos.
- La polivalencia completa y el Target OVR solo viven en autoría (por diseño).
- Tolerancia del aviso de Target OVR (5) y umbrales de IMC del BodyType (25,5 y 22) son valores de partida.
- `PlayerDefinition.TeamId` sigue como pista legacy (ver GOALKEEPER_SYSTEM.md §16).
- Los clubes y dorsales de autoría no se contrastaron todos con fuentes externas; no afectan al contrato.
- Nada se probó en Unity.
