# Character Runtime (contratos)

**Estado: solo contratos y un resolutor de datos.** Nada construye una malla, un rig, un animador ni un `GameObject`: eso necesita Unity y assets (fases A–H de [CREATOR_ENGINE_ROADMAP.md](CREATOR_ENGINE_ROADMAP.md)). *Requires Unity runtime/editor validation.*

## 1. Cadena

```
CharacterSpecification
   ↓ ICharacterAppearanceResolver        (Core, implementado: CatalogAppearanceResolver)
ResolvedCharacter                          ids resueltos + valores finales; todavía datos
   ↓ ICharacterRigResolver / IAnimationResolver   (rig: implementado; animación: contrato)
   ↓ ICharacterAssembler<TRuntime>        (Unity: NO existe)
personaje en pantalla
```

`Core` nunca referencia Unity. La capa Unity implementará `ICharacterAssembler<TRuntime>` con su propio tipo (un envoltorio de `GameObject`).

## 2. Qué existe

| Tipo | Estado |
|---|---|
| `ResolvedCharacter` (piezas por ranura, escalas con **todos** los valores finales, colores, avisos) | ✅ |
| `CatalogAppearanceResolver` (valida ids y ajuste al modelo base; las piezas inválidas quedan vacías con aviso, no hay excepción) | ✅ |
| `ICharacterRigResolver` → `BaseModelDefinition.RigId` | ✅ (un rig común: `fs27_humanoid_v1`) |
| `IAnimationResolver`, `AnimationSetReference` | contrato (sin implementación) |
| `ICharacterGenerator` (geometría procedural por ranura) | contrato (sin implementación) |
| `ICharacterAssembler<TRuntime>` | contrato (sin implementación) |
| `BodyCompatibility`, `AnimationCompatibility`, `RetargetingProfile` | datos (sin uso aún) |
| `MovementSignature` + `MovementSignatureResolver` | ✅ |

Un test verifica que **no existe ninguna implementación** de generador, de resolutor de animación ni de ensamblador: nada finge generar geometría.

## 3. MovementSignature

Cómo debe **modular** la animación compartida un cuerpo concreto, sin una animación propia por jugador. Es una pista de presentación: **nunca cambia cuánto ni a qué velocidad se mueve el jugador** (eso es gameplay: atributos y stamina). No contiene velocidad, posición ni stamina (test).

| Valor | Rango | Depende de |
|---|---|---|
| `StrideLength` | 0,8–1,2 | longitud de piernas, altura, `BodyType` (Tall +0,05, Compact −0,05, Strong −0,02) |
| `Cadence` | 0,8–1,2 | inverso de la zancada, Agility, masa |
| `Lean` | 0–1 | `accelerationTendency`, Acceleration |
| `TurnSharpness` | 0–1 | `turningTendency`, `directionChange`, Agility |
| `AccelerationAggression` | 0–1 | `accelerationTendency`, Acceleration |

Ejemplo (jugador A / B del enunciado): A con zancada larga, aceleración agresiva e inclinación alta; B con zancada corta, cadencia alta y giros más nítidos: se obtiene con los mismos datos de entrada, sin animaciones distintas. Las fórmulas son una primera estimación pensada para calibrar con animaciones reales.

## 4. Un jugador nuevo, paso a paso

1. Datos de apariencia → `CharacterSpecificationValidator` (o prompt → `PromptResult` → borrador).
2. `CharacterRegistry.TryAdd` lo enlaza a un `PlayerDefinition` existente.
3. `CatalogAppearanceResolver.Resolve` → `ResolvedCharacter`.
4. *(Unity, futuro)* ensamblador: malla base + piezas + escalas + materiales + rig + Animator.
5. Gameplay mueve al personaje por `PlayerIntent` → movimiento; el Animator solo lee el estado.

Los pasos 1–3 funcionan y están probados; 4–5 requieren Unity.

## 5. Escala

Los jugadores son **datos + referencias a piezas compartidas** (modelo base, pelo, cara, ropa, rig, animaciones): 10, 100 o 1.000 jugadores no aumentan el tamaño del juego en proporción. ~0,7 KB por personaje → 1.000 personajes ≈ 0,7 MB de datos (test: el peor caso con todo ajustado sigue por debajo de 6 KB). La variedad visual real depende del **catálogo de piezas**: con pocas cabezas y peinados se repiten caras aunque cambie el color.

## 6. Rendimiento móvil (diseño; no medido)

El sistema se diseña para **12 jugadores simultáneos** (6v6), no cientos:
- Un solo rig, un modelo base y animaciones compartidas.
- Los parámetros de forma se aplican **una vez al crear** el personaje (escalas por hueso y, si hay blend shapes, horneadas), no por fotograma.
- Materiales compartidos con color por zonas (camiseta, pantalón, medias, piel, pelo, botas) para mantener pocas llamadas de dibujo.
- Sin datos de internet ni lecturas de disco en el partido; sin asignaciones por fotograma en el resolutor (los tipos de resolución son de uso en carga, no por frame).
- ⚠️ **No medido:** coste real de blend shapes, triángulos por personaje y LOD en un móvil. Hay que verificarlo con un prototipo en dispositivo.
