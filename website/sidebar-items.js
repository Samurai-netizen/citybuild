// Labels and order for the sidebar: short labels from each page's first heading, folder listings without their README
// (it is the category's own page), and the build log newest first. Used by sidebars.js and the docs config.
const fs = require('fs');
const path = require('path');
const {repoRoot, docId} = require('./docs-set');

// A page's first level-1 heading without backticks, or its path when it has none.
function pageTitle(relPath) {
  const text = fs.readFileSync(path.join(repoRoot, relPath), 'utf8');
  const heading = /^# (.+)$/m.exec(text);
  return (heading ? heading[1] : relPath).replace(/`/g, '').trim();
}

/** The sidebar label of a page: `04 · Simulation Model`, `ADR-0005 · Settle on access`, `M0-18 · Spec sync and ADRs`. */
function shortLabel(relPath) {
  const title = pageTitle(relPath);
  const spec = /^core-instructions\/(\d{2})-/.exec(relPath);
  if (spec) return `${spec[1]} · ${title.split(' — ')[0].replace(/\s*\(.*\)$/, '')}`;
  const record = /^((?:ADR|CR)-\d{4}) — (.+)$/.exec(title);
  if (record) return `${record[1]} · ${record[2].split(/[:;]/)[0]}`;
  const logEntry = /^BL-\d{8}-\d{2} · (\S+) — (.+)$/.exec(title);
  if (logEntry) return `${logEntry[1]} · ${logEntry[2]}`;
  return title;
}

// Whether a folder is the build log or one of its year folders.
const isBuildLog = (dirName) => dirName === 'docs/build-log' || dirName.startsWith('docs/build-log/');

// The items with their short labels, folders included; reversed at every level when newestFirst (the build log's newest entry first).
function relabel(items, newestFirst) {
  const result = items.map((item) => {
    if (item.type === 'doc') return {...item, label: shortLabel(`${item.id}.md`)};
    if (item.type === 'category') return {...item, items: relabel(item.items, newestFirst)};
    return item;
  });
  return newestFirst ? result.reverse() : result;
}

/** Docusaurus `sidebarItemsGenerator`: the default listing, relabelled, without the folder's README, build log reversed. */
async function sidebarItemsGenerator({defaultSidebarItemsGenerator, ...args}) {
  const items = await defaultSidebarItemsGenerator(args);
  const dirName = args.item.dirName;
  const readmeId = docId(`${dirName}/README.md`);
  const withoutReadme = items.filter((item) => !(item.type === 'doc' && item.id === readmeId));
  return relabel(withoutReadme, isBuildLog(dirName));
}

module.exports = {shortLabel, sidebarItemsGenerator};
