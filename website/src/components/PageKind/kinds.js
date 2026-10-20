// What kind of page a repository file is (core-instructions/17-documentation-system.md §1 and §7). First match wins.
const KINDS = [
  {
    kind: 'status',
    label: 'Live status',
    text: 'The resume point and the state of the project, updated at every checkpoint.',
    test: (p) => p === 'core-instructions/13-progress.md',
  },
  {
    kind: 'generated',
    label: 'Generated reference',
    text: 'Generated from the code and content by tools/DocTools. Never edit it by hand: change the generator.',
    test: (p) => p.startsWith('docs/reference/generated/'),
  },
  {
    kind: 'history',
    label: 'History · append-only',
    text: 'The record of one task, as it was written at the time. Once merged it never changes; corrections are newer entries.',
    test: (p) => /^docs\/build-log\/\d{4}\//.test(p),
  },
  {
    kind: 'history',
    label: 'History',
    text: 'One line per merged task.',
    test: (p) => p === 'CHANGELOG.md',
  },
  {
    kind: 'decision',
    label: 'Decision record',
    text: 'Why something is the way it is. A superseded decision stays, marked, and names what replaced it.',
    test: (p) => p.startsWith('docs/adr/ADR-') || p === 'core-instructions/12-decisions.md',
  },
  {
    kind: 'change',
    label: 'Change request',
    text: 'A change put to the owner. Only approved ones are built; the status line says where it stands.',
    test: (p) => p.startsWith('docs/changes/CR-'),
  },
  {
    kind: 'plan',
    label: 'Plan',
    text: 'The approved plan: task specs written ahead of the work, and the board of what is done.',
    test: (p) => p.startsWith('docs/plan/'),
  },
  {
    kind: 'howto',
    label: 'How-to guide',
    text: 'Worked steps for a common change. Every change to that area keeps it valid.',
    test: (p) => p.startsWith('docs/how-to/') && !p.endsWith('/README.md'),
  },
  {
    kind: 'spec',
    label: 'Specification · normative',
    text: 'What must be true. If the code disagrees, one of them is wrong and both are fixed in the same pull request. Parts not built yet are marked (planned).',
    test: (p) => p.startsWith('core-instructions/') || p === 'AGENTS.md' || p === 'CLAUDE.md',
  },
  {
    kind: 'built',
    label: 'As built · descriptive',
    text: 'What exists now, how it works and how to use it.',
    test: () => true,
  },
];

// Protected files (core-instructions/15-change-control.md): every edit needs the owner's approval.
const PROTECTED = new Set([
  'AGENTS.md',
  'CLAUDE.md',
  'core-instructions/02-principles.md',
  'core-instructions/15-change-control.md',
  'core-instructions/22-game-mechanics.md',
]);

/** `@site/../core-instructions/04-simulation-model.md` → `core-instructions/04-simulation-model.md`. */
export function repoPath(source) {
  return source.replace(/^@site\/\.\.\//, '');
}

export function pageKind(path) {
  const found = KINDS.find((k) => k.test(path));
  return {...found, isProtected: PROTECTED.has(path)};
}
