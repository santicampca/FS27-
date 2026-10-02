# FS27 — Arquitectura

Juego de fútbol 3D arcade para móviles (Unity 6, URP, C#). Principio: **simple ahora, extensible después**.
Este documento resume lo implementado. Detalle de la Fase 1 en [PHASE1.md](PHASE1.md); sistemas puros
(balón, cámara, input, partido) en [CORE_SYSTEMS.md](CORE_SYSTEMS.md) y datos (jugadores, equipos,
formaciones) en [DATA_CORE.md](DATA_CORE.md).
Dificultad e infraestructura de IA (sin IA todavía): [DIFFICULTY_SYSTEM.md](DIFFICULTY_SYSTEM.md).
**FS27 es 6v6: cada equipo tiene 1 portero + 5 jugadores de campo (6 en total).** Los equipos referencian jugadores por id (`TeamDefinition.PlayerIds` → `PlayerLibrary`).
Sistema de jugadores V2 (identidad, 12 atributos, polivalencia, overall, carta): [PLAYER_SYSTEM.md](PLAYER_SYSTEM.md).
Sistema de portero V1 (perfil con 8 capacidades, GRL = media de las 8, estilos; solo datos, sin IA): [GOALKEEPER_SYSTEM.md](GOALKEEPER_SYSTEM.md) · integración de los 20 porteros: [GOALKEEPER_DATA_INTEGRATION.md](GOALKEEPER_DATA_INTEGRATION.md).
Base de datos de 20 defensas centrales (importador de jugadores de campo, correcciones documentadas, Target OVR frente a Overall calculado): [DEFENDERS_DATA_INTEGRATION.md](DEFENDERS_DATA_INTEGRATION.md) · defensas centrales zurdos (mismo importador, lector compacto): [LEFT_DEFENDERS_DATA_INTEGRATION.md](LEFT_DEFENDERS_DATA_INTEGRATION.md) · laterales (6 LD + 6 LI; lector seccionado): [FULLBACKS_DATA_INTEGRATION.md](FULLBACKS_DATA_INTEGRATION.md) · mediocampistas híbridos (6 MDI + 6 MDD; lector de fichas): [HYBRID_MIDFIELDERS_DATA_INTEGRATION.md](HYBRID_MIDFIELDERS_DATA_INTEGRATION.md) · mediocentros defensivos (10 MCD; lector de fichas propiedad/valor): [MCD_DATA_INTEGRATION.md](MCD_DATA_INTEGRATION.md) · mediocentros centrales (10 MC): [MC_DATA_INTEGRATION.md](MC_DATA_INTEGRATION.md) · mediocampistas ofensivos (10 MCO): [MCO_DATA_INTEGRATION.md](MCO_DATA_INTEGRATION.md).
Creator Engine (prompts semánticos → parches → especificación + FootballDNA 2.0 + comportamientos + plan de ensamblaje, malla procedural y exportación; **sin arte final, sin clips, sin verificar en Unity**): [CREATOR_ENGINE.md](CREATOR_ENGINE.md).

## Capas (assembly definitions)

```
FS27.Core            C# puro, sin UnityEngine. Reglas y modelos: intent, atributos, locomoción, stamina,
                     Ball Core, Camera Architecture, Input, Match Core.
   ▲
FS27.Gameplay        Componentes de juego: PlayerEntity, PlayerMovement, Ball*, FieldBounds, TuningProfile.
   ▲        ▲
FS27.UI     FS27.Input         Presentación y entrada (HUD, joystick virtual, teclado/gamepad).
FS27.Cameras                   Sin dependencias de gameplay (sigue un Transform).
FS27.Infrastructure            Bootstrap y ajustes de runtime.
FS27.Editor                    Solo editor: constructor de la escena Sandbox.
```

Regla: las dependencias van hacia abajo. `Core` no conoce Unity; `Gameplay` no conoce UI, Input ni Cameras.

## Flujo del jugador

```
IIntentSource ──► PlayerIntentProvider ──► PlayerMovement ──► PlayerLocomotion (Core) ──► Rigidbody (kinematic)
 (táctil, teclado,      (elige la fuente)      (FixedUpdate)        (heading + velocidad + stamina)
  gamepad, IA...)
```

El jugador nunca sabe de dónde viene la entrada. La IA futura usará `PlayerIntentProvider.SetOverride`.

## Convenciones

- **Espacio de campo:** X = derecha, Y del intent = "arriba en pantalla" = +Z del mundo. La cámara no rota, así
  que el movimiento es siempre relativo al campo. Rumbo (heading): 0 = +Z, crece hacia +X (igual que el yaw de Unity).
- **Unidades:** metros, segundos, grados en datos editables, radianes dentro de Core.
- **Datos vs comportamiento:** los atributos (1–99) son datos; `MovementTuning` (en un `TuningProfile`) los convierte
  en valores de juego; el comportamiento vive en código y solo lee esos valores. Nada de números mágicos en scripts.
- **Rendimiento:** sin allocations ni LINQ en `Update/FixedUpdate`; jugadores kinematic (no físicos), solo el balón es dinámico.
- **C# 9** (lo que soporta Unity 6). Todo nuestro contenido va dentro de `Assets/_Project`.

## Pruebas

- Lógica de `Core`: 946 tests NUnit en `Assets/_Project/Tests/EditMode`, ejecutables también fuera de Unity:
  `dotnet test Tests/Core.Tests` (más rápido que abrir el editor).
- Unity Test Runner: Window → General → Test Runner → EditMode.

## Qué viene después (no implementado)

Pase, tiro, gameplay e IA del portero, IA, menús, estadio, y el cableado a Unity de los sistemas de Core (adaptadores finos
que se harán tras validar la Fase 1 en el editor). Ver el roadmap acordado.
