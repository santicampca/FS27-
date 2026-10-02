# CharacterSpecification

El contrato central del Creator Engine: una descripción **completa, independiente del renderer y serializable** de un personaje. La puede producir un intérprete de prompts, un editor, un importador o una persona. El motor la valida y solo entonces la usa.

Nunca contiene mallas, texturas ni binarios: **describe** (ids, números y colores `#RRGGBB`).

## 1. Estructura

| Campo | Contenido |
|---|---|
| `schemaVersion` | `"FS27.CharacterSpecification.v1"` |
| `characterId` | id único del personaje |
| `playerId` | el `PlayerDefinition` al que pertenece (vacío = borrador) |
| `baseModelId` | modelo base (por id; reemplazable). Hoy `fs27_base_a` |
| `appearance` | `PlayerAppearance` (abajo) |
| `footballDna` | `FootballDNA` (ver [FOOTBALL_DNA.md](FOOTBALL_DNA.md)) |
| `authoring` | solo en datos de autoría: origen, generador, prompt, notas, `ReferenceProfile`. **No entra en runtime.** |

`PlayerAppearance`: `styleId`, `params` (valores escalares), `choices` (pieza por ranura, por id) y `colors` (color por ranura).

Lo que **no** está (a propósito, para no duplicar `PlayerDefinition`): nombre, dorsal, edad, altura en cm, peso, pie, atributos, `BodyType`. La apariencia los *lee* para dar valores iniciales; el dorsal siempre es el del jugador.

## 2. Parámetros de apariencia (35)

Escalas relativas al modelo base (1 = el modelo base) o deslizadores 0–1. Valores iniciales de la tabla = "neutro"; el estilo `FS27_CARTOON_SPORTS` arranca con cabeza ×1,08 y manos y pies ×1,10.

| Id | Rango | Neutro | Qué es |
|---|---|---|---|
| `body.height` | 0.85 – 1.15 | 1.0 | Overall height scale. |
| `body.mass` | 0.85 – 1.25 | 1.0 | Overall thickness (weight) scale. |
| `body.shoulderWidth` | 0.85 – 1.20 | 1.0 | Shoulder width. |
| `body.torsoWidth` | 0.85 – 1.20 | 1.0 | Torso width. |
| `body.muscularity` | 0 – 1 | 0.5 | 0 lean .. 1 very muscular. |
| `body.armLength` | 0.9 – 1.1 | 1.0 | Arm length. |
| `body.legLength` | 0.9 – 1.1 | 1.0 | Leg length. |
| `body.handScale` | 0.9 – 1.4 | 1.0 | Hand emphasis (the style wants slightly big hands). |
| `body.footScale` | 0.9 – 1.4 | 1.0 | Foot/boot emphasis. |
| `head.scale` | 0.9 – 1.35 | 1.0 | Head size relative to the body (cartoon proportions). |
| `head.width` | 0.85 – 1.15 | 1.0 | Head width. |
| `head.height` | 0.85 – 1.15 | 1.0 | Head height. |
| `head.jaw` | 0 – 1 | 0.5 | 0 soft jaw .. 1 strong jaw. |
| `head.chin` | 0 – 1 | 0.5 | 0 small chin .. 1 prominent chin. |
| `head.cheekbones` | 0 – 1 | 0.5 | 0 flat .. 1 prominent cheekbones. |
| `head.forehead` | 0 – 1 | 0.5 | 0 low .. 1 high forehead. |
| `face.eyeSize` | 0.8 – 1.35 | 1.0 | Eye size. |
| `face.eyeSpacing` | 0.85 – 1.15 | 1.0 | Distance between the eyes. |
| `face.eyeTilt` | 0 – 1 | 0.5 | 0 downturned .. 1 upturned. |
| `face.eyebrowThickness` | 0 – 1 | 0.5 | Eyebrow thickness. |
| `face.noseSize` | 0.8 – 1.2 | 1.0 | Nose size. |
| `face.noseWidth` | 0.8 – 1.2 | 1.0 | Nose width. |
| `face.mouthWidth` | 0.85 – 1.2 | 1.0 | Mouth width. |
| `face.mouthHeight` | 0 – 1 | 0.5 | Mouth vertical position. |
| `face.earSize` | 0.8 – 1.25 | 1.0 | Ear size. |
| `face.expressiveness` | 0 – 1 | 0.5 | How strongly the face reads expressions. |
| `hair.length` | 0 – 1 | 0.3 | 0 shaved .. 1 long. |
| `hair.volume` | 0.6 – 1.5 | 1.0 | Hair volume. |
| `skin.tone` | 0 – 1 | 0.5 | Position on the skin tone ramp (light .. dark). |
| `skin.variation` | 0 – 1 | 0 | Subtle visual variation (freckles, blush...), 0 none. |
| `style.realism` | 0 – 1 | 0.25 | 0 not realistic .. 1 realistic. |
| `style.stylization` | 0 – 1 | 0.7 | 0 none .. 1 strongly stylised. |
| `style.exaggeration` | 0 – 1 | 0.35 | Exaggeration of shapes. |
| `style.expressiveness` | 0 – 1 | 0.6 | Facial and body expressiveness. |
| `style.athleticity` | 0 – 1 | 0.7 | Athletic proportions. |

Son conceptuales: **no se ha verificado** que una base 3D concreta pueda producir todos (por ejemplo, formas faciales finas requieren blend shapes que quizá la base no tenga).

## 3. Piezas (ranuras) y colores

Solo ids; no hay assets detrás todavía. Las ranuras son abiertas: una pieza nueva es una fila del catálogo.

