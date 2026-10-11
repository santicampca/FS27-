// Tanda de penales de Classic. La cuenta (turnos, final anticipado, muerte súbita) es Shootout, copia de PenaltyShootoutState del Core verificada con vectores;
// cada tiro es un duelo real resuelto por el Core (laboratorio de penales). El orden de lanzadores se elige antes de empezar, se guarda y se respeta.
import { $, $$, esc, toast, confirmDialog } from './dom.js';
import { Shootout } from '../core/shootout.js';
import { penaltyDuel } from './duel.js';
import { head, note } from './screen.js';
import { crest } from './components.js';
import { store } from '../core/store.js';
import { fx } from './fx.js';

const orderKey = (teamId) => 'shootoutOrder.' + teamId;

/** Orden de los 5 lanzadores de campo: el guardado si es válido; si no, el orden por defecto del Core. */
export function resolveOrder(data, lab, attack, defend) {
  const takers = lab.pairs[attack.id][defend.id].takers.map((t) => t.id);
  const saved = store.get(orderKey(attack.id), null);
  const base = (data.shootoutDefaultOrder[attack.id] || []).filter((id) => takers.includes(id));
  const order = [];
  for (const id of Array.isArray(saved) ? saved : []) if (takers.includes(id) && !order.includes(id)) order.push(id);
  for (const id of [...base, ...takers]) if (!order.includes(id)) order.push(id);
  return order;
}

