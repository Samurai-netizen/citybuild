# CR-0002 — Project handbook: a Docusaurus site over the docs, kept current by every task

- **Status:** In progress
- **Type:** Process
- **Requested by:** owner · **Date:** 2026-10-20
- **Milestone / era:** M0 (before M0-19) / —
- **Conflicts with pillars (`22`) or the era rule (`25`):** none

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The documentation is complete for its age but hard to read as a whole. It is about 100 Markdown files in `core-instructions/` and `docs/`,
cross-referenced mostly by backticked paths and spec numbers (about 960 paths and 1,200 numbers such as `04`, against 84 real links). People
can't browse it like a book, search it or see at a glance whether a page is a rule, a description of what is built, or history. The owner
also wants the AI workflow to keep the documentation current with every task, with checks that catch drift instead of relying on discipline.

## Proposal
1. **A handbook website (Docusaurus)** in `website/`. It is a presentation layer: it renders the Markdown files **where they are**
   (`core-instructions/`, `docs/`, the root and folder READMEs), so AI tools, hooks and the append-only build log keep their paths.
   - The book order lives only in `website/sidebars.js` (chapters: start here, game design, architecture and engine, server and API, client,
     development guide, how we work, plan and scope, decisions and history).
   - URL = file path (`core-instructions/04-simulation-model.md` → `/core-instructions/04-simulation-model`).
   - Backticked repo paths and spec numbers become links (to the page, or to the file on GitHub for code).
   - Every page shows its kind (specification, as built, how-to, decision, history, plan, generated) and its source file.
   - Mermaid diagrams render. Broken links and anchors fail the build. A page missing from the sidebar fails the build.
2. **The workflow keeps it current.**
   - Every task's build log entry gets a **Docs impact** table: the pages the change can make wrong (from the recipes in `19`), filled at
     the start and closed at the finish (`updated` or `no change: <reason>`).
   - `check-docs.sh --base` fails a PR when a module's code changed and neither its module doc changed nor a Docs impact row explains why,
     and when a deleted or renamed file is still named in the current docs.
   - The site build runs in the required `docs` CI job and in `/finish-task`.
   - `16`, `17`, `19`, `11`, `18`, the skills, the PR template and `AGENTS.md` say the same.
3. **A full audit of the docs against the code**, covering all merged work (KIT, M0-01 to M0-18), with every confirmed finding fixed in the
   doc that owns the fact.

## Acceptance criteria
- [ ] `bash scripts/dev/docs-site.sh build` builds the site with no broken links, anchors or Markdown links, and with every page in the sidebar.
- [ ] Every Markdown file of `core-instructions/` and `docs/` (templates excepted) is a page; no file was moved or renamed.
- [ ] Backticked repo paths and spec numbers link to their page or to the file on GitHub; Mermaid diagrams render.
- [ ] Each page shows its kind and its source file; build log entries say they are append-only history.
- [ ] The CI `docs` job builds the site; `check-docs.sh --base` has the module-doc, renamed-file and Docs impact checks, each tried on a
      failing case.
- [ ] `16`, `17`, `19`, `11`, `18`, the build log template, the PR template, the skills and `AGENTS.md` describe the new steps; the owner's
      instruction is a standing directive in `15`.
- [ ] The audit's findings, fixes and deferrals are in the build log entry; `check-docs.sh --base origin/main` passes.

## Out of scope
- Moving or renaming documentation files, or rewriting the specs' content beyond the audit's fixes.
- Docs versioning (one version: `main`; history is git and the build log), a blog, translations, MDX pages.
- Nothing else: publishing (GitHub Pages) and local search were out of scope until the owner said yes to both on 2026-10-20 (below).

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | none | — |
| Content (`23`, JSON) | none | — |
| API (`05`) | none | — |
| Data / migration (`06`) | none | — |
| Security (`07`) | none; the repo is public already (OD-7), so the site exposes nothing new | — |
| Client (`09`) | none | — |
| Tooling | New Node.js project `website/` (Docusaurus 3.10.2, pinned, `npm ci`); new script `scripts/dev/docs-site.sh` | `website/`, `.gitignore`, `14` (approved dependencies) |
| CI | `docs` job builds the site; `check-docs.sh` gains three PR checks | `.github/workflows/ci.yml`, `scripts/dev/check-docs.sh` |
| Docs (recipes in `19`) | Process docs, the recipe "New documentation page", the audit's fixes | `03`, `11`, `12` (W12), `14`, `15` (OD-11), `16`, `17`, `18`, `19`, `AGENTS.md`, `docs/getting-started.md`, templates, skills |
| Tests (`08`) | none (no code under test changes) | — |

## Risks and alternatives
- **Node.js becomes a second toolchain.** Only for the docs; the game build doesn't need it. Pinned versions and a lockfile keep CI reproducible.
- **Raw HTML-like text** (`<Module name>`) breaks a CommonMark page. Mitigation: the build fails on it, and `17` says to keep placeholders in backticks.
- Alternative: restructure the docs into numbered book folders (the outline the owner was given). Rejected: it breaks every path that AI
  tools, hooks, skills and the merged, append-only build log entries use; the sidebar gives the same book order without moving files.
- Alternative: Zensical or MkDocs. Rejected: the owner chose Docusaurus; Material for MkDocs reaches its end of life in 2027.
- Alternative: MDX pages. Rejected: plain Markdown stays readable on GitHub and by every AI tool, and needs no JSX knowledge to edit.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0002-T1 | Handbook site, docs-as-code workflow and the full docs audit (one PR) | O | In review |

## Owner approval
- Approved on: 2026-10-20 — the owner's request in chat: "Implement a documentation system for citybuild game. Run through all previous
  work, make the documentation up to date, and make changes to the AI workflow, so it continues to keep the documentation up to date with
  every Task. Let's use docusaurus." The owner also asked for "your own improved and well thought out implementation" of an outline from
  another AI. The request covers the protected files this needs (the AI workflow); each edit is listed in the build log entry.
- 2026-10-20, later: publish on GitHub Pages: "GitHub Pages (Recommended)"; search: "Local search (Recommended)"
  (`@easyops-cn/docusaurus-search-local`, G-Dependency approved).
