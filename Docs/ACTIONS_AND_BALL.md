# FS27 — Acciones y balón (Fase 4)

Acciones del jugador sobre el balón y el modelo del balón en el Core: control, primer toque, conducción, pases, tiros, defensa y portero. **No hay IA, tácticas, formaciones, entrenadores, skills completas, animaciones finales, modelos 3D, cámara, UI, economía, online, replay ni sonido.** Todos los números viven en `ActionDefaults` (editables a través de `ActionTuning`); ningún otro archivo los repite (un test comprueba que cada valor por defecto tiene su gemelo editable y viceversa). Lo que la especificación no cerró lo elegí yo y está marcado **DECISION MADE DURING PHASE 4**.

## 1. Arquitectura
Flujo único: **Input → `MatchController` → acción del jugador → balón (Core) → `MatchState` → presentación en Unity**.
- El **Core es la autoridad**. El balón lo simula `BallSimulator` (paso fijo de 60 Hz); Unity solo lo refleja (`MatchBridge`/`BallController`). Unity no decide posesión, resultados ni física.
- `ActionInput` (botón, gesto, dirección, magnitud) → `ActionMapper` (según el contexto) → `ActionRequest` → `MatchController.Perform` → `ActionResult`. La resolución vive en `MatchController.Actions.cs` (clase parcial del motor), las fórmulas puras en `ActionModel` y el perfil del jugador en `ActionProfile`.
- Ficheros: `Core/Actions/{ActionDefaults,ActionTypes,ActionProfile,ActionModel}.cs`, `Core/Match/MatchController.Actions.cs`, `Gameplay/ActionBridge.cs` (solo reenvía el input al Core).
- Orden de cada paso de `Advance`: simular el balón → mover jugadores (con los temporizadores de acción) → interacciones jugador–balón → juzgar el balón (gol/fuera). Todo el azar sale del generador con semilla del partido (`MatchSettings.Seed`, `SeedMixer` + `SeededRandom`), en el orden en que ocurren las acciones: misma semilla y mismos inputs = mismo partido, bit a bit.

## 2. BallSimulator
Sin cambios de física propios. Un ajuste **DECISION MADE DURING PHASE 4**: el balón que crea el motor usa `MagnusCoefficient = 0.012` (antes 0) para que el efecto de un tiro con curva tenga fuerza; un `BallParameters` explícito del llamador se respeta tal cual. Con el balón sin spin el Magnus no hace nada, así que el resto del juego no cambia. Los toques se aplican con `ApplyVelocityChange` (la velocidad la decide la acción, el simulador solo la aplica). Estados: `BallMode` ya existía y se mapea así — Libre=`Free`, Controlado=`Controlled`, Pasado=`InPass`, Disparado=`InShot`, Desviado=`Deflected` (`Rebound` = rebote en pared), Fuera de juego=`Dead`, Gol=`Goal` (**nuevo, añadido al final**; el motor lo pone durante la celebración). Un balón en vuelo que se detiene pasa a `Free`.

## 3. Posesión
Una sola fuente: `MatchBall.HasPossession / PossessingTeam / PossessingPlayerId` (`MatchController.BallCarrier` es solo una lectura). **El balón nunca está pegado al jugador**: tiene su propia posición y velocidad; "controlado" significa que el paso de conducción lo mantiene cerca y que puede perderse. Se adquiere por control limpio, entrada ganada o intercepción; se pierde al patear, perder el control (balón a más de `LooseBallDistance`), por una entrada o choque ajeno, o al salir del campo el jugador. Cambiar de equipo emite `PossessionChanged`. Se guardan último y penúltimo toque (para el asistente) y la última acción del balón (`LastAction`, `LastActionPlayerId`, `SecondsSinceTouch`).

## 4. Control
Un jugador activo toca solo el balón si está a ≤ `TouchReach` y el balón está bajo (`MaxTouchHeight`); el más cercano gana el toque (empate: orden del plantel, local antes que visitante); el último en tocar no lo recoge solo hasta pasados `ReTouchCooldown` s; un balón controlado por otro solo se disputa con entradas. Calidad de control = Control 55 % + Technique 20 % + Agility 15 % + Speed 10 % (todo /99). La acción deliberada `Control` suma `ControlActionBonus`. No existe ningún atributo nuevo: el perfil (`ActionProfile`) se **deriva** de los 12 atributos oficiales al crear el partido (por `IPlayerLookup`, como en la Fase 3) y no se guarda ningún atributo en el motor.