| Ranura | Piezas iniciales |
|---|---|
| `body.preset` | light, athletic, strong, tall, compact |
| `head.shape` | athletic_oval, round, angular, square |
| `face.eyeShape` / `face.eyebrow` / `face.nose` / `face.mouth` | 4 cada una |
| `face.expression` | neutral, determined, smile, intense, cheeky |
| `face.beard` | none, stubble, short_beard |
| `hair.style` | buzz_02, short_crop_01, short_curly_07, short_textured_04, medium_wavy_03, long_tied_05, fade_06, afro_08 |
| `hair.texture` | straight, wavy, curly, coily |
| `kit.shirt` / `kit.shorts` / `kit.socks` / `kit.boots` / `kit.gloves` | variantes de uniforme, botas y guantes de portero |

Ranuras de color: `hair.color`, `kit.primary`, `kit.secondary`, `kit.accent`, `kit.boots`, `kit.gloves`, `eyes.color`. El dorsal **no** es una ranura: es `PlayerDefinition.ShirtNumber`.

Una pieza puede declarar con qué modelos base encaja (`PartDefinition.BaseModels`); el validador lo comprueba. Así la base es reemplazable sin tocar `PlayerDefinition`, gameplay ni IA.

## 4. Estilo

`StylePreset` (`FS27_CARTOON_SPORTS`): un look inicial y un **eje cartoon** que mueve varios parámetros a la vez. "Más cartoon" no es `stylization += 0.1`:

| Parámetro | Efecto por unidad de desplazamiento |
|---|---|
| `style.stylization` | +1,0 |
| `style.realism` | −0,8 |
| `style.exaggeration` | +0,6 |
| `style.expressiveness` | +0,5 |
| `face.expressiveness` | +0,4 |
| `head.scale`, `face.eyeSize` | +0,35 (riesgo "infantil") |
| `body.handScale`, `body.footScale` | +0,2 (riesgo "infantil") |

Quedan **intactas** las proporciones atléticas (hombros, torso, piernas, `style.athleticity`, altura, masa). "Pero no infantil" activa una guarda que reduce los parámetros de riesgo infantil a ×0,4 sin tocar los del propio estilo. Un estilo nuevo es una fila de datos.

## 5. JSON

Determinista (claves ordenadas, máx. 4 decimales) y mínimo (solo lo que difiere del neutro). Ejemplo real generado por el sistema (ejemplo 58, runtime, sin autoría; **732 bytes** en compacto):

```json
{
  "appearance": {
    "choices": { "body.preset": "compact", "face.expression": "determined" },
    "colors": {},
    "params": { "body.footScale": 1.1, "body.handScale": 1.1, "body.height": 0.886, "face.expressiveness": 0.65, "head.scale": 1.08 },
    "styleId": "FS27_CARTOON_SPORTS"
  },
  "baseModelId": "fs27_base_a",
  "characterId": "char-001",
  "footballDna": {
    "behaviors": [ { "id": "ExplosiveExit", "weight": 0.78 }, { "id": "BodyFeint", "weight": 0.786 }, { "id": "StopAndGo", "weight": 0.78 }, { "id": "InsideCut", "weight": 0.85 } ],
    "params": { "dribbling.changeOfPace": 0.92, "dribbling.insideCut": 0.92, "dribbling.takeOn": 1, "movement.accelerationTendency": 0.88, "positioning.halfSpace": 0.75, "positioning.width": 0.88 },
    "schemaVersion": "FS27.FootballDNA.v1"
  },
  "schemaVersion": "FS27.CharacterSpecification.v1"
}
```

Un personaje recién creado ocupa 402 bytes; con los datos de autoría, 1.036. Un personaje con **todos** los parámetros, piezas y comportamientos ajustados cabe en menos de 6 KB (test). Los valores fuera de rango que lleguen en un JSON **se conservan** para que la validación los informe, en vez de corregirlos en silencio.

## 6. Versionado y migración

> **Actualización (fase Intelligence):** el esquema vigente es `FS27.CharacterSpecification.v2` / `FS27.FootballDNA.v2` (comportamientos con prioridad/riesgo/enfriamiento/condición, secuencias, confianza por tendencia y tres semillas). Los ficheros v1 **siguen cargando**: el migrador trae registrado el paso v1→v2. El ejemplo JSON de arriba es de v1. Ver [FOOTBALL_DNA](FOOTBALL_DNA.md) y [RUNTIME_PIPELINE](RUNTIME_PIPELINE.md). El catálogo tiene ahora 51 parámetros de apariencia (en 12 grupos).

- Historia: `FS27.CharacterSpecification.v1`, `FS27.FootballDNA.v1`.
- `SchemaMigrator`: pasos registrados `vN → vN+1` sobre el JSON bruto, encadenados. Un archivo viejo sin migración registrada se **rechaza con un mensaje claro**; uno de una versión **más nueva** que la que conoce el build también (`SchemaVersionNewer`): nunca se adivina.
- Ahora existe un paso real v1→v2 (probado con un fichero v1 verdadero) además del paso inventado de los tests del mecanismo.

## 7. Validación

`CharacterSpecificationValidator.Validate` devuelve **todos** los problemas, con código (`CreatorIssueCode`), severidad (error/aviso), sujeto y mensaje. Comprueba esquema, ids, modelo base, estilo, parámetros (desconocidos, dominio equivocado, fuera de rango, NaN), ranuras y piezas (desconocidas, incompatibles con la base), colores y comportamientos. Un comportamiento que existe pero aún no se puede ejecutar es un **aviso**, no un error.

## 8. Cómo se guarda y se vincula

Un JSON por personaje. `CharacterRegistry` enlaza `playerId → characterId` (uno por jugador, ids únicos, solo acepta datos válidos y jugadores que existen en la `PlayerLibrary`). **No es una segunda biblioteca de jugadores.**
