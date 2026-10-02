# FS27 Creator Engine

Estado: **primer 50 % implementado y probado fuera de Unity** (C# puro en `Core/Creator`, `dotnet test Tests/Core.Tests`). El otro 50 % (geometría 3D, rig, animación, runtime visual) **no existe**: está definido como contratos y hoja de ruta. No se ha probado nada dentro del editor de Unity.

> El **Creator Engine es de FS27**. No depende de Quaternius, de Ready Player Me, de ningún creador de avatares externo ni de ningún proveedor de IA. Un modelo base (de quien sea) es **un dato reemplazable**; Claude es **un proveedor** que produce datos.

## 1. Objetivo

Que una persona describa un jugador con lenguaje natural y FS27 lo convierta en **datos** validados: apariencia + tendencias futbolísticas. Esos datos se unen al `PlayerDefinition` y al `PlayerPlayingProfile` que ya existen; después, un runtime todavía por construir los convierte en un personaje 3D jugable.

```
PROMPT ─► IAICharacterInterpreter ─► PromptResult ─► CharacterSpecification ─► validación ─► datos del jugador
 (humano)   (determinista hoy;          (cambios,        (apariencia + FootballDNA)                │
             un LLM mañana)             conflictos,                                                ▼
                                        no soportado)             [NO EXISTE AÚN] ensamblador (Unity) → rig → animación → gameplay
```

Regla de seguridad: **la IA produce datos, nunca código**. `Prompt → esquema estructurado → validación → sistemas conocidos`. Nada ejecuta texto del usuario ni código generado (hay tests por reflexión y por escaneo de fuentes).

## 2. Capas

| Capa | Dónde vive | Estado |
|---|---|---|
| A. Prompt Intelligence | `Core/Creator` (`IAICharacterInterpreter`, `DeterministicPromptInterpreter`, `PromptLexicon`) | ✅ funciona (léxico, no comprensión semántica) |
| B. Character Specification | `CharacterSpecification` | ✅ |
| C. Player Appearance | `PlayerAppearance`, `ParameterCatalog`, `AppearanceCatalog`, `StyleCatalog` | ✅ datos y validación; ❌ geometría |
| D. Football DNA | `FootballDNA`, `SignatureBehaviorCatalog`, `BehaviorResolver` | ✅ datos, validación y ranking; ❌ ejecución |
| E. Validación | `CharacterSpecificationValidator`, `FootballDNAValidator`, `PromptSpecificationValidator` | ✅ |
| F. Generación / ensamblado | `ICharacterAppearanceResolver`, `ICharacterRigResolver`, `ICharacterGenerator`, `ICharacterAssembler<T>` | ✅ solo el resolutor de datos; ❌ el resto son contratos |
| G. Animación y movimiento | `MovementSignature`, `IAnimationResolver`, `ActionContactEvent` | ✅ firma de movimiento; ❌ animaciones |
| H. Runtime visual (Unity) | — | ❌ requiere Unity y assets |

## 3. Reutilización y no duplicación

| Ya existía | Cómo se usa |
|---|---|
| `PlayerDefinition` | Fuente de identidad, atributos, `BodyType`, altura, peso y **dorsal**. La apariencia no los copia; `AppearanceDefaults.FromPlayer` los usa como valores iniciales. Se enlaza por `PlayerId`. |
| `PlayerPlayingProfile` | Sigue siendo el único sitio de **Risk, Creativity, Aggression**, zonas y roles. El DNA **no los duplica**: el intérprete los devuelve como *ProfileHint* y `ProfileHintApplier` los aplica al perfil. |
| 12 atributos (`PlayerAttributes`) | Siguen en el jugador. El intérprete devuelve *AttributeHint* (deseos); `AttributeHintApplier` **sugiere** valores, nunca los escribe solo. |
| `PlayerLibrary` | Sigue siendo el único registro de jugadores. `CharacterRegistry` solo enlaza `playerId → characterId`. |
| `PlayerCardData` | Sin cambios: sigue siendo una vista derivada y no guarda apariencia. |
| `PlayerIntent`, `PlayerLocomotion`, `MovementTuning`, stamina | Sin cambios. |
| Ball Core, Match Core, Goalkeeper, Difficulty | Sin cambios (los tests comprueban que el Creator no usa tipos del balón ni del partido). |

Evolución de una decisión previa: `PLAYER_SYSTEM.md` §19 dejaba la elección visual concreta "en su propio catálogo por `PlayerId`". Eso es ahora `CharacterSpecification` (enlazada por `PlayerId`); no hay conflicto, solo concreción.

## 4. Decisiones de diseño

1. **Parámetros como datos.** Un parámetro (id, rango, neutro) es una fila de catálogo. Añadir una tendencia o una proporción = una fila; no hay campos nuevos, ni serializador nuevo, ni validador nuevo. (35 de apariencia + 52 de DNA hoy.)
2. **Solo se guarda lo que difiere del neutro.** Un personaje típico ocupa unos 700 bytes de JSON.
3. **Cambios estructurados, no cadenas.** Un cambio es `objetivo + dirección + magnitud + confianza` (`SpecChange`).
4. **El intérprete es una interfaz.** El motor solo conoce `IAICharacterInterpreter`.
5. **No inventar.** Lo no entendido, lo contradictorio y lo imposible se devuelven aparte (`Unresolved`, `Conflicts`, `Unsupported`).
6. **Sin dependencia de internet ni de claves** en el núcleo.

## 5. Qué funciona hoy y qué no

Funciona (probado): especificación, JSON determinista, migración de esquema, validación, estilo, FootballDNA, catálogo de 15 comportamientos, ranking de comportamientos por contexto, intérprete determinista (crear y modificar), conflictos, magnitudes, contexto, firma de movimiento, registro enlazado a jugadores.

No funciona (y por qué):

| Falta | Motivo |
|---|---|
| Ver un personaje | Necesita Unity, malla base y materiales |
| Rig, animación, retargeting | Necesita assets de animación y Unity |
| Ejecutar un comportamiento (`StopAndGo`…) | Necesita animaciones, IA de jugador y el sistema de acciones (pase/tiro no existen) |
| Un modelo de lenguaje real | Necesita un proveedor y una clave; es opcional y está fuera del núcleo |
| Cualquier "geometría procedural" | Calidad aceptable exige piezas hechas; ver [CREATOR_ENGINE_ROADMAP.md](CREATOR_ENGINE_ROADMAP.md) |

## 6. Documentos

[CHARACTER_SPECIFICATION.md](CHARACTER_SPECIFICATION.md) · [FOOTBALL_DNA.md](FOOTBALL_DNA.md) · [PROMPT_INTELLIGENCE.md](PROMPT_INTELLIGENCE.md) · [CHARACTER_RUNTIME.md](CHARACTER_RUNTIME.md) · [ANIMATION_MOVEMENT_ARCHITECTURE.md](ANIMATION_MOVEMENT_ARCHITECTURE.md) · [AUTHORING_VS_RUNTIME.md](AUTHORING_VS_RUNTIME.md) · [CREATOR_ENGINE_ROADMAP.md](CREATOR_ENGINE_ROADMAP.md)
