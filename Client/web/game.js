// FS27 browser client: draws the snapshot of the real Core (running in WebAssembly) and turns touches into the Core's own human input.
// Nothing here knows a football rule: it asks the bridge to advance time, reads a snapshot and draws it.
import { dotnet } from './_framework/dotnet.js';

const H = 13, PF = 8;                       // header fields and fields per player (see GameBridge.Snapshot)
const PHASE = { NotStarted: 0, WaitingForKickoff: 1, Playing: 2, GoalCelebration: 3, WaitingForRestart: 4, Paused: 5, Finished: 6, Break: 7 };
const $ = id => document.getElementById(id);
const params = new URLSearchParams(location.search);
const canvas = $('view'), ctx = canvas.getContext('2d');
let B = null, teams = [], roster = null, snap = null, paused = false, running = false;
let stick = { x: 0, y: 0 }, mirror = 1;
const stats = { frames: 0, steps: 0, lastT: 0, fps: 0, sps: 0, advanceMs: 0, snapshotMs: 0, drawMs: 0, worstFrameMs: 0 };
let banner = '', bannerUntil = 0, lastScore = [0, 0];
let session = null, sp = [0, 0, 0, 0, 1.2, 1], spHoldStart = 0, spZone = 1;                         // what the tester played (for the feedback text)
const VERSION = 'FS27 prueba humana 3 (Core fase 34)';

function resize() { const r = window.devicePixelRatio || 1; canvas.width = Math.floor(innerWidth * r); canvas.height = Math.floor(innerHeight * r); }
addEventListener('resize', resize); resize();

// ---------------------------------------------------------------- boot
async function boot() {
  const { getAssemblyExports, getConfig } = await dotnet.withDiagnosticTracing(false).create();
  const config = getConfig();
  const exports = await getAssemblyExports(config.mainAssemblyName);
  B = exports.FS27.Client.GameBridge;
  await dotnet.run();
  const files = JSON.parse(B.RequiredFiles());
  let done = 0;
  await Promise.all(files.map(async f => {
    const res = await fetch('data/' + f); if (!res.ok) throw new Error('data/' + f + ' ' + res.status);
    B.AddFile(f, await res.text());
    $('loadbar').value = Math.round(100 * ++done / files.length);
  }));
  $('loadmsg').textContent = 'Preparando los equipos…';
  await new Promise(r => setTimeout(r, 30));
  const problems = B.LoadData();
  if (problems) console.warn('database problems:', problems);
  teams = JSON.parse(B.Teams());
  window.__fs27 = { B, stats, get snap() { return snap; }, roster: () => roster, state: () => B.Snapshot(), problems };
  $('loading').classList.add('hidden');
  if (params.get('auto')) { startMatch(+(params.get('home') || 0), +(params.get('away') || 1), +(params.get('seed') || 1), params.get('mode') || 'Classic', params.get('level') || 'Professional'); return; }
  const opts = teams.map(t => `<option value="${t.i}">${t.name}</option>`).join('');
  $('homeSel').innerHTML = opts; $('awaySel').innerHTML = opts; $('awaySel').value = String(Math.min(1, teams.length - 1));
  $('menu').classList.remove('hidden');
}
$('startBtn').onclick = () => { $('menu').classList.add('hidden'); startMatch(+$('homeSel').value, +$('awaySel').value, (Math.random() * 1e6) | 0, $('modeSel').value, $('levelSel').value); };
$('againBtn').onclick = () => { $('end').classList.add('hidden'); $('hintbar').classList.add('hidden'); $('hud').classList.add('hidden'); running = false; $('menu').classList.remove('hidden'); };

function startMatch(h, a, seed, mode, level) {
  const r = B.NewMatch(h, a, seed, mode, level);
  if (r.startsWith('error')) { alert(r); return; }
  const assist = params.has('assist') ? params.get('assist') !== '0' : ($('assistChk') ? $('assistChk').checked : true);
  B.SetAimAssist(assist);
  roster = JSON.parse(r);
  session = { home: h, away: a, seed, mode, level, assist, startedAt: Date.now(), fpsSamples: [], worstMs: 0 };
  showHint('Mueve el círculo · B pasa · desliza A hacia abajo para disparar · ⇄ cambia de jugador', 9000);
  $('hn').textContent = roster.home.slice(0, 12).toUpperCase(); $('an').textContent = roster.away.slice(0, 12).toUpperCase();
  $('hud').classList.remove('hidden'); lastScore = [0, 0]; paused = false; running = true; stats.lastT = performance.now();
  requestAnimationFrame(frame);
}

