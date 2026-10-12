// Recorrido de extremo a extremo con Playwright (opcional; no es dependencia del proyecto): node scripts/e2e.mjs [url] [carpeta-de-capturas]
// Requiere Playwright instalado globalmente (npm i -g playwright) y Chromium. Comprueba rutas, flujos de penal, tanda, VAR, entrenamiento, tienda, cartera y ajustes, en escritorio y móvil.
import { execSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { join } from 'node:path';
import assert from 'node:assert/strict';

const url = process.argv[2] || 'http://localhost:5173/', out = process.argv[3] || '/tmp/shots';
mkdirSync(out, { recursive: true });
const root = execSync('npm root -g').toString().trim();
const { chromium } = await import(pathToFileURL(join(root, 'playwright', 'index.mjs')).href);
const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM || '/opt/pw-browsers/chromium' });
const report = []; let failures = 0;
const step = async (name, fn) => { try { await fn(); report.push('ok   ' + name); } catch (e) { failures++; report.push('FAIL ' + name + ' -> ' + (e.message || e).toString().split('\n')[0]); } };

for (const [label, vp, mobile] of [['desktop', { width: 1366, height: 768 }, false], ['mobile', { width: 390, height: 844 }, true]]) {
  const ctx = await browser.newContext({ viewport: vp, isMobile: mobile, hasTouch: mobile, reducedMotion: 'reduce' });
  const p = await ctx.newPage(); const errs = [];
  p.on('pageerror', (e) => errs.push('pageerror ' + e)); p.on('console', (m) => m.type() === 'error' && errs.push('console ' + m.text()));
  p.on('requestfailed', (r) => errs.push('requestfailed ' + r.url()));
  const shot = (n) => p.screenshot({ path: join(out, `${label}-${n}.png`), fullPage: true });
  const noOverflow = async () => assert.equal(await p.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false, 'desbordamiento horizontal');
  const go = async (hash) => { await p.goto(url + hash); await p.waitForTimeout(450); };

  await step(`${label}: menú con seis opciones`, async () => { await go(''); assert.equal(await p.locator('.tile').count(), 6); assert.ok(await p.locator('#zp-chip').innerText().then((t) => /10/.test(t)), 'saldo inicial 10 ZP'); await noOverflow(); await shot('menu'); });
  for (const r of ['arena', 'classic', 'equipo', 'entrenamiento', 'tienda', 'configuracion', 'cartera']) await step(`${label}: abre ${r} y vuelve`, async () => {
    await go(''); await p.click(r === 'cartera' ? '#zp-chip' : `[data-route="${r}"]`); await p.waitForSelector('#screen-title'); await p.waitForTimeout(700); await noOverflow(); await shot(r);
    await p.click('[data-back]'); await p.waitForTimeout(250); assert.ok(await p.locator('.menu').isVisible());
  });

  await step(`${label}: equipo, jugadores, filtros y tarjeta`, async () => {
    await go('#/equipo'); assert.equal(await p.locator('.tbig').count(), 12);
    await p.click('.tbig >> nth=0'); await p.waitForSelector('.lineup'); await shot('equipo-detalle'); await p.click('.prow >> nth=1'); await p.waitForSelector('dialog[open] .pcard'); await shot('tarjeta'); await p.keyboard.press('Escape');
    await go('#/equipo?tab=jugadores'); assert.ok((await p.locator('.pcard').count()) >= 24);
    await p.selectOption('select[name="role"]', 'GK'); await p.waitForTimeout(150); assert.equal(await p.locator('#cnt').innerText().then((t) => /24 jugadores/.test(t)), true);
    await p.selectOption('select[name="team"]', 'none'); await p.selectOption('select[name="role"]', ''); await p.waitForTimeout(150); assert.match(await p.locator('#cnt').innerText(), /40 jugadores/);
    await noOverflow(); await shot('jugadores');
  });

  await step(`${label}: preparación Classic y entrada al partido`, async () => {
    await go('#/classic'); await p.click('[data-mine="team-vortex-rapids"]'); await p.click('[data-rival="team-solaris-meridian"]'); await p.click('[data-diff="Elite"]'); await shot('classic-prep');
    await p.click('#go'); await p.waitForSelector('.pitchcv'); await p.waitForTimeout(800); await shot('partido-campo');
    await p.click('#play'); await p.waitForTimeout(1200); await p.click('#play'); assert.ok(await p.locator('.scoreboard').isVisible());
  });
  await step(`${label}: Arena no ofrece penal, tanda ni VAR`, async () => { await go('#/arena'); await p.click('#go'); await p.waitForSelector('.pitchcv'); await p.click('[data-tab="penal"]'); assert.match(await p.locator('#scene').innerText(), /Arena no tiene penales/); await p.click('[data-tab="var"]'); assert.match(await p.locator('#scene').innerText(), /Arena no tiene VAR/); await shot('arena-sin-var'); });

  await step(`${label}: penal en partido Classic (lanzar)`, async () => {
    await go('#/classic'); await p.click('#go'); await p.waitForSelector('.pitchcv'); await p.click('[data-tab="penal"]'); await p.click('#kick'); await p.waitForSelector('.sheet #goal'); await shot('penal-aim');
    await p.click('.sheet #goal', { position: { x: 60, y: 30 } }); await p.click('.sheet [data-power="1"]'); await p.dblclick('.sheet #shoot'); await p.waitForSelector('.sheet .result:not([hidden])', { timeout: 8000 }); await shot('penal-result');
    assert.match(await p.locator('.sheet .result').innerText(), /GOL|PARADA|POSTE|LARGUERO|FUERA/); await p.click('#next'); await p.waitForSelector('.sheet', { state: 'detached' });
  });
  await step(`${label}: tanda completa hasta decidirse`, async () => {
    await go('#/partido?tab=tanda'); await p.waitForSelector('#begin'); await shot('tanda-orden'); const first = await p.locator('.ord__name').first().innerText();
    await p.click('[data-down="0"]'); assert.notEqual(await p.locator('.ord__name').first().innerText(), first, 'el orden cambia'); await p.reload(); await p.waitForSelector('#begin'); await p.click('[data-tab="tanda"]').catch(() => {});
    assert.notEqual(await p.locator('.ord__name').first().innerText(), first, 'el orden se guarda');
    await p.click('#begin'); let guard = 0;
    while (guard++ < 40 && !(await p.locator('#rematch').count())) {
      await p.waitForSelector('.sheet #shoot', { timeout: 8000 }); if (await p.locator('.sheet [data-dive]').count()) await p.click('.sheet [data-dive="1"]'); else await p.click('.sheet #goal', { position: { x: 40 + (guard % 3) * 20, y: 20 } });
      await p.click('.sheet #shoot'); await p.waitForSelector('.sheet #next', { timeout: 8000 }); await p.click('#next'); await p.waitForTimeout(80);
    }
    await shot('tanda-final'); assert.match(await p.locator('.result').innerText(), /gana la tanda|empatada/); await noOverflow();
  });
  await step(`${label}: VAR cobra una vez y no cobra en rechazos`, async () => {
    await go('#/partido?tab=var'); await p.waitForSelector('.sc'); const bal0 = Number(await p.locator('#zp-chip b').innerText());
    await p.click('[data-sc="goal-confirmed"]'); await p.click('#ask'); await p.waitForSelector('dialog[open]'); await shot('var-confirm'); await p.click('dialog[open] button[value="ok"]'); await p.waitForSelector('.decide', { timeout: 6000 }); await shot('var-resultado');
    assert.equal(Number(await p.locator('#zp-chip b').innerText()), bal0 - 1, 'un ZP cobrado aunque se confirme'); assert.match(await p.locator('.result').innerText(), /confirmada/i);
    await p.click('#list'); await p.click('[data-sc="rejected-not-eligible"]'); await p.click('#ask'); await p.click('dialog[open] button[value="ok"]'); await p.waitForSelector('.result--bad'); assert.equal(Number(await p.locator('#zp-chip b').innerText()), bal0 - 1, 'sin cobro si se rechaza');
    await p.click('#back-list'); await p.click('[data-sc="duplicate-request"]'); await p.click('#ask'); await p.click('dialog[open] button[value="ok"]'); await p.waitForSelector('.decide', { timeout: 6000 }); assert.equal(Number(await p.locator('#zp-chip b').innerText()), bal0 - 2, 'el doble toque cobra una vez'); await noOverflow();
  });
  await step(`${label}: controles (joystick y A/B/C)`, async () => { await go('#/partido?tab=controles'); await p.waitForSelector('#stick'); await p.locator('#stick').scrollIntoViewIfNeeded(); const box = await p.locator('#stick').boundingBox(); await p.mouse.move(box.x + box.width / 2, box.y + box.height / 2); await p.mouse.down(); await p.mouse.move(box.x + box.width - 4, box.y + box.height / 2, { steps: 4 }); await p.waitForTimeout(500); assert.match(await p.locator('#mv').innerText(), /Esprintando|Corriendo/); await p.mouse.up(); await p.click('[data-b="A"]'); assert.match(await p.locator('#log').innerText(), /no conectado/); await shot('controles'); await noOverflow(); });
  await step(`${label}: entrenamiento da ZP una vez por sesión`, async () => {
    await go('#/entrenamiento'); const bal0 = Number(await p.locator('#zp-chip b').innerText()); await p.click('#start');
    for (let i = 0; i < 5; i++) { await p.waitForSelector('.sheet #goal'); await p.click('.sheet #goal', { position: { x: 30 + i * 12, y: 20 } }); await p.click('.sheet #shoot'); await p.waitForSelector('.sheet #next', { timeout: 8000 }); await p.click('#next'); await p.waitForTimeout(60); }
    await p.waitForSelector('.summary'); await shot('entrenamiento-fin'); assert.equal(Number(await p.locator('#zp-chip b').innerText()), bal0 + 2);
  });
  await step(`${label}: tienda compra, cobra y equipa`, async () => {
    await go('#/tienda'); const bal0 = Number(await p.locator('#zp-chip b').innerText()); await p.click('[data-buy="ball-neon"]'); await p.click('dialog[open] button[value="ok"]'); await p.waitForTimeout(300);
    assert.equal(Number(await p.locator('#zp-chip b').innerText()), bal0 - 3); assert.ok(await p.locator('[data-equip="ball-neon"]').count()); await shot('tienda'); await noOverflow();
    await go('#/tienda'); assert.equal(Number(await p.locator('#zp-chip b').innerText()), bal0 - 3, 'persiste al recargar');
  });
  await step(`${label}: cartera e historial coherentes`, async () => { await go('#/cartera'); const rows = await p.locator('.tbl tbody tr').count(); assert.ok(rows >= 5, 'historial con movimientos'); const bal = Number(await p.locator('#zp-chip b').innerText()); assert.ok(bal >= 0); await shot('cartera-final'); await noOverflow(); });
  await step(`${label}: ajustes se aplican y persisten`, async () => {
    await go('#/configuracion'); await p.locator('label[for=s-text]').click(); await p.locator('label[for=s-contrast]').click(); assert.equal(await p.evaluate(() => document.documentElement.dataset.text + document.documentElement.dataset.contrast), 'bighigh'); await shot('configuracion');
    await p.reload(); await p.waitForTimeout(300); assert.equal(await p.evaluate(() => document.documentElement.dataset.text), 'big'); await go('#/configuracion'); await p.locator('label[for=s-text]').click(); await p.locator('label[for=s-contrast]').click();
  });
  await step(`${label}: teclado y foco visible en el menú`, async () => { await go(''); await p.keyboard.press('ArrowRight'); await p.keyboard.press('ArrowRight'); const r = await p.evaluate(() => document.activeElement.dataset.route); assert.ok(r, 'un tile tiene el foco'); await p.keyboard.press('Enter'); await p.waitForSelector('#screen-title'); await p.keyboard.press('Escape'); await p.waitForTimeout(250); assert.ok(await p.locator('.menu').isVisible()); });
  await step(`${label}: sin errores de consola ni peticiones fallidas`, async () => assert.deepEqual(errs, []));
  await ctx.close();
}
await browser.close();
console.log(report.join('\n')); console.log(`\n${report.length - failures}/${report.length} correctos`); process.exit(failures ? 1 : 0);
