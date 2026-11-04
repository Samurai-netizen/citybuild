"""Checks visual v2's UI files against the rules the client's EditMode tests read from the files as text, so a broken rule shows up before
anyone opens Unity: SharedLookTests (Common.uss imported, no colour or font-size literal in a screen stylesheet, every token defined, every
button a .btn, a touch target of at least 48), MobileLayoutTests (one scroll direction, wide panels can shrink, the portrait HUD and dock
tokens) and the layout tests (every element name and type the views look up, the hidden classes, the pointer rules). It reads the names
from the C# views of the Unity project, so a renamed element fails here too.

    python3 visual-v2/ui-preview/check-ui-rules.py [UI folder] [Presentation folder]

Defaults: visual-v2/unity/Resources/UI and client/CityBuilder/Assets/_Project/Presentation. Exits 1 when a rule fails.
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
UI = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "visual-v2", "unity", "Resources", "UI")
CODE = sys.argv[2] if len(sys.argv) > 2 else os.path.join(REPO, "client", "CityBuilder", "Assets", "_Project", "Presentation")
problems = []


def read(name):
    with open(os.path.join(UI, name), encoding="utf-8") as f:
        return f.read()


def no_comments(text):
    return re.sub(r"/\*.*?\*/", "", text, flags=re.S)


def files(ext):
    return sorted(n for n in os.listdir(UI) if n.endswith(ext))


def rule(name, selector):
    m = re.search(r"(^|\})\s*" + re.escape(selector) + r"\s*\{([^}]*)\}", no_comments(read(name)))
    if not m:
        problems.append(f"{name}: no rule {selector}")
        return ""
    return m.group(2)


def px(body, prop):
    m = re.search(r"(^|[;\s])" + re.escape(prop) + r"\s*:\s*(\d+)px", body)
    if not m:
        problems.append(f"no {prop} in {{ {body.strip()[:80]} }}")
        return -999
    return int(m.group(2))


def fail(text):
    problems.append(text)


# SharedLookTests --------------------------------------------------------------------------------------------------------------------
screens = [n for n in files(".uss") if n != "Common.uss"]
if len(screens) < 9:
    fail(f"only {len(screens)} screen stylesheets")
for n in files(".uxml"):
    t = read(n)
    if "<Style" in t and '<Style src="Common.uss"' not in t:
        fail(f"{n} does not import Common.uss")
for n in screens:
    t = no_comments(read(n))
    if re.search(r"\brgba?\s*\(|#[0-9a-fA-F]{3,8}\b", t):
        fail(f"{n} writes a colour literal")
    if re.search(r"font-size\s*:\s*\d", t):
        fail(f"{n} writes a font-size literal")
defined = set(re.findall(r"(--[a-z0-9-]+)\s*:", no_comments(read("Common.uss"))))
for n in files(".uss"):
    for token in re.findall(r"var\(\s*(--[a-z0-9-]+)", no_comments(read(n))):
        if token not in defined:
            fail(f"{n} uses an undefined token {token}")
buttons = 0
for n in files(".uxml"):
    for b in re.findall(r"<ui:Button\b[^>]*>", read(n)):
        buttons += 1
        if not re.search(r'class="[^"]*\bbtn\b', b):
            fail(f"{n}: a button without .btn: {b}")
if buttons < 13:
    fail(f"only {buttons} buttons")
common = no_comments(read("Common.uss"))
touch = re.search(r"--touch\s*:\s*(\d+)px", common)
if not touch or int(touch.group(1)) < 48:
    fail("--touch is below 48")
btn = re.search(r"\.btn\s*\{([^}]*)\}", common)
if not btn or "min-height: var(--touch)" not in btn.group(1) or "min-width: var(--touch)" not in btn.group(1):
    fail(".btn is not at least the touch target")
for n, under in (("Toasts.uss", False), ("Inspector.uss", True), ("Research.uss", True), ("ConnectionBanner.uss", True)):
    t = no_comments(read(n))
    if under and "var(--hud-height)" not in t:
        fail(f"{n} does not start under the HUD by --hud-height")
    if re.search(r"margin-top\s*:\s*(224|124|108)px", t):
        fail(f"{n} writes an old HUD offset")

# MobileLayoutTests --------------------------------------------------------------------------------------------------------------------
views = 0
for n in files(".uxml"):
    for sv in re.findall(r"<ui:ScrollView\b[^>]*>", read(n)):
        views += 1
        if not re.search(r'\bmode="(Vertical|Horizontal)"', sv):
            fail(f"{n}: a scroll view without one direction: {sv}")
if views < 3:
    fail(f"only {views} scroll views")
wide = 0
for n in files(".uss"):
    for sel, body in re.findall(r"([^{}]+)\{([^}]*)\}", no_comments(read(n))):
        m = re.search(r"(^|[;\s])width\s*:\s*(\d+)px", body)
        if m and int(m.group(2)) > 300:
            wide += 1
            if "max-width: 100%" not in body:
                fail(f"{n}: {sel.strip()} is wider than 300 without max-width: 100%")
if wide < 3:
    fail(f"only {wide} panels wider than 300")
root_tokens, portrait_tokens = rule("Common.uss", ":root"), rule("Common.uss", ".portrait")
bar, portrait_bar = rule("Hud.uss", ".hud-panel"), rule("Hud.uss", ".portrait .hud-panel")
if "flex-direction: column" not in portrait_bar:
    fail("the portrait HUD is not a column")
if px(bar, "height") + 12 != px(root_tokens, "--hud-height"):
    fail("--hud-height is not the HUD's height and 12")
if px(portrait_bar, "height") + 12 != px(portrait_tokens, "--hud-height"):
    fail("the portrait --hud-height is not the portrait HUD's height and 12")
if "margin-bottom: var(--dock-lift)" not in rule("PlacementBar.uss", ".portrait .placement-panel"):
    fail("the portrait placement bar does not sit above the dock")
if px(root_tokens, "--dock-lift") != px(root_tokens, "--touch") + px(root_tokens, "--sp-2"):
    fail("--dock-lift is not the touch target and one gap")
if px(portrait_tokens, "--toast-lift") <= px(root_tokens, "--toast-lift"):
    fail("the portrait toasts do not start higher")

# Layout tests: every element the views look up, with its type, and the hidden classes ---------------------------------------------------
TYPE = {"Label": "Label", "Button": "Button", "ScrollView": "ScrollView", "VisualElement": None}
elements = {}
for n in files(".uxml"):
    for kind, attrs in re.findall(r"<ui:(\w+)\b([^>]*)>", read(n)):
        name = re.search(r'name="([^"]+)"', attrs)
        if name:
            elements[name.group(1)] = (kind, attrs, n)
looked_up = set()
for dirpath, _, names in os.walk(CODE):
    for fn in names:
        if fn.endswith(".cs"):
            with open(os.path.join(dirpath, fn), encoding="utf-8") as f:
                code = f.read()
            for kind, name in re.findall(r'Q<(\w+)>\("([a-z0-9-]+)"\)', code):
                looked_up.add((kind, name))
            for name in re.findall(r'const string \w+Name = "([a-z0-9]+-[a-z0-9-]+)"', code):
                looked_up.add(("VisualElement", name))
for kind, name in sorted(looked_up):
    if name not in elements:
        fail(f"the views look up {name}, which no layout has")
    elif kind in ("Label", "Button", "ScrollView") and elements[name][0] != kind:
        fail(f"{name} must be a {kind}, it is a {elements[name][0]}")
for name, cls in (("build-panel", "build-panel--hidden"), ("inspector-panel", "inspector-panel--hidden"),
                  ("research-panel", "research-panel--hidden"), ("welcome-scrim", "welcome-scrim--hidden"),
                  ("placement-panel", "placement-panel--hidden"), ("connection-banner", "connection-banner--hidden"),
                  ("connection-blocker", "connection-blocker--hidden")):
    if name in elements and cls not in elements[name][1]:
        fail(f"{name} does not start hidden ({cls})")
for name in ("hud-root", "build-root", "inspector-root", "research-root", "welcome-root", "placement-root", "toast-root",
             "connection-root", "connection-status-root"):
    if name in elements and 'picking-mode="Ignore"' not in elements[name][1]:
        fail(f"{name} takes the pointer")
for n in ("Hud.uxml", "HudChip.uxml"):
    for tag in re.findall(r"<ui:\w+\b[^>]*>", read(n)):
        if "picking-mode" not in tag and not tag.startswith("<ui:UXML"):
            fail(f"{n}: an element of the HUD takes the pointer: {tag}")
if re.search(r'name="research-scroll"[^>]*mode="Horizontal"', read("Research.uxml")) is None:
    fail("the research cards do not scroll sideways")
for n in files(".uss"):
    if re.search(r":(first|last|nth)-child|\bgap\s*:", no_comments(read(n))):
        fail(f"{n} uses a selector or property UI Toolkit does not have (:first-child, :last-child, :nth-child, gap)")

print(f"{len(looked_up)} element names looked up by the views; {len(elements)} named elements in the layouts")
if problems:
    print("FAILED:\n  " + "\n  ".join(problems))
    sys.exit(1)
print("All UI rules pass.")