// ---------------------------------------------------------------- hints and feedback
let hintTimer = null;
function showHint(text, ms) { const el = $('hintbar'); el.textContent = text; el.classList.remove('hidden'); clearTimeout(hintTimer); hintTimer = setTimeout(() => el.classList.add('hidden'), ms); }
const QUESTIONS = [['move', 'Mover y cambiar de jugador'], ['pass', 'Pases'], ['shot', 'Disparos'], ['defend', 'Defender y recuperar el balón'], ['ai', 'Compañeros y rivales (¿juegan con sentido?)'], ['fun', 'Diversión en general']];
let ratings = {};
function prepareFeedback() {
  ratings = {}; $('notes').value = ''; $('copyMsg').textContent = ''; $('report').classList.add('hidden');
  $('rates').innerHTML = QUESTIONS.map(([k, label]) => `<div class="rate"><span>${label}</span><span class="stars" data-k="${k}">${[1, 2, 3, 4, 5].map(n => `<button type="button" data-n="${n}">${n}</button>`).join('')}</span></div>`).join('');
  for (const st of $('rates').querySelectorAll('.stars')) st.addEventListener('click', e => { const b = e.target.closest('button'); if (!b) return; ratings[st.dataset.k] = +b.dataset.n; for (const x of st.children) x.classList.toggle('sel', x === b); });
}
function reportText() {
  const lines = [VERSION, 'Fecha: ' + new Date().toISOString().slice(0, 16).replace('T', ' ')];
  if (session && roster) {
    const mins = Math.round((Date.now() - session.startedAt) / 6000) / 10;
    lines.push(`Partido: ${roster.home} ${snap ? snap[2] : '?'} - ${snap ? snap[3] : '?'} ${roster.away} (${session.mode}, ${session.level}, semilla ${session.seed})`, `Ayuda de puntería: ${session.assist ? 'sí' : 'no'} · duración real: ${mins} min`);
    lines.push(`Aparato: ${/Mobi|Android|iPhone|iPad/.test(navigator.userAgent) ? 'móvil/tableta' : 'ordenador'} · pantalla ${innerWidth}x${innerHeight} · ${stats.fps} fps · peor fotograma ${Math.round(stats.worstFrameMs)} ms`);
  }
  lines.push('Valoraciones (1 mal – 5 muy bien):');
  for (const [k, label] of QUESTIONS) lines.push(`- ${label}: ${ratings[k] || 'sin contestar'}`);
  const notes = $('notes').value.trim(); if (notes) lines.push('Comentarios: ' + notes);
  return lines.join('\n');
}
$('copyBtn').onclick = async () => {
  const text = reportText(); const box = $('report'); box.value = text; box.classList.remove('hidden');
  let ok = false;
  try { await navigator.clipboard.writeText(text); ok = true; } catch (e) { /* not allowed here: the fallback below */ }
  if (!ok) { box.focus(); box.select(); try { ok = document.execCommand('copy'); } catch (e) { ok = false; } }
  $('copyMsg').textContent = ok ? 'Copiado. Pégalo en un mensaje y envíalo a quien te pasó el juego.' : 'No he podido copiarlo solo: el texto de abajo está seleccionado; cópialo con «Copiar» de tu teclado o mantén pulsado y elige Copiar.';
};
$('saveBtn').onclick = () => {
  const blob = new Blob([reportText()], { type: 'text/plain;charset=utf-8' }), a = document.createElement('a');
  a.href = URL.createObjectURL(blob); a.download = 'fs27-resultado.txt'; document.body.appendChild(a); a.click(); a.remove();
  $('copyMsg').textContent = 'Archivo «fs27-resultado.txt» guardado (mira en Descargas). Envíalo a quien te pasó el juego.';
};

// ---------------------------------------------------------------- throw-ins and corners taken by the person (Phase 33)
function updateSetPieceUI(now) {
  const on = sp[0] !== 0;
  $('spbox').classList.toggle('hidden', !on); $('spzones').classList.toggle('hidden', sp[0] !== 2);
  if (!on) { spHoldStart = 0; return; }
  $('sptitle').textContent = sp[0] === 2 ? 'TU CÓRNER' : 'TU SAQUE DE BANDA';
  const held = spHoldStart ? Math.min(1, (performance.now() - spHoldStart) / 1000 / Math.max(0.1, sp[4])) : 0;
  $('spfill').style.width = Math.round(held * 100) + '%';
  $('sptext').textContent = (sp[0] === 2 ? 'Elige zona. ' : '') + 'Apunta con el círculo, mantén B para cargar la fuerza y suelta para sacar. Sin hacer nada, saca la IA en ' + Math.ceil(sp[3]) + ' s.';
}
for (const b of document.querySelectorAll('#spzones button')) b.addEventListener('pointerdown', () => { spZone = +b.dataset.z; for (const x of document.querySelectorAll('#spzones button')) x.classList.toggle('sel', x === b); });

