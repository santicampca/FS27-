# FS27 — auditoría estratégica (octubre 2026)

Hechos comprobados en esta sesión (comandos sobre el repositorio, no informes anteriores) y juicio propio, separados. Sin fechas ni porcentajes inventados.

## 1. Estado real

**Verificado**
- Rama `main` sigue en el commit del 1 de octubre; todo el trabajo vive en ramas de fase (`phase-0` … `phase-29`, 146 commits en la cadena actual). Nada de esto está fusionado.
- Core: 262 archivos, ~43.600 líneas de C# puro. Pruebas: 214 archivos; 2.587 pasan en el arnés `dotnet test` (más las explícitas).
- Capa de Unity: ~1.200 líneas (`Gameplay`, `Cameras`, `Input`, `UI`) y **ningún proyecto de Unity**: no hay `ProjectSettings`, `Packages`, escenas, prefabs, arte ni audio (solo `.gitkeep`). `Tests/UnityLayerCheck` solo comprueba que compila una parte (Creator) contra stubs, "no prueba nada del comportamiento en Unity".
- `Web/menu`: sitio estático que muestra datos y **grabaciones** exportadas por el Core; "los controles A/B/C son solo una referencia visual". No ejecuta el Core.
- El README dice "Fase 1 — Sandbox… 5 vs 5": está obsoleto.
- Hasta este bloque **no existía ningún cliente jugable en tiempo real**.

**Por categorías**
| Categoría | Sistemas |
|---|---|
| 1. Implementados y verificados (en Core, con pruebas deterministas) | Locomoción con stamina (`LocomotionStepper`), balón (`BallSimulator`), acciones A/B/C con mapeo por contexto, reglas de partido/periodos/reinicios, goles por línea completa, tarjetas/faltas/penaltis/VAR, datos y equipos 6v6 (jugadores, atributos, roles, 5 dificultades), IA de campo y de portero (Fases 10-29), economía (libro de ZP) |
| 2. Implementados pero poco validados | Sensación de control (nadie la ha medido con una persona), balance (goles +79 % en Classic Pro frente a F22, faltas +27-51 %, sin decisión), IA: pérdida del equipo tras saque de puerta (retención 13-17 %), despeje de portero que solo llega ~12 m (defecto confirmado), ataques por banda (el solape casi no mueve posiciones) |
| 3. Simulados / provisionales | Visor web (reproduce grabaciones), controles del menú web, cámara de Unity sin escena, `VirtualJoystick`/`MatchBridge` de Unity (nunca ejecutados aquí) |
| 4. No existen | Proyecto Unity, arte 3D, animaciones, audio, háptica, interfaz de saques/penaltis/tarjetas para la persona, repetición de goles, tutorial, guardado local, build móvil, medición en móvil real |
| 5. En Core pero sin experiencia real conectada | Economía/progresión, entrenadores, árbitros y VAR con interfaz, Creator (modelos procedurales y GLB), cámara (`CameraDirector`), identidades |

**Lo nuevo de este bloque (`Client/`)**: el Core real en WebAssembly + cliente táctil. Medido en Chromium headless con emulación táctil: carga en ~1,5 s, 60 pasos/s en tiempo real, un partido completo (874 s de juego) en 5,9 s de reloj, y el mismo partido jugado en nativo y en WebAssembly termina en **exactamente el mismo estado** (checksum igual, 55.176 pasos). No medido: móvil real, jugadores reales.

## 2. Qué es "una joya" (criterio propio)
Imprescindible para el primer juego jugable: stick que responde sin latencia perceptible; pase/tiro con asistencia de puntería (en táctil la puntería manual no funciona); cambio automático de jugador acertado; balón legible (sombra, altura); partido completo con reinicios que se entienden; rival que no regala ni hace trampas; un porteros creíble; 3 minutos de partido que den ganas de repetir (ritmo, ocasiones, errores). Después: personalidad de jugadores, 1v1, centros, repeticiones, audio, progresión. El realismo numérico no entra: importa lo que la persona nota en 10 segundos.

