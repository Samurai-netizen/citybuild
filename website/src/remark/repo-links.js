// Remark plugin: makes the repository's cross-references clickable without changing the Markdown files.
//  - `04`, `core-instructions/15` (spec numbers)      → that spec's page
//  - `docs/getting-started.md`, `13-progress.md`, `docs/` (backticked paths of pages) → the page
//  - `shared/Simulation/Runtime/IntegerMath.cs`, `.github/workflows/ci.yml` (other files git tracks) → the file on GitHub
//  - [text](../../scripts/dev/check-docs.sh) (Markdown links to files that are not pages) → the file on GitHub
// A backticked path that doesn't exist (a planned file) stays plain code. Page links are emitted as relative `.md` links, which
// Docusaurus then resolves to URLs (and fails the build if it can't). Details: website/README.md → "Links".
const fs = require('fs');
const path = require('path');

const SPEC_NUMBER = /^\d{2}$/;
const SPEC_SHORT_PATH = /^core-instructions\/(\d{2})$/;
const PATH_CHARS = /^[\w.\-/]+$/;
const HAS_EXTENSION = /\.[A-Za-z0-9]+$/;
const EXTERNAL = /^[a-z][a-z0-9+.-]*:/i; // http:, https:, mailto:, …
const NO_AUTOLINK_INSIDE = new Set(['link', 'linkReference', 'definition', 'code', 'html']);

// The plugin: its options come from docusaurus.config.js, and the transformer it returns rewrites the tree of each page.
module.exports = function repoLinks({repoRoot, pages, tracked, githubRepoUrl, branch}) {
  const pageSet = new Set(pages);
  const existsCache = new Map();
  // What a repository path is: 'file', 'dir' or null; from git's list of tracked files when given, else from the disk (cached).
  const exists = (rel) => {
    if (tracked) return tracked.files.has(rel) ? 'file' : tracked.dirs.has(rel) ? 'dir' : null;
    if (!existsCache.has(rel)) {
      const abs = path.join(repoRoot, rel);
      existsCache.set(rel, fs.existsSync(abs) ? (fs.statSync(abs).isDirectory() ? 'dir' : 'file') : null);
    }
    return existsCache.get(rel);
  };
  const specFiles = fs.readdirSync(path.join(repoRoot, 'core-instructions')).filter((n) => /^\d{2}-.*\.md$/.test(n));
  // The page of the spec with number nn (04 → core-instructions/04-….md), or null when there is none.
  const specPath = (nn) => {
    const name = specFiles.find((n) => n.startsWith(`${nn}-`));
    return name ? `core-instructions/${name}` : null;
  };

  // A repository-relative path → {page} for a handbook page, {github} for another existing file or folder, or null.
  function target(rel) {
    const clean = path.posix.normalize(rel).replace(/\/+$/, '');
    if (clean === '' || clean === '.' || clean.startsWith('..') || clean.includes('node_modules')) return null;
    if (pageSet.has(clean)) return {page: clean};
    const kind = exists(clean);
    if (kind === 'dir' && pageSet.has(`${clean}/README.md`)) return {page: `${clean}/README.md`};
    if (kind) return {github: `${githubRepoUrl}/${kind === 'dir' ? 'tree' : 'blob'}/${branch}/${clean}`};
    return null;
  }

  // The text of a code span → a target, or null when it isn't a reference to the repository.
  function resolveCode(value, sourceDir) {
    const text = value.trim();
    if (SPEC_NUMBER.test(text)) {
      const spec = specPath(text);
      return spec ? {page: spec} : null;
    }
    const short = SPEC_SHORT_PATH.exec(text);
    if (short) {
      const spec = specPath(short[1]);
      return spec ? {page: spec} : null;
    }
    if (!PATH_CHARS.test(text) || !/[A-Za-z0-9]/.test(text)) return null;
    // `/shared/` means the folder at the repository root.
    if (text.startsWith('/')) return target(text.replace(/^\/+/, ''));
    // A bare word (`docs`, `server`) is usually a job, folder or project name in prose, so only paths and file names link.
    if (!text.includes('/') && !HAS_EXTENSION.test(text)) return null;
    const candidates = [path.posix.join(sourceDir, text), text, path.posix.join('core-instructions', text)];
    for (const candidate of candidates) {
      const found = target(candidate);
      if (found) return found;
    }
    return null;
  }

  // A relative .md link from a folder to a page, with an optional #hash; Docusaurus turns it into the page's URL.
  const relativeLink = (fromDir, page, hash = '') => {
    const rel = path.posix.relative(fromDir, page);
    return (rel.startsWith('.') ? rel : `./${rel}`) + hash;
  };

  // Markdown links and definitions: leave page links to Docusaurus, send links to other repository files to GitHub.
  function rewriteUrl(node, sourceDir) {
    const url = node.url || '';
    if (url === '' || url.startsWith('#') || url.startsWith('/') || EXTERNAL.test(url)) return;
    const hashAt = url.indexOf('#');
    const pathPart = hashAt === -1 ? url : url.slice(0, hashAt);
    const hash = hashAt === -1 ? '' : url.slice(hashAt);
    let decoded;
    try {
      decoded = decodeURI(pathPart);
    } catch {
      return;
    }
    const found = target(path.posix.join(sourceDir, decoded));
    if (!found) return; // Docusaurus reports it as a broken link
    if (found.github) node.url = found.github + hash;
    else if (found.page !== path.posix.normalize(path.posix.join(sourceDir, decoded))) node.url = relativeLink(sourceDir, found.page, hash);
  }

  // Walks the tree: rewrites Markdown links and definitions, and turns code spans that name repository files into links
  // (not inside links, code or HTML).
  function visit(node, sourceDir) {
    if (!node.children) return;
    node.children.forEach((child, index) => {
      if (child.type === 'link' || child.type === 'definition') rewriteUrl(child, sourceDir);
      if (child.type === 'inlineCode') {
        const found = resolveCode(child.value, sourceDir);
        if (found) {
          node.children[index] = {
            type: 'link',
            url: found.github || relativeLink(sourceDir, found.page),
            children: [child],
          };
        }
        return;
      }
      if (!NO_AUTOLINK_INSIDE.has(child.type)) visit(child, sourceDir);
    });
  }

  return (tree, file) => {
    const source = path.relative(repoRoot, file.path).split(path.sep).join('/');
    visit(tree, path.posix.dirname(source));
  };
};
