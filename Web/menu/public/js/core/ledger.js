// Cartera de Zenit Points (ZP) de este navegador. Es la MISMA lógica que ZenitLedger del Core (Assets/_Project/Scripts/Core/Economy/ZenitLedger.cs):
// saldo = suma del historial, nunca negativo, ids de transacción idempotentes, JSON estricto "FS27.ZenitLedger.v1".
// Las pruebas (scripts/test.mjs) reproducen los vectores que genera el Core: si esta copia se desvía, la prueba falla.
export const SCHEMA = 'FS27.ZenitLedger.v1';
export const DEFAULT_TUNING = Object.freeze({ starterGrant: 10, rewardMatchCompleted: 5, rewardMatchWon: 3, rewardMatchDrawn: 1, rewardTrainingCompleted: 2, trainingRewardsPerDay: 3, maxBalance: 99999 });

const isInt = (n) => typeof n === 'number' && Number.isInteger(n);

export class ZenitLedger {
  constructor(tuning = DEFAULT_TUNING) { this.tuning = { ...DEFAULT_TUNING, ...tuning }; this.history = []; this.byTx = new Map(); this.balance = 0; }

  static createNew(tuning = DEFAULT_TUNING, stamp = '') {
    const l = new ZenitLedger(tuning);
    if (l.tuning.starterGrant > 0) l.tryEarn(l.tuning.starterGrant, 'Starter grant', '', 'starter', stamp);
    return l;
  }

