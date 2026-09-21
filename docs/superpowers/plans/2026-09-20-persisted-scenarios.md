# Persisted Scenarios Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scenarios — a rule's named sample models — move from Studio tab memory into the backend, attached to a rule, with an optional expected verdict and a link back to the decision they came from.

**Architecture:** A new `IScenarioStore` seam in `Motiv.Serialization` (in-memory default, EF implementation in the same `DbContext`), exposed under `{basePath}/rules/{name}/scenarios` by `Motiv.Serialization.AspNetCore` when `AddScenarios` is called, with reads gated by `Read` and writes by `Author` on the rule's namespace. Not a version log and not governed: a scenario is test data. `@motiv-rules/core` gains the three client calls; Studio's scenario pane loads its rows from the store and writes back on add, edit, clone and delete; the four seeds move into Studio's startup seeding.

**Tech Stack:** C# / .NET 10 (`Motiv.Serialization` also targets net8, net9, netstandard2.0), EF Core, xUnit + Shouldly, the shared `src/testing/StoreConformance` suites; TypeScript, vitest, Testing Library, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md`, section 3 ("Persisted scenarios"). Slice 2 of 5.

## Global Constraints

- `Motiv.Serialization` targets `net8.0;net9.0;netstandard2.0;net10.0`: no `Index`/`Range`, no default interface methods.
- Every `dotnet` call needs `env -u MallocStackLogging -u MallocNanoZone` and the Bash sandbox disabled; `pnpm install` and Playwright web servers need the sandbox disabled too. Grep test output for `error CS` as well as `Failed`.
- Studio's typecheck needs both `@motiv-rules/core` and `@motiv-rules/react` dists built first: `pnpm -C ui/packages/rules-core build && pnpm -C ui/packages/rules-react build`.
- A scenario belongs to one rule. Writes need `GrantVerb.Author` on the rule's name; reads need `GrantVerb.Read`. No new grant verb, no approval gate.
- `StoredScenario.Version` is a compare-and-set: a put at `baseVersion == 0` creates and refuses an existing row; otherwise it must equal the stored version. A delete must name the stored version.
- The Studio pane's structure (region, table, roles, labels) does not change; the a11y sweep must stay green.
- The a11y sweep runs against stubs (`ui/apps/studio/e2e-a11y/stubs.ts`) with no .NET host; every new route the pane calls needs a stub.
- Branch: `claude/persisted-scenarios`, created from `claude/rules-engine-criticisms-5b8447` (slice 1, PR #265). The PR is opened with that branch as its base so GitHub retargets it to `main` once #265 merges.
- Plan and design doc (`docs/superpowers/specs/2026-09-20-persisted-scenarios-design.md`) land on the branch before the PR opens.

## Review Focus

1. Two Studio tabs (or two replicas) editing the same scenario: the second put with a stale base version must be refused with the current version, not silently win. Pinned by `Should_refuse_a_put_at_a_stale_version` (Task 1) on every store.
2. A put for a rule the caller may read but not author must be `403`, and a list for a rule the caller may not read must be `403` (grants are per namespace, so the refusal itself does not leak names). Pinned by `Should_refuse_a_put_without_author_grant` and `Should_refuse_a_list_without_read_grant` (Task 3).
3. A scenario whose model text is not JSON must be storable — Studio keeps a half-typed model verbatim — and must come back verbatim. Pinned by `Should_keep_the_model_text_verbatim_even_when_it_is_not_json` (Task 1) and by the endpoint accepting `model` as a string (Task 3).
4. Startup seeding must be idempotent: a second boot over the same database must not duplicate the seeds, and a rule the operator emptied on purpose must not be re-seeded. Pinned by `Should_seed_four_scenarios_per_rule_once` (Task 5): seeding writes a marker row per rule so an emptied rule stays empty.
5. The pane must render, and the run-all still work, when the scenarios endpoint is absent (an adopter mounted the rules API without `AddScenarios`): the list call answers `404` and the pane shows an empty table with a hint rather than a blank page. Pinned by `renders an empty table with a hint when the host has no scenario store` (Task 6).

---

### Task 1: The scenario store contract and the in-memory store

**Files:**
- Create: `src/Motiv.Serialization/Scenarios/StoredScenario.cs`
- Create: `src/Motiv.Serialization/Scenarios/IScenarioStore.cs`
- Create: `src/testing/StoreConformance/ScenarioStoreConformance.cs`
- Create: `src/Motiv.Serialization.Tests/Scenarios/InMemoryScenarioStoreTests.cs`
- Test project: `src/Motiv.Serialization.Tests`

**Interfaces:**
- Produces:
  - `public sealed record StoredScenario(string RuleName, string Id, string Name, string ModelJson, bool? ExpectedSatisfied, string? SourceDecisionId, int Version, string Author, DateTimeOffset TimestampUtc)`
  - `public sealed class ScenarioWriteResult` with `bool IsConflict`, `int CurrentVersion`, `int Version`, factories `Written(int version)` and `Conflict(int currentVersion)`.
  - `public interface IScenarioStore { Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken); Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken); Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken); Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken); }`
  - `public sealed class InMemoryScenarioStore : IScenarioStore`.
  - `PutAsync` ignores `scenario.Version`, `Author` is kept as given, `TimestampUtc` is stamped by the store; the written row's version is `baseVersion + 1`. Rows are ordered by `(RuleName, Id)` from `LoadAsync` and by insertion order (`TimestampUtc`, then `Id`) from `ForRuleAsync`.

- [ ] **Step 1: Write the conformance suite**

Create `src/testing/StoreConformance/ScenarioStoreConformance.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>What it means to be an <see cref="IScenarioStore"/>, as one suite every implementation derives from.</summary>
public abstract class ScenarioStoreConformance : IAsyncLifetime
{
    protected IScenarioStore Store { get; private set; } = null!;

    protected abstract Task<IScenarioStore> CreateStoreAsync();

    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    protected static StoredScenario Scenario(
        string rule, string id, string name = "Active adult", string model = """{ "age": 30 }""",
        bool? expected = null, string? sourceDecisionId = null, string author = "alice") =>
        new(rule, id, name, model, expected, sourceDecisionId, Version: 0, author, DateTimeOffset.MinValue);

    public async Task InitializeAsync() => Store = await CreateStoreAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    [Fact]
    public async Task Should_start_empty()
    {
        (await Store.LoadAsync(default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_create_at_version_one_and_read_it_back_for_its_rule()
    {
        // Act
        var written = await Store.PutAsync(Scenario("can-checkout", "s1", expected: true, sourceDecisionId: "d-1"), baseVersion: 0, default);

        // Assert
        written.IsConflict.ShouldBeFalse();
        written.Version.ShouldBe(1);
        var row = (await Store.ForRuleAsync("can-checkout", default)).ShouldHaveSingleItem();
        row.Id.ShouldBe("s1");
        row.Name.ShouldBe("Active adult");
        row.ModelJson.ShouldBe("""{ "age": 30 }""");
        row.ExpectedSatisfied.ShouldBe(true);
        row.SourceDecisionId!.ShouldBe("d-1");
        row.Version.ShouldBe(1);
        row.Author.ShouldBe("alice");
        (await Store.ForRuleAsync("other", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_stamp_the_timestamp_on_write()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await Store.PutAsync(Scenario("r", "s1"), 0, default);
        var row = (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem();
        row.TimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
        row.TimestampUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Should_update_at_the_stored_version_and_move_it_forward()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        var written = await Store.PutAsync(Scenario("r", "s1", name: "Renamed", author: "bob"), baseVersion: 1, default);

        written.IsConflict.ShouldBeFalse();
        written.Version.ShouldBe(2);
        var row = (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem();
        row.Name.ShouldBe("Renamed");
        row.Version.ShouldBe(2);
        row.Author.ShouldBe("bob");
    }

    [Fact]
    public async Task Should_refuse_a_put_at_a_stale_version()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);
        await Store.PutAsync(Scenario("r", "s1", name: "v2"), 1, default);

        var stale = await Store.PutAsync(Scenario("r", "s1", name: "stale"), baseVersion: 1, default);

        stale.IsConflict.ShouldBeTrue();
        stale.CurrentVersion.ShouldBe(2);
        (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem().Name.ShouldBe("v2");
    }

    [Fact]
    public async Task Should_refuse_creating_an_id_the_rule_already_holds()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        var again = await Store.PutAsync(Scenario("r", "s1", name: "again"), baseVersion: 0, default);

        again.IsConflict.ShouldBeTrue();
        again.CurrentVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Should_refuse_updating_an_absent_id()
    {
        var missing = await Store.PutAsync(Scenario("r", "nope"), baseVersion: 3, default);

        missing.IsConflict.ShouldBeTrue();
        missing.CurrentVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Should_scope_ids_to_the_rule()
    {
        await Store.PutAsync(Scenario("a", "s1"), 0, default);

        var other = await Store.PutAsync(Scenario("b", "s1"), 0, default);

        other.IsConflict.ShouldBeFalse();
        (await Store.LoadAsync(default)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_delete_at_the_stored_version()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        var deleted = await Store.DeleteAsync("r", "s1", baseVersion: 1, default);

        deleted.IsConflict.ShouldBeFalse();
        (await Store.ForRuleAsync("r", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_refuse_deleting_at_a_stale_version_or_an_absent_id()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        (await Store.DeleteAsync("r", "s1", baseVersion: 2, default)).IsConflict.ShouldBeTrue();
        (await Store.DeleteAsync("r", "s1", baseVersion: 0, default)).IsConflict.ShouldBeTrue();
        var absent = await Store.DeleteAsync("r", "nope", baseVersion: 1, default);
        absent.IsConflict.ShouldBeTrue();
        absent.CurrentVersion.ShouldBe(0);
        (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_keep_the_model_text_verbatim_even_when_it_is_not_json()
    {
        // Studio keeps a half-typed model as typed; the store is not the place to reject it
        const string halfTyped = "{ \"age\": 3";
        await Store.PutAsync(Scenario("r", "s1", model: halfTyped), 0, default);

        (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem().ModelJson.ShouldBe(halfTyped);
    }

    [Fact]
    public async Task Should_list_a_rules_scenarios_in_the_order_they_were_added()
    {
        await Store.PutAsync(Scenario("r", "b", name: "second"), 0, default);
        await Store.PutAsync(Scenario("r", "a", name: "first-by-id-but-added-later"), 0, default);

        (await Store.ForRuleAsync("r", default)).Select(s => s.Name).ShouldBe(["second", "first-by-id-but-added-later"]);
    }
}
```

Create `src/Motiv.Serialization.Tests/Scenarios/InMemoryScenarioStoreTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Scenarios;

/// <summary>The in-memory scenario store against the shared store contract.</summary>
public class InMemoryScenarioStoreTests : ScenarioStoreConformance
{
    protected override Task<IScenarioStore> CreateStoreAsync() =>
        Task.FromResult<IScenarioStore>(new InMemoryScenarioStore());
}
```

- [ ] **Step 2: Run to see it fail to compile**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryScenarioStoreTests" 2>&1 | grep -E "error CS|Passed!|Failed!" | sed -E 's/ \[.*//' | sort -u | head
```

Expected: `error CS0246` on `IScenarioStore`, `StoredScenario`, `InMemoryScenarioStore`.

- [ ] **Step 3: Write the record and result**

Create `src/Motiv.Serialization/Scenarios/StoredScenario.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One named sample model attached to a rule — a row of the rule's scenario table. Test data, not
/// behaviour: it never changes what a rule decides, so it is neither versioned as a log nor governed.
/// </summary>
/// <param name="RuleName">The rule the scenario belongs to.</param>
/// <param name="Id">Stable within the rule; chosen by the caller so a client can create without a round trip.</param>
/// <param name="Name">The row's label.</param>
/// <param name="ModelJson">The model as typed — kept verbatim, JSON or not, so an edit in progress survives.</param>
/// <param name="ExpectedSatisfied">Null for a sample to look at; set for a test to hold.</param>
/// <param name="SourceDecisionId">The logged decision this scenario was saved from, when it was.</param>
/// <param name="Version">The compare-and-set token: a put at 0 creates, otherwise it must equal the stored one.</param>
/// <param name="Author">Who last wrote it.</param>
/// <param name="TimestampUtc">When it was last written; stamped by the store.</param>
public sealed record StoredScenario(
    string RuleName,
    string Id,
    string Name,
    string ModelJson,
    bool? ExpectedSatisfied,
    string? SourceDecisionId,
    int Version,
    string Author,
    DateTimeOffset TimestampUtc);

/// <summary>The outcome of a scenario write: landed at a version, or refused as stale.</summary>
public sealed class ScenarioWriteResult
{
    private ScenarioWriteResult(bool isConflict, int version, int currentVersion)
    {
        IsConflict = isConflict;
        Version = version;
        CurrentVersion = currentVersion;
    }

    /// <summary>Whether the write was refused because the base version was not the stored one.</summary>
    public bool IsConflict { get; }

    /// <summary>The version the row is at after a successful write.</summary>
    public int Version { get; }

    /// <summary>The version the row is actually at when refused, or 0 when the store holds no such row.</summary>
    public int CurrentVersion { get; }

    public static ScenarioWriteResult Written(int version) => new(false, version, version);

    public static ScenarioWriteResult Conflict(int currentVersion) => new(true, 0, currentVersion);
}
```

- [ ] **Step 4: Write the interface and the in-memory store**

Create `src/Motiv.Serialization/Scenarios/IScenarioStore.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Where a rule's scenarios are kept. Rows are replaced in place under a per-row compare-and-set;
/// there is no version log, because a scenario is test data and a superseded sample is not a replay
/// anchor. Scoped by rule: an id is unique within its rule only.
/// </summary>
public interface IScenarioStore
{
    /// <summary>Every scenario of every rule.</summary>
    Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>The scenarios of one rule, in the order they were added.</summary>
    Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken cancellationToken);

    /// <summary>
    /// Creates (<paramref name="baseVersion"/> 0, refused when the id exists) or replaces
    /// (<paramref name="baseVersion"/> must equal the stored version) one scenario. The row's
    /// <see cref="StoredScenario.Version"/> is ignored; the written version is <c>baseVersion + 1</c>
    /// and the timestamp is the store's.
    /// </summary>
    Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken cancellationToken);

    /// <summary>Removes one scenario, when <paramref name="baseVersion"/> is the stored version.</summary>
    Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken cancellationToken);
}

