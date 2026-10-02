# Semantic Prompt Engine

> Vocabulario ≠ inteligencia. Una tabla de frases ("rápido" → speed) no entiende *"no demasiado musculoso"*, *"fuerte en los duelos"* ni *"muy alto y extremadamente bajo"*. Por eso el motor tiene una **capa semántica** encima del vocabulario: el vocabulario es **dato** (paquetes de idioma) y el significado es un **programa estructurado** que se compila de forma determinista.

## 1. Piezas

| Pieza | Qué es |
|---|---|
| `SemanticProgram` | El significado de una petición como datos: `SemanticCommand` (intención, objetivo, operación, dirección, magnitud, negación, valor, sentido, referencia, confianza), `SemanticRelation` (contraste, compromiso, condición), `SemanticConstraint` (preservar, solo esto, todo lo demás) |
| `ConceptCatalog` | Los **conceptos** de los que se puede hablar (`height`, `speed`, `dribbling`, `hairStyle`, `role.winger`, `gk.reflexes`...). Cada concepto tiene **sentidos** por dominio (Visual / Gameplay / Animation), fase (Duelos, Con balón, Presión...) y clave (`tendency` / `ability`), con efectos **primarios** y **colaterales** |
| `LanguagePack` (ES, EN) | Solo palabras: operadores, intensificadores, negadores, contrastes, pistas de contexto, sustantivos con sus modificadores, valores, colores. Añadir un idioma = escribir otro paquete (hay una prueba con portugués) |
| `SemanticParser` | Construye el `SemanticProgram` desde las palabras (offline, determinista) |
| `SemanticCompiler` | `SemanticProgram` + borrador actual → `CharacterSpecificationPatch` + conflictos + no soportado + preguntas + `InterpretationReport` |
| `MagnitudeEngine` | Único lugar donde "un poco" o "muchísimo" se vuelven números |

Cualquier intérprete (parser, modelo de lenguaje, editor) solo tiene que producir un `SemanticProgram`: **todo lo demás es compartido**.

## 2. Magnitud (escala documentada y centralizada)

| Nivel | ES | EN | Delta (relativo) | Absoluto |
|---|---|---|---|---|
| Minimal | un pelín, apenas | barely | 0,04 | 0,15 |
| Slight | ligeramente, poquito | slightly | 0,08 | 0,25 |
| Little | un poco, algo | a little, a bit | 0,12 | 0,35 |
| Moderate | (sin palabra) | (no word) | 0,20 | 0,50 |
| Quite | bastante | quite, fairly | 0,30 | 0,65 |
| Much | muy, mucho | very, a lot | 0,40 | 0,75 |
| VeryMuch | muchísimo | hugely | 0,50 | 0,85 |
| Extreme | extremadamente | extremely | 0,65 | 0,95 |
| Maximum | al máximo | to the max | 1,00 | 1,00 |

*Delta* = fracción del rango de un parámetro que se mueve en un cambio relativo ("hazlo más alto"). *Absoluto* = cuánto se aleja del valor neutro, como fracción del camino hasta el extremo, en una descripción ("muy alto"). Los números son valores de partida, ajustables (`MagnitudeTable`); cambiar uno cambia todos los idiomas.

## 3. Lo que entiende (con salidas reales)

| Petición | Programa |
|---|---|
| `no demasiado musculoso` | `Set muscularity Much NEG [Visual]` → un **límite** (no sube más de ~0,65), no una inversión |
| `que no sea muy alto` | igual: límite suave; en un personaje medio no cambia nada |
| `no alto` | negación simple = **lo contrario** al mismo nivel |
| `sin barba` | `Set beard = none` (quitar) |
| `no quiero que driblee tanto` | `Avoid/Decrease dribbling Moderate sense=tendency` → baja la **tendencia**, no la habilidad |
| `creativo pero no arriesgado` | `Set creativity` + `Set risk NEG` + relación `Contrast` |
| `quiero que se vea fuerte` | `Set strength [Visual]` → músculo/hombros; **no** el atributo Fuerza |
| `fuerte en los duelos` | `Set strength [Gameplay] @Duels` → atributo Fuerza + protección del balón |
| `quiero un jugador fuerte` | juego primero (confianza 1,0) + una lectura visual más débil (0,6) |
| `corra rápido` | un solo comando de velocidad (el verbo y el adjetivo se fusionan) |
| `hazlo más alto` | `Increase height` relativo sobre el personaje actual |
| `cámbiale el pelo` | `Replace hairStyle` (otra opción, elegida con semilla, distinta de la actual) |
| `como el anterior pero con pelo largo` | `Create ref=Previous` + `hairLength` |
| `mantén la cara` / `mantén todo lo demás` / `solo cambia su estilo` | restricciones `Preserve face` / `AllElse` / `OnlyThese` |
| `hazlo mucho más rápido pero sin perder fuerza` | `Increase speed Much` + `Preserve strength` + relación `TradeOff` |
| `más pequeño` | `Decrease overallSize` (altura y volumen) |

