// VAR de Classic. Las escenas son EJECUCIONES REALES del Core (fs27-var-scenarios.json): el Core decidió qué jugada es revisable, quién puede pedirla,
// cuánto cuesta y cuál es la decisión final. La web NO decide nada de eso: muestra lo que el Core respondió y mueve los ZP de la cartera local
// con las mismas reglas que el Core (una sola vez por solicitud, aunque la decisión no cambie; sin cobro si la solicitud se rechaza antes de empezar).
import { $, $$, esc, once, confirmDialog, toast, mmss } from './dom.js';
import { loadScenarios } from '../core/data.js';
import { head, note, loading, failure } from './screen.js';
import { ledger, commitLedger, stampNow, history, settings } from '../app/state.js';
import { fx } from './fx.js';

const T = {
  category: { Goal: 'Gol', RedCard: 'Tarjeta roja', Penalty: 'Penal' },
  original: { GoalAwarded: 'Gol concedido por el árbitro', RedCardShown: 'Tarjeta roja mostrada', NoRedCard: 'Sin tarjeta roja', PenaltyAwarded: 'Penal concedido', PenaltyNotAwarded: 'Penal no concedido' },
  final: { GoalStands: 'El gol se mantiene', GoalDisallowed: 'Gol anulado', RedCardStands: 'Roja confirmada', RedCardRescinded: 'Roja revocada', RedCardGiven: 'Roja concedida', NoRedCardConfirmed: 'Sin roja (confirmado)', PenaltyStands: 'Penal confirmado', PenaltyCancelled: 'Penal cancelado', PenaltyGiven: 'Penal concedido', NoPenaltyConfirmed: 'Sin penal (confirmado)', None: '—' },
  result: { Confirmed: 'Decisión confirmada', Corrected: 'Decisión corregida', NotReviewable: 'No revisable', Rejected: 'Rechazada', Cancelled: 'Cancelada', None: '—' },
  rejection: { NotAvailable: 'No hay VAR en este partido.', NoWallet: 'No hay cartera de ZP conectada.', UnknownIncident: 'El Core no reconoce esa jugada.', NotReviewable: 'Esta jugada ya no se puede revisar.', Duplicate: 'Esa solicitud ya estaba aceptada.', NotEligible: 'Solo puede pedirla el equipo al que la decisión perjudicó.', NotAuthorized: 'Tu equipo no puede pagar revisiones.', LimitReached: 'Se alcanzó el límite de revisiones.', Expired: 'Se acabó el tiempo para pedirla.', InsufficientBalance: 'Saldo de ZP insuficiente.', ReviewInProgress: 'Ya hay una revisión en curso.', MatchNotActive: 'El partido no está en juego.' },
  restart: { FreeKick: 'tiro libre', GoalKick: 'saque de puerta', Penalty: 'penal', KickOff: 'saque inicial', ThrowIn: 'saque de banda', Corner: 'córner' },
  phase: { GoalCelebration: 'celebración del gol', Playing: 'el juego sigue', WaitingForRestart: 'esperando el saque', VarReview: 'revisión VAR' },
  team: { Home: 'Local', Away: 'Visitante' },
};
const effect = (s) => {
  const a = s.after, b = s.before, parts = [];
  parts.push(`Marcador ${b.home}-${b.away} → <b>${a.home}-${a.away}</b>`);
  if (a.restart) parts.push(`Se reanuda con ${T.restart[a.restart] || a.restart} para el ${T.team[a.restartTeam]?.toLowerCase() || a.restartTeam}`); else parts.push('Estado: ' + (T.phase[a.phase] || a.phase));
  return parts.join(' · ');
};

