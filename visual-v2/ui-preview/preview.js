// Renders screenshots of visual v2's UI files (unity/Resources/UI) over the town render, in a portrait and a landscape phone, the way the
// game stacks its screens: every UXML document is a full-screen layer, filled with sample data the way the C# views fill them (texts by
// element name, cloned items, state classes, icons). An approximation of UI Toolkit (see uitk.js), for looking without Unity.
// Run: node visual-v2/ui-preview/preview.js [scenario ...]   Output: visual-v2/ui-preview/shots/<scenario>.png
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const { BASE_CSS, uxmlToHtml, ussToCss } = require('./uitk');

const UI = path.join(__dirname, '..', 'unity', 'Resources', 'UI');
const ICONS = path.join(UI, 'Icons');
const OUT = path.join(__dirname, 'shots');
const TOWN = path.join(__dirname, '..', 'previews', 'town_closeup.jpg');

// The screens in the order the game stacks them (later ones on top).
const LAYERS = ['ConnectionStatus', 'Hud', 'Toasts', 'PlacementBar', 'BuildMenu', 'Research', 'Inspector', 'ConnectionBanner', 'WelcomeBack'];

// The file URL of an icon by name.
function icon(name) {
  return `url("file://${path.join(ICONS, name + '.png')}")`;
}

// The HTML of a cloned item layout (HudChip, BuildCard, ResearchNode), as the views clone them.
function template(name) {
  return uxmlToHtml(path.join(UI, name + '.uxml')).html;
}

// The page: fonts, UI Toolkit defaults, every stylesheet, and each screen as a full-size layer of the phone.
function page(width, height) {
  const css = new Set();
  let layers = '';
  for (const name of LAYERS) {
    const { html, styles } = uxmlToHtml(path.join(UI, name + '.uxml'));
    styles.forEach((s) => css.add(s));
    layers += `<div class="ve doc" data-doc="${name}">${html}</div>`;
  }
  const fonts = ['Fredoka-SemiBold', 'Nunito-Bold', 'Nunito-ExtraBold']
    .map((f) => `@font-face { font-family: '${f}'; src: url("file://${path.join(UI, 'Fonts', f + '.ttf')}"); }`).join('\n');
  const sheets = [...css].map((f) => ussToCss(f)).join('\n');
  return `<!doctype html><html><head><meta charset="utf-8"><style>${fonts}\n${BASE_CSS}\n${sheets}
    body { margin: 0; background: #222; }
    #phone { position: relative; width: ${width}px; height: ${height}px; overflow: hidden;
      background: url("file://${TOWN}") center / cover; }
    .doc { position: absolute; left: 0; top: 0; right: 0; bottom: 0; }
  </style></head><body><div id="phone">${layers}</div></body></html>`;
}

