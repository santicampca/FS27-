import { $, $$, esc, num } from './dom.js';
import { loadData, ATTRIBUTES, LEVEL_ES, GROUP_ES, roleName } from '../core/data.js';
import { head, note, loading, failure } from './screen.js';
import { crest, playerCard, lineupField, teamCard, levelPill, avgRating, playerDialog, tierOf } from './components.js';
import { selection, setSelection } from '../app/state.js';
import { toast } from './dom.js';

const PAGE = 24;

export async function render(el, { segments, query, navigate }) {
  el.innerHTML = head('Equipo', 'EQUIPO', 'DATOS REALES DEL CORE', 'Los 12 equipos y los 160 jugadores salen de la base de datos cerrada del Core (nombres ficticios publicados). Aquí solo se consultan: la edición todavía no existe en el Core.') + loading();
  let data;
  try { data = await loadData(); } catch (e) { el.innerHTML = head('Equipo', 'EQUIPO', 'SIN DATOS', '') + failure(e); return; }
  const team = segments[1] && data.teamsById.get(segments[1]);
  if (segments[1] && !team) { navigate('#/equipo'); return; }
  if (team) return teamView(el, data, team, navigate);
  const tab = query.get('tab') === 'jugadores' ? 'jugadores' : 'equipos';
  el.innerHTML = head('Equipo', 'EQUIPO', 'DATOS REALES DEL CORE', `Los ${data.teams.length} equipos y los ${data.players.length} jugadores salen de la base de datos cerrada del Core (nombres ficticios publicados). Aquí solo se consultan: la edición todavía no existe en el Core.`) +
    `<div class="tabs" role="tablist" aria-label="Vista">
      <a role="tab" class="tab" href="#/equipo" aria-selected="${tab === 'equipos'}">Equipos <small>${data.teams.length}</small></a>
      <a role="tab" class="tab" href="#/equipo?tab=jugadores" aria-selected="${tab === 'jugadores'}">Jugadores <small>${data.players.length}</small></a></div><div id="eq-body"></div>`;
  if (tab === 'equipos') teamsGrid($('#eq-body', el), data);
  else playersCatalog($('#eq-body', el), data, query, navigate);
}

function teamsGrid(host, data) {
  host.innerHTML = `<div class="tgrid">${data.teams.map((t) => `<a class="tbig" href="#/equipo/${esc(t.id)}" style="--tc1:${esc(t.primary)};--tc2:${esc(t.secondary)}">${crest(t, 56)}
    <span class="tbig__txt"><b>${esc(t.name)}</b>${levelPill(t)}<small>${esc(t.theme)}</small><small>Entrenador: ${esc(t.coach || '—')} · formación ${esc(t.formationName)}</small>
    <small class="tbig__rate">Media de titulares <b>${num(avgRating(t, data), 1)}</b></small></span></a>`).join('')}</div>
    <p class="note">${data.unassignedPlayerIds.length} de los ${data.players.length} jugadores del catálogo no pertenecen a ningún equipo en el sorteo actual (semilla ${data.rosterSeed}): se muestran en «Jugadores» como <em>sin equipo asignado</em>.</p>`;
}

function teamView(el, data, team, navigate) {
  const isUser = team.kind === 'User';
  let lineup = team.lineup, formationId = team.formationId;
  if (isUser) {
    const stored = selection.userFormationId;
    const f = data.userFormations.find((x) => x.id === stored) || data.userFormations.find((x) => x.id === team.formationId) || data.userFormations[0];
    lineup = f.lineup; formationId = f.id;
  }
  const row = (id) => { const p = data.playersById.get(id); return `<li><button class="prow" data-player="${esc(id)}"><span class="num">${p.number}</span><span class="nm">${esc(p.name)}<small>${esc(p.isGoalkeeper ? 'Portero' : roleName(data, p.primaryRole))}</small></span><span class="rating tier-${tierOf(p.rating)}">${p.rating}</span></button></li>`; };
  el.innerHTML = head(team.name, team.name.toUpperCase(), (LEVEL_ES[team.level] || team.level).toUpperCase() + (isUser ? ' · MI EQUIPO' : ''), esc(team.description), '#/equipo') +
    `<div class="split"><div class="lineup-wrap">${lineupField(team, lineup, data)}
      ${isUser ? `<label class="fld"><span>Formación de Mi Equipo</span><select id="uf">${data.userFormations.map((f) => `<option value="${esc(f.id)}"${f.id === formationId ? ' selected' : ''}>${esc(f.name)} (${esc(f.id)})</option>`).join('')}</select></label>
        ${note('Las formaciones base son las del Core y se aplican al entrar a un partido. El nombre, los colores y el escudo de Mi Equipo todavía no se pueden editar: el Core los fija en una fase posterior.')}` : ''}
      <p class="field-note">Formación <b>${esc(isUser ? (data.userFormations.find((f) => f.id === formationId) || {}).name : team.formationName)}</b> · entrenador <b>${esc(team.coach || '—')}</b> · ${esc(team.theme)}</p></div>
      <div><h3 class="sub">Titulares</h3><ul class="roster2">${team.active.map(row).join('')}</ul><h3 class="sub">Suplentes</h3><ul class="roster2">${team.bench.map(row).join('')}</ul></div></div>
    <div class="actions"><button class="btn btn--primary" id="use-team">Usar en un partido</button><a class="btn btn--ghost" href="#/equipo?tab=jugadores">Ver todos los jugadores</a></div>`;
  const open = (id) => { const p = data.playersById.get(id); if (p) playerDialog(p, data); };
  el.addEventListener('click', (e) => { const t = e.target.closest('[data-player]'); if (t) open(t.dataset.player); });
  el.addEventListener('keydown', (e) => { if ((e.key === 'Enter' || e.key === ' ') && e.target.matches('.tok')) { e.preventDefault(); open(e.target.dataset.player); } });
  const uf = $('#uf', el); if (uf) uf.addEventListener('change', () => { setSelection({ userFormationId: uf.value }); toast('Formación guardada para tus próximos partidos'); navigate('#/equipo/' + team.id); });
  $('#use-team', el).addEventListener('click', () => { setSelection({ team: team.id, ...(selection.rival === team.id ? { rival: data.teams.find((t) => t.id !== team.id).id } : {}) }); toast(team.name + ' elegido como tu equipo'); navigate('#/' + selection.mode.toLowerCase()); });
}

