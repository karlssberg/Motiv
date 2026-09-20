# Persisted scenarios — Design

**Date:** 2026-09-20
**Status:** Implemented
**Parent:** `2026-09-20-decision-reproduction-mcp-design.md`, section 3. Slice 2 of 5.
**Plan:** `docs/superpowers/plans/2026-09-20-persisted-scenarios.md`

## Problem

Studio's Evaluate pane became a table of named scenarios in September, and the person who tried it
said it was "like having unit tests". But the rows were tab state, seeded from four constants in
the browser: closing the tab lost them, no colleague could see them, and nothing outside the
browser — the coming MCP, a reproduction of a logged decision — could reach them.

## Decisions

1. **A scenario belongs to one rule.** Studio scopes the table to the open rule, and an expected
   verdict is meaningless without one. The id is unique within the rule only.
2. **The client mints the id.** A `PUT` at `baseVersion` 0 creates; the browser can add a row
   without a round trip and persist it as soon as it exists. Ids are 32 hex characters from
   `crypto.randomUUID()`.
3. **Replaced in place under a version compare-and-set, not a log.** A superseded sample is not a
   replay anchor. The EF table `MotivScenario` is keyed `(RuleName, Id)` with `Version` as a
   concurrency token — the shape the proposition table had before it became a log.
4. **Not governed.** A scenario never changes what a rule decides. Reads need `Read` on the rule's
   namespace, writes `Author`; no new grant verb, no approval gate.
5. **The model text is stored verbatim, JSON or not.** Studio keeps a half-typed model as typed;
   the store and the endpoint accept it as a string and hand it back unchanged.
6. **Routes exist only when a store is registered.** `AddScenarios()` on the builder maps
   `rules/{name}/scenarios`; without it the routes answer `404`, which `listScenarios` in
   `@motiv-rules/core` reads as an empty list, so an older host still renders the pane.
7. **Ids beginning with `__` are reserved for host bookkeeping** and never listed. Studio's seeding
   writes the four customer seeds once per rule and then a `__seeded` marker; a second boot skips
   any rule with a marker, so a rule an operator emptied stays empty.
8. **Insertion order is a column.** SQLite cannot `ORDER BY` a `DateTimeOffset`, and a coarse clock
   on Windows CI would tie two quick adds; `ScenarioRow.Sequence` (max plus one on insert, id as
   tiebreak) is what "the order they were added" means in SQL.
9. **Studio saves on blur, add, clone and delete — not per keystroke.** A row minted in the browser
   is written as soon as it exists, marked saving in the same state update that hands it to the
   store, so two quick adds each persist once. A `409` flags the row "changed elsewhere" and Reset
   reloads the rule's scenarios from the store. A delete conflict is not shown: the row is gone
   locally and Reset shows the truth.
10. **Provenance is `Author` and `TimestampUtc`**, flattened, rather than the parent spec's
    `RuleChangeProvenance`: a scenario has no change note, approval reference or build id.
11. **`ExpectedSatisfied` and `SourceDecisionId` are stored but not yet edited in Studio.** They
    exist for the reproduction slice (a decision saved as a scenario) and the MCP's test
    generation; the pane's structure is unchanged in this slice.

## Rejected

- **Attaching scenarios to a model type** so rules over the same model share them: the expected
  verdict is per rule, and the table is scoped to the open rule.
- **Server-generated ids**: an extra round trip before the first save, for nothing.
- **Saving per keystroke**: chatty, and every keystroke would race the previous save's version.
- **A separate seeding table or a host-only flag**: the marker is a scenario under a reserved id,
  so no store implementation knows about seeding.

## Outcome

Every store runs the shared `ScenarioStoreConformance` suite (12 tests). Endpoints: 6 tests
including both grant refusals and the unmapped-route `404`. Studio: the seeding test boots the
host twice over one store; the pane's unit tests cover load, empty-host, save-on-blur, add, delete
and conflict; the axe sweep passed all 80 checks against the stubbed routes.
