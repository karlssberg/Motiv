# Bundle Spec — Durability & Data

Status: draft — synthesis of resolved decisions; no new architecture.
Source tickets: [02](../issues/02-rule-persistence-seam.md) · [09](../issues/09-store-contract-under-publish-lock.md) · [10](../issues/10-version-history-and-rollback.md) · [16](../issues/16-reference-persistence-implementation.md) · [20](../issues/20-multi-instance-refresh.md) · [21](../issues/21-cross-process-write-coordination.md)

## 1. Capability

Durable, versioned, multi-instance-safe storage for rules and propositions, behind a seam thin enough
that the SDK owns every invariant and the store stays a **dumb sink** for *semantic* legality while a
*structural* constraint (a primary key) provides concurrency. Authoring is async and cancellable;
history is append-only and permanent; replicas converge and never silently lose a write.

## 2. SDK surface (`Motiv.Serialization`)

### The seam (02)
- `IRuleStore` beside `IPropositionStore` — **two symmetrical stores, never written in the same
  transaction** (no shared unit of work). Records:
  - `StoredRule(Name, Version, DocumentJson?, …identity facts)` — `DocumentJson` **nullable and
    meaningful** (`null` = "on the compiled default at this version"); must never collapse to an absent
    row.
  - `StoredProposition(Name, ModelType, DocumentJson, Version, Description)`.
- A stored document that no longer binds is **quarantined**; the *fail-fast policy* over quarantine is
  the app's (the SDK provides only the mechanism).

### The async write contract (09)
- **Two-tier exclusion**: an outer `SemaphoreSlim` on `BindingScope` serialises whole operations
  await-safely; the existing inner `Monitor` is left untouched for data-structure mutation. A pure swap
  self-deadlocks (all five `Enrol`/`Withdraw` sites are reentrant), so the layers are not merged.
- The authoring write path is **async**: `SaveAsync` / `DeleteAsync`; `Load()` stays synchronous at
  startup. Rationale is **cancellation** (`WaitAsync(ct)`), not thread-time.
- **Invariant**: the outer gate is acquired only at public entry points; nothing inside may call one
  (`SemaphoreSlim` is not reentrant). Ordering: **bind → check dependents → persist → mutate memory →
  commit** — everything fallible runs before anything mutates.

### Version history (10)
- An **append-only log of immutable version rows**, one per published change:
  `(Name, Version, DocumentJson?, Author, TimestampUtc, ChangeNote?, ApprovalRef?, BuildId?)`,
  PK `(Name, Version)`. Symmetric for propositions.
- Version is **both** a permanent identity (each number names one immutable row) **and** the
  head-is-max concurrency token.
- **Rollback appends** (restoring vN writes vN+1) — records that a rollback happened; consistent with
  `Revert` already moving forward.
- Kept **forever** (~1,800 docs / 5 yr is trivial); future pruning bounded by decision-log references.
- Three provenance anchors identify behaviour: **stored document + `BuildId`** (compiled specs can't be
  fingerprinted) **+ referenced proposition versions** (the replay pin — a fact of the *evaluation*,
  recorded in the decision log, not the version row).

### Multi-instance (20)
- **Snapshot isolation within a replica** (cross-rule straddle is incoherence, ruled out via the
  whole-overlay copy-and-swap `PropositionOverlay` documents); **eventual consistency across replicas,
  made detectable**.
- `RefreshAsync()` in `Motiv.Serialization`; an opt-in `IHostedService` poller in `.AspNetCore` polls a
  cheap **store-derived monotonic generation** and rebuilds only when it moves.
- That same generation is the client-facing **fencing token** giving monotonic-read consistency.
- Three reads: sync `Load()` (startup), `LoadAsync()` (refresh), cheap `GetGenerationAsync()` (poll —
  must not re-read the store).

