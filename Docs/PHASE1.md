# Fase 1 — Sandbox: movimiento, stamina, balón, cámara

Objetivo: que **correr se sienta bien**. Todo el "feel" se ajusta con números, sin tocar código.

## Dónde se ajusta cada cosa

| Quiero cambiar… | Dónde |
|---|---|
| Velocidad máxima, aceleración, frenado, giro, curva del joystick, stamina | `Assets/_Project/Data/Tuning/DefaultTuning.asset` |
| Atributos del jugador (Speed / Acceleration / Stamina, 1–99) | Prefab `Player` → componente **Player Entity** → Attributes |
| Toques al balón (alcance, fuerza, cuánto se adelanta) | Prefab `Player` → componente **Ball Interactor** |
| Masa, rebote, fricción, diámetro del balón | Prefab `Ball` → **Ball Controller** y **Ball Physics** |
| Cámara (distancia, altura, FOV, suavizado) | `Assets/_Project/Data/Cameras/TvCamera.asset` |
| Tamaño y tamaño del joystick | Escena → `HUD Canvas/SafeArea/JoystickZone` → **Virtual Joystick** |

**Truco:** pulsa Play, selecciona `DefaultTuning.asset` y cambia valores: se aplican al instante.
Al salir de Play los cambios en assets **se conservan**; los cambios en objetos de la escena se pierden.

## PlayerIntent

Struct en `Core`: lo que un jugador "quiere hacer" este frame. Hoy solo `Move` (vector en espacio de campo, longitud 0..1).
La longitud es la **deflexión del stick** y decide trote / carrera / sprint. Las acciones (pase, tiro, centro) se añadirán aquí.
Lo produce cualquier `IIntentSource`; `PlayerIntentProvider` elige la fuente con más entrada (táctil y teclado conviven).

## Input

- **VirtualJoystick** (táctil): flotante en la mitad izquierda de la pantalla; aparece donde apoyas el pulgar.
  Tamaños en unidades de canvas (escalan con la pantalla) y respeta el *safe area* (notch).
  **No hay botón de sprint**: el sprint depende de lo lejos que lleves el pulgar.
- **KeyboardGamepadIntentSource** (PC): WASD o flechas = correr; **Space** = trote; **Shift** = sprint (sube de forma progresiva).
  El stick izquierdo del gamepad es analógico y da todo el rango, igual que el táctil.

## PlayerMovement / Locomotion

`PlayerLocomotion` (Core) es el modelo; `PlayerMovement` solo aplica el resultado al Rigidbody kinematic.

1. Deflexión del stick (sin zona muerta) → velocidad objetivo con una curva por tramos:
   - 0–30 %: caminar/trote (hasta 25 % de la velocidad máxima)
   - 30–70 %: carrera (hasta 65 %)
   - 70–95 %: sprint progresivo (65 % → 100 %)
   - ≥ 95 %: sprint máximo
2. El rumbo gira hacia el stick con una velocidad que baja al ir más rápido (más inercia a tope).
3. Un giro brusco baja la velocidad objetivo (invertir la marcha a tope te frena y vuelve a acelerar).
4. La velocidad sube con la aceleración (que decae cerca del tope) o baja con el frenado (2× la aceleración).

Referencia con atributos 70: ~1,2 s hasta casi la velocidad máxima; ~0,5 s en frenar desde sprint.

## Stamina

- Capacidad = segundos de sprint completo (atributo Stamina 70 ≈ 8 s). El gasto es proporcional a lo adentro que estés en la zona de sprint.
- Sin sprint se recupera de forma gradual (≈ 10 s de vacío a lleno parado; la mitad de rápido corriendo).
- Al llegar a 0 → **agotado**: la velocidad queda limitada a "carrera" hasta recuperar un 25 %.
- Preparado para fatiga futura: todo pasa por `StaminaSystem` y `PlayerRuntimeState`.

## Balón

- `BallController`: Rigidbody esférico (masa, rebote, fricción, colisión continua). Único punto de acción sobre el balón.
- `BallPhysics`: fuerzas extra por paso físico (resistencia del aire cuadrática, rodadura). Aquí irán Magnus/efecto.
- **Dribbling** (`BallInteractor`): si corres con el balón cerca y delante de los pies, le das toques que lo empujan por delante.
  El balón nunca es hijo del jugador ni va pegado: sigue siendo físico.

## Cámara

`CameraManager` sigue al jugador con suavizado (SmoothDamp) y algo de anticipación. Todos los valores salen de un `CameraProfile`;
cambiar de perfil mezcla los valores de forma suave: ese es el gancho para TV Close, Wide, Player, Broadcast y Replay.
La cámara mira desde el lado -Z y no rota (el movimiento es siempre relativo al campo).

## Reemplazar la cápsula por un personaje 3D

El prefab `Player` tiene un hijo `Visual`. Cambia su contenido por el modelo y asigna su `Animator` en **Player Animation**
(parámetros `Speed` 0..1 y `Sprinting`). La física/colisión vive en la raíz: no hay que tocar el movimiento.
