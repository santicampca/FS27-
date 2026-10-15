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
const VERSION = 'FS27 prueba humana 7 (Core fase 39)';

// ---------------------------------------------------------------- settings and profile (kept on this device only; every access is guarded: a blocked storage must not break the game)
const DEFAULTS = { vol: 0.7, mute: false, haptics: true, stickScale: 1, btnScale: 1, lefty: false, assist: true, cam: 'follow', zoom: 1.12, text: 1, color: false, reduce: false };
let settings = { ...DEFAULTS }, profile = { played: 0, wins: 0, draws: 0, losses: 0, gf: 0, ga: 0 };
try { settings = { ...DEFAULTS, ...JSON.parse(localStorage.getItem('fs27.settings') || '{}') }; profile = { ...profile, ...JSON.parse(localStorage.getItem('fs27.profile') || '{}') }; } catch (e) { /* no storage: defaults */ }
const saveSettings = () => { try { localStorage.setItem('fs27.settings', JSON.stringify(settings)); } catch (e) { /* ignore */ } };
const saveProfile = () => { try { localStorage.setItem('fs27.profile', JSON.stringify(profile)); } catch (e) { /* ignore */ } };
function applySettings() {
  const r = document.documentElement.style;
  r.setProperty('--stick-scale', settings.stickScale); r.setProperty('--btn-scale', settings.btnScale); r.setProperty('--text-scale', settings.text);
  document.body.classList.toggle('lefty', !!settings.lefty); document.body.classList.toggle('reduce', !!settings.reduce);
  if (master) master.gain.value = settings.mute ? 0 : settings.vol;
}
function profileLine() { $('profileLine').textContent = profile.played ? `Tus partidos: ${profile.played} · ${profile.wins} G ${profile.draws} E ${profile.losses} P · goles ${profile.gf}-${profile.ga}` : ''; }

// ---------------------------------------------------------------- sound (made with the browser's own synthesiser: no files) and vibration
let ac = null, master = null, crowd = null;
function audioInit() {
  if (ac) { if (ac.state === 'suspended') ac.resume(); return; }
  try {
    ac = new (window.AudioContext || window.webkitAudioContext)(); master = ac.createGain(); master.gain.value = settings.mute ? 0 : settings.vol; master.connect(ac.destination);
    // a low crowd murmur: filtered noise
    const buf = ac.createBuffer(1, ac.sampleRate * 2, ac.sampleRate), d = buf.getChannelData(0); for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
    const src = ac.createBufferSource(); src.buffer = buf; src.loop = true; const f = ac.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 380; crowd = ac.createGain(); crowd.gain.value = 0.05; src.connect(f); f.connect(crowd); crowd.connect(master); src.start();
  } catch (e) { ac = null; }
}
function tone(freq, dur, type = 'sine', gain = 0.3, slide = 0) {
  if (!ac || !master) return; const t = ac.currentTime, o = ac.createOscillator(), g = ac.createGain();
  o.type = type; o.frequency.setValueAtTime(freq, t); if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(20, freq + slide), t + dur);
  g.gain.setValueAtTime(gain, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur); o.connect(g); g.connect(master); o.start(t); o.stop(t + dur + 0.02);
}
function burst(dur, gain = 0.25, freq = 1200) {
  if (!ac || !master) return; const t = ac.currentTime, n = Math.floor(ac.sampleRate * dur), buf = ac.createBuffer(1, n, ac.sampleRate), d = buf.getChannelData(0);
  for (let i = 0; i < n; i++) d[i] = (Math.random() * 2 - 1) * (1 - i / n); const s = ac.createBufferSource(); s.buffer = buf; const f = ac.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = freq; const g = ac.createGain(); g.gain.value = gain;
  s.connect(f); f.connect(g); g.connect(master); s.start(t);
}
const SOUND = {
  kick: () => { tone(120, 0.12, 'sine', 0.5, -60); burst(0.05, 0.2, 900); },
  whistle: () => { tone(2900, 0.28, 'square', 0.12); tone(3100, 0.28, 'square', 0.08); },
  goal: () => { [392, 494, 587, 784].forEach((f, i) => setTimeout(() => tone(f, 0.5, 'triangle', 0.25), i * 120)); if (crowd && ac) { crowd.gain.cancelScheduledValues(ac.currentTime); crowd.gain.setValueAtTime(0.25, ac.currentTime); crowd.gain.exponentialRampToValueAtTime(0.05, ac.currentTime + 3); } },
  head: () => { tone(200, 0.08, 'sine', 0.4, -80); },
  foul: () => { tone(2900, 0.5, 'square', 0.12); },
  save: () => { burst(0.1, 0.3, 500); },
  ui: () => { tone(660, 0.06, 'sine', 0.15); },
};
const buzz = ms => { if (settings.haptics && !settings.reduce && navigator.vibrate) { try { navigator.vibrate(ms); } catch (e) { /* ignore */ } } };

