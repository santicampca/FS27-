# FS27 — cliente jugable en el navegador (primer partido real)

El **Core real** (C#, sin cambios) compilado a WebAssembly y un cliente táctil mínimo: stick + A/B/C, vista cenital provisional. Es la primera vez que el juego se puede **jugar en tiempo real** (antes solo existía el Core simulado y un visor que reproduce grabaciones).

- `Fs27.Wasm/` — anfitrión: `GameBridge` (tiempo fijo, entrada humana por `MatchController.SubmitHumanInput`, `BasicDecisionDriver` para ambos equipos, instantánea para dibujar). No contiene reglas, física ni IA.
- `web/` — página: menú, bucle de dibujo, stick, A/B/C con gestos (tap, deslizar arriba/abajo), cambio de jugador, pausa. Sin dependencias de ejecución.
- `build.sh` — publica el sitio estático en `Client/dist` (el Core + los datos de `Docs/authoring`).
- `test/verify.sh` — compila, juega el **mismo partido completo** en nativo y en Chromium con emulación táctil y compara; `test/smoke.mjs` es la comprobación en navegador.

Requisitos para construir/verificar (no son dependencias del juego): .NET 8 SDK + `dotnet workload install wasm-experimental`; para `verify.sh`, node ≥ 18, un Chromium y `playwright-core` (`PLAYWRIGHT_CORE=/ruta/index.mjs`).
Probar a mano: `Client/build.sh` y `node Client/test/serve.mjs Client/dist 8080`, abrir `http://localhost:8080` (en el móvil: la IP del ordenador). Parámetros de URL: `?auto=1&home=0&away=1&seed=7&mode=Classic&level=Professional`. Teclado: flechas/WASD, J/K/L = A/B/C (Shift = deslizar abajo, Ctrl = arriba), Tab = cambiar jugador.

Qué NO es todavía: no tiene arte, audio ni cámara 3D; no se ha probado en un móvil real ni por una persona; no gestiona saques/penaltis/tarjetas con interfaz propia (el Core los resuelve con su IA); no está desplegado.