### Cross-process write coordination (21)
- The version log's **`(Name, Version)` PK *is* the cross-process compare-and-set**: publishing v6 is
  `INSERT RuleVersion(Name, 6, …)`; two replicas at v5 both compute next=6, the PK lets one win and
  rejects the other — the lost update becomes impossible and the audit stays correct.
- **Remove the in-memory `Interlocked.CompareExchange`** (`Rule.cs:170` / `AsyncRule.cs:175`): redundant
  intra-process (the outer gate serialises) and blind inter-process. In-memory keeps the version
  *value*; *enforcement* moves to the PK.
- `SaveAsync` returns the **existing `RuleUpdateResult.VersionConflict`** — now produced by the PK
  violation, not the CAS. Optimistic over a write-lease (low write rate; the mechanism is nearly free).

## 3. App surface (`Motiv.Studio` + `Motiv.Serialization.EntityFrameworkCore`) (16)

- **EF Core** authoring store, mapped as a *thin dumb sink* (no navigation properties, no
  legality-encoding constraints). Three providers: **SQLite (dev) / Postgres / SQL Server**; the
  document is stored as **portable `text`, not native `jsonb`** (the sink never queries into it).
- **Head-as-projection**, not a stored duplicate: `StoredRule` is a slim identity table; current
  `(Version, DocumentJson, Description)` is projected from `max(RuleVersion.Version)` — divergence is
  *unrepresentable*, and this is what makes the `(Name,Version)` PK the concurrency guard.
- Head + version-append + generation-bump are **one transaction**.
- Migrations follow the **ASP.NET Identity pattern**: a derivable `DbContext`, adopter-owned migrations
  — custom columns never conflict, and an SDK field addition breaks at *compile time* (loud).
- **Kept constraints** (dumb-sink is *semantic-only*): PK, `NOT NULL`, unique `Name`, `(Name,Version)`
  PK, intra-aggregate FKs. Cross-aggregate/cross-DB FKs omitted with compensating controls;
  **quarantine-on-load** revalidates semantic legality on every `Load()`.
- SQLite `EnsureCreated` bootstrap for zero-config `compose up`; a one-way **propositions-only** importer
  from `JsonFilePropositionStore`.
- Ships as `Motiv.Serialization.EntityFrameworkCore` (0.x, its own train per ticket 06).

## 4. Invariants (must hold)

- The two stores are never in one transaction; each coordinates independently.
- `DocumentJson == null` is a meaningful state, never an absent row.
- Head never diverges from the version log (it is a projection).
- Everything fallible runs before anything mutates (a failed persist/conflict leaves nothing live).
- `GetGenerationAsync()` must be a scalar read, never a full store read.
- Restore must not move the generation backward while replicas are live (breaks the fencing token).

## 5. New machinery to build

- `IRuleStore`; the async `SaveAsync`/`DeleteAsync`/`LoadAsync`/`GetGenerationAsync` contract; the outer
  `SemaphoreSlim` on `BindingScope`.
- The append-only version log + head projection; rollback-appends.
- `RefreshAsync` + the `IHostedService` poller + the generation column.
- The EF reference store (3 providers, derivable `DbContext`, migrations) + the propositions importer.
- PK-violation → `VersionConflict` mapping; removal of the in-memory CAS.

## 6. Build sequence

1. `IRuleStore` + records + quarantine (02).
2. Async two-tier exclusion + `SaveAsync` path (09).
3. Version log + head projection + rollback (10).
4. Generation + `RefreshAsync` + poller (20).
5. EF reference store + migrations + importer (16).
6. PK-as-CAS; delete the in-memory CAS (21).

## 7. Verification obligations

- A publish that validated then failed to persist leaves nothing live (ordering).
- Two replicas racing a write to the same rule/version: one 200, one 409; audit shows one published
  version + one rejected attempt.
- A stale-base publish returns 409 with the current version.
- Quarantine fires on load for a stored document that no longer binds; the app's fail-fast policy
  decides whether it stops startup.
- The propositions importer round-trips a `JsonFilePropositionStore` file into the EF store.
