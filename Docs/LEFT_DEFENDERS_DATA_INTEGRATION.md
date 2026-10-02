# FS27 — Integración de los 20 defensas centrales zurdos

Estado: **datos, importación, correcciones documentadas y validación implementados y probados fuera de Unity** (C# puro, `dotnet test Tests/Core.Tests`). Reutiliza el sistema genérico de jugadores de campo de [DEFENDERS_DATA_INTEGRATION.md](DEFENDERS_DATA_INTEGRATION.md): **no hay un segundo importador**. No se tocó gameplay, IA, animación, porteros ni los datos de los defensas derechos.

## 1. Fuente y archivos (`Docs/authoring/`)

| Archivo | Qué es |
|---|---|
| `FS27_LEFT_DEFENDERS_DATABASE_V1.md` | Conversión línea a línea de `fs27di.docx` (20 jugadores + tabla global de validación). **Nunca se edita.** |
| `FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md` | 54 correcciones (jugador, campo, antes, después, clase, fuente, razón). Cada una se aplica solo si el "antes" coincide. |
| `FS27_LEFT_DEFENDERS_RECORDS_V1.json` | Registros definitivos (fuente + correcciones). Un test comprueba que son exactamente eso y que se importan por sí solos. |

## 2. Qué se reutilizó y qué se añadió al sistema genérico

Reutilizado sin cambios de contrato: `FieldPlayerDataRecord`, `FieldPlayerRecordImporter`, `FieldPlayerLibraryBuilder`, `AuthoringCorrections`, `DefenderVocabulary`, `NationalityCodes`, `AuthoringIds`, `RosterValidator`, `PlayerRatingCalculator`.

Añadido al sistema genérico (no específico de zurdos), todo con pruebas:
- **`FieldPlayerCompactMarkdown`:** lector del *layout compacto* de esta fuente (una línea por sección, zonas en español, tabla global). Produce los mismos `FieldPlayerDataRecord`. Contrasta la tabla global con las secciones y registra cada discrepancia como problema de lectura.
- **`BodyTypeNormalizer`, rama sin etiqueta:** esta fuente no trae etiqueta de cuerpo. Solo cuando **no hay etiqueta**, el tipo sale de altura y peso con los mismos límites de IMC de siempre (Strong ≥ 25,5; Light < 22) más dos límites nuevos y exclusivos de esta rama: Tall desde 195 cm y Compact con ≤ 176 cm y IMC ≥ 24. **Las reglas con etiqueta no cambian** (los 20 defensas derechos dan exactamente lo mismo; una prueba lo comprueba) y se avisa en cada caso (`BodyTypeDerivedFromMeasurements`).
- **`PreferredFootAmbiguous`:** un pie ambiguo ("Izquierdo / Derecho", ambidiestro, both) es un aviso con el texto original; se mantiene el valor por defecto y **nunca se convierte en Left**. Un texto que no es un pie sigue siendo error.
- **Avisos nuevos:** `SignatureActionsAbsent`, `NationalityMissing`.
- **`AuthoringCorrections`:** campos nuevos `club`, `nationality`, `preferredFoot`, `naturalPosition` y `signatureActions`.
- `NationalityCodes`: Ecuador, Eslovaquia, Croacia y Nigeria.

## 3. Datos de la fuente y datos que faltan

La fuente trae edad, altura, peso, pie, club, posición, zonas, roles con afinidad, Risk/Creativity/Aggression, los 12 atributos, una frase de estilo y el Target OVR. **No trae** nacionalidad, fecha de nacimiento, pie débil, dorsal, etiqueta de cuerpo, Signature Actions, fortalezas ni debilidades. Lo que falta:
- Pie débil (50) y dorsal (99): defaults explícitos con un aviso por jugador (`WeakFootDefaulted`, `ShirtNumberDefaulted`); **no son datos investigados**. El nombre corto se deriva del apellido (`ShortNameDerived`).
- Nacionalidad: se completó como corrección *Inference* (selección que representa cada jugador; conocimiento general, no buscado).
- BodyType: derivado de altura y peso (ver §2).
- Signature Actions: la fuente no tiene ninguna; se completaron 19 como correcciones *Inference* **solo cuando la frase del propio documento nombra la capacidad** (la razón cita el texto). Veiga no tiene ninguna señal explícita y queda sin acciones (aviso). Son etiquetas conceptuales del vocabulario existente.

## 4. Correcciones (54, ninguna toca un atributo ni un Target OVR)

| Clase | Nº | Qué |
|---|---|---|
| **Verified** | 6 | **Guéhi es diestro** (la fuente decía zurdo; FotMob y Pitch Fame) → `Derecho`. **Huijsen**: la fuente dice zurdo y las fuentes externas dicen "ambos"/diestro → `Ambidiestro (las fuentes discrepan)`, queda como pie ambiguo con aviso y el runtime usa el valor por defecto (Right). Clubes desactualizados: Guéhi → Manchester City (enero 2026), Huijsen → Real Madrid, Hancko → Atlético de Madrid, Blind → Ajax. |
| **Design** | 5 | **Gerard Martín**: la fuente dice LB natural ("Banda / Defensa"; verificado: lateral izquierdo que también juega de central) y la tabla global dice CB. El contrato de esta base fija la zona primaria en Defense: zona primaria Defense, secundaria Wing; su posición natural LB se conserva en autoría. **Blind, Lisandro Martínez y Veiga**: "Defensa / Medular / Banda" → una sola zona secundaria, Wing (LB es su primera posición secundaria, igual que Marquinhos en la base de derechos); CDM queda en autoría. |
| **Inference** | 43 | 4 afinidades (Veiga: Wall 85 / Guardian 84; Schlotterbeck: Wall 86 / Guardian 85; intercambio de dos roles con apoyo en el texto y en los ratings de rol oficiales), 20 nacionalidades y 19 conjuntos de Signature Actions. |

Discrepancias que **no** se corrigieron: Huijsen mide 1,97 m en la fuente y 1,95 m en una fuente externa; Pacho 1,87 m frente a 1,88 m (la fuente se conserva). Los clubes de los demás jugadores y los dorsales no se contrastaron.

## 5. Normalización

- **Zonas:** todos Defense como primaria; como máximo una secundaria. Wing: Calafiori, Lucas Hernández, Blind, Hancko, van de Ven, Hincapié, Lisandro Martínez, Gvardiol, Veiga, Bassey y Gerard Martín (11). Los otros 9 no tienen. Las posiciones reales (LB, CDM, CB) se conservan en el registro de autoría; CDM queda fuera de la zona en tres jugadores (`PolyvalenceBeyondZone`).
- **Posición real:** 19 son CB; Gerard Martín es LB en la fuente. En runtime todos son `PlayerRole.Defender`; nadie se vuelve lateral por la zona.
- **Roles:** solo Wall, Guardian, Anchor; no hay roles por pie dominante. Las demás afinidades se conservan tal cual (la diferencia entre roles en el cálculo oficial es de 1–3 puntos, así que no hay contradicción clara).
- **Pie:** 18 zurdos, Guéhi diestro, Huijsen ambiguo.
- **BodyType:** Tall (Huijsen, Botman), Compact (Lisandro Martínez), Light (van de Ven, Bastoni), Athletic (los otros 15).
- **Atributos:** los 12 oficiales, 1–99, sin cambios. Los 90+ son excepcionales (7 valores en 240: Speed 97 y Acceleration 92 de van de Ven, Strength 93 de Magalhães, y Passing/Technique 90–91 de Blind y de Bastoni; nadie tiene más de dos).
- **PlayerId:** `cb-` + nombre en ASCII, único, sin choques con los derechos ni los porteros (`cb-josko-gvardiol`, `cb-marc-guehi`).

## 6. Target OVR frente a Overall calculado

El Target OVR se guarda aparte (`OverallCheck`) y nunca entra en runtime; el Overall oficial lo calcula `PlayerRatingCalculator`. Ningún atributo se tocó para acercarlos. La diferencia (calculado − objetivo) queda entre −2 y +6; solo **Bassey** supera la tolerancia de 5 (objetivo 76, calculado 82) y genera `TargetOvrDiffers`.

## 7. Validación e integración

Pruebas (22 propias + las del importador genérico): 20 jugadores en orden, ids únicos y normalizados, sin choque con los 20 derechos ni los porteros; 12 atributos oficiales en 1–99 y ningún atributo ajeno; roles oficiales (máximo 3, ordenados); zona primaria Defense y máximo una secundaria; pie, pie débil, Risk/Creativity/Aggression en rango; BodyType oficial; 54 correcciones aplicadas con clase, fuente y razón; registros definitivos = fuente + correcciones; inventario exacto de avisos de la base; no hay nombres de jugadores en el código del importador.

**Convivencia:** los 20 porteros + 20 defensas derechos + 20 zurdos viven en un mismo `PlayerLibrary` (60 jugadores, 20 perfiles de portero, `PlayerSystemValidator.ValidateAll` válido, GRL de portero intacto). Un equipo de **1 portero + 5 de campo** mezclando derechos y zurdos valida, y `TeamDefinition.PlayerIds` sigue siendo la única autoridad del roster (`TeamId` queda vacío; el club es solo autoría).

## 8. Deuda restante

- Pie débil y dorsal de los 20 son defaults, no datos investigados.
- Nacionalidad y Signature Actions son inferencias documentadas; las acciones siguen siendo etiquetas (solo `HighPressing` equivale a un comportamiento existente).
- La polivalencia completa (CDM, LB) y el Target OVR solo viven en autoría.
- Umbrales nuevos de la rama sin etiqueta (195 cm, 176 cm, IMC 24) y tolerancia de 5 del Target OVR: valores de partida sin calibrar.
- Clubes y dorsales no verificados salvo los cuatro corregidos.
- `PlayerDefinition.TeamId` sigue como pista legacy.

> **Actualización (alineación de objetivos):** Calvin Bassey (76 → 82) superaba la tolerancia y su objetivo se alineó al Overall calculado; ya no hay `TargetOvrDiffers` en los centrales zurdos. Ver [OVERALL_ALIGNMENT.md](OVERALL_ALIGNMENT.md).
