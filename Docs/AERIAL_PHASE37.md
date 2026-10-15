# Fase 37 — El balón alto como mecánica de fútbol

Rama `phase-37-aerial-play` (apilada sobre la fase 36). Todo va tras banderas: **apagado por defecto en el Core, encendido en el juego y el banco** (`BasicDecisionDriver.AerialPlay`, `MatchController.AerialRulesEnabled`).

## Qué hace
- **Punto de caída** (`AerialPlay`, REQ-0181): simula el vuelo en una copia y calcula dónde y cuándo el balón baja a altura de cabeza. Es una lectura pura, cacheada por instante. Un test comprueba que coincide (±0,6 m) con el vuelo real.
- **Quién llega primero** (REQ-0068, 0192): el mejor colocado de cada equipo (tiempo de carrera; el receptor previsto tiene ventaja) va debajo del balón y **espera allí** sin pasarse; los dos siguientes ocupan el lugar de la segunda jugada (REQ-0193). El portero no entra aquí. La selección automática de la persona elige a ese jugador, no al más cercano al balón actual.
- **Salto propio** (REQ-0184, 0188): `ActionProfile.AerialQuality` (fuerza 40 %, agilidad 30 %, velocidad 20 %, control 10 %) mueve ±6 % el balón más alto que puede cabecear y pesa la mitad en el duelo aéreo y en la calidad del remate.
- **Cabezazo hacia delante y a un compañero libre** (REQ-0194, 0196): nunca hacia atrás por mirar a su portería; si hay un compañero libre en el arco de 55° y a ≤14 m, hacia él.
- **Volea** (REQ-0108, 0146, 0849): un balón en el aire por encima de 0,4 m golpeado con el pie es `ActionDetail.Volley` (calidad menor con poco control, +8° de error). La IA la intenta al primer toque cerca de la portería (balón a la altura de la cadera o menos, descendiendo, pasillo libre).
- **Portero** (REQ-0174, 0189, 0190): decide salir según llegue antes que el balón, el mejor remate rival y el mejor compañero (con su error de juicio), y al tocarlo atrapa solo si es seguro (manos, mando, rivales alrededor, altura); si no, **despeja con los puños** (`ActionDetail.Punched`).

## Resultados (honestos)
- Suite completa: **2642/2642**. 12 pruebas nuevas en `Phase37Tests` (punto de caída, reclamante, quedarse debajo, selección, salto, volea, puños, salida del portero, cabezazo al compañero, escenas deterministas).
- Banco, partidos completos, 36 semillas (sin → con juego aéreo): goles 2,92/3,75 → 3,86/3,50; xG 5,34/6,05 → 5,91/5,72; cabezazos 13,4/12,4 → 13,9/12,7; voleas 0 → 0,17/0,31 por partido; puños 0,33 por partido. Goles totales +10 %, xG total +2 %: **dentro del ruido** (semillas deterministas, no muestras independientes). El equilibrio se mantiene.
- Escenas nuevas `A7_HighBallBox` y `D2_HighBallDefence` (24 semillas): ambos equipos juegan el balón en el aire; en A7 ganan más los defensas (mejor `AerialQuality`), como en el fútbol real; **los atacantes no generan más remates** con esta geometría. No se ha afinado la escena para que salga mejor.
- Cliente: `Client/test/offline.mjs` sigue pasando con las reglas encendidas (0 peticiones de red, 0 errores de página).

## Límites / pendiente
- **Sin estado de salto**: el contacto ocurre automáticamente en la ventana de cabeza; «no saltar demasiado pronto» (REQ-0185) y la animación (REQ-0195) quedan para la fase de animación. Sin colisión física entre cuerpos en el aire (REQ-0186, 0198): solo un ganador por duelo.
- El cabezazo no es nunca un tiro (decisión de la fase 33); el balance de goles no se toca.
- Visibilidad del balón alto en cámara (REQ-0191) y sombra/indicador: depende de la cámara 3D.
- **Nadie ha jugado todavía**: la sensación de los duelos aéreos necesita prueba humana.