// ---------------------------------------------------------------- the log of the person's inputs (to reproduce a control problem: it is copied by hand, never sent)
const inputLog = []; let logSeed = 0, logStart = 0;
function logInput(type, a, b, c, d) { if (!running) return; inputLog.push([+((performance.now() - logStart) / 1000).toFixed(2), type, a, b, c, d]); if (inputLog.length > 3000) inputLog.shift(); }

function resize() { const r = window.devicePixelRatio || 1; canvas.width = Math.floor(innerWidth * r); canvas.height = Math.floor(innerHeight * r); }
addEventListener('resize', resize); resize();

// ---------------------------------------------------------------- boot
async function boot() {
  applySettings();
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
  $('assistChk').checked = settings.assist; profileLine(); applySettings();
  // REQ-0659: the menu keeps what the person chose when he comes back to it
  try {
    const m = JSON.parse(localStorage.getItem('fs27.menu') || '{}');
    for (const id of ['homeSel', 'awaySel', 'modeSel', 'levelSel']) if (m[id] != null && [...$(id).options].some(o => o.value === String(m[id]))) $(id).value = String(m[id]);
  } catch (e) { /* no storage: defaults */ }
  for (const id of ['homeSel', 'awaySel', 'modeSel', 'levelSel']) $(id).addEventListener('change', () => { try { const m = {}; for (const k of ['homeSel', 'awaySel', 'modeSel', 'levelSel']) m[k] = $(k).value; localStorage.setItem('fs27.menu', JSON.stringify(m)); } catch (e) { /* ignore */ } });
  $('menu').classList.remove('hidden');
}
$('practiceBtn').onclick = () => { $('menu').classList.add('hidden'); startMatch(+$('homeSel').value, +$('awaySel').value, (Math.random() * 1e6) | 0, 'Classic', 'Novice', true); };
$('onlineBtn').onclick = () => { $('profileLine').textContent = 'El modo online todavía no existe: necesitaría conexión. El juego completo funciona sin conexión y sin cuenta.'; };
$('startBtn').onclick = () => { $('menu').classList.add('hidden'); startMatch(+$('homeSel').value, +$('awaySel').value, (Math.random() * 1e6) | 0, $('modeSel').value, $('levelSel').value); };
$('againBtn').onclick = () => { $('end').classList.add('hidden'); $('hintbar').classList.add('hidden'); $('hud').classList.add('hidden'); running = false; $('menu').classList.remove('hidden'); };

function startMatch(h, a, seed, mode, level, practice = false) {
  const r = B.NewMatch(h, a, seed, mode, level);
  if (r.startsWith('error')) { alert(r); return; }
  const assist = params.has('assist') ? params.get('assist') !== '0' : ($('assistChk') ? $('assistChk').checked : true);
  settings.assist = assist; saveSettings(); audioInit(); inputLog.length = 0; logSeed = seed; logStart = performance.now(); tutorialStart(practice);
  B.SetAimAssist(assist);
  roster = JSON.parse(r);
  crossHinted = false; session = { home: h, away: a, seed, mode, level, assist, startedAt: Date.now(), fpsSamples: [], worstMs: 0 };
  showHint('Mueve el círculo · B pasa · desliza A hacia abajo para disparar · ⇄ cambia de jugador', 9000);
  $('hn').textContent = roster.home.slice(0, 12).toUpperCase(); $('an').textContent = roster.away.slice(0, 12).toUpperCase();
  $('hud').classList.remove('hidden'); lastScore = [0, 0]; paused = false; running = true; stats.lastT = performance.now();
  requestAnimationFrame(frame);
}