## 5. Primer toque
`score = calidad × pie × stamina − dificultad + bono + ruido`, con `dificultad = 0,8 × clamp(velocidad relativa del balón / FirstTouchMaxBallSpeed) + ángulo/180 × 0,25` (+0,30 si el ángulo supera `MaxControlAngleDegrees`). `score ≥ CleanControlThreshold` → **control limpio** (el balón baja al suelo con la velocidad del jugador y es suyo); `≥ HeavyTouchThreshold` → **toque pesado** (queda como último toque pero el balón se escapa a ~`FirstTouchDistance` por delante, sin posesión); por debajo → **desvío** (rebota con `DeflectSpeed`, evento `BallDeflected`). Pie: dentro de ±`WeakFootAngleDegrees` del frente usa el preferido; más allá hacia su lado débil usa el pie débil, con factor `WeakFootFloor + (1 − WeakFootFloor) × WeakFootQuality/100`. Stamina: factor `StaminaQualityBase + (1 − base) × rendimiento(stamina)` (la curva de la Fase 3, sin tocarla). Correr hacia el balón suma su velocidad a la dificultad.

## 6. Conducción
Cada paso, el balón controlado se acerca a su sitio delante del jugador (`ControlDistance + LeadPerSpeed × velocidad`) con velocidad `v_jugador + PullGain × error`, **limitando el cambio de velocidad** a `CatchUpAcceleration × escala × dt`; `escala = CatchUpFloor + (1 − CatchUpFloor) × calidad`. Calidad de conducción (`MatchController.DribbleControlQuality`) = (½ control + ½ habilidad de regate: Dribbling 40 % + Control 30 % + Agility 20 % + Strength 10 %) × stamina − `PressureEffect` por rival que presiona a ≤ `PressureRadius` (máx. 2) + `ShieldBonus` si protege. Si el balón se aleja más de `LooseBallDistance`, se pierde. Un buen controlador aguanta giros bruscos; uno malo no (tests).

## 7. Pases
Velocidad = `lerp(min, max, potencia) × (0,85 + 0,15 × calidad)`; error angular = `errorMáx × (1 − calidad) × (1 − PowerErrorShare + PowerErrorShare × potencia) × (1 + extra) × ruido[−1,1]`. Calidad de pase = Passing 70 % + Technique 15 % + Control 15 %, × pie × stamina. **Corto** (B toque), **pared** (B deslizar abajo: pase corto con `Detail = Wall`; el evento lleva `WallPass`; no hay IA de aliado, el rebote de vuelta es de fases posteriores), **al hueco / through** (B deslizar arriba: `ThroughSpeedBoost` más rápido y `ThroughExtraError` más error). `LongPass` (balón largo, con altura) está disponible; `SkillLobPass`, `SkillMove` y `CurvedShot` requieren `SkillGranted` (no hay sistema de skills todavía: se rechazan con `SkillNotGranted`; `SkillMove` aun concedida devuelve `NotSupported`). Un pase no tiene destinatario: lleva dirección; quien lo recibe hace su primer toque.

## 8. Disparos
Disparo (A deslizar abajo): velocidad `ShotMinSpeed..ShotMaxSpeed`, ligera elevación `ShotLoft`. Calidad cerca de la portería (≤ `ShotCloseRange`) = Finishing 60 % + Shooting 25 % + Technique 15 %; lejos = Shooting 60 % + Finishing 25 % + Technique 15 %; × pie × stamina. Los tiros que entran los cuenta el motor existente (`GoalScored`, goleador y asistente).

## 9. Chip
Vaselina (A deslizar arriba): velocidad horizontal `ChipMinSpeed..ChipMaxSpeed` y velocidad vertical `ChipVerticalSpeed`; usa la calidad de disparo; emite `ShotPerformed`.

