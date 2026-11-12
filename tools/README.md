Developer tools: `DocTools/` (generates and checks the reference pages in `docs/reference/generated/`, since M0-61; `docs/modules/DocTools.md`)
with its tests in `DocTools.Tests/`, and `BalanceSim/` (economy simulator) *(planned, M0-67)*. The tools build with the server's solution
and package versions: `Directory.Build.props` and `Directory.Packages.props` here import the server's.
