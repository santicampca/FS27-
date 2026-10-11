// Arena y Classic antes de empezar: reglas del modo (del Core), equipo, rival, dificultad, plantilla y confirmación.
import { $, $$, esc, mmss, toast } from './dom.js';
import { loadData, LEVEL_ES } from '../core/data.js';
import { head, note, loading, failure } from './screen.js';
import { teamCard, lineupField, crest } from './components.js';
import { selection, setSelection } from '../app/state.js';

const yes = '<span class="ok" aria-label="Sí">●</span> Sí', no = '<span class="no" aria-label="No">○</span> No';
const DIFF_TIPS = { Novice: 'Errores frecuentes y reacciones lentas.', Amateur: 'Juega con margen de error.', Professional: 'Nivel de referencia del juego.', Elite: 'Decisiones rápidas y pocos errores.', Legend: 'La IA más exigente.' };

function rulesCard(mode, r) {
  const classic = mode === 'Classic';
  const rows = classic
    ? [['Duración', `${r.regulationPeriods} tiempos de ${mmss(r.periodSeconds)} (reloj del Core)`], ['Descanso', `${r.halftimeSeconds} s`], ['Árbitro', r.referee ? yes : no], ['Faltas y tiros libres', r.fouls ? yes : no], ['Tarjetas', r.cards ? yes : no], ['Fueras de juego', r.offside ? yes : no],
       ['Penales (por falta en el área)', r.penalties ? yes : no], ['VAR (gasta ZP)', r.var ? yes : no], ['Córners, saques de banda y de puerta', yes], ['Muros', r.usesWalls ? yes : no], ['Cambios', `hasta ${r.substitutionLimit}`]]
    : [['Duración', `${r.regulationPeriods === 1 ? 'un tiempo' : r.regulationPeriods + ' tiempos'} de ${mmss(r.periodSeconds)} (reloj del Core)`], ['Muros alrededor del campo', r.usesWalls ? yes : no], ['Faltas y tarjetas', r.fouls || r.cards ? yes : no], ['Árbitro', r.referee ? yes : no],
       ['Penales por falta', r.penalties ? yes : no], ['VAR', r.var ? yes : no], ['Fueras de juego', r.offside ? yes : no], ['Córners y saques', r.corners ? yes : no]];
  return `<section class="card card--wide"><h3>Reglas de ${classic ? 'Classic' : 'Arena'} <small>(del Core)</small></h3><dl class="rules">${rows.map(([k, v]) => `<div><dt>${esc(k)}</dt><dd>${v}</dd></div>`).join('')}</dl>
    <p class="field-note">${classic ? 'Si una competición exige ganador y el partido termina empatado, se decide por una tanda de penales de ' + r.shootoutKicksPerTeam + ' tiros por equipo con final anticipado y muerte súbita.' : 'Arena es arcade: sin faltas ni VAR. Si una competición exige ganador y hay empate, se define por penales arcade (el Core ya los resuelve; esta web aún no los reproduce).'}</p></section>`;
}

