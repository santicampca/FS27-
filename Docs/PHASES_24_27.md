# Fases 24-27 — Penales y VAR visibles, economía ZP, menú conectado y primer prototipo visual

Esta iniciativa une el Core (C#, sin Unity) con la web del menú (`Web/menu`, estática, desplegable en Vercel) **sin duplicar reglas**: el Core es la fuente de verdad y la web solo muestra lo que el Core exporta o lo que el Core ya respondió.

## 1. Arquitectura de la conexión Core → web

```
Core C# (Assets/_Project/Scripts/Core)            Web (Web/menu/public)
  base de datos cerrada (160 jugadores, 12 equipos)  ──►  data/fs27-data.json            (datos y reglas)
  penales físicos reales (ExecutePenalty)             ──►  data/fs27-penalty-lab.json      (resultados reales de ~215.000 penales)
  VAR real (RequestVarReview + ZenitLedgerWallet)     ──►  data/fs27-var-scenarios.json    (10 revisiones reales)
  partidos reales simulados por el Core               ──►  data/traces/*.json              (8 grabaciones de 90 s)
  PenaltyShootoutState, PenaltyAi, ZenitLedger        ──►  data/fs27-vectors.json          (vectores que la web debe reproducir)
```

* **Generación**: `Tools/webdata.sh` (≈2 min; sets `FS27_WRITE_WEB_DATA=1`). Código: `Assets/_Project/Tests/EditMode/WebExportTests.cs`.
* **Frescura**: sin la variable, esos mismos tests **comprueban** que los archivos versionados no están desactualizados (regeneran todo menos el laboratorio, que se verifica con un hash de entradas y una muestra de celdas jugadas de nuevo). Si el Core cambia, el test falla hasta ejecutar `Tools/webdata.sh` y versionar el resultado.
* **Sin ids internos**: los ids de la base llevan nombres reales de la investigación (ver `DATA_LOCK.md`). Los archivos públicos usan ids opacos `p-xxxxxxxx` (FNV-1a del id interno) y un test verifica que ningún id interno se filtra.
* **La web no ejecuta el Core.** No hay WebAssembly ni servidor: el Core se ejecuta en `dotnet test` y la web lee el resultado.

## 2. Fase 24 — Penales y VAR visibles

| Pieza | Dónde | Estado |
|---|---|---|
| Penal en partido Classic (elegir lanzador elegible, dirección, altura, potencia, animación, resultado del Core) | `js/ui/duel.js`, escena «Penal» | Resultado **real del Core**; animación de la web |
| Tanda de penales (orden editable y guardado, tiros por equipo del Core, final anticipado, muerte súbita) | `js/ui/tanda.js`, `js/core/shootout.js` | Cuenta = copia de `PenaltyShootoutState` verificada con 84 secuencias del Core; tiros = Core |
| VAR (indicador, categoría, quién puede pedirla, coste, saldo, confirmación, estado, decisión inicial/final, efecto, historial) | `js/ui/var.js` | Respuestas **reales del Core** (10 escenarios); cobro en la cartera local con las reglas del Core |
| Contrato del cliente | `GetVarView()`, `VarReview`, `PenaltyState` | Ya existían; la web los consume vía `fs27-var-scenarios.json` |

**Qué hace el Core y qué hace la web**

* El Core decide: si la jugada es revisable, quién puede pedir la revisión, el coste, el resultado (Confirmed/Corrected/Rejected), la decisión final y su efecto (marcador, reanudación), y el resultado de cada penal incluyendo poste, larguero, parada, rebote y regla del segundo toque. La interfaz **no** reimplementa nada de eso.
* La web hace: elegir la celda más cercana de la rejilla, mostrar la respuesta, animar y mover los ZP de la cartera local.
* **Discretización**: el laboratorio es una rejilla (9 laterales × 4 alturas × 3 potencias × 3 saltos del portero × 5 lanzadores por duelo de equipos). Cada celda es un penal real jugado en el Core con la física del balón, el marco de la portería y la estirada. La jugada del usuario se redondea a la celda más cercana. Nota: el Core es determinista, así que cada celda tiene un único resultado; el azar de un partido real lo aportan la semilla y el estado del partido.
* **Cobro de ZP del VAR**: una solicitud válida cobra el coste de su categoría **una sola vez, cambie o no la decisión**; una rechazada antes de empezar (saldo, equipo no elegible, duplicado, jugada ya revisada, etc.) no cobra. En el Core el id de solicitud es el id de transacción del libro (idempotente). En la web, cada escena jugada tiene su propio id (`var:<escena>:<run>`) y el doble toque se bloquea y además se reconoce como duplicado.
* La web nunca decide la revisabilidad: si el escenario grabado por el Core fue rechazado (p. ej. `NotEligible`), muestra ese rechazo. Solo aplica la regla de saldo (`InsufficientBalance`) a su propia cartera local, con la misma semántica que `ZenitLedger`.

### Reglas de rebote y segundo toque (Core, Fase 23)
En un penal del partido el lanzador no puede tocar el rebote de poste o larguero antes que otro jugador; sí puede después de un toque del portero; un segundo toque ilegal se sanciona con tiro libre indirecto; un penal da un solo gol/evento; una tanda termina cada tiro en su primer resultado. La web no las repite: muestra el resultado final que devuelve el Core.

## 3. Fase 25 — Economía de Zenit Points

* **Core**: `Core/Economy/ZenitLedger.cs` — `ZenitLedger` (saldo = suma exacta del historial, nunca negativo, tope `MaxBalance`, ids de transacción idempotentes), `ZenitLedgerWallet : IZenitWallet` (solo el lado humano tiene cuenta; la IA nunca paga), `ZenitRewards` (recompensas) y `ZenitLedgerJson` (formato `FS27.ZenitLedger.v1`, lectura estricta: un documento cuyo historial no cuadra se rechaza, no se repara). Números en `ZenitDefaults/ZenitTuning`.
* **Persistencia**: el Core no lee archivos; el host guarda el JSON. La web lo guarda en `localStorage` (clave `fs27.ledger`) con **identidad local** (un navegador = una cartera). Una cartera dañada se aparta como `fs27.ledger.corrupt` y se crea una nueva avisando al usuario.
* **Misma lógica en la web**: `js/core/ledger.js` es una copia del libro; `scripts/test.mjs` reproduce los guiones de `fs27-vectors.json` generados por el Core (resultados de cada operación y documento final idénticos).
* **Cómo se gana ZP** (primera versión pequeña, configurable y sin balancear): subvención inicial 10; partido completado 5 (+3 si gana, +1 si empata; solo con partido `Finished`, id por partido); sesión de entrenamiento completada 2 (máx. 3 por día, id por sesión). Abrir pantallas no da nada; repetir una acción ya contada no paga dos veces. **En la web solo está conectado el entrenamiento** (la web no juega partidos reales).
* **Costes del VAR**: siguen siendo los del Core (`VarTuning`, 1 ZP por categoría, valor provisional); no se cambiaron.
* **Pruebas**: 18 tests nuevos (`ZenitLedgerTests`): saldo inicial, ganar, gastar, saldo insuficiente, ids repetidos, tope, JSON ida y vuelta y reinicio, documento corrupto, recompensas (partido real finalizado, límite diario), VAR con cambio de decisión y sin cambio, rechazos sin movimiento, saldo insuficiente, doble solicitud y segunda revisión del mismo incidente, reinicio y reintento, coste configurable, la IA no paga y la economía no cambia el resultado deportivo.
* **Límite conocido**: la cartera local se puede editar (restaurar un texto válido con más saldo). Sirve para jugar, no es a prueba de trampas; una cuenta con servidor llegará más adelante.

## 4. Fase 26 — Menú conectado

Las seis opciones se mantienen (ARENA, CLASSIC, EQUIPO, ENTRENAMIENTO, TIENDA, CONFIGURACIÓN). Cartera (`#/cartera`) es una pantalla auxiliar que se abre desde el contador de ZP.

* **Equipo**: 12 equipos (11 del Core + «Mi Equipo») y 160 jugadores reales. Tarjetas con valoración oficial (`PlayerRatingCalculator`), rol oficial (los 12 roles), atributos, porteros con sus 8 capacidades y estilo; alineación real con las posiciones de la formación; filtros por equipo (incluido «sin equipo asignado»: 40 jugadores no están en ningún equipo del sorteo con semilla 7), rol y atributo; orden por valoración/nombre/atributo; paginación. Mi Equipo permite elegir entre las 4 formaciones base del Core (el nombre, colores y escudo aún no se pueden editar: el Core los deja para una fase posterior).
* **Arena / Classic**: reglas reales del modo (desde el Core), equipo, rival, dificultad (5 niveles del Core), plantilla y confirmación con validación. Arena y Classic mantienen sus reglas separadas (`GameModeRules`): Arena sin faltas, tarjetas, penales por falta ni VAR.
* **Entrenamiento**: práctica de penales (lanzar o parar) con resultados reales del Core; completar 5 tiros da ZP.
* **Tienda**: 7 cosméticos que se pagan con ZP de la cartera local; cada compra es un movimiento idempotente (`shop:<artículo>`) y el equipado cambia de verdad el visor (balón, estela, césped) y los marcos de tarjeta. Sin dinero real. El juego aún no lee el inventario.
* **Configuración**: solo ajustes que se aplican: reducir animaciones, texto grande, alto contraste, sonido y volumen (tonos generados), vibración (si el dispositivo la permite), modo zurdo en los controles; restablecer y borrar datos locales.

## 5. Fase 27 — Preparación del partido y prototipo visual

* Flujo: modo → equipo → rival → dificultad → plantilla → confirmar → **Partido** (`#/partido`). Las selecciones se guardan (`fs27.selection`) y viajan a cada escena.
* **Contrato de la petición de partido** (`selection` en `app/state.js`): `{ mode: 'Arena'|'Classic', team, rival, difficulty, userFormationId }`. Es lo que un cliente de juego pasaría al Core (`MatchSettings`, `SetFormation`, `DifficultyDefinition`).
* **Campo**: visor 2D (canvas) que reproduce **grabaciones reales de partidos simulados por el Core** (8: Arena y Classic × 4 emparejamientos; posiciones a 10 Hz, eventos de gol/falta/tarjeta/penal/córner). Marcador y reloj salen de la grabación. No son interactivas.
* **Controles**: joystick virtual y botones A/B/C con las convenciones del juego (sin botón de sprint; esprintar = empujar hasta el borde y gasta resistencia; A/B/C según contexto con y sin balón). El muñeco responde de verdad en pantalla con la velocidad máxima real del jugador (`LocomotionProfile`) y los umbrales del Core, pero **es una demostración de la interfaz, no el motor**: los botones solo muestran el nombre de la acción y la pantalla lo dice.
* **Integración con el Core**: la web respeta el diseño actual pero no ejecuta `MatchBridge`. Cuando exista un cliente conectado deberá: `MatchBridge.StartMatch` no confirma por sí solo el saque inicial si hay jugadores registrados (usar `ConfirmRestartReady`), `RequestTouch` se ignora con jugadores registrados (usar la vía de control correcta), y el cliente no duplica reloj, marcador, árbitro ni decisiones.

## 6. Qué es real y qué es prototipo

| Función | Datos del Core | Resultado del Core | Interactivo | Notas |
|---|---|---|---|---|
| Equipos, jugadores, valoraciones, formaciones | sí | — | consulta | exportación verificada |
| Penal (lanzar/parar) | sí | **sí** (rejilla) | sí | animación propia de la web |
| Tanda | sí | **sí** (cada tiro) | sí | cuenta verificada con vectores |
| VAR | sí | **sí** (10 escenas grabadas) | sí | solo escenas; no es un partido en vivo |
| Cartera de ZP | reglas del Core | copia verificada | sí | local al navegador |
| Partido | grabaciones | **sí** (grabado) | solo reproducir | no se puede intervenir |
| Controles A/B/C | umbrales y velocidades del Core | no | joystick sí; A/B/C no | demostración de interfaz |

## 7. Limitaciones y trabajo pendiente

* No hay partido interactivo del Core en la web (haría falta compilar el Core a WebAssembly o un cliente Unity; no se hizo para no añadir herramientas pesadas ni costes).
* Rejilla de penales discreta: el Core ya soporta apuntar con continuidad; una conexión directa eliminaría el redondeo.
* La tanda arcade de Arena (resuelta por `ArenaPenaltyResolver`) está implementada en el Core pero no en la web.
* El VAR muestra escenas; en un partido real el incidente aparecería durante el juego. No hay repetición de jugadas (el Core no guarda datos de repetición).
* «Mi Equipo»: nombre, colores y escudo no editables (limitación del Core). Los 40 jugadores sin equipo están en el catálogo pero no en ninguna plantilla del sorteo.
* Cartera local sin servidor; la recompensa por partido existe en el Core pero la web no juega partidos.
* Balance: no se cambió nada de balance en estas fases.

## 8. Pruebas

* Core: `dotnet test Tests/Core.Tests` (suite completa; ver el informe final para el recuento).
* Web: `cd Web/menu && npm run build` ejecuta `check` (rutas, imports, sintaxis, sin recursos externos) + `test` (vectores del Core, datos, laboratorio, VAR) + build. Recorrido en navegador: `node scripts/e2e.mjs [url] [carpeta]` (Playwright opcional): 21 comprobaciones en escritorio (1366×768) y móvil (390×844) — rutas, penal, tanda, VAR, entrenamiento, tienda, cartera, ajustes, teclado, sin errores de consola ni desbordes.
