# The reference persistence implementation — schema, migrations, backup

Type: grilling
Status: resolved
Blocked by: 09, 10

## Question

The abstractions come from 02 and 09; the version record from 10. This ticket is the *reference
implementation* the app ships to prove them — the concrete half of the two-sidedness rule.

The session must resolve:

1. **Which stack?** EF Core is the default expectation for an ASP.NET Core product, and
   `src/examples/Motiv.EntityFramework.Tests` shows EF is already exercised somewhere in this repo —
   check what that project actually does before assuming. The alternative is Dapper plus hand-written
   SQL, which for a store this narrow (load-all, save-one, delete-one) may genuinely be simpler.
2. **Which databases are supported?** Postgres and SQL Server are the enterprise floor; SQLite is
   what makes `docker compose up` work with no dependencies. Does the reference implementation
   target a provider or stay provider-agnostic — and does JSON column support (which both Postgres
   and SQL Server have, and which suits a `RuleDocument` exactly) leak into the schema?
3. **The schema.** Rules, propositions, versions, drafts, approvals, grants, decision log. Which are
   separate tables, and does the decision log belong in the same database *at all* — its volume
   profile is completely different from everything else.
4. **Migrations.** An enterprise upgrading the app must migrate its schema. EF migrations shipped in
   the package, SQL scripts, or an idempotent bootstrap? And what is the story when the SDK adds a
   field — does the adopter's fork of the reference implementation break?
5. **Backup, restore, and disaster recovery.** Mostly the adopter's problem, but the product must
   document what is safe to back up and whether a partial restore can leave the binding scope
   inconsistent.
6. **Does the app ship a migration path from the current state?** `JsonFilePropositionStore` writes a
   file; existing demo users have one. A one-way importer may be worth an hour.

Feeds the fog patch "health/readiness probes, configuration, and secrets".

## Grounded in the code

- **The existing EF project is not a persistence precedent.** `Motiv.EntityFramework.Tests`
  (`QueryTranslationTests.cs`) proves *spec-to-SQL query translation* — `Spec.From(...)` pushed down
  into an `IQueryable` `WHERE` clause against a `CustomerDbContext` of domain rows. There is no store,
  no persisted document. EF Core *the library* is a proven, referenced dependency (SQLite provider);
  there is **no store implementation to reuse**. Sub-1's caution was warranted.
- **The store is a "dumb sink."** `IPropositionStore`'s own remarks: *"validates nothing and enforces
  no invariants. Legality is decided by `PropositionSet` before anything reaches here."* This decides
  the schema shape below more than any provider choice does.
- **The contract in code is still the pre-09 synchronous one** (`Load`/`Save`/`Delete`). This ticket
  implements against ticket 09's *decided* async contract, not the current file.
- **Only propositions are file-persisted today** — `JsonFilePropositionStore` exists; there is no
  `JsonFileRuleStore`. So sub-6's importer is propositions-only.

## Answer

**EF Core for the authoring store — three providers, document-as-text, mapped as a thin dumb sink. The
decision log is a *separate* sink in a *separate* database, never an EF table. Delivered as a derivable
`DbContext` with adopter-owned migrations (the ASP.NET Identity pattern). SQLite bootstrap keeps
`docker compose up` dependency-free.**

### Sub-1 — EF Core for authoring; raw sink for decisions

EF Core for the authoring store. It is human-rate (a few writes/minute), so EF's change-tracking and
LINQ overhead — the usual case *against* it — never bite, while its wins are concrete: **one entity
model migrated across three providers**, and it is the **least-surprising choice for the enterprise
.NET adopters who are the reference implementation's audience**. Dapper + hand-SQL was genuinely
weighed — the store is narrow enough (load-all / save-one / delete-one / get-scalar) that it would be
*simpler code* — and lost on provider-agnosticism and familiarity, not on capability.