export function renderShootout(host, { data, lab, mine, rival, rules }) {
  const kicks = rules.shootoutKicksPerTeam, sudden = rules.shootoutSuddenDeath;
  const nameOf = (id) => data.playersById.get(id).name;

  function setup() {
    const order = resolveOrder(data, lab, mine, rival);
    const rivalOrder = resolveOrder(data, lab, rival, mine);
    const list = order.map((id, i) => `<li class="ord"><span class="ord__n">${i + 1}</span><span class="ord__name">${esc(nameOf(id))}<small>${esc(data.playersById.get(id).style || '')}</small></span>
      <button class="iconbtn" data-up="${i}" aria-label="Subir a ${esc(nameOf(id))}" ${i === 0 ? 'disabled' : ''}>▲</button><button class="iconbtn" data-down="${i}" aria-label="Bajar a ${esc(nameOf(id))}" ${i === order.length - 1 ? 'disabled' : ''}>▼</button></li>`).join('');
    host.innerHTML = `<section class="card card--wide"><h3>Tanda de penales · ${crest(mine, 22)} ${esc(mine.name)} vs ${crest(rival, 22)} ${esc(rival.name)}</h3>
      <dl class="rules"><div><dt>Tiros por equipo</dt><dd>${kicks} (del Core)</dd></div><div><dt>Final anticipado</dt><dd>Sí: acaba cuando un equipo ya no puede alcanzar al otro</dd></div><div><dt>Muerte súbita</dt><dd>${sudden ? 'Sí, si siguen empatados' : 'No'}</dd></div><div><dt>Empieza</dt><dd>Local (${esc(mine.name)})</dd></div></dl>
      <h4 class="sub">Tu orden de lanzadores</h4><ol class="orders">${list}</ol>
      <p class="field-note">El orden se guarda en este navegador y la tanda lo respeta. En la tanda el orden se repite en muerte súbita. Regla del Core: si un lanzador dejara de poder tirar (expulsado o sustituido), lo reemplaza el siguiente elegible del orden de forma determinista. Aquí ningún jugador puede quedar inhabilitado.</p>
      <p class="field-note">Orden del rival (elegido por el Core): ${rivalOrder.map((id) => esc(data.playersById.get(id).shortName)).join(' · ')}</p>
      <div class="actions"><button class="btn btn--primary btn--lg" id="begin">Empezar la tanda</button></div></section>`;
    const move = (i, d) => { const o = order.slice(); const j = i + d; [o[i], o[j]] = [o[j], o[i]]; store.set(orderKey(mine.id), o); setup(); $(d < 0 ? `[data-up="${j}"]` : `[data-down="${j}"]`, host)?.focus(); };
    $$('[data-up]', host).forEach((b) => b.addEventListener('click', () => move(Number(b.dataset.up), -1)));
    $$('[data-down]', host).forEach((b) => b.addEventListener('click', () => move(Number(b.dataset.down), 1)));
    $('#begin', host).addEventListener('click', () => run(order, rivalOrder));
  }

  async function run(orderH, orderA) {
    const so = new Shootout(kicks, sudden, 'Home');
    const seed0 = Math.floor(Math.random() * 1e9);
    const board = () => {
      const row = (team, t) => { const cells = so.kicks.filter((k) => k.team === team); const n = Math.max(kicks, cells.length); return `<div class="trow"><span class="trow__t">${crest(t, 26)}<b>${esc(t.shortName)}</b></span><span class="trow__k">${Array.from({ length: n }, (_, i) => `<i class="kick ${cells[i] ? (cells[i].scored ? 'kick--g' : 'kick--m') : ''}${i >= kicks ? ' kick--sd' : ''}" aria-label="${cells[i] ? (cells[i].scored ? 'gol' : 'fallo') : 'pendiente'}"></i>`).join('')}</span><b class="trow__s">${so.goals[team]}</b></div>`; };
      const nextTeam = so.nextTeam, idx = so.taken[nextTeam], order = nextTeam === 'Home' ? orderH : orderA;
      host.innerHTML = `<section class="card card--wide tanda" aria-live="polite"><div class="tanda__head"><h3>Tanda de penales</h3>${so.inSuddenDeath ? '<span class="pill">MUERTE SÚBITA</span>' : `<span class="pill pill--ghost">Ronda ${Math.min(so.round + 1, kicks)} de ${kicks}</span>`}</div>
        ${row('Home', mine)}${row('Away', rival)}
        ${so.isDecided ? '' : `<p class="field-note">Siguiente: <b>${esc(nameOf(order[idx % order.length]))}</b> (${esc((nextTeam === 'Home' ? mine : rival).name)})</p>`}</section>`;
    };
    board();
    while (!so.isDecided) {
      const team = so.nextTeam, order = team === 'Home' ? orderH : orderA, idx = so.taken[team], takerId = order[idx % order.length];
      const attack = team === 'Home' ? mine : rival, defend = team === 'Home' ? rival : mine;
      const takerIndex = lab.pairs[attack.id][defend.id].takers.findIndex((t) => t.id === takerId);
      const r = await penaltyDuel({ data, lab, attack, defend, role: team === 'Home' ? 'kick' : 'keep', takerIndex, seed: seed0 + so.kicks.length * 7, allowCancel: true,
        context: { title: 'Tanda de penales', subtitle: `${mine.shortName} ${so.goals.Home} - ${so.goals.Away} ${rival.shortName} · tiro de ${attack.shortName}`, reason: team === 'Home' ? 'Tu turno: elige dónde y con qué fuerza lanzar.' : 'Turno del rival: elige hacia dónde lanzarte.' },
        nextLabel: 'Continuar' });
      if (!r) { const stop = await confirmDialog({ title: 'Abandonar la tanda', body: '<p>Si abandonas, la tanda no se completa.</p>', ok: 'Abandonar', cancel: 'Seguir', danger: true }); if (stop) { setup(); toast('Tanda abandonada'); return; } continue; }
      so.recordKick(r.result.code === 'G');                  // un tiro de tanda termina en su primer resultado: no hay segundo disparo ni rechace
      board();
    }
    const win = so.hasWinner ? (so.winner === 'Home' ? mine : rival) : null;
    const regularDone = so.taken.Home >= kicks && so.taken.Away >= kicks && so.taken.Home === so.taken.Away;
    const early = so.hasWinner && !regularDone;
    fx.goal();
    host.insertAdjacentHTML('beforeend', `<section class="card card--wide result result--${win === mine ? 'good' : 'bad'}" role="status"><b class="result__big">${win ? esc(win.name) + ' gana la tanda' : 'La tanda termina empatada'}</b>
      <span>${so.goals.Home} - ${so.goals.Away} · ${so.kicks.length} tiros${early ? ' · final anticipado' : ''}${so.taken.Home > kicks ? ' · decidida en muerte súbita' : ''}</span>
      <div class="actions"><button class="btn btn--ghost" id="rematch">Revancha</button><button class="btn btn--primary" id="change">Cambiar el orden</button></div></section>`);
    $('#rematch', host).addEventListener('click', () => run(orderH, orderA)); $('#change', host).addEventListener('click', () => setup());
  }
  setup();
}
