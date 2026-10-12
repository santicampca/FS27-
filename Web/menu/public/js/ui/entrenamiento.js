// Entrenamiento: práctica de penales con resultados reales del Core. Completar una sesión de 5 tiros da ZP (con límite diario y sin repeticiones).
import { $, $$, esc, toast } from './dom.js';
import { loadData, loadLab } from '../core/data.js';
import { rewards } from '../core/ledger.js';
import { head, note, loading, failure } from './screen.js';
import { teamCard, crest } from './components.js';
import { penaltyDuel } from './duel.js';
import { selection, setSelection, ledger, commitLedger, stampNow, dayKey, history } from '../app/state.js';
import { fx } from './fx.js';

const KICKS = 5;

export async function render(el, { navigate }) {
  const lead = 'Practica penales con la física real del Core: el resultado de cada tiro lo calcula el Core, no la web. Completar una sesión de 5 tiros da Zenit Points (hasta 3 sesiones al día).';
  el.innerHTML = head('Entrenamiento', 'ENTRENAMIENTO', 'PRÁCTICA DE PENALES', lead) + loading();
  let data, lab;
  try { [data, lab] = await Promise.all([loadData(), loadLab()]); } catch (e) { el.innerHTML = head('Entrenamiento', 'ENTRENAMIENTO', 'SIN DATOS', '') + failure(e); return; }
  const st = { team: data.teamsById.has(selection.team) ? selection.team : data.teams[0].id, rival: data.teamsById.has(selection.rival) ? selection.rival : data.teams[1].id, role: 'kick' };
  if (st.team === st.rival) st.rival = data.teams.find((t) => t.id !== st.team).id;

  const draw = (summary = '') => {
    const mine = data.teamsById.get(st.team), rival = data.teamsById.get(st.rival);
    const past = history.get('training').slice(0, 5);
    const z = ledger.tuning;
    el.innerHTML = head('Entrenamiento', 'ENTRENAMIENTO', 'PRÁCTICA DE PENALES', lead) + (summary || '') +
      `<section class="card card--wide"><h3>Configura la sesión</h3>
        <div class="segment segment--wide" role="group" aria-label="Rol"><button data-role="kick" aria-pressed="${st.role === 'kick'}">Soy el lanzador</button><button data-role="keep" aria-pressed="${st.role === 'keep'}">Soy el portero</button></div>
        <h4 class="sub">${st.role === 'kick' ? 'Equipo que lanza' : 'Equipo que lanza (la IA)'}</h4><div class="tpick tpick--sm" role="group">${data.teams.map((t) => teamCard(t, data, { selected: t.id === st.team, disabled: t.id === st.rival, attr: 'data-mine' })).join('')}</div>
        <h4 class="sub">Equipo que defiende (su portero te espera)</h4><div class="tpick tpick--sm" role="group">${data.teams.map((t) => teamCard(t, data, { selected: t.id === st.rival, disabled: t.id === st.team, attr: 'data-rival' })).join('')}</div>
        <div class="actions"><button class="btn btn--primary btn--lg" id="start">Empezar sesión de ${KICKS} tiros</button></div>
        ${note(`<b>Recompensa:</b> completar la sesión da <b>${z.rewardTrainingCompleted} ZP</b> (máximo ${z.trainingRewardsPerDay} sesiones al día). Abandonar a mitad no da nada. Cada sesión tiene su propio identificador: repetir una sesión ya contada no paga dos veces.`)}</section>
      ${past.length ? `<section class="card card--wide"><h3>Últimas sesiones</h3><ul class="hist">${past.map((s) => `<li><b>${s.goals}/${s.kicks}</b> ${s.role === 'kick' ? 'goles' : 'paradas'}<span>${esc(s.team)} vs ${esc(s.rival)}</span><small>${esc(new Date(s.at).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' }))} · ${esc(s.reward)}</small></li>`).join('')}</ul></section>` : ''}`;
    $$('[data-role]', el).forEach((b) => b.addEventListener('click', () => { st.role = b.dataset.role; draw(); $(`[data-role="${st.role}"]`, el).focus(); }));
    $$('[data-mine]', el).forEach((b) => b.addEventListener('click', () => { st.team = b.dataset.mine; draw(); $(`[data-mine="${st.team}"]`, el)?.focus(); }));
    $$('[data-rival]', el).forEach((b) => b.addEventListener('click', () => { st.rival = b.dataset.rival; draw(); $(`[data-rival="${st.rival}"]`, el)?.focus(); }));
    $('#start', el).addEventListener('click', () => session(mine, rival));
  };

  let running = false;
  async function session(mine, rival) {
    if (running) return; running = true;
    const id = 's' + Date.now().toString(36) + Math.floor(Math.random() * 1e4).toString(36);
    let goals = 0, done = 0, saves = 0;
    for (let i = 1; i <= KICKS; i++) {
      const r = await penaltyDuel({ data, lab, attack: mine, defend: rival, role: st.role, seed: hash(id + i), context: { title: `Entrenamiento · tiro ${i} de ${KICKS}`, subtitle: st.role === 'kick' ? `Goles ${goals}/${done}` : `Paradas ${saves}/${done}` }, nextLabel: i < KICKS ? 'Siguiente tiro' : 'Ver resumen' });
      if (!r) { running = false; toast('Sesión abandonada: no se cuenta ni da ZP', 'bad'); draw(); return; }
      done++; if (r.result.code === 'G') goals++; else saves++;
    }
    running = false;
    const good = st.role === 'kick' ? goals : saves;
    const res = rewards.awardTrainingCompleted(ledger, dayKey(), id, true, stampNow());
    let msg;
    if (res.ok && !res.duplicate) { commitLedger(); fx.coin(); msg = `+${res.entry.amount} ZP (saldo ${res.balanceAfter})`; }
    else if (res.failure === 'LimitReached') msg = `Sin ZP: ya completaste ${ledger.tuning.trainingRewardsPerDay} sesiones hoy`;
    else msg = 'Sin ZP';
    history.push('training', { at: stampNow(), role: st.role, goals: good, kicks: KICKS, team: mine.shortName, rival: rival.shortName, reward: msg });
    draw(`<section class="card card--wide summary"><h3>Sesión completada</h3><p class="bigscore">${good} / ${KICKS}</p><p>${st.role === 'kick' ? 'goles marcados' : 'penales parados'} · ${esc(msg)}</p></section>`);
  }
  draw();
}

function hash(s) { let h = 2166136261 >>> 0; for (const c of s) { h ^= c.charCodeAt(0); h = Math.imul(h, 16777619) >>> 0; } return h; }
