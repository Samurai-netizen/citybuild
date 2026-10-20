// The CityBuilder handbook. It renders the repository's Markdown where it lives (docs-set.js); nothing is copied.
// How it works and how to change it: website/README.md. Rules for writing pages: core-instructions/17-documentation-system.md §7.
const {themes} = require('prism-react-renderer');
const {repoRoot, listPages, listTracked} = require('./docs-set');
const {sidebarItemsGenerator} = require('./sidebar-items');
const repoLinks = require('./src/remark/repo-links');

const githubRepoUrl = 'https://github.com/Samurai-netizen/citybuild';
const branch = 'main';
const pages = listPages();
const tracked = listTracked();

/** @type {import('@docusaurus/types').Config} */
module.exports = {
  title: 'CityBuilder Handbook',
  tagline: 'How the game is designed, how it is built, and how we work',
  favicon: 'img/logo.svg',
  // Where the site would be published (GitHub Pages of this repository); both can be overridden for another host.
  url: process.env.DOCS_SITE_URL || 'https://samurai-netizen.github.io',
  baseUrl: process.env.DOCS_BASE_URL || '/citybuild/',
  trailingSlash: false,

  // Every broken link, anchor or Markdown link fails the build: that is the docs' link check.
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  onDuplicateRoutes: 'throw',
  markdown: {
    // `.md` files are plain CommonMark + GitHub tables (no MDX), exactly what GitHub and the AI tools read.
    format: 'detect',
    mermaid: true,
    hooks: {onBrokenMarkdownLinks: 'throw', onBrokenMarkdownImages: 'throw'},
  },
  themes: ['@docusaurus/theme-mermaid'],

  i18n: {defaultLocale: 'en', locales: ['en']},
  customFields: {githubRepoUrl, branch},

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          path: repoRoot,
          include: pages,
          exclude: [],
          routeBasePath: '/',
          // URL = file path: core-instructions/04-simulation-model.md → /core-instructions/04-simulation-model
          numberPrefixParser: false,
          sidebarPath: require.resolve('./sidebars.js'),
          sidebarItemsGenerator,
          // docPath is relative to the docs folder, the repository root (a plain string would get a `../` prefix)
          editUrl: ({docPath}) => `${githubRepoUrl}/edit/${branch}/${docPath}`,
          showLastUpdateTime: true,
          beforeDefaultRemarkPlugins: [[repoLinks, {repoRoot, pages, tracked, githubRepoUrl, branch}]],
        },
        blog: false,
        pages: false,
        theme: {customCss: require.resolve('./src/css/custom.css')},
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {respectPrefersColorScheme: true},
      docs: {sidebar: {hideable: true, autoCollapseCategories: false}},
      navbar: {
        title: 'CityBuilder Handbook',
        logo: {alt: 'CityBuilder', src: 'img/logo.svg'},
        items: [
          {type: 'docSidebar', sidebarId: 'handbook', label: 'Handbook', position: 'left'},
          {type: 'doc', docId: 'core-instructions/13-progress', label: 'Status', position: 'left'},
          {type: 'doc', docId: 'docs/plan/M0-PLAN', label: 'Plan', position: 'left'},
          {type: 'doc', docId: 'core-instructions/12-decisions', label: 'Decisions', position: 'left'},
          {type: 'doc', docId: 'docs/build-log/README', label: 'Build log', position: 'left'},
          {href: githubRepoUrl, label: 'GitHub', position: 'right'},
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Start',
            items: [
              {label: 'The project', to: '/'},
              {label: 'Getting started', to: '/docs/getting-started'},
              {label: 'Documentation map', to: '/docs'},
            ],
          },
          {
            title: 'Build',
            items: [
              {label: 'Architecture', to: '/docs/architecture/overview'},
              {label: 'Change recipes', to: '/core-instructions/19-change-recipes'},
              {label: 'Session protocol', to: '/core-instructions/16-session-protocol'},
            ],
          },
          {
            title: 'Source',
            items: [
              {label: 'Repository', href: githubRepoUrl},
              {label: 'Pull requests', href: `${githubRepoUrl}/pulls`},
            ],
          },
        ],
        copyright: 'Built from the Markdown files of the repository. Proprietary, all rights reserved.',
      },
      prism: {
        theme: themes.github,
        darkTheme: themes.dracula,
        additionalLanguages: ['csharp', 'bash', 'powershell', 'json', 'yaml'],
      },
      mermaid: {theme: {light: 'neutral', dark: 'dark'}},
      tableOfContents: {minHeadingLevel: 2, maxHeadingLevel: 4},
    }),
};
