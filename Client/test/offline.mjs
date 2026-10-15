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
await page.tap('#startBtn');
await page.waitForFunction(() => window.__fs27 && window.__fs27.snap && window.__fs27.snap.length > 13 && window.__fs27.state()[1] === 2, null, { timeout: 30000 });
check('a match starts with a tap on JUGAR', true);
check('a hint explains the controls', await page.evaluate(() => !document.getElementById('hintbar').classList.contains('hidden')));
await page.screenshot({ path: path.join(shots, 'offline-play.png') });
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
check('six questions to rate', (await page.evaluate(() => document.querySelectorAll('#rates .rate').length)) === 6);
await page.tap('#rates .stars[data-k="pass"] button[data-n="4"]'); await page.tap('#rates .stars[data-k="fun"] button[data-n="5"]');
await page.fill('#notes', 'prueba automática');
await page.tap('#copyBtn');
const report = await page.inputValue('#report');
check('the text to send has the match, the ratings and the comment', report.includes('Partido:') && report.includes('Pases: 4') && report.includes('Diversión en general: 5') && report.includes('prueba automática') && report.includes('sin contestar'), report);
await page.screenshot({ path: path.join(shots, 'offline-end.png') });
check('nothing was fetched from the network', requests === 0, { requests });
check('no page errors', errors.length === 0, errors.slice(0, 3));
await browser.close();
if (fail.length) { console.log('FAILED: ' + fail.join(', ')); process.exit(1); }
console.log('offline game: all checks passed');
