// Configuración: solo ajustes que de verdad se aplican en esta web y se guardan en este navegador.
import { $, $$, esc, toast, confirmDialog } from './dom.js';
import { head, note } from './screen.js';
import { settings, setSetting, resetSettings, ledger } from '../app/state.js';
import { fx, vibrate } from './fx.js';

export function render(el, { navigate }) {
  const canVibrate = typeof navigator.vibrate === 'function';
  const sw = (id, title, sub, on, disabled = false) => `<label class="setting" for="${id}"><span><b>${esc(title)}</b><small>${sub}</small></span><input class="switch" type="checkbox" id="${id}" ${on ? 'checked' : ''} ${disabled ? 'disabled' : ''} role="switch"></label>`;
  const draw = () => {
    el.innerHTML = head('Configuración', 'CONFIGURACIÓN', 'AJUSTES REALES', 'Cada ajuste de esta pantalla cambia algo de verdad en esta web y se guarda en este navegador. Los ajustes del juego (cámara, controles del Core, audio del motor) llegarán con el cliente de juego.') +
      `<div class="settings">
        ${sw('s-motion', 'Reducir animaciones', 'Quita transiciones, el balón del fondo y las animaciones de los penales.', settings.reduceMotion)}
        ${sw('s-text', 'Texto grande', 'Aumenta el tamaño del texto de toda la web.', settings.bigText)}
        ${sw('s-contrast', 'Alto contraste', 'Fondos más oscuros y bordes más marcados.', settings.highContrast)}
        ${sw('s-sound', 'Sonido', 'Efectos generados en el navegador: pitido, tiro, gol, parada, VAR y monedas.', settings.sound)}
        <div class="setting"><span><b>Volumen</b><small>Solo afecta a los efectos de esta web.</small></span><input type="range" id="s-vol" min="0" max="100" value="${settings.volume}" aria-label="Volumen"> <button class="btn btn--ghost" id="s-test">Probar</button></div>
        ${sw('s-vib', 'Vibración', canVibrate ? 'Vibra al marcar gol, parar o dar en el poste (móviles compatibles).' : 'Este dispositivo no permite vibración desde el navegador.', settings.vibration && canVibrate, !canVibrate)}
        ${sw('s-left', 'Modo zurdo en los controles', 'Intercambia el lado del joystick y de los botones A/B/C en la pantalla Controles.', settings.leftHanded)}
      </div>
      <div class="actions"><button class="btn btn--ghost" id="s-reset">Restablecer ajustes</button><a class="btn btn--ghost" href="#/cartera">Cartera de ZP (${ledger.balance})</a><button class="btn btn--danger" id="s-wipe">Borrar todos los datos locales</button></div>
      ${note('Los datos locales son: ajustes, cartera de ZP e historial, inventario de la tienda, selecciones de partido, orden de la tanda e historiales. Borrarlos es definitivo.')}`;
    const bind = (id, key) => $('#' + id, el).addEventListener('change', (e) => { setSetting(key, e.target.checked); if (key === 'sound' && e.target.checked) fx.click(); });
    bind('s-motion', 'reduceMotion'); bind('s-text', 'bigText'); bind('s-contrast', 'highContrast'); bind('s-sound', 'sound'); bind('s-vib', 'vibration'); bind('s-left', 'leftHanded');
    $('#s-vol', el).addEventListener('input', (e) => setSetting('volume', Number(e.target.value)));
    $('#s-test', el).addEventListener('click', () => { fx.goal(); vibrate(60); });
    $('#s-reset', el).addEventListener('click', () => { resetSettings(); toast('Ajustes restablecidos'); draw(); });
    $('#s-wipe', el).addEventListener('click', async () => {
      if (!(await confirmDialog({ title: 'Borrar todos los datos locales', body: '<p>Se borrarán tus ajustes, tu cartera de ZP (con su historial), tus compras y tus historiales de este navegador.</p><p><b>No se puede deshacer.</b></p>', ok: 'Borrar todo', danger: true }))) return;
      try { Object.keys(localStorage).filter((k) => k.startsWith('fs27.')).forEach((k) => localStorage.removeItem(k)); } catch { /* sin almacenamiento */ }
      location.hash = '#/'; location.reload();
    });
  };
  draw();
}
