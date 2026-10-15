# FS27 — Prueba humana sencilla

Esta guía es para quien **prueba el juego**, no para quien lo programa. No hace falta saber nada técnico.

## Qué recibes

Un único archivo: **`FS27-jugable.html`** (unos 12 MB). Te lo pasa quien organiza la prueba (por correo, mensajería o USB). **No se publica en ninguna web ni hace falta internet.**

## Cómo abrirlo

1. Guarda el archivo donde quieras (por ejemplo, en Descargas).
2. **Ábrelo con doble clic** (en el móvil: ábrelo desde «Archivos» / «Descargas» y elige tu navegador: Chrome, Edge, Safari o Firefox).
3. Verás una pantalla verde que dice «Cargando el motor…». Tarda unos segundos la primera vez.
4. Cuando aparezca el menú, pulsa **JUGAR**. No hace falta tocar nada más: ya viene todo elegido.

> Si en el móvil el juego se ve vertical, **gira el teléfono** a horizontal.

## Cómo se juega

| Qué quieres hacer | Cómo |
|---|---|
| Mover a tu jugador | Arrastra el círculo de la izquierda de la pantalla |
| Pasar | Pulsa **B** (el pase va hacia donde empujas el círculo) |
| Pase largo al espacio | Desliza **B** hacia arriba |
| Disparar | Desliza **A** hacia abajo (cuanto más largo el deslizamiento, más fuerte) |
| Vaselina | Desliza **A** hacia arriba |
| Presionar al rival (sin balón) | Pulsa **A** |
| Cambiar de jugador | Pulsa **⇄** |
| Pausa | Botón **❚❚** arriba a la derecha |

Tu jugador lleva un **aro amarillo**. Si prefieres ordenador: flechas o WASD para moverte, **J / K / L** son A / B / C, **Tab** cambia de jugador.

Al terminar el partido (unos 15 minutos) o cuando quieras parar, la pantalla final te pide **valorar** de 1 a 5 seis cosas y escribir lo que quieras.

## Qué nos interesa que pruebes

1. **Un partido completo** con la configuración por defecto.
2. **Otro partido sin «Ayuda de puntería»** (casilla del menú, junto a JUGAR). Queremos saber si notas la diferencia y cuál prefieres.
3. Si te apetece, otra **dificultad** (Novice = fácil … Elite = difícil).

Cosas concretas que nos ayudan mucho:

- ¿Pasas a quien quieres pasar, o el balón va a otro sitio?
- ¿El botón **⇄** te da el jugador que esperas, o salta solo?
- ¿Los compañeros se desmarcan y se ofrecen, o te dejan solo?
- ¿Los rivales presionan y roban de forma creíble?
- ¿Te ha parecido injusto o raro algo (goles, faltas, el portero)?

## Cómo enviarnos tu opinión

Cuando termines (o cuando pares el partido y llegues a la pantalla final):

1. Marca los números del 1 al 5 y, si quieres, escribe un comentario.
2. Pulsa **COPIAR RESULTADO** y **pégalo en un mensaje** a quien te pasó el juego.
   - Si no se copia solo, el texto aparece en una caja ya seleccionada: mantén pulsado y elige «Copiar».
   - Alternativa: **GUARDAR COMO ARCHIVO** crea `fs27-resultado.txt` (mira en Descargas) y nos lo envías.

El resultado incluye el partido, el nivel, si usaste la ayuda y datos del aparato (tamaño de pantalla y fluidez). **No incluye tu nombre, ubicación ni nada personal.**

## Si algo no funciona

| Problema | Qué hacer |
|---|---|
| La pantalla se queda en «Cargando» más de un minuto | Cierra la pestaña y vuelve a abrir el archivo. Prueba con otro navegador (Chrome o Edge van mejor) |
| Se va a trompicones | Cierra otras aplicaciones y pestañas. Anota en el comentario el aparato que usas |
| Los botones no responden | Gira el teléfono a horizontal y recarga la página |
| Aparece un mensaje de error | Haz una captura de pantalla y envíanosla junto con tu opinión |

## Lo que esto **no** es todavía (para ser honestos)

Es un prototipo **sin arte, sonido ni cámara 3D**: los jugadores son círculos. Lo que se prueba es **cómo se siente jugar** y si el fútbol tiene sentido. Las pruebas automáticas del motor son muy exigentes, pero **no sustituyen a una persona jugando**: por eso necesitamos tu opinión.

## Para quien organiza la prueba (resumen técnico)

- El archivo se genera con `node Client/pack-offline.mjs` a partir de `Client/dist` (que genera `Client/build.sh`). Incluye el motor del juego (el mismo Core en WebAssembly), la interfaz y los datos: no descarga nada.
- Verificado de forma automática (Chromium sin cabeza, emulando un móvil, abriendo el archivo desde disco, sin servidor ni red): arranque, partido, pantalla final, formulario y texto resultante (`node Client/test/offline.mjs`).
- **No verificado**: en un móvil real, en Safari/Firefox, ni con una persona. Si el navegador del móvil no deja abrir archivos locales con WebAssembly, la alternativa es un servidor local (`node Client/test/serve.mjs Client/dist 8080` y abrir `http://IP-del-ordenador:8080` desde el móvil en la misma red Wi-Fi).
- Nada se ha publicado ni desplegado.
