# Spec 2C — The EF Core Reference Store — Design

**Date:** 2026-08-18
**Status:** Approved (design)
**Source:** Build step 5 of bundle spec [2 — Durability & Data](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/2-durability-and-data.md),
resolving ticket [16](https://github.com/karlssberg/Motiv/issues/116). Follows
[#126](https://github.com/karlssberg/Motiv/pull/126) (Spec 2B — Multi-Instance Refresh), which named
this slice "plan 2C". Build step 6 (ticket [21](https://github.com/karlssberg/Motiv/issues/121))
already landed inside 2A — see "Ticket 21 is already done" below.

## Summary

Every store in the tree today keeps its data in a process or a file. `InMemoryRuleStore` forgets
everything on restart; `JsonFileRuleStore` survives a restart but says so itself: *"not atomic across
processes — two processes appending at exactly the same instant can both read a stale file — so it is
a sample store, not a production one."*

This slice ships the production one: `Motiv.Serialization.EntityFrameworkCore`, an EF Core authoring
store over SQLite, Postgres and SQL Server, where the `(Name, Version)` primary key is enforced by the
database rather than by a re-read of a file. The sample app switches to it by default, carrying its
existing rule history across with a store-to-store importer.

It closes the Durability & Data bundle.

## Ticket 21 is already done

The bundle's build sequence lists step 6 as "PK-as-CAS; delete the in-memory CAS". That work landed
early, inside 2A:

- `grep -rn "CompareExchange" src/ --include="*.cs"` returns nothing. The `Rule.cs:170` /
  `AsyncRule.cs:175` CAS the ticket targets no longer exists.
- `RuleAppendResult.Conflict(name, currentVersion)` exists and is the documented outcome of
  `IRuleStore.AppendAsync`.
- `StoredRule`'s own remarks state head-as-projection as a contract on every store: *"Never appended:
  every store derives this by projection from the highest `Version` in the log."*

So step 6 contributes no new code to 2C. It contributes **verification obligations** — the racing-writers
and stale-base tests below — which is the only part of it that was ever going to need a real database.

## Decisions (locked)

1. **Map the contract as it stands.** 2C implements today's two interfaces exactly: an append-only
   `RuleVersion` table for rules, a plain upsert table for propositions. No SDK contract change.

   `IPropositionStore.WriteAsync` returns bare `Task` with no conflict outcome and no version log,
   while `IRuleStore.AppendAsync` returns `RuleAppendResult` over an append-only log. Ticket 16's
   schema lists a `PropositionVersion` table and ticket 21 sub-5 says both stores get PK-guarded
   appends, so this asymmetry is a real gap — but closing it is a **breaking change to
   `IPropositionStore`** touching `PropositionSet`, both file stores and the in-memory store. It gets
   its own spec rather than riding along inside a new package's first release.

2. **SQLite proven; Postgres and SQL Server buildable.** The conformance suite runs against SQLite
   only. The other two providers ship as referenced packages with an in-process DDL-generation test.
   This is sound *only because* conflict detection inspects no provider error codes — see decision 5.

3. **The sample switches to EF/SQLite by default.** Two-sidedness is a standing constraint of the
   map: every capability lands as an SDK abstraction plus a reference implementation in the app. The
   JSON stores stay in the tree as the importer's source and as a second `IRuleStore` implementation
   for the conformance suite.

4. **The importer covers both stores, rules with full history.** Ticket 16 scoped it
   propositions-only on the stated premise that *"there is no `JsonFileRuleStore`"*. 2A invalidated
   that premise. Switching the default while abandoning the rule log would discard the audit trail an
   approval gate depends on — the exact outcome `JsonFileRuleStore`'s own remarks call indefensible.

5. **Conflict detection is provider-agnostic.** No SQLite `1555` / Postgres `23505` / SQL Server
   `2627` error-code inspection anywhere.

6. **One conformance suite, three stores.** The store contract becomes abstract base test classes
   that `InMemoryRuleStore`, `JsonFileRuleStore` and the EF store all derive from.

7. **A uniform generation counter row for both stores**, rather than deriving the rule generation
   from the append-only log. Chosen for one mechanism and one mental model over two.

## Why the conformance suite is the load-bearing part

`InMemoryRuleStore`'s remarks make a claim 2C depends on entirely:

> Real, not a stub — it implements the same primary key, so the conflict path this store produces is
> the one a database store produces, and a test written against it holds against Postgres.

It is currently a comment, not a mechanism: all seven tests in `InMemoryRuleStoreTests` construct
`InMemoryRuleStore` directly. 2C is the point where that claim either becomes structural or quietly
becomes false, because 2C is the first slice with a second serious implementation to hold it to.

Writing a parallel EF-specific suite instead would create two independent statements of one contract.
Ticket 21 deleted the in-memory CAS on precisely that reasoning — *"keeping two authorities for one
invariant is how they drift"* — and duplicating the test contract for convenience immediately after
would be inconsistent.

## Architecture

### `src/Motiv.Serialization.EntityFrameworkCore` (new)

Targets `net10.0` only, matching `Motiv.Serialization.AspNetCore` and the EF Core 10 packages already
pinned in `Directory.Packages.props`. Ships on the 0.x rules-stack train per ticket 06; unpublished.

**Row entities are distinct from the SDK records.** `MotivStoreDbContext` maps its own `RuleVersionRow`,
`PropositionRow` and `StoreGenerationRow` types, with explicit translation to and from
`StoredRuleVersion` / `StoredProposition`. Two reasons:

- `Motiv.Serialization` keeps no EF dependency, and the schema becomes an artefact this package owns.
- It delivers the loud compile-time break ticket 16 sub-4 asked for. The ticket assumed Fluent mapping
  would supply it, but it would not — EF conventions would silently map a newly added SDK property.
  Translating a *positional record* does break the build.

The context is derivable, following the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` pattern:
adopters derive it to add columns and own their migrations, so an SDK migration never conflicts with
adopter columns.

Both stores resolve `IDbContextFactory<MotivStoreDbContext>` and open a fresh context per operation.
This is required — the stores are registered as singletons and `DbContext` is not thread-safe — and it
structurally guarantees the two stores never share a transaction, which is a bundle-level invariant.

#### Schema

| Table | Columns | Key |
|---|---|---|
| `MotivRuleVersion` | `Name`, `Version`, `DocumentJson?`, `Author`, `TimestampUtc`, `ChangeNote?`, `ApprovalRef?`, `BuildId?` | PK `(Name, Version)` |
| `MotivProposition` | `Name`, `ModelType`, `DocumentJson`, `Version`, `Description?` | PK `Name` |
| `MotivStoreGeneration` | `Scope`, `Generation` | PK `Scope` |

`DocumentJson` is portable `text` / `nvarchar(max)`, not native `jsonb` — the sink never queries into
the document, so native JSON buys nothing and would fork the schema per provider.

`DocumentJson` is nullable and meaningful on `MotivRuleVersion` (`null` = "on the compiled default at
this version") and must never collapse to an absent row.

`MotivStoreGeneration` holds exactly two rows, `rules` and `propositions`. Distinct rows mean the two
stores never contend on the same lock, so the uniform mechanism costs nothing in practice. Within a
store the row does serialize concurrent appends to *different* rules — at a governance product's write
rate that is free, and it makes the conflict path deterministic rather than racy.

**Accepted cost of decision 7:** a counter can be forgotten. Any out-of-band writer that inserts a
version row without bumping `MotivStoreGeneration` leaves replicas silently skewed, because the
generation is the fencing token. Ticket 16 already assigns the mitigation — least-privilege
credentials making the app the single writer — and this is documented for both stores rather than
avoided for one.

**Kept constraints** (dumb-sink is *semantic*-only): PK on `Name`; PK `(Name, Version)`; `NOT NULL` on
`Name`, `Version`, `ModelType`. No FKs across the rule/proposition aggregate boundary, since the two
are never written together. No constraint encodes binding legality — that stays with `RuleSet`, and
quarantine-on-load revalidates it every `Load()`.

### `Motiv.Serialization.AspNetCore` (one additive change)

`AddPropositions` and `AddRuleStore` today take a pre-built instance and call
`AddSingleton<IRuleStore>(store)`. An EF store needs `IDbContextFactory` from the container, so both
gain a factory overload:

```csharp
public MotivRulesBuilder AddPropositions(Func<IServiceProvider, IPropositionStore> factory)
public MotivRulesBuilder AddRuleStore(Func<IServiceProvider, IRuleStore> factory, bool failFastOnQuarantine = true)
```

Purely additive; existing signatures and their called-twice guards are untouched. This is the only
change outside the new package.

### `Motiv.RulesEngine.Sample`

`AddMotivEntityFrameworkStore(options => options.UseSqlite(...))` registers the context factory and
both stores; the composition root calls the new factory overloads. The connection string comes from
`Motiv:Store:ConnectionString`, defaulting to a SQLite file under the content root. SQLite bootstraps
via `EnsureCreated`, so `docker compose up` needs no migration step.

**`docker-compose.yml` needs no change.** The demo service mounts no volume, so today's `rules.json`
is already ephemeral inside the container; a SQLite file there is parity, not a regression.

The `AddRefresh()` comment in `Program.cs` — *"The stores above reread their JSON files per
operation"* — is updated: an EF store opening a fresh context per operation gets the same property
more honestly.

## Data flow

### `AppendAsync` (the conflict path)

1. Open a context and begin a transaction.
2. Read the current max version for every distinct name in the batch, in one query.
3. If any `(Name, Version)` in the batch is already taken, return
   `RuleAppendResult.Conflict(name, currentMax)` without attempting an insert. This read is also what
   supplies `currentVersion` — an exception could not.
4. Otherwise insert every row, bump `MotivStoreGeneration['rules']`, and commit. All in one
   transaction: the batch is all-or-nothing, and the generation moves with the write it describes.
5. If another replica commits between steps 2 and 4, `SaveChanges` throws `DbUpdateException` and the
   transaction rolls back. Re-read: if a row we tried to insert now exists, it was a conflict; if not,
   the failure was something else and it rethrows.

Step 5 is why decision 2 is sound. The race path's correctness depends only on EF's own
`DbUpdateException` contract, not on provider-specific unique-violation behaviour, so proving it on
SQLite genuinely generalises. Had conflicts been detected by error code, "SQLite proven, others
buildable" would have been an unsound trade.

### `WriteAsync` (propositions)

One transaction: apply every save as an upsert keyed on `Name`, apply every delete, then bump
`MotivStoreGeneration['propositions']`. A name never appears in both lists, so no ordering question
arises. An empty batch writes nothing and does not bump. There is no conflict outcome here — the
contract has none (decision 1), and the upsert is last-writer-wins exactly as
`InMemoryPropositionStore` already is.

### `Load` / `LoadAsync`

Reads all version rows and projects the head in memory — the highest `Version` per `Name` — rather
than translating a greatest-n-per-group query, which differs across providers. Ticket 10 puts the
corpus at roughly 1,800 rows over five years, and ticket 16 already calls this query trivial at those
counts. `Load()` uses EF's synchronous APIs directly; never `.Result`.

### `GetGenerationAsync`

A single-row scalar read of `MotivStoreGeneration`. Never a store read — every replica polls it on a
timer.

### The importer

Needs no EF knowledge at all, so it is a generic store-to-store copy over the public interfaces:

- **Rules:** `from.Load()` yields the names; `from.HistoryAsync(name)` yields every version row;
  `to.AppendAsync(rows)` replays them preserving version numbers, authors, timestamps, change notes
  and approval refs. The `(Name, Version)` PK is identical in both stores, so this is a straight row
  copy.
- **Propositions:** `from.Load()` then `to.WriteAsync(new PropositionBatch(saves, []))`.

It ships in the EF package rather than `Motiv.Serialization`, to avoid committing SDK surface while
`IPropositionStore` is still expected to change (decision 1). It runs one-way behind an explicit
`Motiv:Store:ImportFromJson` flag and **refuses a non-empty target**, which makes a double import
impossible without tracking any import state.

## Error handling

| Situation | Behaviour |
|---|---|
| `(Name, Version)` already taken | `RuleAppendResult.Conflict(name, currentVersion)` — a value, not an exception |
| Concurrent append wins the race | Same conflict value, via the `DbUpdateException` re-read |
| `DbUpdateException` that is not a conflict | Rethrown — a disk-full error must never be reported as a version conflict |
| Connection failure mid-publish | Propagates before memory mutates; nothing goes live |
| Cancellation | Token passed to every async EF call; a hung store is escapable, which is ticket 09's whole reason for the async contract |
| Empty batch | No write, no generation bump — otherwise every replica rebuilds its world on a timer for nothing |
| Stored document no longer binds | Unchanged: quarantine stays SDK-side, `RuleSet` decides, the app owns fail-fast policy |

## Testing

**Conformance suite.** `RuleStoreConformance` and `PropositionStoreConformance` abstract base classes
with an abstract store-factory member, placed in `src/testing/` and linked into each test project —
the precedent `ShouldlyLineEndingExtensions.cs` already sets, avoiding a new shared project.
`InMemoryRuleStoreTests` becomes a derivation; `JsonFileRuleStore` and `EfRuleStore` derive from the
same suite. The seven existing rule behaviours become the definition of "is an `IRuleStore`": head
projection, null-document-as-head, duplicate `(Name, Version)` conflict, all-or-nothing batch,
generation moves on success, generation does not move on rejection, history in version order.

**`Motiv.Serialization.EntityFrameworkCore.Tests`** (new). Fresh temp SQLite file per test, so PK
enforcement and transactions are real rather than emulated. Needs the same `NuGetAuditSuppress` for
`GHSA-2m69-gcr7-jv3q` that `Motiv.EntityFramework.Tests` already carries.

**Provider DDL tests.** `GenerateCreateScript()` per provider — in-process, no server, no Docker —
proving the model maps and the DDL is producible for Postgres and SQL Server.

**Bundle verification obligations** (spec 2 §7), each an explicit test:

- A publish that validated then failed to persist leaves nothing live.
- Two writers racing the same rule and version: one appends, one gets `VersionConflict`; the log shows
  one published version.
- A stale-base publish returns the conflict carrying the current version.
- Quarantine fires on load for a stored document that no longer binds.
- The importer round-trips both stores, rule history included, and refuses a non-empty target.

**Regression.** The sample's tests translate their isolation from a temp `Rules:Path` file to a temp
SQLite connection string. The full solution suite and the UI e2e suite must stay green — per CLAUDE.md,
example-project tests assert on justification output and break easily.

## Build sequence

1. Conformance base classes in `src/testing/`; `InMemoryRuleStoreTests` and the proposition equivalent
   rewritten as derivations. Green before anything new exists.
2. New project, `MotivStoreDbContext`, row entities, translation, entity configuration.
3. `EfRuleStore` written against the conformance suite. TDD: the suite fails first.
4. `EfPropositionStore`, likewise.
5. `JsonFileRuleStore` derived from the same suite — a free second confirmation the suite is
   implementation-neutral.
6. Provider packages and the `GenerateCreateScript()` tests.
7. The store-to-store importer plus its round-trip test.
8. Factory overloads in `.AspNetCore`.
9. Sample wiring, test isolation translation, `Program.cs` comment updates.
10. Bundle verification obligations.
11. Docs.

## Explicitly out of scope

- **Proposition version log / PK-guarded proposition appends** (decision 1) — its own spec, because it
  breaks `IPropositionStore`.
- **A public `Motiv.Serialization.Testing` package.** The natural end state under two-sidedness, and
  the conformance suite is built to become it — but it commits public API surface for a contract still
  expected to move, and ticket 06 requires barrel curation before publishing.
- **Testcontainers coverage** for Postgres and SQL Server (decision 2).
- **`Draft` / `ChangeRequest` / `Grant` tables** — provisional in ticket 16, owned by tickets 11 and 13.
- **The decision log.** Ticket 16 is explicit that it is a separate sink in a separate database, never
  an EF table here. Ticket 15 owns it.
- **The `Motiv.Studio` rename** (ticket 108) — a separate slice; 2C wires the sample where it stands.
- **Migrations shipped in the package.** Adopters own theirs, per the Identity pattern; dev uses
  `EnsureCreated`.
- **Backup and DR tooling** — documentation only, per ticket 16 sub-5.

## Risks

| Risk | Mitigation |
|---|---|
| SQLite-only proof hides a provider difference | Conflict detection inspects no provider error codes, so the untested surface is the DDL, which the `GenerateCreateScript()` tests cover |
| The forget-to-bump hazard now applies to rules too (decision 7) | Documented for both stores; least-privilege single-writer credentials per ticket 16 |
| Switching the sample's default breaks e2e | Importer carries history across; test isolation translates one-for-one from temp file to temp database; full solution and e2e suites are gate conditions |
| Rewriting passing store tests as derivations could weaken them | Step 1 lands first and stays green with no production change beneath it, so any loss of coverage is visible before the EF store exists |
| `EnsureCreated` and migrations do not mix | Intentional and is the Identity pattern's own split: `EnsureCreated` for dev SQLite, adopter-owned migrations for production. Documented |
