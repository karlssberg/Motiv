# Store fixture — the teardown that reached into every other test's connections — Plan

**Date:** 2026-09-09
**Ticket:** [#219](https://github.com/karlssberg/Motiv/issues/219)
**Design:** [`2026-09-09-store-fixture-pool-clear-design.md`](../specs/2026-09-09-store-fixture-pool-clear-design.md)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§6 step 5 (the EF reference store) — the defect is in the fixture that suite runs on, not in the store
it proves.

## The problem

`Motiv.Serialization.EntityFrameworkCore.Tests.EfPropositionStoreTests` failed once during the
full-solution baseline run for [#211](https://github.com/karlssberg/Motiv/issues/211), on a clean tree,
and never reproduced: green alone, green three times as a whole project, green in the next full run.
The project takes 0.6 s alone and 8 s inside the loaded solution — the failure only appeared loaded.

The ticket's hypothesis was that `SqliteStoreFixture.DisposeAsync` calls
`SqliteConnection.ClearAllPools()`, which is process-global, while each fixture owns a private
GUID-named database file — so one class's teardown clears pooled connections belonging to every other
fixture still running. The ticket was explicit that this was **not proven**, and that a fix aimed at the
wrong mechanism would look like it worked.

## Approach

1. **Reproduce before fixing**, as the ticket demands. A blind loop is not a reproduction: twelve
   iterations of the project at `xUnit.MaxParallelThreads=24` under six CPU burners were all green.
   Instead, probe the hypothesised mechanism directly — one thread writing and loading through the
   store, another calling `ClearAllPools()` in a tight loop.
2. Read the actual failure, not the predicted one, and let it choose the fix.
3. Apply the convention this repository already established for it, rather than inventing one.
4. Leave a gate, because the fix is the **absence** of a call and no behavioural test keeps an absence
   absent.
5. Ship only assertions whose redness is measured, not assumed.

## Steps

- [x] Probe the mechanism; capture the real exception and stack.
- [x] Find the working example: `SqliteDecisionFixture` in `Motiv.Serialization.Sql.Tests` already
      solves this, and its remarks already say why `ClearAllPools` is wrong.
- [x] Write the failing tests and watch them go red for the right reasons.
- [x] Fix all three call sites, not just the one the flake was observed through.
- [x] Share one gate source file across the three assemblies that keep SQLite files on disk; red-prove
      it by mutation with a full rebuild between.
- [x] Measure the behavioural race test's detection rate; cut it when it came out at 2-in-3.
- [x] Full solution suite.
- [x] `code-simplifier` pass; apply what it found.
- [x] Plan + design in the implementation commit; ledger row on
      [#169](https://github.com/karlssberg/Motiv/issues/169); close the ticket.

## What was cut

**The behavioural race test.** It was written, watched red, restructured after review, and then
measured: 0 of 5 alone, 1 of 3 under whole-suite contention, 2 of 3 at 3000 iterations and eight
seconds a run. A test that is red two times in three is not a regression net — it is a coin flip a
future reader will use to "verify" a revert. The gate detects the same reintroduction deterministically
in milliseconds, so the probe is recorded in the design doc instead, with its measurements.

**Consolidating the two same-assembly `ConnectionString` helpers**, per the review's own rating of the
point as marginal: it would make `DerivedContextTests` depend on a fixture it otherwise does not use.

Nothing was deferred to a follow-up. The sweep to three call sites is wider than the observed failure
but is the same defect in the same repository, and leaving two of them would have left the gate lying.