/// <summary>The default store: scenarios live for the lifetime of the process.</summary>
public sealed class InMemoryScenarioStore : IScenarioStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Rule, string Id), StoredScenario> _rows = new();
    private readonly List<(string Rule, string Id)> _order = [];

    public Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<StoredScenario>>([.. _order.Select(key => _rows[key])]);
    }

    public Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredScenario>>(
                [.. _order.Where(key => key.Rule == ruleName).Select(key => _rows[key])]);
        }
    }

    public Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (scenario.RuleName, scenario.Id);
            var current = _rows.TryGetValue(key, out var existing) ? existing.Version : 0;
            if (baseVersion != current)
                return Task.FromResult(ScenarioWriteResult.Conflict(current));

            if (existing is null)
                _order.Add(key);

            var version = baseVersion + 1;
            _rows[key] = scenario with { Version = version, TimestampUtc = DateTimeOffset.UtcNow };
            return Task.FromResult(ScenarioWriteResult.Written(version));
        }
    }

    public Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var key = (ruleName, id);
            var current = _rows.TryGetValue(key, out var existing) ? existing.Version : 0;
            if (existing is null || baseVersion != current)
                return Task.FromResult(ScenarioWriteResult.Conflict(current));

            _rows.Remove(key);
            _order.Remove(key);
            return Task.FromResult(ScenarioWriteResult.Written(current));
        }
    }
}
```

- [ ] **Step 5: Run the suite**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//' | sort -u | head
```

Expected: `Passed!`, 12 more tests than before. (The EF and Studio test projects also compile the conformance folder; they build but have no subclass yet.)

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization/Scenarios src/testing/StoreConformance/ScenarioStoreConformance.cs src/Motiv.Serialization.Tests/Scenarios
git commit -m "Scenarios — the store contract, an in-memory store, and the conformance suite

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: The EF scenario store

