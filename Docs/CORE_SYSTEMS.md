# Sistemas Core (C# puro, sin Unity)

Cuatro sistemas independientes en `Assets/_Project/Scripts/Core`. No dependen de Unity ni entre sí
(salvo tipos básicos: `Vec2`, `Vec3`, `PlayerIntent`), se prueban fuera del editor con
`dotnet test Tests/Core.Tests` (124 tests) y **no sustituyen nada de lo ya existente** en Unity:
cuando validemos el juego, los componentes de Unity pasarán a ser adaptadores finos sobre ellos.

| Sistema | Carpeta | Tests |
|---|---|---|
| Ball Core | `Core/Ball` | `BallCoreTests` |
| Camera Architecture | `Core/Camera` | `CameraCoreTests` |
| Input / PlayerIntent | `Core/Input` (+ `PlayerIntent`, `IIntentSource` en la raíz) | `InputCoreTests` |
| Match Core | `Core/Match` | `MatchCoreTests` |

## Regla de sprint (vale para todo el proyecto)

**El sprint depende únicamente de la intensidad del joystick.** No hay botón, flag ni parámetro de sprint en
`PlayerIntent` ni en ninguna fuente de entrada. La deflexión del stick (0..1) pasa por `InputCurve`
(trote → carrera → sprint progresivo) y `StaminaSystem` decide el resto. Dos tests-guarda lo protegen
(`NoSprintButtonOrFlag_ExistsInTheInputContract`, `PlayerIntent_CarriesOnlyMovement_NoBooleanFlags`) y otros
comprueban el recorrido completo pulgar → intent → locomoción.
En PC, `Shift` solo *emula el pulgar en el borde* del stick (sube de forma progresiva); el emulador solo emite una deflexión.

---

## Ball Core

Modelo de referencia de la física del balón, determinista y sin allocations.

- `BallParameters`: radio, masa, gravedad, arrastre cuadrático, rodadura, restitución, fricción de bote, Magnus (0 = apagado), amortiguación del spin. Los valores por defecto replican el balón de Unity.
- `BallState`: posición, velocidad, spin, `IsGrounded`.
- `BallSimulator.Step(ref state, parameters, dt, colliders?, extraForce?)`: gravedad, arrastre, Magnus, bote con fricción, rodadura (el spin sigue la rodadura sin deslizar), reposo. Subdivide `dt` internamente (`MaxSubstep`), así el resultado casi no depende de los FPS.
- Extensible sin tocar el simulador: `IBallForce` (viento, efectos) e `IBallCollider` (hoy `PlaneCollider` para muros; postes y travesaño irán aquí).
- `BallPredictor.Predict` / `PredictPath`: mira hacia adelante sobre una copia (portero, interceptación, cámara, replays).
- `DribbleTouchModel`: la regla de dribbling ("si corres con el balón cerca y delante, le das un toque") como lógica pura. Decide; no aplica. Espejo de `BallInteractor`.
- `BallSimulator.ApplyVelocityChange`: punto único para golpes (la base sobre la que se construirán pase y tiro, **aún sin implementar**).

**Cableado futuro en Unity:** `BallController`/`BallPhysics` siguen usando PhysX. El modelo Core sirve para predecir y para afinar parámetros; `BallInteractor` podrá delegar en `DribbleTouchModel`.

## Camera Architecture

- `CameraContext` (qué mira la cámara: objetivo, balón) → `ICameraMode` → `CameraPose` (posición, punto de mira, FOV).
- `FollowCameraMode`: la cámara de seguimiento suavizada con anticipación, dirigida por `CameraProfileData`. **TV, TV Close y Wide son la misma clase con distintos datos** (`CreateTv/CreateTvClose/CreateWide`). Cambiar valores del perfil en vivo mezcla en vez de saltar. `BallBias` encuadra entre jugador y balón.
- `CameraDirector`: registra modos, cambia de modo con fundido (`SetMode(id, segundos)`), y nunca arranca un modo desde un estado viejo (hace *snap* al cambiar).
- `CameraModeId` reserva `Player`, `Broadcast` y `Replay`: para añadirlos basta implementar `ICameraMode` y `Register`.
- `SmoothMath.SmoothDamp` (como el de Unity, sin sobrepasar el objetivo).

**Cableado futuro:** `CameraManager` pasará a rellenar un `CameraContext`, llamar a `director.Evaluate` y aplicar la pose; el asset `CameraProfile` contendrá un `CameraProfileData`.

## Input / PlayerIntent

```
pulgar/teclas/gamepad/IA ──► IIntentSource ──► IntentRouter ──► PlayerIntent ──► PlayerLocomotion
```

- `PlayerIntent`: solo `Move` (longitud 0..1). Las acciones (pase/tiro/centro) se añadirán más adelante, **no ahora**.
- `VirtualStickModel`: toda la matemática del joystick flotante (posición de la base, límites de la zona, deflexión, propiedad de un solo dedo). Entradas = eventos de puntero; salida = deflexión.
- `DigitalStickEmulator`: teclas digitales → deflexión con rampa (correr / trotar / borde).
- `IntentRouter`: el override (IA) gana; si no, gana la fuente con más intensidad (táctil y teclado conviven). Sanea NaN/infinitos.
- `ScriptedIntentSource`: fuente programable para tests, IA y replays.

**Cableado futuro:** `VirtualJoystick`, `KeyboardGamepadIntentSource` y `PlayerIntentProvider` delegarán en estas clases.

## Match Core

Máquina de estados del partido, sin Unity.

```
NotStarted → WaitingForKickoff → Playing → GoalCelebration → WaitingForKickoff …
                                    └──► WaitingForRestart → Playing …
(cualquier fase viva) ⇄ Paused        Playing → Finished (se acaba el reloj)
```

- `MatchRules.Evaluate`: gol (dentro de los palos y bajo el travesaño), córner o saque de meta según quién tocó último, saque de banda para el rival. "Fuera" = la pelota entera ha cruzado la línea.
- `MatchController`: `Start()`, `Tick(dt, ball)`, `RegisterTouch(team)`, `ConfirmRestartReady()`, `Pause()/Resume()`. Eventos: `PhaseChanged`, `GoalScored`, `RestartRequested`, `MatchFinished`.
- El reloj corre solo con el balón vivo. Un gol en el último instante cuenta; el partido termina tras la celebración.
- El controlador **pide** el saque (`RestartRequested` con tipo, equipo y posición) y espera; la presentación coloca jugadores y balón y llama a `ConfirmRestartReady()`.
- `MatchState`: vista de solo lectura para HUD, audio e IA. `FieldDimensions` / `MatchSettings`: datos (duración, celebración, quién saca, dirección de ataque).

**Cableado futuro:** un `MatchManager` de Unity leerá la posición del balón cada `FixedUpdate`, avisará de los toques y obedecerá `RestartRequested`.

## Lo que NO está hecho (a propósito)

Pase, tiro, portero, IA, equipos, formaciones, menús, HUD de partido, cableado de estos sistemas a escenas/prefabs.
