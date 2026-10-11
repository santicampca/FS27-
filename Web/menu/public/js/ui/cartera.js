// Cartera de ZP: saldo, historial completo, cómo ganar ZP y copia de seguridad. Todo vive en este navegador.
import { $, esc, toast, confirmDialog } from './dom.js';
import { head, note } from './screen.js';
import { ledger, replaceLedger, commitLedger } from '../app/state.js';
import { ZenitLedger } from '../core/ledger.js';
import { store } from '../core/store.js';

const REASON_ES = { 'Starter grant': 'Subvención inicial', 'Match completed': 'Partido completado', 'Match won': 'Partido ganado', 'Match drawn': 'Partido empatado', 'Training completed': 'Entrenamiento completado', 'VAR review': 'Revisión VAR', Shop: 'Tienda' };

export function render(el) {
  const t = ledger.tuning;
  const draw = () => {
    el.innerHTML = head('Cartera', 'CARTERA', 'ZENIT POINTS', 'Los ZP son una moneda virtual interna de FS27. No son dinero real y no se pueden comprar.', '#/') +
      `<div class="split"><section class="card"><h3>Saldo</h3><p class="bigscore"><span class="gem" aria-hidden="true"></span> ${ledger.balance} <small>ZP</small></p><p class="field-note">${ledger.history.length} movimientos · el saldo es la suma exacta del historial y nunca baja de cero.</p></section>
        <section class="card"><h3>Cómo ganar ZP</h3><ul class="plain"><li>Completar una sesión de <a href="#/entrenamiento">Entrenamiento</a> (5 penales): <b>${t.rewardTrainingCompleted} ZP</b>, hasta ${t.trainingRewardsPerDay} sesiones al día.</li><li>Terminar un partido: <b>${t.rewardMatchCompleted} ZP</b> (+${t.rewardMatchWon} si ganas, +${t.rewardMatchDrawn} si empatas). La web aún no juega partidos reales, así que esta recompensa todavía no se concede aquí.</li><li>Nada se gana por abrir pantallas ni por repetir una acción ya contada.</li></ul></section></div>
      <section class="card card--wide"><h3>Historial</h3>${ledger.history.length ? `<div class="tablewrap"><table class="tbl"><thead><tr><th>#</th><th>Fecha</th><th>Motivo</th><th>Referencia</th><th class="r">ZP</th><th class="r">Saldo</th></tr></thead><tbody>${ledger.history.slice().reverse().map((e) => `<tr><td>${e.seq}</td><td>${esc(e.stamp ? new Date(e.stamp).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' }) : '—')}</td><td>${esc(REASON_ES[e.reason] || e.reason)}</td><td><small>${esc(e.reference || '—')}</small></td><td class="r ${e.kind === 'earn' ? 'pos' : 'neg'}">${e.kind === 'earn' ? '+' : '−'}${e.amount}</td><td class="r">${e.after}</td></tr>`).join('')}</tbody></table></div>` : '<p class="field-note">Sin movimientos.</p>'}</section>
      <section class="card card--wide"><h3>Copia de seguridad</h3><p class="field-note">Exporta tu cartera como texto o restaura una copia. Un texto cuyo historial no cuadre con su saldo se rechaza.</p>
        <textarea id="bk" class="ta" rows="5" spellcheck="false" aria-label="Copia de la cartera">${esc(ledger.toText())}</textarea>
        <div class="actions"><button class="btn btn--ghost" id="copy">Copiar</button><button class="btn btn--primary" id="restore">Restaurar desde el texto</button></div></section>
      ${note('La cartera es local: cada navegador tiene la suya. Cuando exista una cuenta de jugador, esta misma cartera (formato <code>FS27.ZenitLedger.v1</code>, el del Core) se sincronizará.')}`;
    $('#copy', el).addEventListener('click', async () => { try { await navigator.clipboard.writeText($('#bk', el).value); toast('Copiado'); } catch { $('#bk', el).select(); toast('Selecciona y copia el texto', 'info'); } });
    $('#restore', el).addEventListener('click', async () => {
      const r = ZenitLedger.fromText($('#bk', el).value);
      if (!r.ok) { toast('Texto rechazado: ' + r.error, 'bad'); return; }
      if (!(await confirmDialog({ title: 'Restaurar cartera', body: `<p>Se reemplazará tu cartera actual (${ledger.balance} ZP) por la del texto (${r.ledger.balance} ZP).</p>`, ok: 'Restaurar', danger: true }))) return;
      store.set('ledger.backup', ledger.toText()); replaceLedger(r.ledger); toast('Cartera restaurada'); draw();
    });
  };
  draw();
}