export async function render(el, { mode, navigate }) {
  const classic = mode === 'Classic';
  const title = classic ? 'CLASSIC' : 'ARENA';
  el.innerHTML = head(title, title, classic ? 'MODO PRINCIPAL' : 'MODO ARCADE', classic ? 'El fútbol 6 contra 6 con árbitro, faltas, tarjetas, penales y VAR. Prepara el partido: las reglas y los equipos son los del Core.' : 'Partidos rápidos y directos con muros. Prepara el partido: las reglas y los equipos son los del Core.') + loading();
  let data;
  try { data = await loadData(); } catch (e) { el.innerHTML = head(title, title, 'SIN DATOS', '') + failure(e); return; }
  if (selection.mode !== mode) setSelection({ mode });
  if (!data.teamsById.has(selection.team)) setSelection({ team: data.teams.find((t) => t.kind === 'User').id });
  if (!data.teamsById.has(selection.rival) || selection.rival === selection.team) setSelection({ rival: data.teams.find((t) => t.id !== selection.team).id });
  if (!data.difficulties.some((d) => d.level === selection.difficulty)) setSelection({ difficulty: 'Professional' });

  const draw = () => {
    const mine = data.teamsById.get(selection.team), rival = data.teamsById.get(selection.rival);
    const userF = mine.kind === 'User' ? (data.userFormations.find((f) => f.id === selection.userFormationId) || data.userFormations.find((f) => f.id === mine.formationId) || data.userFormations[0]) : null;
    const lineup = userF ? userF.lineup : mine.lineup;
    const errors = [];
    if (!mine) errors.push('Elige tu equipo.'); if (!rival) errors.push('Elige un rival.'); if (mine && rival && mine.id === rival.id) errors.push('El rival debe ser otro equipo.');
    if (!data.difficulties.some((d) => d.level === selection.difficulty)) errors.push('Elige una dificultad.');
    el.innerHTML = head(title, title, classic ? 'MODO PRINCIPAL' : 'MODO ARCADE', classic ? 'El fútbol 6 contra 6 con árbitro, faltas, tarjetas, penales y VAR. Prepara el partido: las reglas y los equipos son los del Core.' : 'Partidos rápidos y directos con muros. Prepara el partido: las reglas y los equipos son los del Core.') +
      `<ol class="steps">
        <li class="step"><h3><span>1</span> Reglas del modo</h3>${rulesCard(mode, data.rules[mode])}</li>
        <li class="step"><h3><span>2</span> Tu equipo</h3><div class="tpick" role="group" aria-label="Tu equipo">${data.teams.map((t) => teamCard(t, data, { selected: t.id === selection.team, attr: 'data-mine' })).join('')}</div></li>
        <li class="step"><h3><span>3</span> Rival</h3><div class="tpick" role="group" aria-label="Rival">${data.teams.map((t) => teamCard(t, data, { selected: t.id === selection.rival, disabled: t.id === selection.team, attr: 'data-rival' })).join('')}</div></li>
        <li class="step"><h3><span>4</span> Dificultad</h3><div class="segment segment--wide" role="group" aria-label="Dificultad">${data.difficulties.map((d) => `<button type="button" data-diff="${esc(d.level)}" aria-pressed="${d.level === selection.difficulty}">${esc(d.name)}</button>`).join('')}</div>
          <p class="field-note">${esc(DIFF_TIPS[selection.difficulty] || '')} Es la dificultad del Core que gobierna a la IA rival.</p></li>
        <li class="step"><h3><span>5</span> Plantilla</h3><div class="split"><div>${lineupField(mine, lineup, data)}</div><div>
          ${mine.kind === 'User' ? `<label class="fld"><span>Formación de Mi Equipo</span><select id="uf">${data.userFormations.map((f) => `<option value="${esc(f.id)}"${userF.id === f.id ? ' selected' : ''}>${esc(f.name)} (${esc(f.id)})</option>`).join('')}</select></label>` : `<p class="field-note">Formación del entrenador: <b>${esc(mine.formationName)}</b>. Solo Mi Equipo permite elegir la formación.</p>`}
          <p class="field-note"><a href="#/equipo/${esc(mine.id)}">Ver la plantilla completa</a></p></div></div></li>
        <li class="step"><h3><span>6</span> Confirmar</h3>
          <div class="versus"><div>${crest(mine, 56)}<b>${esc(mine.name)}</b><small>Local · ${esc(LEVEL_ES[mine.level])}</small></div><span class="vs">VS</span><div>${crest(rival, 56)}<b>${esc(rival.name)}</b><small>Visitante · ${esc(LEVEL_ES[rival.level])}</small></div></div>
          <p class="field-note">${esc(title)} · dificultad ${esc((data.difficulties.find((d) => d.level === selection.difficulty) || {}).name || '')}</p>
          ${errors.length ? `<ul class="errors" role="alert">${errors.map((e) => `<li>${esc(e)}</li>`).join('')}</ul>` : ''}
          <div class="actions"><button class="btn btn--primary btn--lg" id="go"${errors.length ? ' disabled' : ''}>Entrar al partido</button></div>
          ${note('<b>Qué ocurre al entrar:</b> la web abre la vista del partido con estas selecciones. Todavía no se ejecuta un partido interactivo del Core: se reproducen grabaciones reales del Core y se juegan escenas reales (penal, tanda, VAR). Ver <a href="#/partido">Partido</a>.')}</li></ol>`;
    $$('[data-mine]', el).forEach((b) => b.addEventListener('click', () => { const id = b.dataset.mine; setSelection({ team: id, ...(selection.rival === id ? { rival: data.teams.find((t) => t.id !== id).id } : {}) }); draw(); $(`[data-mine="${id}"]`, el)?.focus(); }));
    $$('[data-rival]', el).forEach((b) => b.addEventListener('click', () => { setSelection({ rival: b.dataset.rival }); draw(); $(`[data-rival="${b.dataset.rival}"]`, el)?.focus(); }));
    $$('[data-diff]', el).forEach((b) => b.addEventListener('click', () => { setSelection({ difficulty: b.dataset.diff }); draw(); $(`[data-diff="${b.dataset.diff}"]`, el)?.focus(); }));
    const uf = $('#uf', el); if (uf) uf.addEventListener('change', () => { setSelection({ userFormationId: uf.value }); draw(); });
    const go = $('#go', el); if (go) go.addEventListener('click', () => { if (errors.length) return toast('Revisa las selecciones', 'bad'); setSelection({ mode }); navigate('#/partido'); });
    const bar = $('[data-back]', el); if (bar) bar.addEventListener('click', () => navigate('#/'));
  };
  draw();
}