## 10. Curva
Un tiro con `ActionRequest.Curve` (−1..1) recibe *spin* vertical (`CurveSpinRate`) y el Magnus del `BallSimulator` curva la trayectoria. Un `Shot` normal admite hasta `BasicCurveLimit` de la curva; `CurvedShot` (skill) admite la curva completa. Más error con curva (`CurveExtraError`) y más elevación (`CurveLift`) para que el balón esté en el aire.

## 11. Defensa
- **Presionar / contener** (A toque, sin balón): acción continua de `PressureSeconds`; reduce el control de un conductor cercano y gasta stamina por segundo. Con balón, A toque = **proteger** (`Shield`).
- **Entrada de pie** (A abajo) y **entrada deslizante** (A arriba): solo contra el poseedor rival a ≤ `TackleReach` / `SlideReach` del balón. Margen = habilidad de entrada (Defense 60 % + Strength 20 % + Agility 10 % + Speed 10 %) × rendimiento − habilidad de regate del poseedor × rendimiento (+ protección) + ruido (± `TackleNoise`) (+ `SlideBonus` al deslizarse). `≥ TackleCleanMargin` → el balón es del defensor; `≥ TackleDeflectMargin` → balón suelto (nadie lo tiene); si no, fallo y el defensor queda desequilibrado `FailedTackleLockSeconds`. La deslizante mueve al jugador `SlideSpeed` durante `SlideLockSeconds` (no puede actuar ni girar) y, sin objetivo, igualmente se ejecuta (puede tocar un balón libre). **Falta** (solo en modos con faltas — Classic): un fallo por la espalda con margen ≤ `StandingFoulMargin`/`SlideFoulMargin` emite **solo** `FoulCommitted`; no hay árbitro ni reanudación en esta fase.
- **Choque físico** (B toque): a ≤ `ChallengeReach` del poseedor; domina la fuerza (Strength 50 % + Defense 20 % + Agility 10 % + Speed 20 % frente a Strength 50 % + Control 20 % + Agility 10 % + Dribbling 20 %).
- **Intercepción / bloqueo** (B abajo, `Intercept`/`Block`): sobre un balón libre o en vuelo (no el pase de su propio equipo) a ≤ `InterceptReach`; si es lento y el resultado es bueno lo controla; si es muy rápido (> `ShotSpeedForBlock`) o se pidió bloqueo, lo desvía (`BlockSpeedFactor`); si no, falla. Calidad = Defense 35 % + Agility 25 % + Control 20 % + Speed 20 %.
- **Despeje** (B arriba): patada larga y alta con mucho error (`ClearanceMaxError`), emite `ClearancePerformed`. Calidad = Strength 30 % + Passing 30 % + Defense 20 % + Technique 20 %.

## 12. Portero
Mismo sistema que cualquier jugador; se identifica por `MatchPlayer.InGoal`. Dentro de su alcance (`TouchReach × (KeeperReachBase + KeeperReachRange × Diving)`, alto hasta el larguero) hace el primer toque con **calidad de portero** (Handling 60 % + Reflexes 40 %; la dificultad baja con los reflejos): **atrapa** (`GoalkeeperCatch`), **rechaza** (`GoalkeeperSave`, `Parried`: el balón sale más lento y de lado, evento `BallDeflected`) o no llega. **Distribución** (`GoalkeeperDistribution`: B toque = corta, B arriba = larga) solo con el portero en su puesto y con el balón. **Rush / salir** (`GoalkeeperRush`, A arriba sin balón): solo el contrato — se acepta si el balón está a ≤ `RushMaxDistance` y deja `RushRemaining`; el movimiento lo hará la fase de IA/animación. **Portero de emergencia:** un jugador de campo con los guantes usa números derivados (`EmergencyKeeperProfile`, factor 0,75 de la arquitectura); no es un portero real y no se guarda ningún atributo nuevo.

## 13. Stamina
Cada acción cuesta stamina según `ActionDefaults` (pase, tiro, despeje, entrada, deslizante, choque, intercepción, rush; los golpeos escalan de la mitad a todo con la potencia). Se resta de `Locomotion.CurrentStamina` (sin salir de 0..100) y se suma al desgaste normal de correr. **Las fórmulas de la Fase 3 no se tocan** (sus tests siguen verdes sin cambios). La stamina penaliza la calidad (control, pase, disparo, regate, entradas) con un factor suave, nunca bloquea la acción.

