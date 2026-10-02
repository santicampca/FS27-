> Ampliado por [ANIMATION_ARCHITECTURE](ANIMATION_ARCHITECTURE.md) (selección de animación y personalidad de movimiento); lo de aquí sigue siendo válido.

# Animación y movimiento

Esta fase **no cambia** el movimiento (`PlayerLocomotion`, `MovementTuning`, stamina) ni `PlayerIntent`. Fija cómo se conectará la animación.

```
Joystick ─┐
          ├─► PlayerIntent ─► Movement ─► MovementState ─► Animation
IA ───────┘
```

Humano e IA producen el **mismo** `PlayerIntent` y usan el **mismo** movimiento. La apariencia y el FootballDNA **no forman parte de esa cadena**: el DNA solo llega al gameplay por `BehaviorResolver`, que ordena comportamientos para que la IA elija y emita un `PlayerIntent`.

## 1. Movimiento por gameplay, no por root motion

Recomendación: **movimiento controlado por gameplay (cinemático) + animación "in place"**. Root motion solo para acciones puntuales (p. ej. una entrada deslizante), caso por caso.

| Necesidad de FS27 | Por qué |
|---|---|
| Control preciso con el joystick y respuesta inmediata | El movimiento por código reacciona al instante; con root motion mandaría la animación |
| Velocidad y aceleración por atributos | Salen de Speed/Acceleration (`PlayerStats`); con root motion saldrían del clip |
| IA con el mismo sistema | Ambos producen `PlayerIntent`; la IA no depende de clips |
| Replays y multijugador futuro | Sincronizar posiciones y estados es más simple que curvas de animación |

Coste a gestionar: el **patinaje de pies**. Se resuelve alimentando el Animator con la **velocidad real**, sincronizando zancadas en un blend tree y, solo si hace falta, IK de pies. (Fuentes consultadas: discusiones de Unity y guías de locomoción; son criterio general, **no una medición en FS27**.)

## 2. De la velocidad a la animación

El movimiento actual ya está definido por `MovementTuning` y **se mantiene**: el joystick decide trote/carrera/sprint por intensidad (hoy: trote hasta 0,30, carrera hasta 0,70, sprint progresivo desde ahí y completo a 0,95; todos configurables). **No hay botón de sprint.**

El Animator recibe la velocidad real relativa a la máxima del jugador (como ya hace `PlayerAnimation`: `Speed` 0–1 y `Sprinting`) y elige el estado por **umbrales configurables**, no rígidos. Dos jugadores con Speed 90 y 70 usan la misma animación de carrera: el primero llega más lejos y acelera antes **porque la simulación lo decide**, no el clip.

## 3. Animaciones compartidas y firmas

- Una biblioteca por rig (`fs27_humanoid_v1`), no por jugador. Etiquetas previstas: idle, walk, jog, run, sprint, turn, stop, strafe, backpedal, jump, fall, recover; luego receive, control, dribble, pases, shot, header, tackle, intercept, block, shield; y de portero: idle, move, dive, save, catch, parry, punch, distribution, recovery. **Ninguna existe todavía.**
- La individualidad sale de `MovementSignature` (zancada, cadencia, inclinación, giros) y del DNA (cuándo y con qué frecuencia usar `BodyFeint`, `StopAndGo`, `ExplosiveExit`…), no de animaciones propias.
- Adaptación al tipo de cuerpo, sin juegos de animación por cuerpo: **velocidad de reproducción y zancada** por la firma, escala de huesos (el rig humanoide comparte animaciones entre proporciones), mezcla por parámetro de velocidad y, solo si hace falta, IK. Retargeting entre rigs distintos queda como contrato (`RetargetingProfile`).

## 4. Acciones de fútbol y el balón

`FootballActionKind` enumera las acciones (control, regate, pases, centro, tiro, cabezazo, entrada, intercepción, bloqueo, protección, acciones de portero). El flujo previsto:

```
Gameplay decide la acción → Animation la muestra → evento de contacto (ActionContactEvent) → Ball Core → física/trayectoria
```

- El **balón es un objeto independiente**; nunca es hijo del jugador.
- La animación solo informa "ahora hay contacto" (acción, instante normalizado, parte del cuerpo). **No lleva velocidad ni dirección del balón**: eso lo decide gameplay con Ball Core (test).
- El Creator Engine no modifica Ball Core.

## 5. Qué requiere Unity

Animator y blend trees, retargeting, IK, eventos de animación, root motion puntual y todo el ensamblado visual: *requires Unity runtime/editor validation*. Aquí solo se prepara el contrato de datos.