**Used as a thin persistence layer, not a rich ORM:** entities are ~1:1 with rows, no navigation
properties, no cascade deletes, no relational constraints encoding binding legality — because the SDK
owns every invariant (the dumb-sink contract). EF here is typed SQL + a migration engine. **But "no
constraints" is too broad — see the risk analysis below; identity and structural constraints are kept.**

**The decision log is explicitly *not* EF.** Its machine-rate append profile is exactly what EF change
tracking is wrong for, and ticket 09 already split `IDecisionSink` off the authoring path. It ships as
a distinct raw-append sink → sub-3.

### Sub-2 — three providers, document stored as text (not native JSON)

Provider-agnostic over **SQLite (dev default), Postgres, and SQL Server** (the enterprise floor). One
schema, one entity model, provider selected by configuration.

`DocumentJson` is stored as a **portable `text` / `nvarchar(max)` string, not a native `jsonb`/`json`
column.** The decisive fact: the dumb sink **never queries *into* the document** — it loads whole
documents and binds them in-process, never `WHERE document->>'field' = …`. So native-JSON indexing and
path queries buy nothing, while a native column would **fork the schema per provider** for zero
functional gain. Portability wins because the query capability it costs is capability we never use. (An
adopter wanting to *report* on document contents in SQL can switch the column type in their fork — an
explicit customization, not the reference default.)

### Sub-3 — the schema, and the decision log is a separate database

**Authoring `DbContext`** (one consistent unit, one backup unit):

| Table | Shape | Source |
|---|---|---|
| `StoredRule` | `(Name PK, ModelType, MetadataType, IsAsync, IsPolicy, Version, Description, DocumentJson?)` | ticket 02 head row = `RuleSetEntry` |
| `RuleVersion` | `(Name, Version, DocumentJson?, Author, TimestampUtc, ChangeNote?, ApprovalRef?, BuildId?)`, PK `(Name, Version)` | ticket 10 append-only log |
| `StoredProposition` | `(Name PK, ModelType, DocumentJson, Version, Description)` | ticket 02 |
| `PropositionVersion` | same shape as `RuleVersion` | ticket 10 symmetry |
| `Grant` | `(Subject, Prefix, Verb)` | ticket 12 app-owned source, **only when active** |
| `StoreGeneration` | single row `(Id=0, Generation bigint)` | ticket 20 |

