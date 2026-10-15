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
  const setLanguage=async value=>{await page.locator('#language-toggle').click();await page.locator('#language-menu [data-language="'+value+'"]').click();};
  const setMode=async value=>{for(let index=0;index<3;index++){if(await page.locator('#theme-toggle').getAttribute('data-mode')===value)return;await page.locator('#theme-toggle').click();}if(await page.locator('#theme-toggle').getAttribute('data-mode')!==value)throw Error('Theme cycle failed');};
  const setPalette=async value=>{await page.locator('button[data-palette="'+value+'"]').click();};
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(standalone.url);
    assert.ok(await page.locator('.win').evaluate(element => parseFloat(getComputedStyle(element).borderTopWidth) >= 1));
    assert.equal(await page.locator('.t-ctrl button').count(), 3);
    await page.locator('button[onclick="openDrawer()"]').click();
    const palettes = await page.locator('button[data-palette]').evaluateAll(options => options.map(option => option.dataset.palette));
    assert.equal(palettes.length, 8);
    assert.equal(await page.locator('.titlebar select').count(), 0);
    assert.equal(await page.locator('.switch:disabled').count(), 2);
    for (const language of ['zh-CN', 'en-US']) {
      await setLanguage(language);
      for (const palette of palettes) {
        await setPalette(palette);
        for (const mode of ['Light', 'Dark']) {
          await setMode(mode);
          assert.deepEqual(await page.evaluate(() => [document.documentElement.dataset.palette, document.documentElement.dataset.mode]), [palette, mode]);
        }
      }
      await page.locator('#about-open').click();
      await page.waitForFunction(expected => document.getElementById('about-title').textContent === expected, language === 'zh-CN' ? '关于童壁' : 'About KidWall');
      assert.equal(await page.locator('#update-cancel').isVisible(), false);
      assert.equal(await page.locator('#about-build-time').textContent(), '2026-10-15 00:00:00');
      assert.ok(await page.locator('.about-window').evaluate(element => parseFloat(getComputedStyle(element).borderTopWidth) >= 1));
      assert.ok(await page.locator('.about-brand img').evaluate(image => image.complete && image.naturalWidth > 0));
      await page.locator('.about-window').screenshot({ path: path.join(output, `about-${language}.png`) });
      await page.locator('#about-close').click();
    }
    await page.locator('#about-open').click();
    await page.locator('#update-action').click();
    await page.waitForFunction(() => document.getElementById('update-status').textContent.includes('1.0.9'));
    await page.locator('#update-action').click();
    await page.waitForFunction(() => document.getElementById('update-action').textContent === 'Install update');
    await page.locator('#update-action').click();
    assert.equal(await page.locator('#update-status').textContent(), 'Open download folder');
    await page.locator('#about-close').click();
    await setLanguage('zh-CN');
    await setPalette('DotNet');
    await page.locator('button[onclick="closeDrawer()"]').first().click();
    await page.evaluate(() => { localStorage.removeItem('kidwall-gallery'); favorites.clear();currentId=null;renderCats();renderGrid(); });
    await page.evaluate(() => openModal(2));
    assert.equal(await page.locator('#stageImg .badge').count(), 0, 'Decorative effects must not imply dynamic media');
    await page.locator('#previewFavorite').click();
    assert.equal(await page.locator('#modal').evaluate(element=>element.classList.contains('show')),true);
    assert.match(await page.locator('#previewFavorite').textContent(),/已收藏/);
    await page.locator('#previewApply').click();
    assert.equal(await page.locator('#grid .current-badge').count(),1);
    await page.reload();
    assert.equal(await page.locator('#grid .current-badge').count(),1,'Current state survives prototype reload');
    await page.locator('[data-cat="收藏"]').click();
    assert.equal(await page.locator('#grid .card').count(),1,'Favorite survives prototype reload');
    await page.evaluate(() => openModal(2));
    await page.locator('#previewFavorite').click();
    assert.equal(await page.locator('#grid .card').count(),0,'Removing favorite updates active filter');
    await page.locator('#modal .close').click();
    await page.locator('[data-cat="全部"]').click();
    await page.locator('button[onclick="openDrawer()"]').click();
    await setLanguage('en-US');
    await page.locator('button[onclick="closeDrawer()"]').first().click();
    await page.locator('#search').fill('Space');
    assert.equal(await page.locator('#grid .card').count(),3,'Search uses translated category names');
    await page.locator('#search').fill('');
    await page.evaluate(() => openModal(4));
    assert.equal(await page.locator('#stageImg .badge').count(),1);
    assert.equal(await page.locator('#stageImg .badge').innerText(),'Dynamic');
    const previewSize=await page.locator('#modal .stage').evaluate(element=>parseFloat(getComputedStyle(element).width));
    assert.equal(previewSize,920,'Preview uses the native stage width');
    await page.locator('#modal .close').click();
    await page.locator('button[onclick="openDrawer()"]').click();
    await setLanguage('zh-CN');
    await page.locator('button[onclick="closeDrawer()"]').first().click();
    assert.equal(await page.locator('#grid .badge').count(), await page.evaluate(() => WALLPAPERS.filter(item => item.cat === '动态').length));
    await page.locator('.win').screenshot({ path: path.join(output, 'gallery-dark.png'), animations: 'disabled' });
    assert.deepEqual(errors, []);
    await standalone.verify(page);
    assert.ok(await page.locator('#grid img').first().evaluate(image => image.complete && image.naturalWidth > 0));
    console.log('PASS KidWall prototype: two languages, three modes, eight palettes, About and update transitions.');
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