**Files:**
- Modify: `src/Motiv.Serialization.EntityFrameworkCore/Rows.cs`
- Modify: `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreDbContext.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore/EfScenarioStore.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/EfScenarioStoreTests.cs`
- Modify: `src/Motiv.Serialization.EntityFrameworkCore.Tests/SchemaTests.cs`, `ProviderSchemaTests.cs`
- Test project: `src/Motiv.Serialization.EntityFrameworkCore.Tests`

**Interfaces:**
- Consumes: `IScenarioStore`, `StoredScenario`, `ScenarioWriteResult` (Task 1).
- Produces: `ScenarioRow` mapped to table `MotivScenario` keyed `(RuleName, Id)` with `Version` as a concurrency token; `DbSet<ScenarioRow> MotivStoreDbContext.Scenarios`; `EfScenarioStore(IDbContextFactory<MotivStoreDbContext>)`.

- [ ] **Step 1: Write the tests**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/EfScenarioStoreTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>The EF Core scenario store against the shared store contract.</summary>
public class EfScenarioStoreTests : ScenarioStoreConformance
{
    private SqliteStoreFixture _fixture = null!;

    protected override async Task<IScenarioStore> CreateStoreAsync()
    {
        _fixture = await SqliteStoreFixture.CreateAsync();
        return new EfScenarioStore(_fixture.Factory);
    }

    protected override async Task DisposeStoreAsync() => await _fixture.DisposeAsync();
}
```

In `SchemaTests.cs`, add `script.ShouldContain("MotivScenario");` to the create-script test and rename it `Should_create_the_four_tables`; add:

```csharp
    [Fact]
    public async Task Should_key_scenarios_on_rule_and_id_with_version_as_the_concurrency_token()
    {
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        var entity = context.Model.FindEntityType(typeof(ScenarioRow))!;

        entity.FindPrimaryKey()!.Properties.Select(p => p.Name).ShouldBe(["RuleName", "Id"]);
        entity.FindProperty("Version")!.IsConcurrencyToken.ShouldBeTrue();
    }
```

In `ProviderSchemaTests.cs`, add `script.ShouldContain("MotivScenario", customMessage: provider);` to the create-script theory.

- [ ] **Step 2: Run to see the failures**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u | head -3
```

Expected: `error CS0246` on `EfScenarioStore` and `ScenarioRow`.

- [ ] **Step 3: Add the row, the mapping and the store**

Append to `Rows.cs` before `internal static class RowMapping`:

```csharp
public class ScenarioRow
{
    public string RuleName { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelJson { get; set; } = string.Empty;
    public bool? ExpectedSatisfied { get; set; }
    public string? SourceDecisionId { get; set; }
    public int Version { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
}
```

and inside `RowMapping`:

```csharp
    public static StoredScenario ToRecord(this ScenarioRow row) =>
        new(row.RuleName, row.Id, row.Name, row.ModelJson, row.ExpectedSatisfied, row.SourceDecisionId,
            row.Version, row.Author, row.TimestampUtc);
```

In `MotivStoreDbContext.cs` add `public DbSet<ScenarioRow> Scenarios => Set<ScenarioRow>();` and, in `OnModelCreating`:

```csharp
        modelBuilder.Entity<ScenarioRow>(entity =>
        {
            entity.ToTable("MotivScenario");
            entity.HasKey(row => new { row.RuleName, row.Id });
            entity.Property(row => row.Name).IsRequired();
            entity.Property(row => row.ModelJson).IsRequired();
            entity.Property(row => row.Author).IsRequired();
            // Rows are replaced in place, so the version is a concurrency token: every UPDATE and
            // DELETE carries `AND Version = @original`, and a replica that committed first leaves
            // this one matching no rows — DbUpdateConcurrencyException, provider-agnostic.
            entity.Property(row => row.Version).IsConcurrencyToken();
        });
```

Create `EfScenarioStore.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>The scenario store over one table replaced in place, with the version as a concurrency token.</summary>
public sealed class EfScenarioStore(IDbContextFactory<MotivStoreDbContext> contextFactory) : IScenarioStore
{
    public async Task<IReadOnlyList<StoredScenario>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Scenarios.AsNoTracking()
            .OrderBy(row => row.RuleName).ThenBy(row => row.TimestampUtc).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToRecord())];
    }

    public async Task<IReadOnlyList<StoredScenario>> ForRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Scenarios.AsNoTracking()
            .Where(row => row.RuleName == ruleName)
            .OrderBy(row => row.TimestampUtc).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToRecord())];
    }

    public async Task<ScenarioWriteResult> PutAsync(StoredScenario scenario, int baseVersion, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Scenarios
            .SingleOrDefaultAsync(row => row.RuleName == scenario.RuleName && row.Id == scenario.Id, cancellationToken);
        var current = existing?.Version ?? 0;
        if (baseVersion != current)
            return ScenarioWriteResult.Conflict(current);

        var version = baseVersion + 1;
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            context.Scenarios.Add(new ScenarioRow
            {
                RuleName = scenario.RuleName, Id = scenario.Id, Name = scenario.Name, ModelJson = scenario.ModelJson,
                ExpectedSatisfied = scenario.ExpectedSatisfied, SourceDecisionId = scenario.SourceDecisionId,
                Version = version, Author = scenario.Author, TimestampUtc = now,
            });
        }
        else
        {
            existing.Name = scenario.Name;
            existing.ModelJson = scenario.ModelJson;
            existing.ExpectedSatisfied = scenario.ExpectedSatisfied;
            existing.SourceDecisionId = scenario.SourceDecisionId;
            existing.Version = version;
            existing.Author = scenario.Author;
            existing.TimestampUtc = now;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ScenarioWriteResult.Written(version);
        }
        catch (DbUpdateException)
        {
            // Another replica moved the row between the read and this write: a concurrency-token
            // miss on update, or a key collision on create. Report where the row now stands.
            return ScenarioWriteResult.Conflict(await CurrentVersionAsync(scenario.RuleName, scenario.Id, cancellationToken));
        }
    }

    public async Task<ScenarioWriteResult> DeleteAsync(string ruleName, string id, int baseVersion, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Scenarios
            .SingleOrDefaultAsync(row => row.RuleName == ruleName && row.Id == id, cancellationToken);
        var current = existing?.Version ?? 0;
        if (existing is null || baseVersion != current)
            return ScenarioWriteResult.Conflict(current);

        context.Scenarios.Remove(existing);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ScenarioWriteResult.Written(current);
        }
        catch (DbUpdateException)
        {
            return ScenarioWriteResult.Conflict(await CurrentVersionAsync(ruleName, id, cancellationToken));
        }
    }

    private async Task<int> CurrentVersionAsync(string ruleName, string id, CancellationToken cancellationToken)
    {
        await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await fresh.Scenarios.AsNoTracking()
            .Where(row => row.RuleName == ruleName && row.Id == id)
            .Select(row => row.Version)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Run the EF tests**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//' | sort -u | head
```

Expected: `Passed!`. If `Should_list_a_rules_scenarios_in_the_order_they_were_added` is flaky because two writes share a timestamp tick, the `ThenBy(Id)` tiebreak is wrong for it: make the in-memory store the reference and order EF rows by a store-assigned `Sequence` — add `public long Sequence { get; set; }` to `ScenarioRow` mapped with `.ValueGeneratedOnAdd()`, order by it, and drop the timestamp ordering. Ledger the ruling.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore src/Motiv.Serialization.EntityFrameworkCore.Tests
git commit -m "EF scenario store — MotivScenario keyed (RuleName, Id), version as the concurrency token

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: The scenario endpoints

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs` (add `AddScenarios`)
- Create: `src/Motiv.Serialization.AspNetCore/ScenariosContracts.cs`
- Create: `src/Motiv.Serialization.AspNetCore/MotivScenarioEndpoints.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (map when registered)
- Create: `src/Motiv.Serialization.AspNetCore.Tests/ScenarioEndpointTests.cs`
- Test project: `src/Motiv.Serialization.AspNetCore.Tests`

