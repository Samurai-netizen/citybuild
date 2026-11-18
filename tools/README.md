Developer tools: `DocTools/` (generates and checks the reference pages in `docs/reference/generated/`, since M0-61; `docs/modules/DocTools.md`)
with its tests in `DocTools.Tests/`, `BalanceSim/` (the balance simulator, since M0-67; `docs/modules/BalanceSim.md`) with its tests in `BalanceSim.Tests/`, and `MetricsReport/` (the player metrics page from the recorded player events, since CR-0009-T2;
`docs/modules/MetricsReport.md`) with its tests in `MetricsReport.Tests/`. The tools build with the server's solution
and package versions: `Directory.Build.props` and `Directory.Packages.props` here import the server's.

**[MANUAL] MetricsReport, sort by click** (CR-0009-T2 plan point A; the page's structure is tested, the click is not): run
`bash server-panel/panel.sh metrics`, then in the page click every header of the checkpoint explorer and of one other table: the first
click sorts descending (▼), the second ascending (▲), and "—" stays below every number. Then type in the explorer's filter box and
tick "Named only": rows hide and come back. Do it once with the network off: the page looks the same.
