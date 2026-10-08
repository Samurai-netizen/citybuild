# Project Context

## What we are building
**CityBuilder** (working title): a mobile city builder in the genre of *Tap Tap Builder*, built on different pillars:
- Progress comes from **real-time production chains** ("2 Logs → 1 Plank, 60 per hour"), not tapping.
- **Workers** are assigned to buildings: more workers, faster production.
- The city starts in the **Middle Ages**, and **research** moves it through eras (medieval → space age).
  Each era adds new major mechanics.
- There is **no XP and no city level**. Progress = research + building levels + land.
- From Era II, players trade on an **open market** at their own prices.

Design files: reference game `20` · final vision `21` · mechanics `22` · M0 numbers `23` · economy `24` ·
eras and research `25` · open market `26`.

## Who builds it
- **The owner** directs: chooses tasks, approves plans, CRs and PRs, merges, and does [MANUAL] Unity steps.
- **AI coders** build it one after another: Claude Code (Opus 5.5 for `[O]`, Sonnet 5.5 for `[S]`) and possibly other tools.
- No coder remembers earlier sessions. The repository (resume point, build log, docs) is the shared memory (`16`).
- AI coders can't click inside the Unity Editor, so editor-only steps are marked **[MANUAL]**.

## Current phase
**M0 — 14-day technical vertical slice** of Era I: production chains with workers, research, offline progress,
and a server-authoritative API. Nothing from Era II+ is built, but the foundation must not block it.
Live status: `13-progress.md`. Plan: `10-milestones.md`.

## Stack (verified October 2026 — do not upgrade mid-milestone)

| Layer | Choice |
|---|---|
| Client | Unity **6.3 LTS** (supported to Dec 2027), C# 9, UI Toolkit, Input System, Unity Test Framework |
| Client JSON | `com.unity.nuget.newtonsoft-json` |
| Shared engine | `CityBuilder.Simulation`: netstandard2.1, C# 9, used as a Unity local package and a .NET project |
| Server | **.NET 10** (LTS), ASP.NET Core minimal APIs, built-in OpenAPI, ProblemDetails, rate limiter, `TimeProvider` |
| Persistence | EF Core 10 + Npgsql, **PostgreSQL 18.6** (pinned) |
| Tests | xUnit, Testcontainers (PostgreSQL), `FakeTimeProvider`, Unity Test Framework |
| Local infra | Docker Compose: `postgres`, `server` |
| Deferred | Redis, background workers, cloud hosting, CI/CD, analytics (see milestones) |

## Platforms
Targets are Android and iOS. During M0, run in the Unity Editor and make development builds for desktop or Android.
iOS builds require macOS + Xcode.

## Originality
Genre inspiration only. Do not copy names, art, UI layouts, text or audio from Tap Tap Builder or any other game.
All content in this repo is original.

## Where things are
Principles `02` · Architecture `03` · Simulation `04` · API `05` · Data `06` · Security `07` · Testing `08` ·
Unity `09` · Milestones `10` · Workflow `11` · Decisions `12` · Progress `13` · Coding standards `14` ·
Change control `15` · Session protocol `16` · Documentation `17` · Git/CI `18` · Change recipes `19` ·
Out of scope `30` · Glossary `31`. Descriptive docs: `docs/README.md`. Plan: `docs/plan/M0-PLAN.md`.
