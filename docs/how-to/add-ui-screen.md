# Add a UI screen

A screen is something the player reads and taps: the build menu, the inspector, the research screen (`core-instructions/09-unity-client.md`
→ M0 screens). It renders what the client already holds and passes the player's intent on; it never decides an outcome. Recipe "New client
screen or view" in `core-instructions/19-change-recipes.md` is the checklist; this guide walks through it with the build menu (M0-45).

## Before you start
- The screen needs a plan task or an approved CR. If it changes the design (`21`–`26`), that is G-Design: ask first.
- Know which state it shows and where it comes from: `DisplayCity.Current` (the latest snapshot advanced to now), the `GameContent`, the
  connection state. A screen reads these; it never keeps its own copy of a balance, a cost or a timer.
- Know what a tap does. Commands go through `CommandSender`, never through `ApiClient` (`.claude/rules/unity.md`). A hint from the client
  (affordable, free builder, locked) is a hint only: the server checks again, so a hint never disables a button that the server would accept.

## The shape: view-model, thin component, layout
| Part | Where | What it is |
|---|---|---|
| View-model | `Presentation/<Feature>/…Model.cs` | Plain C#: the texts and flags of the screen, built from content and state. No `UnityEngine` objects, so an EditMode test checks every sentence. |
| Component | `Presentation/<Feature>/<Screen>.cs` | A thin `MonoBehaviour`: loads the layout, writes the view-model into the elements by name, forwards taps back to the view-model. No rules. |
| Layout | `Resources/UI/<Screen>.uxml`, `.uss` | UI Toolkit text files. Loaded by name with `Resources.Load` (decision 024), so the folder is fixed. Start each file with a comment saying what it is for. |

## Worked example: the build menu
One card per building type of the content, with its cost, build time, workers and recipe; locked cards say what they require and cannot be
selected; unaffordable cards warn but can be selected.

**1. The texts: `BuildCardModel`.** `BuildCardModel.Create(content, city, definition)` returns an immutable card: `Name`, `CostText`
(`Cost: 150 Coins, 30 Logs`), `BuildTimeText` (`Build time: 2m`, from `DurationText.Format`), `WorkersText`, `RecipeText`
(`2 Logs → 1 Planks · 60/h at 3 workers`), `LockText` and `HintText`. Every name and number comes from the definition. The rate is the
engine's `ProductionDisplay.PerHour`, not a second formula, and the lock is `CityQueries.IsUnlocked`, the same rule the server applies.
The card implements `IEquatable`, so the view redraws only what changed.

**2. The state: `BuildMenuModel`.** It holds the cards (one per `content.Buildings` entry, in content order) and `Selected`. `Update(city)`
makes the cards again and raises `CardsChanged` only when one differs; `TryToggle(typeId)` selects or deselects and refuses a locked or
unknown type. Selecting is display state: the placement flow (M0-46) reads `Selected` through `PlacementFlow`, which also closes the sheet when a card is picked. Test both classes in EditMode with the real content
of `Tests/Fixtures/content.json` (`Tests/EditMode/Presentation/Build/`): every sentence, the lock, each hint, the selection rules.

**3. The layout.** `BuildMenu.uxml` is a root (`picking-mode="Ignore"`, so empty space passes taps to the ground), a closed sheet with a
horizontal `ScrollView` and a "Build" button. `BuildCard.uxml` is one card whose labels are found by name. `BuildMenu.uss` styles both.
Sizes are in panel units (reference resolution 1200 × 800), so a 64 button is well above the 48 dp touch target. A state is a USS class
(`build-card--locked`, `build-card--selected`, `build-panel--hidden`) switched with `EnableInClassList`.
Add a layout test (`BuildMenuLayoutTests`): it loads the UXML and checks that every name the component looks up exists. A renamed
element then fails a test, not a player.