// ---------------------------------------------------------------- loop
function frame(now) {
  if (!running) return;
  const dtMs = now - stats.lastT; stats.lastT = now;
  stats.worstFrameMs = Math.max(stats.worstFrameMs * 0.98, dtMs);
  const dt = Math.min(0.1, dtMs / 1000);
  if (!paused) {
    const t0 = performance.now();
    stats.steps += B.Advance(dt);
    stats.advanceMs += performance.now() - t0;
  }
  sp = B.SetPieceInfo(); updateSetPieceUI(now);
  const t1 = performance.now(); snap = B.Snapshot(); stats.snapshotMs += performance.now() - t1;
  const t2 = performance.now(); draw(now); stats.drawMs += performance.now() - t2;
  stats.frames++;
  if (now - (stats.sec || 0) >= 1000) { stats.fps = stats.frames; stats.sps = stats.steps; stats.msAdv = stats.advanceMs / Math.max(1, stats.frames); stats.msSnap = stats.snapshotMs / Math.max(1, stats.frames); stats.msDraw = stats.drawMs / Math.max(1, stats.frames); stats.frames = 0; stats.steps = 0; stats.advanceMs = stats.snapshotMs = stats.drawMs = 0; stats.sec = now; $('fps').textContent = `${stats.fps} fps · ${stats.sps} pasos/s · core ${stats.msAdv.toFixed(1)} ms`; }
  requestAnimationFrame(frame);
}

// ---------------------------------------------------------------- drawing
const fmt = s => { s = Math.max(0, Math.floor(s)); return String(Math.floor(s / 60)).padStart(2, '0') + ':' + String(s % 60).padStart(2, '0'); };
const colorOf = (team, gk) => gk ? (team ? '#f1c40f' : '#2ecc71') : (team ? '#e74c3c' : '#3498db');

