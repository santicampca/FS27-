import { esc, num } from './dom.js';
import { ATTRIBUTES, KEEPER_CAPS, LINE_ES, GROUP_ES, KEEPER_STYLE_ES, LEVEL_ES, roleName } from '../core/data.js';
import { inventory } from '../app/state.js';

export const tierOf = (rating) => (rating >= 86 ? 'gold' : rating >= 80 ? 'silver' : 'bronze');

export function crest(team, size = 40) {
  const p = team.primary, s = team.secondary;
  return `<svg class="crest" width="${size}" height="${size}" viewBox="0 0 48 56" role="img" aria-label="Escudo de ${esc(team.name)}"><path d="M24 2 44 9v20c0 12-8 20-20 25C12 49 4 41 4 29V9z" fill="${esc(p)}" stroke="${esc(s)}" stroke-width="3"/><path d="M4 22h40v8H4z" fill="${esc(s)}" opacity=".9"/><text x="24" y="19" text-anchor="middle" font-size="11" font-weight="900" fill="${esc(s)}" font-family="Impact,Arial Narrow,sans-serif">${esc(team.shortName)}</text></svg>`;
}

export const cardFrame = () => inventory.equipped.card || 'default';

/** Tarjeta de jugador. Todo sale de los datos reales del Core; el marco es cosmético (tienda). */
export function playerCard(p, data, { compact = false, selected = false, tag = '' } = {}) {
  const team = data.teamsById.get(data.teamOfPlayer.get(p.id));
  const tier = tierOf(p.rating);
  const role = p.isGoalkeeper ? 'Portero' : roleName(data, p.primaryRole);
  const rows = p.isGoalkeeper ? KEEPER_CAPS.map(([k, l]) => [l, p.keeper[k]]) : ATTRIBUTES.map(([k, l]) => [l, p.attributes[k]]);
  const shown = compact ? rows.slice(0, 6) : rows;
  const bars = shown.map(([l, v]) => `<li><span>${esc(l)}</span><b>${v}</b><i style="--w:${Math.max(0, Math.min(100, v))}%"></i></li>`).join('');
  const sub = p.isGoalkeeper ? (KEEPER_STYLE_ES[p.keeperStyle] || '') : (p.style ? GROUP_ES[p.roleGroup] : '');
  return `<article class="pcard pcard--${tier} frame-${cardFrame()}${selected ? ' is-selected' : ''}" data-player="${esc(p.id)}">
    <header class="pcard__top"><span class="pcard__rating">${p.rating}</span><span class="pcard__role">${esc(role)}${sub ? `<small>${esc(sub)}</small>` : ''}</span>${tag ? `<span class="pcard__tag">${esc(tag)}</span>` : ''}</header>
    <h4 class="pcard__name">${esc(p.name)}</h4>
    <p class="pcard__team">${team ? crest(team, 18) + esc(team.name) : '<em>Sin equipo asignado</em>'}</p>
    <ul class="pcard__bars">${bars}</ul>
    ${compact ? '' : `<p class="pcard__facts">${esc(LINE_ES[p.line] || p.line)} · ${p.age} años · ${p.heightCm} cm · pie ${p.foot === 'Left' ? 'izquierdo' : p.foot === 'Right' ? 'derecho' : 'ambos'}${p.isGoalkeeper ? '' : ` · ${esc(p.zone)}`}</p>`}
  </article>`;
}

/** Campo con la alineación de un equipo (datos reales de la formación y de los jugadores). */
export function lineupField(team, lineup, data, { color } = {}) {
  const col = color || team.primary;
  const pos = (depth, width) => [4 + depth * 90, 7 + width * 48.5];
  const tokens = [];
  const gk = data.playersById.get(lineup.goalkeeper);
  if (gk) tokens.push(token(gk, 5, 31.25, col, true));
  for (const s of lineup.field) { const p = data.playersById.get(s.id); if (p) { const [x, y] = pos(s.depth, s.width); tokens.push(token(p, x, y, col, false)); } }
  return `<svg class="lineup" viewBox="0 0 100 62.5" role="img" aria-label="Alineación de ${esc(team.name)}"><rect width="100" height="62.5" rx="2" class="lineup__grass"/>
    <g class="lineup__lines"><rect x="2" y="2" width="96" height="58.5"/><path d="M50 2v58.5"/><circle cx="50" cy="31.25" r="8"/><rect x="2" y="17" width="12" height="28.5"/><rect x="86" y="17" width="12" height="28.5"/></g>${tokens.join('')}</svg>`;
}
function token(p, x, y, col, gk) {
  return `<g class="tok" transform="translate(${x.toFixed(2)} ${y.toFixed(2)})" data-player="${esc(p.id)}" tabindex="0" role="button" aria-label="${esc(p.name)}, valoración ${p.rating}"><circle r="3.6" fill="${gk ? '#ffd23d' : esc(col)}" stroke="#fff" stroke-width=".6"/><text y="1.2" text-anchor="middle" font-size="3.4" font-weight="800" fill="#06101f">${p.number}</text><text x="${gk ? 4.2 : 0}" y="${gk ? 1 : 7.2}" text-anchor="${gk ? 'start' : 'middle'}" font-size="2.6" font-weight="700" fill="#fff" stroke="#000" stroke-width=".35" paint-order="stroke">${esc(p.shortName)}</text></g>`;
}

export const levelPill = (t) => `<span class="pill pill--ghost">${esc(LEVEL_ES[t.level] || t.level)}${t.kind === 'User' ? ' · tuyo' : ''}</span>`;
export const avgRating = (team, data) => { const r = team.active.map((id) => data.playersById.get(id).rating); return r.reduce((a, b) => a + b, 0) / r.length; };
export const teamCard = (t, data, { selected = false, disabled = false, attr = 'data-team' } = {}) =>
  `<button type="button" class="tcard${selected ? ' is-selected' : ''}" ${attr}="${esc(t.id)}" ${disabled ? 'disabled' : ''} aria-pressed="${selected}" style="--tc1:${esc(t.primary)};--tc2:${esc(t.secondary)}">${crest(t, 44)}<span class="tcard__txt"><b>${esc(t.name)}</b><small>${esc(LEVEL_ES[t.level] || t.level)}${t.kind === 'User' ? ' · Mi Equipo' : ''} · media ${num(avgRating(t, data), 1)}</small></span></button>`;

export function playerDialog(p, data) {
  const d = document.createElement('dialog'); d.className = 'dlg dlg--card';
  d.innerHTML = `<div class="dlg__box">${playerCard(p, data)}<div class="dlg__actions"><button class="btn btn--primary" autofocus>Cerrar</button></div></div>`;
  document.body.appendChild(d);
  d.querySelector('button').addEventListener('click', () => d.close());
  d.addEventListener('click', (e) => { if (e.target === d) d.close(); });
  d.addEventListener('close', () => d.remove());
  if (typeof d.showModal === 'function') d.showModal(); else d.setAttribute('open', '');
}
