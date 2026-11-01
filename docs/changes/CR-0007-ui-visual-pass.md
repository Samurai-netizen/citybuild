# CR-0007 — UI visual pass: smaller, tidy, consistent

- **Status:** Approved
- **Type:** Design change
- **Requested by:** owner · **Date:** 2026-11-02
- **Milestone / era:** M0 / Era I
- **Conflicts with pillars (`22`) or the era rule (`25`):** none. It changes how the screens look, not what they do (no new mechanic, no new
  screen). It is a design change (G-Design, `15`), and it touches one written rule of `09`: "Touch targets ≥ 48 dp" (see Questions).

<!-- Status values: Proposed | Approved | In progress | Done | Rejected | Superseded.
     Only the owner moves a CR to Approved. No code before that. -->

## Problem / goal
The owner, after playing the M0 slice in the Editor: "Basically all of the UI elements are too big, and also very ugly. Improve the looks of
UI. It should be not as good as a final version, but it should look decent."

What the code shows (read, not measured on screen):
- **Scale.** `Resources/UI/PanelSettings.asset` is *Scale With Screen Size*, reference 1200 × 800, match width. Everything is drawn so that 1200
  panel units span the screen's width, whatever the screen. Fonts are mostly 20 to 26 units (up to 36), buttons 56 to 64 high, panels 460 to 720
  wide. On a 1920-wide Game view one unit is 1.6 px, so a 24 unit label is 38 px high.
- **No shared look.** 10 stylesheets (959 lines) each repeat their own sizes and colours: near-black translucent boxes with white text, and accents
  picked per screen (green `46,98,56`, red `110,60,56`, amber `255,200,90`, ...). Nothing ties them to the warm "toy block" palette of the art
  (`21` → The look, CR-0005), so the UI and the city look like two different games.
- **Fixed offsets that assume each other.** The toasts start 224 below the top, the inspector 124, the connection banner 108, the Research button
  sits at a fixed offset to the right of Build: all assume the HUD's height. Changing one size moves the rest out of line.

Why now: the M0 slice is about to be judged by the owner and others (M0-60 and on). A UI that looks unfinished hides whether the game works.

## Proposal
Make the whole client UI look decent and consistent, without changing what any screen does.

1. **One shared look.** A common stylesheet (`Resources/UI/Common.uss`) imported by every layout: colour, spacing, radius and font-size tokens as
   USS variables, and the shared classes (panel, chip, title, muted text, button with primary / danger / quiet variants, divider, scrim, card).
   Each screen's stylesheet keeps only layout (what sits where) and uses the tokens.
2. **A smaller, calmer scale.** One agreed set of sizes (about three font sizes, one button height, one radius, one spacing step) and a scale
   chosen with the owner (see Questions). Panels are narrower, text is smaller, buttons are no taller than they need to be.
3. **A palette taken from the art.** Warm neutrals and the three accent colours (go / warn / stop) from the Era I palette texture, not pure
   black and white. Contrast stays readable on the city behind it.
4. **Offsets that follow the HUD.** The HUD's height becomes one token and the toasts, the banner, the inspector and the Research button position
   from it, so a size change moves them together.
5. **Every screen, every state:** HUD and its chips, build menu and cards, placement bar, building inspector, research screen, welcome-back
   report, toasts, status label, fatal error screen, and the connection banner and blocking screen of M0-55.

One task, by the owner's choice (see Owner approval): CR-0007-T1 does every screen in one go, and the owner looks at the result once, at the end.

## Acceptance criteria
- [ ] `Common.uss` exists and every layout in `Resources/UI/` imports it; a test fails for a layout that does not.
- [ ] No screen stylesheet writes a colour or a font size as a literal (`rgb(`, `rgba(`, `#`, `font-size: <n>px`): they use the tokens. A test reads the
      stylesheets and fails for a literal outside `Common.uss`.
- [ ] Every button of every screen uses the shared button classes (one height, one radius, the variants), checked by a layout test per screen.
- [ ] The HUD's height is one token, and the toasts, banner, inspector and Research button position from it (no `224`, `124` or `108` written in a
      screen stylesheet).
- [ ] No element name, text or behaviour changes: every existing EditMode and PlayMode test passes without an edit to an assertion (a test that
      names a style class may change its class name; that is logged).
- [ ] On a landscape phone screen (2400 × 1080, and also 1920 × 1080 and 1280 × 720), no panel overlaps the HUD or runs off the screen. The AI coder
      looks at a screenshot of every screen at those sizes, and the owner does the [MANUAL] look (steps in `client/README.md`). Portrait is out of scope.
