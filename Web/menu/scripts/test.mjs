// Pruebas sin dependencias: node scripts/test.mjs
// Comprueban que las copias en JavaScript (cartera de ZP, cuenta de la tanda, decisiones de la IA de penales) reproducen EXACTAMENTE lo que hace el Core (vectores generados por el Core),
// que los datos exportados son completos y no contienen ids internos con nombres reales, y que la lectura del laboratorio de penales es coherente.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { ZenitLedger, rewards, DEFAULT_TUNING } from '../public/js/core/ledger.js';
import { Shootout } from '../public/js/core/shootout.js';
import { aiKick, aiKeeper, lookup, decodeCell, indexFor, cellOffset, cellCount, rng } from '../public/js/core/penalty.js';

const pub = fileURLToPath(new URL('../public/', import.meta.url));
const read = (f) => JSON.parse(readFileSync(join(pub, 'data', f), 'utf8'));
let passed = 0;
const test = (name, fn) => { try { fn(); passed++; console.log('  ok   ' + name); } catch (e) { console.error('  FAIL ' + name + '\n       ' + (e && e.message)); process.exitCode = 1; } };
const near = (a, b, tol, msg) => assert.ok(Math.abs(a - b) <= tol, (msg || '') + ' ' + a + ' != ' + b);

const vectors = read('fs27-vectors.json');
const data = read('fs27-data.json');
const lab = read('fs27-penalty-lab.json');

test('los valores por defecto de la cartera son los del Core', () => {
  const t = vectors.zenit.tuning;
  for (const k of Object.keys(t)) assert.equal(DEFAULT_TUNING[k], t[k], k);
});

for (const script of vectors.ledger) {
  test('cartera: guion "' + script.name + '" igual que el Core', () => {
    const l = new ZenitLedger();
    for (const op of script.ops) {
      let r;
      if (op.op === 'earn') r = l.tryEarn(op.amount, op.reason, op.reference, op.tx, op.stamp);
      else if (op.op === 'spend') r = l.trySpend(op.amount, op.reason, op.reference, op.tx, op.stamp);
      else {
        const [kind, a, b, c] = op.op.split(':');
        r = kind === 'match' ? rewards.awardMatchCompleted(l, a, b === 'true', c, 't') : rewards.awardTrainingCompleted(l, a, b, c === 'true', 't');
      }
      const e = op.expect;
      assert.equal(r.ok, e.ok, op.op + ' ok'); assert.equal(r.duplicate, e.duplicate, op.op + ' duplicate'); assert.equal(r.failure, e.failure, op.op + ' failure');
      assert.equal(r.balanceAfter, e.balanceAfter, op.op + ' balanceAfter');
      if (e.balanceBefore !== undefined) assert.equal(r.balanceBefore, e.balanceBefore, op.op + ' balanceBefore');
    }
    assert.deepEqual(l.toJSON(), script.finalJson, 'el documento guardado es igual al del Core');
    const back = ZenitLedger.fromText(l.toText());
    assert.ok(back.ok, back.error); assert.deepEqual(back.ledger.toJSON(), l.toJSON());
  });
}

test('cartera: un documento corrupto se rechaza, no se arregla', () => {
  const l = ZenitLedger.createNew(); l.trySpend(3, 'Shop', '', 'a');
  const good = l.toJSON();
  const mutate = (f) => { const d = JSON.parse(JSON.stringify(good)); f(d); return JSON.stringify(d); };
  const bad = [
    mutate((d) => { d.balance = 70; }), mutate((d) => { d.entries[0].after = 11; }), mutate((d) => { d.entries[1].seq = 5; }), mutate((d) => { d.entries[1].tx = 'starter'; }),
    mutate((d) => { d.entries[1].amount = -3; }), mutate((d) => { d.schema = 'Otro.v1'; }), mutate((d) => { d.entries[1].kind = 'steal'; }), '{no es json', '', '[]',
  ];
  for (const b of bad) assert.equal(ZenitLedger.fromText(b).ok, false, b.slice(0, 60));
  assert.equal(ZenitLedger.fromText(JSON.stringify(good)).ok, true);
});