// ---------------------------------------------------------------- the statistics of the match and the profile (REQ 0014, 0651)
let finalStats = null;
function endStats() {
  const st = JSON.parse(B.Stats()); finalStats = st; const h = st.home, a = st.away;
  const row = (l, x, y, suffix = '') => `<tr><td>${x}${suffix}</td><td>${l}</td><td>${y}${suffix}</td></tr>`;
  $('endStats').innerHTML = row('Posesión', h.possession, a.possession, ' %') + row('Tiros', h.shots, a.shots) + row('Pases', h.passes, a.passes) + row('Entradas', h.tackles, a.tackles) + row('Faltas', h.fouls, a.fouls) + row('Córners', h.corners, a.corners) + row('Saques de banda', h.throwIns, a.throwIns) + row('Cabezazos', h.headers, a.headers);
  profile.played++; if (snap[2] > snap[3]) profile.wins++; else if (snap[2] < snap[3]) profile.losses++; else profile.draws++; profile.gf += snap[2]; profile.ga += snap[3]; saveProfile();
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
  if (finalStats) lines.push(`Estadísticas (tú-rival): posesión ${finalStats.home.possession}-${finalStats.away.possession} %, tiros ${finalStats.home.shots}-${finalStats.away.shots}, pases ${finalStats.home.passes}-${finalStats.away.passes}, faltas ${finalStats.home.fouls}-${finalStats.away.fouls}, cabezazos ${finalStats.home.headers}-${finalStats.away.headers}`);
  lines.push(`Ajustes: cámara ${settings.cam}, zurdo ${settings.lefty ? 'sí' : 'no'}, sonido ${settings.mute ? 'no' : 'sí'}, entradas registradas ${inputLog.length}`);
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

// ---------------------------------------------------------------- what the Core reports: sound, vibration, notices (REQ 0009, 0646)
let suggested = -1, canCross = false, crossHinted = false;
const NOTICE = { ThrowInAwarded: 'SAQUE DE BANDA', CornerAwarded: 'CÓRNER', GoalKickAwarded: 'SAQUE DE PUERTA', FreeKickAwarded: 'FALTA', PenaltyAwarded: 'PENALTI', HalfTimeStarted: 'DESCANSO', YellowCardIssued: 'TARJETA AMARILLA', RedCardIssued: 'TARJETA ROJA' };
function handleEvents() {
  const q = B.DrainEvents(); if (!q) return;
  for (const item of q.split(',')) {
    if (!item) continue; const [kind, team] = item.split(':');
    if (kind === 'ShotPerformed' || kind === 'PassPerformed') { SOUND.kick(); if (team === '0') buzz(12); }
    else if (kind === 'Header') SOUND.head();
    else if (kind === 'GoalScored') { SOUND.goal(); buzz(team === '0' ? [60, 40, 120] : 40); }
    else if (kind === 'FoulCommitted' || kind === 'YellowCardIssued' || kind === 'RedCardIssued') { SOUND.foul(); buzz(50); }
    else if (kind === 'BallDeflected') SOUND.save();
    else if (kind === 'MatchEnded' || kind === 'HalfTimeStarted') SOUND.whistle();
    else if (kind === 'ThrowInAwarded' || kind === 'CornerAwarded' || kind === 'GoalKickAwarded' || kind === 'FreeKickAwarded' || kind === 'PenaltyAwarded') SOUND.whistle();
    if (NOTICE[kind] && !(kind === 'GoalKickAwarded' && team !== '0')) { banner = NOTICE[kind]; bannerUntil = performance.now() + 1500; }
  }
}

// ---------------------------------------------------------------- the practice: five steps, each one done by really doing it (REQ 0057, 0761)
let tu = null;
const TU_STEPS = [
  ['1/5 · Moverte', 'Arrastra el círculo de la izquierda y corre un poco.', t => t.moved >= 1.5],
  ['2/5 · Pasar', 'Con el balón, empuja el círculo hacia un compañero y pulsa B.', t => t.passed],
  ['3/5 · Disparar', 'Cerca de la portería rival, desliza A hacia abajo.', t => t.shot],
  ['4/5 · Cambiar de jugador', 'Cuando defiendas, pulsa ⇄ para controlar al más cercano al balón.', t => t.switched],
  ['5/5 · ¡Listo!', 'Ya sabes lo básico. Sigue jugando; sal desde la pausa cuando quieras.', t => t.doneAt && performance.now() - t.doneAt > 4000],
];
function tutorialStart(on) { tu = on ? { step: 0, moved: 0, passed: false, shot: false, switched: false, lastT: performance.now(), doneAt: 0 } : null; $('tutorial').classList.toggle('hidden', !on); if (on) tutorialShow(); }
function tutorialShow() { if (!tu) return; const [title, text] = TU_STEPS[tu.step]; $('tuTitle').textContent = title; $('tuText').textContent = text; }
function tutorialTick() {
  if (!tu) return; const now = performance.now(), dt = (now - tu.lastT) / 1000; tu.lastT = now;
  if (Math.hypot(stick.x, stick.y) > 0.3 && !paused) tu.moved += dt;
  const a = B.LastAction(); if (a && a !== tu.lastA && snap && snap[10] < 0.6) { tu.lastA = a; if (/Pass|ThroughPass/.test(a)) tu.passed = true; if (/Shot|Chip/.test(a)) tu.shot = true; }
  if (TU_STEPS[tu.step][2](tu)) { tu.step++; if (tu.step >= TU_STEPS.length) { tutorialStart(false); return; } if (tu.step === 4) tu.doneAt = now; tutorialShow(); SOUND.ui(); }
}
$('tuSkip').onclick = () => tutorialStart(false);

// ---------------------------------------------------------------- settings screen and pause
const bind = (id, key, kind = 'num') => { const el = $(id); el.onchange = el.oninput = () => { settings[key] = kind === 'bool' ? el.checked : kind === 'str' ? el.value : +el.value; if (key === 'assist' && B) B.SetAimAssist(settings.assist); applySettings(); saveSettings(); }; };
function settingsOpen() { const m = { setStick: 'stickScale', setBtn: 'btnScale', setZoom: 'zoom', setVol: 'vol', setText: 'text' }; for (const [id, k] of Object.entries(m)) $(id).value = settings[k]; for (const [id, k] of Object.entries({ setLefty: 'lefty', setAssist: 'assist', setMute: 'mute', setHaptics: 'haptics', setColor: 'color', setReduce: 'reduce' })) $(id).checked = !!settings[k]; $('setCam').value = settings.cam; $('settings').classList.remove('hidden'); SOUND.ui(); }
bind('setStick', 'stickScale'); bind('setBtn', 'btnScale'); bind('setZoom', 'zoom'); bind('setVol', 'vol'); bind('setText', 'text'); bind('setCam', 'cam', 'str');
bind('setLefty', 'lefty', 'bool'); bind('setAssist', 'assist', 'bool'); bind('setMute', 'mute', 'bool'); bind('setHaptics', 'haptics', 'bool'); bind('setColor', 'color', 'bool'); bind('setReduce', 'reduce', 'bool');
$('settingsBtn').onclick = () => { audioInit(); settingsOpen(); }; $('pauseSettingsBtn').onclick = settingsOpen; $('settingsClose').onclick = () => $('settings').classList.add('hidden');
function setPaused(on) { paused = on; $('pausebox').classList.toggle('hidden', !on || !running); $('logMsg').textContent = ''; }
$('resumeBtn').onclick = () => setPaused(false);
$('quitBtn').onclick = () => { setPaused(false); running = false; tutorialStart(false); $('hud').classList.add('hidden'); $('hintbar').classList.add('hidden'); $('spbox').classList.add('hidden'); $('menu').classList.remove('hidden'); profileLine(); };
$('logBtn').onclick = async () => {
  const text = JSON.stringify({ version: VERSION, seed: logSeed, session, events: inputLog });
  let ok = false; try { await navigator.clipboard.writeText(text); ok = true; } catch (e) { /* fallback below */ }
  if (!ok) { const box = $('report'); box.value = text; box.classList.remove('hidden'); box.select(); try { ok = document.execCommand('copy'); } catch (e) { ok = false; } }
  $('logMsg').textContent = ok ? `Copiado (${inputLog.length} entradas). Pégalo en un mensaje si algo no responde bien.` : 'No he podido copiarlo solo.';
};

// ---------------------------------------------------------------- throw-ins, corners, free kicks and penalties taken by the person (Phases 33 and 35)
const SP_TITLE = { 1: 'TU SAQUE DE BANDA', 2: 'TU CÓRNER', 3: 'TU TIRO LIBRE', 4: 'TU PENALTI' };
let spKind = 0;
function updateSetPieceUI(now) {
  const on = sp[0] !== 0;
  $('spbox').classList.toggle('hidden', !on); $('spzones').classList.toggle('hidden', sp[0] < 2);
  if (!on) { spHoldStart = 0; spKind = 0; return; }
  if (spKind !== sp[0]) {   // the zone buttons of this kind: a free kick may also be passed (-1); a corner or a penalty goes to a zone
    spKind = sp[0]; spZone = sp[0] === 3 ? -1 : 1;
    $('spzones').innerHTML = (sp[0] === 3 ? '<button data-z="-1">Pase</button>' : '') + '<button data-z="0">Izquierda</button><button data-z="1">Centro</button><button data-z="2">Derecha</button>';
    for (const b of document.querySelectorAll('#spzones button')) { b.classList.toggle('sel', +b.dataset.z === spZone); b.addEventListener('pointerdown', () => { spZone = +b.dataset.z; for (const x of document.querySelectorAll('#spzones button')) x.classList.toggle('sel', x === b); }); }
  }
  $('sptitle').textContent = SP_TITLE[sp[0]];
  const held = spHoldStart ? Math.min(1, (performance.now() - spHoldStart) / 1000 / Math.max(0.1, sp[4])) : 0;
  $('spfill').style.width = Math.round(held * 100) + '%';
  $('sptext').textContent = (sp[0] >= 2 ? 'Elige hacia dónde. ' : '') + 'Apunta con el círculo, mantén B para cargar la fuerza y suelta para sacar. Sin hacer nada, saca la IA en ' + Math.ceil(sp[3]) + ' s.';
}

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
  sp = B.SetPieceInfo(); updateSetPieceUI(now); handleEvents(); if ((stats.frames & 7) === 0) { suggested = B.SuggestedIndex(); const cc = B.CanCross(); if (cc !== canCross) { canCross = cc; $('btnA').textContent = cc ? 'CENTRO' : 'A'; $('btnA').classList.toggle('cross', cc); if (cc && !crossHinted) { crossHinted = true; showHint('Estás en banda: desliza A hacia arriba para centrar. Corto = raso, medio = tenso, largo = alto. Con el círculo eliges primer palo, centro o segundo palo.', 8000); } } } tutorialTick();
  const t1 = performance.now(); snap = B.Snapshot(); stats.snapshotMs += performance.now() - t1;
  const t2 = performance.now(); draw(now); stats.drawMs += performance.now() - t2;
  stats.frames++;
  if (now - (stats.sec || 0) >= 1000) { stats.fps = stats.frames; stats.sps = stats.steps; stats.msAdv = stats.advanceMs / Math.max(1, stats.frames); stats.msSnap = stats.snapshotMs / Math.max(1, stats.frames); stats.msDraw = stats.drawMs / Math.max(1, stats.frames); stats.frames = 0; stats.steps = 0; stats.advanceMs = stats.snapshotMs = stats.drawMs = 0; stats.sec = now; $('fps').textContent = `${stats.fps} fps · ${stats.sps} pasos/s · core ${stats.msAdv.toFixed(1)} ms`; }
  requestAnimationFrame(frame);
}

