// Lectura del laboratorio de penales (fs27-penalty-lab.json): resultados REALES del Core para cada jugada discretizada.
// La web no calcula nada de física: busca la celda más cercana a lo que eligió el usuario y muestra lo que el Core respondió.
const B64 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_';

export const OUTCOMES = Object.freeze({
  G: { key: 'Scored', label: '¡GOL!', good: true },
  S: { key: 'Saved', label: 'PARADA', good: false },
  P: { key: 'HitPost', label: 'POSTE', good: false },
  B: { key: 'HitBar', label: 'LARGUERO', good: false },
  M: { key: 'Missed', label: 'FUERA', good: false },
  X: { key: 'Other', label: 'ANULADO', good: false },
});

export function nearest(values, x) {
  let best = 0, d = Infinity;
  for (let i = 0; i < values.length; i++) { const e = Math.abs(values[i] - x); if (e < d) { d = e; best = i; } }
  return best;
}

export function cellCount(grid) { return grid.lateral.length * grid.height.length * grid.power.length * grid.dive.length; }

/** Índices de la rejilla para una elección (lateral -1..1 con la izquierda positiva vista por el lanzador, altura, potencia, salto del portero). */
export function indexFor(grid, aim, dive) {
  return { li: nearest(grid.lateral, aim.lateral), hi: nearest(grid.height, aim.height), pi: nearest(grid.power, aim.power), di: nearest(grid.dive, dive) };
}

export function cellOffset(grid, takerIndex, idx) {
  const perTaker = cellCount(grid);
  const within = ((idx.li * grid.height.length + idx.hi) * grid.power.length + idx.pi) * grid.dive.length + idx.di;
  return (takerIndex * perTaker + within) * 3;
}

/** Decodifica una celda de 3 caracteres: resultado y dónde estaba el balón cuando se decidió. */
export function decodeCell(text3) {
  const o = OUTCOMES[text3[0]] || OUTCOMES.X;
  return { code: text3[0], outcome: o.key, label: o.label, good: o.good, lateral: B64.indexOf(text3[1]) / 63 * 3 - 1.5, height: B64.indexOf(text3[2]) / 63 * 2.4 };
}

export function lookup(lab, attackTeamId, defendTeamId, takerIndex, aim, dive) {
  const pair = lab.pairs[attackTeamId] && lab.pairs[attackTeamId][defendTeamId];
  if (!pair) return null;
  const idx = indexFor(lab.grid, aim, dive);
  const off = cellOffset(lab.grid, takerIndex, idx);
  const text = pair.cells.substr(off, 3);
  if (text.length !== 3) return null;
  return { ...decodeCell(text), cell: { lateral: lab.grid.lateral[idx.li], height: lab.grid.height[idx.hi], power: lab.grid.power[idx.pi], dive: lab.grid.dive[idx.di] } };
}

/** Decisiones de la IA del Core (PenaltyAi.Kick / PenaltyAi.Keeper): mismas fórmulas, probadas con vectores del Core. */
export function aiKick(ai, quality, r1, r2, r3) {
  const q = Math.max(0, Math.min(1, quality));
  const a = { power: ai.power, lateral: 0, height: 0 };
  if (r1 < ai.centreChance) a.lateral = 0; else a.lateral = (r2 < 0.5 ? 1 : -1) * (ai.sideLateral + ai.sideAccuracyBonus * q);
  a.height = r3 < ai.highChance ? 0.8 : 0.2;
  return a;
}
export function aiKeeper(ai, r1, r2, r3) {
  return { high: r3 < ai.keeperHighChance, lateral: r1 < ai.keeperCentreChance ? 0 : (r2 < 0.5 ? 1 : -1) * ai.sideLateral };
}

/** Generador pseudoaleatorio determinista (mulberry32): la misma semilla da la misma tanda. */
export function rng(seed) {
  let a = seed >>> 0;
  return () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}
