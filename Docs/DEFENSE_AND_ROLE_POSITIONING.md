# FS27 — Defensa básica y posicionamiento por rol (Fase 9)

Estado: **defensa básica funcional y posicionamiento coherente por rol, en el Core, determinista y sin Unity.** Rama `phase-9-defense-role-positioning`, desde `7bdff60` (2167/2167).
No es una defensa táctica: es la cadena **rival con balón → defensor elegido → cerrar → disputa (placaje / deslizante / contacto / intercepción) → recuperación posible**, con coordinación mínima (un principal y, con motivo, un segundo).
La IA sigue sin ser autoridad: solo produce `MovementIntent` / `ActionIntent`; las acciones las resuelve el Core (`Perform` → resolutor → `BallSimulator`, con `SkillResolver`).

**(DECISIÓN FASE 9)** = valor o criterio mío, no especificado. Los números están en `DefenseDefaults/DefenseTuning` y `RolePositioningDefaults/RolePositioningTuning` (el nombre `PositioningTuning` ya existía en `Core/Ai`, por eso el prefijo `Role`).

## 1. Posicionamiento por rol

### 1.1 `RolePositionAssigner`
`TryAssign(TeamRoster | starterIds, FormationDefinition, IPlayerLookup, out RoleAssignment, out problem)` — `Core/Positioning/`.
- **Entrada:** los 6 titulares del roster (o los que están en pista), la formación (fuente de verdad de huecos, zonas, orientación y espejo) y la biblioteca de jugadores. El `PlayingProfileCatalog` y el `PlayerRatingCalculator` se reutilizan; no se inventa ningún atributo.
- **Portero:** el titular con rol `Goalkeeper` va al hueco de portería. Nunca se elige a un jugador de campo para ese hueco; si no hay portero titular, falla con un mensaje. El portero de emergencia no lo decide el asignador: sigue siendo cosa de `TryAssignEmergencyKeeper` / `GoalkeeperSlot`.
- **Campo:** se prueban las 120 formas de dar los 5 jugadores a los 5 huecos y se elige la que maximiza, sumada sobre los huecos,
  `RoleWeight × ajuste de rol + ZoneWeight × adecuación de zona` (0,6 / 0,4, **DECISIÓN FASE 9**).
  - **Zona de un hueco:** la zona de su posición en el campo (`PlayingProfileDefaults.ZoneOfRelativePosition`: los huecos anchos son `Wing`).
  - **Ajuste de rol:** el mejor de los (hasta 3) roles del jugador: afinidad/100 × cuánto encaja ese rol en esa zona (`RoleZones`: cada uno de los 12 roles tiene una zona propia y una segunda; 1,0 / 0,5). Tabla de hechos, sin nombres de jugadores ni de equipos.
  - **Adecuación de zona:** `PlayerRatingCalculator.GetZoneSuitability` (0..100) / 100: ya incorpora la zona principal/secundaria del jugador y sus atributos.
- **Desempates, en orden:** mayor suma de ajuste de rol → mayor suma de adecuación de zona → hueco a hueco, el de mayor Overall (**solo** como desempate) → la permutación que aparece antes con los jugadores ordenados por `PlayerId`. Sin azar: sin `System.Random`, sin `UnityEngine.Random`, sin `DateTime`, sin `Guid`; ni el orden del roster ni la semilla del partido influyen (test).
- **No toca el sorteo:** `RosterGenerator`, los roster, Elite/Strong/Balanced/Modest y la semilla del sorteo no se modifican (test: roster idénticos antes y después, y el sorteo con la misma semilla da lo mismo).
- **Resultado para los 12 equipos reales:** 12 asignaciones válidas, cada hueco con un jugador, ninguno repetido, portero en el hueco de portería; los jugadores cuyo `PrimaryZone` coincide con la zona de su hueco son al menos tantos como con el orden de plantilla (test).

### 1.2 `RoleAssignment` en el partido
`MatchController.SetRoleAssignment(side, assignment)`. Sin asignación se conserva el comportamiento anterior (jugadores de campo en orden de plantilla). Con ella, `PlaceFormation` y `RefreshHomePositions` usan el orden de huecos. Un jugador que sale de pista **cede su hueco** al siguiente jugador de campo sin hueco, en orden de plantilla (el sustituto hereda); volver a llamar a `SetRoleAssignment` reasigna de forma explícita. Con menos jugadores que huecos (una expulsión) los huecos sobrantes quedan vacíos (`FormationPlacement` lo tolera).

### 1.3 Home
`WorldPlayer.Home` / `HasHome` (de la Fase 8) se alimentan del hueco asignado: la posición del hueco (girada según el sentido de ataque actual, dentro del campo, el portero en su zona). Es una caché derivada que **solo se recalcula** cuando cambian la formación, la asignación, el sentido de ataque (cambio de campo) o quién está en pista (sustitución, expulsión, portero de emergencia). No cambia tick a tick (test).