function playersCatalog(host, data, query, navigate) {
  const q = { text: query.get('q') || '', team: query.get('team') || '', role: query.get('role') || '', sort: query.get('sort') || 'rating', page: Number(query.get('page') || 1) };
  const sorts = [['rating', 'Valoración'], ['name', 'Nombre'], ...ATTRIBUTES.map(([k, l]) => ['attr:' + k, l])];
  host.innerHTML = `<form class="filters" id="flt" role="search">
      <label class="fld"><span>Buscar</span><input type="search" name="q" value="${esc(q.text)}" placeholder="Nombre…" autocomplete="off"></label>
      <label class="fld"><span>Equipo</span><select name="team"><option value="">Todos</option><option value="none"${q.team === 'none' ? ' selected' : ''}>Sin equipo asignado</option>${data.teams.map((t) => `<option value="${esc(t.id)}"${q.team === t.id ? ' selected' : ''}>${esc(t.name)}</option>`).join('')}</select></label>
      <label class="fld"><span>Rol</span><select name="role"><option value="">Todos</option><option value="GK"${q.role === 'GK' ? ' selected' : ''}>Portero</option>${data.roles.map((r) => `<option value="${esc(r.id)}"${q.role === r.id ? ' selected' : ''}>${esc(r.name)} (${esc(GROUP_ES[r.group])})</option>`).join('')}</select></label>
      <label class="fld"><span>Ordenar por</span><select name="sort">${sorts.map(([v, l]) => `<option value="${esc(v)}"${q.sort === v ? ' selected' : ''}>${esc(l)}</option>`).join('')}</select></label></form>
    <p class="count" id="cnt" role="status"></p><div class="pgrid" id="pg"></div><div class="actions"><button class="btn btn--ghost" id="more" hidden>Mostrar más</button></div>`;
  const grid = $('#pg', host), cnt = $('#cnt', host), more = $('#more', host);
  let shown = PAGE * Math.max(1, q.page);
  const filtered = () => {
    let list = data.players.slice();
    const text = q.text.trim().toLowerCase();
    if (text) list = list.filter((p) => (p.name + ' ' + p.shortName).toLowerCase().includes(text));
    if (q.team === 'none') list = list.filter((p) => !data.teamOfPlayer.has(p.id)); else if (q.team) list = list.filter((p) => data.teamOfPlayer.get(p.id) === q.team);
    if (q.role === 'GK') list = list.filter((p) => p.isGoalkeeper); else if (q.role) list = list.filter((p) => p.primaryRole === q.role);
    const val = (p) => (q.sort.startsWith('attr:') ? (p.isGoalkeeper ? -1 : p.attributes[q.sort.slice(5)]) : p.rating);
    if (q.sort === 'name') list.sort((a, b) => a.name.localeCompare(b.name, 'es')); else list.sort((a, b) => val(b) - val(a) || a.name.localeCompare(b.name, 'es'));
    return list;
  };
  const draw = () => {
    const list = filtered();
    cnt.textContent = list.length ? `${list.length} jugador${list.length === 1 ? '' : 'es'}` + (list.length > shown ? ` · mostrando ${shown}` : '') : 'Ningún jugador coincide con los filtros.';
    grid.innerHTML = list.slice(0, shown).map((p) => playerCard(p, data, { compact: true })).join('');
    more.hidden = list.length <= shown;
  };
  $('#flt', host).addEventListener('input', (e) => {
    const f = new FormData($('#flt', host)); q.text = f.get('q') || ''; q.team = f.get('team') || ''; q.role = f.get('role') || ''; q.sort = f.get('sort') || 'rating'; shown = PAGE;
    if (e.target.name !== 'q') { const p = new URLSearchParams({ tab: 'jugadores' }); for (const k of ['team', 'role', 'sort']) if (q[k] && !(k === 'sort' && q[k] === 'rating')) p.set(k, q[k]); history.replaceState(null, '', '#/equipo?' + p); }
    draw();
  });
  $('#flt', host).addEventListener('submit', (e) => e.preventDefault());
  more.addEventListener('click', () => { shown += PAGE; draw(); });
  host.addEventListener('click', (e) => { const c = e.target.closest('.pcard'); if (c) playerDialog(data.playersById.get(c.dataset.player), data); });
  draw();
}
