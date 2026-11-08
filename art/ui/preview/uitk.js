// A small UXML and USS reader for the browser preview of visual v2's UI: turns a UXML layout into HTML elements and a USS stylesheet into
// CSS with UI Toolkit's defaults (every element a column flex box with border-box sizing). It is an approximation for looking at the
// layouts without Unity, never a test of Unity's own layout: text measure, wrapping and some properties differ.
const fs = require('fs');
const path = require('path');

// UI Toolkit's defaults for every element, a label, a button and a scroll view, written as CSS.
const BASE_CSS = `
.ve { display: flex; flex-direction: column; position: relative; box-sizing: border-box; flex-shrink: 1; flex-grow: 0;
  flex-basis: auto; min-width: 0; min-height: 0; border: 0 solid transparent; margin: 0; padding: 0; white-space: nowrap; }
.unity-label, .unity-button { font-size: 12px; }
.unity-button { margin: 1px 3px; padding: 1px 6px; border-width: 1px; border-radius: 3px; background-color: #e4e4e4;
  align-items: center; justify-content: center; text-align: center; }
.unity-scroll-view { overflow: hidden; }
.unity-scroll-view__content-container { flex-shrink: 0; }
.unity-scroll-view.horizontal > .unity-scroll-view__content-container { flex-direction: row; width: max-content; }
.ve.glyphed::after { content: ''; position: absolute; left: 0; top: 0; right: 0; bottom: 0; pointer-events: none;
  -webkit-mask-image: var(--glyph); -webkit-mask-repeat: no-repeat; -webkit-mask-position: var(--glyph-x, center) var(--glyph-y, center);
  -webkit-mask-size: var(--glyph-size, contain); background-color: var(--tint, #fff); }
`;

// Reads the attributes of an XML start tag into an object.
function attributes(tag) {
  const out = {};
  for (const m of tag.matchAll(/([\w:-]+)="([^"]*)"/g)) out[m[1]] = m[2];
  return out;
}

// Escapes text for HTML.
function escapeHtml(text) {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// Converts one UXML file to HTML and the list of its stylesheet paths. Elements keep their name (data-name) and classes.
function uxmlToHtml(file) {
  const text = fs.readFileSync(file, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
  const styles = [];
  let html = '';
  const stack = [];
  for (const m of text.matchAll(/<(\/?)([\w:.]+)([^>]*?)(\/?)>/g)) {
    const [, closing, tagName, rest, selfClosing] = m;
    const kind = tagName.replace(/^ui:/, '');
    if (kind === 'UXML') continue;
    if (kind === 'Style') {
      styles.push(path.join(path.dirname(file), attributes(rest).src));
      continue;
    }
    if (closing) {
      html += stack.pop();
      continue;
    }
    const a = attributes(rest);
    const classes = ['ve'];
    let close = '</div>';
    let inner = '';
    if (kind === 'Label') classes.push('unity-label', 'unity-text-element');
    if (kind === 'Button') classes.push('unity-button', 'unity-text-element');
    if (kind === 'ScrollView') {
      classes.push('unity-scroll-view', (a.mode || 'Vertical').toLowerCase());
      inner = '<div class="ve unity-scroll-view__content-container" data-content="1">';
      close = '</div></div>';
    }
    if (a.class) classes.push(...a.class.split(/\s+/));
    const name = a.name ? ` data-name="${a.name}"` : '';
    const label = a.text ? escapeHtml(a.text) : '';
    html += `<div class="${classes.join(' ')}"${name}>${inner}${label}`;
    if (selfClosing) html += close;
    else stack.push(close);
  }
  return { html, styles };
}

// Converts one value of a USS property (url() paths are made absolute file URLs).
function convertUrl(value, ussDir) {
  return value.replace(/url\("([^"]+)"\)/g, (_, p) => `url("file://${path.resolve(ussDir, p)}")`);
}

// The CSS declarations for one USS declaration.
function convertDeclaration(prop, value, ussDir) {
  value = convertUrl(value.trim(), ussDir);
  switch (prop) {
    case '-unity-font-definition':
    case '-unity-font': {
      const font = path.basename(value.replace(/^url\("|"\)$/g, ''), '.ttf');
      return `font-family: '${font}'`;
    }
    case '-unity-font-style':
      return value === 'bold' ? 'font-weight: bold' : value === 'italic' ? 'font-style: italic' : '';
    case '-unity-text-align': {
      const [v, h] = value.split('-');
      const vMap = { upper: 'flex-start', middle: 'center', lower: 'flex-end' };
      const hMap = { left: 'flex-start', center: 'center', right: 'flex-end' };
      return `justify-content: ${vMap[v]}; text-align: ${h}; --text-h: ${hMap[h]}`;
    }
    case '-unity-background-scale-mode':
      return value === 'scale-to-fit' ? 'background-size: contain; background-repeat: no-repeat; background-position: center'
        : value === 'scale-and-crop' ? 'background-size: cover; background-position: center' : 'background-size: 100% 100%';
    case '-unity-background-image-tint-color':
      return `--tint: ${value}`;
    case 'background-image':
      return /Glyphs\//.test(value) ? `--glyph: ${value}; background-image: none` : `background-image: ${value}`;
    case 'background-size':
      return `background-size: ${value}; --glyph-size: ${value}`;
    case 'background-position-x':
      return `background-position-x: ${value}; --glyph-x: ${value}`;
    case 'background-position-y':
      return `background-position-y: ${value}; --glyph-y: ${value}`;
    case '-unity-overflow-clip-box':
    case '-unity-paragraph-spacing':
    case '-unity-text-outline-width':
    case '-unity-text-outline-color':
      return '';
    default:
      return `${prop}: ${value}`;
  }
}

// Converts a USS stylesheet to CSS: comments dropped, :disabled read as the class "disabled", every declaration converted.
function ussToCss(file) {
  const dir = path.dirname(file);
  const text = fs.readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
  let css = '';
  for (const m of text.matchAll(/([^{}]+)\{([^}]*)\}/g)) {
    const selector = m[1].trim().replace(/:disabled/g, '.disabled').replace(/:root/g, ':root');
    const decls = m[2].split(';').map((d) => d.trim()).filter(Boolean).map((d) => {
      const i = d.indexOf(':');
      return convertDeclaration(d.slice(0, i).trim(), d.slice(i + 1), dir);
    }).filter(Boolean);
    css += `${selector} { ${decls.join('; ')} }\n`;
  }
  return css;
}

module.exports = { BASE_CSS, uxmlToHtml, ussToCss };