function draw(now) {
  const W = canvas.width, Hh = canvas.height, L = roster.fieldLength, Wd = roster.fieldWidth;
  mirror = snap[12] ? 1 : -1;
  const fit = Math.min(W / (L + 6), Hh / (Wd + 4)), scale = fit * 1.12;
  const bx = snap[6], bz = snap[8];
  const maxCx = Math.max(0, (L + 6) / 2 - W / scale / 2), maxCy = Math.max(0, (Wd + 4) / 2 - Hh / scale / 2);
  const camX = Math.max(-maxCx, Math.min(maxCx, bx * mirror)) * mirror, camY = Math.max(-maxCy, Math.min(maxCy, bz * mirror)) * mirror;
  const sx = x => W / 2 + mirror * (x - camX) * scale, sy = y => Hh / 2 + mirror * (y - camY) * scale;
  ctx.fillStyle = '#0d4d22'; ctx.fillRect(0, 0, W, Hh);
  // pitch (stripes, lines, goals)
  const x0 = sx(-L / 2), x1 = sx(L / 2), y0 = sy(-Wd / 2), y1 = sy(Wd / 2);
  const left = Math.min(x0, x1), top = Math.min(y0, y1), pw = Math.abs(x1 - x0), ph = Math.abs(y1 - y0);
  for (let i = 0; i < 10; i++) { ctx.fillStyle = i % 2 ? '#1a7a3a' : '#188036'; ctx.fillRect(left + pw * i / 10, top, pw / 10 + 1, ph); }
  ctx.strokeStyle = 'rgba(255,255,255,.85)'; ctx.lineWidth = Math.max(2, scale * 0.12);
  ctx.strokeRect(left, top, pw, ph);
  ctx.beginPath(); ctx.moveTo(sx(0), top); ctx.lineTo(sx(0), top + ph); ctx.stroke();
  ctx.beginPath(); ctx.arc(sx(0), sy(0), scale * 3.2, 0, 7); ctx.stroke();
  const gw = roster.goalWidth; ctx.lineWidth = Math.max(3, scale * 0.25); ctx.strokeStyle = '#fff';
  for (const gx of [-L / 2, L / 2]) { ctx.beginPath(); ctx.moveTo(sx(gx), sy(-gw / 2)); ctx.lineTo(sx(gx), sy(gw / 2)); ctx.stroke(); }
  // players, drawn from the back to the front
  const n = roster.players.length, idx = [...Array(n).keys()].sort((a, b) => snap[H + a * PF + 1] - snap[H + b * PF + 1]);
  const ctrl = snap[9];
  for (const i of idx) {
    const b = H + i * PF, p = roster.players[i]; if (snap[b + 5]) continue;
    const px = sx(snap[b]), py = sy(snap[b + 1]), r = Math.max(7, scale * 0.9);
    ctx.fillStyle = 'rgba(0,0,0,.28)'; ctx.beginPath(); ctx.ellipse(px, py + r * 0.55, r * 0.95, r * 0.4, 0, 0, 7); ctx.fill();
    if (i === ctrl) { ctx.strokeStyle = '#fff200'; ctx.lineWidth = 3; ctx.beginPath(); ctx.arc(px, py, r + 5, 0, 7); ctx.stroke(); }
    ctx.fillStyle = colorOf(p.team, p.gk); ctx.beginPath(); ctx.arc(px, py, r, 0, 7); ctx.fill();
    // facing: heading 0 = +Y of the field, growing towards +X
    const hd = snap[b + 2], fx = Math.sin(hd), fy = Math.cos(hd);
    ctx.strokeStyle = 'rgba(255,255,255,.9)'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(px, py); ctx.lineTo(px + mirror * fx * r * 1.15, py + mirror * fy * r * 1.15); ctx.stroke();
    ctx.fillStyle = '#fff'; ctx.font = `bold ${Math.max(9, r * 0.9)}px sans-serif`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(p.number || '', px, py);
    if (p.team === 0 && snap[b + 4] >= 0) { const st = Math.max(0, Math.min(1, snap[b + 4] / 12)); ctx.fillStyle = 'rgba(0,0,0,.5)'; ctx.fillRect(px - r, py - r - 9, r * 2, 4); ctx.fillStyle = st > .3 ? '#7CFC00' : '#ff5555'; ctx.fillRect(px - r, py - r - 9, r * 2 * st, 4); }
  }
  // ball: ground shadow + raised ball
  const hgt = snap[7], bpx = sx(bx), bpy = sy(bz), br = Math.max(4, scale * 0.32);
  ctx.fillStyle = 'rgba(0,0,0,.35)'; ctx.beginPath(); ctx.ellipse(bpx, bpy + br * 0.5, br, br * 0.5, 0, 0, 7); ctx.fill();
  ctx.fillStyle = '#fff'; ctx.strokeStyle = '#222'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(bpx, bpy - hgt * scale * 0.5, br * (1 + hgt * 0.08), 0, 7); ctx.fill(); ctx.stroke();
  // HUD text
  $('hs').textContent = snap[2]; $('as').textContent = snap[3]; $('clock').textContent = fmt(snap[5]);
  if (snap[2] !== lastScore[0] || snap[3] !== lastScore[1]) { banner = 'GOL'; bannerUntil = now + 2200; lastScore = [snap[2], snap[3]]; }
  const phase = snap[1]; let text = '';
  if (now < bannerUntil) text = banner; else if (phase === PHASE.Break) text = 'DESCANSO'; else if (phase === PHASE.Paused) text = 'PAUSA';
  $('banner').textContent = paused ? 'PAUSA' : text;
  if (snap[11] && running) {
    running = false; prepareFeedback(); $('endTitle').textContent = snap[2] > snap[3] ? '¡Victoria!' : snap[2] < snap[3] ? 'Derrota' : 'Empate'; $('endScore').textContent = `${roster.home} ${snap[2]} – ${snap[3]} ${roster.away}`; $('end').classList.remove('hidden');
  }
}

// ---------------------------------------------------------------- input
const stickEl = $('stick'), knob = $('knob'); let stickId = null;
function stickMove(e) {
  const r = stickEl.getBoundingClientRect(), R = r.width / 2;
  let dx = (e.clientX - (r.left + R)) / R, dy = (e.clientY - (r.top + R)) / R; const m = Math.hypot(dx, dy);
  if (m > 1) { dx /= m; dy /= m; }
  const dead = 0.08; if (Math.hypot(dx, dy) < dead) { dx = 0; dy = 0; }
  knob.style.transform = `translate(${dx * R * 0.7}px, ${dy * R * 0.7}px)`;
  stick = { x: dx, y: dy }; B && B.SetStick(mirror * dx, mirror * dy);
}
stickEl.addEventListener('pointerdown', e => { stickId = e.pointerId; stickEl.setPointerCapture(e.pointerId); stickMove(e); });
stickEl.addEventListener('pointermove', e => { if (e.pointerId === stickId) stickMove(e); });
const stickEnd = e => { if (e.pointerId !== stickId) return; stickId = null; knob.style.transform = ''; stick = { x: 0, y: 0 }; B && B.SetStick(0, 0); };
stickEl.addEventListener('pointerup', stickEnd); stickEl.addEventListener('pointercancel', stickEnd);
// the stick direction can change when the sides switch: keep the Core's input in the field's frame
setInterval(() => { if (B && stickId !== null) B.SetStick(mirror * stick.x, mirror * stick.y); }, 50);

