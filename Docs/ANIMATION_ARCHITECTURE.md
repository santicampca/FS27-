# Animation Architecture

**Regla de oro:** los personajes no tienen animaciones propias; **modulan** animaciones compartidas. Y la animación **nunca** decide dónde va el jugador: el movimiento viene de `PlayerIntent → Movement → MovementState`, nunca de root motion.

```
Humano / IA → PlayerIntent → Movement → MovementState ──► (solo lectura) ──► AnimationSelectionContext
                                                                                   │
CharacterSpecification ─► MovementSignature ─┐                                     ▼
                       └► MovementPersonality ┴──────────────► CatalogAnimationResolver ──► AnimationCandidate[] / AnimationPlan
                                                                                   │
                                                                         UnityAnimatorAdapter (sin verificar)
```

## Datos (`AnimationModel`)
`AnimationProfile`: id (`run_loop`), estado (`Planned/Placeholder/Available`), etiquetas (`run`, `feint`, `shot`...), acción de fútbol, estilo de movimiento, rigs compatibles, rango de velocidad (m/s) para el que se creó, bucle, duración, `RootMotionWeight` (0: el juego mueve, el clip decora), comportamientos para los que sirve. `AnimationCatalog.CreateDefault()` trae 27 perfiles: locomoción por velocidad (idle, walk, jog, run, sprint), giro, parada, arranque, regates (conducción, finta, recorte dentro/fuera, parada y arranque), escudo, control, pases, centro, tiros, cabezazo, entrada, intercepción, bloqueo y los del portero. **Todos `Planned`: no hay ni un clip.**

## Selección (`CatalogAnimationResolver`, implementa `IAnimationResolver`)
`Select(AnimationSelectionContext)`: velocidad actual, acción, estilo, comportamiento y rig → candidatos ordenados. Reglas: una acción solo admite clips de **su** acción; en locomoción nunca salen clips de acción; el estilo específico suma o descarta; la velocidad dentro del rango suma, fuera resta; una etiqueta que pide el comportamiento (`AnimationTags`) suma; a igualdad, gana un clip que existe sobre uno planeado. Determinista (empates por id).
`Plan(...)` devuelve el `AnimationPlan`: id, velocidad de reproducción (0,8–1,2), escala de zancada, inclinación (solo si se mueve) y `NoClipYet`.

## Modulación del cuerpo
- `MovementSignature` (ya existente): zancada, cadencia, inclinación, agudeza de giro, agresividad de aceleración (de altura, piernas, masa, atributos y DNA).
- `MovementPersonality` (nuevo): `PlaybackSpeedScale` 0,9–1,1, `StrideScale` 0,9–1,1, `LeanDegrees` 0–12, `Bounce`, `ArmSwing`, `Anticipation`, `Posture` 0–1 y `IdleVariant` 0–3 (por semilla). Un jugador ligero rebota más que uno pesado y musculoso. **Solo presentación y todos los campos están acotados.**

## Pruebas que protegen la regla
- Ningún archivo de locomoción, estadísticas, tuning, resistencia o estado de runtime puede mencionar `MovementPersonality`, `AnimationPlan`, `CatalogAnimationResolver`, `AnimationProfile`, `MovementStyle`, `FootballDNA` ni `CharacterSpecification`.
- `MovementPersonality` solo tiene `float`/`int` y ningún campo de "velocidad" de juego.
- Todo perfil de animación tiene `RootMotionWeight == 0`.
- No existe `Sprint` como estilo, acción ni campo del intent.

## Pendiente
Clips reales (locomoción primero), `Animator Controller`, retargeting entre rigs, eventos de contacto con el balón (`ActionContactEvent` ya definido: la animación **avisa**, el Ball Core **decide**) y la verificación del adaptador en Unity.
