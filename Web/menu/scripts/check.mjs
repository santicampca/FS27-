// Comprobaciones estáticas, sin dependencias: archivos, seis rutas, referencias y sintaxis de JS.
import { readFileSync, existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { join, resolve } from 'node:path';

const pub = resolve(fileURLToPath(new URL('..', import.meta.url)), 'public');
const errors = [];
const need = (c, m) => { if (!c) errors.push(m); };

for (const f of ['index.html', 'styles.css', 'app.js', 'favicon.svg']) need(existsSync(join(pub, f)), `falta public/${f}`);
if (!errors.length) {
  const html = readFileSync(join(pub, 'index.html'), 'utf8');
  const js = readFileSync(join(pub, 'app.js'), 'utf8');
  const routes = [...html.matchAll(/data-route="([a-z]+)"/g)].map((m) => m[1]);
  const want = ['arena', 'classic', 'equipo', 'entrenamiento', 'tienda', 'configuracion'];
  need(JSON.stringify(routes) === JSON.stringify(want), `el menú debe tener exactamente ${want.join(', ')}; hay: ${routes.join(', ')}`);
  for (const r of want) need(new RegExp(`\\b${r}\\s*\\(\\)\\s*\\{`).test(js), `app.js no define la pantalla "${r}"`);
  for (const n of ['ARENA', 'CLASSIC', 'EQUIPO', 'ENTRENAMIENTO', 'TIENDA', 'CONFIGURACIÓN']) need(html.includes(`>${n}<`), `el menú no muestra "${n}"`);
  need(!/https?:\/\/(?!www\.w3\.org)/.test(html + js), 'no debe haber recursos externos ni llamadas de red');
  need(!/\b(fetch|XMLHttpRequest|WebSocket)\b/.test(js), 'app.js no debe llamar a ningún endpoint');
  try { execFileSync(process.execPath, ['--check', join(pub, 'app.js')], { stdio: 'pipe' }); } catch (e) { errors.push('app.js no compila: ' + e.stderr); }
}
if (errors.length) { console.error('FALLO:\n- ' + errors.join('\n- ')); process.exit(1); }
console.log('check OK: 6 opciones, 6 pantallas, sin recursos externos ni endpoints.');