## 14. Eventos
Reutilizados: `PossessionChanged`, `FoulCommitted`, `GoalScored`, `BallOutOfBounds`, `BallBouncedOffWall`. Nuevos (al final de `GameplayEventKind`): `BallTouched`, `PassPerformed`, `ShotPerformed`, `BallDeflected`, `TacklePerformed`, `InterceptionPerformed`, `ClearancePerformed`. Arena los permite todos (sigue sin faltas, fuera, etc.). Además `MatchController.ActionPerformed` (y `MatchBridge.ActionPerformed` en Unity) entrega cada `ActionResult`, incluidos los toques automáticos.

## 15. Inputs
`ActionInput(botón A/B, gesto toque/arriba/abajo, dirección, magnitud)`. Mapeo por contexto (`ActionMapper`):

| | Toque | Deslizar abajo | Deslizar arriba |
|---|---|---|---|
| **A con balón** | proteger | disparo | vaselina |
| **B con balón** | pase corto | pase pared | pase al hueco |
| **A sin balón** | presionar/contener | entrada de pie | entrada deslizante |
| **B sin balón** | choque físico | intercepción/bloqueo | despeje |
| **Portero con balón** | B: distribución corta | (común) | B: distribución larga |
| **Portero sin balón** | (común) | (común) | A: rush |

Resultados explícitos: `ActionOutcome` Accepted / Rejected / Failed / Completed y `ActionRejection` (partido no vivo, jugador desconocido o no activo, acción bloqueada, balón fuera de alcance o no disponible, sin posesión, sin objetivo, no es portero, skill no concedida, no soportado, input inválido). Nunca se lanzan excepciones por juego normal. `ActionResult` lleva quién, qué acción, pie, intensidad, dirección, calidad, detalle, velocidad del balón, coste de stamina, posesión ganada y el otro jugador: suficiente para una animación futura.

## 16. Integración con `MatchController`
`MatchController` pasa a ser `partial`. `Advance` llama a las interacciones jugador–balón tras la locomoción; `Perform`, `PerformInput`, `GetActionContext`, `BallCarrier` y `DribbleControlQuality` son la API nueva; el constructor admite un `ActionTuning` opcional. Las acciones solo existen con el balón vivo (`Playing`): en pausa, saque pendiente, celebración, descanso o fin no se toca el balón. Arena: el balón rebota en las paredes (los eventos que Arena no tiene se descartan en el bus). Classic: el balón puede salir y genera sus eventos de saque. Un jugador sustituido o expulsado no puede actuar.

## 17. Integración con la locomoción
Las acciones no frenan al jugador (se puede pasar o disparar corriendo); la entrada deslizante y la entrada fallida bloquean el input unos instantes (`ActionLockRemaining`) con el mismo `LocomotionStepper`. El estado de acción (`PressureRemaining`, `ShieldRemaining`, `SlideRemaining`, `RushRemaining`, `KickCooldownRemaining`) vive en `MatchPlayer`. La velocidad del jugador entra en el primer toque (velocidad relativa) y en la conducción (el balón sigue su velocidad).

## 18. Presentación en Unity
`ActionBridge` (nuevo) reenvía el input neutro al Core y devuelve su respuesta; `MatchBridge` expone `ActionPerformed` y sigue reflejando `MatchState.Ball` en el rigidbody. No hay lógica de decisión en Unity. Tras la auditoría: `MatchBridge.RequestTouch` (el toque antiguo de `BallInteractor`) se ignora cuando el Core tiene jugadores, y con plantillas `StartMatch` ya no confirma el saque solo (el host coloca a los jugadores con `PlacePlayer` y llama a `MatchBridge.ConfirmRestartReady`). **Honestidad sobre la verificación:** el código de Unity solo se compiló contra stubs de UnityEngine escritos a mano; **no se ha ejecutado en el editor de Unity**.

