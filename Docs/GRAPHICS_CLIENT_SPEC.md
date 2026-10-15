# Especificación del cliente gráfico 3D (campo, cámaras, modelo, animaciones, estilos)

**Estado: especificación. No hay arte ni cliente 3D.** Cada cifra se marca como *del Core* (existe y se puede comprobar en el código) o *propuesta* (a validar con el prototipo, no es una decisión cerrada). El cliente 3D **solo lee** el Core por `RenderFeed` (`Docs/CORE_RENDER_INTERFACE.md`); nunca decide resultados.

## 1. Campo
**Del Core** (`FieldDimensions`, `BallParameters`): 40 m de largo × 25 m de ancho; portería de 5 m de boca y 2 m de alto; balón de 0,2 m de radio (grande a propósito para leerse); coordenadas con centro en (0,0), X a lo largo, Y a lo ancho, **la altura es el eje Y del mundo** (el balón usa X, Y=altura, Z=ancho).
| Elemento | Especificación (propuesta) |
|---|---|
| Césped | Plano con franjas alternas de 2 tonos (≈ 5 m) para dar sensación de velocidad; textura de baja resolución sin normal map |
| Líneas | Banda, fondo, centro y área; ancho de línea ≥ 0,12 m para verse desde la cámara ancha; **Classic**: todas visibles. **Arena**: el campo está cerrado, así que las líneas de banda y de fondo no son límites (se pueden atenuar) |
| Porterías | Postes y larguero sólidos con la boca de 5 × 2 m del Core; red con un modelo simple de 2 planos (sin simulación de tela). La colisión la decide el Core (`GoalFrameCollider`), nunca el motor gráfico |
| Límites **Arena** | Muro/mamparas transparentes alrededor (el Core hace rebotar el balón, `ArenaWalls`, restitución 0,7); en la boca de la portería no hay muro. Altura visual ≥ 3 m; el balón no sale |
| Límites **Classic** | Sin muro: líneas y un margen de grada/publicidad fuera de las líneas para que el balón se vea salir |
| Sombras | Una sombra circular suave bajo el balón y bajo cada jugador (decal), **imprescindible para leer balones altos** (REQ-0191) |

## 2. Cámaras
**Del Core**: `CameraModeId { Tv, TvClose, Wide, Player, Broadcast, Replay }` y perfiles (`CameraProfileData`): TV = distancia 15, altura 12, FOV 40; TvClose = 10 / 8; Wide = 22 / 18, FOV 45, sesgo al balón 0,3. Suavizados 0,25 s (seguimiento) y 0,35 s (transición); anticipación 0,3 s. `FollowCameraMode` solo implementa el seguimiento; Player, Broadcast y Replay son nombres reservados.
| Cámara | Uso | Propuesta |
|---|---|---|
| Tv (por defecto) | Partido | Perfil del Core; encuadra balón + jugador controlado; sigue el balón con el suavizado del perfil |
| TvClose | Partido cercano | Perfil del Core; para móvil pequeño |
| Wide | Táctica | Perfil del Core; ve ambas áreas a la vez |
| Player | Controlado | Detrás-arriba del jugador controlado; solo si la prueba humana lo pide |
| Broadcast / Replay | Repeticiones, celebraciones | No hasta tener repeticiones (el registro de comandos de la fase 38 permite reproducir un partido) |
**Reglas**: la cámara nunca decide la lógica; en móvil vertical y horizontal el campo completo debe caber en la cámara Wide; el balón alto debe quedar siempre dentro del encuadre (≥ 5 m por encima de la cámara TV no se corta con FOV 40, a comprobar en el prototipo).

