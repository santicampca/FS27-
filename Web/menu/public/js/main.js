// FS27 — menú web. Router por hash, menú principal con las seis opciones y cartera de ZP en la cabecera.
// Esta web NO ejecuta el Core: muestra datos exportados por el Core y reproduce resultados reales que el Core grabó. Ver Docs/PHASES_24_27.md.
import { $, $$, esc } from './ui/dom.js';
import { applySettings, ledger, onLedger, takeLedgerNotice } from './app/state.js';
import { toast } from './ui/dom.js';

applySettings();

const body = document.body, screenEl = $('#screen'), main = $('#main'), chip = $('#zp-chip');
const ROUTES = {
  arena: () => import('./ui/matchsetup.js').then((m) => (el, c) => m.render(el, { ...c, mode: 'Arena' })),
  classic: () => import('./ui/matchsetup.js').then((m) => (el, c) => m.render(el, { ...c, mode: 'Classic' })),
  equipo: () => import('./ui/equipo.js').then((m) => m.render),
  entrenamiento: () => import('./ui/entrenamiento.js').then((m) => m.render),
  tienda: () => import('./ui/tienda.js').then((m) => m.render),
  configuracion: () => import('./ui/configuracion.js').then((m) => m.render),
  cartera: () => import('./ui/cartera.js').then((m) => m.render),
  partido: () => import('./ui/partido.js').then((m) => m.render),
};
const THEME = { arena: 'arena', classic: 'classic', equipo: 'equipo', entrenamiento: 'entrenamiento', tienda: 'tienda', configuracion: 'configuracion', cartera: 'tienda', partido: 'classic' };
const TITLES = { arena: 'Arena', classic: 'Classic', equipo: 'Equipo', entrenamiento: 'Entrenamiento', tienda: 'Tienda', configuracion: 'Configuración', cartera: 'Cartera', partido: 'Partido' };

const showBalance = () => { if (chip) { chip.innerHTML = `<span class="gem" aria-hidden="true"></span> <b>${ledger.balance}</b> <small>ZP</small>`; chip.setAttribute('aria-label', `Cartera: ${ledger.balance} Zenit Points`); } };
showBalance(); onLedger(showBalance);
const notice = takeLedgerNotice(); if (notice) toast(notice, 'bad');

export function navigate(hash) { if (location.hash === hash) render(); else location.hash = hash; }

function parse() {
  const raw = location.hash.replace(/^#\/?/, ''), [path, qs] = raw.split('?');
  return { segments: path.split('/').filter(Boolean).map(decodeURIComponent), query: new URLSearchParams(qs || '') };
}

let lastRoute = '', teardown = null, token = 0;
async function render() {
  const { segments, query } = parse(), route = segments[0] && ROUTES[segments[0]] ? segments[0] : '';
  if (teardown) { try { teardown(); } catch { /* nada */ } teardown = null; }
  const my = ++token;
  if (!route) {
    body.dataset.view = 'menu'; screenEl.hidden = true; screenEl.innerHTML = ''; delete screenEl.dataset.theme; document.title = 'FS27 — Menú principal';
    const tile = $(`[data-route="${lastRoute}"]`) || null; if (lastRoute && tile) tile.focus({ preventScroll: true }); else main.focus({ preventScroll: true });
    if (tile) selectTile(tile); lastRoute = ''; return;
  }
  lastRoute = route; body.dataset.view = 'screen'; screenEl.hidden = false; screenEl.dataset.theme = THEME[route]; document.title = 'FS27 — ' + TITLES[route];
  const view = document.createElement('div'); view.className = 'view'; screenEl.replaceChildren(view); window.scrollTo(0, 0);
  try {
    const fn = await ROUTES[route]();
    if (my !== token) return;
    const result = await fn(view, { segments, query, navigate });
    if (my !== token) { if (typeof result === 'function') result(); return; }
    if (typeof result === 'function') teardown = result;
  } catch (e) {
    view.innerHTML = `<p class="note note--bad" role="alert"><b>No se pudo abrir esta pantalla.</b> ${esc(e && e.message)}</p><div class="actions"><a class="btn btn--primary" href="#/">Volver al menú</a></div>`;
  }
  const h = $('#screen-title', view); if (h) h.focus({ preventScroll: true });
}

// volver: botones «VOLVER» con destino opcional
screenEl.addEventListener('click', (e) => { const b = e.target.closest('[data-back]'); if (b) { e.preventDefault(); navigate(b.dataset.back || '#/'); } });

// ---- menú principal: teclado geométrico
const tiles = $$('.tile');
function selectTile(t) { tiles.forEach((x) => x.classList.toggle('is-selected', x === t && document.activeElement === t)); }
tiles.forEach((t) => { t.addEventListener('focus', () => selectTile(t)); t.addEventListener('blur', () => t.classList.remove('is-selected')); });
function neighbour(from, dir) {
  const a = from.getBoundingClientRect(), ac = [a.left + a.width / 2, a.top + a.height / 2]; let best = null, bestScore = Infinity;
  for (const t of tiles) {
    if (t === from) continue; const b = t.getBoundingClientRect(), dx = b.left + b.width / 2 - ac[0], dy = b.top + b.height / 2 - ac[1];
    const along = dir === 'ArrowRight' ? dx : dir === 'ArrowLeft' ? -dx : dir === 'ArrowDown' ? dy : -dy; if (along <= 4) continue;
    const across = dir === 'ArrowLeft' || dir === 'ArrowRight' ? Math.abs(dy) : Math.abs(dx), score = along + across * 2; if (score < bestScore) { bestScore = score; best = t; }
  }
  return best;
}
document.addEventListener('keydown', (e) => {
  if (e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey || document.body.classList.contains('has-sheet') || document.querySelector('dialog[open]')) return;
  if (body.dataset.view === 'screen') {
    const tag = (document.activeElement && document.activeElement.tagName) || '';
    if (e.key === 'Escape' || (e.key === 'Backspace' && !/INPUT|TEXTAREA|SELECT/.test(tag))) { e.preventDefault(); const b = $('[data-back]', screenEl); navigate(b ? b.dataset.back || '#/' : '#/'); }
    return;
  }
  if (!e.key.startsWith('Arrow')) return;
  const cur = tiles.includes(document.activeElement) ? document.activeElement : null;
  if (!cur) { e.preventDefault(); tiles[0].focus(); return; }
  const next = neighbour(cur, e.key); if (next) { e.preventDefault(); next.focus(); }
});

window.addEventListener('hashchange', render);
render();
