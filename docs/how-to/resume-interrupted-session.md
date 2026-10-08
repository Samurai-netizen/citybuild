# How to resume an interrupted session

A session can end at any moment: the AI ran out of tokens, the laptop closed, or the owner switched tools.
This guide shows how the **next** coder (a person, Claude Code or any other AI tool) picks the work up without
losing anything and without guessing. The rules are in `core-instructions/16-session-protocol.md` §F; this page is
the worked example. It was written in M0-01.

## Quick start
- **Claude Code:** start a session in the repo and run `/resume`. It does steps 1–3 below read-only and proposes the next step.
- **Any other tool:** "Read AGENTS.md, then resume following core-instructions/16-session-protocol.md §F."
- **A person:** follow the steps below by hand.

## Step 1: Read where things stand
1. Open `core-instructions/13-progress.md` → **Resume point**. Note the state, task, branch, build log entry,
   done steps, next step, open questions and anything marked "Known broken".
2. If the state is **IN PROGRESS**, open the build log entry named there and read its **last** `### Session N` section
   and the Decisions table. The decisions tell you *why* the code looks the way it does.
3. Print the task spec: `bash scripts/dev/show-task.sh <ID>`.

## Step 2: Compare with git
```bash
git status
git branch --show-current
git log --oneline -8
```
Then pick the matching case:

| What you find | What it means | What to do |
|---|---|---|
| Clean tree, on the task branch, branch pushed | The last session checkpointed properly | Continue from "Next step" |
| Uncommitted changes that the resume point explains ("half-done: …") | The session died mid-step | Run the build and tests, then finish or redo that step |
| Uncommitted changes nobody explains | Unknown work (another tool, the Unity Editor, a person) | Don't discard them. Inspect, test, then keep them (and log that) or move them to a rescue branch, and ask the owner |
| Resume point says IN PROGRESS but the branch isn't here | The work was pushed from another machine | `git fetch origin`, then `git switch task/<ID>-<slug>`. Unpushed work is lost; redo it from the last checkpoint |

## Step 3: Sign in to the build log
Before changing anything, append a new session section to the task's build log entry, with your tool and model:
```markdown
### Session 2 — 2026-10-09 · Codex / gpt-x   (or "Owner (manual)")
- **Plan:** resume at step 3 of Session 1 (…)
- **Done:** …
- **Stopped at / next:** …
```
Then continue the normal loop: small green steps and a checkpoint (`/checkpoint`, or `16` §D) after each one.

## Worked example
*Illustrative.* M0-15 ("Invariant properties") was interrupted when the first coder ran out of tokens.

**1. The resume point says:**
```text
- State: IN PROGRESS
- Task: M0-15 · Invariant properties
- Branch: task/M0-15-invariant-properties
- Build log entry: docs/build-log/2026/2026-10-12-01-M0-15.md
- Done steps: 1 conservation, 2 non-negative stock
- Next step: 3 storage-cap invariant (stock + reserved ≤ cap)
- Known broken: —
```

**2. Git shows unexplained changes:**
```text
$ git branch --show-current
task/M0-15-invariant-properties
$ git status --short
 M server/tests/CityBuilder.Simulation.Tests/Properties/InvariantTests.cs
?? server/tests/CityBuilder.Simulation.Tests/Properties/CapInvariant.cs
```
The resume point says nothing about a half-done step 3, so these changes are **unexplained**.
The session-start hook (Claude Code) also prints them with a warning.

**3. Inspect before deciding.** Read the diff and run the tests:
```bash
git diff
dotnet test server/CityBuilder.slnx --filter "Category!=Integration"
```
- **Case A — the tests pass and the diff is clearly step 3 in progress:** keep it. Log in Session 2:
  "Found uncommitted step-3 work (cap invariant), tests green, kept it." Finish step 3, then checkpoint.
- **Case B — the tests fail or you can't tell what the change is for:** move it out of the way without losing it:
  ```bash
  git switch -c rescue/2026-10-13-M0-15          # the changes come along
  git add -A
  git commit -m "wip(M0-15): rescue unexplained changes"
  git push -u origin HEAD
  git switch task/M0-15-invariant-properties     # clean again
  ```
  Log the rescue branch in the build log and in the resume point's open questions, ask the owner, then redo step 3
  from the last checkpoint.

**4. The branch isn't on this machine.** For example, the owner switched from a Windows PC to a Mac:
```bash
git fetch origin
git switch task/M0-15-invariant-properties   # creates the local branch tracking origin
```
Anything that was never pushed is gone. The build log says what was planned, so redo it from there.

## Never do this
- `git reset --hard`, `git clean -f` or a force-push to "get a clean state" without the owner's OK (`16` §F.4).
- Delete or "fix" changes you don't understand.
- Start a new branch or a new build log entry for the same task (`new-log-entry.sh` prints the existing entry instead).
- Rely on your own tool's memory of earlier sessions. Only the repository counts.

## Related
`core-instructions/16-session-protocol.md` (§D checkpoint, §F resume) · `core-instructions/13-progress.md` (resume point) ·
`docs/build-log/README.md` · Claude Code skills `/resume` and `/checkpoint`.
