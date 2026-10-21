// The one list of repository files that become handbook pages. The docs config, the sidebar check and the
// repo-links remark plugin all read it, so they always agree (website/README.md → Files).
const {execFileSync} = require('child_process');
const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '..');

// Whole folders: every Markdown file below them is a page.
const pageFolders = ['core-instructions', 'docs'];

// Single files outside those folders.
const pageFiles = [
  'README.md',
  'AGENTS.md',
  'CLAUDE.md',
  'CONTRIBUTING.md',
  'CHANGELOG.md',
  'client/README.md',
  'server/README.md',
  'shared/README.md',
  'tools/README.md',
  'website/README.md',
];

// Templates are scaffolding for new files, not pages; links to them go to GitHub.
const isExcluded = (relPath) => path.posix.basename(relPath) === 'TEMPLATE.md';

function walk(relDir) {
  const absDir = path.join(repoRoot, relDir);
  if (!fs.existsSync(absDir)) return [];
  return fs
    .readdirSync(absDir, {withFileTypes: true})
    .sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))
    .flatMap((entry) => {
      const rel = path.posix.join(relDir, entry.name);
      if (entry.isDirectory()) return walk(rel);
      return entry.isFile() && entry.name.endsWith('.md') ? [rel] : [];
    });
}

/** Repository-relative paths (forward slashes) of every page, sorted. */
function listPages() {
  const files = [
    ...pageFolders.flatMap(walk),
    ...pageFiles.filter((rel) => fs.existsSync(path.join(repoRoot, rel))),
  ];
  return [...new Set(files)].filter((rel) => !isExcluded(rel)).sort();
}

/** The doc id Docusaurus gives a page: its path without `.md` (number prefixes are kept). */
const docId = (relPath) => relPath.replace(/\.md$/, '');

/**
 * Files and folders that git tracks, so links go only to things that exist on GitHub (not to ignored or untracked
 * local files). Returns null outside a git checkout; callers then fall back to the file system.
 */
function listTracked() {
  let output;
  try {
    output = execFileSync('git', ['ls-files', '-z'], {cwd: repoRoot, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024});
  } catch {
    return null;
  }
  const files = new Set(output.split('\0').filter(Boolean));
  const dirs = new Set();
  for (const file of files) {
    const parts = file.split('/');
    for (let i = 1; i < parts.length; i += 1) dirs.add(parts.slice(0, i).join('/'));
  }
  return {files, dirs};
}

module.exports = {repoRoot, listPages, docId, listTracked};
