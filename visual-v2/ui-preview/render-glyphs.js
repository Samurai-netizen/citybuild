// Renders the UI glyphs of visual v2 (the line shapes of the UI showcase) to white PNGs on a transparent background, 96 x 96, into
// visual-v2/unity/Resources/UI/Glyphs/. USS tints them (-unity-background-image-tint-color). Run: node visual-v2/ui-preview/render-glyphs.js
const path = require('path');
const fs = require('fs');
const zlib = require('zlib');
const { chromium } = require('playwright');

// Each glyph: its SVG content on a 24 x 24 grid, drawn with round 2.5 px strokes like the showcase's "Line glyphs".
const GLYPHS = {
  close: '<path d="M6 6l12 12M18 6L6 18"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  minus: '<path d="M5 12h14"/>',
  lock: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>',
  time: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  done: '<path d="M5 12.5l4.5 4.5L19 7"/>',
  makes: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  alert: '<path d="M12 6v8"/><circle cx="12" cy="18.5" r="0.6" fill="#fff"/>',
};

// Encodes RGBA pixels as a PNG (no filter, deflate), with every pixel white and only the alpha of `alpha` kept: a browser canvas
// cannot keep a colour in a fully transparent pixel, and a black one would pull a dark fringe in when Unity filters the glyph.
function whitePng(alpha, size) {
  const rows = [];
  for (let y = 0; y < size; y++) {
    const row = Buffer.alloc(1 + size * 4, 255);
    row[0] = 0;
    for (let x = 0; x < size; x++) {
      row[1 + x * 4 + 3] = alpha[(y * size + x) * 4 + 3];
    }
    rows.push(row);
  }
  // One PNG chunk: length, type, data, CRC of type and data.
  const chunk = (type, data) => {
    const head = Buffer.alloc(8);
    head.writeUInt32BE(data.length, 0);
    head.write(type, 4, 'ascii');
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(zlib.crc32(Buffer.concat([Buffer.from(type, 'ascii'), data])) >>> 0, 0);
    return Buffer.concat([head, data, crc]);
  };
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', ihdr),
    chunk('IDAT', zlib.deflateSync(Buffer.concat(rows), { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

// Writes one glyph: the SVG is drawn on a canvas in the page, its alpha read back and written as a white PNG.
async function renderGlyph(page, name, body, outDir) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 24 24" fill="none" stroke="#fff"
    stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">${body}</svg>`;
  const alpha = await page.evaluate(async (source) => {
    const img = new Image();
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(source);
    await img.decode();
    const canvas = document.createElement('canvas');
    canvas.width = 96;
    canvas.height = 96;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(img, 0, 0, 96, 96);
    return Array.from(ctx.getImageData(0, 0, 96, 96).data);
  }, svg);
  fs.writeFileSync(path.join(outDir, `${name}.png`), whitePng(alpha, 96));
}

// Renders every glyph.
async function main() {
  const outDir = path.join(__dirname, '..', 'unity', 'Resources', 'UI', 'Glyphs');
  fs.mkdirSync(outDir, { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 96, height: 96 }, deviceScaleFactor: 1 });
  for (const [name, body] of Object.entries(GLYPHS)) {
    await renderGlyph(page, name, body, outDir);
  }
  await browser.close();
  console.log(`${Object.keys(GLYPHS).length} glyphs in ${outDir}`);
}

main();