**Convergencia:** `más rápido`, `quiero que sea más rápido`, `hazlo rápido`, `dale más velocidad`, `quiero que corra más` (y `make him faster`, `give him more speed`...) producen **el mismo** `Increase speed` y el mismo personaje (pruebas).

## 4. Reglas de negación

- Negación + nivel alto (`no muy`, `no demasiado`, `not too`) → **límite**: el valor no pasa de `1 − nivel` (+0,05) del camino al extremo; solo baja si ya estaba por encima.
- Negación sin magnitud → **dirección contraria** al mismo nivel.
- `poco`, `muy poco` → **voltea** la dirección ("poco agresivo" = agresividad baja).
- Negación de un verbo de juego (`no quiero que driblee`) → `Avoid` (menos de esa tendencia).
- `sin X` sobre algo que se puede poner o quitar (barba, pelo, comportamientos, roles) → **quitar**.
- `no más rápido` → no pasar del valor actual.

## 5. Contexto y sentidos

El mismo concepto significa cosas distintas según el contexto, y nunca se mezclan en silencio:
- **Dominio:** `se vea`, `luzca`, `look`... → Visual; `juegue`, `en el campo`, `plays`... → Gameplay.
- **Fase:** `en los duelos`, `con balón`, `sin balón`, `al defender`, `al atacar`, `bajo presión`, `en sprint` (y sus equivalentes en inglés).
- **Sentido:** `driblador` (habilidad) ≠ `que regatee mucho` (tendencia).
- Si un concepto **no tiene** sentido en el dominio pedido (`que se vea creativo`) → se informa como **no soportado**, no se inventa.
- Si no hay sentido para la fase pedida → se aplica en general y se **avisa**.

## 6. Conflictos y límites honestos

`SemanticConflict` (tipo, severidad, objetivos, mensajes, estado de resolución):
- **Contradicción** (`muy alto y extremadamente bajo`): error, `NeedsUserInput`; ninguna de las dos se aplica; se pregunta cuál.
- **Quitar y añadir** (`sin pelo pero con pelo largo`): igual.
- **Tensión:** un efecto colateral contra algo pedido directamente → **gana lo pedido directamente**, el colateral se descarta (informativo).
- **Violación de "mantén":** se bloquea el cambio y se registra (gana lo que se pidió conservar).
- `UnsupportedCapability` (razón, sistema necesario, implementación futura sugerida): `hazlo más joven` (no hay edad ni caras por edad), animaciones de velocidad de reproducción...
- Palabras desconocidas → `Unparsed`; ambigüedades (`largo` sin saber de qué) → `Ambiguities`; ambas **bajan la confianza** y generan preguntas. Por debajo de la confianza mínima (0,5) no se aplica.

**Límites:** entiende lo que sus paquetes de idioma conocen. No resuelve ironía, comparaciones con personas reales ("como un crack"), números con unidades ("1,85 m") ni referencias largas. Para eso está el contrato de modelo de lenguaje: su salida pasa por los mismos validadores.

## 7. Modelo de lenguaje como intérprete (contrato)

Ver [AUTHORING_PIPELINE](AUTHORING_PIPELINE.md#modelos-de-lenguaje). Resumen: `LlmSemanticInterpreter` pide un `SemanticProgram` en JSON con un esquema estricto, valida (esquema → semántico → dominio → simulación de aplicación), repara hasta 2 veces mostrando los errores al modelo y, si no hay forma, usa el parser offline o pide reformular. **Nunca se ha conectado a un servicio real.**