**Interfaces:**
- Consumes: `IScenarioStore`, `GrantGate.Refuse(http, verb, name, json)`, `PrincipalIdentity.Subject`, `EndpointResponses.NonPositiveBaseVersion`, `ErrorResponse`.
- Produces:
  - `MotivRulesBuilder.AddScenarios(IScenarioStore? store = null)` and `AddScenarios(Func<IServiceProvider, IScenarioStore>)`, registering `IScenarioStore` as a singleton, refusing a second call.
  - Routes under the rules group, mapped only when an `IScenarioStore` is registered: `GET /rules/{name}/scenarios` → `200 ScenarioEntry[]`; `PUT /rules/{name}/scenarios/{id}` with `ScenarioPutRequest(string Name, string Model, bool? ExpectedSatisfied, string? SourceDecisionId, int BaseVersion)` → `200 { version }` or `409 { currentVersion }` or `400 { error }`; `DELETE /rules/{name}/scenarios/{id}?baseVersion=` → `200 { version }` / `409` / `400`. `403` from `GrantGate` on every route (`Read` for GET, `Author` for PUT/DELETE). An unknown rule name is still served (a scenario may precede its rule's first publish) — the store is the authority, not the `RuleSet`.
  - `ScenarioEntry(string Id, string Name, string Model, bool? ExpectedSatisfied, string? SourceDecisionId, int Version, string Author, DateTimeOffset TimestampUtc)`. `Model` is the verbatim text, a string, not a `JsonElement`: the store keeps half-typed input.

- [ ] **Step 1: Write the endpoint tests**

Create `ScenarioEndpointTests.cs`, following `PropositionEndpointTests.StartAsync` (test auth, slim builder, `MapMotivRules("/api/rules")`) but with `.AddScenarios()` on the builder and a `Customer` rule registered via `AddRule<...>` is not needed — the store is the authority:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.AspNetCore.Tests;

public class ScenarioEndpointTests
{
    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static async Task<WebApplication> StartAsync(Action<IServiceCollection>? grants = null)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddMotivRules(registry, options).AddScenarios();
        grants?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> Put(
        HttpClient client, string rule, string id, string name, string model, int baseVersion,
        bool? expected = null, string? source = null) =>
        client.PutAsJsonAsync($"/api/rules/rules/{rule}/scenarios/{id}", new
        {
            name, model, expectedSatisfied = expected, sourceDecisionId = source, baseVersion,
        });

    [Fact]
    public async Task Should_create_list_update_and_delete_a_rules_scenarios()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "alice");

        var created = await Put(client, "can-checkout", "s1", "Active adult", """{ "age": 30 }""", 0, expected: true, source: "d-1");
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32().ShouldBe(1);

        var listed = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios");
        var entry = listed.EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("id").GetString().ShouldBe("s1");
        entry.GetProperty("model").GetString().ShouldBe("""{ "age": 30 }""");
        entry.GetProperty("expectedSatisfied").GetBoolean().ShouldBeTrue();
        entry.GetProperty("sourceDecisionId").GetString().ShouldBe("d-1");
        entry.GetProperty("author").GetString().ShouldBe("alice");

        var updated = await Put(client, "can-checkout", "s1", "Renamed", "{ \"age\": 3", 1);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stale = await Put(client, "can-checkout", "s1", "Stale", "{}", 1);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentVersion").GetInt32().ShouldBe(2);

        var deleted = await client.DeleteAsync("/api/rules/rules/can-checkout/scenarios/s1?baseVersion=2");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios")).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_answer_400_for_a_missing_name_or_a_negative_base_version()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        (await Put(client, "r", "s1", "", "{}", 0)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Put(client, "r", "s1", "x", "{}", -1)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.DeleteAsync("/api/rules/rules/r/scenarios/s1?baseVersion=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
```

Then add the two grant tests by reading how `GrantEnforcementTests.cs` registers a restricted `IGrantSource` (grep `IGrantSource` there) and using the same registration through the `grants` hook:

```csharp
    [Fact]
    public async Task Should_refuse_a_put_without_author_grant()
    {
        // Arrange — a caller who may read pricing.* but not author it
        await using var app = await StartAsync(services => services.AddSingleton<IGrantSource>(
            new FixedGrants([new NamespaceGrant("pricing.", GrantVerb.Read)])));
        var client = app.GetTestClient();

        (await Put(client, "pricing.vat", "s1", "x", "{}", 0)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/rules/rules/pricing.vat/scenarios")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_refuse_a_list_without_read_grant()
    {
        await using var app = await StartAsync(services => services.AddSingleton<IGrantSource>(
            new FixedGrants([new NamespaceGrant("pricing.", GrantVerb.Read)])));
        var client = app.GetTestClient();

        (await client.GetAsync("/api/rules/rules/fraud.screen/scenarios")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
```

`FixedGrants` is whatever fixed-grant `IGrantSource` test double `GrantEnforcementTests.cs` already defines; reuse it by name (make it `internal` if it is `private`).

- [ ] **Step 2: Run to see the failures**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.AspNetCore.Tests --filter "FullyQualifiedName~ScenarioEndpointTests" 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u | head -3
```

Expected: `error CS1061` — `AddScenarios` does not exist.

- [ ] **Step 3: Register, contract, map**

In `MotivRulesServiceCollectionExtensions.cs`, add to `MotivRulesBuilder` after the `AddPropositions` overloads:

```csharp
    /// <summary>Stores each rule's scenarios — named sample models — behind <c>{basePath}/rules/{name}/scenarios</c>. In memory when no store is given.</summary>
    public MotivRulesBuilder AddScenarios(IScenarioStore? store = null) =>
        AddScenariosCore(_ => store ?? new InMemoryScenarioStore());

    public MotivRulesBuilder AddScenarios(Func<IServiceProvider, IScenarioStore> storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        return AddScenariosCore(storeFactory);
    }

    private MotivRulesBuilder AddScenariosCore(Func<IServiceProvider, IScenarioStore> storeFactory)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(IScenarioStore)))
            throw new InvalidOperationException(
                $"{nameof(AddScenarios)} has already been called. Call it once — a second call " +
                "would silently replace the first store, as DI registration is last-wins.");

        Services.AddSingleton<IScenarioStore>(storeFactory);
        return this;
    }
```

Create `ScenariosContracts.cs`:

```csharp
namespace Motiv.Serialization.AspNetCore;

/// <summary>One scenario of a rule as the API lists it. <c>Model</c> is the text as stored, JSON or not.</summary>
public sealed record ScenarioEntry(
    string Id, string Name, string Model, bool? ExpectedSatisfied, string? SourceDecisionId,
    int Version, string Author, DateTimeOffset TimestampUtc);

/// <summary>A create (<c>baseVersion</c> 0) or replace of one scenario.</summary>
public sealed record ScenarioPutRequest(
    string Name, string Model, bool? ExpectedSatisfied, string? SourceDecisionId, int BaseVersion);

public sealed record ScenarioSaveResponse(int Version);

public sealed record ScenarioConflictResponse(int CurrentVersion);
```

Create `MotivScenarioEndpoints.cs`:

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Motiv.Serialization.AspNetCore;

internal static class MotivScenarioEndpoints
{
    internal static void MapScenarioEndpoints(RouteGroupBuilder group, IScenarioStore scenarios, JsonSerializerOptions json)
    {
        group.MapGet("/rules/{name}/scenarios", async (string name, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Read, name, json) is { } refusal)
                return refusal;

            var rows = await scenarios.ForRuleAsync(name, http.RequestAborted);
            return Results.Json(rows.Select(ToEntry).ToArray(), json);
        });

        group.MapPut("/rules/{name}/scenarios/{id}", async (string name, string id, ScenarioPutRequest request, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Author, name, json) is { } refusal)
                return refusal;

            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.Json(new ErrorResponse("The request must include a name."), json, statusCode: 400);
            if (request.Model is null)
                return Results.Json(new ErrorResponse("The request must include a model."), json, statusCode: 400);
            if (request.BaseVersion < 0)
                return Results.Json(new ErrorResponse("baseVersion must be 0 to create, or the version last observed."), json, statusCode: 400);

            var scenario = new StoredScenario(
                name, id, request.Name, request.Model, request.ExpectedSatisfied, request.SourceDecisionId,
                Version: 0, PrincipalIdentity.Subject(http.User), DateTimeOffset.MinValue);
            return ToResult(await scenarios.PutAsync(scenario, request.BaseVersion, http.RequestAborted), json);
        });

        group.MapDelete("/rules/{name}/scenarios/{id}", async (string name, string id, int baseVersion, HttpContext http) =>
        {
            if (GrantGate.Refuse(http, GrantVerb.Author, name, json) is { } refusal)
                return refusal;

            if (baseVersion <= 0)
                return EndpointResponses.NonPositiveBaseVersion(json);

            return ToResult(await scenarios.DeleteAsync(name, id, baseVersion, http.RequestAborted), json);
        });
    }

    private static ScenarioEntry ToEntry(StoredScenario row) =>
        new(row.Id, row.Name, row.ModelJson, row.ExpectedSatisfied, row.SourceDecisionId, row.Version, row.Author, row.TimestampUtc);

    private static IResult ToResult(ScenarioWriteResult written, JsonSerializerOptions json) =>
        written.IsConflict
            ? Results.Json(new ScenarioConflictResponse(written.CurrentVersion), json, statusCode: 409)
            : Results.Json(new ScenarioSaveResponse(written.Version), json);
}
```

In `MotivRulesEndpoints.MapMotivRules` (the overload that takes `registry`), after the propositions mapping:

```csharp
        if (endpoints.ServiceProvider.GetService<IScenarioStore>() is { } scenarios)
            MotivScenarioEndpoints.MapScenarioEndpoints(group, scenarios, json);
```

If `MapRuleEndpoints` maps `/rules/{name}` with a catch-all that would swallow `/rules/{name}/scenarios`, it does not: ASP.NET Core routes by template, and `/rules/{name}/evaluate` already coexists.

- [ ] **Step 4: Run the AspNetCore tests**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.AspNetCore.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//' | sort -u | head
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "Scenario endpoints — GET/PUT/DELETE under rules/{name}/scenarios, Read to list and Author to write

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: The client calls in `@motiv-rules/core`

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts`
- Modify: `ui/packages/rules-core/src/client.ts`
- Modify: `ui/packages/rules-core/src/index.ts`
- Modify: `ui/packages/rules-core/test/client.test.ts`
- Modify: `ui/packages/rules-core/test/api-surface.test.ts` (only if it lists types; runtime names are unchanged since methods live on `RulesApiClient`)

**Interfaces:**
- Produces, in `contracts.ts`:
  ```ts
  export interface ScenarioEntry { id: string; name: string; model: string; expectedSatisfied: boolean | null; sourceDecisionId: string | null; version: number; author: string; timestampUtc: string; }
  export interface ScenarioPutRequest { name: string; model: string; expectedSatisfied: boolean | null; sourceDecisionId: string | null; baseVersion: number; }
  export type ScenarioSaveResult = { outcome: 'saved'; version: number } | { outcome: 'conflict'; currentVersion: number };
  ```
- On `RulesApiClient`: `listScenarios(rule: string): Promise<ScenarioEntry[]>` (a `404` resolves to `[]` — the host has no scenario store); `putScenario(rule: string, id: string, request: ScenarioPutRequest): Promise<ScenarioSaveResult>`; `deleteScenario(rule: string, id: string, baseVersion: number): Promise<ScenarioSaveResult>`.

- [ ] **Step 1: Write the client tests**

Append to `client.test.ts` inside the `describe('RulesApiClient')` block:

```ts
  it('lists a rule’s scenarios, and treats a missing scenario store as an empty list', async () => {
    const entries = [{ id: 's1', name: 'Minor', model: '{ "age": 16 }', expectedSatisfied: null, sourceDecisionId: null, version: 1, author: 'alice', timestampUtc: '2026-09-20T00:00:00Z' }];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(entries));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    expect(await client.listScenarios('can-checkout')).toEqual(entries);
    expect(fetchMock).toHaveBeenCalledWith('/api/rules/rules/can-checkout/scenarios', { method: 'GET' });

    fetchMock.mockResolvedValue(jsonResponse({ error: 'not here' }, 404));
    expect(await client.listScenarios('can-checkout')).toEqual([]);
  });

  it('puts a scenario and returns saved or conflict as values', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ version: 1 }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });
    const request = { name: 'Minor', model: '{ "age": 16 }', expectedSatisfied: null, sourceDecisionId: null, baseVersion: 0 };

    expect(await client.putScenario('can-checkout', 's1', request)).toEqual({ outcome: 'saved', version: 1 });
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe('/api/rules/rules/can-checkout/scenarios/s1');
    expect(init.method).toBe('PUT');
    expect(JSON.parse(init.body)).toEqual(request);

    fetchMock.mockResolvedValue(jsonResponse({ currentVersion: 2 }, 409));
    expect(await client.putScenario('can-checkout', 's1', request)).toEqual({ outcome: 'conflict', currentVersion: 2 });
  });

  it('deletes a scenario at a base version', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ version: 1 }));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetchMock });

    expect(await client.deleteScenario('can-checkout', 's1', 1)).toEqual({ outcome: 'saved', version: 1 });
    expect(fetchMock).toHaveBeenCalledWith('/api/rules/rules/can-checkout/scenarios/s1?baseVersion=1', { method: 'DELETE' });
  });
```

- [ ] **Step 2: Run to see them fail**

```bash
pnpm -C ui/packages/rules-core test -- client 2>&1 | tail -15
```

Expected: type errors or `listScenarios is not a function`.

- [ ] **Step 3: Implement**

Add the three types to `contracts.ts` after `BrokenDependent`. Add to `RulesApiClient`:

```ts
  /** GET {baseUrl}/rules/{rule}/scenarios — `[]` when the host mounted no scenario store (404). */
  async listScenarios(rule: string): Promise<ScenarioEntry[]> {
    const response = await this.#fetch(`${this.#baseUrl}/rules/${encodeURIComponent(rule)}/scenarios`, { method: 'GET' });
    if (response.status === 404) { this.#trackGeneration(response); return []; }
    return this.#read<ScenarioEntry[]>(response);
  }

  /** PUT {baseUrl}/rules/{rule}/scenarios/{id} — 409 returns a typed conflict rather than throwing. */
  async putScenario(rule: string, id: string, request: ScenarioPutRequest): Promise<ScenarioSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/rules/${encodeURIComponent(rule)}/scenarios/${encodeURIComponent(id)}`,
      { method: 'PUT', headers: { 'content-type': 'application/json' }, body: JSON.stringify(request) },
    );
    return this.#readScenarioResult(response);
  }

  /** DELETE {baseUrl}/rules/{rule}/scenarios/{id}?baseVersion=N */
  async deleteScenario(rule: string, id: string, baseVersion: number): Promise<ScenarioSaveResult> {
    const response = await this.#fetch(
      `${this.#baseUrl}/rules/${encodeURIComponent(rule)}/scenarios/${encodeURIComponent(id)}?baseVersion=${baseVersion}`,
      { method: 'DELETE' },
    );
    return this.#readScenarioResult(response);
  }

  async #readScenarioResult(response: Response): Promise<ScenarioSaveResult> {
    this.#trackGeneration(response);
    if (response.ok) {
      const body = (await response.json()) as { version: number };
      return { outcome: 'saved', version: body.version };
    }
    if (response.status === 409) {
      const body = (await response.json().catch(() => undefined)) as { currentVersion?: number } | undefined;
      if (body && typeof body.currentVersion === 'number') return { outcome: 'conflict', currentVersion: body.currentVersion };
    }
    return this.#throwFromErrorResponse(response);
  }