export async function renderVar(host, { onClose } = {}) {
  host.innerHTML = loading('revisiones reales del Core');
  let data;
  try { data = await loadScenarios(); } catch (e) { host.innerHTML = failure(e); return; }
  const scenarios = data.scenarios;
  const listView = () => {
    host.innerHTML = `<section class="card card--wide"><h3>VAR · escenas reales del Core</h3>
      <p class="field-note">${esc(data.note)}</p>
      <div class="scgrid">${scenarios.map((s) => `<button class="sc" data-sc="${esc(s.id)}"><b>${esc(s.title)}</b><small>${esc(T.category[s.incident?.category] || '')} · ${s.review.charged ? 'cuesta ' + s.review.costZp + ' ZP' : 'sin cobro'}</small><span>${esc(s.story)}</span></button>`).join('')}</div></section>
      ${historyCard()}`;
    $$('[data-sc]', host).forEach((b) => b.addEventListener('click', () => runView(scenarios.find((s) => s.id === b.dataset.sc))));
  };

  const historyCard = () => {
    const h = history.get('var').slice(0, 8);
    return `<section class="card card--wide"><h3>Historial de decisiones</h3>${h.length ? `<ul class="hist">${h.map((x) => `<li><b>${esc(T.category[x.category] || x.category)}</b> ${esc(x.original)} → <b>${esc(x.final)}</b><span>${esc(x.result)} · ${esc(x.cost)}</span><small>${esc(new Date(x.at).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' }))}</small></li>`).join('')}</ul>` : '<p class="field-note">Todavía no has pedido ninguna revisión.</p>'}</section>`;
  };

  function runView(s) {
    const runId = Math.random().toString(36).slice(2, 8);
    const txId = 'var:' + s.id + ':' + runId;                // un identificador por escena jugada: es lo que impide cobrar dos veces
    const inc = s.incident;
    const costZp = s.review.costZp || s.reviewAtRequest.costZp || s.viewBefore.costZp || 1;
    let state = 'idle';                                        // idle | review | done
    const lock = once();
    const draw = (body = '') => {
      host.innerHTML = `<div class="actions"><button class="btn btn--ghost" id="back-list">← Todas las escenas</button></div>
        <section class="card card--wide var"><div class="var__bar"><span class="var__badge${state === 'review' ? ' is-on' : ''}" aria-live="polite">VAR</span>
          <span class="var__clock">${state === 'review' ? 'Reloj detenido · revisión en curso' : state === 'done' ? 'El juego se reanuda' : 'Juego detenido por la jugada'}</span><span class="chip chip--zp"><span class="gem" aria-hidden="true"></span> <b id="bal">${ledger.balance}</b> <small>ZP</small></span></div>
          <h3>${esc(s.title)}</h3><p>${esc(s.story)}</p>
          <dl class="rules"><div><dt>Jugada</dt><dd>${esc(T.category[inc.category] || inc.category)}</dd></div><div><dt>Decisión inicial</dt><dd>${esc(T.original[inc.original] || inc.original)}</dd></div>
            <div><dt>Implicados</dt><dd>${esc(inc.offender || '—')} / ${esc(inc.victim || '—')}</dd></div><div><dt>Puede pedirla</dt><dd>${esc(T.team[inc.eligibleTeam] || inc.eligibleTeam)} <small>(el Core la ofrece al equipo perjudicado)</small></dd></div>
            <div><dt>Marcador</dt><dd>${s.before.home} - ${s.before.away}</dd></div><div><dt>Coste</dt><dd><b>${costZp} ZP</b>, también si la decisión no cambia</dd></div></dl>
          ${body}</section>${historyCard()}`;
      $('#back-list', host).addEventListener('click', () => (onClose ? onClose() : listView()));
    };
    draw(`<div class="actions"><button class="btn btn--primary btn--lg" id="ask">Pedir revisión VAR (${costZp} ZP)</button></div>
      ${note('Solo el Core decide si la jugada es revisable. Si la solicitud es válida, el coste se cobra una sola vez aunque el VAR confirme la decisión del árbitro; si se rechaza antes de empezar no se cobra nada.')}`);
    $('#ask', host).addEventListener('click', async () => {
      if (state !== 'idle') return;
      const ok = await confirmDialog({ title: 'Pedir revisión VAR', body: `<p>La revisión cuesta <b>${costZp} ZP</b>. Tu saldo es <b>${ledger.balance} ZP</b>.</p><p>El coste se cobra <b>aunque la decisión no cambie</b>. Si el Core rechaza la solicitud antes de empezar, no se cobra nada.</p>`, ok: `Pedir (${costZp} ZP)` });
      if (!ok || !lock()) return;                              // el segundo toque no repite nada
      fx.var();
      const rejection = s.review.rejection;
      if (rejection && rejection !== 'None') return rejected(T.rejection[rejection] || rejection, rejection);
      const charge = ledger.trySpend(costZp, 'VAR review', 'VAR:' + inc.category + ':' + inc.id, txId, stampNow());
      if (!charge.ok) return rejected(T.rejection.InsufficientBalance, 'InsufficientBalance', true);
      commitLedger(); fx.coin();
      state = 'review';
      draw(`<div class="var__review" role="status"><span class="spinner" aria-hidden="true"></span> El VAR revisa la jugada… <small>3 s de revisión · el reloj no corre</small></div>
        <p class="field-note">Cobrado: ${costZp} ZP (saldo ${charge.balanceBefore} → ${charge.balanceAfter}). Acciones bloqueadas hasta la decisión.</p>`);
      const wait = settings.reduceMotion ? 0 : 3000;
      setTimeout(() => decided(charge), wait);
    });

    function rejected(msg, code, local) {
      state = 'done';
      history.push('var', { at: stampNow(), category: inc.category, original: T.original[inc.original] || inc.original, final: 'Sin revisión', result: 'Rechazada', cost: 'sin cobro' });
      draw(`<div class="result result--bad" role="status"><b class="result__big">Solicitud rechazada</b><span>${esc(msg)}</span><small>${local ? 'Regla del Core aplicada a tu cartera local (saldo ' + ledger.balance + ' ZP): no se inicia la revisión ni se cobra.' : 'Respuesta real del Core (' + esc(code) + '): no se inicia la revisión ni se cobra.'} Saldo: ${ledger.balance} ZP.</small></div>`);
    }

    function decided(charge) {
      state = 'done';
      const r = s.review, corrected = r.result === 'Corrected';
      let dup = '';
      if (s.id === 'duplicate-request') { const again = ledger.trySpend(costZp, 'VAR review', 'VAR:' + inc.category + ':' + inc.id, txId, stampNow()); dup = `<p class="field-note">Doble toque simulado: la misma solicitud se envió otra vez → <b>${again.duplicate ? 'reconocida, no se cobra de nuevo' : 'ERROR'}</b>. Saldo ${ledger.balance} ZP.</p>`; }
      if (corrected) fx.save(); else fx.click();
      history.push('var', { at: stampNow(), category: inc.category, original: T.original[inc.original] || inc.original, final: T.final[r.final] || r.final, result: T.result[r.result], cost: costZp + ' ZP' });
      draw(`<div class="result result--${corrected ? 'good' : 'neutral'}" role="status"><b class="result__big">${esc(T.result[r.result])}</b>
        <div class="decide"><div><small>Decisión inicial</small><b>${esc(T.original[inc.original] || inc.original)}</b></div><span aria-hidden="true">→</span><div><small>Decisión final</small><b>${esc(T.final[r.final] || r.final)}</b></div></div>
        <span>${effect(s)}</span><small>Coste cobrado: ${costZp} ZP${corrected ? '' : ' (la decisión no cambió y el coste se cobra igual)'} · saldo ${charge.balanceBefore} → ${charge.balanceAfter}. El reloj no corrió durante la revisión.</small></div>${dup}
        <div class="actions"><button class="btn btn--ghost" id="again">Repetir la escena</button><button class="btn btn--primary" id="list">Más escenas</button></div>`);
      $('#again', host).addEventListener('click', () => runView(s)); $('#list', host).addEventListener('click', () => listView());
    }
  }
  listView();
}

export async function render(el, { navigate }) {
  el.innerHTML = head('VAR', 'VAR', 'CLASSIC', 'La revisión por vídeo de Classic: pides revisar un gol, una roja o un penal, pagas ZP y el Core decide.', '#/classic') + '<div id="var-host"></div>';
  renderVar($('#var-host', el));
}
