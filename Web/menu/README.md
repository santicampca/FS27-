# FS27 — menú web

Sitio estático sin dependencias de ejecución (HTML + CSS + JS con módulos). Muestra **datos y resultados reales que exporta el Core de FS27** (equipos, jugadores, penales, VAR, grabaciones de partidos). **No ejecuta el Core en vivo** ni juega partidos completos. Ver `Docs/PHASES_24_27.md`.

Seis opciones: ARENA, CLASSIC, EQUIPO, ENTRENAMIENTO, TIENDA, CONFIGURACIÓN (la cartera de ZP se abre desde el contador de la cabecera).

## Local
```
cd Web/menu
npm run dev      # http://localhost:5173/  (sirve public/)
npm run build    # check + test + copia public/ a dist/
npm start        # sirve dist/
node scripts/e2e.mjs http://localhost:5173/   # recorrido con Playwright (opcional, requiere Playwright global)
```
Requiere Node >= 18. Cero dependencias.

## Datos (public/data)
Los genera el Core, no se editan a mano. Para regenerarlos tras un cambio en el Core: `Tools/webdata.sh` desde la raíz del repositorio (necesita .NET 8). Los tests del Core (`--filter Name~WebExport`) fallan si los archivos versionados están desactualizados.

## Publicar en Vercel (manual)
1. Importa el repositorio y fija **Root Directory** = `Web/menu`.
2. Framework Preset: *Other*. Install: `npm install --no-audit --no-fund`. Build: `npm run build`. Output: `dist` (ya están en `vercel.json`).
3. La rama de producción es la que elijas en *Settings → Environments → Production → Branch Tracking*.

## Alcance
- La cartera de ZP, el inventario de la tienda y los ajustes viven solo en el navegador (`localStorage`, claves `fs27.*`).
- Los penales, la tanda y el VAR muestran respuestas reales del Core; los controles A/B/C son solo una referencia visual.
