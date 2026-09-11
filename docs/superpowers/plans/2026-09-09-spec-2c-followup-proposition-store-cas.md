# Spec 2C follow-up — The check that was in the wrong process — Implementation Plan

**Design:** [`2026-09-09-spec-2c-followup-proposition-store-cas-design.md`](../specs/2026-09-09-spec-2c-followup-proposition-store-cas-design.md)
**Ticket:** [#129](https://github.com/karlssberg/Motiv/issues/129)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§6 step 6 — "PK-as-CAS; delete the in-memory CAS (21)", applied to the proposition side. Deferred by
decision 1 of [Spec 2C](../specs/2026-08-18-spec-2c-ef-reference-store-design.md).

## What this is

`IPropositionStore.WriteAsync` returns `Task<PropositionWriteResult>` instead of a bare `Task`, and
every batch entry becomes a compare-and-set against the version the store holds for that name.

This is *not* "invent a conflict outcome for propositions". The outcome already existed —
`PropositionUpdateResult.VersionConflict` — and `PropositionSet.PrepareUpdateCore` and
`PrepareWithdrawCore` already produced it. It was produced against **this replica's memory**, which is
silent about every other replica. The work is moving enforcement into the store, exactly as
[#121](https://github.com/karlssberg/Motiv/issues/121) did for rules.

## The three open questions, answered before any code

The ticket ends with three questions. They are answered in the design doc and settled here so the
sequence below is unambiguous.

1. **Append-only log, or conditional write?** Conditional write. The rule log exists for rollback and
   audit under an approval gate; propositions offer neither, and history is a *separate* question from
   concurrency. Conflating them would make this slice a schema change, a migration, a history API and
   a retention policy — four PRs, not one.
2. **How do deletes work?** Moot under a conditional write: a deletion removes the row and names the
   version it must still be at. Deleting an absent name stops being a no-op and becomes a conflict at
   `currentVersion 0` — a writer who observed a row another writer has already removed.
3. **What happens to `StoredProposition.Version`?** It becomes the checked token. It was already
   authored and carried on the row; it was simply never read by anything.

## Global constraints

- **The predicate must be the rule side's, not a new one.** A save lands when its version is
  *strictly greater* than the stored version; because rule versions are contiguous per name, that is
  the same statement as the rule log's "this `(Name, Version)` is not taken". One predicate, two
  schemas. Anything else is a second mental model to maintain.
- **No provider error codes.** Spec 2C decision 5, and it is what makes proving the EF store on SQLite
  generalise to Postgres and SQL Server.
- **No migration.** The `Version` column already exists; making it EF's concurrency token changes the
  generated `WHERE` clause and emits no DDL.
- **The in-memory `expectedVersion` check stays.** #121 deleted the `Interlocked.CompareExchange`, not
  `Rule.PrepareUpdate`'s `current.Version != expectedVersion` — which is still there at `Rule.cs:191`.
  The in-memory comparison is what makes the *prepared document* bind against the writer's own basis;
  the store check is what makes the answer true across replicas. Deleting the first would silently
  bind a stale writer's document against the winner's world.
- **One PR.** History, rollback and replay for propositions are out of scope and get their own ticket.

## Sequence

1. **Baseline.** Full-solution build and test before touching anything, so any later red is
   attributable. (Note: `dotnet` in this environment needs `env -u MallocStackLogging
   -u MallocNanoZone` — MinVer captures a child process's stderr into the version string, and the
   sandbox's malloc warnings land there. Symptom is `error CS7034` on a version string containing
   `MallocStackLogging`.)
2. **The contract.** `PropositionDeletion(Name, Version)` replaces the bare `string` in
   `PropositionBatch.Deletes`; `PropositionWriteResult` mirrors `RuleAppendResult`; `WriteAsync`
   returns it. `InMemoryPropositionStore` enforces the predicate.
3. **The conformance suite.** The contract is what a store *is*, so it is expressed there first —
   including the two existing tests whose meaning this change inverts
   (`Should_ignore_deleting_an_absent_name` becomes `Should_refuse_…`).
4. **`JsonFilePropositionStore`** and **`EfPropositionStore`.** The EF store is where the mechanism
   actually differs: a replace-in-place `UPDATE` succeeds silently under read-committed, so the
   read-then-catch shape `EfRuleStore` uses is not sufficient on its own. `Version` becomes a
   concurrency token.
5. **`PropositionSet`.** `PersistAndCommitCoreAsync` and `WithdrawCoreAsync` map a store conflict to
   `PropositionUpdateResult.VersionConflict`.
6. **`ChangeRequestSet`.** The envelope's proposition half gains the refusal its rule half already has.
   Where the rule half is already durable, a conflict is `PersistenceDesynced`, not `VersionConflict` —
   the outcome is chosen by what is durable, not by whether the store answered with a value or a throw.
7. **`StoreImport`.** The importer copies rows at the versions they already carry into an empty target,
   which is exactly why the predicate is "past the head" rather than "the head plus one". It gains the
   same conflict guard the rule half has.
8. **Prove the guards are load-bearing.** A green suite is not a live guard — neuter each store's
   conflict check at the call site and confirm the conformance tests go red, in all three stores.
9. **Close what step 8 exposes.** The mutation is expected to fail *store* tests only; if no
   SDK-level test fails, the `PropositionSet` and governance conflict paths are untested and that gap
   is part of this slice, not a follow-up.
10. **Docs.** `docs/propositions/IPropositionStore.md` currently documents the gap as a decision. It
    must stop saying "last writer wins", and the asymmetry table must narrow to history alone.
11. **Full solution suite**, compared against step 1.
12. **The mandatory `code-simplifier` pass**, asked specifically whether three near-identical
    `FindConflict` helpers across three assemblies are justified by the rule side's precedent or are
    over-duplication. Hand it the argument *for* leaving them, so that agreeing costs it nothing and
    disagreeing costs it something — a review that is only given the case it is expected to confirm
    confirms it.

## Expected fallout

- Every `IPropositionStore` test double in the tree breaks on the return type. That is the breaking
  change the ticket priced, and it is the cheap half.
- `PropositionSetAsyncWriteTests.Should_write_a_save_and_a_delete_as_one_batch` deletes a name that
  was never saved. Under the new contract that batch is a conflict. The fix is to save the name first,
  not to drop the deletion — dropping it would leave the test asserting nothing about batch shape.
- `Should_ignore_deleting_an_absent_name` inverts. Worth noting in the design doc as a *contract*
  change rather than a test fix: "the store is a dumb sink" was being read as "the store checks
  nothing", when `IRuleStore`'s own remarks already draw the line at *semantic* legality.

## Out of scope, and getting its own ticket

A proposition version log — history, rollback, and the document behind the decision log's replay
pin. Filed as [#224](https://github.com/karlssberg/Motiv/issues/224).
Spec 2's §2 says the log is "symmetric for propositions"; this slice delivers the concurrency half of
that symmetry and leaves the history half open. It is a schema, a migration, a retention policy and an
API, and it should not ride along inside a contract change.
