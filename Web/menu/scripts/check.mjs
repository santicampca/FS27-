// Comprobaciones estáticas, sin dependencias: archivos, seis opciones del menú, rutas, referencias, sin recursos externos y sintaxis de todo el JS.
import { readFileSync, existsSync, readdirSync, statSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { join, resolve, relative } from 'node:path';

const pub = resolve(fileURLToPath(new URL('..', import.meta.url)), 'public');
const errors = [];
const need = (c, m) => { if (!c) errors.push(m); };
const walk = (d) => readdirSync(d).flatMap((f) => { const p = join(d, f); return statSync(p).isDirectory() ? walk(p) : [p]; });

for (const f of ['index.html', 'styles.css', 'app.css', 'favicon.svg', 'js/main.js', 'data/fs27-data.json', 'data/fs27-penalty-lab.json', 'data/fs27-var-scenarios.json', 'data/fs27-vectors.json', 'data/traces/index.json']) need(existsSync(join(pub, f)), `falta public/${f}`);
if (!errors.length) {
  const html = readFileSync(join(pub, 'index.html'), 'utf8');
  const main = readFileSync(join(pub, 'js/main.js'), 'utf8');
  const routes = [...html.matchAll(/data-route="([a-z]+)"/g)].map((m) => m[1]);
  const want = ['arena', 'classic', 'equipo', 'entrenamiento', 'tienda', 'configuracion'];
  need(JSON.stringify(routes) === JSON.stringify(want), `el menú debe tener exactamente ${want.join(', ')}; hay: ${routes.join(', ')}`);
  for (const r of want) need(new RegExp(`\\b${r}\\s*:`).test(main), `main.js no define la ruta "${r}"`);
  for (const n of ['ARENA', 'CLASSIC', 'EQUIPO', 'ENTRENAMIENTO', 'TIENDA', 'CONFIGURACIÓN']) need(html.includes(`>${n}<`), `el menú no muestra "${n}"`);
  const files = walk(pub);
  for (const f of files.filter((x) => /\.(js|html|css)$/.test(x))) {
    const text = readFileSync(f, 'utf8');
    need(!/https?:\/\/(?!www\.w3\.org)/.test(text), `${relative(pub, f)}: no debe haber recursos externos`);
    if (f.endsWith('.js')) {
      need(!/\bXMLHttpRequest\b|\bWebSocket\b|\bsendBeacon\b/.test(text), `${relative(pub, f)}: no debe llamar a ningún endpoint`);
      if (!f.endsWith(join('core', 'data.js'))) need(!/\bfetch\s*\(/.test(text), `${relative(pub, f)}: solo core/data.js puede leer (los datos locales)`);
      try { execFileSync(process.execPath, ['--check', f], { stdio: 'pipe' }); } catch (e) { errors.push(`${relative(pub, f)} no compila: ${e.stderr}`); }
    }
  }
  // todo import relativo existe
  for (const f of files.filter((x) => x.endsWith('.js'))) for (const m of readFileSync(f, 'utf8').matchAll(/(?:from|import\()\s*['"](\.[^'"]+)['"]/g)) need(existsSync(resolve(f, '..', m[1])), `${relative(pub, f)} importa ${m[1]} que no existe`);
  const idx = JSON.parse(readFileSync(join(pub, 'data/traces/index.json'), 'utf8'));
  for (const t of idx.traces) need(existsSync(join(pub, 'data', t.file)), `falta la grabación ${t.file}`);
}
if (errors.length) { console.error('FALLO:\n- ' + errors.join('\n- ')); process.exit(1); }
console.log('check OK: 6 opciones, rutas, módulos sin errores de sintaxis, imports existentes, sin recursos externos ni endpoints.');
