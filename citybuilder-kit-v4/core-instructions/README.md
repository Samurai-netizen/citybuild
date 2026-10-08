# Core Instructions — index

The **normative** documents: what must be true about the game, the code and the way it's built.
They're written for AI coders first and readable by people. The descriptive docs (what exists, how to run it, what happened)
are in `docs/` (map: `docs/README.md`). Entry points: `AGENTS.md` (any AI tool), `CLAUDE.md` (Claude Code).

## Engineering (01–13)
| File | Read when | Owns |
|---|---|---|
| `01-project-context.md` | Starting cold | What, who, stack, phase |
| `02-principles.md` | Always (auto-loaded) | Non-negotiable rules |
| `03-architecture.md` | Adding projects, layers, endpoints | Intended system shape, dependencies, cross-player rule, repo layout |
| `04-simulation-model.md` | Production, workers, research, time | Engine spec and invariants |
| `05-api-contract.md` | Endpoints, DTOs, client networking | Endpoints, snapshot, errors, idempotency |
| `06-data-model.md` | Persistence, migrations, transactions | Tables, locks, lock ordering, write pattern |
| `07-security-model.md` | Any endpoint or command | Trust boundaries, threats, tests |
| `08-testing-strategy.md` | Writing tests | Test layers, invariants, commands |
| `09-unity-client.md` | Anything under `client/` | Unity structure, screens, AI-friendly rules |
| `10-milestones.md` | Deciding scope | M0–M5+ and exit criteria |
| `11-workflow.md` | Every task | Lifecycle, Definition of Ready/Done, task report |
| `12-decisions.md` | Making or questioning a decision | Game, engineering and workflow decisions |
| `13-progress.md` | Always (auto-loaded) | **Resume point** and live status |

## AI-building process (14–19)
| File | Read when | Owns |
|---|---|---|
| `14-coding-standards.md` | Writing any code | Size, clarity, correctness, XML docs, approved dependencies |
| `15-change-control.md` | Before anything outside the task | Autonomy levels, gates, protected files, feature intake (CRs), standing owner directives |
| `16-session-protocol.md` | Every session | Start, checkpoint, finish, resume — the repo is the only memory |
| `17-documentation-system.md` | Writing docs or the build log | Doc kinds, single source of truth map, build log rules, freshness |
| `18-git-and-ci.md` | Branching, committing, PRs | Branches, commit format, PRs, hooks, CI, GitHub settings |
| `19-change-recipes.md` | Planning any change | Where code goes, which tests, which docs, per change type |

## Game design (20–26)
| File | Read when | Owns |
|---|---|---|
| `20-reference-tap-tap-builder.md` | Design discussions | The reference game and how we differ |
| `21-game-vision.md` | UI/UX and art direction | Final look and feel across eras |
| `22-game-mechanics.md` | Designing features | **Owner's pillars** and all mechanics by era |
| `23-mvp-content.md` | M0 content and tests | Exact M0 numbers, golden scenario #1 |
| `24-economy-design.md` | Balancing, simulator | Scarcity, faucets/sinks, pacing, metrics |
| `25-eras-and-research.md` | Progression | Eras, research rules, major vs minor mechanics |
| `26-open-market.md` | Anything cross-player | Market design + what M0 must already respect |

## Scope and words (30–31)
| `30-out-of-scope.md` | Before adding anything new | What not to build in M0 (and never) |
|---|---|---|
| `31-glossary.md` | Naming things | Shared vocabulary |

## Maintenance rules
- One topic per file, under ~150 lines. Split rather than grow.
- When code and these files disagree, fix one of them in the same PR; never leave both.
- Content numbers: once the JSON exists it wins; update `23` alongside it.
- Protected files (`15`) change only with the owner's approval. The pillars in `22` change only with the owner.