// Sample data and the scenarios: each fills the screens like the views would and opens what it shows.
const FILL = String.raw`
window.$ = (name, root) => (root || document).querySelector('[data-name="' + name + '"]');
window.text = (name, value, root) => { const e = $(name, root); if (e) { e.textContent = value; e.style.display = value ? '' : 'none'; } };
window.pic = (name, url, root) => { const e = $(name, root); if (e) e.style.backgroundImage = url; };
window.add = (parent, html) => { const t = document.createElement('div'); t.innerHTML = html; const el = t.firstElementChild; parent.appendChild(el); return el; };
window.fillHud = (T, I) => {
  const chips = $('hud-chips');
  [['item_coins', '85', '', '+300/h', 'up'], ['item_logs', '14', '500', '0/h', ''], ['item_planks', '1', '500', '-60/h', 'down']]
    .forEach(([ic, stock, cap, rate, trend]) => {
      const c = add(chips, T.chip);
      const i = $('hud-chip-icon', c); i.classList.add('hud-chip-icon--picture'); i.style.backgroundImage = I(ic); i.textContent = '';
      text('hud-chip-stock', stock, c); text('hud-chip-cap', cap, c); text('hud-chip-rate', rate, c);
      if (trend) $('hud-chip-rate', c).classList.add('hud-rate--' + (trend === 'up' ? 'rising' : 'falling'));
    });
  pic('hud-workers-icon', I('workers')); pic('hud-builders-icon', I('builders')); pic('hud-ring-icon', I('res_sharp_axes'));
  text('hud-workers', '1 / 8'); text('hud-builders', '1 / 2'); text('hud-research-name', 'Sharpened Axes'); text('hud-research-time', '35% · 19:30');
  text('hud-era', 'Early Middle Ages'); text('hud-connection-text', 'Online'); $('hud-connection-dot').classList.add('hud-dot--ok');
  $('hud-ring').style.background = 'conic-gradient(#F2C230 126deg, rgba(255,246,227,0.25) 0deg)'; $('hud-ring').style.borderRadius = '50%';
};
window.fillBuild = (T, I, open) => {
  const list = $('build-cards').querySelector('[data-content]');
  [['bld_house', 'Cottage', '50 coins', '30 s', 'Homes for 4 workers', '', ''],
   ['bld_woodcutter', "Woodcutter's Hut", '80 coins', '1 m', 'Up to 2 workers', 'Makes 1 log per cycle', ''],
   ['bld_sawmill', 'Sawmill', '150 coins, 30 logs', '2 m', 'Up to 3 workers', '2 logs → 1 plank', 'Need 16 more logs'],
   ['bld_trading_post', 'Trading Post', '200 coins, 20 planks', '3 m', 'Up to 2 workers', '2 planks → 10 coins', 'Research Trade Charter first']]
    .forEach(([ic, name, cost, time, workers, recipe, hint], k) => {
      const c = add(list, T.card);
      pic('card-icon', I(ic), c); text('card-name', name, c); text('card-cost', cost, c); text('card-time', time, c);
      text('card-workers', workers, c); text('card-recipe', recipe, c); text('card-lock', k === 3 ? 'Locked' : '', c); text('card-hint', hint, c);
      if (k === 3) { c.classList.add('build-card--locked'); c.classList.add('disabled'); }
      if (k === 1) c.classList.add('build-card--selected');
    });
  if (open) { $('build-panel').classList.remove('build-panel--hidden'); text('build-toggle', 'Close'); }
};
window.fillResearch = (T, I, open) => {
  const cols = $('research-columns');
  const row = add(cols, '<div class="ve research-row"></div>');
  [['res_trade_charter', 'Trade Charter', 'Unlocks the Trading Post', '150 coins', '10 m', 'Done', '', 'done'],
   ['res_sharp_axes', 'Sharpened Axes', 'Woodcutters work 20% faster', '250 coins', '30 m', '35% · 19:30 left', '', 'running'],
   ['res_storehouses', 'Storehouses', '+250 storage for logs and planks', '300 coins, 50 logs', '45 m', '', 'Starts when the slot is free', 'available'],
   ['res_frame_saw', 'Frame Saw', 'Sawmills work 20% faster', '400 coins, 30 planks', '1 h', 'Needs Sharpened Axes', '', 'locked']]
    .forEach(([ic, name, effect, cost, time, status, note, state]) => {
      const n = add(row, T.node);
      pic('research-node-icon', I(ic), n); text('research-node-name', name, n); text('research-node-effect', effect, n);
      text('research-node-cost', cost, n); text('research-node-time', time, n); text('research-node-status', status, n); text('research-node-note', note, n);
      n.classList.add('research-node--' + state);
      $('research-node-finance', n).style.display = state === 'available' ? '' : 'none';
      $('research-node-ring', n).style.display = state === 'running' ? '' : 'none';
      if (state === 'running') { const r = $('research-node-ring', n); r.style.background = 'conic-gradient(#F2C230 126deg, rgba(122,78,45,0.25) 0deg)'; r.style.borderRadius = '50%'; }
    });
  text('research-era', 'Era I · Early Middle Ages'); text('research-rule', 'One research at a time');
  if (open) { $('research-panel').classList.remove('research-panel--hidden'); text('research-toggle', 'Close'); }
};
window.fillInspector = (I) => {
  $('inspector-panel').classList.remove('inspector-panel--hidden');
  pic('inspector-icon', I('bld_sawmill')); text('inspector-name', 'Sawmill'); text('inspector-level', 'Level 1');
  text('inspector-status', 'Working'); text('inspector-time', 'Next plank in 0:42'); text('inspector-recipe', '2 logs → 1 plank every 60 s');
  text('inspector-rates', 'Makes 60 planks/h, uses 120 logs/h'); text('inspector-workers', '3 / 3');
  text('inspector-preview', 'With 2 workers: 40 planks/h'); text('inspector-stepper-note', '');
  $('inspector-plus').classList.add('disabled');
  text('inspector-upgrade-title', 'Level 2'); text('inspector-upgrade-cost', '250 coins, 30 planks'); text('inspector-upgrade-time', 'Takes 3 m');
  text('inspector-upgrade-gain', 'Up to 6 workers'); text('inspector-upgrade-note', 'Need 29 more planks'); $('inspector-upgrade').classList.add('disabled');
};
window.fillWelcome = () => {
  $('welcome-scrim').classList.remove('welcome-scrim--hidden');
  text('welcome-title', 'Welcome back!');
  const s = $('welcome-sections');
  [['welcome-heading', 'While you were away · 7 h 12 m'], ['welcome-line', 'Made 864 logs, 432 planks and 2,160 coins'],
   ['welcome-line', 'Used 864 logs and 432 planks'], ['welcome-heading', 'Done'], ['welcome-line', 'Research: Trade Charter'],
   ['welcome-line', 'Built: Cottage']].forEach(([cls, t]) => { const l = add(s, '<div class="ve unity-label unity-text-element"></div>'); l.classList.add(cls); l.textContent = t; });
  text('welcome-empty', ''); text('welcome-cap-note', 'Production pauses after 8 hours away.');
};
window.fillToasts = (items) => {
  const list = $('toast-list');
  items.forEach(([t, kind]) => { const l = add(list, '<div class="ve unity-label unity-text-element toast"></div>'); l.classList.add('toast--' + kind); l.textContent = t; });
};
window.fillPlacement = () => {
  $('placement-panel').classList.remove('placement-panel--hidden');
  text('placement-prompt', "Tap a free tile for the Woodcutter's Hut"); text('placement-hint', 'This tile is taken'); text('placement-error', '');
  $('placement-place').classList.add('disabled');
};
window.fillBanner = () => {
  $('connection-banner').classList.remove('connection-banner--hidden');
  text('connection-message', 'No connection: your town may be out of date'); text('connection-synced', 'Last synced 2 min ago');
};
window.finish = () => {
  document.querySelectorAll('.ve').forEach((e) => { if (getComputedStyle(e).getPropertyValue('--glyph').trim()) e.classList.add('glyphed'); });
};
`;

