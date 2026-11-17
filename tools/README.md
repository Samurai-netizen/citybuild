Developer tools: `DocTools/` (generates and checks the reference pages in `docs/reference/generated/`, since M0-61; `docs/modules/DocTools.md`)
with its tests in `DocTools.Tests/`, `BalanceSim/` (the balance simulator, since M0-67; `docs/modules/BalanceSim.md`) with its tests in `BalanceSim.Tests/`, and `MetricsReport/` (the player metrics page from the recorded player events, since CR-0009-T2;
`docs/modules/MetricsReport.md`) with its tests in `MetricsReport.Tests/`. The tools build with the server's solution
and package versions: `Directory.Build.props` and `Directory.Packages.props` here import the server's.
