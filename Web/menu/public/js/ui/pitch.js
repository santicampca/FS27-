// Visor 2D del campo: reproduce una grabación REAL de un partido simulado por el Core (posiciones cada 0,1 s). No decide nada: dibuja lo que el Core grabó.
import { inventory } from '../app/state.js';
import { clamp } from './dom.js';

const SKINS = {
  ball: { default: '#ffffff', 'ball-neon': '#9dff2c', 'ball-gold': '#ffd23d' },
  pitch: { default: ['#0d6b3a', '#0f7a43'], 'pitch-night': ['#0a3d2c', '#0c4a35'], 'pitch-diag': ['#0d6b3a', '#127f47'] },
};

const dist = (a, b) => { const p = (h) => [parseInt(h.slice(1, 3), 16), parseInt(h.slice(3, 5), 16), parseInt(h.slice(5, 7), 16)]; const x = p(a), y = p(b); return Math.hypot(x[0] - y[0], x[1] - y[1], x[2] - y[2]); };
export const kitColors = (home, away) => ({ home: home.primary, away: dist(home.primary, away.primary) < 110 ? away.secondary : away.primary });

export class PitchView {
  constructor(canvas, trace, players, teams, data) {
    this.c = canvas; this.g = canvas.getContext('2d'); this.trace = trace; this.frames = trace.frames; this.duration = this.frames[this.frames.length - 1][0] / 10;
    this.players = players; this.colors = kitColors(teams.home, teams.away); this.data = data; this.t = 0; this.trail = [];
    this.len = trace.pitch.length, this.wid = trace.pitch.width, this.goalW = trace.pitch.goalWidth;
    this.resize(); this.ro = new ResizeObserver(() => { this.resize(); this.draw(); }); this.ro.observe(canvas);
  }
  destroy() { this.ro.disconnect(); }
  resize() { const r = this.c.getBoundingClientRect(), d = Math.min(2, window.devicePixelRatio || 1); this.c.width = Math.max(10, Math.round(r.width * d)); this.c.height = Math.max(10, Math.round(r.height * d)); this.k = this.c.width / (this.len + 4); }
  frameAt(t) {
    const f = this.frames; let lo = 0, hi = f.length - 1; const t10 = t * 10;
    while (hi - lo > 1) { const mid = (lo + hi) >> 1; if (f[mid][0] <= t10) lo = mid; else hi = mid; }
    const a = f[lo], b = f[Math.min(hi, f.length - 1)], span = b[0] - a[0], k = span > 0 ? clamp((t10 - a[0]) / span, 0, 1) : 0;
    return a.map((v, i) => (i === 0 ? t : v + (b[i] - v) * k));
  }
  seek(t) { this.t = clamp(t, 0, this.duration); this.draw(); }
  X(m) { return (m + this.len / 2 + 2) * this.k; } Y(m) { return (m + this.wid / 2 + 2) * this.k; }
  draw() {
    const g = this.g, w = this.c.width, h = this.c.height, k = this.k; if (!this.frames.length) return;
    const skin = SKINS.pitch[inventory.equipped.pitch] || SKINS.pitch.default;
    g.clearRect(0, 0, w, h); g.fillStyle = skin[0]; g.fillRect(0, 0, w, h);
    const x0 = this.X(-this.len / 2), y0 = this.Y(-this.wid / 2), pw = this.len * k, ph = this.wid * k;
    const diag = inventory.equipped.pitch === 'pitch-diag';
    g.save(); g.beginPath(); g.rect(x0, y0, pw, ph); g.clip();
    for (let i = 0; i < 10; i++) { g.fillStyle = i % 2 ? skin[1] : skin[0]; if (diag) { g.save(); g.translate(x0 + pw / 2, y0 + ph / 2); g.rotate(-0.35); g.fillRect(-pw + i * pw / 5, -ph * 1.5, pw / 5, ph * 3); g.restore(); } else g.fillRect(x0 + i * pw / 10, y0, pw / 10, ph); }
    g.restore();
    g.strokeStyle = 'rgba(255,255,255,.85)'; g.lineWidth = Math.max(1.5, k * 0.12); g.strokeRect(x0, y0, pw, ph);
    g.beginPath(); g.moveTo(this.X(0), y0); g.lineTo(this.X(0), y0 + ph); g.stroke();
    g.beginPath(); g.arc(this.X(0), this.Y(0), 3 * k, 0, 7); g.stroke();
    for (const s of [-1, 1]) { g.strokeRect(s < 0 ? x0 : x0 + pw - 7 * k, this.Y(-8), 7 * k, 16 * k); g.strokeRect(s < 0 ? x0 : x0 + pw - 2.2 * k, this.Y(-this.goalW / 2), 2.2 * k, this.goalW * k); }
    const fr = this.frameAt(this.t), pl = this.players;
    for (let i = 0; i < 12; i++) {
      const px = this.X(fr[4 + i * 2] / 100), py = this.Y(fr[5 + i * 2] / 100), home = i < 6, gk = i === 0 || i === 6;
      g.beginPath(); g.arc(px, py, 1.05 * k, 0, 7); g.fillStyle = gk ? '#ffd23d' : (home ? this.colors.home : this.colors.away); g.fill(); g.lineWidth = Math.max(1, k * 0.14); g.strokeStyle = '#fff'; g.stroke();
      const p = this.data.playersById.get(pl[i]); g.fillStyle = '#06101f'; g.font = `800 ${Math.max(8, k * 1.05)}px system-ui,sans-serif`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(p ? p.number : '', px, py + 0.5);
    }
    const bx = this.X(fr[1] / 100), by = this.Y(fr[2] / 100), bh = fr[3] / 100, ballCol = SKINS.ball[inventory.equipped.ball] || SKINS.ball.default;
    if (inventory.equipped.trail) { this.trail.push([bx, by]); if (this.trail.length > 14) this.trail.shift(); g.lineWidth = k * 0.35; for (let i = 1; i < this.trail.length; i++) { g.strokeStyle = `rgba(255,255,255,${i / this.trail.length * 0.5})`; g.beginPath(); g.moveTo(...this.trail[i - 1]); g.lineTo(...this.trail[i]); g.stroke(); } }
    g.beginPath(); g.ellipse(bx, by + 0.3 * k, 0.55 * k, 0.3 * k, 0, 0, 7); g.fillStyle = 'rgba(0,0,0,.35)'; g.fill();
    g.beginPath(); g.arc(bx, by - bh * k * 0.7, (0.5 + Math.min(bh, 3) * 0.06) * k, 0, 7); g.fillStyle = ballCol; g.fill(); g.lineWidth = 1; g.strokeStyle = '#0b1226'; g.stroke();
  }
}
