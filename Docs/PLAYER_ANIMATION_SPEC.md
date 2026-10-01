# FS27 — Especificación de animaciones del primer jugador

**Solo diseño: no hay animaciones implementadas.** Este documento fija nombres, propósito, parámetros y transiciones,
para que arte, animador y código hablen el mismo idioma. Las acciones de balón (pase, tiro, control…) dependen de sistemas de
gameplay que **todavía no existen**; aquí se define el contrato de animación, no su lógica. Las cifras de tiempo son objetivos iniciales,
a confirmar jugando en Unity.

## 1. Principios

1. **El gameplay manda, la animación obedece.** Las animaciones **no** mueven al jugador (*sin root motion*): `PlayerMovement` decide posición y rumbo y el clip se adapta (ver §6).
2. **Sin botón de sprint.** El sprint se anima a partir de la **velocidad** (que sale de la intensidad del joystick) y del estado de stamina. Ninguna animación ni parámetro depende de un input de sprint.
3. **Poses exageradas, legibles a ~110 px** ([VISUAL_STYLE.md](VISUAL_STYLE.md) §15): lo que se ve en TV son inclinación del torso, zancada y brazos.
4. **Un esqueleto, un set de clips** compartido por todos los jugadores; la personalidad individual se añade con variaciones (celebraciones, idles) y con la velocidad de reproducción.
5. **Barato**: autoría a 30 FPS, clips comprimidos, máx. 2 capas de Animator, parámetros por *hash* ([PLAYER_ASSET_SPEC.md](PLAYER_ASSET_SPEC.md) §10).

## 2. Convención de nombres

Clip: `FS27_Player_<Categoría>_<Nombre>` — categorías `Loco`, `Ball`, `React`.
Ejemplos: `FS27_Player_Loco_Run`, `FS27_Player_Ball_PassShort`, `FS27_Player_React_Celebrate_A`.
Estados del Animator: el mismo nombre sin prefijo (`Loco_Run`). Parámetros en `PascalCase`.

## 3. Parámetros del Animator

**Existentes hoy** (los escribe `PlayerAnimation`, ya implementado):

| Parámetro | Tipo | Origen |
|---|---|---|
| `Speed` | float 0..1 | velocidad actual ÷ velocidad máxima del jugador (`PlayerRuntimeState.Speed / PlayerStats.TopSpeed`) |
| `Sprinting` | bool | `PlayerRuntimeState.IsSprinting` (zona de sprint del joystick + stamina), nunca un botón |

**Propuestos** (futuros, derivados del estado ya existente en Core salvo indicación):

| Parámetro | Tipo | Significado / origen |
|---|---|---|
| `Exhausted` | bool | `IsExhausted`: stamina agotada |
| `Stamina01` | float | `Stamina01`: respiración/cansancio gradual (capa aditiva) |
| `Accel` | float −1..1 | signo y magnitud de la variación de velocidad (acelerando / frenando), suavizado |
| `TurnRate` | float −1..1 | velocidad de giro del rumbo (izquierda − / derecha +) |
| `HeadingError` | float −180..180° | ángulo entre el rumbo actual y el deseado (cambios de dirección) |
| `HasBall` | bool | **futuro**: el jugador lleva el balón controlado (gameplay de balón) |
| `ShotPower` | float 0..1 | **futuro**: potencia del golpeo, para elegir/mezclar la variante |
| `CelebrationIndex` | int | variante de celebración |

**Disparadores (*triggers*, todos futuros):** `Pass`, `PassLong`, `Shoot`, `ShootPower`, `ShootPlaced`, `Receive`, `Trap`, `ControlFail`, `Hit`, `Stumble`, `Celebrate`, `Frustrated`.
**Eventos de animación** (en el clip): `BallContact` (instante exacto del golpeo/toque: lo usa el gameplay para sincronizar), `FootstepL`/`FootstepR` (sonido y polvo).

## 4. Catálogo de animaciones

Prioridad: **P0** = necesaria para el primer personaje jugable · **P1** = siguiente · **P2** = pulido. Duración = ciclo (bucle) o clip completo (una vez). Estado en *Loop*: ↻ bucle, ▶ una vez.

### 4.1 Movimiento (`Loco_*`) — se puede animar ya (la lógica existe)

