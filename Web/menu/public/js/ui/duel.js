// El duelo de penal (Classic): tirador, dirección, altura y potencia contra el portero. TODO el resultado sale del laboratorio del Core
// (fs27-penalty-lab.json): la web elige la celda más cercana a la decisión del usuario y muestra lo que el Core respondió.
import { esc, once, clamp } from './dom.js';
import { lookup, aiKick, aiKeeper, nearest, rng } from '../core/penalty.js';
import { settings } from '../app/state.js';
import { fx } from './fx.js';
import { crest } from './components.js';

const POWERS = [['Suave', 0.6], ['Media', 0.8], ['Fuerte', 1.0]];
const X0 = 60, XS = 38, YG = 52, YS = 34;                   // lateral -> x (la izquierda del lanzador es positiva y queda a la izquierda), altura -> y
const gx = (lat) => X0 - lat * XS, gy = (h) => YG - h * YS;

export function describeAim(a) {
  const side = Math.abs(a.lateral) > 1.02 ? (a.lateral > 0 ? 'fuera, a la izquierda' : 'fuera, a la derecha') : a.lateral > 0.7 ? 'esquina izquierda' : a.lateral > 0.15 ? 'izquierda' : a.lateral < -0.7 ? 'esquina derecha' : a.lateral < -0.15 ? 'derecha' : 'centro';
  const alt = a.height > 1.02 ? 'por encima del larguero' : a.height > 0.65 ? 'arriba' : a.height > 0.35 ? 'media altura' : 'raso';
  return side + ' · ' + alt;
}