## 19. Límites de la Fase 4
Sin IA (no se elige destinatario de pase ni compañero de pared), sin tácticas/formaciones/entrenadores, sin catálogo de skills (solo contratos), sin cabezazo/centro/tiro colocado (`NotSupported`), sin árbitro ni reanudaciones por falta, sin fuera de juego, sin animaciones ni arte (los perfiles son `Planned`), sin movimiento propio del portero, sin colisión entre jugadores, sin regla de pase atrás al portero. Los valores son iniciales y se reafinan en una fase de balance.

## 20. Decisiones tomadas — DECISION MADE DURING PHASE 4
1. El contexto decide qué hace cada botón (con balón / sin balón / portero); tabla de la sección 15.
2. `FootballActionKind` crece **al final** (23–26): `Pressure`, `SlidingTackle`, `PhysicalChallenge`, `Clearance`. `Tackle` = entrada de pie; `Intercept` y `Block` son resultados del mismo control (B abajo). Cada acción nueva tiene su perfil de animación `Planned`.
3. Las acciones de skill se rechazan sin `SkillGranted`; el sistema de skills no existe aún.
4. Toque automático del balón por proximidad (alcance, altura, enfriamiento) en lugar de una acción obligatoria; el más cercano gana.
5. Primer toque con tres resultados (limpio / pesado / desvío) y puntuación con ruido de semilla; umbrales en `ActionDefaults`.
6. Conducción por aceleración limitada de persecución (el balón puede perderse), no por pegado.
7. Pie: preferido dentro de ±45° del frente, débil más allá hacia su lado débil; factor del pie débil `0,60..1`.
8. Pesos del perfil de acción derivados de los 12 atributos oficiales; ninguno nuevo; `Reaction` sigue siendo legacy.
9. El azar sale de `MatchSettings.Seed` (campo nuevo, 1 por defecto) en orden de acciones.
10. Curva con Magnus (`MagnusCoefficient` por defecto del balón del motor) y *spin* vertical.
11. Faltas: solo el evento `FoulCommitted` en modos con faltas; sin árbitro.
12. Portero: atrapa / rechaza / no llega; rush solo contrato; el de emergencia usa el perfil derivado; puede atrapar pases atrás (sin regla de pase atrás).
13. Costes de stamina centralizados en `ActionDefaults`, la mitad con potencia 0.
14. `Vec2` gana el operador unario `-` (aditivo).
15. `BallMode.Goal` añadido al final; el balón está en `Goal` durante la celebración.
16. Todos los valores de `ActionDefaults` (tabla siguiente) son valores iniciales míos, para afinar en balance.

## Tabla de `ActionDefaults`

**geometry (metres): who can touch the ball**

| Parámetro | Valor | Qué es |
|---|---|---|
| `TouchReach` | 1.0 | A player within this horizontal distance of the ball (and low enough) can touch it on his own: the automatic first touch. |
| `KickReach` | 1.2 | Reach for kicking actions (pass, shot, chip, clearance): the ball must be this close to the player's feet. |
| `TackleReach` | 1.0 | Reach of a standing tackle, measured to the ball. |
| `SlideReach` | 1.9 | Reach of a sliding tackle. |
| `ChallengeReach` | 1.6 | Reach of a physical challenge, measured to the ball carrier. |
| `InterceptReach` | 2.2 | Reach of the intercept/block control, measured to the ball. |
| `MaxTouchHeight` | 1.1 | Highest the lowest point of the ball may be for a foot/body touch. |
| `ReTouchCooldown` | 0.3 | The last player to touch the ball cannot take it automatically again for this long (seconds): a kicker does not re-collect his own pass. |

**control, first touch and dribbling**