  has(tx) { return typeof tx === 'string' && this.byTx.has(tx); }
  getEntry(tx) { return this.byTx.get(tx) || null; }
  tryEarn(amount, reason, reference, tx, stamp = '') { return this.#apply('earn', amount, reason, reference, tx, stamp); }
  trySpend(amount, reason, reference, tx, stamp = '') { return this.#apply('spend', amount, reason, reference, tx, stamp); }

  #apply(kind, amount, reason, reference, tx, stamp) {
    const r = { ok: false, duplicate: false, failure: 'None', entry: null, balanceBefore: this.balance, balanceAfter: this.balance };
    if (typeof tx !== 'string' || tx === '') { r.failure = 'InvalidTransaction'; return r; }
    const first = this.byTx.get(tx);
    if (first) {
      r.ok = true; r.duplicate = true; r.entry = first;
      r.balanceBefore = first.kind === 'earn' ? first.after - first.amount : first.after + first.amount; r.balanceAfter = first.after;
      return r;
    }
    if (!isInt(amount) || amount <= 0 || amount > this.tuning.maxBalance) { r.failure = 'InvalidAmount'; return r; }
    let after;
    if (kind === 'spend') { if (this.balance < amount) { r.failure = 'InsufficientBalance'; return r; } after = this.balance - amount; }
    else { if (this.balance + amount > this.tuning.maxBalance) { r.failure = 'BalanceCap'; return r; } after = this.balance + amount; }
    const e = { seq: this.history.length + 1, kind, amount, reason: reason || '', reference: reference || '', tx, after, stamp: stamp || '' };
    this.history.push(e); this.byTx.set(tx, e); this.balance = after;
    r.ok = true; r.entry = e; r.balanceAfter = after;
    return r;
  }

  toJSON() { return { schema: SCHEMA, balance: this.balance, entries: this.history.map((e) => ({ ...e })) }; }
  toText() { return JSON.stringify(this.toJSON()); }

  /** Lectura estricta: un documento cuyo historial no cuadra con su saldo se rechaza, no se arregla. */
  static fromText(text, tuning = DEFAULT_TUNING) {
    let doc; try { doc = JSON.parse(text); } catch (e) { return { ok: false, error: 'No es JSON: ' + e.message }; }
    if (!doc || doc.schema !== SCHEMA) return { ok: false, error: 'No es un documento ' + SCHEMA + '.' };
    if (!Array.isArray(doc.entries)) return { ok: false, error: 'Falta la lista de movimientos.' };
    const l = new ZenitLedger(tuning); let balance = 0;
    for (let i = 0; i < doc.entries.length; i++) {
      const e = doc.entries[i];
      if (!e || (e.kind !== 'earn' && e.kind !== 'spend')) return { ok: false, error: 'Movimiento ' + (i + 1) + ': tipo desconocido.' };
      if (e.seq !== i + 1) return { ok: false, error: 'Movimiento ' + (i + 1) + ': la secuencia no es continua.' };
      if (typeof e.tx !== 'string' || e.tx === '' || l.byTx.has(e.tx)) return { ok: false, error: 'Movimiento ' + e.seq + ': id repetido o vacío.' };
      if (!isInt(e.amount) || e.amount <= 0) return { ok: false, error: 'Movimiento ' + e.seq + ': cantidad no válida.' };
      balance += e.kind === 'earn' ? e.amount : -e.amount;
      if (balance < 0 || balance > l.tuning.maxBalance) return { ok: false, error: 'Movimiento ' + e.seq + ': el saldo sale de su rango.' };
      if (e.after !== balance) return { ok: false, error: 'Movimiento ' + e.seq + ': el saldo guardado no coincide con el historial.' };
      const entry = { seq: e.seq, kind: e.kind, amount: e.amount, reason: String(e.reason ?? ''), reference: String(e.reference ?? ''), tx: e.tx, after: e.after, stamp: String(e.stamp ?? '') };
      l.history.push(entry); l.byTx.set(entry.tx, entry);
    }
    l.balance = balance;
    if (doc.balance !== balance) return { ok: false, error: 'El saldo guardado no coincide con el historial.' };
    return { ok: true, ledger: l };
  }
}

const NOT_EARNED = (l) => ({ ok: false, duplicate: false, failure: 'NotEarned', entry: null, balanceBefore: l.balance, balanceAfter: l.balance });

/** Las formas de ganar ZP. Cada recompensa tiene un id construido con LO QUE pasó: repetir la acción no paga dos veces. */
export const rewards = {
  matchCompletedId: (matchId) => 'reward:match:' + matchId + ':completed',
  matchResultId: (matchId) => 'reward:match:' + matchId + ':result',
  trainingId: (dayKey, sessionId) => 'reward:training:' + dayKey + ':' + sessionId,

  awardMatchCompleted(ledger, matchId, matchFinished, outcome, stamp = '') {
    if (!matchFinished || !matchId) return NOT_EARNED(ledger);
    const t = ledger.tuning;
    const completed = ledger.tryEarn(t.rewardMatchCompleted, 'Match completed', matchId, rewards.matchCompletedId(matchId), stamp);
    const bonus = outcome === 'Won' ? t.rewardMatchWon : outcome === 'Drawn' ? t.rewardMatchDrawn : 0;
    if (bonus > 0) ledger.tryEarn(bonus, outcome === 'Won' ? 'Match won' : 'Match drawn', matchId, rewards.matchResultId(matchId), stamp);
    return completed;
  },

  awardTrainingCompleted(ledger, dayKey, sessionId, completed, stamp = '') {
    if (!completed || !dayKey || !sessionId) return NOT_EARNED(ledger);
    const id = rewards.trainingId(dayKey, sessionId);
    if (ledger.has(id)) return ledger.tryEarn(ledger.tuning.rewardTrainingCompleted, 'Training completed', sessionId, id, stamp);
    const prefix = 'reward:training:' + dayKey + ':';
    const today = ledger.history.filter((e) => e.tx.startsWith(prefix)).length;
    if (today >= ledger.tuning.trainingRewardsPerDay) return { ok: false, duplicate: false, failure: 'LimitReached', entry: null, balanceBefore: ledger.balance, balanceAfter: ledger.balance };
    return ledger.tryEarn(ledger.tuning.rewardTrainingCompleted, 'Training completed', sessionId, id, stamp);
  },
};