```

Export the three types from `index.ts` in the contracts block. Run `pnpm -C ui/packages/rules-core test` in full; if `api-surface.test.ts` fails, it enumerates runtime exports only and nothing runtime was added — read its failure before touching the approved list.

- [ ] **Step 4: Run, build, commit**

```bash
pnpm -C ui/packages/rules-core test 2>&1 | tail -5 && pnpm -C ui/packages/rules-core typecheck 2>&1 | tail -3 && pnpm -C ui/packages/rules-core build 2>&1 | tail -2
```

Expected: all pass; `dist/` rebuilt.

```bash
git add ui/packages/rules-core
git commit -m "@motiv-rules/core — listScenarios, putScenario and deleteScenario

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Studio host — store wiring, schema guard, and seeding

**Files:**
- Modify: `src/Motiv.Studio/Program.cs`
- Modify: `src/Motiv.Studio/StoreSchema.cs`
- Create: `src/Motiv.Studio/ScenarioSeeds.cs`
- Modify: `src/Motiv.Studio.Tests/StoreSchemaTests.cs`
- Create: `src/Motiv.Studio.Tests/ScenarioSeedingTests.cs`
- Test project: `src/Motiv.Studio.Tests`

**Interfaces:**
- Consumes: `EfScenarioStore`, `MotivRulesBuilder.AddScenarios`, `MotivStoreDbContext.Scenarios`, `RuleSet.Rules` (the `RuleSetEntry` list with `Name`).
- Produces: `internal static class ScenarioSeeds { public static async Task<int> SeedAsync(IScenarioStore store, IEnumerable<string> ruleNames, CancellationToken ct); }` — writes the four customer seeds for every rule the store holds **no marker for**, then writes a marker so an emptied rule stays empty. The marker is a scenario row with id `"__seeded"`, name `"seeded"`, model `"{}"`, that the endpoint list filters out.

