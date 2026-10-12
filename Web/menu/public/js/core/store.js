// Almacenamiento local seguro (nunca lanza). Todo lo que guarda la web vive solo en este navegador.
const PREFIX = 'fs27.';
export const store = {
  get(key, fallback) {
    try { const v = globalThis.localStorage && globalThis.localStorage.getItem(PREFIX + key); return v === null || v === undefined ? fallback : JSON.parse(v); } catch { return fallback; }
  },
  set(key, value) {
    try { globalThis.localStorage.setItem(PREFIX + key, JSON.stringify(value)); return true; } catch { return false; }
  },
  remove(key) { try { globalThis.localStorage.removeItem(PREFIX + key); } catch { /* sin almacenamiento */ } },
};
