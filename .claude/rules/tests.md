---
paths:
  - "server/tests/**"
  - "client/**/Tests/**"
---

# Test rules

Strategy: `core-instructions/08-testing-strategy.md`.

- Name tests `Method_Scenario_ExpectedResult`; security tests start with `Security_` and name the property.
- One behaviour per test. Arrange with builders/fixtures, not copy-pasted setup.
- Integration tests: `[Trait("Category", "Integration")]`, real PostgreSQL via Testcontainers,
  in-process API via `WebApplicationFactory`. Assert database state and ledger rows, not just status codes.
- Time only through `FakeTimeProvider`. No `Thread.Sleep`, no real clock, no test-order dependence.
- Property tests use fixed seeds and print the failing seed and case.
- Golden scenarios use a frozen test content file so balance changes don't silently rewrite expectations.
- Never delete, skip, or loosen an assertion to make a suite pass. If you believe a test is wrong,
  explain why in the report before changing it.
