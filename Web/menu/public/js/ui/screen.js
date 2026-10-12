import { esc } from './dom.js';
export const backButton = (label = 'VOLVER', to = '') => `<button type="button" class="back" data-back="${esc(to)}"><i></i> ${esc(label)}</button>`;
export const head = (crumb, title, pill, lead, backTo = '') => `
  <div class="screen__bar">${backButton('VOLVER', backTo)}<span class="screen__crumb">Menú · ${esc(crumb)}</span></div>
  <div class="screen__tags"><span class="pill">${esc(pill)}</span></div>
  <h2 id="screen-title" tabindex="-1">${esc(title)}</h2>${lead ? `<p class="screen__lead">${lead}</p>` : ''}`;
export const note = (html, kind = '') => `<p class="note ${kind}">${html}</p>`;
export const loading = (what = 'datos del Core') => `<p class="loading" role="status"><span class="spinner" aria-hidden="true"></span> Cargando ${esc(what)}…</p>`;
export const failure = (e) => `<p class="note note--bad" role="alert"><b>No se pudieron cargar los datos.</b> ${esc(e && e.message ? e.message : e)}. Comprueba la conexión y recarga la página.</p>`;
