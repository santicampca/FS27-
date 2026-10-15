# Qué se puede diseñar en paralelo para el prototipo visual 3D

El Core no depende de ningún asset (la capa Unity solo lee estado; `Tests/UnityLayerCheck` vigila que el Core no use tipos de Unity). Por eso el arte puede empezar ya. Esta tabla separa lo que **no espera a nada**, lo que **espera a una decisión pequeña** y lo que **depende de sistemas aún incompletos**. Es un plan, no arte hecho: no hay modelos, texturas, audio ni animaciones finales en el repositorio (REQ-0921 a 0940 siguen sin cumplirse como arte).

## 1. Puede empezar ya (no depende de más gameplay)
| Trabajo | Requisitos | Base que ya existe |
|---|---|---|
| Dirección de arte: estilo cartoon 3D moderno, proporciones y siluetas legibles desde cámara de partido | 0921, 0922, 0924 | `CHARACTER_SPECIFICATION.md`, `CHARACTER_GENERATION.md` |
| Uniformes ficticios, caras originales, paleta con contraste a cámara ancha | 0923, 0925, 0935 | los jugadores y equipos son datos ficticios bloqueados (`DATA_LOCK*.md`) |
| Balón: tamaño y contraste | 0932 | radio 0,2 m en `BallParameters` (la hitbox ya existe) |
| Campo: líneas, áreas y límites; postes y red | 0933, 0934 | dimensiones y colisión de marcos en el Core (`GoalFrameCollider`) |
| Rig común y biblioteca de animaciones base (correr, giro, golpeo, cabezazo, salto, parada) | 0930, 0937 | `ANIMATION_ARCHITECTURE.md`, `ANIMATION_MOVEMENT_ARCHITECTURE.md`: el Core ya emite acciones con tipo y calidad |
| Presupuestos móviles: triángulos, LOD, texturas comprimidas, partículas | 0927, 0928, 0936, 0940 | sin medir todavía en un aparato real |
| Licencias de recursos | 0938 | decisión humana: registrar origen y licencia de cada recurso |

## 2. Espera una decisión pequeña
- Cámara por defecto y zoom (afecta a la silueta y al tamaño del balón): decidir tras la primera prueba humana del HTML.
- Cuántos modelos de cuerpo (REQ-0931, «diferencias moderadas»): decidir con diseño de equipos.
- Estilo de celebraciones y su duración: depende del tono del juego.

## 3. Depende de sistemas del Core
| Trabajo | Depende de | Estado |
|---|---|---|
| Animar el cabezazo, la volea, los puños, el salto | la fase 37 (acciones con calidad y tipo ya emitidas) y de un **estado de salto** (REQ-0185, 0195) | falta el estado de salto en el Core |
| Mostrar balones altos legibles (sombra, indicador) | la cámara 3D (REQ-0191) | el punto de caída ya se calcula (`AerialPlay`) |
| Animar centros y tipos de pase | las acciones `Cross`/`LongPass` con estilo (fase 36) | listas |
| Reacciones de portero (blocaje, despeje) | animación de portero | `GoalkeeperCatch/Save`, `Punched` listos |
| Sincronizar dos clientes con el mismo partido | red (no existe) | solo las bases (fase 38) |

## 4. Contrato entre arte y Core (para no reescribir lógica, REQ-0939)
- El Core produce **estado y eventos** (posiciones, velocidades, acciones con tipo/calidad, eventos de partido). El arte los consume; nunca decide resultados.
- Un asset se sustituye cambiando el modelo/animación enlazado a un identificador (jugador, balón, campo), sin tocar `Assets/_Project/Scripts/Core`.
- Orden recomendado de integración: (1) campo y balón con cámara fija, (2) un jugador con rig común y locomoción, (3) acciones de balón, (4) portero, (5) aéreo, (6) repeticiones y celebraciones.

## 5. Lo que necesita a una persona
Elegir estilo definitivo, aprobar siluetas, crear o encargar modelos, comprobar licencias y probar en un móvil real el coste en fotogramas. Nada de esto se puede dar por hecho desde el Core.
