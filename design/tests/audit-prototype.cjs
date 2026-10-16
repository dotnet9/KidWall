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
  async function checkAppearanceLanguage(language){
    const matches=await page.evaluate(expected=>{
      const words=KIDWALL_RESOURCES[expected].Localization.Appearance,mode=document.getElementById('theme-toggle'),languageButton=document.getElementById('language-toggle');
      return document.documentElement.lang===expected && document.querySelector('footer').textContent===KIDWALL_RESOURCES[expected].Localization.Main.Footer.Text && mode.title===words.Mode+': '+words[mode.dataset.mode] && mode.getAttribute('aria-label')===mode.title && languageButton.title===words.Language && languageButton.getAttribute('aria-label')===languageButton.title && [...document.querySelectorAll('button[data-palette]')].every(button=>button.title===words[button.dataset.palette] && button.getAttribute('aria-label')===button.title);
    },language);
    assert.ok(matches,'Title-bar language, tooltips and accessibility labels switch immediately: '+language);
  }
  const setLanguage=async value=>{await page.locator('#language-toggle').click();await page.locator('#language-menu [data-language="'+value+'"]').click();await checkAppearanceLanguage(value);};
  const setMode=async value=>{for(let index=0;index<3;index++){if(await page.locator('#theme-toggle').getAttribute('data-mode')===value){await checkAppearanceLanguage(await page.locator('html').getAttribute('lang'));return;}await page.locator('#theme-toggle').click();}if(await page.locator('#theme-toggle').getAttribute('data-mode')!==value)throw Error('Theme cycle failed');await checkAppearanceLanguage(await page.locator('html').getAttribute('lang'));};
  const setPalette=async value=>{await page.locator('button[data-palette="'+value+'"]').click();};
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(standalone.url);
    assert.ok(await page.locator('.win').evaluate(element => parseFloat(getComputedStyle(element).borderTopWidth) >= 1));
    assert.equal(await page.locator('.t-ctrl button').count(), 3);
    assert.ok(await page.locator('.titlebar').evaluate(element=>{
      const brand=element.querySelector('.t-app').getBoundingClientRect(),search=element.querySelector('.search').getBoundingClientRect(),appearance=element.querySelector('.title-appearance').getBoundingClientRect();
      return Math.abs(search.width-220)<1 && Math.abs((search.left+search.right)/2-(brand.right+appearance.left)/2)<2;
    }),'Search stays centered between the brand and appearance controls as in the native window');
    assert.deepEqual(await page.locator('.t-ctrl button').evaluateAll(buttons=>buttons.map(button=>button.getAttribute('onclick'))),['minWin(event)','maxWin(event)','closeWin(event)']);
    await page.locator('.t-ctrl button').nth(0).click();
    assert.ok(await page.locator('#win').evaluate(element=>element.classList.contains('min') && getComputedStyle(element).pointerEvents==='none'));
    await page.locator('#reopen').click();assert.ok(await page.locator('#win').evaluate(element=>!element.classList.contains('min') && !element.classList.contains('closed')));
    await page.locator('#maxBtn').click();
    await page.waitForFunction(()=>Math.abs(parseFloat(getComputedStyle(document.getElementById('win')).width)-innerWidth)<2);
    assert.ok(await page.locator('#win').evaluate(element=>element.classList.contains('full') && Math.abs(parseFloat(getComputedStyle(element).width)-innerWidth)<2));
    await page.locator('#maxBtn').click();await page.waitForFunction(()=>parseFloat(getComputedStyle(document.getElementById('win')).width)<innerWidth);assert.ok(await page.locator('#win').evaluate(element=>!element.classList.contains('full') && parseFloat(getComputedStyle(element).width)<innerWidth));
    await page.locator('.t-ctrl button').nth(2).click();
    assert.ok(await page.locator('#win').evaluate(element=>element.classList.contains('closed') && getComputedStyle(element).pointerEvents==='none'));
    await page.locator('#reopen').click();assert.ok(await page.locator('#win').evaluate(element=>!element.classList.contains('closed')));
    await page.locator('button[onclick="openDrawer()"]').click();
    const palettes = await page.locator('button[data-palette]').evaluateAll(options => options.map(option => option.dataset.palette));
    assert.equal(palettes.length, 8);
    assert.deepEqual(palettes,['DotNet','Aurora','Ember','Default','Aquatic','Desert','Dusk','NightSky']);
    const swatches=await page.locator('#theme-palettes button i').evaluateAll(dots=>dots.map(dot=>[getComputedStyle(dot).backgroundColor,getComputedStyle(dot).backgroundImage]));
    assert.equal(swatches[0][0],'rgb(81, 43, 212)');assert.ok(swatches[1][1].startsWith('linear-gradient('));
    assert.equal(await page.locator('.titlebar select').count(), 0);
    assert.equal(await page.locator('.switch:disabled').count(), 2);
    for (const language of ['zh-CN', 'en-US']) {
      await setLanguage(language);
      await setMode('System');
      for (const palette of palettes) {
        await setPalette(palette);
        for (const mode of ['Light', 'Dark']) {
          await setMode(mode);
          assert.deepEqual(await page.locator('#theme-palettes button i').evaluateAll(dots=>dots.map(dot=>[getComputedStyle(dot).backgroundColor,getComputedStyle(dot).backgroundImage])),swatches,'Palette swatches remain stable across modes');
          assert.deepEqual(await page.evaluate(() => [document.documentElement.dataset.palette, document.documentElement.dataset.mode]), [palette, mode]);
        }
      }
      await page.locator('#about-open').click();
      await page.waitForFunction(expected => document.getElementById('about-title').textContent === expected, language === 'zh-CN' ? '关于童壁' : 'About KidWall');
      assert.equal(await page.locator('#update-cancel').isVisible(), false);
      assert.equal(await page.locator('#about-build-time').textContent(), '2026-10-15 00:00:00');
      assert.ok(await page.locator('.about-window').evaluate(element => parseFloat(getComputedStyle(element).borderTopWidth) >= 1));
      assert.equal(await page.locator('.about-controls button').count(),3);
      assert.equal(await page.locator('[data-about-action="minimize"]').isDisabled(),true);assert.equal(await page.locator('[data-about-action="maximize"]').isDisabled(),true);
      assert.ok(await page.locator('.about-brand img').evaluate(image => image.complete && image.naturalWidth > 0));
      await page.locator('.about-window').screenshot({ path: path.join(output, `about-${language}.png`) });
      await page.locator('#about-close').click();
      assert.equal(await page.locator('#about-overlay').evaluate(element=>element.classList.contains('show')),false);
    }
    await page.locator('#about-open').click();
    // 更新示例跟随当前原型版本递增，避免发版后仍等待旧版本文本。
    const nextVersion = (await page.locator('.about-details dd').first().textContent()).trim().split('.').map(Number);
    nextVersion[nextVersion.length - 1] += 1;
    const expectedAvailable = await page.evaluate(version => KIDWALL_RESOURCES['en-US'].Localization.About.AboutAvailable.replace('{0}', version), nextVersion.join('.'));
    await page.locator('#update-action').click();
    await page.waitForFunction(expected => document.getElementById('update-status').textContent === expected, expectedAvailable);
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
    await setMode('Dark');
    await page.locator('.win').screenshot({ path: path.join(output, 'gallery-dark.png'), animations: 'disabled' });
    assert.deepEqual(errors, []);
    await standalone.verify(page);
    assert.ok(await page.locator('#grid img').first().evaluate(image => image.complete && image.naturalWidth > 0));
    console.log('PASS KidWall prototype: two languages, three modes, eight palettes, About and update transitions.');
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