// ---------------------------------------------------------------- drawing
const fmt = s => { s = Math.max(0, Math.floor(s)); return String(Math.floor(s / 60)).padStart(2, '0') + ':' + String(s % 60).padStart(2, '0'); };
const colorOf = (team, gk) => settings.color ? (gk ? (team ? '#ffffff' : '#56b4e9') : (team ? '#e69f00' : '#0072b2')) : (gk ? (team ? '#f1c40f' : '#2ecc71') : (team ? '#e74c3c' : '#3498db'));

function draw(now) {
  const W = canvas.width, Hh = canvas.height, L = roster.fieldLength, Wd = roster.fieldWidth;
  mirror = snap[12] ? 1 : -1;
  const fit = Math.min(W / (L + 6), Hh / (Wd + 4)), scale = fit * (settings.cam === 'wide' ? 1 : settings.zoom);
  const bx = snap[6], bz = snap[8];
  const maxCx = Math.max(0, (L + 6) / 2 - W / scale / 2), maxCy = Math.max(0, (Wd + 4) / 2 - Hh / scale / 2);
  const camX = settings.cam === 'wide' ? 0 : Math.max(-maxCx, Math.min(maxCx, bx * mirror)) * mirror, camY = settings.cam === 'wide' ? 0 : Math.max(-maxCy, Math.min(maxCy, bz * mirror)) * mirror;
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
  if (settings.cam === 'wide') { /* the whole pitch, fixed */ }
  for (const i of idx) {
    const b = H + i * PF, p = roster.players[i]; if (snap[b + 5]) continue;
    const px = sx(snap[b]), py = sy(snap[b + 1]), r = Math.max(7, scale * 0.9);
    ctx.fillStyle = 'rgba(0,0,0,.28)'; ctx.beginPath(); ctx.ellipse(px, py + r * 0.55, r * 0.95, r * 0.4, 0, 0, 7); ctx.fill();
    if (i === suggested && i !== ctrl && sp[0] === 0) { ctx.strokeStyle = 'rgba(255,255,255,.55)'; ctx.setLineDash([4, 4]); ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(px, py, r + 4, 0, 7); ctx.stroke(); ctx.setLineDash([]); }
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
  // HUD: who has the ball (the team of the carrier) and the stamina of the controlled player with the sprint warning
  { let who = -1; for (let i = 0; i < n; i++) if (snap[H + i * PF + 3]) { who = roster.players[i].team; break; }
    $('possTxt').textContent = who < 0 ? 'balón libre' : who === 0 ? 'balón tuyo' : 'balón rival'; $('possDot').style.color = who < 0 ? '#bbb' : colorOf(who, false);
    if (ctrl >= 0) { const st = Math.max(0, Math.min(1, snap[H + ctrl * PF + 4] / 12)); $('stamFill').style.width = Math.round(st * 100) + '%'; $('stamFill').style.background = st > .3 ? '#7CFC00' : '#ff5555'; $('stamTxt').textContent = Math.hypot(stick.x, stick.y) > 0.85 && st > 0 ? 'SPRINT' : (st <= .3 ? 'CANSADO' : 'RESISTENCIA'); } }
  // HUD text
  $('hs').textContent = snap[2]; $('as').textContent = snap[3]; $('clock').textContent = fmt(snap[5]);
  if (snap[2] !== lastScore[0] || snap[3] !== lastScore[1]) { banner = 'GOL'; bannerUntil = now + 2200; lastScore = [snap[2], snap[3]]; }
  const phase = snap[1]; let text = '';
  if (now < bannerUntil) text = banner; else if (phase === PHASE.Break) text = 'DESCANSO'; else if (phase === PHASE.Paused) text = 'PAUSA';
  $('banner').textContent = paused ? 'PAUSA' : text;
  if (snap[11] && running) {
    running = false; prepareFeedback(); $('endTitle').textContent = snap[2] > snap[3] ? '¡Victoria!' : snap[2] < snap[3] ? 'Derrota' : 'Empate'; $('endScore').textContent = `${roster.home} ${snap[2]} – ${snap[3]} ${roster.away}`; endStats(); $('end').classList.remove('hidden'); tutorialStart(false); setPaused(false);
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
  stick = { x: dx, y: dy }; B && B.SetStick(mirror * dx, mirror * dy); if ((stats.frames & 3) === 0) logInput('stick', +dx.toFixed(2), +dy.toFixed(2));
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
    if (sp[0] !== 0 && button === 2) { const hold = spHoldStart ? (performance.now() - spHoldStart) / 1000 : 0; spHoldStart = 0; logInput('sp', +hold.toFixed(2), spZone, +(mirror * stick.x).toFixed(2), +(mirror * stick.y).toFixed(2)); B.TakeSetPiece(mirror * stick.x, mirror * stick.y, hold, sp[0] >= 2 ? spZone : -1); return; }
    const dy = e.clientY - y0; let gesture = 0, power = 0.7;
    if (dy < -28) { gesture = 1; power = Math.min(1, 0.55 + (-dy - 28) / 120); if (button === 1 && canCross) power = Math.min(1, 0.3 + (-dy - 28) / 150); }   // on the flank the swipe up on A is a cross: up to 82 px ground, to 112 px driven, beyond high else if (dy > 28) { gesture = 2; power = Math.min(1, 0.55 + (dy - 28) / 120); }
    logInput('press', button, gesture, +power.toFixed(2), +(mirror * stick.x).toFixed(2)); B && B.Press(button, gesture, mirror * stick.x, mirror * stick.y, power);
  };
  btn.addEventListener('pointerup', end); btn.addEventListener('pointercancel', e => { if (e.pointerId === id) { id = null; btn.classList.remove('on'); } });
}
action($('btnA'), 1); action($('btnB'), 2); action($('btnC'), 3);
$('btnSw').addEventListener('pointerdown', () => { if (B) { logInput('switch'); B.SwitchPlayer(); if (tu) tu.switched = true; } });
$('btnPause').addEventListener('pointerdown', () => { setPaused(!paused); });
// keyboard (testing on a computer): arrows/WASD, J/K/L = A/B/C (hold Shift = swipe down), Tab = switch
const keys = new Set();
function kbStick() { const x = (keys.has('ArrowRight') || keys.has('d') ? 1 : 0) - (keys.has('ArrowLeft') || keys.has('a') ? 1 : 0), y = (keys.has('ArrowDown') || keys.has('s') ? 1 : 0) - (keys.has('ArrowUp') || keys.has('w') ? 1 : 0); const m = Math.hypot(x, y) || 1; stick = { x: x / m * (x || y ? 1 : 0), y: y / m * (x || y ? 1 : 0) }; B && B.SetStick(mirror * stick.x, mirror * stick.y); }
addEventListener('keydown', e => { if (e.repeat) return; keys.add(e.key); kbStick(); if (sp[0] !== 0 && e.key.toLowerCase() === 'k') { spHoldStart = performance.now(); return; } const bt = { j: 1, k: 2, l: 3 }[e.key.toLowerCase()]; if (bt && B) B.Press(bt, e.shiftKey ? 2 : (e.ctrlKey ? 1 : 0), mirror * stick.x, mirror * stick.y, 0.8); if (e.key === 'Tab') { e.preventDefault(); if (B) { logInput('switch'); B.SwitchPlayer(); if (tu) tu.switched = true; } } if (e.key === 'Escape' && running) setPaused(!paused); });
addEventListener('keyup', e => { keys.delete(e.key); kbStick(); if (sp[0] !== 0 && e.key.toLowerCase() === 'k' && spHoldStart) { const hold = (performance.now() - spHoldStart) / 1000; spHoldStart = 0; B.TakeSetPiece(mirror * stick.x, mirror * stick.y, hold, sp[0] >= 2 ? spZone : -1); } });
document.addEventListener('contextmenu', e => e.preventDefault());
// a call, a notification or a change of tab must not leave a button or the stick stuck (FS27-REQ-0053): everything is released and the match pauses
function releaseAll() {
  stickId = null; knob.style.transform = ''; stick = { x: 0, y: 0 }; keys.clear(); spHoldStart = 0;
  if (B) B.SetStick(0, 0);
  if (running) setPaused(true);
}
addEventListener('blur', releaseAll);
document.addEventListener('visibilitychange', () => { if (document.hidden) releaseAll(); });

boot().catch(e => { $('loadmsg').textContent = 'Error: ' + e.message; console.error(e); });
