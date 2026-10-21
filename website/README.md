# The handbook site (`website/`)

The handbook is a [Docusaurus](https://docusaurus.io/) site that renders the repository's Markdown files **where they live**:
`core-instructions/`, `docs/`, the root files and the folder READMEs. Nothing is copied or generated into another folder, so a page and
its file are always the same text. The rules for writing pages are in `core-instructions/17-documentation-system.md` §7; this page
explains how the site works and how to change it.

## Run it
| What | Command |
|---|---|
| Install the pinned packages (first time, and after `package-lock.json` changes) | `bash scripts/dev/docs-site.sh install` |
| Build it as CI does (fails on broken links, anchors and sidebar gaps) | `bash scripts/dev/docs-site.sh build` |
| Preview while editing, with live reload: http://localhost:3000/citybuild/ | `bash scripts/dev/docs-site.sh start` |
| Serve the last build: http://localhost:3000/citybuild/ | `bash scripts/dev/docs-site.sh serve` |

Node.js 20 or newer is needed (CI uses 24). Without Bash, the same works with `npm --prefix website ci` and `npm --prefix website run build`.
The live preview reads the page list once at start: restart it after adding or removing a page. Search works only on a build
(`build`, then `serve`), not in the live preview.

## Files
| File | Job |
|---|---|
| `docs-set.js` | The one list of files that become pages: everything under `core-instructions/` and `docs/`, plus named single files. `TEMPLATE.md` files are not pages |
| `sidebars.js` | The book: chapters in reading order. Fails the build when a page is in no chapter, naming the page |
| `sidebar-items.js` | Sidebar labels (`04 · Simulation Model`, `ADR-0005 · …`, `M0-18 · …`), folder listings, build log newest first |
| `src/remark/repo-links.js` | Turns cross-references into links (below) |
| `src/components/PageKind/` | The line above each page that says what kind of page it is, and its source file |
| `src/theme/DocItem/Content/` | Puts that line on every page (a wrapped theme component) |
| `docusaurus.config.js` | Site settings: the docs folder (`..`), URLs, link checks, navbar, footer |
| `src/css/custom.css`, `static/img/` | Colours and the logo |

## URLs
A page's URL is its file path without `.md`, and a folder's `README.md` is the folder's page:
`core-instructions/04-simulation-model.md` → `/core-instructions/04-simulation-model/`, `docs/adr/README.md` → `/docs/adr/`
(`trailingSlash: true`: every page is a folder with an `index.html`, the shape GitHub Pages serves without redirects).
Number prefixes are kept on purpose (`numberPrefixParser: false`), so the path and the URL never disagree. Front matter (`slug:`, `id:`)
is not used: it would make the URL differ from the path.

## Links
Most cross-references in this repository are code spans, not Markdown links. The remark plugin makes them clickable on the site and
changes nothing in the files:

| In the Markdown | On the site |
|---|---|
| `` `04` ``, `` `core-instructions/15` `` (a spec number) | the spec's page |
| `` `docs/getting-started.md` ``, `` `13-progress.md` ``, `` `docs/` `` (a page, or a folder with a README) | that page |
| `` `shared/Simulation/Runtime/IntegerMath.cs` ``, `` `scripts/dev/` `` (any other file or folder) | the file on GitHub (`main`) |
| `[text](../../scripts/dev/check-docs.sh)` (a link to a file that is not a page) | the file on GitHub |
| `` `tools/DocTools` `` before it exists, `` `docs/modules/<Name>.md` `` (no such file) | plain code, no link |

A path is tried relative to the page's folder first, then from the repository root, then inside `core-instructions/`. A bare word
(`` `docs` ``, `` `server` ``) never links: in prose it is usually a CI job or a project name. Markdown links between pages work on
GitHub and on the site alike; Docusaurus turns them into page URLs and fails the build if one points nowhere.

## Page kinds
Every page starts with one line that says what it is (`kinds.js`, first match wins):

| Kind | Pages |
|---|---|
| Live status | `core-instructions/13-progress.md` |
| Generated reference | `docs/reference/generated/` (from M0-61) |
| History · append-only | build log entries; `CHANGELOG.md` |
| Decision record | ADRs, `core-instructions/12-decisions.md` |
| Change request | `docs/changes/CR-*.md` |
| Plan | `docs/plan/` |
| How-to guide | `docs/how-to/` |
| Specification · normative | the rest of `core-instructions/`, `AGENTS.md`, `CLAUDE.md` (protected ones say so) |
| As built · descriptive | everything else: architecture, modules, getting started, indexes, folder READMEs |

## What fails the build
- A broken link, a link to a missing heading (`#anchor`), or a Markdown link to a file that doesn't exist.
- A page that is in no sidebar chapter, or a sidebar entry for a file that is not a page.
- Markdown the parser can't read. Pages are CommonMark (`.md`, never MDX): raw HTML-like text such as a bare `<Module name>` becomes an
  HTML element, so placeholders go in backticks.

## Changing the site
- **A new page:** follow the recipe "New documentation page" in `core-instructions/19-change-recipes.md`.
- **A new chapter or a new folder of pages:** edit `sidebars.js`; a folder with a README becomes a category with `folder(label, dir)`.
- **A new single file outside `core-instructions/` and `docs/`:** add it to `pageFiles` in `docs-set.js`, then to `sidebars.js`.
- **Packages:** versions are pinned exactly in `package.json` and locked in `package-lock.json`; CI installs with `npm ci`. A new package
  is a G-Dependency decision (`core-instructions/15-change-control.md`) and goes on the approved list in `core-instructions/14-coding-standards.md`.
  Upgrades: change the version in `package.json`, run `npm install` in `website/`, build, and commit both files.

## Publishing
Every pull request builds and checks the site (CI job `docs`). After each merge to `main`, the workflow
`.github/workflows/docs-site.yml` builds it again and publishes it to GitHub Pages: https://samurai-netizen.github.io/citybuild/
(the owner's choice in CR-0002). `url` and `baseUrl` in `docusaurus.config.js` point there; `DOCS_SITE_URL` and `DOCS_BASE_URL`
override them for another host. Search is local (`@easyops-cn/docusaurus-search-local`): its index is built with the site.

## Troubleshooting
| Message | Fix |
|---|---|
| `These pages are not in the handbook sidebar: …` | Add each page to a chapter in `sidebars.js` |
| `Markdown link with URL … couldn't be resolved` | The linked file doesn't exist or is not a page: fix the path, or link a file the site can reach |
| `Docusaurus found broken anchors!` | A `#heading` link doesn't match a heading; anchors are the heading in lowercase with spaces as `-` |
| `Docusaurus found broken links!` | A link points at a URL with no page; the message names the page that has it |
| `npm ci` fails with a lockfile mismatch | `package.json` changed without `npm install`: run it in `website/` and commit `package-lock.json` |
