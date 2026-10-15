// End-to-end check of the browser client in a real (headless) Chromium with touch emulation: the page loads the real Core in WebAssembly, a scripted "player" plays with real touch events on the on-screen
// stick and buttons, and we measure that it runs in real time. Usage: node Client/test/smoke.mjs <dist dir> [screenshot.png]
// Needs a Chromium (PLAYWRIGHT_BROWSERS_PATH or CHROME) and playwright-core (PLAYWRIGHT_CORE=/path/to/playwright-core/index.mjs); neither is a dependency of the game.
import { spawn } from 'node:child_process'; import fs from 'node:fs'; import path from 'node:path';
const dist = path.resolve(process.argv[2] || 'Client/dist'), shot = process.argv[3] || '/tmp/fs27-play.png', port = 8123;
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || 'playwright-core');
const here = path.dirname(new URL(import.meta.url).pathname);
const server = spawn('node', [path.join(here, 'serve.mjs'), dist, String(port)], { stdio: 'ignore' }); await new Promise(r => setTimeout(r, 500));
const results = {}; const fail = [];
const check = (name, ok, detail) => { results[name] = { ok, detail }; if (!ok) fail.push(name); console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail !== undefined ? '  ' + JSON.stringify(detail) : '')); };
const exe = process.env.CHROME || fs.readdirSync(process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers').filter(d => d.startsWith('chromium-')).map(d => path.join(process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers', d, 'chrome-linux', 'chrome'))[0];
const browser = await chromium.launch({ executablePath: exe, args: ['--no-sandbox'] });
const ctx = await browser.newContext({ viewport: { width: 844, height: 390 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
const page = await ctx.newPage(); const errors = [];
page.on('pageerror', e => errors.push(String(e))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
const t0 = Date.now();
await page.goto(`http://localhost:${port}/?auto=1&home=0&away=1&seed=7&mode=Classic&level=Professional`);
await page.waitForFunction(() => window.__fs27 && window.__fs27.snap && window.__fs27.snap.length > 13, null, { timeout: 180000 });
check('loads the real Core and starts a match', true, { loadSeconds: +((Date.now() - t0) / 1000).toFixed(1), databaseProblems: await page.evaluate(() => window.__fs27.problems || '') });
check('database loaded without problems', (await page.evaluate(() => window.__fs27.problems)) === '');
const roster = await page.evaluate(() => window.__fs27.roster());
const onPitch0 = await page.evaluate(() => { const s = window.__fs27.state(), n = window.__fs27.roster().players.length; let c = 0; for (let i = 0; i < n; i++) if (s[13 + i * 8 + 5] === 0) c++; return c; });
check('12 players on the pitch (6 per team, bench kept off), 2 keepers in the squads', onPitch0 === 12 && roster.players.filter(p => p.gk).length >= 2, { onPitch: onPitch0, squad: roster.players.length });

// real-time capacity while idle (the AI plays both teams): steps per real second must reach the Core's 60
await page.waitForTimeout(6000);
const s1 = await page.evaluate(() => ({ ...window.__fs27.stats }));
check('real time: >= 55 Core steps per second', s1.sps >= 55, { fps: s1.fps, stepsPerSecond: s1.sps, coreMsPerFrame: +s1.msAdv.toFixed(2), snapshotMs: +s1.msSnap.toFixed(2), drawMs: +s1.msDraw.toFixed(2) });

// touch helpers (CDP touch events produce the pointer events the page listens to)
const cdp = await ctx.newCDPSession(page);
const touch = (type, x, y, id = 1) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints: type === 'touchEnd' ? [] : [{ x, y, id }] });
const center = async sel => page.evaluate(s => { const r = document.querySelector(s).getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2, w: r.width }; }, sel);
const snap = () => page.evaluate(() => Array.from(window.__fs27.state()));
const H = 13, PF = 8;
// wait for play
await page.waitForFunction(() => window.__fs27.state()[1] === 2, null, { timeout: 30000 });
let a = await snap(); const ci = a[9];
check('a player is controlled', ci >= 0, { controlledIndex: ci });

// stick: drag right for 1.5 s -> the controlled player moves along +X in the field (the Core's frame; the page mirrors the view when the sides switch)
const st = await center('#stick');
let s0 = await snap(), c0 = s0[9], p0 = { x: s0[H + c0 * PF], y: s0[H + c0 * PF + 1] };
await touch('touchStart', st.x, st.y); await touch('touchMove', st.x + st.w * 0.45, st.y); await page.waitForTimeout(1500);
let s2 = await snap(); const c2 = s2[9], dirSign = s2[12] ? 1 : -1;
const moved = c2 === c0 ? (s2[H + c0 * PF] - p0.x) * dirSign : NaN;
await touch('touchEnd');
check('the stick moves the controlled player (screen right = attacking direction)', Number.isNaN(moved) || moved > 1.5, { metresAlongAttack: +moved.toFixed(2), sameControlled: c2 === c0 });

// a scripted person plays 45 s with touch events: run to the ball, pass (B tap), shoot (A swipe down) near goal
let actions = {}, gotBall = 0; const tEnd = Date.now() + 45000; const A = await center('#btnA'), Bc = await center('#btnB');
while (Date.now() < tEnd) {
  const s = await snap(); if (s[1] !== 2) { await page.waitForTimeout(100); continue; }
  const ci2 = s[9], b = H + ci2 * PF, px = s[b], py = s[b + 1], hasBall = s[b + 3] === 1, bx = s[6], bz = s[8], dir = s[12] ? 1 : -1;
  const tx = hasBall ? 20 * dir : bx, ty = hasBall ? 0 : bz; let dx = (tx - px) * dir, dy = (ty - py) * dir; const m = Math.hypot(dx, dy) || 1; dx /= m; dy /= m;
  await touch('touchMove', st.x + dx * st.w * 0.45, st.y + dy * st.w * 0.45).catch(() => {});
  if (!(await page.evaluate(() => 0))) { /* keep the stick down */ }
  await touch('touchStart', st.x + dx * st.w * 0.45, st.y + dy * st.w * 0.45, 1).catch(() => {});
  if (hasBall) {
    gotBall++;
    const nearGoal = Math.abs(20 * dir - px) < 10;
    if (nearGoal) { await touch('touchStart', A.x, A.y, 2); await touch('touchMove', A.x, A.y + 70, 2); await touch('touchEnd', 0, 0, 2); }
    else { await touch('touchStart', Bc.x, Bc.y, 2); await touch('touchEnd', 0, 0, 2); }
    await page.waitForTimeout(150);
    const la = await page.evaluate(() => window.__fs27.B.LastAction()); if (la) actions[la] = (actions[la] || 0) + 1;
  }
  await page.waitForTimeout(120);
}
const end = await snap(); const bt = await page.evaluate(() => ({ ...window.__fs27.stats }));
check('touch buttons reach the Core (actions performed by the person)', Object.keys(actions).length > 0, actions);
check('the person had the ball at least once', gotBall > 0, { framesWithBall: gotBall });
check('still real time while playing', bt.sps >= 55, { fps: bt.fps, stepsPerSecond: bt.sps, worstFrameMs: +bt.worstFrameMs.toFixed(1) });
check('match clock advances', end[0] > 40, { matchSeconds: +end[0].toFixed(1), score: end[2] + '-' + end[3] });
await page.screenshot({ path: shot });

// capacity: how much faster than real time the Core can run on this machine (a phone is slower than this)
const cap = await page.evaluate(() => { const B = window.__fs27.B; const t = performance.now(); let steps = 0; for (let i = 0; i < 20; i++) steps += B.Advance(10); return { steps, ms: performance.now() - t }; });
const x = cap.steps / (cap.ms / 1000) / 60;
check('capacity: Core runs >= 3x faster than real time on this machine', x >= 3, { speedRelativeToRealTime: +x.toFixed(1), coreStepsPerSecond: Math.round(cap.steps / (cap.ms / 1000)) });
// a complete match (nobody touches the controls): it must reach the final whistle through half-time, restarts and celebrations
const full = await page.evaluate(() => { const B = window.__fs27.B; B.NewMatch(2, 3, 11, 'Classic', 'Professional'); const t = performance.now(); const phases = new Set(); let worstWait = 0, wait = 0, finished = false, s, steps = 0;
  for (let i = 0; i < 400000 && !finished; i++) { steps += B.Advance(8 / 60); s = B.Snapshot(); phases.add(s[1]); if (s[1] !== 2) wait += 8 / 60; else { worstWait = Math.max(worstWait, wait); wait = 0; } finished = s[11] === 1; }
  return { finished, matchSeconds: +s[0].toFixed(1), score: s[2] + '-' + s[3], phases: [...phases], worstNonPlayingSeconds: +worstWait.toFixed(1), wallSeconds: +((performance.now() - t) / 1000).toFixed(1), steps, checksum: B.Checksum() }; });
check('a complete match reaches the final whistle', full.finished, full);
if (process.env.EXPECT_CHECKSUM) check('determinism: the WebAssembly match ends in exactly the state of the same match played natively', full.checksum === process.env.EXPECT_CHECKSUM, { wasm: full.checksum, native: process.env.EXPECT_CHECKSUM, steps: full.steps });
check('no pause lasts more than 20 s of match time (no stuck restart)', full.worstNonPlayingSeconds <= 20, { worst: full.worstNonPlayingSeconds });
check('no page errors', errors.length === 0, errors.slice(0, 3));
await browser.close(); server.kill();
fs.writeFileSync(process.env.SMOKE_OUT || '/tmp/fs27-smoke.json', JSON.stringify(results, null, 1));
console.log(fail.length ? 'FAILED: ' + fail.join(', ') : 'ALL PASSED');
process.exit(fail.length ? 1 : 0);
