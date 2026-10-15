// Checks the single-file offline game (Client/dist-offline/FS27-jugable.html) the way a person meets it: opened from disk (file://), no server, no network:
// the menu appears, a match starts with real touch input, a whole match is played through to the end form, the form takes ratings and produces the text to send.
// Usage: node Client/test/offline.mjs [path to FS27-jugable.html] [screenshot dir]. Needs a Chromium and playwright-core like smoke.mjs.
import fs from 'node:fs'; import path from 'node:path';
const file = path.resolve(process.argv[2] || 'Client/dist-offline/FS27-jugable.html'), shots = process.argv[3] || '/tmp';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || 'playwright-core');
const exe = process.env.CHROME || fs.readdirSync(process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers').filter(d => d.startsWith('chromium-')).map(d => path.join(process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers', d, 'chrome-linux', 'chrome'))[0];
const fail = []; const check = (name, ok, detail) => { console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail !== undefined ? '  ' + JSON.stringify(detail) : '')); if (!ok) fail.push(name); };
const browser = await chromium.launch({ executablePath: exe, args: ['--no-sandbox'] });
const ctx = await browser.newContext({ viewport: { width: 844, height: 390 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
const page = await ctx.newPage(); const errors = [];
page.on('pageerror', e => errors.push(e.stack || String(e))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
let requests = 0; page.on('request', r => { const u = r.url(); if (!u.startsWith('file:') && !u.startsWith('blob:') && !u.startsWith('data:')) requests++; });
const t0 = Date.now();
await page.goto('file://' + file);
await page.waitForSelector('#menu:not(.hidden)', { timeout: 60000 });
check('the menu appears from disk, without a server', true, { seconds: +((Date.now() - t0) / 1000).toFixed(1) });
check('12 teams to choose from', (await page.evaluate(() => document.querySelectorAll('#homeSel option').length)) === 12);
check('the instructions are on the menu', (await page.evaluate(() => document.querySelector('.how').innerText)).includes('Disparar'));
await page.screenshot({ path: path.join(shots, 'offline-menu.png') });
// settings: opened from the menu, applied at once, kept on the device
await page.tap('#settingsBtn'); await page.waitForSelector('#settings:not(.hidden)');
await page.check('#setLefty'); await page.fill('#setZoom', '1.3'); await page.evaluate(() => { const e = document.getElementById('setCam'); e.value = 'wide'; e.dispatchEvent(new Event('change')); });
check('the settings apply at once (left-handed layout)', await page.evaluate(() => document.body.classList.contains('lefty')));
check('the settings are kept on the device', await page.evaluate(() => JSON.parse(localStorage.getItem('fs27.settings')).cam === 'wide'));
await page.tap('#settingsClose');
await page.evaluate(() => { const e = document.getElementById('setLefty'); });
await page.tap('#startBtn');
await page.waitForFunction(() => window.__fs27 && window.__fs27.snap && window.__fs27.snap.length > 13 && window.__fs27.state()[1] === 2, null, { timeout: 30000 });
check('a match starts with a tap on JUGAR', true);
check('the HUD tells who has the ball and the stamina', (await page.textContent('#possTxt')).length > 0 && (await page.textContent('#stamTxt')).length > 0);
check('a hint explains the controls', await page.evaluate(() => !document.getElementById('hintbar').classList.contains('hidden')));
await page.screenshot({ path: path.join(shots, 'offline-play.png') });
// the person crosses with the controls he already has: on the flank with the ball, the button A says CENTRO and a swipe up on it crosses (Phase 36)
{
  const cdp = await ctx.newCDPSession(page);
  const touch = (type, x, y) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints: type === 'touchEnd' ? [] : [{ x, y, id: 7 }] });
  const swipeUpOnA = async (px) => { const r = await page.evaluate(() => { const b = document.getElementById('btnA').getBoundingClientRect(); return { x: b.left + b.width / 2, y: b.top + b.height / 2 }; }); await touch('touchStart', r.x, r.y); await touch('touchMove', r.x, r.y - px / 2); await touch('touchMove', r.x, r.y - px); await touch('touchEnd', r.x, r.y - px); };
  const results = [];
  for (const [label, px, style] of [['short', 45, 'Ground'], ['medium', 95, 'Driven'], ['long', 150, 'High']]) {
    const setup = await page.evaluate(() => window.__fs27.B.TestSetup('cross'));
    await page.waitForTimeout(400);
    const can = await page.evaluate(() => window.__fs27.B.CanCross()); const lbl = await page.textContent('#btnA');
    if (label === 'short') check('on the flank with the ball the button A says CENTRO', setup === 'ok' && can === true && lbl === 'CENTRO', { setup, can, lbl });
    await swipeUpOnA(px); await page.waitForTimeout(150);
    const last = await page.evaluate(() => window.__fs27.B.LastAction());
    let apex = 0; for (let i = 0; i < 12; i++) { apex = Math.max(apex, await page.evaluate(() => window.__fs27.state()[7])); await page.waitForTimeout(100); }
    results.push({ label, last, apex: +apex.toFixed(2) });
  }
  check('a swipe up on A crosses (a real Cross action of the Core)', results.every(r => r.last.startsWith('Cross')), results);
  const [g, d, h] = results.map(r => r.apex);
  check('short, medium and long swipes give a ground, a driven and a high cross', g < 0.5 && d > g && h > d && h > 2.0, { ground: g, driven: d, high: h });
  await page.evaluate(async () => { const B = window.__fs27.B; for (let i = 0; i < 200; i++) { B.Advance(8 / 60); } });
}
// losing focus (a call, a notification) releases the stick and pauses the match (FS27-REQ-0053)
await page.evaluate(() => window.dispatchEvent(new Event('blur')));
await page.waitForTimeout(300);
check('losing focus pauses the match and leaves nothing pressed', (await page.textContent('#banner')) === 'PAUSA');
await page.waitForTimeout(100);
check('the pause panel is shown', await page.evaluate(() => !document.getElementById('pausebox').classList.contains('hidden')));
await page.tap('#btnPause'); await page.waitForTimeout(200);
check('the pause button resumes it', (await page.textContent('#banner')) !== 'PAUSA');
// a throw-in or corner of the person's team waits for him: aim, charge with the button, release (Phase 33)
const found = await page.evaluate(async () => { const B = window.__fs27.B; for (let i = 0; i < 20000; i++) { B.Advance(8 / 60); if (B.SetPieceInfo()[0] !== 0) return B.SetPieceInfo(); if (i % 400 === 0) await new Promise(r => setTimeout(r, 0)); } return null; });
check('the game waits for the person at his own throw-in or corner', found !== null, found);
if (found) {
  await page.waitForTimeout(300);
  check('the aim panel is shown', await page.evaluate(() => !document.getElementById('spbox').classList.contains('hidden')));
  await page.screenshot({ path: path.join(shots, 'offline-setpiece.png') });
  await page.keyboard.down('k'); await page.waitForTimeout(700);
  const mid = await page.evaluate(() => parseInt(document.getElementById('spfill').style.width));
  check('holding the button fills the charge bar', mid > 20 && mid < 100, { percent: mid });
  const before = await page.evaluate(() => window.__fs27.state()[1]);
  await page.keyboard.up('k'); await page.waitForTimeout(500);
  const after = await page.evaluate(() => ({ phase: window.__fs27.state()[1], info: Array.from(window.__fs27.B.SetPieceInfo()) }));
  check('releasing takes the throw-in/corner once and play resumes', after.info[0] === 0 && after.phase === 2, { before, after });
}
// play the whole match fast (the page's own loop keeps running; this only adds Core time), then the end form must come up
await page.evaluate(async () => { const B = window.__fs27.B; for (let i = 0; i < 14000 && B.Snapshot()[11] === 0; i++) { B.Advance(8 / 60); if (i % 400 === 0) await new Promise(r => setTimeout(r, 0)); } });
await page.waitForSelector('#end:not(.hidden)', { timeout: 60000 });
check('the end form appears', true, { title: await page.textContent('#endTitle'), score: await page.textContent('#endScore') });
check('the final statistics are shown', (await page.evaluate(() => document.querySelectorAll('#endStats tr').length)) >= 7);
check('six questions to rate', (await page.evaluate(() => document.querySelectorAll('#rates .rate').length)) === 6);
await page.tap('#rates .stars[data-k="pass"] button[data-n="4"]'); await page.tap('#rates .stars[data-k="fun"] button[data-n="5"]');
await page.fill('#notes', 'prueba automática');
await page.tap('#copyBtn');
const report = await page.inputValue('#report');
check('the text to send has the match, the ratings and the comment', report.includes('Partido:') && report.includes('Pases: 4') && report.includes('Diversión en general: 5') && report.includes('prueba automática') && report.includes('sin contestar'), report);
await page.screenshot({ path: path.join(shots, 'offline-end.png') });
// the practice: a second visit, five guided steps done by really doing them (FS27-REQ-0057)
const p2 = await ctx.newPage(); await p2.goto('file://' + file); await p2.waitForSelector('#menu:not(.hidden)', { timeout: 60000 });
check('the profile line remembers the finished match', (await p2.textContent('#profileLine')).includes('Tus partidos: 1'));
await p2.evaluate(() => { const e = document.getElementById('levelSel'); e.value = 'Elite'; e.dispatchEvent(new Event('change')); });
await p2.reload(); await p2.waitForSelector('#menu:not(.hidden)', { timeout: 60000 });
check('the menu keeps the options the person chose when he comes back (FS27-REQ-0659)', (await p2.evaluate(() => document.getElementById('levelSel').value)) === 'Elite');
check('the buttons have a label and a hint (FS27-REQ-0658)', (await p2.evaluate(() => ['btnA', 'btnB', 'btnC', 'btnSw'].every(id => document.getElementById(id).getAttribute('aria-label') && document.getElementById(id).title))));
check('the online mode is shown as not available yet (FS27-REQ-0886)', (await p2.evaluate(() => document.getElementById('onlineBtn').getAttribute('aria-disabled') === 'true')));
await p2.tap('#practiceBtn'); await p2.waitForFunction(() => window.__fs27 && window.__fs27.state()[1] === 2, null, { timeout: 30000 });
check('the practice shows its first step', (await p2.textContent('#tuTitle')).startsWith('1/5'));
await p2.keyboard.down('d'); await p2.waitForTimeout(2200); await p2.keyboard.up('d'); await p2.waitForTimeout(300);
check('moving advances the practice to the second step', (await p2.textContent('#tuTitle')).startsWith('2/5'), await p2.textContent('#tuTitle'));
await p2.close();
check('nothing was fetched from the network', requests === 0, { requests });
check('no page errors', errors.length === 0, errors.slice(0, 3));
await browser.close();
if (fail.length) { console.log('FAILED: ' + fail.join(', ')); process.exit(1); }
console.log('offline game: all checks passed');