| Parámetro | Valor | Qué es |
|---|---|---|
| `ControlDistance` | 0.8 | How far in front of the player the controlled ball sits at rest. |
| `LeadPerSpeed` | 0.1 | Extra distance of the ball in front of the player per m/s of his speed. |
| `LooseBallDistance` | 2.2 | Beyond this distance from his feet a dribbler loses control of the ball. |
| `ControlSpeed` | 6.0 | Largest speed (m/s) a first touch leaves the ball with relative to the player. |
| `CatchUpAcceleration` | 18.0 | Largest acceleration (m/s^2) with which the ball can follow a dribbling player at control quality 1; scaled down for worse control. |
| `CatchUpFloor` | 0.35 | Share of the catch-up acceleration kept at control quality 0. |
| `PullGain` | 8.0 | How strongly the ball is pulled back towards its place in front of the player (1/s). |
| `FirstTouchDistance` | 1.0 | Where a heavy (poorly controlled) first touch sends the ball, in metres in front of the player. |
| `MaxControlAngleDegrees` | 130.0 | Beyond this angle between the player's facing and the ball a touch cannot be controlled (it deflects the ball). |
| `FirstTouchMaxBallSpeed` | 20.0 | Ball speed (m/s) at which a first touch is hardest. |
| `CleanControlThreshold` | 0.2 | First-touch score at or above which the ball is controlled cleanly. |
| `HeavyTouchThreshold` | -0.05 | First-touch score at or above which the player still gets the ball, but heavily (it runs away from him). |
| `FirstTouchNoise` | 0.1 | Half-width of the seeded noise added to a first-touch score. |
| `DeflectSpeed` | 3.5 | Speed (m/s) a mis-controlled ball bounces away with. |
| `PressureRadius` | 3.0 | A pressing opponent within this distance reduces a dribbler's control. |
| `PressureEffect` | 0.25 | Control quality lost per pressing opponent (at most two count). |
| `PressureSeconds` | 1.5 | How long the pressure/contain action lasts. |
| `PressureDrainPerSecond` | 0.6 | Extra stamina per second while pressing. |

**kicking: speeds (m/s), errors (degrees)**

| Parámetro | Valor | Qué es |
|---|---|---|
| `DefaultPower` | 0.6 | Power used when an action input carries none. |
| `PassMinSpeed` | 5.0 | Short pass at power 0. |
| `PassMaxSpeed` | 17.0 | Short pass at power 1 (with perfect passing). |
| `ThroughSpeedBoost` | 1.15 | A through ball is hit this much harder. |
| `LongPassMinSpeed` | 9.0 | Lofted pass at power 0. |
| `LongPassMaxSpeed` | 22.0 | Lofted pass at power 1. |
| `LongPassLoft` | 0.55 | Vertical speed of a lofted pass as a share of its horizontal speed. |
| `ShotMinSpeed` | 12.0 | Shot at power 0. |
| `ShotMaxSpeed` | 30.0 | Shot at power 1 (with perfect shooting). |
| `ChipMinSpeed` | 5.0 | Horizontal speed of a chip at power 0. |
| `ChipMaxSpeed` | 13.0 | Horizontal speed of a chip at power 1. |
| `ChipVerticalSpeed` | 5.5 | Vertical speed of a chip. |
| `ClearanceMinSpeed` | 12.0 | Clearance at power 0. |
| `ClearanceMaxSpeed` | 26.0 | Clearance at power 1. |
| `ClearanceLoft` | 0.45 | Vertical speed of a clearance as a share of its horizontal speed. |
| `PassMaxError` | 12.0 | Largest angular error of a short pass (degrees) for the worst passer at full power. |
| `ThroughExtraError` | 0.5 | Extra error share of a through ball (riskier). |
| `LongPassMaxError` | 18.0 | Largest angular error of a lofted pass. |
| `ShotMaxError` | 16.0 | Largest angular error of a shot. |
| `CurveExtraError` | 0.25 | Extra error share of a curved shot. |
| `ChipMaxError` | 14.0 | Largest angular error of a chip. |
| `ClearanceMaxError` | 35.0 | Largest angular error of a clearance. |
| `PowerErrorShare` | 0.6 | How much of the error depends on power (error x (1 - share + share x power)). |
| `WeakFootAngleDegrees` | 45.0 | A kick this far towards the weak side of the body uses the weak foot. |
| `WeakFootFloor` | 0.6 | Weak-foot quality factor at weak foot 0 (it is 1 at weak foot 100). |
| `StaminaQualityBase` | 0.85 | A tired player kicks worse: factor = Base + (1 - Base) x performance. |
| `CurveSpinRate` | 14.0 | Spin (rad/s) of a curved shot at technique quality 1. |
| `MagnusCoefficient` | 0.012 | Curve strength of the match ball (acceleration = coefficient x spin x velocity). |
| `ShotCloseRange` | 12.0 | Within this distance of the goal Finishing counts more than Shooting. |

