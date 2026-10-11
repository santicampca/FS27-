// Partido: vista de preparación y primer prototipo visual. HONESTO sobre lo que es: la web NO ejecuta el Core en vivo.
//  · "Campo" reproduce grabaciones reales de partidos simulados por el Core (posiciones, goles, faltas...).
//  · "Penal", "Tanda" y "VAR" son escenas jugables cuyos resultados son respuestas reales del Core (laboratorio de penales y revisiones grabadas).
//  · "Controles" es la referencia visual de la futura interfaz móvil (joystick y A/B/C): el movimiento del muñeco es real en pantalla, las acciones NO están conectadas al Core.
import { $, $$, esc, mmss, toast, clamp } from './dom.js';
import { loadData, loadLab, loadTraceIndex, loadTrace, LEVEL_ES } from '../core/data.js';
import { head, note, loading, failure } from './screen.js';
import { crest } from './components.js';
import { PitchView } from './pitch.js';
import { penaltyDuel } from './duel.js';
import { renderShootout } from './tanda.js';
import { renderVar } from './var.js';
import { selection, settings, inventory } from '../app/state.js';

const EVENT_TEXT = {
  GoalScored: (p) => `¡Gol${p ? ' de ' + p : ''}!`, FoulCommitted: (p) => `Falta${p ? ' de ' + p : ''}`, YellowCardIssued: (p) => `Tarjeta amarilla${p ? ' a ' + p : ''}`, RedCardIssued: (p) => `Tarjeta roja${p ? ' a ' + p : ''}`,
  PenaltyAwarded: () => 'Penal concedido', ShotPerformed: (p) => `Disparo${p ? ' de ' + p : ''}`, CornerAwarded: () => 'Córner', FreeKickTaken: (p) => `Tiro libre${p ? ' de ' + p : ''}`, OffsideDetected: () => 'Fuera de juego', PeriodEnded: () => 'Final del tiempo', HalfTimeStarted: () => 'Descanso',
};
const TABS = [['campo', 'Campo'], ['penal', 'Penal'], ['tanda', 'Tanda'], ['var', 'VAR'], ['controles', 'Controles']];