**Ruling to carry:** the marker row is the idempotence mechanism. `MotivScenarioEndpoints` and `EfScenarioStore` do not know about it; the Studio host filters it from `GET` responses via a small endpoint filter? No — simpler and honest: `ScenarioSeeds` checks `ForRuleAsync(rule)` and seeds only when the rule has **no rows at all and no marker**, and the marker is `StoredScenario` with `Id = "__seeded"`, which Studio's pane hides by id. Keep it that simple; the plan's Review Focus 4 is pinned by the seeding test.

- [ ] **Step 1: Write the tests**

In `StoreSchemaTests.cs` add `(await check.Scenarios.CountAsync()).ShouldBe(0);` beside the other three counts, and update the "three tables" wording in the test name to "four".

Create `ScenarioSeedingTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Motiv.Studio.Tests;

public class ScenarioSeedingTests
{
    [Fact]
    public async Task Should_seed_four_scenarios_per_rule_once()
    {
        // Arrange — one isolated database, booted twice
        var connection = $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-seed-{Guid.NewGuid():N}.db")}";
        WebApplicationFactory<Program> Boot() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("Motiv:Store:ConnectionString", connection)
            .UseSetting("Motiv:Decisions:ConnectionString", $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-seed-d-{Guid.NewGuid():N}.db")}"));

        // Act — first boot seeds; delete one seed; second boot must not put it back
        await using (var first = Boot())
        {
            var client = first.CreateClient();
            var rows = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios");
            rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(
                ["Active adult, 3 orders", "Minor", "Dormant account", "New, no orders"]);

            var minor = rows.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Minor");
            var deleted = await client.DeleteAsync(
                $"/api/rules/rules/can-checkout/scenarios/{minor.GetProperty("id").GetString()}?baseVersion={minor.GetProperty("version").GetInt32()}");
            deleted.IsSuccessStatusCode.ShouldBeTrue();
        }

        await using (var second = Boot())
        {
            var rows = await second.CreateClient().GetFromJsonAsync<JsonElement>("/api/rules/rules/can-checkout/scenarios");
            rows.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(
                ["Active adult, 3 orders", "Dormant account", "New, no orders"]);
        }
    }
}
```

If the Studio test host requires the test identity header for grants (see how `DecisionLogEndpointTests` posts to `/api/checkout` — it does not add one), the dev identity is on by default and permits reads; if the delete is refused with 403, add the header the other Studio tests use for an administrator (grep `X-Test-User` / `DevIdentity` in `src/Motiv.Studio.Tests`).

