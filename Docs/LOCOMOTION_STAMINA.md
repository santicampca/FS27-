# FS27 — Locomoción y stamina (Fase 3)

Solo movimiento y stamina. No hay pases, tiros, tackles, regate, control del balón, skills, IA, formaciones ni animaciones. Los números viven en `LocomotionDefaults` (editables a través de `LocomotionTuning`); ningún otro archivo los repite (un test lo vigila).

## Autoridad y paso de simulación
- El **Core es la autoridad**: el estado de movimiento de cada jugador (`MatchPlayer.Locomotion`: posición, velocidad, orientación, input, velocidad actual, stamina actual, `MovementState`) vive en el `MatchState` y solo lo avanza el `MatchController`. Unity solo lo refleja (`LocomotionPresenter` copia posición y orientación al `Transform`; no usa rigidbody ni tiempo de Unity).
- **Paso fijo de 60 Hz**: `LocomotionDefaults.SimulationHz = 60`, `FixedDeltaTime = 1/60`. Es el mismo valor que ya usaba el motor (`MatchDefaults.SimulationStepSeconds` ahora lo toma de aquí). El Core no usa `Time`, reloj del sistema ni aleatoriedad.
- Los jugadores solo se mueven mientras el balón está vivo. Entradas: `MatchController.SetPlayerInput(side, id, Vec2)`; colocación: `PlacePlayer`.

## Flujo por paso (`LocomotionStepper.Step`)
`input` → intención (magnitud 0..1, dirección unitaria; NaN = sin input) → `SmoothStep` → velocidad objetivo → aceleración/frenado → giro → velocidad → posición, y la stamina baja/sube de forma continua.

| Pieza | Fórmula (valores iniciales de tuning) |
|---|---|
| Curva del joystick | `intensidad = m²(3 − 2m)` (0,10→0,03 · 0,25→0,16 · 0,5→0,5 · 0,7→0,78 · 0,85→0,94 · 1→1). Continua: no hay saltos entre walk/run/sprint |
| Velocidad máxima | `MaxSpeed = 4,2 + 0,0333 × Speed` (60→6,20 · 90→7,20 · 99→7,50 m/s) |
| Velocidad objetivo | `MaxSpeed × Rendimiento(stamina) × intensidad` (con el límite de sprint de abajo) |
| Aceleración | `MaxAcceleration = 4,0 + 0,08 × Acceleration`; efectiva = `× (0,70 + 0,30 × Rendimiento)` |
| Frenado | `MaxDeceleration = 6,0 + 0,05 × Agility` (distinta de la aceleración) |
| Giro | `AgilityFactor = 0,70 + 0,30 × Agility/99`; `TurnFactor = 1 − 0,55 × velocidad/MaxSpeed`; `tasa = BaseTurnRate × AgilityFactor × TurnFactor` con `BaseTurnRate = 900°/s` (valor inicial elegido: la especificación da los factores, no la base) |
| Velocidad / posición | `Velocity = orientación × velocidad`; `Position += Velocity × dt` |

Responsabilidades: **Speed** = cuánto corre · **Acceleration** = cómo de rápido llega · **Agility** = frenado y giro (nunca la velocidad máxima). Strength, BodyType, el perfil de juego (Risk/Creativity/Aggression/pie) **no** se usan: pertenecen a fases posteriores. No se creó ningún atributo: los cuatro de entrada son Speed, Acceleration, Agility y Stamina, leídos del `PlayerDefinition` a través del `IPlayerLookup` (el `MatchController` no guarda copias; un test lo comprueba). Un jugador que el lookup no conoce usa los atributos neutros por defecto. Los **porteros usan la misma locomoción** (no hay controlador aparte).

Un velocista no puede girar 180° al instante: la orientación gira a tasa limitada y la velocidad sigue la orientación, así que necesita espacio.

## Stamina (0–100)
`CurrentStamina` (estado, 0..100, nunca fuera de rango) es distinto del atributo `Stamina` (resistencia).
- **Rendimiento** (curva continua, lineal entre puntos): 100→1,00 · 80→0,98 · 70→0,95 · 50→0,90 · 40→0,84 · 30→0,77 · 20→0,68 · 10→0,55 · 0→0,40. Escala la velocidad máxima y, en parte, la aceleración.
- **Disponibilidad de sprint:** `clamp((stamina − 10)/30, 0, 1)` (1 desde 40). Limita la parte de la intensidad por encima del nivel de carrera (`SmoothStep(0,70)`); **el input nunca se bloquea**, el jugador simplemente va más lento. Con stamina 0 no alcanza el sprint máximo pero sigue moviéndose y recuperando.
- **Consumo** por segundo: `3,0 × m² × multiplicadorSprint ÷ factorAtributo` (50%→0,75 · 70%→1,47 · 100%→4,05 con factor 1). El multiplicador de sprint sube de forma continua de 1 (en 0,70) a 1,35 (en 1,0). Caminar apenas consume.
- **Recuperación** por segundo: `2,0 × factorAtributo × zona`, `factorAtributo = 0,75 + 0,50 × Stamina/99` (también reduce el consumo: mejor atributo, menos gasto), zona: parado ×1, caminar ×0,25, correr y sprintar ×0.
- `MovementState` (Idle/Walk/Run/Sprint/Exhausted) es solo descriptivo.

## Recuperación de intervalos (reglas de la Fase 1, sin reglas nuevas)
Fracción de la stamina **máxima**, nunca por encima de 100, aplicada por el motor en cada intervalo a los jugadores en pista: Classic **+25 %** (descanso, antes de la prórroga y descanso de prórroga) · Arena **+10 %** (cambio de lado, antes de la prórroga y descanso de prórroga). `LocomotionDefaults.ClassicIntervalRecovery/ArenaIntervalRecovery` son las mismas constantes del motor.

## Relación con el modelo anterior
El modelo del sandbox (`PlayerLocomotion`, `InputCurve`, `MovementTuning`, `PlayerStats`, `StaminaSystem`, Unity `PlayerMovement`) **no se tocó**: usa otras fórmulas (tanque en segundos de sprint, zona muerta, bloqueo por agotamiento) y sus tests lo fijan. Convive hasta que la escena de juego migre al presentador del Core. El `PlayerRuntimeState.Stamina` antiguo del motor se sigue recuperando en los intervalos como en la Fase 1.

## Límites de la Fase 3
Sin límites de campo ni del área, sin salida de portero, sin selección de jugador, sin contacto ni colisiones entre jugadores, sin interacción con el balón (los jugadores se mueven pero no tocan el balón; eso es la Fase 4), sin animaciones.