`DocumentJson` **nullable and meaningful** (null = "on the compiled default"); it must never collapse to
an absent row (ticket 02). Head row + version append + generation bump are **one transaction** (ticket
10, inside ticket 09's gate) — an artefact's head and its history never disagree, and a reader that
sees generation N sees all writes ≤ N.

**The decision log does not belong in this database.** It is a separate sink, a separate connection,
and may target a **different database or engine** entirely. Reasons: its volume is orders of magnitude
higher (machine-rate vs human-rate), so a decision-write storm would degrade authoring reads if
co-located; its retention differs (versions kept forever per ticket 10; decisions likely a compliance
window); and ticket 10 already established the three-records model in which the decision log merely
*references* version history. Ship it as ticket 15's `IDecisionSink` against its own store. **Provisional,
pending their tickets:** `Draft` (ticket 11) and `ChangeRequest`/`Approval` (ticket 13) tables are named
here but their columns are those tickets' calls, not settled now.

### Sub-4 — migrations: the Identity pattern, adopter-owned

Ship a **derivable `MotivStoreDbContext`** with entity configurations in a rules-stack package
(`Motiv.Serialization.EntityFrameworkCore`, 0.x, unpublished until the stack ships — ticket 06). The
adopter **generates and owns their migrations** against a context they may derive to add columns —
exactly how `Microsoft.AspNetCore.Identity.EntityFrameworkCore` works.

This dissolves the sub-question's worry — *"does the adopter's fork break when the SDK adds a field?"*:
- Custom adopter columns live on their **derived context**, so a new SDK migration never conflicts.
- An SDK field addition is a new property on a `Stored*` record → the adopter's mapping fails to
  **compile** until they map it. A **loud, compile-time** break, which is the correct failure mode for
  a schema change, not a silent runtime drift.

Dev (SQLite) uses an **idempotent `EnsureCreated` bootstrap** so `docker compose up` needs no migration
step; production applies migrations (startup-behind-a-flag or `dotnet ef database update`).

### Sub-5 — backup, restore, DR

Mostly the adopter's problem; the product documents the safe boundaries:
- The **authoring DB is one consistent backup unit** (rules, versions, propositions, grants,
  generation). The **decision log is a separate unit** with its own retention.
- **Hazard, documented loudly:** a restore must **not move `StoreGeneration` backwards while replicas
  are live** — a client holding a newer fencing token (ticket 20) would observe the store regress,
  breaking monotonic-read consistency. Restore is a full-stop operation, not online.
- A restored document that no longer binds against the deployed build **quarantines on next load**
  (ticket 02) — restore safety is coupled to code/document version match, and the app's fail-fast
  policy (ticket 02) decides whether that stops startup.

### Sub-6 — migration from the current state

Ship a **one-way, propositions-only importer** in Motiv.Studio: read the `JsonFilePropositionStore`
file, `SaveAsync` each into the EF store, done — ~an hour, and it is the only bridge existing demo
users need (rules were never file-persisted). One-way by design; no attempt to keep the file in sync.

## Risk — what "no constraints" actually costs, and the mitigations

"Dumb sink → no constraints" was overbroad. There are **three kinds of constraint with three risk
profiles**, and only two of them are the dumb-sink argument's to omit. The unifying question is *"what
writes to this table that is not `PropositionSet`/`RuleSet`?"* — a DBA, a second replica, a migration
job, a botched restore, a hand-edit (the codebase's `namespaceTree.ts` comment already admits a
*"quarantined hand-edited store"* exists). A DB constraint is the one guarantee that survives the SDK
being bypassed; the SDK's in-memory invariants protect nothing an out-of-band writer does.

**Portability never required dropping constraints.** PK / unique / `NOT NULL` / `CHECK` are all fully
portable across the three providers. The only portability-motivated calls were document-as-`text` (not
a constraint) and no *cross-database* FKs (impossible anyway).

| Kind | Example | SQL-expressible? | Verdict | Mitigation |
|---|---|---|---|---|
| **Identity / structural** | PK on `Name`; `NOT NULL` on `Name`/`Version`/`ModelType`; `(Name,Version)` PK; `CHECK Version ≥ 0` | Yes, portably | **KEEP** (was wrongly swept out) | They *are* the mitigation — free, and the SDK already assumes them |
| **Cross-aggregate / cross-DB referential** | decision-log → `RuleVersion` (separate DB); `ApprovalRef` → `ChangeRequest` | Only within a DB | **OMIT across boundaries; keep intra-aggregate** | Soft-reference validation at write; ticket 10's "can't prune a referenced version" in app code |
| **Semantic / binding** | "document binds"; "null doc ⟺ compiled default" | **No** (needs the binder) | SDK-only | **Quarantine-on-load** (ticket 02) — revalidated every `Load()` |

### The failure scenarios and their fixes

1. **Duplicate / identity-less rows** (no PK/unique/NOT NULL) → `Load()` builds its Ordinal dictionary
   and the wrong document silently wins. **Keep the identity constraints** — "broken sink", not "dumb
   sink". The `(Name,Version)` PK also **doubles as the cross-replica append guard** (racing appends:
   one wins, one hits a unique violation and retries), pre-empting part of ticket 21.
2. **Head/log divergence** — nothing enforces `StoredRule.Version == max(RuleVersion.Version)`; a
   partial restore or out-of-band write makes "v5" resolve to a document that isn't what v5 said. **The
   strongest mitigation is structural: do not store current version/document as mutable columns that
   duplicate the top of the log — derive them.** `StoredRule` becomes a **slim identity table**
   (`Name`, `ModelType`, `MetadataType`, `IsAsync`, `IsPolicy` — constant across versions); current
   `(Version, DocumentJson, Description)` is **projected from `max(RuleVersion.Version)`**. Divergence
   becomes *unrepresentable*, not merely detected. **This decides the head-vs-projection latitude ticket
   16 was given, and feeds a refinement to ticket 02's `(Name, Version, DocumentJson?)` head row: Version
   and DocumentJson are projections of the log, not stored duplicates.** The startup greatest-n-per-group
   query is trivial at these row counts.