| Clip | Pri. | Tipo | Duración | Propósito / pose | Se activa con |
|---|---|---|---|---|---|
| `Loco_Idle` | P0 | ↻ | ~2,0 s | parado, respiración y cambio de peso; listo para actuar | `Speed ≈ 0` |
| `Loco_Walk` | P0 | ↻ | ~1,0 s | paso tranquilo (joystick apenas desplazado) | `Speed` ≈ 0,10 |
| `Loco_Jog` | P0 | ↻ | ~0,80 s | trote ligero, torso casi vertical | `Speed` ≈ 0,25 (fin del tramo trote) |
| `Loco_Run` | P0 | ↻ | ~0,70 s | carrera sostenida, ligera inclinación | `Speed` ≈ 0,65 (fin del tramo carrera) |
| `Loco_Sprint` | P0 | ↻ | ~0,55 s | carrera máxima: torso inclinado, brazos amplios, zancada larga | `Speed` → 1,0 |
| `Loco_AccelStart` | P1 | ▶ | ~0,5 s | impulso al arrancar (inclinación hacia delante, primeros pasos potentes) | `Accel` alta desde `Speed` bajo |
| `Loco_DecelStop` | P1 | ▶ | ~0,45 s | frenada: pasos cortos, cuerpo hacia atrás, brazos de equilibrio | `Accel` muy negativa hacia `Speed ≈ 0` |
| `Loco_TurnL` / `Loco_TurnR` | P1 | ▶ | ~0,4 s | giro en el sitio / a baja velocidad (~90°) | `\|TurnRate\|` alto con `Speed` bajo |
| `Loco_CutL` / `Loco_CutR` | P1 | ▶ | ~0,35 s | **cambio de dirección** a velocidad: planta y empuje lateral | `\|HeadingError\|` 25–150° con `Speed` alto |
| `Loco_Reverse` | P2 | ▶ | ~0,5 s | cambio de sentido casi total (~180°) | `\|HeadingError\|` > ~150° |
| `Loco_LeanL` / `Loco_LeanR` | P2 | aditivo | – | inclinación suave en curvas | `TurnRate` (capa aditiva) |

Anclajes con el sistema actual (ya medidos en los tests, atributos 70): llegar a casi la velocidad máxima tarda ~**1,2 s**; frenar desde sprint ~**0,5 s**.
`Loco_AccelStart` y `Loco_DecelStop` deben durar parecido, para que animación y movimiento no se contradigan. Los giros bruscos ya **reducen la velocidad** en el modelo
(invertir la marcha casi la frena), así que `Loco_Reverse` coincide con esa pérdida de velocidad.

### 4.2 Balón (`Ball_*`) — contrato futuro (los sistemas de gameplay aún no existen)

| Clip | Pri. | Tipo | Duración | Propósito | Parámetros / evento |
|---|---|---|---|---|---|
| `Ball_DribbleJog` / `Ball_DribbleRun` / `Ball_DribbleSprint` | P1 | ↻ | como su `Loco_*` | conducción: toques cortos con el pie, mirada al balón; **un toque por zancada** | `HasBall`, `Speed`; evento `BallContact` en cada toque (hoy lo decide `DribbleTouchModel`) |
| `Ball_Trap` | P1 | ▶ | ~0,5 s | controlar el balón (amortiguar con pie/pecho) | trigger `Trap` |
| `Ball_Receive` | P1 | ▶ | ~0,4 s | preparación para recibir un pase (cuerpo abierto, pie listo) | trigger `Receive` |
| `Ball_Shield` | P2 | ↻ | ~1,2 s | proteger el balón: cuerpo bajo, brazo extendido | `HasBall` + estado de presión |
| `Ball_PassShort` | P1 | ▶ | ~0,5 s | pase raso, pierna recta, seguimiento corto | trigger `Pass`; `BallContact` ≈ 0,2 s |
| `Ball_PassLong` | P1 | ▶ | ~0,7 s | pase largo/centro: balanceo amplio, elevación | trigger `PassLong`; `BallContact` ≈ 0,3 s |
| `Ball_Shot` | P1 | ▶ | ~0,6 s | tiro estándar | trigger `Shoot` |
| `Ball_ShotPower` | P1 | ▶ | ~0,8 s | tiro potente: carga de pierna, cuerpo atrás, golpeo seco | trigger `ShootPower`; `ShotPower` |
| `Ball_ShotPlaced` | P1 | ▶ | ~0,6 s | tiro colocado: interior del pie, apunta, golpeo suave | trigger `ShootPlaced` |
| `Ball_ControlFail` | P2 | ▶ | ~0,6 s | mal control: el balón se escapa, tropezón corto | trigger `ControlFail` |

Estas animaciones se **disparan por eventos de gameplay**; la animación no decide si el tiro sale. `BallContact` es el único acoplamiento: marca el instante en que el
gameplay aplica el golpeo, de modo que balón y pie coincidan visualmente.

### 4.3 Reacciones (`React_*`)

| Clip | Pri. | Tipo | Duración | Propósito | Se activa con |
|---|---|---|---|---|---|
| `React_Celebrate_A/B/C` | P1 | ▶ | 2–4 s | celebración de gol (puño, carrera con brazos abiertos, deslizamiento…); la personalidad del jugador | `Celebrate` + `CelebrationIndex` |
| `React_Hit` | P2 | ▶ | ~0,5 s | recibir un golpe/choque: sacudida del torso | `Hit` |
| `React_Stumble` | P2 | ▶ | ~0,9 s | perder el equilibrio y recuperarlo | `Stumble` |
| `React_TiredIdle` | P1 | ↻ | ~2,0 s | cansancio parado: manos en las rodillas, respiración fuerte | `Exhausted` y `Speed ≈ 0` |
| `React_TiredRun` | P1 | ↻ | ~0,8 s | carrera cansada: zancada corta, hombros caídos | `Exhausted` en movimiento |
| `React_Frustrated` | P2 | ▶ | ~0,9 s | frustración: manos a la cabeza / puñetazo al aire | `Frustrated` |