## 3. Cuellos de botella (respuestas directas)
1. **Lo demasiado lento**: 29 fases de Core sin un solo minuto de juego con una persona. Se perfeccionó la IA y el balance sin poder juzgar la diversión.
2. **Poco valor para el jugador**: nombres de identidades, documentación de fases, afinado de las 5 dificultades y del balance Arena/Classic antes de que exista el partido; el menú web con economía simulada.
3. **Perfeccionado antes de necesitarlo**: balance fino por dificultad, escenarios de VAR, penaltis con laboratorio de 200.000 tiros.
4. **Automatizable**: comparaciones pareadas con intervalos, banco de escenarios, y ahora el partido humano-simulado por toques reales (smoke test).
5. **Paralelizable sin conflicto**: (a) IA/Core (banco de escenarios), (b) cliente (sensación, UI), (c) visual (procedural, sin tocar el Core), (d) datos. Son carpetas distintas con contratos estables (`SubmitHumanInput`, `Snapshot`).
6. **Requiere integración real**: sensación de control, cámara, legibilidad, asistencia de puntería, diversión. Ninguna se puede juzgar con tests de Core.
7. **Arquitectura que acelera**: Core puro y determinista (portó a WebAssembly sin cambiar una línea y es idéntico bit a bit al nativo), contratos de entrada/salida claros. **Que frena**: depender de Unity para ver cualquier cosa (no hay proyecto, pesado de instalar para ti), y el flujo "fase → informe → fusionar" sin decisión de fusión.
8. **Mínimo para un partido divertido en móvil**: cliente en tiempo real + asistencia de puntería + cambio de jugador + reinicios jugables + vista legible + sonido básico.
9. **Dejar / simplificar / posponer**: más fases de balance sin jugadores; el menú web como producto; 5 dificultades afinadas; Unity hasta tener una razón medible; nuevas bases de datos; 1v1/centros avanzados.

## 4. Investigación (fuentes)
- **Verificado leyendo el código**: `bryanoliveira/soccer-twos-env` es un envoltorio Python de un binario Unity de 2v2 con evaluación por pares; `JustArmaan/Unity-Soccer` es un prototipo de 5 scripts (velocidad instantánea, `AddForce` fijo, gol por trigger). Ninguno aporta una solución útil de movimiento, balón, goles o entrada.
- **Verificado probando aquí**: .NET 8 compila el Core a WebAssembly con el workload `wasm-experimental` (sin Emscripten, sin AOT) y corre en Chromium a ~55-100× tiempo real en esta máquina; ICU es necesario (los importadores de datos y la regla de nombres cortos usan `string.Normalize`): con `InvariantGlobalization` el Core carga mal los nombres con acentos.
- **Hipótesis (no verificadas)**: el rendimiento en un móvil medio (probablemente varias veces más lento que esta máquina, aún muy por encima de 1×); que un Unity WebGL/otro motor no aporte nada que no se pueda hacer con este camino para el primer partido; licencias y costes de librerías 3D web (p. ej. three.js, MIT) — pendiente revisar antes de incorporar nada.
- **Propuesta propia**: vista 3D con modelos procedurales del Creator ya existente (el Core genera mallas y GLB) cuando la sensación esté probada.

## 5. Hoja de ruta por resultados jugables (sin fechas)
| Etapa | La persona podrá… | Hace falta | Reutiliza | Paralelo | Terminada cuando | Riesgos | No hacer aún |
|---|---|---|---|---|---|---|---|
| R0 (hecha) | jugar un partido completo en el navegador con stick y A/B/C | host WebAssembly + cliente | todo el Core | — | `Client/test/verify.sh` en verde (hecho) | móvil real sin medir | arte, audio |
| R1 Sensación | notar que el control responde y que pasar/tirar acierta | asistencia de puntería (pase al compañero en el cono del stick; tiro a portería), cambio de jugador pulido, medición de latencia entrada→movimiento, pruebas con toques reales | `NormalizedInput`, `ActionMapper` | cliente ∥ IA | métricas objetivas (latencia ≤ N pasos, % de pases al receptor pretendido con la asistencia) + tú jugando | decisiones de diseño de control | pulir visuales |
| R2 Partido completo | vivir saques de banda, córners, faltas, penaltis, tarjetas y descanso con su interfaz | pantallas de reinicio con temporizador (la IA toma si no actúas), avisos | `SetPieceState`, `FreeKickState`, penaltis | cliente ∥ IA | un partido de 14 min sin bloqueos y sin pantallas confusas (smoke ampliado) | tiempos de espera | repeticiones |
| R3 Identidad visual | ver jugadores y estadio coherentes (3D procedural o sprites) | capa de dibujo nueva sobre `Snapshot` | Creator (mallas/GLB) | visual ∥ todo | captura y fps ≥ 30 en móvil medio | licencias/coste de librería | animación compleja |
| R4 Móvil | instalarlo/abrirlo como app | PWA o envoltorio, audio, háptica, medición en dispositivo | `Client/dist` | ∥ R3 | partido a 30+ fps en dos móviles reales | tamaño, rendimiento | tiendas |
| R5 Profundidad | rivales y compañeros con personalidad, 1v1 y centros mejores | IA del Core (banco de escenarios) | `ScenarioBench` | ∥ todo | pareados con IC y partidas humanas | balance | afinar 5 niveles |

## 6. Decisión de alto impacto que necesito de ti
Cómo lo pruebas tú: no hay despliegue (requiere tu autorización expresa). Opciones: (1) desplegar `Client/dist` como sitio estático aparte (Vercel, coste cero) para jugar desde el móvil; (2) que lo sirvas en tu ordenador con `Client/build.sh` y `serve.mjs`. Recomiendo (1) en una URL privada.