- [ ] **Step 2: Run to see it fail**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Studio.Tests --filter "FullyQualifiedName~ScenarioSeedingTests|FullyQualifiedName~StoreSchemaTests" 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//' | sort -u | head
```

Expected: `error CS1061` on `check.Scenarios`; after fixing the compile, the seeding test fails with a `404` on the scenarios route.

- [ ] **Step 3: Wire the store, the guard and the seeds**

In `Program.cs`, after `.AddPropositions(...)`:

```csharp
    // Seam: scenarios. Each rule's sample models live in the same database as the rules, so a
    // scenario saved in one replica is on every other's Evaluate table after a refresh of the tab.
    .AddScenarios(provider => new EfScenarioStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
```

In `StoreSchema.cs`, add the probe `("MotivScenario", () => context.Scenarios.AsNoTracking().AnyAsync(cancellationToken)),` and change "all three tables" to "all four tables" in the warning text.

Create `ScenarioSeeds.cs`:

```csharp
using Motiv.Serialization;

/// <summary>
/// The four sample customers every demo rule starts with — the rows Studio's Evaluate table used to
/// seed in the browser. Seeded once per rule: a rule whose scenarios an operator emptied stays empty,
/// because the marker row remains.
/// </summary>
internal static class ScenarioSeeds
{
    internal const string MarkerId = "__seeded";

    private static readonly (string Name, string Model)[] Seeds =
    [
        ("Active adult, 3 orders", """{ "customerId": "cust-42", "age": 30, "isActive": true, "orderCount": 3, "orders": [{ "total": 120 }] }"""),
        ("Minor", """{ "customerId": "cust-7", "age": 16, "isActive": true, "orderCount": 1, "orders": [{ "total": 20 }] }"""),
        ("Dormant account", """{ "customerId": "cust-9", "age": 41, "isActive": false, "orderCount": 12, "orders": [{ "total": 300 }] }"""),
        ("New, no orders", """{ "customerId": "cust-1", "age": 25, "isActive": true, "orderCount": 0 }"""),
    ];

    public static async Task<int> SeedAsync(IScenarioStore store, IEnumerable<string> ruleNames, CancellationToken cancellationToken)
    {
        var seeded = 0;
        foreach (var rule in ruleNames)
        {
            var existing = await store.ForRuleAsync(rule, cancellationToken);
            if (existing.Any(row => row.Id == MarkerId))
                continue;

            foreach (var (name, model) in Seeds)
            {
                await store.PutAsync(
                    new StoredScenario(rule, Guid.NewGuid().ToString("N"), name, model, null, null, 0, "system", DateTimeOffset.MinValue),
                    baseVersion: 0, cancellationToken);
                seeded++;
            }

            // The marker is a scenario too, so no store needs to know about seeding; Studio hides it by id.
            await store.PutAsync(
                new StoredScenario(rule, MarkerId, "seeded", "{}", null, null, 0, "system", DateTimeOffset.MinValue),
                baseVersion: 0, cancellationToken);
        }

        return seeded;
    }
}
```

In `Program.cs`, after the `StoreSchema.EnsureCreatedAsync` block:

```csharp
// Seam: the Evaluate table's seeds. Once per rule, then left alone — see ScenarioSeeds.
{
    var rules = app.Services.GetRequiredService<RuleSet>();
    var seeded = await ScenarioSeeds.SeedAsync(
        app.Services.GetRequiredService<IScenarioStore>(),
        rules.Rules.Select(rule => rule.Name),
        CancellationToken.None);
    app.Logger.LogInformation("Scenario seeds: wrote {Seeded} scenarios", seeded);
}
```

Then, so the marker never reaches a client, filter it in `MotivScenarioEndpoints.MapScenarioEndpoints`'s GET: no — the endpoint is generic. Filter in Studio instead: Studio's pane hides ids starting with `__` (Task 6). The seeding test above lists through the endpoint and expects four names, so the marker **must** be excluded by the endpoint or the test asserts five. Ruling: the endpoint excludes ids starting with `__` and documents them as reserved for host bookkeeping. Add to the GET handler: `.Where(row => !row.Id.StartsWith("__", StringComparison.Ordinal))`, with a comment, and a line in the AspNetCore docs table.

- [ ] **Step 4: Run the Studio tests**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Studio.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//' | sort -u | head
```

Expected: `Passed!`. Also re-run `src/Motiv.Serialization.AspNetCore.Tests` after the `__` filter.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Studio src/Motiv.Studio.Tests src/Motiv.Serialization.AspNetCore
git commit -m "Studio — scenarios stored in the EF store, seeded once per rule at startup

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Studio's scenario pane reads and writes the store

**Files:**
- Modify: `ui/apps/studio/src/panes/scenarios.ts`
- Modify: `ui/apps/studio/src/panes/ScenarioPane.tsx`
- Modify: `ui/apps/studio/test/panes/scenarios.test.ts`
- Modify: `ui/apps/studio/test/panes/ScenarioPane.test.tsx`
- Modify: `ui/apps/studio/test/panes/RuleDocument.test.tsx`, `ui/apps/studio/test/App.test.tsx` (client mocks gain `listScenarios`)
- Modify: `ui/apps/studio/e2e-a11y/stubs.ts`

**Interfaces:**
- `Scenario.id` becomes `string`; `Scenario` gains `version: number` (0 until saved) and `saving: 'idle' | 'saving' | 'conflict' | 'error'`.
- `scenarios.ts`: `seedScenarios` is removed; `fromStored(entries: ScenarioEntry[]): Scenario[]` added; `addScenario(rows)` and `cloneScenario(rows, id)` generate ids with `newId()` (`crypto.randomUUID()` with a fallback to `Math.random`-based hex); `withSaved(rows, id, version)` and `withSaving(rows, id, state)` added.
- `ScenarioPane` loads with `client.listScenarios(ruleName)` on mount and when `ruleName` changes; writes with `putScenario` on add, clone, and on **blur** of the name and model inputs (not per keystroke); `deleteScenario` on delete; **Reset** re-lists from the store. A `409` marks the row `conflict` with a visible "changed elsewhere — reset to reload" hint; a thrown error marks `error` with the message.

- [ ] **Step 1: Update the unit tests**

In `scenarios.test.ts`: replace the seeds test with a `fromStored` test (four entries in, four rows out, ids preserved, all unevaluated, version carried), and give `addScenario`/`cloneScenario` tests string-id expectations (`typeof row.id === 'string'`, distinct).

In `ScenarioPane.test.tsx`: extend `client()` with

```ts
    listScenarios: vi.fn().mockResolvedValue(SEEDS.map((s, i) => ({ id: `s${i + 1}`, ...s, expectedSatisfied: null, sourceDecisionId: null, version: 1, author: 'system', timestampUtc: '2026-09-20T00:00:00Z' }))),
    putScenario: vi.fn().mockResolvedValue({ outcome: 'saved', version: 2 }),
    deleteScenario: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
```

where `SEEDS` is the four `{ name, model }` pairs copied from the old `SEED` constant. `settleCatalog` already awaits an act tick; the list resolves in the same tick. Then add:

```ts
  it('loads the rule’s scenarios from the store', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    expect(api.listScenarios).toHaveBeenCalledWith('can-checkout');
    expect(row('Active adult, 3 orders')).toBeDefined();
  });

  it('renders an empty table with a hint when the host has no scenario store', async () => {
    const api = client();
    (api.listScenarios as ReturnType<typeof vi.fn>).mockResolvedValue([]);
    renderPane(api);
    await settleCatalog();
    expect(screen.getByText('No scenarios. Add one, or reset to reload.')).toBeDefined();
    fireEvent.click(screen.getByRole('button', { name: 'Run all' }));
    expect(api.evaluate).not.toHaveBeenCalled();
  });

  it('saves a renamed scenario when its editor loses focus, at its version', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const name = screen.getByLabelText('scenario name');
    fireEvent.change(name, { target: { value: 'Teenager' } });
    expect(api.putScenario).not.toHaveBeenCalled();
    fireEvent.blur(name);
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledWith('can-checkout', 's2',
      expect.objectContaining({ name: 'Teenager', baseVersion: 1 })));
  });

  it('adds and deletes through the store', async () => {
    const api = client();
    renderPane(api);
    await settleCatalog();
    fireEvent.click(screen.getByRole('button', { name: 'Add' }));
    await waitFor(() => expect(api.putScenario).toHaveBeenCalledWith('can-checkout', expect.any(String),
      expect.objectContaining({ name: 'Scenario 5', baseVersion: 0 })));
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'delete Minor' }));
    await waitFor(() => expect(api.deleteScenario).toHaveBeenCalledWith('can-checkout', 's2', 1));
  });

  it('flags a row the store says changed elsewhere, and Reset reloads it', async () => {
    const api = client();
    (api.putScenario as ReturnType<typeof vi.fn>).mockResolvedValue({ outcome: 'conflict', currentVersion: 3 });
    renderPane(api);
    await settleCatalog();
    fireEvent.click(within(row('Minor')).getByRole('button', { name: 'details of Minor' }));
    const name = screen.getByLabelText('scenario name');
    fireEvent.change(name, { target: { value: 'Teenager' } });
    fireEvent.blur(name);
    await waitFor(() => expect(screen.getByText(/changed elsewhere/)).toBeDefined());
    fireEvent.click(screen.getByRole('button', { name: 'Reset' }));
    await waitFor(() => expect(api.listScenarios).toHaveBeenCalledTimes(2));
  });
```

Update the existing "clones, deletes, and resets to the seeds" test: Reset now re-lists, so after clicking Reset `await settleCatalog()` before asserting the four names, and the Reset tooltip text becomes "Reload this rule's scenarios from the store". The "adds two scenarios from two quick clicks" test still holds (names come from row count). In `RuleDocument.test.tsx` and `App.test.tsx`, add `listScenarios: vi.fn().mockResolvedValue([])` to the client mocks.

- [ ] **Step 2: Run the Studio unit tests to see them fail**

```bash
pnpm -C ui/packages/rules-react build 2>&1 | tail -1 && pnpm -C ui/apps/studio test -- panes 2>&1 | tail -20
```

Expected: failures on `listScenarios`, `fromStored`, and the seed-based expectations.

- [ ] **Step 3: Implement `scenarios.ts` changes**

```ts
export interface Scenario {
  id: string;
  name: string;
  model: string;
  /** The store's version, 0 until first saved. */
  version: number;
  saving: 'idle' | 'saving' | 'conflict' | 'error';
  saveError?: string;
  comparison: Comparison;
  open: boolean;
  violations: SchemaViolation[];
}

const newId = (): string =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID().replace(/-/g, '')
    : Array.from({ length: 32 }, () => Math.floor(Math.random() * 16).toString(16)).join('');

const fresh = (id: string, name: string, model: string, version = 0, open = false): Scenario =>
  ({ id, name, model, version, saving: 'idle', comparison: UNEVALUATED, open, violations: [] });

/** Rows from the store, unevaluated. Ids beginning with `__` are host bookkeeping and never shown. */
export const fromStored = (entries: readonly ScenarioEntry[]): Scenario[] =>
  entries.filter((e) => !e.id.startsWith('__')).map((e) => fresh(e.id, e.name, e.model, e.version));

export const addScenario = (rows: Scenario[]): Scenario[] =>
  [...rows, fresh(newId(), `Scenario ${rows.length + 1}`, '{\n  \n}', 0, true)];

export function cloneScenario(rows: Scenario[], id: string): Scenario[] {
  const index = rows.findIndex((r) => r.id === id);
  if (index < 0) return rows;
  const source = rows[index]!;
  const copy = fresh(newId(), `${source.name} (copy)`, source.model, 0, true);
  return [...rows.slice(0, index + 1), copy, ...rows.slice(index + 1)];
}

export const withSaved = (rows: Scenario[], id: string, version: number): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, version, saving: 'idle' as const, saveError: undefined } : r));

export const withSaving = (rows: Scenario[], id: string, saving: Scenario['saving'], saveError?: string): Scenario[] =>
  rows.map((r) => (r.id === id ? { ...r, saving, saveError } : r));
```

Every other function's `id: number` becomes `id: string`; `nextIdIn` and `SEED`/`seedScenarios` are deleted. Import `ScenarioEntry` from `@motiv-rules/core`.

- [ ] **Step 4: Implement the pane changes**

In `ScenarioPane.tsx`:
- `const [rows, setRows] = useState<Scenario[]>([]);` plus a `load` function used in a `useEffect` keyed on `[props.client, props.ruleName]`:

```ts
  const load = useCallback(async () => {
    const entries = await props.client.listScenarios(props.ruleName);
    setRows(fromStored(entries));
  }, [props.client, props.ruleName]);
  useEffect(() => { void load(); }, [load]);
```

- A `persist(row: Scenario)` helper:

```ts
  const persist = async (row: Scenario): Promise<void> => {
    setRows((current) => withSaving(current, row.id, 'saving'));
    try {
      const saved = await props.client.putScenario(props.ruleName, row.id, {
        name: row.name, model: row.model, expectedSatisfied: null, sourceDecisionId: null, baseVersion: row.version,
      });
      setRows((current) => saved.outcome === 'saved'
        ? withSaved(current, row.id, saved.version)
        : withSaving(current, row.id, 'conflict', `changed elsewhere (now v${saved.currentVersion}) — reset to reload`));
    } catch (error) {
      setRows((current) => withSaving(current, row.id, 'error', error instanceof Error ? error.message : String(error)));
    }
  };
```

- **Add**: `const next = addScenario(rows); setRows(next); void persist(next[next.length - 1]!);` — compute from the committed `rows` closure is wrong for two quick clicks; instead use the functional update and persist from inside a `useEffect` that watches for rows with `version === 0 && saving === 'idle'` and calls `persist` once per such row, marking it `saving` synchronously in the same update. Implement that effect:

```ts
  useEffect(() => {
    const unsaved = rows.filter((r) => r.version === 0 && r.saving === 'idle');
    if (unsaved.length === 0) return;
    setRows((current) => unsaved.reduce((acc, r) => withSaving(acc, r.id, 'saving'), current));
    for (const r of unsaved) void persist(r);
  }, [rows]);
```

- **Clone** follows the same path (a cloned row has `version: 0`).
- **Edit**: `onEdit` keeps the local state change; add `onCommit={() => void persistById(row.id)}` wired to `onBlur` of both inputs, where `persistById` reads the latest row from a ref of `rows` (`const rowsRef = useRef(rows); rowsRef.current = rows;`) and calls `persist` only when `version > 0` (unsaved rows are handled by the effect above) and `saving !== 'saving'`.
- **Delete**: `const target = rowsRef.current.find(...)`; remove locally, then `if (target.version > 0) void props.client.deleteScenario(props.ruleName, target.id, target.version)` — a conflict on delete is ignored (the row is gone locally; Reset shows the truth).
- **Reset**: `onClick={() => void load()}`, tooltip "Reload this rule's scenarios from the store".
- The empty-table hint: `No scenarios. Add one, or reset to reload.`
- In `ScenarioRow`, render the save state after the name: `{row.saving === 'conflict' && <span className="pane-hint">{row.saveError}</span>}` and likewise for `error`; unchanged structure otherwise.
- `runAll` skips nothing new; rows with `version === 0` still run.

- [ ] **Step 5: Stub the route for the a11y sweep**

In `e2e-a11y/stubs.ts` `ROUTES`, before the `/rules/[^/?]+` catch-all, add:

```ts
  [/\/api\/rules\/rules\/[^/]+\/scenarios(\/[^/?]+)?(\?.*)?$/, SCENARIOS],
```

with `const SCENARIOS = [ /* the four seeds as ScenarioEntry objects, ids s1..s4, version 1, author 'system' */ ];` and make the router answer `PUT`/`DELETE` on that pattern with `{ version: 2 }` — read how the stub router picks bodies by method (`page.route('**/api/**'...` at line ~142) and follow it.

- [ ] **Step 6: Run the Studio unit tests, typecheck, and the a11y sweep**

```bash
pnpm -C ui/apps/studio test 2>&1 | tail -8 && pnpm -C ui/apps/studio typecheck 2>&1 | tail -3
```

Expected: all pass, no type errors. Then (sandbox disabled, may take a few minutes):

```bash
pnpm -C ui/apps/studio a11y 2>&1 | tail -6
```

Expected: `passed`. A timeout flake under load is known (memory note); re-run once before treating a failure as real.

- [ ] **Step 7: Commit**

```bash
git add ui/apps/studio
git commit -m "Studio — the Evaluate table's scenarios live in the store: loaded on open, saved on blur, add, clone and delete; Reset reloads

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Docs, e2e, full verification, simplifier, PR

**Files:**
- Modify: `docs/live-rules/AspNetCore.md` (three table rows + a "Scenarios" paragraph), `docs/live-rules/entity-framework-store.md` (fourth table row and backup unit), `CONTEXT.md` (glossary: **Scenario**)
- Create: `docs/superpowers/specs/2026-09-20-persisted-scenarios-design.md`

- [ ] **Step 1: Docs**

In `docs/live-rules/AspNetCore.md`, after the rule-management table, add:

```markdown
### Scenarios

With `AddScenarios()` on the builder, each rule carries a table of named sample models — what
Studio's Evaluate pane shows — stored in the backend rather than the browser. A scenario is test
data: it never changes what a rule decides, so it is not versioned as a log and not governed.
Reads need `Read` on the rule's namespace, writes need `Author`. Ids beginning with `__` are
reserved for host bookkeeping and never listed.

| Method & path | Request | Responses |
|---|---|---|
| `GET {basePath}/rules/{name}/scenarios` | &mdash; | `200` &mdash; array of `{ id, name, model, expectedSatisfied, sourceDecisionId, version, author, timestampUtc }`; `403` |
| `PUT {basePath}/rules/{name}/scenarios/{id}` | `{ name, model, expectedSatisfied?, sourceDecisionId?, baseVersion }` (`baseVersion` 0 creates) | `200 { version }`; `409 { currentVersion }`; `400 { error }`; `403` |
| `DELETE {basePath}/rules/{name}/scenarios/{id}` | `?baseVersion=n` | `200 { version }`; `409 { currentVersion }`; `400`; `403` |

`model` is the text as typed, JSON or not, so an edit in progress survives a reload.
```

In `entity-framework-store.md`, add the `MotivScenario` row (`ScenarioRow` / `StoredScenario`, "A rule's scenarios, one row per (rule, id), replaced in place under a version concurrency token") and make the backup unit "four tables". In `CONTEXT.md` under Authoring:

```markdown
**Scenario**:
A named sample model attached to a rule — a row of its Evaluate table — with an optional expected
verdict and, when it was saved from a logged decision, that decision's id. Test data, not behaviour:
stored in the backend, replaced in place under a version compare-and-set, never governed.
_Avoid_: Sample, fixture, test case (as a stored noun)
```

- [ ] **Step 2: Design doc**

Create `docs/superpowers/specs/2026-09-20-persisted-scenarios-design.md` with: Date, Status Implemented, Parent (section 3 of the reproduction design), Plan path; Problem (scenarios were tab memory, unreachable by anything but the tab); Decisions (per-rule; caller-chosen id; version CAS, not a log; not governed; `Read`/`Author`; model text verbatim; `__` ids reserved and the seed marker; seeded once per rule; save on blur, not per keystroke; `listScenarios` treats 404 as empty); Rejected (attach to model type; server-generated ids; save per keystroke; expected-assertions); Outcome (test counts as measured).

- [ ] **Step 3: Full verification**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet build 2>&1 | grep -E "Build succeeded|Build FAILED| error " | sort -u | head -3
env -u MallocStackLogging -u MallocNanoZone dotnet test --no-build 2>&1 | grep -E "Passed!|Failed!" | sed -E 's/ \[.*//'
env -u MallocStackLogging -u MallocNanoZone ~/.dotnet/dotnet test src/Motiv.Serialization.Tests -f net8.0 --no-build 2>&1 | grep -E "Passed!|Failed!"
pnpm -C ui/packages/rules-core test 2>&1 | tail -3 && pnpm -C ui/apps/studio test 2>&1 | tail -3 && pnpm -C ui/apps/studio typecheck 2>&1 | tail -2
```

Expected: every project `Passed!`, UI suites green. Then the e2e suite, which boots the real Studio host and relies on the seeds: run `pnpm -C ui/apps/studio e2e` with the sandbox disabled. If port 5100 is held by another checkout (memory note), set `baseURL` via the config's env override to a spare port for this run and say so in the PR; if it cannot run, say which spec files were not run and why.

- [ ] **Step 4: The mandatory `code-simplifier` pass**

Spawn `code-simplifier:code-simplifier` over `git diff claude/rules-engine-criticisms-5b8447...HEAD -- src/ ui/`, foreground commands only, no commits; apply what it finds; re-run the affected suites; commit as "simplifier pass".

- [ ] **Step 5: Commit docs, push, open the PR**

```bash
git add docs CONTEXT.md
git commit -m "Docs and design — persisted scenarios (slice 2 of decision reproduction)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push -u origin claude/persisted-scenarios
```

Open the PR with `gh pr create --base claude/rules-engine-criticisms-5b8447` (stacked on #265; GitHub retargets to `main` when #265 merges), describing: the store seam and its CAS, the routes and grants, the `__` reservation and seed marker, the Studio save-on-blur behaviour, what the a11y sweep and e2e did or did not run, and the test counts.
