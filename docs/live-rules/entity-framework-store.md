---
title: Entity Framework Core Store
---

[`IRuleStore`](durability.md) and [`IPropositionStore`](../propositions/IPropositionStore.md) are
seams the host supplies — Studio ships a JSON-file implementation, but a process that outlives a
container or runs more than one replica wants a real database behind them.
`Motiv.Serialization.EntityFrameworkCore` is that implementation: one EF Core schema, `EfRuleStore`
and `EfPropositionStore`, backing both interfaces over SQLite, PostgreSQL or SQL Server.

```bash
dotnet add package Motiv.Serialization.EntityFrameworkCore
```

## Registering a Store

```csharp
public static IServiceCollection AddMotivEntityFrameworkStore(
    this IServiceCollection services, Action<DbContextOptionsBuilder> configure);

public static IServiceCollection AddMotivEntityFrameworkStore<TContext>(
    this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    where TContext : MotivStoreDbContext;
```

The non-generic overload registers `MotivStoreDbContext` itself &mdash; the zero-config path, where
the SDK's schema is the whole schema. The generic one registers a context
[the adopter derived](#migrations), which is what production wants. `configure` selects the provider
in both &mdash; pick one:

```csharp
// SQLite
builder.Services.AddMotivEntityFrameworkStore(options =>
    options.UseSqlite("Data Source=motiv-store.db"));

// PostgreSQL
builder.Services.AddMotivEntityFrameworkStore(options =>
    options.UseNpgsql("Host=localhost;Database=motiv"));

// SQL Server
builder.Services.AddMotivEntityFrameworkStore(options =>
    options.UseSqlServer("Server=localhost;Database=motiv"));
```

This registers `IDbContextFactory<MotivStoreDbContext>` &mdash; a factory, not a scoped `DbContext`,
because `EfRuleStore` and `EfPropositionStore` are singletons and `DbContext` is not thread-safe. Wire
both stores from that one factory:

```csharp
builder.Services.AddMotivRules(registry, options)
    .AddPropositions(provider => new EfPropositionStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddRuleStore(provider => new EfRuleStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddRule<CanCheckoutRule>();
```

A fresh `DbContext` is opened per store operation. That is what keeps the rule store and the
proposition store out of one another's transactions &mdash; [they are never written together](durability.md#remarks)
&mdash; and lets two application processes over the same database behave exactly like two replicas,
which is what [multi-instance refresh](../multi-instance/index.md) relies on.

## The Schema

Three tables, one `MotivStoreDbContext`:

| Table                 | Maps to                                    | Purpose                                              |
|------------------------|---------------------------------------------|-------------------------------------------------------|
| `MotivRuleVersion`     | `RuleVersionRow` / `StoredRuleVersion`      | The append-only rule version log.                    |
| `MotivProposition`     | `PropositionRow` / `StoredProposition`      | Authored propositions, one row per name.             |
| `MotivStoreGeneration` | `StoreGenerationRow`                        | Where each store stands &mdash; one row per scope.   |

**`MotivRuleVersion`** is [the version log](durability.md#the-version-log) already documented for
`IRuleStore` in general, persisted as-is:

- **`(Name, Version)`** is the primary key, enforced by the database rather than by re-reading a
  file &mdash; this is the cross-process compare-and-set two replicas race on.
- **`DocumentJson` is nullable, and null is meaningful.** A null value means "reverted to the
  compiled default at this version," never an absent row. A migration or a hand-written script that
  collapses a null `DocumentJson` into skipping the row destroys that distinction &mdash; a revert
  becomes indistinguishable from a rule that was never authored.
- **`Author`, `TimestampUtc`, `ChangeNote`, `ApprovalRef` and `BuildId`** are the provenance columns,
  carried straight through from `RuleChangeProvenance`.

**`MotivProposition`** holds one row per authored proposition, replaced in place on every save
&mdash; there is no version log on this side, and no conflict outcome; see
[the rule-side asymmetry](../propositions/IPropositionStore.md#the-asymmetry-with-irulestore) for why
that is deliberate and what closing it would cost.

**`MotivStoreGeneration`** holds two rows, keyed by scope (`"rules"` and `"propositions"`), because
the two stores share no sequence &mdash; a rule publish never bumps the propositions generation, and
vice versa. `GetGenerationAsync()` on either store reads its own scope only. This is the fencing
token [multi-instance refresh](../multi-instance/index.md) polls.

Constraints in `OnModelCreating` are identity and structure only &mdash; primary keys and `NOT NULL`.
Nothing here encodes binding legality; `RuleSet` and `PropositionSet` decide that, and quarantine
revalidates it on load.

## Migrations

`MotivStoreDbContext` is designed to be **derived**, the same split
`Microsoft.AspNetCore.Identity.EntityFrameworkCore` draws for `IdentityDbContext`:

```csharp
public class AppStoreDbContext(DbContextOptions<AppStoreDbContext> options)
    : MotivStoreDbContext(options)
{
    // add your own DbSets and OnModelCreating overrides here
}
```

- **Development** calls `EnsureCreatedAsync()` against `MotivStoreDbContext` directly. There is no
  migrations-history table to seed, no migration assembly to reference, and a fresh container starts
  from nothing:

  ```csharp
  await using var context = app.Services
      .GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()
      .CreateDbContext();

  await context.Database.EnsureCreatedAsync();
  ```

  **Two instances starting together race that call.** The first creates the database and begins
  issuing `CREATE TABLE`; the second sees a database that exists with no tables in it yet and issues
  the same statements, and one of them fails with "table already exists". A host that can start more
  than one instance against one store should verify the schema rather than trust the call &mdash;
  create it, then check that all three tables read, and only continue past a failure that left the
  schema complete. `StoreSchema` in `src/Motiv.Studio/StoreSchema.cs` is that
  guard, and is what Studio calls on startup; a bad connection string, an unwritable path and a
  permission error all still take the process down with their original exception.

- **Production** derives its own context (as above), registers it through the generic overload, and
  owns migrations for it with the usual `dotnet ef migrations add` / `Database.Migrate()` workflow:

  ```csharp
  builder.Services.AddMotivEntityFrameworkStore<AppStoreDbContext>(options =>
      options.UseNpgsql(connectionString));
  ```

  The generic parameter is load-bearing, not decoration. `EfRuleStore` and `EfPropositionStore` take
  `IDbContextFactory<MotivStoreDbContext>`, and that interface is *invariant* &mdash; an
  `IDbContextFactory<AppStoreDbContext>` is not assignable to it. This overload registers the
  adopter's factory *and* an adapter over it, so the two stores resolve exactly what they always did
  while every context they open is `AppStoreDbContext`. Both factories are registered, so
  `dotnet ef` and `Database.Migrate()` see the derived context they need. Because migrations are
  scoped to the adopter's derived context, an SDK schema change can never conflict with a column the
  adopter added.

**`EnsureCreated` and migrations deliberately do not mix.** `EnsureCreated` skips the migrations
history table entirely; calling it against a context that also has migrations leaves the database in
a state EF can't reconcile on the next `Migrate()`. Pick one path per environment and don't cross
them &mdash; `EnsureCreated` for a zero-config demo and local dev, migrations for anything that
persists past a container restart.

## Backup and Restore

The three tables are **one backup unit**. `MotivRuleVersion`, `MotivProposition` and
`MotivStoreGeneration` live in the same database precisely so a single backup captures a consistent
snapshot of both stores and the generations they were at &mdash; back up or restore the whole
database, never one table alone.

**A restore must never move a generation backward while replicas are live.** The generation column
is the fencing token behind [multi-instance refresh](../multi-instance/index.md)'s monotonic reads: a
replica that has already observed generation 9 will not rebuild from a store now claiming generation
4, because from its perspective that looks like no write happened at all, not like a write it needs
to catch up on. Restoring an old backup onto a live database silently strands every replica on stale
state until the generation counter climbs back past what they've already seen. Take replicas offline
(or take the whole application offline) before restoring anything that could move a generation
backward, and let a fresh `AddRefresh()` poll pick up the restored world only once every replica has
restarted against it.

## The Single-Writer Rule

The application is the only thing that should hold write credentials to these tables. Every write
through `EfRuleStore` or `EfPropositionStore` bumps the matching `MotivStoreGeneration` row in the
same transaction as the data it describes &mdash; that pairing is what lets a replica trust "the
generation moved" as a proxy for "there's something new to load," without re-reading the whole store
on every poll.

An out-of-band writer &mdash; a DBA running a manual `UPDATE`, a data-fix script, a migration that
also seeds rows &mdash; that inserts or edits `MotivRuleVersion` or `MotivProposition` rows without
also bumping the generation leaves every replica silently skewed: the data changed, but nothing tells
a poller to notice. Grant the application the only write credentials to these tables, and keep DBA
access read-only outside of migrations, which own their own schema changes and aren't expected to
touch generation bookkeeping the same way.

## Importing from the JSON Stores

A deployment moving off file-backed stores (`JsonFileRuleStore`, `JsonFilePropositionStore`) carries
its history in with `StoreImport`:

```csharp
var imported = await StoreImport.CopyAsync(
    sourceRules: new JsonFileRuleStore(rulesPath),
    targetRules: app.Services.GetRequiredService<IRuleStore>(),        // the EF store
    sourcePropositions: new JsonFilePropositionStore(propositionsPath),
    targetPropositions: app.Services.GetRequiredService<IPropositionStore>(),
    cancellationToken);

imported.Imported;      // false if the target already held anything — nothing was copied
imported.RuleVersions;  // rows replayed
imported.Propositions;  // rows copied
```

The sample wires this behind a configuration flag, so it can be left on in every environment without
risk:

```jsonc
// appsettings.json
{ "Motiv:Store:ImportFromJson": true }
```

```csharp
if (builder.Configuration.GetValue("Motiv:Store:ImportFromJson", false))
{
    var imported = await StoreImport.CopyAsync(/* ... */);
}
```

- **One-way.** The copy runs source-to-target only; there is no export path back out of the EF store.
- **Refused once the target holds anything.** `CopyAsync` checks both target stores before writing a
  single row: if either already has data, it returns `Imported: false` and copies nothing. That
  makes the flag idempotent &mdash; leaving it enabled after a successful import is harmless, because
  the second and every later startup are no-ops.
- **The whole version log, not just the head.** The importer replays every `StoredRuleVersion` for
  each rule name, not only its current version, so the imported store's audit trail matches the
  source's &mdash; a head-only copy would restamp every rule as authored at import time.
- **All of a rule's history or none of it &mdash; per name, not per import.** Each rule's history is
  appended in a single `AppendAsync` call, so a name either arrives complete or not at all. The
  import as a whole is *not* atomic and cannot be: the two stores are
  [never written in the same transaction](durability.md#remarks), and the rule side is one call per
  name.
- **A failure part-way through throws, and says so.** Propositions are copied first, then the rules
  one name at a time, so the rule store &mdash; the side the refuse-check reads first &mdash; is the
  last thing to become non-empty. If anything fails after the first write, `CopyAsync` throws an
  `InvalidOperationException` naming how much landed and saying the target is now *partially
  imported*. That is not a state a retry repairs: the target is no longer empty, so the next run is
  refused and reports `Imported: false`, which is indistinguishable from the benign already-done
  case. Empty the target &mdash; drop and recreate its tables &mdash; and import again. Everything
  fallible that can be done before the first write is done first, so a source that cannot be read
  fails while the target is still untouched.

## Remarks

- **Portable text, not native JSON columns.** `DocumentJson` and proposition documents are stored as
  plain text, not a provider's native `jsonb`/`json` type &mdash; the store never queries into a
  document, so a native JSON column would buy nothing and would fork the schema per provider.
- **A fresh `DbContext` per operation**, not a pooled or scoped one, both because the stores are
  singletons and because it structurally guarantees the rule and proposition stores never share a
  transaction.
- **Conflict detection inspects no provider error code.** `EfRuleStore.AppendAsync` reads the
  versions already taken inside its transaction to build a `Conflict` result, and only falls back to
  catching `DbUpdateException` for the race where another replica commits between that read and the
  insert &mdash; the only behaviour relied on there is EF's own, which is what lets the same store
  code run unmodified against SQLite, PostgreSQL and SQL Server.

## Next Steps

- See [Rule Durability](durability.md) for the `IRuleStore` contract this store implements, and the
  version log, quarantine, and rollback semantics that apply regardless of which store backs them.
- See [`IPropositionStore`](../propositions/IPropositionStore.md) for the proposition-side contract.
- See [Multi-Instance Refresh](../multi-instance/index.md) for how the generation columns this store
  maintains drive `AddRefresh()`.
