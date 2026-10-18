# Add a research effect type

An effect type is what completing a research node changes (`core-instructions/25-eras-and-research.md` → Effect types). M0 has three:
`unlockBuilding`, `laborReduction` and `storageCapBonus`. A node that only uses those is a content-only change (`add-building-or-item.md`).
A new type needs engine code, content validation, tests and docs: recipe "New engine rule or effect type" in `core-instructions/19-change-recipes.md`
is the checklist; this guide walks through it with an example.

## Before you start
- A new effect type changes the game's design. It needs a plan task or an approved CR (`core-instructions/15-change-control.md`), and its era:
  major mechanics arrive only with a new era (`25`), and nothing of a later era is usable early (OD-10).
- Decide what kind of effect it is. A **derived** effect changes a value that a query computes from `CityState.CompletedResearch`
  (all three M0 types are derived). A **one-time** effect changes stored state once, when the node completes (for example a grant of goods).
  Prefer derived: nothing is stored twice, and `Simulator.Advance` needs no new code.

## Worked example: `builderSlotBonus`
Goal: "Guild of Masons → +1 builder" from the `25` roadmap. The numbers only show the shape; they are not a design.
```json
{ "type": "builderSlotBonus", "amount": 1 }
```

**1. The effect class.** `shared/Simulation/Runtime/Content/BuilderSlotBonusEffect.cs`, beside `StorageCapBonusEffect`: `sealed`, derived from
`ResearchEffect`, an `internal` constructor (only the factory makes content), XML docs that name the JSON type, `#nullable enable` on line 1,
and its `.meta` file (`docs/modules/CityBuilder.Simulation.md` → Known limitations). Add it to the closed list in `ResearchEffect`'s summary.
```csharp
/// <summary>Adds builder slots to <see cref="GameRules.BuilderSlots"/>. JSON: <c>builderSlotBonus</c>.</summary>
public sealed class BuilderSlotBonusEffect : ResearchEffect
{
    internal BuilderSlotBonusEffect(long amount) { Amount = amount; }

    /// <summary>The extra builder slots (greater than zero).</summary>
    public long Amount { get; }
}
```

**2. Reading it.** In `Runtime/Content/ContentReader.cs`: a constant `_effectBuilderSlotBonus = "builderSlotBonus"`, a `case` in `ReadEffect`
that calls `AllowOnlyEffectFields` with only the values this type has, and the type in the `default` message. The data class
`Content/Data/ResearchEffectData.cs` already has `amount`; a value no type has yet needs a new property there, and the doc comment of `Type` lists the
types. Unknown types and stray values are already errors, so a typo in a content file fails at load.

**3. Validating it.** Every value the engine relies on gets a rule (`04` §14), checked in the class that owns that kind of rule:
- `ValueRules.CheckResearch`: `amount` > 0 (`ContentErrorCode.NotPositive`).
- `ReferenceRules.CheckEffectTargets`: an effect with a target checks that it exists and is the right kind (`InvalidEffectTarget`). This one
  has none.
- A rule across nodes or content, in its own class like `LaborRules` (Σ reductions ≤ `MaxLaborReductionPermille`): here perhaps "builder slots
  stay within a limit". A new code gets an XML doc in `ContentErrorCode` and a line in `04` §14.

**4. The engine reads it through a query.** Put the derived value in `Runtime/State/CityQueries.cs` and use it everywhere the base value was read:
```csharp
public static long BuilderSlots(CityState city, GameContent content)
{
    long slots = content.Rules.BuilderSlots;
    foreach (ResearchId completed in city.CompletedResearch)
    {
        foreach (ResearchEffect effect in ResearchOf(content, completed).Effects)
        {
            if (effect is BuilderSlotBonusEffect bonus) { slots = checked(slots + bonus.Amount); }
        }
    }

    return slots;
}
```
Here `CityRules.HasFreeBuilder` reads `content.Rules.BuilderSlots` and changes to the query; the snapshot's `builders.slots` (`05`) shows it.
Switch on the effect **type**, never on a research id or an era number (`.claude/rules/simulation.md`).

**5. When it applies.** For a derived effect, nothing changes in `Runtime/Settlement/`. `ResearchTimerSource` adds the node to
`CompletedResearch` at its end second, before that second's building completions and starts (`04` §6), so the effect counts from that second.
Values fixed when something starts keep their old value: a running cycle keeps its `CycleLabor`. If the effect changes something a
completion reads (auto-staffing, an upgrade), the order "research first" becomes observable: add a test for it (M0-09 build log, decision 19).

A **one-time** effect is applied in `ResearchTimerSource.CompleteDue`, right after `city.CompleteResearch()`, through a new internal transition
of `CityState`. Every resource it adds or takes must appear in the `SettlementReport` and the ledger (`04` invariant 4, `06` ledger reasons),
which changes the report and ledger contract (`05`, `06`): that needs approval like any contract change.

**6. Content, server and client.** Use the type in `server/content/mvp-content.json` only when the content design (`23`) does, with a new
`contentVersion`. `GET /content` carries research effects to the client *(planned, M0-28 and M0-38)*, and the research screen describes each
type *(planned, M0-52)*: both get the new type.

## Tests
| What | Where (`server/tests/CityBuilder.Simulation.Tests/`) |
|---|---|
| The JSON type is read; stray values are rejected | `Content/ContentShapeTests` |
| Each new rule fails with its code and path | `Content/ContentValueRuleTests`, `ContentReferenceRuleTests` or the class of its rule |
| Valid data becomes the effect class | `Content/GameContentFactoryTests` |
| The query, with and without the completed node | `State/CityQueriesTests` |
| The effect counts from the completion second, mid-advance; what already runs keeps its value | `Settlement/AdvanceProductionTests` or a new class under `Settlement/` |
| Mutations that read the new value | `Rules/` (here `PlaceBuildingTests` and `UpgradeBuildingTests`: a third builder after the node completes) |

The invariant and property suites (`04` §10; *planned, M0-14 and M0-15*) must stay green. A new kind of runtime code (not just one more `case`)
gets an EditMode test on Unity's runtime (`client/CityBuilder/Assets/_Project/Tests/EditMode`).

## Docs in the same PR
- `25` → Effect types: the row's "In M0" column (or the milestone).
- `04` §5 (the effect list) and §14 (every new content rule); §6 or §7 if the timing differs.
- `docs/modules/CityBuilder.Simulation.md`: the `ResearchDefinition, ResearchEffect` row and the queries.
- `CHANGELOG.md`; a decision row in `core-instructions/12-decisions.md` if the change is project-wide; this guide stays valid.