**4. The component: `BuildMenu`.** `BuildMenu.Create(parent, model)` loads the assets, makes a child object with a `UIDocument` (the shared
`UI/PanelSettings`, its own sort order) and returns null after one logged error that names a missing asset. The `UIDocument` builds its tree
when it is enabled, so give it its settings while the object is inactive, and fill the tree in `Start`, not right after activation.
`Start` clones `BuildCard` once per card, hooks a `Clickable` to `Tap(typeId)` and subscribes to the model. `RefreshCards` writes the texts and
hides an empty line (`style.display`); `RefreshSelection` marks the selected card. `OnDestroy` unsubscribes. Keep what a test needs
public (`Tap`, `SetOpen`, `Model`, the element names as constants): the PlayMode assembly cannot see `internal`.

**5. Wire it in `GameRoot`.** The composition root builds what the screen needs and nothing else does: `CreateBuildMenu` runs once connected,
and `ShowBuildings` calls `Model.Update(city)` on every `DisplayCity.Changed` (once a second), so a finished research, a freed builder or
new stock shows within a second. No singleton, no `Find*` call.

**6. Do not let taps fall through.** The ground reads the Input System directly, so a tap on a card would also select the tile behind it.
`BuildMenu.IsOverUi(screenPosition)` asks the panel (`panel.Pick`, after `ScreenPoint.ToTopLeft`: the Input System counts pixels from the bottom left, a panel from the top left, and `ScreenToPanel` does not flip), `GameRoot` hands it to `CityWorld.IsPointerOverUi`, and
`CityInputReader` leaves a press that started over the UI to the UI. Any screen with elements that take taps needs its area covered by
that predicate: a second screen means extending it, not replacing it: `GameRoot.IsOverUi` ors the menu's, the placement bar's, the building inspector's and the research screen's `IsOverUi`, and both use `UiRoot.IsOver` / `UiRoot.PadBySafeArea` (`Presentation/Ui/`).

**7. Respect the safe area.** `SafeAreaInsets.From(Screen.safeArea, …)` turns the notch and the bars into panel units; the component adds
them to its root padding when the layout is computed (`GeometryChangedEvent`).

**8. Test in the scene.** `Tests/PlayMode/BuildMenuTests.cs` creates the menu for the real content and a city, waits a frame for `Start`,
and checks the tree: one card per type, a locked card's lock text and disabled state, that a tap on it selects nothing, that a new city
updates the cards, that the sheet opens. Click a card with the component's `Tap`, not with a fake pointer.

## Checklist
- [ ] A screen the player opens and closes (a menu, a sheet, a panel) joins the `MenuGroup` in `GameRoot` (owner directive OD-16): `Join(closeAction)` and a call of `NotifyOpened()` when it goes from closed to open, so opening it closes the others and theirs close it. The close action is safe when it is closed already and keeps no state that belongs to another flow. A bar or toast of a flow is not a menu.
- [ ] Texts and flags in a plain class with EditMode tests; no game number written in code.
- [ ] Layout in `Resources/UI/`, a comment at the top of each file, a layout test for the names the component looks up.
- [ ] A picture comes from `UiIcons` (an icon of an item, a building type or a research node, found by content id; `UiIcons.Show(element, icon)` sets it as the element's background), never from a path written in the screen. A new content id needs a PNG named `item_<id>`, `bld_<typeId>` or `res_<id>` in `Resources/UI/Icons/` (`art/README.md`); `UiIconsTests` fails without it. The screen keeps its text when there is no picture.
- [ ] Component thin: no rule, no `ApiClient`, no allocation per frame (refresh on events, not in `Update`).
- [ ] A result needs no code of its own: a command's success or error is shown as a toast by `CommandFeedback` (`FeedbackMessages` holds the sentences); a screen shows an error line only when the player must see it next to the buttons, and then the same sentence, never the code or the server's text.
- [ ] Hints are hints: the server's check decides; nothing is subtracted or spawned before it answers.
- [ ] Touch targets of at least 48 dp, the safe area respected, pointers over the screen kept from the ground.
- [ ] Every type and member has an XML doc comment (the repository tests check it); new `.meta` files committed.
- [ ] Docs: `09` (the built line and the screen's paragraph), `docs/modules/CityBuilder.Client.Presentation.md`, the test index.

Run the Unity tests with `scripts/dev/test-unity.sh` (`client/README.md`). With the Editor open on the project, the script refuses to run;
close the Editor, or run it on a copy of the project.