### 1.4 Controlador de Home en juego (`HomeTarget`)
Sin tarea defensiva inmediata, el jugador va a su Home **atraído hacia el balón** pero **nunca más de `HomeMaxDeviation` (6 m)**, y menos atraído cuanto más lejos está el balón (`HomeBallRange`, `HomeFarPullShare`). No está quieto (el objetivo sigue al balón), no persigue siempre el balón y no abandona su zona; si está a más de `FarFromHomeDistance` de su Home, vuelve.

## 2. Defensa

### 2.1 Quién defiende (`DefenseRoles`)
Con el rival con balón, para cada jugador de campo del equipo:
`score = −distancia·1,0 + situado entre el portador y su portería (coseno)·3,0 − distancia a su Home·0,3 + 1,5 si ya está presionando − 20 si tiene el enfriamiento de patada − 100 si desliza o está bloqueado`.
Nunca se elige al portero ni a quien tiene menos de `PressMinStamina` (20). Empate: el de la plantilla antes. **Principal** = mejor puntuación. **Secundario** (como máximo uno): el mejor de los demás a menos de `SecondaryMaxDistance` (14 m) del portador, **solo si** el principal está a más de `SecondaryTriggerDistance` (6 m) o el portador está a menos de `SecondaryDangerDistance` (14 m) de la portería que se defiende. Es una función pura de la vista, determinista, sin sistema de utilidad.
**Garantía comprobada:** ni en una malla de 21 situaciones ni en las simulaciones de 60 s el equipo que defiende manda a más de 2 jugadores a la vez (`Cerrar`, `Placar`, `Deslizar`, `Contacto`, `Interceptar`).

### 2.2 Principal: cerrar y disputar (`BasicDecisionEngine`)
Orden, para el principal con capacidad de actuar:
1. **Placaje de pie** (`Tackle`): balón a ≤ `TackleDistance` (0,95 m) y no por detrás del portador.
2. **Placaje deslizante** (`SlidingTackle`), selectivo: balón entre 1,25 y 1,8 m (algo más lejos con la skill `aggressive_tackle`), portador a ≥ 2,5 m/s corriendo hacia la portería que se defiende (coseno ≥ 0,5), defensor dentro del cono de su trayectoria (≤ 60°), stamina ≥ 30 y no por detrás.
3. **Contacto** (`PhysicalChallenge`): portador a ≤ `ChallengeDistance` (1,5 m) y placaje no claro (balón lejos o por detrás).
4. **Cerrar** (`CloseDown`): `MovementIntent` hacia el balón, a `PressMagnitude` (0,8) a menos de 5 m; lejos de él (> 12 m) hacia un punto del lado de su portería, a 3 m del portador; cansado (< 35) a `TiredMagnitude` (0,5).
Las acciones van por `ActionIntent` → `ActionRequest` → el resolutor existente (Defense, Agility, Strength, Stamina, skills, presión/ángulo; **no se duplica ni se modifica `ActionModel`**). En Classic un contacto por detrás puede ser falta por las reglas existentes; en Arena nunca (test).

### 2.3 Segundo defensor y el resto
- **Secundario:** `CloseDown` a un punto a `SecondaryStandOff` (4 m) del portador, del lado de su portería. Si está cansado, vuelve a su Home.
- **Resto:** Home atraído hacia el balón (`HomeTarget`). **Transición mínima:** durante `TransitionSeconds` (3 s) tras perder el balón (`MatchBall.SecondsSincePossessionChange`, nuevo) los que no defienden vuelven a su Home a velocidad máxima. No hay FSM ni contrapresión.

### 2.4 Intercepción
Si el balón viaja tras un toque rival (velocidad ≥ 2 m/s) y no va a un compañero, se predice su trayectoria con `WorldView.PredictBallPosition` (el modelo de referencia sobre una copia) en pasos de 0,25 s hasta 1,5 s (2,0 s con la skill `interception_reading`). Un jugador **puede llegar** a la muestra k si `distancia − 2,0 m ≤ velocidad máxima·0,85·tiempo`. **Solo el que llega antes** (empate: el de la plantilla antes) va (`Intercept`: `MovementIntent` al punto) y, con el balón a ≤ 2 m y a ≤ 1,6 m de altura, juega `ActionIntent(Intercept)`. El receptor previsto de un pase propio va al encuentro del balón (`ReceiveBall`).

### 2.5 Skills defensivas
Solo consulto `ResolvedSkills.Has(efecto)` de la vista (no repito requisitos): `aggressive_tackle` amplía la ventana de deslizante; `interception_reading` alarga el horizonte de intercepción. Los efectos reales de las skills (más calidad de placaje, de intercepción…) siguen aplicándose en el Core (`SkillResolver`): un test lo comprueba comparando el resultado de un mismo placaje con y sin skill.

