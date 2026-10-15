# Interfaz de integración Core → cliente 3D (`RenderFeed`)

**Qué es**: un contrato **de solo lectura**, versionado y sin tipos de motor, para que cualquier cliente (web 3D, Godot, un visor de repeticiones) dibuje el partido sin conocer el Core por dentro. **No migra ni reemplaza nada**: `GameBridge` (cliente 2D), el Core y sus pruebas siguen igual. Código: `Assets/_Project/Scripts/Core/Match/RenderFeed.cs`. Pruebas: `Phase40Tests` (4).

## 1. Salida: `RenderFrame`
Se rellena con `RenderFeed.Fill(frame)` **una vez por fotograma presentado** (no por paso de simulación; los eventos y la última acción se acumulan hasta el siguiente `Fill`). Los arrays se reutilizan (sin basura): copia lo que necesites conservar.
| Campo | Contenido |
|---|---|
| `ContractVersion` (=`RenderContract.Version`, hoy 1), `Frame` | Versión de la forma del contrato (sube si se quita un campo o cambia su significado) y número de fotograma |
| `MatchTime`, `Phase`, `Period`, `Mode`, `ScoreHome/Away`, `ClockRemaining`, `HomeAttacksPositiveX`, `Field` | Estado del partido (Arena/Classic en `Mode`) |
| `Ball` | Posición (X largo, **Y altura**, Z ancho), velocidad, *spin*, `Grounded`, `Mode`, radio, poseedor |
| `Players[PlayerCount]` | Por jugador: id estable, equipo, en campo, portero, con balón, controlado por persona, posición y velocidad (campo, metros), orientación (rad.), velocidad escalar, resistencia 0..1, **`Pose`** (Idle, Walk, Jog, Run, Sprint, Slide, Shield, Press, Evade, Down), **última acción** (tipo, detalle, resultado, edad en segundos, calidad) y tarjetas |
| `Events` | `GameplayEvent`s desde el último `Fill` (gol, falta, saque, pase, disparo, rebote en muro…) con posición y detalle |

Convenciones: campo con centro en (0,0), X a lo largo, Y a lo ancho (los jugadores); el balón usa el mundo del Core (X, altura, Z). Orientación 0 = +Y, creciendo hacia +X.

## 2. Entrada
No hay una entrada nueva: el cliente envía **`NormalizedInput`** (palo + un botón + gesto + dirección + potencia + curva) a `MatchController.SubmitHumanInput`, y las entradas pueden registrarse con `InputCommandLog` (fase 38) para repeticiones y para reproducir el partido. Cambiar de jugador: `SelectNearestToBall`. Saques de puerta/córner/tiro libre/penalti: los métodos existentes (`ExecuteSetPiece`, `ExecuteFreeKick`…). El cliente 3D **no** tiene permiso para mover jugadores ni el balón.

## 3. Garantías (probadas)
- Solo lee: con o sin `RenderFeed` el partido termina con el mismo `MatchChecksum` (`TheRenderFeed_OnlyReads…`).
- Coincide con el estado del Core (balón, marcador, jugadores) y trae a los 12 + 8 en el orden Home, Away.
- Determinista: dos partidos iguales dan los mismos eventos y las mismas posiciones.
- Sin tipos de motor: el fichero no contiene `UnityEngine`, `Godot` ni `System.Numerics`.

## 4. Cómo se conecta cada candidato
| Cliente | Conexión |
|---|---|
| 3D web | `Client/Fs27.Wasm` expone `RenderFrame` serializado (JSON compacto o matriz como `Snapshot()` hoy) por `[JSExport]`; three.js/Babylon lo dibuja. Hoy `Snapshot()` (matriz de 13 + 8 × jugadores) sirve al 2D y **no se toca** |
| Godot .NET | Referencia el Core como proyecto/ensamblado `net8.0` y llama a `RenderFeed.Fill` en `_Process` |
| Visor de repeticiones | Reproduce un `InputCommandLog` y lee `RenderFeed` |

## 5. Límites y pendiente
- Falta serializar el `RenderFrame` en el puente web (siguiente bloque) y medir su coste (por fotograma: ~20 jugadores × ~15 campos).
- Sin estado de salto en el Core: `Pose` no tiene `Jumping`; el punto de caída de `AerialPlay` puede publicarse como un campo adicional (no rompe la versión).
- Sin interpolación: el cliente interpola entre fotogramas si presenta a más Hz que la simulación (60 Hz).
- La igualdad bit a bit entre plataformas (WASM, Godot, IL2CPP) **no está probada**; solo se garantiza dentro de una misma plataforma.
