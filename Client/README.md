# FS27 — cliente jugable en el navegador (primer partido real)

El **Core real** (C#, sin cambios) compilado a WebAssembly y un cliente táctil mínimo: stick + A/B/C, vista cenital provisional. Es la primera vez que el juego se puede **jugar en tiempo real** (antes solo existía el Core simulado y un visor que reproduce grabaciones).

- `Fs27.Wasm/` — anfitrión: `GameBridge` (tiempo fijo, entrada humana por `MatchController.SubmitHumanInput`, `BasicDecisionDriver` para ambos equipos, instantánea para dibujar). No contiene reglas, física ni IA.
- `web/` — página: menú, bucle de dibujo, stick, A/B/C con gestos (tap, deslizar arriba/abajo), cambio de jugador, pausa. Sin dependencias de ejecución.
- `build.sh` — publica el sitio estático en `Client/dist` (el Core + los datos de `Docs/authoring`).
- `test/verify.sh` — compila, juega el **mismo partido completo** en nativo y en Chromium con emulación táctil y compara; `test/smoke.mjs` es la comprobación en navegador.

Requisitos para construir/verificar (no son dependencias del juego): .NET 8 SDK + `dotnet workload install wasm-experimental`; para `verify.sh`, node ≥ 18, un Chromium y `playwright-core` (`PLAYWRIGHT_CORE=/ruta/index.mjs`).
Probar a mano: `Client/build.sh` y `node Client/test/serve.mjs Client/dist 8080`, abrir `http://localhost:8080` (en el móvil: la IP del ordenador). Parámetros de URL: `?auto=1&home=0&away=1&seed=7&mode=Classic&level=Professional`. Teclado: flechas/WASD, J/K/L = A/B/C (Shift = deslizar abajo, Ctrl = arriba), Tab = cambiar jugador.

Qué NO es todavía: no tiene arte, audio ni cámara 3D; no se ha probado en un móvil real ni por una persona; no gestiona saques/penaltis/tarjetas con interfaz propia (el Core los resuelve con su IA); no está desplegado.

## Prueba humana sin servidor (archivo único)

`node Client/pack-offline.mjs` empaqueta `Client/dist` en **un solo archivo** (`Client/dist-offline/FS27-jugable.html`, ≈12 MB: la página, el motor en WebAssembly y los datos) que se abre con doble clic, sin servidor ni internet. Para enviarlo a una persona a mano (correo, mensajería, USB). **No se publica.** Instrucciones para esa persona (sin conocimientos técnicos): `Docs/PRUEBA_HUMANA.md`.

- La pantalla final lleva un formulario (seis valoraciones de 1 a 5 y un comentario) que produce un texto para copiar o un `.txt`; no contiene datos personales.
- `node Client/test/offline.mjs Client/dist-offline/FS27-jugable.html` lo comprueba abriéndolo desde disco (`file://`) en Chromium sin cabeza con emulación táctil: menú, partido, pantalla final, formulario, **cero peticiones de red**.
- El empaquetado parchea dos líneas del cargador de .NET (direcciones absolutas relativas a su propia URL, que dentro de un archivo único no existe); el script falla si el cargador cambia, para no producir un archivo roto en silencio.
- No verificado: móvil real, Safari/Firefox, una persona jugando.