**defending**

| Parámetro | Valor | Qué es |
|---|---|---|
| `TackleNoise` | 0.12 | Half-width of the seeded noise added to a tackle margin. |
| `TackleCleanMargin` | 0.05 | Margin at or above which a tackle wins the ball cleanly. |
| `TackleDeflectMargin` | -0.08 | Margin at or above which a tackle at least knocks the ball loose. |
| `SlideBonus` | -0.05 | Sliding is a bigger commitment: margin modifier. |
| `StandingFoulMargin` | -0.3 | A standing tackle failing by this much from behind is a foul. |
| `SlideFoulMargin` | -0.15 | A sliding tackle failing by this much is a foul. |
| `SlideSpeed` | 6.0 | Speed (m/s) a sliding player carries. |
| `SlideLockSeconds` | 0.7 | A slider cannot act or steer for this long. |
| `FailedTackleLockSeconds` | 0.25 | A failed standing tackle leaves the player unbalanced for this long. |
| `ChallengeLooseSpeed` | 2.5 | Speed (m/s) the ball squirts out with after a won challenge. |
| `InterceptCleanThreshold` | 0.25 | Intercept score at or above which the ball is taken. |
| `InterceptBlockThreshold` | 0.05 | Intercept score at or above which the ball is at least blocked/deflected. |
| `InterceptNoise` | 0.1 | Half-width of the seeded noise on an intercept score. |
| `BlockSpeedFactor` | 0.3 | A blocked ball keeps this share of its speed. |
| `ShotSpeedForBlock` | 14.0 | A ball faster than this is blocked rather than controlled. |

**goalkeeper rush**

| Parámetro | Valor | Qué es |
|---|---|---|
| `RushMaxDistance` | 12.0 | A rush is only reasonable when the ball is this close to the goalkeeper. |
| `RushSeconds` | 1.2 | How long an accepted rush request stays active. |
| `KickCooldownSeconds` | 0.25 | After a kick the same player cannot start another action for this long. |
| `ShotLoft` | 0.06 | Vertical speed of a normal shot as a share of its horizontal speed (a driven shot). |
| `CurveLift` | 0.14 | Vertical speed of a curved shot as a share of its horizontal speed (it must be in the air for the curve to act). |
| `ShieldSeconds` | 1.0 | How long a shield (A tap with the ball) lasts. |
| `ShieldBonus` | 0.15 | Dribble skill added to a carrier who is shielding the ball. |
| `ControlActionBonus` | 0.1 | A deliberate control action adds this to the first-touch score. |
| `KeeperReachBase` | 1.2 | A goalkeeper in goal reaches this many times the normal touch reach with no diving ability... |
| `KeeperReachRange` | 0.8 | ...and this many more with perfect diving. |
| `KeeperParryFactor` | 0.5 | A parried ball keeps this share of its speed. |
| `FreeBallSpeed` | 1.0 | A ball in flight (pass, shot, deflection) slower than this is simply free. |
| `DribbleGainRange` | 0.5 | How much the dribbler's own skill adds to the ball control quality while he runs with the ball. |
| `BasicCurveLimit` | 0.5 | A plain shot may carry a curve up to this share of the full curve (the full curve needs the curved-shot skill). |

**formula weights (the shapes of the formulas; the weights of the attributes are in ActionProfile)**