- [ ] The touch-target rule of `09` holds: every control that takes a tap is at least 48 dp high and wide on a phone (decided by the owner, see Owner approval). `09` states how panel units map to dp.
- [ ] The owner looks at the screens and says "decent" (recorded under Owner input in the task's build log entry).
- [ ] Docs: `09` (UX rules, built line), `docs/how-to/add-ui-screen.md` (use the tokens and shared classes), the Presentation module doc (layout list),
      `client/README.md` ([MANUAL] looks), CHANGELOG.

## Out of scope
- Final art: new icons, fonts, illustrations, 9-slice frames, animation, sound. (A custom font is a new asset and needs the owner's approval.)
- New screens, new features or moved features: a button stays on the screen where it is. Small position tidying inside a screen is in scope.
- Changing any text, except to shorten one that no longer fits.
- The city view (ground, buildings, status rings and bubbles, camera) and the Blender art.
- Portrait layouts, tablets as a target, light mode, themes, localisation, accessibility features beyond the contrast and touch-target rules above.
- A UI asset-store package or any other new dependency.

## Impact analysis
| Area | Impact | Files / docs |
|---|---|---|
| Engine (`04`) | none | — |
| Content (`23`, JSON) | none | — |
| API (`05`) | none | — |
| Data / migration (`06`) | none | — |
| Security (`07`) | none | — |
| Client (`09`) | every layout and stylesheet in `Resources/UI/` (10 stylesheets and 13 UXML layouts); `PanelSettings.asset` (scale); the few C# constants that mirror a stylesheet size (the safe-area `BasePadding` of each screen) | `client/CityBuilder/Assets/_Project/Resources/UI/*`, `Presentation/*` constants |
| Docs (recipes in `19`) | "New client screen or view" recipe: `09` UX rules and built line, the add-ui-screen how-to, the Presentation module doc, `client/README.md` | see Acceptance criteria |
| Tests (`08`) | a new EditMode test for the shared look (imports, no literals, shared button classes); layout tests keep their element names; PlayMode tests unchanged | `Tests/EditMode/Presentation/` |

## Risks and alternatives
- **The scale question has no single right answer.** A phone is held close and a desktop monitor is not, so a size that looks right in the Editor
  can be too small to hit on a phone, and the reverse. The 48 dp rule is about the phone. T1 makes the choice with the owner on screen (the
  reference resolution can be tried live in the Editor's Inspector) and the result goes into `09`.
- **Looks are subjective and I cannot see the screen.** Tests can hold the structure (shared classes, no literals, no overlap in the layout), not
  "decent". The owner's look after each task is the acceptance, so each task is small enough to look at in a few minutes.
- **Fixed offsets** (toast 224, inspector 124, banner 108) are replaced by one HUD token; a layout that depends on the HUD's real height at run
  time (measured, not assumed) is the alternative and costs more code. The token is enough for M0.
- **Alternative A, change only the reference resolution** (one number, an hour of work): everything shrinks at once but stays as ugly, and the
  per-screen colours and sizes stay scattered. Cheap to do first inside T1 so the owner sees the effect at once.
- **Alternative B, a Figma design first** (a Figma connection exists in this environment): better looks, more work, and a design the owner would have
  to approve before any code. Not proposed for "decent, not final"; possible later for the final UI.
- **Alternative C, leave it** until the final art: the slice stays hard to judge. Not recommended.

## Questions for the owner
1. **Which screen do you judge by?** The Editor's Game view (which resolution?) or a phone? This decides the scale in T1.
2. **Which look?** (a) the current dark panels with light text, tidied and warmed, or (b) warm "wood and parchment" panels that match the art.
   I recommend (a) for this pass: it keeps text readable on any ground and is the smaller change.
3. **Touch targets.** Keep "≥ 48 dp" for a phone (the Editor then shows compact buttons) or relax it for now? I recommend keeping it.
4. **The connection banner and blocking screen** of M0-55 (merged) are included in T4. Say if you want them first, since they are new.

## Tasks (filled in after approval)
| ID | Title | Model | Status |
|---|---|---|---|
| CR-0007-T1 | UI visual pass: every screen, the shared stylesheet and the scale | S | In progress |

## Owner approval
- Approved on: 2026-11-02. The owner's answers to the four questions, in their words: "1. Focus on mobile landscape view. 2. Look "b". 3. Keep
  touch targets. 4. Decide for yourself. Also do not divide the task for smaller ones - do everything in one go, even if it will take an hour".
  - Question 4 (the banner and blocking screen): decided by the AI coder: they are part of the one task like every other screen.
  - Look "b": warm wood and parchment panels that match the art.
  - Touch targets stay at 48 dp or more on a phone; the scale is therefore set in dp-like units (decision in the build log).
  - One task, not four: CR-0007-T1.