test('cartera: nueva tiene la subvención inicial como primer movimiento y no baja de cero', () => {
  const l = ZenitLedger.createNew();
  assert.equal(l.balance, 10); assert.equal(l.history[0].tx, 'starter');
  assert.equal(l.trySpend(11, 'x', '', 'big').failure, 'InsufficientBalance'); assert.equal(l.balance, 10);
  assert.equal(l.trySpend(10, 'x', '', 'all').ok, true); assert.equal(l.balance, 0);
  assert.equal(l.trySpend(1, 'x', '', 'one').ok, false);
});

test('tanda: ' + vectors.shootout.length + ' secuencias iguales que PenaltyShootoutState del Core', () => {
  for (const v of vectors.shootout) {
    const s = new Shootout(v.kicksPerTeam, v.suddenDeath, v.first);
    v.scored.forEach((scored, i) => {
      const st = v.states[i];
      assert.equal(s.nextTeam, st.team, 'equipo del tiro ' + i);
      assert.equal(s.recordKick(scored), true);
      assert.equal(s.isDecided, st.decided, 'decidida ' + i); assert.equal(s.hasWinner ? s.winner : '', st.winner, 'ganador ' + i);
      assert.equal(s.goals.Home, st.goalsHome); assert.equal(s.goals.Away, st.goalsAway); assert.equal(s.taken.Home, st.takenHome); assert.equal(s.taken.Away, st.takenAway);
      assert.equal(s.isDecided ? '' : s.nextTeam, st.nextTeam, 'siguiente ' + i); assert.equal(s.inSuddenDeath, st.suddenDeath, 'muerte súbita ' + i); assert.equal(s.round, st.round, 'ronda ' + i);
    });
    assert.equal(s.recordKick(true), false, 'una tanda decidida no admite más tiros');
  }
});

test('IA de penales: ' + vectors.penaltyAi.length + ' decisiones iguales que PenaltyAi del Core', () => {
  const ai = data.config.penalty.ai;
  for (const v of vectors.penaltyAi) {
    const k = aiKick(ai, v.quality, v.r1, v.r2, v.r3), g = aiKeeper(ai, v.r1, v.r2, v.r3);
    near(k.lateral, v.kick.lateral, 0.002, 'lanzador lateral'); near(k.height, v.kick.height, 0.002, 'altura'); near(k.power, v.kick.power, 0.002, 'potencia');
    near(g.lateral, v.keeper.lateral, 0.002, 'portero lateral'); assert.equal(g.high, v.keeper.high);
  }
});