/** @returns {Promise<null|{takerIndex:number,takerId:string,aim:object,dive:number,result:object,kicker:object}>} */
export function penaltyDuel(o) {
  const { data, lab, attack, defend, role, context = {} } = o;
  const pair = lab.pairs[attack.id] && lab.pairs[attack.id][defend.id];
  const grid = lab.grid, ai = data.config.penalty.ai;
  const rnd = rng(o.seed ?? 1);
  const keeper = data.playersById.get(defend.lineup.goalkeeper);
  const takers = pair.takers.map((t) => ({ ...t, p: data.playersById.get(t.id) }));
  const locked = o.takerIndex != null;
  let takerIndex = locked ? o.takerIndex : Math.max(0, takers.findIndex((t) => t.suggested));
  let aim = { lateral: 0, height: 0.2, power: 0.8 }, dive = 0, busy = false, finished = false;
  const take = once();

  return new Promise((resolve) => {
    const sheet = document.createElement('div'); sheet.className = 'sheet'; sheet.setAttribute('role', 'dialog'); sheet.setAttribute('aria-modal', 'true'); sheet.setAttribute('aria-label', context.title || 'Penal');
    const kick = role === 'kick';
    const gridDots = grid.lateral.flatMap((l) => grid.height.map((h) => `<circle cx="${gx(l)}" cy="${gy(h)}" r="${l === 0 && h === 0.2 ? 0 : 0.9}" class="gdot"/>`)).join('');
    sheet.innerHTML = `<div class="sheet__box">
      <header class="sheet__head"><div><b>${esc(context.title || 'Penal')}</b><small>${esc(context.subtitle || '')}</small></div>${o.allowCancel === false ? '' : '<button class="iconbtn" data-x aria-label="Cerrar">✕</button>'}</header>
      ${context.reason ? `<p class="sheet__reason">${context.reason}</p>` : ''}
      <div class="duel">
        <div class="duel__side">
          <p class="duel__who">${crest(attack, 22)} <b>${esc(attack.name)}</b> lanza · ${crest(defend, 22)} <b>${esc(defend.name)}</b> defiende · portero <b>${esc(keeper.name)}</b> <span class="rating tier-bronze">${keeper.rating}</span></p>
          ${kick ? `<div class="takers" role="radiogroup" aria-label="Lanzador">${takers.map((t, i) => `<button type="button" role="radio" class="taker" data-taker="${i}" aria-checked="${i === takerIndex}" ${locked && i !== takerIndex ? 'disabled' : ''}>
              <b>${esc(t.p.name)}</b><small>${esc(t.p.style || 'Jugador')} · disparo ${Math.round(t.shotClose * 100)}${t.suggested ? ' · ★ recomendado' : ''}</small></button>`).join('')}</div>` : `<p class="field-note">Eres el portero. Elige hacia dónde lanzarte: el lanzador rival (${esc(takers[takerIndex].p.name)}) decidirá su tiro sin saberlo.</p>`}
        </div>
        <div class="duel__goal">
          <svg id="goal" viewBox="0 0 120 64" tabindex="0" role="application" aria-label="Portería. ${kick ? 'Toca o usa las flechas para apuntar.' : 'Elige el lado del salto con los botones.'}" class="goalsvg">
            <defs><pattern id="net" width="3" height="3" patternUnits="userSpaceOnUse"><path d="M0 0L3 3M3 0L0 3" stroke="rgba(255,255,255,.16)" stroke-width=".3"/></pattern></defs>
            <rect x="0" y="0" width="120" height="64" class="goalsvg__bg"/><rect x="${gx(1)}" y="${gy(1)}" width="${XS * 2}" height="${YS}" fill="url(#net)"/>
            ${kick ? `<g class="gdots">${gridDots}</g>` : ''}
            <path d="M${gx(1)} ${YG}V${gy(1)}H${gx(-1)}V${YG}" class="goalsvg__frame"/><path d="M4 ${YG}H116" class="goalsvg__line"/>
            <g id="keeper" class="keeper"><rect x="-3.2" y="-13" width="6.4" height="13" rx="2.2"/><circle cy="-15.2" r="2.7"/><path d="M-3 -11L-9 -6M3 -11L9 -6" /></g>
            <g id="ball"><circle r="2.6" class="ball"/><path d="M-1.6 -.4l1.6-1.4 1.6 1.4-.6 1.8h-2z" fill="#0b1226"/></g>
            ${kick ? '<g id="ret" class="ret"><circle r="3.2"/><path d="M-5 0H5M0 -5V5"/></g>' : ''}
          </svg>
          <p class="aimtext" id="aimtext" aria-live="polite"></p>
        </div>
        <div class="duel__ctl">
          ${kick ? `<fieldset class="power"><legend>Potencia</legend><div class="segment" role="radiogroup">${POWERS.map(([l, v]) => `<button type="button" role="radio" data-power="${v}" aria-checked="${v === aim.power}">${l}</button>`).join('')}</div></fieldset>`
                 : `<fieldset class="power"><legend>Tu salto</legend><div class="segment" role="radiogroup">${[['Izquierda', 1], ['Centro', 0], ['Derecha', -1]].map(([l, v]) => `<button type="button" role="radio" data-dive="${v}" aria-checked="${v === dive}">${l}</button>`).join('')}</div></fieldset>`}
          <button class="btn btn--primary btn--lg" id="shoot">${kick ? '¡Lanzar!' : '¡Lanzarse!'}</button>
          <p class="field-note">Resultado calculado por el Core con la física real del balón, el marco de la portería y la estirada del portero. La rejilla de la web es una discretización: la jugada se redondea a la celda más cercana.</p>
        </div>
      </div>
      <div class="result" id="result" hidden role="status"></div></div>`;
    document.body.appendChild(sheet); document.body.classList.add('has-sheet');
    const $q = (s) => sheet.querySelector(s), $$q = (s) => Array.from(sheet.querySelectorAll(s));
    const goal = $q('#goal'), ret = $q('#ret'), ball = $q('#ball'), keeperEl = $q('#keeper');
    const place = (el, x, y, s = 1) => el.setAttribute('transform', `translate(${x} ${y}) scale(${s})`);
    const spot = () => place(ball, 60, 58, 1.5); const keeperHome = () => place(keeperEl, 60, YG);
    spot(); keeperHome();
    const text = () => { $q('#aimtext').textContent = kick ? 'Apuntas: ' + describeAim(aim) + ' · potencia ' + POWERS[nearest(POWERS.map((p) => p[1]), aim.power)][0].toLowerCase() : 'Te lanzas: ' + (dive > 0 ? 'a la izquierda' : dive < 0 ? 'a la derecha' : 'te quedas en el centro'); if (ret) place(ret, gx(aim.lateral), gy(aim.height)); };
    text();
    const setAimFromPoint = (clientX, clientY) => {
      const r = goal.getBoundingClientRect(), x = (clientX - r.left) / r.width * 120, y = (clientY - r.top) / r.height * 64;
      const lat = (X0 - x) / XS, h = (YG - y) / YS;
      aim.lateral = grid.lateral[nearest(grid.lateral, clamp(lat, -1.2, 1.2))]; aim.height = grid.height[nearest(grid.height, clamp(h, 0, 1.2))]; text();
    };
    if (kick) {
      goal.addEventListener('pointerdown', (e) => { if (busy) return; goal.setPointerCapture?.(e.pointerId); setAimFromPoint(e.clientX, e.clientY); fx.click(); });
      goal.addEventListener('pointermove', (e) => { if (!busy && e.buttons) setAimFromPoint(e.clientX, e.clientY); });
      goal.addEventListener('keydown', (e) => {
        if (busy) return; const li = grid.lateral.indexOf(aim.lateral), hi = grid.height.indexOf(aim.height);
        if (e.key === 'ArrowLeft') aim.lateral = grid.lateral[Math.min(grid.lateral.length - 1, li + 1)];
        else if (e.key === 'ArrowRight') aim.lateral = grid.lateral[Math.max(0, li - 1)];
        else if (e.key === 'ArrowUp') aim.height = grid.height[Math.min(grid.height.length - 1, hi + 1)];
        else if (e.key === 'ArrowDown') aim.height = grid.height[Math.max(0, hi - 1)]; else return;
        e.preventDefault(); text();
      });
    }
    $$q('[data-taker]').forEach((b) => b.addEventListener('click', () => { if (busy || locked) return; takerIndex = Number(b.dataset.taker); $$q('[data-taker]').forEach((x) => x.setAttribute('aria-checked', String(x === b))); fx.click(); }));
    $$q('[data-power]').forEach((b) => b.addEventListener('click', () => { if (busy) return; aim.power = Number(b.dataset.power); $$q('[data-power]').forEach((x) => x.setAttribute('aria-checked', String(x === b))); text(); fx.click(); }));
    $$q('[data-dive]').forEach((b) => b.addEventListener('click', () => { if (busy) return; dive = Number(b.dataset.dive); $$q('[data-dive]').forEach((x) => x.setAttribute('aria-checked', String(x === b))); text(); fx.click(); }));

    const close = (value) => { if (finished) return; finished = true; sheet.remove(); document.body.classList.remove('has-sheet'); document.removeEventListener('keydown', onKey); resolve(value); };
    const onKey = (e) => { if (e.key === 'Escape' && o.allowCancel !== false && !busy) { e.preventDefault(); close(null); } };
    document.addEventListener('keydown', onKey);
    const x = $q('[data-x]'); if (x) x.addEventListener('click', () => { if (!busy) close(null); });

    const tween = (el, to, ms, s = 1) => new Promise((done) => {
      if (settings.reduceMotion || ms <= 0) { place(el, to[0], to[1], s); return done(); }
      const m = /translate\(([-\d.]+) ([-\d.]+)\)/.exec(el.getAttribute('transform') || ''); const from = m ? [Number(m[1]), Number(m[2])] : to; const t0 = performance.now();
      const step = (t) => { const k = clamp((t - t0) / ms, 0, 1), e = 1 - (1 - k) * (1 - k); place(el, from[0] + (to[0] - from[0]) * e, from[1] + (to[1] - from[1]) * e - Math.sin(k * Math.PI) * 4, s - (s - 0.7) * e); k < 1 ? requestAnimationFrame(step) : done(); };
      requestAnimationFrame(step);
    });

    $q('#shoot').addEventListener('click', async () => {
      if (!take()) return;                                  // un doble toque no lanza dos veces
      busy = true; $q('#shoot').disabled = true; fx.whistle();
      const taker = takers[takerIndex]; let finalAim = aim, finalDive = dive;
      if (kick) { const k = aiKeeper(ai, rnd(), rnd(), rnd()); finalDive = grid.dive[nearest(grid.dive, k.lateral)]; }
      else { const k = aiKick(ai, taker.shotClose, rnd(), rnd(), rnd()); finalAim = { lateral: k.lateral, height: k.height, power: k.power }; }
      const result = lookup(lab, attack.id, defend.id, takerIndex, finalAim, finalDive);
      if (!result) { close(null); return; }
      await new Promise((r) => setTimeout(r, settings.reduceMotion ? 0 : 500));
      fx.kick();
      const end = [gx(clamp(result.lateral, -1.4, 1.4)), gy(clamp(result.height, 0, 1.3))];
      await Promise.all([tween(keeperEl, [gx(finalDive) , YG - (result.code === 'S' ? 2 : 0)], 520), tween(ball, end, 520, 0.7)]);
      if (ret && kick) { /* el retículo se queda donde estaba */ }
      if (result.code === 'G') fx.goal(); else if (result.code === 'S') fx.save(); else if (result.code === 'P' || result.code === 'B') fx.frame(); else fx.miss();
      const box = $q('#result'); box.hidden = false; box.className = 'result result--' + (result.good ? 'good' : 'bad');
      const who = kick ? takers[takerIndex].p.name : taker.p.name;
      const detail = result.code === 'G' ? `${esc(who)} bate a ${esc(keeper.name)}.` : result.code === 'S' ? `${esc(keeper.name)} la detiene.` : result.code === 'P' ? 'El balón da en el poste.' : result.code === 'B' ? 'El balón da en el larguero.' : result.code === 'M' ? 'El tiro se va fuera.' : '';
      box.innerHTML = `<b class="result__big">${esc(result.label)}</b><span>${detail}</span><small>Tiro: ${esc(describeAim(finalAim))} · salto del portero: ${finalDive > 0 ? 'izquierda' : finalDive < 0 ? 'derecha' : 'centro'}. Resuelto por el Core.</small><button class="btn btn--primary" id="next" autofocus>${esc(o.nextLabel || 'Continuar')}</button>`;
      $q('#next').addEventListener('click', () => close({ takerIndex, takerId: taker.id, takerName: taker.p.name, aim: finalAim, dive: finalDive, result, kick }));
      $q('#next').focus();
    });
    sheet.querySelector('#shoot').focus();
  });
}