export async function render(el, { query, navigate }) {
  const lead = 'Vista del partido con las selecciones que hiciste. Aún no es un partido jugable del Core: reproduce grabaciones reales y juega escenas reales (penal, tanda, VAR).';
  el.innerHTML = head('Partido', 'PARTIDO', 'PROTOTIPO VISUAL', lead, '#/' + selection.mode.toLowerCase()) + loading();
  let data, lab, tindex;
  try { [data, lab, tindex] = await Promise.all([loadData(), loadLab(), loadTraceIndex()]); } catch (e) { el.innerHTML = head('Partido', 'PARTIDO', 'SIN DATOS', '') + failure(e); return; }
  const mode = selection.mode, classic = mode === 'Classic';
  const mine = data.teamsById.get(selection.team), rival = data.teamsById.get(selection.rival);
  const problems = [];
  if (!mine) problems.push('Falta elegir tu equipo.'); if (!rival) problems.push('Falta elegir el rival.'); if (mine && rival && mine.id === rival.id) problems.push('El rival debe ser otro equipo.');
  if (problems.length) { el.innerHTML = head('Partido', 'PARTIDO', 'FALTAN DATOS', lead, '#/' + mode.toLowerCase()) + `<ul class="errors" role="alert">${problems.map((p) => `<li>${esc(p)}</li>`).join('')}</ul><div class="actions"><a class="btn btn--primary" href="#/${mode.toLowerCase()}">Ir a preparar el partido</a></div>`; return; }
  const diff = (data.difficulties.find((d) => d.level === selection.difficulty) || {}).name || selection.difficulty;
  let tab = TABS.some(([k]) => k === query.get('tab')) ? query.get('tab') : 'campo';
  let cleanup = null;

  const draw = () => {
    if (cleanup) { cleanup(); cleanup = null; }
    el.innerHTML = head('Partido', 'PARTIDO', 'PROTOTIPO VISUAL', lead, '#/' + mode.toLowerCase()) +
      `<div class="versus versus--bar"><div>${crest(mine, 40)}<b>${esc(mine.name)}</b></div><span class="vs">${esc(mode.toUpperCase())}</span><div>${crest(rival, 40)}<b>${esc(rival.name)}</b></div></div>
       <p class="field-note center">${esc(mode)} · dificultad ${esc(diff)} · tus selecciones se transmiten tal cual a cada escena</p>
       <div class="tabs" role="tablist" aria-label="Escenas del partido">${TABS.map(([k, l]) => `<button role="tab" class="tab" data-tab="${k}" aria-selected="${k === tab}">${l}</button>`).join('')}</div><div id="scene"></div>`;
    $$('[data-tab]', el).forEach((b) => b.addEventListener('click', () => { tab = b.dataset.tab; history.replaceState(null, '', '#/partido?tab=' + tab); draw(); $(`[data-tab="${tab}"]`, el).focus(); }));
    const host = $('#scene', el);
    if (tab === 'campo') cleanup = fieldScene(host);
    else if (tab === 'penal') penalScene(host);
    else if (tab === 'tanda') tandaScene(host);
    else if (tab === 'var') varScene(host);
    else cleanup = controlsScene(host);
  };

  // ---------------- campo: reproducción de grabaciones reales ----------------
  function fieldScene(host) {
    const options = tindex.traces.filter((t) => t.mode === mode);
    const exact = options.find((t) => t.home === mine.id && t.away === rival.id);
    let chosen = exact || options[0];
    host.innerHTML = `<section class="card card--wide"><div class="field-head">${exact ? `<p class="field-note"><b>Hay una grabación real de ${esc(mine.name)} vs ${esc(rival.name)}.</b></p>` : `<p class="field-note">No hay grabación de ${esc(mine.name)} vs ${esc(rival.name)} en ${esc(mode)}. Se muestran las ${options.length} grabaciones disponibles: elige una.</p>`}
      <label class="fld"><span>Grabación</span><select id="rec">${options.map((t) => `<option value="${esc(t.id)}"${t.id === chosen.id ? ' selected' : ''}>${esc(data.teamsById.get(t.home).name)} vs ${esc(data.teamsById.get(t.away).name)}</option>`).join('')}</select></label></div>
      <div id="viewer"></div></section>`;
    let view = null, raf = 0, playing = false, last = 0, speed = 1, destroyed = false, trace = null, teams = null;
    const stop = () => { playing = false; cancelAnimationFrame(raf); };
    async function load(id) {
      stop(); if (view) { view.destroy(); view = null; }
      const v = $('#viewer', host); v.innerHTML = loading('la grabación');
      let doc; try { doc = await loadTrace(tindex.traces.find((t) => t.id === id).file); } catch (e) { v.innerHTML = failure(e); return; }
      if (destroyed) return;
      trace = doc.trace; teams = { home: data.teamsById.get(trace.home), away: data.teamsById.get(trace.away) };
      v.innerHTML = `<div class="scoreboard"><div class="sb__t">${crest(teams.home, 30)}<b>${esc(teams.home.shortName)}</b></div><div class="sb__s"><span id="sh">0</span> - <span id="sa">0</span></div><div class="sb__t sb__t--r"><b>${esc(teams.away.shortName)}</b>${crest(teams.away, 30)}</div><span class="sb__clock" id="clock">00:00</span></div>
        <canvas id="cv" class="pitchcv" aria-label="Campo con las posiciones de los jugadores y el balón" role="img"></canvas>
        <div class="player-ctl"><button class="btn btn--primary" id="play">▶ Reproducir</button><input type="range" id="seek" min="0" max="${trace.seconds}" step="0.1" value="0" aria-label="Posición de la grabación"><label class="fld fld--inline"><span>Velocidad</span><select id="spd"><option value="0.5">0,5×</option><option value="1" selected>1×</option><option value="2">2×</option><option value="4">4×</option></select></label></div>
        <ol class="ticker" id="tick" aria-live="off"></ol><p class="field-note">Grabación real: partido simulado por el Core con su IA en ambos equipos (dificultad Professional, ${trace.seconds} s, semilla ${trace.seed}). Es una repetición: no se puede intervenir ni afecta a tus ZP.</p>`;
      const pids = trace.players; view = new PitchView($('#cv', host), trace, pids, teams, data);
      const name = (pid) => (data.playersById.get(pid) || {}).shortName || '';
      const goals = trace.events.filter((e) => e.kind === 'GoalScored');
      const update = () => {
        const t = view.t; let h = 0, a = 0; for (const g of goals) if (g.t <= t) (g.team === 'Home' ? h++ : a++);
        $('#sh', host).textContent = h; $('#sa', host).textContent = a; $('#clock', host).textContent = mmss(t); $('#seek', host).value = t;
        const shown = trace.events.filter((e) => e.t <= t && EVENT_TEXT[e.kind] && e.kind !== 'ShotPerformed').slice(-5).reverse();
        $('#tick', host).innerHTML = shown.map((e) => `<li class="${e.kind === 'GoalScored' ? 'is-goal' : ''}"><b>${mmss(e.t)}</b> ${esc(EVENT_TEXT[e.kind](name(e.player)))}${e.team ? ` <small>${esc((e.team === 'Home' ? teams.home : teams.away).shortName)}</small>` : ''}</li>`).join('') || '<li><small>Sin incidencias todavía</small></li>';
      };
      const loop = (ts) => { if (!playing) return; const dt = (ts - last) / 1000; last = ts; view.seek(view.t + dt * speed); update(); if (view.t >= view.duration) { playing = false; $('#play', host).textContent = '↺ Repetir'; finish(); return; } raf = requestAnimationFrame(loop); };
      const finish = () => { const sh = goals.filter((g) => g.team === 'Home').length, sa = goals.length - sh; $('#tick', host).insertAdjacentHTML('afterbegin', `<li class="is-goal"><b>FIN</b> Resultado final ${esc(teams.home.shortName)} ${sh} - ${sa} ${esc(teams.away.shortName)} · ${trace.events.filter((e) => e.kind === 'ShotPerformed').length} disparos · ${trace.events.filter((e) => e.kind === 'FoulCommitted').length} faltas</li>`); };
      $('#play', host).addEventListener('click', () => { if (playing) { stop(); $('#play', host).textContent = '▶ Reproducir'; return; } if (view.t >= view.duration - 0.05) view.seek(0); playing = true; last = performance.now(); $('#play', host).textContent = '⏸ Pausa'; raf = requestAnimationFrame(loop); });
      $('#seek', host).addEventListener('input', (e) => { view.seek(Number(e.target.value)); update(); });
      $('#spd', host).addEventListener('change', (e) => { speed = Number(e.target.value); });
      view.seek(0); update();
    }
    $('#rec', host).addEventListener('change', (e) => { chosen = options.find((t) => t.id === e.target.value); load(chosen.id); });
    load(chosen.id);
    return () => { destroyed = true; stop(); if (view) view.destroy(); };
  }

  // ---------------- penal en partido (Classic) ----------------
  function penalScene(host) {
    if (!classic) { host.innerHTML = note('<b>Arena no tiene penales por falta.</b> Las faltas no existen en Arena; solo una tanda de desempate arcade (no disponible en la web todavía). Cambia a Classic para esta escena.', 'note--warn') + `<div class="actions"><a class="btn btn--primary" href="#/classic">Ir a Classic</a></div>`; return; }
    let score = { home: 0, away: 0 }, n = 0;
    const offender = data.playersById.get(rival.lineup.field[0].id), victim = data.playersById.get(mine.lineup.field[0].id);
    const draw = () => {
      host.innerHTML = `<section class="card card--wide"><h3>Penal en un partido Classic</h3><div class="scoreboard"><div class="sb__t">${crest(mine, 30)}<b>${esc(mine.shortName)}</b></div><div class="sb__s">${score.home} - ${score.away}</div><div class="sb__t sb__t--r"><b>${esc(rival.shortName)}</b>${crest(rival, 30)}</div></div>
        <p>${esc(offender.name)} (${esc(rival.shortName)}) derriba a ${esc(victim.name)} (${esc(mine.shortName)}) dentro del área. El árbitro pita <b>penal</b>.</p>
        <div class="actions"><button class="btn btn--primary btn--lg" id="kick">Lanzar el penal (tú lanzas)</button><button class="btn btn--ghost btn--lg" id="keep">Defender el penal (lanza ${esc(rival.shortName)})</button></div>
        ${note('El Core resuelve la física del balón, el marco, la estirada del portero y las reglas de rebote y de segundo toque (el lanzador no puede tocar el rebote de un poste o larguero antes que otro jugador). Esta escena muestra el resultado final del penal que devuelve el Core.')}</section>`;
      const go = async (role) => {
        n++;
        const attack = role === 'kick' ? mine : rival, defend = role === 'kick' ? rival : mine;
        const r = await penaltyDuel({ data, lab, attack, defend, role, seed: 1000 + n, context: { title: 'Penal', subtitle: `${mine.shortName} ${score.home} - ${score.away} ${rival.shortName}`, reason: `Falta de ${esc(offender.name)} sobre ${esc(victim.name)} en el área.` } });
        if (!r) return; if (r.result.code === 'G') { if (role === 'kick') score.home++; else score.away++; }
        draw();
      };
      $('#kick', host).addEventListener('click', () => go('kick')); $('#keep', host).addEventListener('click', () => go('keep'));
    };
    draw();
  }

  function tandaScene(host) {
    if (!classic) { host.innerHTML = note('<b>La tanda arcade de Arena aún no está en la web.</b> El Core ya resuelve la tanda de Arena con un duelo arcade (zona, potencia y altura contra la elección del portero), pero esta web solo tiene la tanda física de Classic.', 'note--warn') + `<div class="actions"><a class="btn btn--primary" href="#/classic">Ir a Classic</a></div>`; return; }
    renderShootout(host, { data, lab, mine, rival, rules: data.rules.Classic });
  }

  function varScene(host) {
    if (!classic) { host.innerHTML = note('<b>Arena no tiene VAR.</b> No hay árbitro ni faltas en Arena, así que no hay nada que revisar.', 'note--warn') + `<div class="actions"><a class="btn btn--primary" href="#/classic">Ir a Classic</a></div>`; return; }
    renderVar(host);
  }

  // ---------------- controles: referencia de la interfaz móvil ----------------
  function controlsScene(host) {
    const loc = data.config.locomotion;
    const star = data.playersById.get(mine.lineup.field[Math.min(2, mine.lineup.field.length - 1)].id);
    const left = settings.leftHanded;
    host.innerHTML = `<section class="card card--wide"><h3>Controles móviles · referencia</h3>
      <p class="field-note">Mueve el joystick: el muñeco (${esc(star.name)}, velocidad máxima ${star.maxSpeed.toFixed(1)} m/s según el Core) responde de verdad en esta pantalla. Caminar, correr y esprintar dependen de cuánto empujas; <b>no hay botón de sprint</b>: esprintar es empujar hasta el borde y gasta resistencia. Es una <b>demostración de la interfaz</b>, no el motor del Core.</p>
      <div class="ctl-stage" id="stage"><canvas id="cs" class="ctlcv" aria-label="Zona de práctica del joystick"></canvas>
        <div class="stick ${left ? 'stick--r' : ''}" id="stick" aria-label="Joystick virtual"><i id="knob"></i></div>
        <div class="abc ${left ? 'abc--l' : ''}"><button class="abtn abtn--c" data-b="C">C</button><button class="abtn abtn--b" data-b="B">B</button><button class="abtn abtn--a" data-b="A">A</button></div></div>
      <div class="hud"><span id="mv">Quieto</span><div class="bar" aria-label="Resistencia"><i id="sta" style="--w:100%"></i></div><span id="log" aria-live="polite">Toca A, B o C</span></div>
      <div class="segment segment--wide" role="group" aria-label="Contexto de los botones"><button data-ctx="atk" aria-pressed="true">Con balón</button><button data-ctx="def" aria-pressed="false">Sin balón</button></div>
      <p class="field-note" id="legend"></p>
      ${note('<b>No conectado:</b> los botones A, B y C solo muestran el nombre de la acción prevista. Las acciones reales del Core (disparo, pase, entrada…) aún no se pueden ejecutar desde la web. Puedes cambiar la mano dominante en Configuración.')}</section>`;
    const cv = $('#cs', host), g = cv.getContext('2d'), stick = $('#stick', host), knob = $('#knob', host);
    const W = 40, H = 25; let px = 0, pz = 0, vx = 0, vz = 0, sta = loc.maxStamina, mag = 0, dir = [0, 0], face = 0, raf = 0, last = performance.now(), alive = true;
    const size = () => { const r = cv.getBoundingClientRect(), d = Math.min(2, window.devicePixelRatio || 1); cv.width = r.width * d; cv.height = r.height * d; };
    size(); const ro = new ResizeObserver(size); ro.observe(cv);
    const ctxText = { atk: 'Con balón · A: disparo raso (curva con efecto; al soltar, posible disparo elevado) · B: pase corto o al espacio · C: habilidad según el contexto', def: 'Sin balón · A: presión, entrada o barrida · B: duelo físico, bloqueo, intercepción o despeje · C: salida del portero (cuando corresponde)' };
    let ctx = 'atk'; const legend = () => { $('#legend', host).textContent = ctxText[ctx]; }; legend();
    $$('[data-ctx]', host).forEach((b) => b.addEventListener('click', () => { ctx = b.dataset.ctx; $$('[data-ctx]', host).forEach((x) => x.setAttribute('aria-pressed', String(x === b))); legend(); }));
    $$('[data-b]', host).forEach((b) => b.addEventListener('pointerdown', () => { const m = { atk: { A: 'disparo', B: 'pase', C: 'habilidad' }, def: { A: 'presión / entrada', B: 'duelo / despeje', C: 'salida del portero' } }; $('#log', host).textContent = `${b.dataset.b}: ${m[ctx][b.dataset.b]} (no conectado al Core)`; b.classList.add('is-hit'); setTimeout(() => b.classList.remove('is-hit'), 160); }));
    const setStick = (e) => { const r = stick.getBoundingClientRect(), R = r.width / 2, cx = r.left + R, cy = r.top + R; let dx = e.clientX - cx, dy = e.clientY - cy; const m = Math.hypot(dx, dy); const k = m > R ? R / m : 1; dx *= k; dy *= k; mag = Math.min(1, m / R); dir = m ? [(e.clientX - cx) / m, (e.clientY - cy) / m] : [0, 0]; knob.style.transform = `translate(${dx}px,${dy}px)`; };
    let active = false;
    stick.addEventListener('pointerdown', (e) => { active = true; stick.setPointerCapture(e.pointerId); setStick(e); });
    stick.addEventListener('pointermove', (e) => { if (active) setStick(e); });
    const release = () => { active = false; mag = 0; knob.style.transform = ''; }; stick.addEventListener('pointerup', release); stick.addEventListener('pointercancel', release);
    const keys = new Set(); const onKey = (e, down) => { const k = e.key.toLowerCase(); if (['arrowup', 'arrowdown', 'arrowleft', 'arrowright', 'w', 'a', 's', 'd'].includes(k)) { down ? keys.add(k) : keys.delete(k); e.preventDefault(); } };
    const kd = (e) => onKey(e, true), ku = (e) => onKey(e, false); window.addEventListener('keydown', kd); window.addEventListener('keyup', ku);
    const step = (ts) => {
      if (!alive) return; const dt = Math.min(0.05, (ts - last) / 1000); last = ts;
      let m = mag, d = dir;
      if (keys.size) { const kx = (keys.has('arrowright') || keys.has('d') ? 1 : 0) - (keys.has('arrowleft') || keys.has('a') ? 1 : 0), kz = (keys.has('arrowdown') || keys.has('s') ? 1 : 0) - (keys.has('arrowup') || keys.has('w') ? 1 : 0); const n = Math.hypot(kx, kz) || 1; d = [kx / n, kz / n]; m = (kx || kz) ? 1 : 0; }
      const sprinting = m >= loc.sprintThreshold && sta > loc.exhaustedThreshold;
      const walk = m < loc.walkThreshold; const speedFrac = m < 0.08 ? 0 : walk ? 0.3 * (m / loc.walkThreshold) : sprinting ? 1 : 0.35 + 0.5 * clamp((m - loc.walkThreshold) / (loc.sprintThreshold - loc.walkThreshold), 0, 1);
      const target = star.maxSpeed * speedFrac; const tvx = d[0] * target, tvz = d[1] * target;
      vx += (tvx - vx) * Math.min(1, dt * 8); vz += (tvz - vz) * Math.min(1, dt * 8);
      px = clamp(px + vx * dt, -W / 2 + 1, W / 2 - 1); pz = clamp(pz + vz * dt, -H / 2 + 1, H / 2 - 1); if (Math.hypot(vx, vz) > 0.3) face = Math.atan2(vz, vx);
      sta = clamp(sta + (sprinting ? -loc.baseStaminaDrain * loc.sprintMultiplier * 0.01 : (walk ? loc.baseStaminaRecovery * loc.walkRecoveryFactor : loc.baseStaminaRecovery * 0.5) * 0.01) * dt * 100, 0, loc.maxStamina);
      $('#mv', host).textContent = m < 0.08 ? 'Quieto' : walk ? 'Caminando' : sprinting ? 'Esprintando' : 'Corriendo'; $('#sta', host).style.setProperty('--w', (sta / loc.maxStamina * 100).toFixed(0) + '%');
      const k = cv.width / (W + 2); g.clearRect(0, 0, cv.width, cv.height); g.fillStyle = '#0d6b3a'; g.fillRect(0, 0, cv.width, cv.height); g.strokeStyle = 'rgba(255,255,255,.8)'; g.lineWidth = 2; g.strokeRect(k, (cv.height - H * k) / 2, W * k, H * k);
      const sx = (px + W / 2 + 1) * k, sz = (pz + H / 2) * k + (cv.height - H * k) / 2; g.beginPath(); g.arc(sx, sz, 1.1 * k, 0, 7); g.fillStyle = mine.primary; g.fill(); g.strokeStyle = '#fff'; g.stroke();
      g.beginPath(); g.moveTo(sx, sz); g.lineTo(sx + Math.cos(face) * 1.7 * k, sz + Math.sin(face) * 1.7 * k); g.stroke();
      raf = requestAnimationFrame(step);
    };
    raf = requestAnimationFrame(step);
    return () => { alive = false; cancelAnimationFrame(raf); ro.disconnect(); window.removeEventListener('keydown', kd); window.removeEventListener('keyup', ku); };
  }

  draw();
  return () => { if (cleanup) cleanup(); };
}
