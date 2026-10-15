const standaloneFixture = require('./standalone-fixture.cjs');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const output = path.resolve(process.env.KIDWALL_AUDIT_OUTPUT || path.join(__dirname, 'artifacts'));
  fs.mkdirSync(output, { recursive: true });
  const standalone = standaloneFixture(path.resolve(__dirname, '../index.html'));
  const browser = await chromium.launch(process.env.CI ? {} : { channel: 'msedge' });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    await standalone.attach(page);
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(standalone.url);
    await page.locator('button[onclick="openDrawer()"]').click();
    const palettes = await page.locator('#theme-palette option').evaluateAll(options => options.map(option => option.value));
    assert.equal(palettes.length, 8);
    assert.equal(await page.locator('#theme-mode option').count(), 3);
    assert.equal(await page.locator('.switch:disabled').count(), 2);
    for (const language of ['zh-CN', 'en-US']) {
      await page.selectOption('#language', language);
      for (const palette of palettes) {
        await page.selectOption('#theme-palette', palette);
        for (const mode of ['Light', 'Dark']) {
          await page.selectOption('#theme-mode', mode);
          assert.deepEqual(await page.evaluate(() => [document.documentElement.dataset.palette, document.documentElement.dataset.mode]), [palette, mode]);
        }
      }
      await page.locator('#about-open').click();
      await page.waitForFunction(expected => document.getElementById('about-title').textContent === expected, language === 'zh-CN' ? '关于童壁' : 'About KidWall');
      assert.equal(await page.locator('#update-cancel').isVisible(), false);
      assert.ok(await page.locator('.about-brand img').evaluate(image => image.complete && image.naturalWidth > 0));
      await page.locator('.about-window').screenshot({ path: path.join(output, `about-${language}.png`) });
      await page.locator('#about-close').click();
    }
    await page.locator('#about-open').click();
    await page.locator('#update-action').click();
    await page.waitForFunction(() => document.getElementById('update-status').textContent.includes('1.0.8'));
    await page.locator('#update-action').click();
    await page.waitForFunction(() => document.getElementById('update-action').textContent === 'Install update');
    await page.locator('#update-action').click();
    assert.equal(await page.locator('#update-status').textContent(), 'Open download folder');
    await page.locator('#about-close').click();
    await page.selectOption('#language', 'zh-CN');
    await page.selectOption('#theme-palette', 'DotNet');
    await page.locator('button[onclick="closeDrawer()"]').first().click();
    assert.equal(await page.locator('#grid .badge').count(), await page.evaluate(() => WALLPAPERS.filter(item => item.cat === '动态').length));
    await page.locator('.win').screenshot({ path: path.join(output, 'gallery-dark.png'), animations: 'disabled' });
    assert.deepEqual(errors, []);
    await standalone.verify(page);
    assert.ok(await page.locator('#grid img').first().evaluate(image => image.complete && image.naturalWidth > 0));
    console.log('PASS KidWall prototype: two languages, three modes, eight palettes, About and update transitions.');
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