## 3. Modelo low-poly
**Del Core**: un único rig `fs27_humanoid_v1`; personajes procedurales por piezas (`AssemblyPlan`) con exportación a GLB; estilos y proporciones en `CharacterSpecification`.
| Aspecto | Propuesta |
|---|---|
| Presupuesto por jugador | 1.200–2.000 triángulos en LOD0 (22 en pantalla ≈ 25–45 k), LOD1 ≈ 600 a más de 25 m |
| Material | 1–2 materiales por jugador (piel/pelo + uniforme) con color plano o textura 256 × 256; sin normal maps |
| Siluetas | Cabeza algo grande, piernas y brazos claros; diferencias de cuerpo moderadas para no cambiar la hitbox (REQ-0931) |
| Uniformes | Ficticios; dos juegos por equipo con contraste a cámara ancha (REQ-0935) |
| Balón | Esfera de 0,2 m con 2 colores de alto contraste; rotación visible según el *spin* del Core |
| Huesos | ≤ 22 por jugador (móvil) |
| Colisión visual | Sin colisiones del motor gráfico; solo posición del Core |

## 4. Animaciones
**Del Core**: `AnimationCatalog.CreateDefault()` ya lista los clips que se necesitan, en tres grupos. El cliente elige por `RenderPlayer.Pose` (locomoción) y `RenderPlayer.LastAction/LastActionAge` (acciones), y modula con `MovementPersonality` (velocidad ×0,9–1,1, zancada, inclinación). **Ninguna animación mueve al jugador** (`RootMotionWeight == 0`): el Core manda.
- **Locomoción (5)**: idle, walk_loop, jog_loop, run_loop, sprint_loop (umbrales de velocidad del catálogo).
- **Movimiento (≈ 8)**: turn_sharp, stop_plant, burst_start, dribble_carry/feint/cut_inside/cut_outside/stop_restart.
- **Acciones de balón (≈ 20)**: control_touch, pass_short/long/wall/through/skill_lob, cross_swing, shot_instep/placed/curved, chip_lift, header_jump, clearance_kick, tackle_stand/slide, intercept_step, block_brace, challenge_shoulder, press_contain, shield_hold, skill_move, gk_dive/catch/throw/rush.
- **Nuevas por las fases 36–37 (propuestas, sin clip)**: `header_pass`, `volley_strike`, `gk_punch` (para `ActionDetail.Punched`), `cross_ground/driven/high` (variantes de `cross_swing`).
- Prioridad de producción: locomoción → pase/disparo/control → entrada/portero → cabezazo/centro → celebraciones. Los eventos de contacto con el balón (`ActionContactEvent`) los *avisa* la animación y los *decide* el Core.
- **Estado de salto**: el Core no tiene un estado de salto (REQ-0185/0195). La animación de cabezazo debe poder arrancar ≈ 0,3 s antes del contacto usando el punto de caída de `AerialPlay`; si no, el salto se verá tarde. Decisión pendiente: añadir un estado `Jumping` al Core o resolverlo solo en la presentación.

## 5. Estilos Arena y Classic
| | Arena | Classic |
|---|---|---|
| Del Core | Campo cerrado con rebote, sin saques de banda/córner/puerta, 2 × 6 min sin descanso, sin faltas | Campo con líneas, reanudaciones, faltas y tarjetas, 2 × 8 min con descanso |
| Look (propuesta) | Mamparas, luces de neón, césped oscuro, colores saturados, efectos más marcados | Estadio abierto, césped natural en franjas, paleta más sobria |
| Cámara por defecto | Wide (la partida es de espacio cerrado) | Tv |
| Efectos | Estela del balón y chispas en los muros | Marcas de césped sencillas |
| Audio/HUD | Ritmo rápido | Ambiente de estadio |
Ambos estilos usan **el mismo modelo, rig y animaciones**; solo cambian entorno, iluminación y efectos. Las reglas siguen en el Core (`GameModeRules`); el cliente solo lee `RenderFrame.Mode`.

## 6. Criterios de aceptación del prototipo visual (todos medibles)
1. Campo completo y balón visibles en la cámara Wide en 844 × 390 y en 390 × 844.
2. Un balón alto se lee (sombra + posición) en 3 cámaras.
3. 12 jugadores + balón a ≥ 30 fps en el dispositivo móvil más modesto elegido (§4 de `ENGINE_EVALUATION.md`).
4. El resultado y la secuencia de eventos son idénticos con y sin el cliente 3D (`MatchChecksum`).
5. Ninguna lógica de juego en el cliente (revisión de código + prueba de contrato).
