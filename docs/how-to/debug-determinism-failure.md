# Debug a property test failure

The property tests in `server/tests/CityBuilder.Simulation.Tests/Properties/` run every seed from 0 to 499 over two frozen contents
(`golden-v1` and the rule `fixture`) and check determinism, split and timer invariance (M0-14) and invariants 4–13 of
`core-instructions/04-simulation-model.md` §10 (M0-15). When one fails, this guide takes you from the failing seed to a minimal rule test
and a fix. The test harness itself is described in `docs/modules/CityBuilder.Simulation.md` → Tests.

## Before you start
- A failure is a finding, not noise. Never change a seed, lower a floor, skip a seed or loosen a check to get green (G-Tests, `15`).
- A failure in engine code that your task didn't change is a bug for a `Bug` CR (`15`): write it down, shrink it, then ask the owner
  whether to fix it in your PR or separately (M0-14's CR-0001 is the example).
- The seeds are deterministic: `SeededRandom` is SplitMix64, pinned by `SeededRandomTests`, so a seed fails the same way on every machine.

## 1. Read the message
Every property prints what it needs to replay the case. An invariant failure looks like this (shortened; from a bug planted in M0-15 to test the checks):
```text
Invariant 6 (StorageCap) failed on content golden-v1, seed 29: operation 28, branch at clock 119947: AssignWorkers 3 1, then Advance 212,
step from clock 120097 to 120127: logs: stock + reserved went from 500 to 501 (refund 0), the cap is 500
seed 29, 31 operations from the starting city:
  1. PlaceBuilding woodcutter (3,4)
  ...
```
- **Invariant or property, content, seed:** which test, and `PropertyContent.Named(content)`.
- **Operation N:** the step happened during or after the N-th operation of the list (0 = after the whole history).
- **Step from clock A to B:** the observer (`InvariantObserver`) cuts every advance within the production cap into steps that end at each
  next timer, so a step holds at most one event second, at its end. `(dormant tail)` means the part of an advance after its production
  window, compared with the test model `DormantTail`.
- **Branch:** the observer sometimes leaves the history on a copy (`branch at clock C: <command>, then Advance G`). The city at clock C is
  the history's city after operation N − 1, advanced to C.
- The rest is the difference: the invariant's own words, or the first differing field (`inventory[logs]: expected 2, actual 1`).

A message that says `the replay threw` fails every invariant at once: read the exception first. One that says `The observed steps differ
from the generated history` means stepping changed the result, which is a split invariance failure (property 2) seen from here.

## 2. Reproduce it
Run only the failing test, for example:
```bash
dotnet test server/tests/CityBuilder.Simulation.Tests --filter "FullyQualifiedName~InvariantPropertyTests.Observe_EveryStep_ProductionKeepsStockPlusReservedWithinTheCap"
```
Then write a scratch test (never committed) next to the properties that rebuilds the city just before the step and prints it:
```csharp
[Fact]
public void Scratch()
{
    GameContent content = PropertyContent.Named("golden-v1");
    CityHistory history = CityGenerator.Generate(content, seed: 29);
    CityState city = history.CopyAfter(content, 27);                   // the city after operation N − 1
    Simulator.Advance(city, content, 119947 - city.Clock);             // only for a branch: up to its clock (within the cap)
    CityRules.AssignWorkers(city, content, 3, 1);                      // the branch's command, then its zero-length advance
    Simulator.Advance(city, content, 0);
    Simulator.Advance(city, content, 120097 - city.Clock);             // up to the step's start
    Assert.Fail(CityDump.Describe(city));                              // read the state before the failing step
}
```
`CityDump.Describe` prints every stored value. Advance once more to the step's end and compare. Within the production cap, advancing in
pieces gives the same city as one advance (property 2), so you can stop at any second you like.

## 3. Shrink it to the rule
Look at the state before the step and find the two or three buildings involved: the one whose numbers are wrong and those that share an
item with it. Then build that situation directly, without the history:
- Use the rule fixture (`State/CityFixture`): `City(clock, inventory, buildings, completedResearch)` with `Idle`, `Constructing`,
  `Upgrading` and `Producing` buildings, and `CityFixture.TestContent`. Its numbers are in the summaries of `Rules/*Tests.cs`.
- Keep only what makes the failure happen: one item at its cap, one consumer, one producer. Put the clock right before the event.
- Drive it with `Simulator.Advance` and the `EngineRules/Commands` helpers (a mutation, then a zero-length advance), as the server will.
- Check the exact expected state with `CityDump.BuildingLine` or `CityDump.Describe`.

Example: CR-0001 started as split invariance failing on golden-v1, seed 249, over 130 s with five buildings. Its rule was "a start frees
room under a cap for a lower id". The rule test needs three buildings and one zero-length advance:
```csharp
// EngineRules/StartRuleTests.cs
CityState city = City(clock: 1000, inventory: [("wood", 100)], completedResearch: ["carpentry"], buildings:
    [Idle(1, "hut", 0, 0), Idle(2, "lumber_camp", 1, 0, workers: 2), Idle(3, "workshop", 2, 0, workers: 1)]);
Simulator.Advance(city, TestContent, 0);
Assert.Equal("building 2 lumber_camp (1,0) L1 w=2 Producing 1000..1020 target=0 labor=40/40@1000", CityDump.BuildingLine(city, 2));
```

## 4. Write the rule test, then fix
1. Put the test in `EngineRules/<Area>RuleTests.cs` (start, cycle, worker, research, upgrade), named after the rule
   (`StartRule_HigherIdStartFreesRoomForALowerId_BothStartInTheSameSecond`). Run it and watch it fail for the reason you expect.
2. If the spec is unclear or the fix changes a rule players see, stop and ask (G-Design, a CR). Otherwise fix the engine in
   `shared/Simulation/Runtime/` and update `04` where the rule's wording changes.
3. Run the rule test, then the whole Simulation suite: the property that found the bug must now pass, and nothing else may change.
4. Keep both: the rule test and the property. Write the root cause in your build log entry (recipe "Bug fix" in `19`), with the seed.

## When the property, not the engine, is wrong
It happens: a check can expect something `04` doesn't say. Find the sentence in `04` that the check enforces. If there is none, the
check is wrong: explain it in the build log before you change it (G-Tests), and add a test that pins the corrected behaviour.

## Determinism failures (property 1)
If the same seed gives a different result twice, look for what `04` §9 forbids: `Dictionary` or `HashSet` order, static mutable state,
`DateTime`, randomness, floating point. `StructuralEquality` names the first differing field; the operation before it is the suspect.
