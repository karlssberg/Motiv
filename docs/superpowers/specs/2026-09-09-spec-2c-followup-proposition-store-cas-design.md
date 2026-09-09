# Spec 2C follow-up — The check that was in the wrong process — Design

**Date:** 2026-09-09
**Ticket:** [#129](https://github.com/karlssberg/Motiv/issues/129)
**Plan:** [`2026-09-09-spec-2c-followup-proposition-store-cas.md`](../plans/2026-09-09-spec-2c-followup-proposition-store-cas.md)
**Source:** bundle spec
[2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
§2 "Cross-process write coordination (21)" and §6 step 6, applied to the proposition side.
**Lineage:** Spec 2A ([#125](https://github.com/karlssberg/Motiv/pull/125), which landed step 6 for
rules early) → Spec 2B ([#126](https://github.com/karlssberg/Motiv/pull/126)) → Spec 2C
([#128](https://github.com/karlssberg/Motiv/pull/128), which deferred this) → here.

## The defect was a check that could not see the thing it was checking

`PropositionSet.UpdateAsync` and `WithdrawAsync` have always taken an `expectedVersion`, and both have
always done this:

```csharp
if (current.Version != expectedVersion)
    return WritePrepare.Rejected(PropositionUpdateResult.VersionConflict(current.Version));
```

So propositions *had* optimistic concurrency. It just ran against `Scope.Current.Authored` — this
replica's memory. Two replicas over one store both read v1, both find their own memory at v1, both
compute v2, both write. `WriteAsync` returned a bare `Task`, so neither could be told, and the second
write silently replaced the first.

That is the situation the rule side was in before
[#121](https://github.com/karlssberg/Motiv/issues/121): a check that is **redundant intra-process**
(the publish gate already serialises writes) and **insufficient inter-process**. The ticket named this
precisely, and it is why the slice is much smaller than "close the rule/proposition asymmetry" sounds.

## The one idea: two schemas, one predicate

The rule store's compare-and-set is a primary key. Publishing v6 is `INSERT RuleVersion(Name, 6, …)`;
two replicas at v5 both compute 6, the PK lets one win.

A proposition table replaces rows rather than appending them, so it has no `(Name, Version)` PK to
violate. The instinct is that this makes the proposition side structurally weaker, and that closing
the gap therefore requires building the append-only log. It does not — because **rule versions are
contiguous per name**, and so:

> "version *v* is not already taken" ≡ "*v* is strictly greater than the head"

Those are the same predicate. The first is how you say it against an append-only log; the second is
how you say it against a row that gets replaced. Both are enforced by the database, both make a lost
update impossible, and both hand back the current version so a stale editor can re-base.

So the contract is:

- a **save** claims a position no writer has claimed yet → it lands only when
  `StoredProposition.Version` is **strictly greater** than the stored version (0 when no row exists);
- a **deletion** names a position that must still be the writer's → it lands only when
  `PropositionDeletion.Version` **equals** the stored version.

One stale entry refuses the whole batch, which is what keeps a governed envelope's proposition half
all-or-nothing.

### Why "strictly greater" and not "the head plus one"

`>` looks lax next to `== stored + 1`. It is deliberate, and the importer is why:
`StoreImport.CopyAsync` copies proposition rows into an empty target **at the versions they already
carry** — a proposition at v7 arrives as v7, not renumbered to v1. Under `== stored + 1` that import
would be a conflict against an empty store. Under `>` it is fine, and no lost update slips through:
every failure mode the strict form catches (`v` already written, `v` behind the head, a create over an
existing name) is caught by `>` too, because in each of them the store is already at or past `v`.

This is also, again, exactly the rule side: `AppendAsync` accepts a history import at v1…v7 into an
empty log for the same reason.

## What was *not* built, and why that is not a hedge

The ticket asks whether propositions need a full append-only log. They do not need one *for
concurrency*, and this slice does not build one.

The rule log exists for **rollback and audit under an approval gate** ([#110](https://github.com/karlssberg/Motiv/issues/110)):
`RuleSet.RestoreAsync` reads `HistoryAsync`, and a rollback appends rather than rewrites. Propositions
offer neither operation. Concurrency and history are separate obligations that the rule side happens
to discharge with one mechanism, and reading that coincidence as a requirement would have turned a
contract change into a schema change, a migration, a history API and a retention policy.

What is genuinely left open: spec 2 §2 names three provenance anchors, the third being *"referenced
proposition versions (the replay pin)"*. The decision log records the proposition version an
evaluation used; with no proposition log, that version number names a document nobody kept. That is a
real gap — but it is a gap in **replay**, not in concurrency, and it is filed as
[#224](https://github.com/karlssberg/Motiv/issues/224) rather than smuggled in here. The
`docs/propositions/IPropositionStore.md` asymmetry table now says exactly this: history is still
asymmetric, concurrency is not.

## Decisions

1. **Conditional write, not an append-only log.** Above. Reversible: if a `PropositionVersion` table
   ever lands, the same `WriteAsync` contract is implemented by a PK insert with no caller change —
   which is the point of stating the predicate in the interface rather than in a schema.
2. **No new field for the expected version.** `StoredProposition.Version` was already authored and
   carried on every row; it was simply never read. It becomes the token. Only deletions needed a new
   carrier, because `Deletes` was a bare `IReadOnlyList<string>` — hence `PropositionDeletion`.
3. **Deleting an absent name is now a conflict, not a no-op.** This reads as a reversal of "a store is
   a dumb sink", and is not. `IRuleStore`'s own remarks already drew the line: a store is dumb about
   *semantic* legality and load-bearing about *structure*. A deletion naming v1 against a store holding
   no row is a writer acting on a row someone else already removed — structurally stale, and the exact
   thing this slice exists to report.
4. **The in-memory `expectedVersion` check stays.** #121 deleted the `Interlocked.CompareExchange`, not
   the plain comparison — `Rule.PrepareUpdate` still carries it at `Rule.cs:191`. It is not redundant
   with the store check, it is *upstream* of it: it decides which document gets bound and which world
   the dependent closure is rebound against. Delete it and a stale writer's document would be prepared
   against the winner's world and only then refused — the right answer for the wrong reason, and the
   wrong answer as soon as the two worlds differ.
5. **EF uses a concurrency token; the rule store's read-then-catch is not enough on its own.** See
   below.
6. **A conflict after the rule half is durable is `PersistenceDesynced`, not `VersionConflict`.** See
   below.

## Where the EF store genuinely differs from the rule store

`EfRuleStore.AppendAsync` reads the taken versions inside the transaction, then inserts, then catches
`DbUpdateException` and re-reads to decide whether it lost a race. The read is not the guard — the
**primary key** is; the read only exists to produce the `currentVersion` a conflict must carry, since
an exception cannot supply it.

Transplanting that shape alone to a replace-in-place table would have produced a check that reports a
property it does not check. EF's generated `UPDATE` for a tracked entity is
`UPDATE … WHERE Name = @name` — no version predicate. Under read-committed, two transactions can both
`SELECT` version 1, both pass an in-memory comparison, and the second `UPDATE` blocks on the row lock,
then proceeds and overwrites. The read-then-write is not atomic, and nothing throws.

The fix is one line of mapping:

```csharp
entity.Property(row => row.Version).IsConcurrencyToken();
```

Every generated `UPDATE` and `DELETE` now carries `AND Version = @original`, so the loser matches no
rows and EF raises `DbUpdateConcurrencyException` — a subclass of `DbUpdateException`, so the existing
catch-and-re-read shape handles it unchanged, and no provider error code is inspected (Spec 2C
decision 5 holds). A create is still guarded by the `Name` primary key. It emits no DDL, so there is
no migration.

**The generalisable half:** when you copy a guard between two schemas, check what in the original was
actually load-bearing. Here the read looked like the guard and was not; the constraint was. A copy
that brings the read and leaves the constraint behind is green everywhere and guards nothing.

## The governance envelope: one refusal, two outcomes

`ChangeRequestSet` persists rules first, then propositions, as two independent batches. Its rule half
could already return `VersionConflict`; its proposition half could only throw, and a throw after the
rule half committed was reported as `PersistenceDesynced` — the outcome that says *do not simply
retry*, because a retry re-prepares against the unchanged live rule version and collides with the row
this attempt already wrote.

Now the proposition half can refuse with a value too, and the same fork applies:

| Envelope | Proposition half refuses | Outcome |
|---|---|---|
| Propositions only | conflict | `VersionConflict`, naming the proposition and the version to re-base onto |
| Rules **and** propositions | conflict | `PersistenceDesynced` — the rule row is already durable |
| Rules **and** propositions | throws | `PersistenceDesynced` — unchanged |

**The outcome is chosen by what is already durable, not by how the store answered.** A conflict is an
ordinary, retryable refusal when nothing landed, and a desync when the rule half did — even though it
is the identical refusal in both rows. Writing the two branches to share one `Desynced` helper is what
makes that reading survive: previously the desync message was inline in a `catch`, which quietly
implied the outcome was *about* exceptions.

## Verification

Bundle spec 2 §7 lists "two replicas racing a write to the same rule/version: one 200, one 409" and
"a stale-base publish returns 409 with the current version". Both were covered for rules in
`DurabilityObligationsTests` and for propositions by nothing. They are now covered for propositions
too, plus the withdrawal case the rule side has no analogue for.

Ten conformance cases were added to `PropositionStoreConformance`, so every store — in-memory, JSON
file, EF/SQLite — is held to the same predicate.

### The mutation, and the gap it found

The slice's tests were written alongside its implementation rather than strictly before it, so the
guards were proved load-bearing by mutation instead: each store's conflict check was neutered *at its
call site* (`_ = FindConflict(batch);`) and the suites re-run.

Result: **8 failures in each of the three conformance derivations, and zero anywhere else.**

That zero was the finding. It meant the SDK-level paths this slice exists to enable — `PropositionSet`
turning a store refusal into `VersionConflict`, the governance envelope turning one into
`VersionConflict` or `PersistenceDesynced` — were reachable only through a store that never refused,
and so were asserted by nothing. Six tests were added to close it, and re-mutating confirms four of
them go red without the guard. The remaining two use a store double that always conflicts, so they
hold independently of any store's own logic — which is the point: they pin the *mapping*, not the
enforcement.

Two notes worth keeping:

- **A store-level mutation only exercises store-level tests.** If a slice's whole purpose is that a
  new outcome propagates, the mutation must be checked for what it *fails to* break, not only for what
  it breaks.
- **`PropositionSet.Load()` is once-only** (Spec 2B split the startup read from `RefreshAsync`
  deliberately). A test wanting a second replica at a known basis has to *construct* it at that
  moment; calling `Load()` again throws, and the first draft of these tests did.

## What a reader should not reconstruct from the diff

- The version predicate looks like two rules (`>` for saves, `==` for deletions) and is one: both are
  a compare-and-set against the stored version, differing only in whether the entry claims a new
  position or names an existing one.
- `>` rather than `== stored + 1` is load-bearing for the importer, not laxity.
- `PropositionBatch.FindConflict` is public API on purpose, and is the *contract* rather than a
  convenience — see the simplifier note below.

## The `code-simplifier` round

Recorded here because a shipped slice's review round is where the reasoning lives.

It was asked one loaded question: three near-identical `FindConflict` helpers had shipped in
`InMemoryPropositionStore`, `JsonFilePropositionStore` and `EfPropositionStore`, and the prompt handed
it the argument *for* leaving them — the rule side has the same shape, the three live in three
assemblies so sharing means widening public API, and CLAUDE.md cautions explicitly against
over-DRYing stores whose backing differs.

**It refused the framing, and it was right.** The precedent is not the same shape:
`InMemoryRuleStore.AppendAsync` walks a per-name list of log rows while `EfRuleStore.FindConflictAsync`
builds a taken-set and a per-name `highest` map from a projection query. Those two share a *statement*
and essentially no code; consolidating them would need exactly the branching abstraction the CLAUDE.md
caution is about. The three proposition scans were textually identical apart from one lookup
expression — no nuance to preserve, and the extracted method takes data, not a mode.

What settles it is that **this predicate is the contract**, documented as such on `WriteAsync`. Three
copies in three assemblies can silently disagree, and only the conformance suite would notice, and only
for the cases it happens to cover. So:

```csharp
public PropositionWriteResult? FindConflict(Func<string, int> storedVersion)
```

on `PropositionBatch`, with each store supplying only its lookup. Public API is a real cost and it
lands on the right side: `IPropositionStore` is an advertised extension point and `PropositionBatch`
was already public, so a third-party store previously had to reconstruct this predicate from prose.

**The generalisable half: "don't over-DRY" is about branching, not about repetition.** Two
implementations that share a sentence and no code should stay apart; three that share the code and
differ in one datum should not. The tell is whether the extracted thing takes a *mode* or a *value*.

One consequence worth naming, since it cuts the other way: the mutation proof above now has a single
point. Neutering the one helper reddens **12** tests in `Motiv.Serialization.Tests` and 8 in each of
the other two derivations — a stronger signal than before, but also a reminder that the conformance
suite is now the *only* thing holding three stores to one predicate rather than three things that
happened to agree.

Left alone, with reasons it gave: `EfPropositionStore.WriteAsync`'s length (it reads as one
transactional sequence, and splitting the apply loops out would hide that the tracked entities from
`RowsNamedByAsync` are the same objects being mutated — load-bearing for the concurrency token); the
`Desynced` helper; and — correctly — **the empty-batch early return, which stays duplicated in all
three stores.** It could have moved into the shared helper too. Each store's reason for it genuinely
differs (a counter, an mtime, a transaction round trip) and each carries its own comment saying so.
That is the case the CLAUDE.md caution is actually about, sitting three lines above the case it is not.
