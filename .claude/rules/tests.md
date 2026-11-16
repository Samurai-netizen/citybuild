---
paths:
  - "server/tests/**"
  - "client/**/Tests/**"
---

# Test rules

Strategy: `core-instructions/08-testing-strategy.md`.

- Name tests `Method_Scenario_ExpectedResult`; security tests start with `Security_` and name the property.
- One behaviour per test. Arrange with builders/fixtures, not copy-pasted setup.
- Every test class has an XML doc comment that says what it covers, every test one that says the behaviour it proves, and every helper
  one that says what it builds (`core-instructions/14-coding-standards.md` → Documentation in code).
- Integration tests: `[Trait("Category", "Integration")]`, real PostgreSQL via Testcontainers,
  in-process API via `WebApplicationFactory`. Assert database state and ledger rows, not just status codes.
- Time only through `FakeTimeProvider`. No `Thread.Sleep`, no real clock, no test-order dependence.
- Property tests use fixed seeds and print the failing seed and case.
- Golden scenarios use a frozen test content file so balance changes don't silently rewrite expectations.
- **Tests first:** write the test, run it, see it fail because the behaviour is missing, then write the code (`08` → Test-first loop).
  Save the red state (`test(<ID>): red, …`) before the code that makes it green.
- **Run the smallest set that can fail** (`08` → Test tiers): in the loop `bash scripts/dev/test.sh <area> <TestClass>`; at a
  checkpoint `bash scripts/dev/test.sh changed`; the full suite (`quality.sh`) once at the finish and where the task spec's "Full run"
  line says so. When a Unity test fails, read `result="Failed"` in `client/CityBuilder/Logs/test-unity/EditMode.xml` or `PlayMode.xml`,
  never the whole multi-megabyte `.log` file.
- Never delete, skip, or loosen an assertion to make a suite pass. If you believe a test is wrong,
  explain why in the report before changing it.