## 5. Estructura del Animator y transiciones

Dos capas como máximo:
- **Base** (cuerpo completo): máquina de estados de locomoción + balón + reacciones.
- **Aditiva** (opcional, P2): respiración según `Stamina01` y *lean* en curvas. Se desactiva en gama baja.

### Base — locomoción (corazón del sistema, P0)

`Locomotion` es un **árbol de mezcla 1D por `Speed`**:

```
Speed:  0.00 ── Idle
        0.10 ── Walk
        0.25 ── Jog
        0.65 ── Run
        1.00 ── Sprint
```

Los umbrales coinciden con el tuning actual (`JogSpeedFraction = 0.25`, `RunSpeedFraction = 0.65`), así que mover el joystick de forma progresiva recorre
trote → carrera → sprint **sin interruptor** (igual que la velocidad real). `Sprinting` solo refuerza efectos (respiración, brazos), no cambia de clip.

| De → A | Condición | Mezcla | ¿Interrumpible? |
|---|---|---|---|
| `Locomotion` → `Loco_AccelStart` | `Accel` > 0,6 y `Speed` < 0,25 | 0,08 s | sí |
| `Loco_AccelStart` → `Locomotion` | fin del clip o `Speed` ≥ 0,5 | 0,15 s | – |
| `Locomotion` → `Loco_DecelStop` | `Accel` < −0,6 y `Speed` > 0,5 | 0,08 s | sí (si `Speed` vuelve a subir) |
| `Loco_DecelStop` → `Locomotion` | fin del clip | 0,10 s | – |
| `Locomotion` → `Loco_TurnL/R` | `\|TurnRate\|` > 0,5 y `Speed` < 0,25 | 0,10 s | sí |
| `Locomotion` → `Loco_CutL/R` | `\|HeadingError\|` entre 25° y 150°, `Speed` > 0,5 | 0,06 s | no hasta ~60 % |
| `Locomotion` → `Loco_Reverse` | `\|HeadingError\|` > 150° y `Speed` > 0,3 | 0,08 s | no |
| `Locomotion` → `React_TiredRun` / `React_TiredIdle` | `Exhausted` | 0,25 s | sí |
| `React_Tired*` → `Locomotion` | no `Exhausted` | 0,30 s | – |
| Cualquier estado → `Ball_*` (una vez) | trigger correspondiente | 0,05–0,10 s | no hasta `BallContact` |
| `Ball_*` → `Locomotion` | fin del clip | 0,15 s | – |
| Cualquier estado → `React_Celebrate_*` | `Celebrate` | 0,20 s | solo por reinicio de partido |
| Cualquier estado → `React_Hit` / `Stumble` | `Hit` / `Stumble` | 0,05 s | prioridad sobre `Ball_*` |

Prioridad de estados (de mayor a menor): `Celebrate` > `Stumble`/`Hit` > acciones `Ball_*` > `Loco_Cut/Turn/Reverse/Accel/Decel` > locomoción base > cansancio.
Las transiciones usan **histéresis** en los umbrales (p. ej. entrar en `DecelStop` a −0,6 y salir a −0,3) para evitar parpadeos con el joystick.

## 6. Sincronización con el movimiento (sin deslizamiento de pies)

El jugador se mueve por código; para que los pies no patinen, la **velocidad de reproducción** de cada clip de locomoción se escala:

```
playbackSpeed = velocidadReal(m/s) / velocidadDeReferenciaDelClip(m/s)
```

Velocidades de referencia para un jugador tipo (atributo Speed 70, velocidad máxima ≈ 8,5 m/s): trote ≈ 2,1 m/s, carrera ≈ 5,5 m/s, sprint ≈ 8,5 m/s.
Cada jugador, con su propio `TopSpeed`, reproduce los mismos clips a distinta velocidad (la personalidad de un jugador rápido sale gratis). Margen de escala recomendado: 0,8–1,25.

## 7. Cómo se conectará (sin implementarlo ahora)

`PlayerAnimation` (ya existente, escribe `Speed` y `Sprinting`) se ampliará o sustituirá por el *driver* de animación de la capa de presentación
([PLAYER_PRESENTATION_ARCHITECTURE.md](PLAYER_PRESENTATION_ARCHITECTURE.md)), que traduce estado de gameplay → parámetros. Esa traducción
(umbrales, histéresis, suavizado de `Accel`/`TurnRate`) es lógica pura y se podrá probar en Core sin Unity cuando toque.

## 8. Orden de producción propuesto

1. **P0 locomoción**: `Idle`, `Walk`, `Jog`, `Run`, `Sprint` + árbol de mezcla (suficiente para ver al personaje correr en la Fase 1).
2. P1 locomoción: arranque, frenada, giros y cambios de dirección; cansancio.
3. P1 balón (cuando existan pase/tiro): dribbling, pase corto/largo, tiros, control.
4. Celebraciones y reacciones.
5. P2: pulido aditivo (respiración, *lean*), `Reverse`, mal control, frustración.
