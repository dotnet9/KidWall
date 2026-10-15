/* 应用输出资源用于原型；这里只模拟更新状态，不访问系统安装器。 */
(() => {
  const resources = window.KIDWALL_RESOURCES;
  const flatten = (value, prefix = '', result = {}) => {
    for (const [key, item] of Object.entries(value)) {
      const path = prefix ? `${prefix}.${key}` : key;
      if (item && typeof item === 'object') flatten(item, path, result);
      else if (typeof item === 'string') result[path] = item;
    }
    return result;
  };
  const dictionaries = Object.fromEntries(Object.entries(resources).map(([key, value]) => [key, flatten(value)]));
  let language = 'zh-CN';
  const text = key => dictionaries[language][`Localization.${key}`] ?? key;
  const pairs = Object.entries(dictionaries['zh-CN']).filter(([key, value]) => key.startsWith('Localization.') && /[\u3400-\u9fff]/u.test(value) && !value.includes('{'))
    .sort((a, b) => b[1].length - a[1].length);
  const original = new WeakMap();
  function translateText(source) {
    if (language === 'zh-CN') return source;
    let result = source;
    for (const [key, value] of pairs) result = result.replaceAll(value, dictionaries['en-US'][key]);
    return result;
  }
  function localize() {
    document.documentElement.lang = language;
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, { acceptNode: node => node.parentElement.closest('script,style,[data-key]') ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT });
    while (walker.nextNode()) {
      const node = walker.currentNode;
      if (!original.has(node)) original.set(node, node.nodeValue);
      const value = translateText(original.get(node));
      if (node.nodeValue !== value) node.nodeValue = value;
    }
    document.querySelectorAll('[data-key]').forEach(element => { const value = text(element.dataset.key); if (element.textContent !== value) element.textContent = value; });
    document.querySelectorAll('[data-placeholder-key]').forEach(element => { element.placeholder = text(element.dataset.placeholderKey); });
  }
  const palettes = window.KIDWALL_PALETTES;
  const system = matchMedia('(prefers-color-scheme: dark)');
  function applyAppearance() {
    const mode = document.getElementById('theme-mode').value;
    const palette = document.getElementById('theme-palette').value;
    const dark = mode === 'Dark' || (mode === 'System' && system.matches);
    const colors = palettes[palette][dark ? 'Dark' : 'Light'];
    const root = document.documentElement;
    root.dataset.mode = mode; root.dataset.palette = palette; root.dataset.dark = String(dark);
    const values = { '--primary': colors.Primary, '--accent': colors.Accent, '--app-bg': colors.Background,
      '--surface': colors.Surface, '--ink': colors.Text, '--ink-2': colors.Muted,
      '--ink-3': colors.Muted, '--stroke': colors.Border, '--glass': colors.Raised };
    for (const [key, value] of Object.entries(values)) root.style.setProperty(key, value);
  }
  document.getElementById('theme-mode').addEventListener('change', applyAppearance);
  document.getElementById('theme-palette').addEventListener('change', applyAppearance);
  document.getElementById('language').addEventListener('change', event => { language = event.target.value; localize(); showUpdate(); });
  system.addEventListener('change', applyAppearance);
  document.getElementById('about-open').addEventListener('click', () => document.getElementById('about-overlay').classList.add('show'));
  document.getElementById('about-close').addEventListener('click', () => document.getElementById('about-overlay').classList.remove('show'));
  document.getElementById('about-overlay').addEventListener('click', event => { if (event.target.id === 'about-overlay') event.target.classList.remove('show'); });
  document.addEventListener('keydown', event => { if (event.key === 'Escape') document.getElementById('about-overlay').classList.remove('show'); });
  let updateState = 'Idle';
  let timer;
  const action = document.getElementById('update-action');
  function showUpdate() {
    document.getElementById('update-status').textContent = updateState === 'Available' ? text('About.AboutAvailable').replace('{0}', '1.0.8') : updateState === 'Downloaded' ? text('About.AboutDownloaded') : updateState === 'Canceled' ? text('About.AboutCanceled') : '';
    action.textContent = text(updateState === 'Checking' ? 'About.AboutChecking' : updateState === 'Available' ? 'About.UpdateDownload' : updateState === 'Downloading' ? 'About.AboutDownloading' : updateState === 'Downloaded' ? 'About.UpdateInstall' : 'About.UpdateCheck');
    action.disabled = ['Checking','Downloading'].includes(updateState);
    document.getElementById('update-cancel').hidden = !action.disabled;
    document.getElementById('update-progress').hidden = updateState !== 'Downloading';
  }
  action.addEventListener('click', () => {
    if (updateState === 'Downloaded') { document.getElementById('update-status').textContent = text('About.AboutOpenFolder'); return; }
    const checking = updateState !== 'Available';
    updateState = checking ? 'Checking' : 'Downloading'; showUpdate();
    timer = setTimeout(() => { updateState = checking ? 'Available' : 'Downloaded'; showUpdate(); }, 700);
  });
  document.getElementById('update-cancel').addEventListener('click', () => { clearTimeout(timer); updateState = 'Canceled'; showUpdate(); });
  new MutationObserver(() => localize()).observe(document.body, { childList:true, subtree:true, characterData:true });
  applyAppearance(); localize(); showUpdate();
})();
