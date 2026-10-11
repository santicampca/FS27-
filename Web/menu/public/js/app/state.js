// Estado de la web: ajustes, cartera de ZP, inventario y selección de partido. Todo se guarda solo en este navegador.
import { store } from '../core/store.js';
import { ZenitLedger } from '../core/ledger.js';

export const DEFAULT_SETTINGS = Object.freeze({ reduceMotion: false, bigText: false, highContrast: false, sound: true, volume: 60, vibration: true, leftHanded: false, hints: true });
export const settings = { ...DEFAULT_SETTINGS, ...store.get('settings', {}) };

export function applySettings() {
  const r = document.documentElement;
  r.dataset.motion = settings.reduceMotion ? 'off' : 'on';
  r.dataset.text = settings.bigText ? 'big' : 'normal';
  r.dataset.contrast = settings.highContrast ? 'high' : 'normal';
}
export function setSetting(key, value) { settings[key] = value; store.set('settings', settings); applySettings(); }
export function resetSettings() { Object.assign(settings, DEFAULT_SETTINGS); store.set('settings', settings); applySettings(); }

// ---- cartera de ZP
const listeners = new Set();
export const stampNow = () => new Date().toISOString();
export const dayKey = () => { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); };

let corruptNotice = '';
function loadLedger() {
  const text = store.get('ledger', null);
  if (typeof text === 'string') {
    const r = ZenitLedger.fromText(text);
    if (r.ok) return r.ledger;
    store.set('ledger.corrupt', text);
    corruptNotice = 'La cartera guardada estaba dañada (' + r.error + '). Se conservó una copia aparte y se creó una nueva.';
  }
  const l = ZenitLedger.createNew(undefined, stampNow());
  store.set('ledger', l.toText());
  return l;
}
export let ledger = loadLedger();
export const takeLedgerNotice = () => { const n = corruptNotice; corruptNotice = ''; return n; };
export function onLedger(fn) { listeners.add(fn); return () => listeners.delete(fn); }
/** Guarda la cartera después de cada movimiento y avisa a quien la muestra. */
export function commitLedger() { store.set('ledger', ledger.toText()); listeners.forEach((f) => f(ledger)); }
export function replaceLedger(next) { ledger = next; commitLedger(); }

// ---- inventario de cosméticos de la web
export const inventory = { owned: [], equipped: {}, ...store.get('inventory', {}) };
export function saveInventory() { store.set('inventory', inventory); }

// ---- selección de partido
export const DEFAULT_SELECTION = Object.freeze({ mode: 'Classic', team: 'team-user', rival: 'team-solaris-meridian', difficulty: 'Professional', userFormationId: '' });
export const selection = { ...DEFAULT_SELECTION, ...store.get('selection', {}) };
export function setSelection(patch) { Object.assign(selection, patch); store.set('selection', selection); }

// ---- historiales pequeños
export const history = {
  get: (key) => store.get('history.' + key, []),
  push(key, item, max = 30) { const list = store.get('history.' + key, []); list.unshift(item); store.set('history.' + key, list.slice(0, max)); },
};