| Parámetro | Valor | Qué es |
|---|---|---|
| `KeeperLongKickShare` | 0.6 | Long distribution quality: this share Kicking, the rest Distribution. |
| `KeeperShortDistributionShare` | 0.7 | Short distribution quality: this share Distribution, the rest Kicking. |
| `KickSpeedQualityBase` | 0.85 | A kick's speed is scaled by Base + (1 - Base) x quality. |
| `CurveSpinQualityBase` | 0.5 | A curve's spin is scaled by Base + (1 - Base) x quality. |
| `PowerCostBase` | 0.5 | A kick costs Base of its stamina at power 0 and all of it at power 1. |
| `QualityReportBase` | 0.5 | Duel margins are reported as a 0..1 quality around this value. |
| `KeeperHandlingShare` | 0.6 | Goalkeeper touch quality: this share Handling, the rest Reflexes. |
| `KeeperReflexEaseBase` | 1.2 | A keeper's reflexes ease the ball: difficulty speed x (Base - Range x reflexes). |
| `KeeperReflexEaseRange` | 0.6 | See KeeperReflexEaseBase. |
| `ParryAngleMinDegrees` | 35.0 | A parry sends the ball at least this far off the line it came on. |
| `ParryAngleRangeDegrees` | 25.0 | ...and up to this much further. |
| `ParryLift` | 2.0 | Vertical speed (m/s) of a parried ball. |
| `DeflectCarryShare` | 0.25 | A bad first touch keeps this share of the ball's own horizontal velocity. |
| `DeflectLiftShare` | 0.2 | A bad first touch keeps this share of the ball's vertical velocity. |
| `InterceptHeightFactor` | 1.6 | An intercept reaches this many times the normal touch height. |
| `InterceptDifficultyWeight` | 0.5 | Weight of the ball's speed in the intercept difficulty. |
| `BlockAngleSpreadDegrees` | 55.0 | A block sends the ball off up to this angle either side of straight back. |
| `BlockMaxLift` | 2.0 | Largest vertical speed (m/s) of a blocked ball. |
| `BlockLiftShare` | 0.1 | Share of the ball's speed that becomes lift when blocked. |
| `DribbleControlShare` | 0.5 | Dribbling control: this share first-touch control quality, the rest dribble skill. |
| `FirstTouchSpeedWeight` | 0.8 | Weight of the relative ball speed in the first-touch difficulty. |
| `FirstTouchAngleWeight` | 0.25 | Weight of the approach angle in the first-touch difficulty. |
| `FirstTouchBehindPenalty` | 0.3 | Extra difficulty when the ball comes from beyond the maximum control angle. |

**stamina cost per action (points of 0..100)**

| Parámetro | Valor | Qué es |
|---|---|---|
| `PassStaminaCost` | 0.5 | Pass at full power (half at zero power). |
| `ShotStaminaCost` | 1.5 | Shot at full power. |
| `ClearanceStaminaCost` | 1.5 | Clearance at full power. |
| `TackleStaminaCost` | 2.5 | Standing tackle. |
| `SlideStaminaCost` | 6.0 | Sliding tackle. |
| `ChallengeStaminaCost` | 3.0 | Physical challenge. |
| `InterceptStaminaCost` | 1.5 | Intercept/block. |
| `RushStaminaCost` | 1.0 | Goalkeeper rush. |

---

## Fase 6 — skills dentro de las acciones
`SkillMove` deja de ser un contrato (`NotSupported`): es una acción real. `SkillLobPass` y `CurvedShot` siguen resolviéndose en `ResolveKick`, pero ahora con la maestría de la skill del jugador. `ActionRequest.SkillGranted` pasa a legado; el permiso lo decide `SkillResolver.CanUseAction`. Se añaden: botón `C`, `ActionInput.Curve`, `ActionRequest.SkillId`, `ActionResult.SkillId` y `ActionResult.WeakFoot`, y los detalles `Evaded`, `PartlyEvaded`, `Fumbled`. Sin skills, cada acción da **exactamente** el mismo resultado que en la Fase 4. Ver [SKILLS_REAL.md](SKILLS_REAL.md).

---

## Fase 7 — objetivo de pase e intenciones
`ActionRequest` gana `Target` (`PassTarget`: ninguno / jugador / posición) y `Source` (`ControlSource`: humano / IA); `ActionResult` gana `TargetKind`, `TargetPlayerId` y `Source`; `MatchBall.IntendedReceiverId` recuerda al receptor mientras el pase va en camino. Un `ActionIntent` se convierte en el **mismo** `ActionRequest` (`PerformIntent`) y una `NormalizedInput` en un `ActionInput` (`SubmitHumanInput`): no hay otra ruta al resolutor. Ver [AI_INFRASTRUCTURE.md](AI_INFRASTRUCTURE.md).