test('datos: 160 jugadores y 12 equipos reales, plantillas completas y sin ids internos', () => {
  assert.equal(data.players.length, 160); assert.equal(data.teams.length, 12);
  const ids = new Set(data.players.map((p) => p.id)); assert.equal(ids.size, 160);
  assert.equal(data.teams.filter((t) => t.kind === 'User').length, 1); assert.equal(data.teams.filter((t) => t.level === 'Elite' && t.kind === 'Official').length, 2);
  for (const t of data.teams) {
    assert.equal(t.active.length, 6, t.id); assert.equal(t.bench.length, 4, t.id);
    for (const id of [...t.active, ...t.bench]) assert.ok(ids.has(id), t.id + ' ' + id);
    assert.equal(t.lineup.field.length, 5); assert.ok(ids.has(t.lineup.goalkeeper));
  }
  const assigned = new Set(data.teams.flatMap((t) => [...t.active, ...t.bench]));
  assert.equal(assigned.size, 120); assert.equal(data.unassignedPlayerIds.length, 40);
  for (const id of data.unassignedPlayerIds) assert.ok(ids.has(id) && !assigned.has(id));
  assert.equal(data.roles.length, 12);
  for (const p of data.players) { if (!p.isGoalkeeper) assert.ok(data.roles.some((r) => r.id === p.primaryRole), p.id); assert.match(p.id, /^p-[0-9a-f]{8}$/); }
  const raw = readFileSync(join(pub, 'data', 'fs27-data.json'), 'utf8');
  assert.equal(/"(gk|cb|fb|dc|mc|sd|wg|am|dm|cm)-[a-z]/.test(raw), false, 'ningún id interno (llevan nombres reales) llega a un archivo público');
});

test('datos: reglas de Arena y Classic separadas', () => {
  const a = data.rules.Arena, c = data.rules.Classic;
  assert.equal(a.fouls, false); assert.equal(a.cards, false); assert.equal(a.penalties, false); assert.equal(a.var, false); assert.equal(a.referee, false); assert.equal(a.usesWalls, true); assert.equal(a.physicalPenalties, false);
  assert.equal(c.fouls, true); assert.equal(c.cards, true); assert.equal(c.penalties, true); assert.equal(c.var, true); assert.equal(c.referee, true); assert.equal(c.usesWalls, false); assert.equal(c.physicalPenalties, true);
  assert.equal(c.shootoutKicksPerTeam, 3);
});

test('laboratorio: 132 duelos, 5 lanzadores cada uno, celdas completas y lectura coherente', () => {
  let pairs = 0;
  for (const a of data.teams) for (const b of data.teams) {
    if (a.id === b.id) continue;
    const p = lab.pairs[a.id][b.id]; pairs++;
    assert.equal(p.takers.length, 5); assert.equal(p.cells.length, 5 * cellCount(lab.grid) * 3, a.id + ' vs ' + b.id);
    assert.equal(p.takers.filter((t) => t.suggested).length, 1);
    for (const t of p.takers) assert.ok(a.active.includes(t.id) || a.bench.includes(t.id));
  }
  assert.equal(pairs, 132);
  const A = data.teams[0].id, B = data.teams[1].id;
  const r = lookup(lab, A, B, 2, { lateral: 0.6, height: 0.2, power: 0.8 }, 0);
  assert.ok(r && ['G', 'S', 'P', 'B', 'M'].includes(r.code));
  const idx = indexFor(lab.grid, { lateral: 0.6, height: 0.2, power: 0.8 }, 0);
  assert.equal(r.code, lab.pairs[A][B].cells[cellOffset(lab.grid, 2, idx)]);
  const d = decodeCell('GkU'); assert.ok(d.lateral > -1.5 && d.lateral < 1.5 && d.height >= 0 && d.height <= 2.4);
  const seen = new Set(); for (const t of Object.values(lab.pairs)) for (const p of Object.values(t)) for (let i = 0; i < p.cells.length; i += 3) seen.add(p.cells[i]);
  assert.ok(seen.has('G') && seen.has('S') && seen.has('M'), 'el Core produce goles, paradas y fallos: ' + [...seen].join(''));
});

test('el generador pseudoaleatorio es determinista', () => {
  const a = rng(42), b = rng(42); for (let i = 0; i < 20; i++) assert.equal(a(), b());
});

test('escenarios del VAR: coste cobrado una vez y rechazos sin cobro (ejecuciones reales del Core)', () => {
  const sc = read('fs27-var-scenarios.json').scenarios; const by = Object.fromEntries(sc.map((s) => [s.id, s]));
  assert.equal(sc.length, 10);
  for (const id of ['goal-confirmed', 'goal-disallowed', 'penalty-cancelled', 'penalty-given', 'penalty-not-given-confirmed', 'red-rescinded', 'red-confirmed', 'duplicate-request']) {
    assert.equal(by[id].review.charged, true, id); assert.equal(by[id].balanceBefore - by[id].balanceAfter, data.config.var.costGoalZp, id);
    assert.equal(by[id].ledger.filter((e) => e.kind === 'Spend').length, 1, id + ' un solo cobro'); assert.equal(by[id].clockRanDuringReview, false, id);
  }
  assert.equal(by['goal-confirmed'].review.result, 'Confirmed'); assert.equal(by['goal-disallowed'].review.result, 'Corrected');
  assert.equal(by['rejected-no-balance'].review.charged, false); assert.equal(by['rejected-no-balance'].balanceAfter, 0); assert.equal(by['rejected-not-eligible'].review.charged, false);
});

console.log('\n' + passed + ' pruebas correctas' + (process.exitCode ? ' (con fallos)' : ''));
