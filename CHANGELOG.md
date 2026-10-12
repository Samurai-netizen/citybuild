# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Each merged task adds one line under **Unreleased**, with its task id. Milestone releases move the lines into a version section.

## Unreleased

### Added
- Instruction kit: specs, AI workflow (session protocol, change control, build log, documentation system), Claude Code skills,
  subagents and hooks, git hooks, CI, M0 plan with 84 tasks (KIT).
- Kit verified on macOS; `new-log-entry.sh` fixed for the first entry of a day; GitHub `main` ruleset and security settings;
  public-repo directive OD-7; skeleton folders; how-to for resuming an interrupted session; kit audit (M0-01).

### Fixed
- Kit audit follow-ups A1, A2, A3, A9, A15, A16: `[PLAN]` tasks write ahead before the plan (no Plan Mode), the Bash guard no longer
  reads here-documents, comments or quoted text and checks each command on its own (new `scripts/dev/test-guard-bash.sh`),
  `scripts/dev/**` is a protected path, `scripts/` counts as code for the PR checks and the Stop hook, `.DS_Store` is untracked,
  and the license question is parked: no `LICENSE` file for now, directive OD-8 (KIT-AUDIT-FIXES).