function action(btn, button) {
  let y0 = 0, id = null;
  btn.addEventListener('pointerdown', e => { id = e.pointerId; y0 = e.clientY; btn.setPointerCapture(id); btn.classList.add('on'); if (sp[0] !== 0 && button === 2) spHoldStart = performance.now(); });
  const end = e => {
    if (e.pointerId !== id) return; id = null; btn.classList.remove('on');
    if (sp[0] !== 0 && button === 2) { const hold = spHoldStart ? (performance.now() - spHoldStart) / 1000 : 0; spHoldStart = 0; B.TakeSetPiece(mirror * stick.x, mirror * stick.y, hold, sp[0] === 2 ? spZone : -1); return; }
    const dy = e.clientY - y0; let gesture = 0, power = 0.7;
    if (dy < -28) { gesture = 1; power = Math.min(1, 0.55 + (-dy - 28) / 120); } else if (dy > 28) { gesture = 2; power = Math.min(1, 0.55 + (dy - 28) / 120); }
    B && B.Press(button, gesture, mirror * stick.x, mirror * stick.y, power);
  };
  btn.addEventListener('pointerup', end); btn.addEventListener('pointercancel', e => { if (e.pointerId === id) { id = null; btn.classList.remove('on'); } });
}
action($('btnA'), 1); action($('btnB'), 2); action($('btnC'), 3);
$('btnSw').addEventListener('pointerdown', () => B && B.SwitchPlayer());
$('btnPause').addEventListener('pointerdown', () => { paused = !paused; });
// keyboard (testing on a computer): arrows/WASD, J/K/L = A/B/C (hold Shift = swipe down), Tab = switch
const keys = new Set();
function kbStick() { const x = (keys.has('ArrowRight') || keys.has('d') ? 1 : 0) - (keys.has('ArrowLeft') || keys.has('a') ? 1 : 0), y = (keys.has('ArrowDown') || keys.has('s') ? 1 : 0) - (keys.has('ArrowUp') || keys.has('w') ? 1 : 0); const m = Math.hypot(x, y) || 1; stick = { x: x / m * (x || y ? 1 : 0), y: y / m * (x || y ? 1 : 0) }; B && B.SetStick(mirror * stick.x, mirror * stick.y); }
addEventListener('keydown', e => { if (e.repeat) return; keys.add(e.key); kbStick(); if (sp[0] !== 0 && e.key.toLowerCase() === 'k') { spHoldStart = performance.now(); return; } const bt = { j: 1, k: 2, l: 3 }[e.key.toLowerCase()]; if (bt && B) B.Press(bt, e.shiftKey ? 2 : (e.ctrlKey ? 1 : 0), mirror * stick.x, mirror * stick.y, 0.8); if (e.key === 'Tab') { e.preventDefault(); B && B.SwitchPlayer(); } });
addEventListener('keyup', e => { keys.delete(e.key); kbStick(); if (sp[0] !== 0 && e.key.toLowerCase() === 'k' && spHoldStart) { const hold = (performance.now() - spHoldStart) / 1000; spHoldStart = 0; B.TakeSetPiece(mirror * stick.x, mirror * stick.y, hold, sp[0] === 2 ? spZone : -1); } });
document.addEventListener('contextmenu', e => e.preventDefault());
// a call, a notification or a change of tab must not leave a button or the stick stuck (FS27-REQ-0053): everything is released and the match pauses
function releaseAll() {
  stickId = null; knob.style.transform = ''; stick = { x: 0, y: 0 }; keys.clear(); spHoldStart = 0;
  if (B) B.SetStick(0, 0);
  if (running) paused = true;
}
addEventListener('blur', releaseAll);
document.addEventListener('visibilitychange', () => { if (document.hidden) releaseAll(); });

boot().catch(e => { $('loadmsg').textContent = 'Error: ' + e.message; console.error(e); });