### 2.6 Cansancio
No hay una stamina nueva: el gasto sigue siendo el de `LocomotionStepper` y `SpendStamina`. Un defensor por debajo de `PressMinStamina` deja de ser elegido (otro asume la presión); por debajo de `TiredStamina` presiona más lento; el secundario cansado vuelve a casa.

### 2.7 Selección automática
`AutoSelection` (Fase 8): con el rival con balón, el jugador seleccionado del lado humano es el **defensor principal**; con balón, se mantiene el jugador humano. La IA nunca sustituye al jugador controlado por una persona (el driver no decide por él).

## 3. Simulación (60 s, equipos y formaciones reales, asignación por rol)
Medidas de una ejecución (equipos 3 contra 8; semilla 63 en Arena y 61 en Classic). **Medido, no balanceado.**

| | Arena | Classic |
|---|---|---|
| Decisiones | 4798 | 4800 |
| Acciones | 166 | 156 |
| Posesiones / pérdidas | 59 / 58 | 60 / 59 |
| Placajes / deslizantes / contactos / intercepciones (intentos) | 16 / 2 / 38 / 36 | 12 / 3 / 29 / 35 |
| Recuperaciones | 47 | 45 |
| Faltas | 0 | 1 |
| Pases / tiros / goles | 57 / 8 / 2 | 56 / 2 / 0 |
| Distancia media del defensor principal al portador | 3,18 m | 3,84 m |

Invariantes comprobados cada 10 pasos: 12 jugadores, un portero por equipo, posiciones y balón finitos, stamina 0–100, dentro del campo (en Arena el balón dentro de las paredes), separación, Home válido para los 12, posesión válida, ningún jugador en dos huecos, nunca más de 2 defensores. Determinismo: la misma semilla da la misma asignación, las mismas decisiones, placajes, pases, goles y contadores; otra semilla da otro partido.

## 4. Límites de esta fase
No hay: Utility AI, Behavior Tree, GOAP, FSM táctica, defensa zonal, marcaje individual, cobertura avanzada, presión coordinada avanzada, línea defensiva dinámica, transiciones completas, contrapresión, contra-sprint, overlapping, half-space, desmarques, llegadas al área, IA de portero avanzada, Coach AI, dificultad, árbitro, competición, estadísticas, Unity. No se implementaron las 12 skills diferidas ni se crearon formaciones. **Limitaciones conocidas:**
- La defensa solo reacciona al poseedor y a los pases en vuelo; no cubre pasillos ni espacios.
- El ataque sigue siendo el de la Fase 8 (con mejores Home y la recepción del pase previsto).
- Los números son iniciales; la proporción de recuperaciones, intercepciones y contactos frente a placajes no está balanceada (se midió: más contactos e intercepciones que placajes).
- Un jugador de campo que hace de portero de emergencia usa el hueco de portería, pero no se reasigna al resto por rol.
- Los reinicios siguen confirmándose desde fuera.

## 5. Legado (auditado; no se borra nada)
| Sistema | ¿Consumidores? | Estado |
|---|---|---|
| `PlayerIntent`, `IIntentSource`, `PlayerIntentProvider` | Sí (locomoción/animación antigua de Unity) | Sustituidos funcionalmente por intents y `NormalizedInput`; se eliminan cuando Unity lea del Core. |
| `PlayerLocomotion`, `StaminaSystem` | Aislados (Core 1–2 refs; Unity 1 y 0) | Sustituidos por la locomoción de la Fase 3. |
| `DribbleTouchModel`, `BallInteractor` | 1 y 1 | Sustituidos por la Fase 4; `BallInteractor` aún lo usa Unity. |
| `MatchController.Tick` | Sí (lo llama `Advance`; hosts y tests) | No es legado: API de observación. |
| `Core/Ai/*` (`PositioningModel`, `PressureModel`, `AnticipationModel`, `TeamStyle`, `PlayingProfile`) | `PlayingProfile` sí (el asignador lee `PlayerPlayingProfile`, `RoleAffinity`, `PitchZone`); el resto sin consumidores de partido | `PlayingProfile` ahora se usa de verdad. El resto: pendiente de posicionamiento táctico/presión (fase posterior). |
La limpieza completa queda para la fase de cierre del Core/Unity.

## 6. Fase 10
La defensa de esta fase no cambió. El ataque sin balón (apoyo, carrera, amplitud, desmarque) está en [ATTACK_SUPPORT_RUNS.md](ATTACK_SUPPORT_RUNS.md); los roles de la asignación también alimentan ahora el reparto de trabajos ofensivos mediante `WorldPlayer.Roles` (`MatchController.SetPlayingProfiles`).
