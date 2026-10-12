// Utilidades mínimas de DOM. Todo texto que viene de datos pasa por esc().
export const $ = (s, r = document) => r.querySelector(s);
export const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
export const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
export const num = (n, d = 0) => Number(n).toLocaleString('es-ES', { maximumFractionDigits: d, minimumFractionDigits: d });
export const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
export const mmss = (sec) => { const s = Math.max(0, Math.floor(sec)); return String(Math.floor(s / 60)).padStart(2, '0') + ':' + String(s % 60).padStart(2, '0'); };

/** Cuenta las pulsaciones: evita ejecutar dos veces una acción por doble toque. Devuelve true la primera vez. */
export function once() { let used = false; return () => { if (used) return false; used = true; return true; }; }

export function toast(message, kind = 'info') {
  let host = document.getElementById('toasts');
  if (!host) { host = document.createElement('div'); host.id = 'toasts'; host.setAttribute('role', 'status'); host.setAttribute('aria-live', 'polite'); document.body.appendChild(host); }
  const t = document.createElement('div'); t.className = 'toast toast--' + kind; t.textContent = message; host.appendChild(t);
  setTimeout(() => t.classList.add('is-leaving'), 3600); setTimeout(() => t.remove(), 4200);
}

/** Cuadro de confirmación accesible (usa <dialog>). Devuelve una promesa con true/false. */
export function confirmDialog({ title, body, ok = 'Confirmar', cancel = 'Cancelar', danger = false }) {
  return new Promise((resolve) => {
    const d = document.createElement('dialog'); d.className = 'dlg';
    d.innerHTML = `<form method="dialog" class="dlg__box"><h3 class="dlg__title">${esc(title)}</h3><div class="dlg__body">${body}</div>
      <div class="dlg__actions"><button class="btn btn--ghost" value="cancel">${esc(cancel)}</button><button class="btn ${danger ? 'btn--danger' : 'btn--primary'}" value="ok" autofocus>${esc(ok)}</button></div></form>`;
    document.body.appendChild(d);
    d.addEventListener('close', () => { const v = d.returnValue === 'ok'; d.remove(); resolve(v); });
    if (typeof d.showModal === 'function') d.showModal(); else { d.setAttribute('open', ''); }
  });
}