const SCENARIOS = {
  city: (portrait) => `fillHud(T, I); fillBuild(T, I, false); fillResearch(T, I, false); fillToasts([['Cottage placed', 'success']]);`,
  build: () => `fillHud(T, I); fillBuild(T, I, true); fillResearch(T, I, false);`,
  inspector: () => `fillHud(T, I); fillBuild(T, I, false); fillResearch(T, I, false); fillInspector(I);`,
  research: () => `fillHud(T, I); fillBuild(T, I, false); fillResearch(T, I, true);`,
  welcome: () => `fillHud(T, I); fillBuild(T, I, false); fillResearch(T, I, false); fillWelcome();`,
  placement: () => `fillHud(T, I); fillBuild(T, I, false); fillResearch(T, I, false); fillPlacement(); fillToasts([['No free workers: build a Cottage', 'error']]);`,
  offline: () => `fillHud(T, I); fillBuild(T, I, false); fillResearch(T, I, false); fillBanner();`,
};

// Renders every scenario (or the named ones) in portrait (400 x 866, the screen roots carry "portrait") and landscape (866 x 400).
async function main() {
  const wanted = process.argv.slice(2);
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await chromium.launch({ args: ['--allow-file-access-from-files'] });
  for (const [portrait, w, h] of [[true, 400, 866], [false, 866, 400]]) {
    const html = page(w, h);
    const file = path.join(OUT, portrait ? '_portrait.html' : '_landscape.html');
    fs.writeFileSync(file, html);
    for (const [name, script] of Object.entries(SCENARIOS)) {
      if (wanted.length && !wanted.includes(name)) continue;
      const tab = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
      await tab.goto('file://' + file);
      await tab.evaluate(FILL);
      const templates = { chip: template('HudChip'), card: template('BuildCard'), node: template('ResearchNode') };
      await tab.evaluate(({ portraitMode, templates: T, icons, body }) => {
        if (portraitMode) document.querySelectorAll('.doc').forEach((r) => r.classList.add('portrait'));
        // The CSS url() of an icon by name, as UiIcons finds it.
        const I = (n) => `url("file://${icons}/${n}.png")`;
        new Function('T', 'I', body)(T, I);
        window.finish();
      }, { portraitMode: portrait, templates, icons: ICONS, body: script(portrait) });
      await tab.evaluate(() => document.fonts.ready);
      await tab.screenshot({ path: path.join(OUT, `${name}-${portrait ? 'portrait' : 'landscape'}.png`) });
      await tab.close();
    }
  }
  await browser.close();
  console.log('shots in', OUT);
}

main();
