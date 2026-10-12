// Efectos de sonido y vibración reales (WebAudio / Vibration API), regulados por los ajustes. Sin archivos de audio: son tonos generados.
import { settings } from '../app/state.js';

let ctx = null;
function audio() {
  if (!settings.sound || settings.volume <= 0) return null;
  try { ctx = ctx || new (window.AudioContext || window.webkitAudioContext)(); if (ctx.state === 'suspended') ctx.resume(); return ctx; } catch { return null; }
}
function tone(freq, start, dur, type = 'sine', gain = 0.25) {
  const c = audio(); if (!c) return;
  const o = c.createOscillator(), g = c.createGain(); const t0 = c.currentTime + start;
  o.type = type; o.frequency.setValueAtTime(freq, t0);
  g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(Math.max(0.0002, gain * settings.volume / 100), t0 + 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
  o.connect(g).connect(c.destination); o.start(t0); o.stop(t0 + dur + 0.05);
}
export function vibrate(pattern) { if (settings.vibration && navigator.vibrate) { try { navigator.vibrate(pattern); } catch { /* sin soporte */ } } }

export const fx = {
  whistle() { tone(2200, 0, 0.35, 'sine', 0.2); tone(2600, 0.05, 0.3, 'sine', 0.12); },
  kick() { tone(140, 0, 0.12, 'triangle', 0.4); },
  goal() { [523, 659, 784, 1047].forEach((f, i) => tone(f, i * 0.09, 0.22, 'triangle', 0.25)); vibrate([60, 40, 120]); },
  save() { tone(220, 0, 0.18, 'square', 0.18); tone(180, 0.12, 0.2, 'square', 0.15); vibrate(80); },
  miss() { tone(160, 0, 0.3, 'sawtooth', 0.12); vibrate(40); },
  frame() { tone(880, 0, 0.12, 'triangle', 0.3); tone(660, 0.1, 0.2, 'triangle', 0.2); vibrate([30, 30, 30]); },
  click() { tone(660, 0, 0.05, 'square', 0.08); },
  coin() { tone(988, 0, 0.08, 'square', 0.15); tone(1319, 0.08, 0.16, 'square', 0.15); },
  var() { tone(440, 0, 0.12, 'square', 0.15); tone(440, 0.2, 0.12, 'square', 0.15); },
};