3. **Orphaned references** — cross-*database* ones (decision-log → version) can't be FKs, so they are
   soft references + the prune rule; but intra-aggregate FKs that don't cross ticket 02's boundary
   (`RuleVersion.Name` → `StoredRule.Name`) are cheap and portable — **keep those**. The omission is
   "no FKs across the aggregate/DB boundaries", not "no FKs".
4. **Semantic corruption from out-of-band writes** — the class SQL cannot guard. **Quarantine-on-load
   (ticket 02) is the compensating control**: legality is re-derived on every `Load()`, so bad data is
   contained and surfaced (fail-fast per app policy) rather than silently bound. Extend the startup
   check to the structural invariants a projection doesn't already moot (no duplicate names, monotonic
   versions).
5. **The out-of-band writer itself** — least-privilege credentials so the **app is the single writer**
   (DBAs read-only outside migrations); document that any direct write must bump `StoreGeneration` or
   replicas silently skew (ticket 20's token only works if every writer bumps it); implement optimistic
   concurrency as a **conditional `UPDATE … WHERE Version = @expected`** checking affected rows — correct
   across replicas with no added constraint.

**Through-line:** structural risks are *designed out* (projection + kept identity constraints), semantic
risks are *caught on ingress* (quarantine), operational risks are *bounded by* single-writer
least-privilege — almost none of it at the cost of portability.

## Downstream

- **To fog "health/readiness probes":** readiness = the store answers `GetGenerationAsync()`; the EF
  connection is the natural probe target.
- **To ticket 15:** the decision log is a *separate database/sink*, not a table here — 15 owns its
  schema and retention.
- **To tickets 11 / 13:** `Draft` and `ChangeRequest` tables are provisional; settle their columns when
  those tickets resolve, then finalise the migration.
- **To ticket 06:** `Motiv.Serialization.EntityFrameworkCore` is a new rules-stack package on the 0.x
  train, unpublished until the stack ships.

## Inherited from ticket 02

- Two tables, not one: `StoredRule(Name, Version, DocumentJson?)` and the existing
  `StoredProposition(Name, ModelType, DocumentJson, Version, Description)`. They are never written in
  the same transaction, so they need no shared unit of work.
- `DocumentJson` is **nullable** for rules and the null is meaningful ("on the compiled default at
  this version") — it must not be collapsed to an absent row.
- The app owns the **fail-fast policy** over quarantine: whether any quarantined rule should stop
  startup. The SDK only provides the quarantine mechanism.

## Inherited from ticket 09

- The store contract is **async** on the write path (`SaveAsync` / `DeleteAsync`), so the reference
  implementation uses EF Core's async APIs. This removes the Cosmos-style "async-only SDK" objection
  entirely.
- `Load` remains synchronous at startup — `Func<IServiceProvider, T>` has no async form — so the
  implementation needs both a sync startup read and an async read for ticket 20's refresh.
- Ticket 20 may require the store to expose a change signal (version column, etag, change feed).
  Decide 20 before finalising the schema.

## Inherited from ticket 20

- The store grows a **store-derived monotonic generation** — one column/sequence serving three jobs:
  refresh triggering, the client-facing fencing token, and possibly write coordination (ticket 21).
  It must be store-side, not replica-local, or cross-replica comparison is meaningless.
- Three reads, not one: synchronous `Load()` (startup), `LoadAsync()` (refresh), and a cheap
  `GetGenerationAsync()` (polling). The poll must **not** re-read the store — only the scalar.
- The AspNetCore package ships an opt-in `IHostedService` poller; the reference implementation must
  expose whatever it needs to poll cheaply.
