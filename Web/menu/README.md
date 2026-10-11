# FS27 — menú web (demostración de interfaz)

Sitio estático sin dependencias (HTML + CSS + JS). **Solo interfaz**: no ejecuta el Core C#, ni partidos, ni penales, ni VAR, y no hace llamadas de red. Los datos (equipo, NIV, ZP) son de ejemplo.

Seis opciones: ARENA, CLASSIC, EQUIPO, ENTRENAMIENTO, TIENDA, CONFIGURACIÓN. Cada una abre una pantalla provisional con botón VOLVER (también `Esc`). Flechas del teclado para moverse por el menú, `Intro` para entrar; funciona con ratón y táctil.

## Local
```
cd Web/menu
npm run dev      # http://localhost:5173/  (sirve public/)
npm run build    # comprueba y copia public/ a dist/
npm start        # sirve dist/
```
Requiere Node >= 18. Cero dependencias (no hace falta `npm install`).

## Publicar en Vercel (manual; no se ha desplegado nada)
1. Importa el repositorio en Vercel y fija **Root Directory** = `Web/menu`.
2. Framework Preset: *Other*. Install: `npm install --no-audit --no-fund`. Build: `npm run build`. Output: `dist` (ya están en `vercel.json`).
3. Deploy. El plan gratuito basta.

## Alcance
- Configuración: solo «Reducir animaciones» tiene efecto (en esta web, vía `localStorage`); el resto es maqueta.
- Tienda: sin pagos ni saldo ZP real.
