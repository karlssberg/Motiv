# Spec 2C — EF Core Reference Store Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Motiv.Serialization.EntityFrameworkCore` — a durable, multi-process-safe authoring store where the `(Name, Version)` primary key is enforced by a database — and switch the sample app onto it without losing its rule history.

**Architecture:** A new EF Core package maps its own row entities (distinct from the SDK's `StoredRuleVersion` / `StoredProposition` records) onto three tables, and implements `IRuleStore` / `IPropositionStore` against a `IDbContextFactory` so each operation gets a fresh, non-shared context. Conflict detection reads current versions inside the transaction and falls back to a `DbUpdateException` re-read, inspecting no provider error codes. A shared, source-linked conformance suite defines what it means to *be* a store, and every implementation — in-memory, JSON file, EF — derives from it.

**Tech Stack:** .NET 10, EF Core 10 (SQLite / Npgsql / SQL Server providers), xunit 2.9, Shouldly 4.3.

**Design doc:** [`docs/superpowers/specs/2026-08-18-spec-2c-ef-reference-store-design.md`](../specs/2026-08-18-spec-2c-ef-reference-store-design.md)

## Global Constraints

- **TDD is mandatory** (CLAUDE.md): write the failing test, run it, confirm it fails for the right reason, then implement. Never write implementation code without a test.
- **Run tests with the user-local runtime:** prefix every `dotnet test` with `DOTNET_ROOT=$HOME/.dotnet`. Without it, net8.0/net9.0 test targets fail to launch.
- **`net472` never runs locally but CI builds it.** Run a bare `dotnet build Motiv.slnx` before pushing. `Motiv.Serialization.Tests` targets `net8.0;net9.0;net472;net10.0`, so anything source-linked into it must compile on all four.
- **No `DateTimeOffset.UnixEpoch`** — unavailable on net472/netstandard2.0. Build epochs by hand: `new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)`.
- **`TreatWarningsAsErrors` is on** repo-wide (`Directory.Build.props`). A warning fails the build.
- **Central package management** is on. Every new package needs a `<PackageVersion>` entry in `Directory.Packages.props` before a `<PackageReference>` can name it.
- **New projects must be added to `Motiv.slnx`**, or CI will not build them.
- **Run the full solution suite before claiming done** (CLAUDE.md): example projects assert on justification strings and break easily.
- **Mandatory post-implementation review** (CLAUDE.md): after tests pass, spawn a `code-simplifier` agent over the changed code and apply what it finds.
- The new EF package targets **`net10.0` only**.
- The store is a **dumb sink for *semantic* legality**. Keep identity/structural constraints (PK, NOT NULL); add nothing that encodes binding legality.

## A correction to the design doc

The design doc says the conformance suite follows the source-linking precedent set by `src/testing/ShouldlyLineEndingExtensions.cs`. **That precedent does not exist** — `grep -rn "ShouldlyLineEnding" --include="*.csproj" --include="*.cs" .` shows the file is referenced by no project and used by no code. It is orphaned.

Source-linking is still the right mechanism, because a shared *project* would have to reconcile `net472` (`Motiv.Serialization.Tests`) with `net10.0` (the other two consumers), whereas a linked source file simply compiles under whatever TFMs its host project has. So this plan **establishes** the pattern rather than following it, and gives `src/testing/` a real purpose.

Consequence for the code: the shared file cannot rely on `GlobalUsings.cs`, because `Motiv.RulesEngine.Sample.Tests` has none. It carries explicit `using` directives.

## File Structure

**Created:**

| Path | Responsibility |
|---|---|
| `src/testing/StoreConformance/RuleStoreConformance.cs` | The seven behaviours that define `IRuleStore`. Source-linked into three test projects. |
| `src/testing/StoreConformance/PropositionStoreConformance.cs` | The nine behaviours that define `IPropositionStore`. Source-linked likewise. |
| `src/Motiv.Serialization.EntityFrameworkCore/Motiv.Serialization.EntityFrameworkCore.csproj` | The package. |
| `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreDbContext.cs` | Derivable context; table and key configuration. |
| `src/Motiv.Serialization.EntityFrameworkCore/Rows.cs` | `RuleVersionRow`, `PropositionRow`, `StoreGenerationRow` + translation to/from SDK records. |
| `src/Motiv.Serialization.EntityFrameworkCore/EfRuleStore.cs` | `IRuleStore` over the context factory. |
| `src/Motiv.Serialization.EntityFrameworkCore/EfPropositionStore.cs` | `IPropositionStore` over the context factory. |
| `src/Motiv.Serialization.EntityFrameworkCore/StoreImport.cs` | Generic store-to-store copy over the public interfaces. |
| `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreServiceCollectionExtensions.cs` | `AddMotivEntityFrameworkStore`. |
| `src/Motiv.Serialization.EntityFrameworkCore.Tests/*` | Conformance derivations, DDL tests, race tests, import tests. |

**Modified:**

| Path | Change |
|---|---|
| `src/Motiv.Serialization.Tests/Rules/InMemoryRuleStoreTests.cs` | Becomes a conformance derivation. |
| `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs` | Becomes a conformance derivation. |
| `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj` | Links the conformance sources. |
| `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs` | Adds factory overloads. |
| `src/examples/Motiv.RulesEngine.Sample/Program.cs` | Wires the EF store; runs the importer. |
| `src/examples/Motiv.RulesEngine.Sample.Tests/*` | Isolation moves from temp file path to temp connection string. |
| `Directory.Packages.props`, `Motiv.slnx` | New packages and projects. |

---

### Task 1: The store conformance suite

Extract the store contract into shared base classes and rewrite the two in-memory test classes as derivations. **No production code changes in this task** — if a test breaks here, the extraction is wrong, not the store.

**Files:**
- Create: `src/testing/StoreConformance/RuleStoreConformance.cs`
- Create: `src/testing/StoreConformance/PropositionStoreConformance.cs`
- Modify: `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`
- Modify: `src/Motiv.Serialization.Tests/Rules/InMemoryRuleStoreTests.cs`
- Modify: `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Motiv.Serialization.Testing.RuleStoreConformance` with `protected abstract Task<IRuleStore> CreateStoreAsync()`, `protected virtual Task DisposeStoreAsync()`, `protected IRuleStore Store { get; }`, `protected static StoredRuleVersion Row(string name, int version, string? documentJson = "{}")`. And `Motiv.Serialization.Testing.PropositionStoreConformance` with `protected abstract Task<IPropositionStore> CreateStoreAsync()`, `protected virtual Task DisposeStoreAsync()`, `protected IPropositionStore Store { get; }`, `protected static StoredProposition Stored(string name, int version = 1)`.

- [ ] **Step 1: Create the rule conformance base class**

Create `src/testing/StoreConformance/RuleStoreConformance.cs`. The seven `[Fact]` bodies are moved verbatim from `InMemoryRuleStoreTests`, with `new InMemoryRuleStore()` replaced by the inherited `Store`.

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>
/// What it means to be an <see cref="IRuleStore"/>, as one suite every implementation derives from.
/// </summary>
/// <remarks>
/// <see cref="InMemoryRuleStore"/> claims that "a test written against it holds against Postgres".
/// This class is what makes that claim structural rather than a comment: the same behaviours run
/// against the in-memory store, the JSON file store and the EF Core store, so a divergence between
/// them is a failing test rather than a discovery in production.
/// </remarks>
public abstract class RuleStoreConformance : IAsyncLifetime
{
    // Built by hand rather than via DateTimeOffset.UnixEpoch — that static field is unavailable on
    // net472/netstandard2.0, two of Motiv.Serialization.Tests' target frameworks.
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The store under test. A fresh, empty one per test.</summary>
    protected IRuleStore Store { get; private set; } = null!;

    /// <summary>Creates an empty store. Called once per test.</summary>
    protected abstract Task<IRuleStore> CreateStoreAsync();

    /// <summary>Releases whatever <see cref="CreateStoreAsync"/> allocated. Does nothing by default.</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    /// <summary>One version row, with the identity facts a test does not care about filled in.</summary>
    protected static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    public async Task InitializeAsync() => Store = await CreateStoreAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    [Fact]
    public async Task Should_project_the_head_from_the_highest_version()
    {
        // Arrange
        await Store.AppendAsync([Row("a", 2, """{"v":2}""")], default);
        await Store.AppendAsync([Row("a", 3, """{"v":3}""")], default);

        // Act
        var heads = Store.Load();

        // Assert — head is a projection, never a stored duplicate, so it cannot diverge
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(3);
        heads[0].DocumentJson!.ShouldBe("""{"v":3}""");
    }

    [Fact]
    public async Task Should_keep_a_null_document_as_a_head_rather_than_an_absent_row()
    {
        // Arrange — a revert records that the rule went back to the compiled default
        await Store.AppendAsync([Row("a", 1, """{"v":1}""")], default);
        await Store.AppendAsync([Row("a", 2, documentJson: null)], default);

        // Act
        var heads = Store.Load();

        // Assert
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(2);
        heads[0].DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_reject_a_duplicate_name_and_version_as_a_conflict()
    {
        // Arrange — this is the cross-process compare-and-set: two replicas both computing next = 2
        await Store.AppendAsync([Row("a", 1)], default);
        await Store.AppendAsync([Row("a", 2, """{"winner":true}""")], default);

        // Act
        var result = await Store.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);
        Store.Load()[0].DocumentJson!.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_append_a_whole_batch_or_none_of_it()
    {
        // Arrange — an envelope's rows must not land half-way; the second row conflicts
        await Store.AppendAsync([Row("b", 1)], default);

        // Act
        var result = await Store.AppendAsync([Row("a", 1), Row("b", 1)], default);

        // Assert — 'a' must not have landed
        result.IsConflict.ShouldBeTrue();
        result.Name!.ShouldBe("b");
        Store.Load().ShouldHaveSingleItem();
        Store.Load()[0].Name.ShouldBe("b");
    }

    [Fact]
    public async Task Should_move_the_generation_forward_on_every_successful_append()
    {
        // Arrange
        var before = await Store.GetGenerationAsync(default);

        // Act
        await Store.AppendAsync([Row("a", 1)], default);
        var after = await Store.GetGenerationAsync(default);

        // Assert
        after.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_not_move_the_generation_on_a_rejected_append()
    {
        // Arrange
        await Store.AppendAsync([Row("a", 1)], default);
        var before = await Store.GetGenerationAsync(default);

        // Act
        await Store.AppendAsync([Row("a", 1)], default);

        // Assert — a rejected write changed nothing, so replicas must not be told to rebuild
        (await Store.GetGenerationAsync(default)).ShouldBe(before);
    }

    [Fact]
    public async Task Should_return_the_whole_history_of_a_name_in_version_order()
    {
        // Arrange
        await Store.AppendAsync([Row("a", 2)], default);
        await Store.AppendAsync([Row("a", 1)], default);

        // Act
        var history = await Store.HistoryAsync("a", default);

        // Assert — kept forever, in order, so "what did v1 say?" is always answerable
        history.Select(row => row.Version).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Should_read_the_same_heads_asynchronously_as_synchronously()
    {
        // Arrange — Load and LoadAsync are separate methods for startup vs refresh, not two answers
        await Store.AppendAsync([Row("a", 1)], default);

        // Act
        var asynchronous = await Store.LoadAsync(default);

        // Assert
        asynchronous.Select(head => head.Name).ShouldBe(Store.Load().Select(head => head.Name));
    }
}
```

Note the eighth test: `Load`/`LoadAsync` agreement had no rule-side equivalent, though the proposition suite has one. It is added here because an EF implementation could easily let the two diverge.

- [ ] **Step 2: Create the proposition conformance base class**

Create `src/testing/StoreConformance/PropositionStoreConformance.cs`. Bodies moved verbatim from `InMemoryPropositionStoreTests`, with `new InMemoryPropositionStore()` replaced by `Store`.

```csharp
using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>
/// What it means to be an <see cref="IPropositionStore"/>, as one suite every implementation
/// derives from — the proposition-side twin of <see cref="RuleStoreConformance"/>.
/// </summary>
public abstract class PropositionStoreConformance : IAsyncLifetime
{
    /// <summary>The store under test. A fresh, empty one per test.</summary>
    protected IPropositionStore Store { get; private set; } = null!;

    /// <summary>Creates an empty store. Called once per test.</summary>
    protected abstract Task<IPropositionStore> CreateStoreAsync();

    /// <summary>Releases whatever <see cref="CreateStoreAsync"/> allocated. Does nothing by default.</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    /// <summary>One proposition row, with a document that binds nowhere in particular.</summary>
    protected static StoredProposition Stored(string name, int version = 1) =>
        new(name, "customer", $$"""{ "rule": { "spec": "is-active", "name": "{{name}}" } }""", version, null);

    public async Task InitializeAsync() => Store = await CreateStoreAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    [Fact]
    public void Should_start_empty()
    {
        // Act & Assert
        Store.Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_round_trip_a_saved_proposition()
    {
        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("customer.is-eligible")), default);

        // Assert
        var loaded = Store.Load();
        loaded.Count.ShouldBe(1);
        loaded[0].Name.ShouldBe("customer.is-eligible");
        loaded[0].ModelType.ShouldBe("customer");
        loaded[0].Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_replace_a_proposition_saved_under_the_same_name()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Assert
        Store.Load().Count.ShouldBe(1);
        Store.Load()[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_delete_by_name()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);
        await Store.WriteAsync(PropositionBatch.Save(Stored("b")), default);

        // Act
        await Store.WriteAsync(PropositionBatch.Delete("a"), default);

        // Assert
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_ignore_deleting_an_absent_name()
    {
        // Act & Assert — the store is a dumb sink; the set decides what is legal. Not throwing is the assertion.
        await Store.WriteAsync(PropositionBatch.Delete("absent"), default);
    }

    [Fact]
    public async Task Should_write_a_save_and_a_delete_in_one_batch()
    {
        // Arrange — the batch shape is what makes an envelope all-or-nothing
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Act
        await Store.WriteAsync(new PropositionBatch([Stored("b")], ["a"]), default);

        // Assert
        Store.Load().Select(proposition => proposition.Name).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Should_read_the_same_rows_asynchronously_as_synchronously()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Act
        var asynchronous = await Store.LoadAsync(default);

        // Assert
        asynchronous.Select(row => row.Name).ShouldBe(Store.Load().Select(row => row.Name));
    }

    [Fact]
    public async Task Should_move_the_generation_when_a_write_lands()
    {
        // Arrange
        var before = await Store.GetGenerationAsync(default);

        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);

        // Assert
        (await Store.GetGenerationAsync(default)).ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_leave_the_generation_still_when_a_batch_changes_nothing()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a")), default);
        var before = await Store.GetGenerationAsync(default);

        // Act — an empty batch is not a write
        await Store.WriteAsync(new PropositionBatch([], []), default);

        // Assert — a poller that rebuilt on this would rebuild forever
        (await Store.GetGenerationAsync(default)).ShouldBe(before);
    }
}
```

- [ ] **Step 3: Link the conformance sources into `Motiv.Serialization.Tests`**

Add to `src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj`, after the existing `<ItemGroup>` holding the `ProjectReference`:

```xml
    <!--
        The store contract, shared as source rather than as a project: this project targets net472
        while the EF Core test project is net10.0-only, and a linked file compiles under whatever
        frameworks its host has, where a shared project would have to reconcile them.
    -->
    <ItemGroup>
        <Compile Include="..\testing\StoreConformance\*.cs" LinkBase="StoreConformance" />
    </ItemGroup>
```

- [ ] **Step 4: Rewrite `InMemoryRuleStoreTests` as a derivation**

Replace the entire contents of `src/Motiv.Serialization.Tests/Rules/InMemoryRuleStoreTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Rules;

/// <summary>
/// The in-memory store against the shared store contract. It is the oracle the other
/// implementations are held to, so it must pass the same suite they do.
/// </summary>
public class InMemoryRuleStoreTests : RuleStoreConformance
{
    protected override Task<IRuleStore> CreateStoreAsync() =>
        Task.FromResult<IRuleStore>(new InMemoryRuleStore());
}
```

- [ ] **Step 5: Rewrite `InMemoryPropositionStoreTests` as a derivation**

Replace the entire contents of `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.Tests.Propositions;

/// <summary>The in-memory proposition store against the shared store contract.</summary>
public class InMemoryPropositionStoreTests : PropositionStoreConformance
{
    protected override Task<IPropositionStore> CreateStoreAsync() =>
        Task.FromResult<IPropositionStore>(new InMemoryPropositionStore());
}
```

- [ ] **Step 6: Run the tests**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj -f net10.0 --filter "FullyQualifiedName~StoreTests"
```

Expected: PASS — 8 rule tests and 9 proposition tests. If any fail, the extraction changed behaviour; fix the extraction, not the store.

- [ ] **Step 7: Verify net472 still compiles**

```bash
dotnet build src/Motiv.Serialization.Tests/Motiv.Serialization.Tests.csproj
```

Expected: build succeeds for all four TFMs. This is the step that catches a `DateTimeOffset.UnixEpoch` or collection-expression slip.

- [ ] **Step 8: Commit**

```bash
git add src/testing/StoreConformance src/Motiv.Serialization.Tests
git commit -m "test(stores): extract the store contract into a shared conformance suite"
```

---

### Task 2: The EF package skeleton, context and rows

**Files:**
- Modify: `Directory.Packages.props`
- Create: `src/Motiv.Serialization.EntityFrameworkCore/Motiv.Serialization.EntityFrameworkCore.csproj`
- Create: `src/Motiv.Serialization.EntityFrameworkCore/Rows.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreDbContext.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/SchemaTests.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/SqliteStoreFixture.cs`
- Modify: `Motiv.slnx`

**Interfaces:**
- Consumes: nothing from Task 1 (the conformance suite is used from Task 3 onward).
- Produces: `MotivStoreDbContext` with `DbSet<RuleVersionRow> RuleVersions`, `DbSet<PropositionRow> Propositions`, `DbSet<StoreGenerationRow> StoreGenerations`; row types `RuleVersionRow`, `PropositionRow`, `StoreGenerationRow`; extension methods `RuleVersionRow.ToRecord()`, `StoredRuleVersion.ToRow()`, `PropositionRow.ToRecord()`, `StoredProposition.ToRow()`; and the test helper `SqliteStoreFixture` with `Task<SqliteStoreFixture> CreateAsync()`, `IDbContextFactory<MotivStoreDbContext> Factory`, `ValueTask DisposeAsync()`.

- [ ] **Step 1: Add the package versions**

In `Directory.Packages.props`, inside the existing `<ItemGroup>`, next to the existing `Microsoft.EntityFrameworkCore.Sqlite` line:

```xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.9" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.9" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

If `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0 does not restore, run `dotnet package search Npgsql.EntityFrameworkCore.PostgreSQL` and take the highest stable 10.x.

- [ ] **Step 2: Create the package project**

Create `src/Motiv.Serialization.EntityFrameworkCore/Motiv.Serialization.EntityFrameworkCore.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <IsPackable>true</IsPackable>
        <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
        <PackageId>Motiv.Serialization.EntityFrameworkCore</PackageId>
        <Title>Motiv.Serialization.EntityFrameworkCore</Title>
        <Description>An EF Core authoring store for Motiv rules and propositions, over SQLite, PostgreSQL or SQL Server.</Description>
        <PackageTags>Motiv, Rules Engine, Entity Framework Core, Persistence, Specification Pattern</PackageTags>
        <PackageReadmeFile>README.md</PackageReadmeFile>
        <PackageIcon>icon.png</PackageIcon>
        <TargetFramework>net10.0</TargetFramework>
        <MinVerTagPrefix>rules-</MinVerTagPrefix>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="MinVer" PrivateAssets="all" />
        <PackageReference Include="Microsoft.EntityFrameworkCore" />
        <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
        <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
        <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
    </ItemGroup>

    <ItemGroup>
        <InternalsVisibleTo Include="$(AssemblyName).Tests" />
    </ItemGroup>

    <ItemGroup>
        <None Include="..\..\README.md" Pack="true" PackagePath="\" />
        <None Include="..\..\LICENSE" Pack="true" PackagePath="\" />
        <None Include="..\..\icon.png" Pack="true" PackagePath="\" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Motiv.Serialization\Motiv.Serialization.csproj" />
    </ItemGroup>

    <!--
        SQLitePCLRaw.lib.e_sqlite3 (transitive via Microsoft.EntityFrameworkCore.Sqlite) is flagged
        by NuGet audit for GHSA-2m69-gcr7-jv3q (CVE-2025-6965), a SQLite memory-corruption issue in
        an obscure aggregate-query edge case. No patched version is published for NuGet yet. This
        store issues no aggregate queries over untrusted input, so the advisory is suppressed here.
    -->
    <ItemGroup>
        <NuGetAuditSuppress Include="https://github.com/advisories/GHSA-2m69-gcr7-jv3q" />
    </ItemGroup>

</Project>
```

`MinVerTagPrefix` is `rules-` rather than `v`, because ticket 06 puts the rules stack on its own 0.x version train so its churn does not drag `Motiv`'s major.

- [ ] **Step 3: Create the row entities and translation**

Create `src/Motiv.Serialization.EntityFrameworkCore/Rows.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>One row of the append-only rule version log.</summary>
/// <remarks>
/// Deliberately not <see cref="StoredRuleVersion"/> itself. Keeping the persisted shape separate
/// keeps <c>Motiv.Serialization</c> free of any EF dependency, and makes the schema an artefact this
/// package owns — so an SDK field addition breaks <see cref="RowMapping"/> at compile time rather
/// than being silently mapped by EF's conventions.
/// </remarks>
public class RuleVersionRow
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? DocumentJson { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string? ChangeNote { get; set; }
    public string? ApprovalRef { get; set; }
    public string? BuildId { get; set; }
}

/// <summary>One authored proposition, keyed by name. Replaced in place, never appended.</summary>
public class PropositionRow
{
    public string Name { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string DocumentJson { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Where one store stands. Two rows exist — <c>rules</c> and <c>propositions</c> — because the two
/// stores are never written in the same transaction and so share no sequence.
/// </summary>
public class StoreGenerationRow
{
    public string Scope { get; set; } = string.Empty;
    public long Generation { get; set; }
}

/// <summary>
/// Between the persisted rows and the SDK's records. Positional-record construction is the point:
/// add a parameter to <see cref="StoredRuleVersion"/> and this file stops compiling, which is the
/// loud break a schema change deserves.
/// </summary>
internal static class RowMapping
{
    public static StoredRuleVersion ToRecord(this RuleVersionRow row) =>
        new(row.Name, row.Version, row.DocumentJson, row.Author, row.TimestampUtc,
            row.ChangeNote, row.ApprovalRef, row.BuildId);

    public static RuleVersionRow ToRow(this StoredRuleVersion version) =>
        new()
        {
            Name = version.Name,
            Version = version.Version,
            DocumentJson = version.DocumentJson,
            Author = version.Author,
            TimestampUtc = version.TimestampUtc,
            ChangeNote = version.ChangeNote,
            ApprovalRef = version.ApprovalRef,
            BuildId = version.BuildId,
        };

    public static StoredProposition ToRecord(this PropositionRow row) =>
        new(row.Name, row.ModelType, row.DocumentJson, row.Version, row.Description);

    public static PropositionRow ToRow(this StoredProposition proposition) =>
        new()
        {
            Name = proposition.Name,
            ModelType = proposition.ModelType,
            DocumentJson = proposition.DocumentJson,
            Version = proposition.Version,
            Description = proposition.Description,
        };
}
```

- [ ] **Step 4: Create the DbContext**

Create `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The authoring store's schema: one consistent unit and one backup unit, holding rules,
/// propositions and where each store stands.
/// </summary>
/// <remarks>
/// <para>
/// Derivable, following <c>Microsoft.AspNetCore.Identity.EntityFrameworkCore</c>: an adopter derives
/// this to add their own columns and owns their migrations, so an SDK migration can never conflict
/// with an adopter column.
/// </para>
/// <para>
/// The constraints here are identity and structure only — primary keys and NOT NULL. Nothing encodes
/// binding legality, which <c>RuleSet</c> decides and quarantine-on-load revalidates. The
/// <c>(Name, Version)</c> key on <see cref="RuleVersions"/> is the exception that proves the rule: it
/// is structural, and it is the cross-process compare-and-set.
/// </para>
/// </remarks>
public class MotivStoreDbContext : DbContext
{
    /// <summary>For the common case: a context configured for itself.</summary>
    public MotivStoreDbContext(DbContextOptions<MotivStoreDbContext> options) : base(options)
    {
    }

    /// <summary>For a derived context, which passes its own options type.</summary>
    protected MotivStoreDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>The append-only rule version log.</summary>
    public DbSet<RuleVersionRow> RuleVersions => Set<RuleVersionRow>();

    /// <summary>Authored propositions, one row per name.</summary>
    public DbSet<PropositionRow> Propositions => Set<PropositionRow>();

    /// <summary>Where each store stands, one row per scope.</summary>
    public DbSet<StoreGenerationRow> StoreGenerations => Set<StoreGenerationRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RuleVersionRow>(entity =>
        {
            entity.ToTable("MotivRuleVersion");
            entity.HasKey(row => new { row.Name, row.Version });
            entity.Property(row => row.Name).IsRequired();
            entity.Property(row => row.Author).IsRequired();
            // Portable text, not a native jsonb/json column: the sink never queries into the
            // document, so native JSON buys nothing and would fork the schema per provider.
            entity.Property(row => row.DocumentJson);
        });

        modelBuilder.Entity<PropositionRow>(entity =>
        {
            entity.ToTable("MotivProposition");
            entity.HasKey(row => row.Name);
            entity.Property(row => row.ModelType).IsRequired();
            entity.Property(row => row.DocumentJson).IsRequired();
        });

        modelBuilder.Entity<StoreGenerationRow>(entity =>
        {
            entity.ToTable("MotivStoreGeneration");
            entity.HasKey(row => row.Scope);
        });
    }
}
```

- [ ] **Step 5: Create the test project**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <OutputType>Library</OutputType>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" />
        <PackageReference Include="Shouldly" />
        <PackageReference Include="xunit" />
        <PackageReference Include="xunit.runner.visualstudio">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
        <PackageReference Include="coverlet.collector">
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            <PrivateAssets>all</PrivateAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Motiv.Serialization.EntityFrameworkCore\Motiv.Serialization.EntityFrameworkCore.csproj" />
    </ItemGroup>

    <ItemGroup>
        <Compile Include="..\testing\StoreConformance\*.cs" LinkBase="StoreConformance" />
    </ItemGroup>

    <ItemGroup>
        <NuGetAuditSuppress Include="https://github.com/advisories/GHSA-2m69-gcr7-jv3q" />
    </ItemGroup>

</Project>
```

- [ ] **Step 6: Create the SQLite test fixture**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/SqliteStoreFixture.cs`. A real temp file, not SQLite in-memory: primary-key enforcement and transactions must be the database's, or the tests prove nothing.

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization.EntityFrameworkCore;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// A throwaway SQLite database on disk, plus a context factory over it. On disk rather than
/// in-memory so the primary key and the transactions under test are the database's own.
/// </summary>
public sealed class SqliteStoreFixture : IAsyncDisposable
{
    private readonly string _path;

    private SqliteStoreFixture(string path, IDbContextFactory<MotivStoreDbContext> factory)
    {
        _path = path;
        Factory = factory;
    }

    /// <summary>Opens a fresh context per call, as the stores do.</summary>
    public IDbContextFactory<MotivStoreDbContext> Factory { get; }

    /// <summary>Creates the file and the schema.</summary>
    public static async Task<SqliteStoreFixture> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"motiv-store-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<MotivStoreDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var factory = new TestContextFactory(options);

        await using var context = factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync();

        return new SqliteStoreFixture(path, factory);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        // Pooled connections keep a handle on the file, so the delete below fails without this.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Implements only the synchronous member: <c>CreateDbContextAsync</c> has a default interface
    /// implementation that forwards to it.
    /// </summary>
    private sealed class TestContextFactory(DbContextOptions<MotivStoreDbContext> options)
        : IDbContextFactory<MotivStoreDbContext>
    {
        public MotivStoreDbContext CreateDbContext() => new(options);
    }
}
```

- [ ] **Step 7: Write the failing schema test**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/SchemaTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

public class SchemaTests
{
    [Fact]
    public async Task Should_create_the_three_tables()
    {
        // Arrange
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert
        script.ShouldContain("MotivRuleVersion");
        script.ShouldContain("MotivProposition");
        script.ShouldContain("MotivStoreGeneration");
    }

    [Fact]
    public async Task Should_key_the_version_log_on_name_and_version()
    {
        // Arrange — this key is the cross-process compare-and-set, not a formality
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var key = context.Model.FindEntityType(typeof(RuleVersionRow))!.FindPrimaryKey()!;

        // Assert
        key.Properties.Select(property => property.Name).ShouldBe(["Name", "Version"]);
    }
}
```

- [ ] **Step 8: Register the projects in the solution**

In `Motiv.slnx`, add alongside the other `src/Motiv.*` entries:

```xml
  <Project Path="src/Motiv.Serialization.EntityFrameworkCore/Motiv.Serialization.EntityFrameworkCore.csproj" />
  <Project Path="src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj" />
```

Match the surrounding element style exactly — open `Motiv.slnx` and copy the shape of an existing line.

- [ ] **Step 9: Run the schema tests**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj
```

Expected: both PASS. The conformance sources are linked but no class derives from them yet, so they contribute no tests.

- [ ] **Step 10: Commit**

```bash
git add Directory.Packages.props Motiv.slnx src/Motiv.Serialization.EntityFrameworkCore src/Motiv.Serialization.EntityFrameworkCore.Tests
git commit -m "feat(ef-store): add the EF Core package, schema and test fixture"
```

---

### Task 3: `EfRuleStore`

The conflict path is this slice's central risk. Write it against the conformance suite, which already exists and cannot be edited to accommodate the implementation.

**Files:**
- Create: `src/Motiv.Serialization.EntityFrameworkCore/EfRuleStore.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/EfRuleStoreTests.cs`

**Interfaces:**
- Consumes: `RuleStoreConformance` (Task 1); `MotivStoreDbContext`, `RuleVersionRow`, `StoreGenerationRow`, `RowMapping.ToRow`/`ToRecord`, `SqliteStoreFixture` (Task 2).
- Produces: `public sealed class EfRuleStore(IDbContextFactory<MotivStoreDbContext> contextFactory) : IRuleStore`, and `internal static class GenerationScopes` with `public const string Rules = "rules"` and `public const string Propositions = "propositions"`.

- [ ] **Step 1: Write the failing conformance derivation**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/EfRuleStoreTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The EF Core store against the same contract the in-memory store passes. This class is the point
/// of the conformance suite: the claim that a test written against InMemoryRuleStore holds against a
/// database is checked here rather than asserted in a comment.
/// </summary>
public class EfRuleStoreTests : RuleStoreConformance
{
    private SqliteStoreFixture _fixture = null!;

    protected override async Task<IRuleStore> CreateStoreAsync()
    {
        _fixture = await SqliteStoreFixture.CreateAsync();
        return new EfRuleStore(_fixture.Factory);
    }

    protected override async Task DisposeStoreAsync() => await _fixture.DisposeAsync();
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~EfRuleStoreTests"
```

Expected: FAIL to compile with `CS0246: The type or namespace name 'EfRuleStore' could not be found`.

- [ ] **Step 3: Implement `EfRuleStore`**

Create `src/Motiv.Serialization.EntityFrameworkCore/EfRuleStore.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>The scope keys of the two <see cref="StoreGenerationRow"/> rows.</summary>
internal static class GenerationScopes
{
    public const string Rules = "rules";
    public const string Propositions = "propositions";
}

/// <summary>
/// The rule store over a relational database, where the <c>(Name, Version)</c> primary key is
/// enforced by the database rather than by a re-read of a file.
/// </summary>
/// <remarks>
/// <para>
/// Conflicts are detected without inspecting any provider error code. The common path reads the
/// versions already taken inside the transaction — which is also the only way to obtain the
/// <c>currentVersion</c> a conflict must carry, since an exception cannot supply it. The race path,
/// where another replica commits between that read and the insert, catches
/// <see cref="DbUpdateException"/> and re-reads to decide whether it was a conflict or something
/// else entirely. That is what makes proving this store on SQLite generalise to PostgreSQL and SQL
/// Server: the only behaviour relied on is EF's own.
/// </para>
/// <para>
/// A fresh context per operation, because the store is a singleton and <see cref="DbContext"/> is
/// not thread-safe — and because it structurally guarantees this store never shares a transaction
/// with the proposition store.
/// </para>
/// </remarks>
public sealed class EfRuleStore(IDbContextFactory<MotivStoreDbContext> contextFactory) : IRuleStore
{
    /// <inheritdoc />
    public IReadOnlyList<StoredRule> Load()
    {
        using var context = contextFactory.CreateDbContext();
        return ProjectHeads(context.RuleVersions.AsNoTracking().ToList());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return ProjectHeads(
            await context.RuleVersions.AsNoTracking().ToListAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadGenerationAsync(context, GenerationScopes.Rules, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken)
    {
        // An empty batch is not a write: moving the generation would make every replica rebuild its
        // whole world, on a timer, for nothing.
        if (versions.Count == 0)
            return RuleAppendResult.Appended;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var conflict = await FindConflictAsync(context, versions, cancellationToken);
        if (conflict is not null)
            return conflict;

        foreach (var version in versions)
            context.RuleVersions.Add(version.ToRow());

        await BumpGenerationAsync(context, GenerationScopes.Rules, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return RuleAppendResult.Appended;
        }
        catch (DbUpdateException)
        {
            // Another replica committed between the read above and this insert. Roll back and ask
            // the store what happened: a row of ours now present means we lost the race; anything
            // else — a full disk, a dropped connection — is not a version conflict and must not be
            // reported as one.
            await transaction.RollbackAsync(cancellationToken);

            await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
            var raced = await FindConflictAsync(fresh, versions, cancellationToken);
            if (raced is not null)
                return raced;

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.RuleVersions.AsNoTracking()
            .Where(row => row.Name == name)
            .OrderBy(row => row.Version)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <summary>
    /// The first batch row whose version is already taken, or null when the batch is clear. Reads
    /// every name at once: the batch is all-or-nothing, so one round trip decides the whole thing.
    /// </summary>
    private static async Task<RuleAppendResult?> FindConflictAsync(
        MotivStoreDbContext context,
        IReadOnlyList<StoredRuleVersion> versions,
        CancellationToken cancellationToken)
    {
        var names = versions.Select(version => version.Name).Distinct(StringComparer.Ordinal).ToList();

        var existing = await context.RuleVersions.AsNoTracking()
            .Where(row => names.Contains(row.Name))
            .Select(row => new { row.Name, row.Version })
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
            return null;

        var taken = new HashSet<string>(
            existing.Select(row => Key(row.Name, row.Version)), StringComparer.Ordinal);

        var highest = existing
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(row => row.Version), StringComparer.Ordinal);

        foreach (var version in versions)
        {
            if (taken.Contains(Key(version.Name, version.Version)))
                return RuleAppendResult.Conflict(version.Name, highest[version.Name]);
        }

        return null;
    }

    // A composite key as one string, because a HashSet of value tuples would box on the target
    // frameworks and the readability is worth more here than the allocation.
    private static string Key(string name, int version) => $"{name} {version}";

    /// <summary>The head projection: the highest version per name, reduced to what a load needs.</summary>
    private static IReadOnlyList<StoredRule> ProjectHeads(List<RuleVersionRow> rows) =>
    [
        .. rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .Select(group => group.Aggregate((head, row) => row.Version > head.Version ? row : head))
            .Select(head => new StoredRule(head.Name, head.Version, head.DocumentJson))
    ];

    private static async Task<long> ReadGenerationAsync(
        MotivStoreDbContext context, string scope, CancellationToken cancellationToken)
    {
        var row = await context.StoreGenerations.AsNoTracking()
            .SingleOrDefaultAsync(generation => generation.Scope == scope, cancellationToken);

        return row?.Generation ?? 0;
    }

    /// <summary>
    /// Moves this store's generation, tracked so the increment is written by the caller's
    /// <c>SaveChangesAsync</c> — the bump and the write it describes land in one transaction.
    /// </summary>
    internal static async Task BumpGenerationAsync(
        MotivStoreDbContext context, string scope, CancellationToken cancellationToken)
    {
        var row = await context.StoreGenerations
            .SingleOrDefaultAsync(generation => generation.Scope == scope, cancellationToken);

        if (row is null)
            context.StoreGenerations.Add(new StoreGenerationRow { Scope = scope, Generation = 1 });
        else
            row.Generation++;
    }
}
```

- [ ] **Step 4: Run the conformance suite**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~EfRuleStoreTests"
```

Expected: all 8 PASS. If `Should_append_a_whole_batch_or_none_of_it` fails, the pre-read is not covering every row in the batch. If `Should_not_move_the_generation_on_a_rejected_append` fails, the bump is happening before the conflict check.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore/EfRuleStore.cs src/Motiv.Serialization.EntityFrameworkCore.Tests/EfRuleStoreTests.cs
git commit -m "feat(ef-store): implement EfRuleStore against the store conformance suite"
```

---

### Task 4: `EfPropositionStore`

**Files:**
- Create: `src/Motiv.Serialization.EntityFrameworkCore/EfPropositionStore.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/EfPropositionStoreTests.cs`

**Interfaces:**
- Consumes: `PropositionStoreConformance` (Task 1); `SqliteStoreFixture`, `PropositionRow`, `RowMapping` (Task 2); `GenerationScopes`, `EfRuleStore.BumpGenerationAsync` (Task 3).
- Produces: `public sealed class EfPropositionStore(IDbContextFactory<MotivStoreDbContext> contextFactory) : IPropositionStore`.

- [ ] **Step 1: Write the failing conformance derivation**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/EfPropositionStoreTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>The EF Core proposition store against the shared store contract.</summary>
public class EfPropositionStoreTests : PropositionStoreConformance
{
    private SqliteStoreFixture _fixture = null!;

    protected override async Task<IPropositionStore> CreateStoreAsync()
    {
        _fixture = await SqliteStoreFixture.CreateAsync();
        return new EfPropositionStore(_fixture.Factory);
    }

    protected override async Task DisposeStoreAsync() => await _fixture.DisposeAsync();
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~EfPropositionStoreTests"
```

Expected: FAIL to compile with `CS0246: The type or namespace name 'EfPropositionStore' could not be found`.

- [ ] **Step 3: Implement `EfPropositionStore`**

Create `src/Motiv.Serialization.EntityFrameworkCore/EfPropositionStore.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The proposition store over a relational database — the twin of <see cref="EfRuleStore"/>, and
/// never written in the same transaction as it.
/// </summary>
/// <remarks>
/// There is no conflict outcome here, because the contract has none: a proposition row is replaced
/// in place, last writer wins, exactly as <c>InMemoryPropositionStore</c> behaves. The append-only
/// version log the rule side has is a deliberate asymmetry, deferred to its own spec because closing
/// it is a breaking change to <see cref="IPropositionStore"/>.
/// </remarks>
public sealed class EfPropositionStore(IDbContextFactory<MotivStoreDbContext> contextFactory)
    : IPropositionStore
{
    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        using var context = contextFactory.CreateDbContext();
        return [.. context.Propositions.AsNoTracking().ToList().Select(row => row.ToRecord())];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Propositions.AsNoTracking().ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <inheritdoc />
    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await context.StoreGenerations.AsNoTracking()
            .SingleOrDefaultAsync(
                generation => generation.Scope == GenerationScopes.Propositions, cancellationToken);

        return row?.Generation ?? 0;
    }

    /// <inheritdoc />
    public async Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        // An empty batch is not a write — see EfRuleStore.AppendAsync for why that matters.
        if (batch.Saves.Count == 0 && batch.Deletes.Count == 0)
            return;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var save in batch.Saves)
        {
            var existing = await context.Propositions
                .SingleOrDefaultAsync(row => row.Name == save.Name, cancellationToken);

            if (existing is null)
            {
                context.Propositions.Add(save.ToRow());
                continue;
            }

            existing.ModelType = save.ModelType;
            existing.DocumentJson = save.DocumentJson;
            existing.Version = save.Version;
            existing.Description = save.Description;
        }

        foreach (var name in batch.Deletes)
        {
            var existing = await context.Propositions
                .SingleOrDefaultAsync(row => row.Name == name, cancellationToken);

            // An absent name is not an error: the store is a dumb sink, and the set decides legality.
            if (existing is not null)
                context.Propositions.Remove(existing);
        }

        await EfRuleStore.BumpGenerationAsync(
            context, GenerationScopes.Propositions, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Run the conformance suite**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~EfPropositionStoreTests"
```

Expected: all 9 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore/EfPropositionStore.cs src/Motiv.Serialization.EntityFrameworkCore.Tests/EfPropositionStoreTests.cs
git commit -m "feat(ef-store): implement EfPropositionStore against the store conformance suite"
```

---

### Task 5: Hold the JSON file stores to the same contract

A free third implementation, to check the suite is implementation-neutral rather than quietly shaped around the two that already pass. If the JSON stores fail here, that is a real finding about them, not about the suite.

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample.Tests/Motiv.RulesEngine.Sample.Tests.csproj`
- Create: `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFileRuleStoreConformanceTests.cs`
- Create: `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreConformanceTests.cs`

**Interfaces:**
- Consumes: `RuleStoreConformance`, `PropositionStoreConformance` (Task 1); the existing `JsonFileRuleStore(string path)` and `JsonFilePropositionStore(string path)` from the sample.
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Link the conformance sources into the sample test project**

Add to `src/examples/Motiv.RulesEngine.Sample.Tests/Motiv.RulesEngine.Sample.Tests.csproj`, after the existing `ProjectReference` item group:

```xml
    <ItemGroup>
        <Compile Include="..\..\testing\StoreConformance\*.cs" LinkBase="StoreConformance" />
    </ItemGroup>
```

Note the extra `..\` — this project sits two levels below `src/`, not one.

- [ ] **Step 2: Write the derivations**

Create `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFileRuleStoreConformanceTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>
/// The sample's file-backed rule store against the same contract the in-memory and EF stores pass.
/// A third implementation is what shows the suite describes the contract rather than any one store.
/// </summary>
public class JsonFileRuleStoreConformanceTests : RuleStoreConformance
{
    private string _path = string.Empty;

    protected override Task<IRuleStore> CreateStoreAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"rules-{Guid.NewGuid():N}.json");
        return Task.FromResult<IRuleStore>(new JsonFileRuleStore(_path));
    }

    protected override Task DisposeStoreAsync()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        return Task.CompletedTask;
    }
}
```

Create `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreConformanceTests.cs`:

```csharp
using Motiv.Serialization;
using Motiv.Serialization.Testing;

namespace Motiv.RulesEngine.Sample.Tests;

/// <summary>The sample's file-backed proposition store against the shared store contract.</summary>
public class JsonFilePropositionStoreConformanceTests : PropositionStoreConformance
{
    private string _path = string.Empty;

    protected override Task<IPropositionStore> CreateStoreAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"propositions-{Guid.NewGuid():N}.json");
        return Task.FromResult<IPropositionStore>(new JsonFilePropositionStore(_path));
    }

    protected override Task DisposeStoreAsync()
    {
        if (File.Exists(_path))
            File.Delete(_path);
        return Task.CompletedTask;
    }
}
```

Both stores are top-level types in the sample with no namespace declaration, so they resolve from the global namespace and need no `using`.

- [ ] **Step 3: Run them**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/examples/Motiv.RulesEngine.Sample.Tests/Motiv.RulesEngine.Sample.Tests.csproj --filter "FullyQualifiedName~Conformance"
```

Expected: all 17 PASS. If a test fails, **stop and report it** — the suite has found a real divergence in a shipped store, and whether to fix the store or narrow the contract is a decision, not a mechanical fix.

- [ ] **Step 4: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample.Tests
git commit -m "test(stores): hold the JSON file stores to the store conformance suite"
```

---

### Task 6: Provider DDL verification

Postgres and SQL Server ship as configuration, verified by generating their DDL in-process. No server, no Docker, no Testcontainers.

**Files:**
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/ProviderSchemaTests.cs`

**Interfaces:**
- Consumes: `MotivStoreDbContext` (Task 2).
- Produces: nothing later tasks depend on.

- [ ] **Step 1: Write the test**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/ProviderSchemaTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// That the model maps and its DDL is producible on every supported provider. The conformance suite
/// runs on SQLite alone, which is sound only because conflict detection inspects no provider error
/// code — so what is left unproven is the schema, and this is what proves it.
/// </summary>
public class ProviderSchemaTests
{
    public static TheoryData<string, Action<DbContextOptionsBuilder>> Providers => new()
    {
        { "SQLite", builder => builder.UseSqlite("Data Source=:memory:") },
        { "PostgreSQL", builder => builder.UseNpgsql("Host=localhost;Database=motiv") },
        { "SQL Server", builder => builder.UseSqlServer("Server=localhost;Database=motiv") },
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public void Should_generate_a_create_script_for_every_table(
        string provider, Action<DbContextOptionsBuilder> configure)
    {
        // Arrange — no connection is opened: script generation is a model operation
        var builder = new DbContextOptionsBuilder<MotivStoreDbContext>();
        configure(builder);
        using var context = new MotivStoreDbContext(builder.Options);

        // Act
        var script = context.Database.GenerateCreateScript();

        // Assert
        script.ShouldContain("MotivRuleVersion", customMessage: provider);
        script.ShouldContain("MotivProposition", customMessage: provider);
        script.ShouldContain("MotivStoreGeneration", customMessage: provider);
    }
}
```

- [ ] **Step 2: Run it**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~ProviderSchemaTests"
```

Expected: 3 PASS. If PostgreSQL fails on `DateTimeOffset`, Npgsql maps it to `timestamptz` and needs no model change — read the actual error before altering anything.

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore.Tests/ProviderSchemaTests.cs
git commit -m "test(ef-store): verify the schema generates on all three providers"
```

---

### Task 7: The store-to-store importer

It needs no EF knowledge: `Load()` gives the names, `HistoryAsync` gives every version row, `AppendAsync` replays them. So it is a generic copy over the public interfaces that happens to solve JSON to EF.

**Files:**
- Create: `src/Motiv.Serialization.EntityFrameworkCore/StoreImport.cs`
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/StoreImportTests.cs`

**Interfaces:**
- Consumes: `IRuleStore`, `IPropositionStore`, `InMemoryRuleStore`, `InMemoryPropositionStore` from `Motiv.Serialization`; `SqliteStoreFixture` (Task 2); `EfRuleStore` (Task 3); `EfPropositionStore` (Task 4).
- Produces: `public sealed record StoreImportResult(bool Imported, int RuleVersions, int Propositions)` and `public static class StoreImport` with `public static Task<StoreImportResult> CopyAsync(IRuleStore sourceRules, IRuleStore targetRules, IPropositionStore sourcePropositions, IPropositionStore targetPropositions, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/StoreImportTests.cs`:

```csharp
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

public class StoreImportTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "build-7");

    private static StoredProposition Proposition(string name) =>
        new(name, "customer", """{ "rule": { "spec": "is-active" } }""", 1, null);

    [Fact]
    public async Task Should_carry_every_version_across_not_just_the_head()
    {
        // Arrange — the audit trail is the point: a head-only import would claim the rule was
        // authored at import time, which is exactly what an approval gate cannot tolerate
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1, """{"v":1}""")], default);
        await sourceRules.AppendAsync([Row("a", 2, """{"v":2}""")], default);
        await sourceRules.AppendAsync([Row("a", 3, documentJson: null)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new EfRuleStore(fixture.Factory);

        // Act
        var result = await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        result.Imported.ShouldBeTrue();
        result.RuleVersions.ShouldBe(3);

        var history = await targetRules.HistoryAsync("a", default);
        history.Select(row => row.Version).ShouldBe([1, 2, 3]);
        history[2].DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_preserve_authorship_and_timestamps()
    {
        // Arrange — a copy that restamped these would produce a truthful-looking but false record
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync(
            [new StoredRuleVersion("a", 1, "{}", "bob", Epoch, "why", "cr-9", "build-3")], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new EfRuleStore(fixture.Factory);

        // Act
        await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        var row = (await targetRules.HistoryAsync("a", default)).ShouldHaveSingleItem();
        row.Author.ShouldBe("bob");
        row.TimestampUtc.ShouldBe(Epoch);
        row.ChangeNote.ShouldBe("why");
        row.ApprovalRef.ShouldBe("cr-9");
        row.BuildId.ShouldBe("build-3");
    }

    [Fact]
    public async Task Should_copy_propositions_too()
    {
        // Arrange
        var sourcePropositions = new InMemoryPropositionStore();
        await sourcePropositions.WriteAsync(
            PropositionBatch.Save(Proposition("customer.is-eligible")), default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetPropositions = new EfPropositionStore(fixture.Factory);

        // Act
        var result = await StoreImport.CopyAsync(
            new InMemoryRuleStore(), new EfRuleStore(fixture.Factory),
            sourcePropositions, targetPropositions, default);

        // Assert
        result.Propositions.ShouldBe(1);
        targetPropositions.Load().ShouldHaveSingleItem().Name.ShouldBe("customer.is-eligible");
    }

    [Fact]
    public async Task Should_refuse_a_target_that_already_holds_rules()
    {
        // Arrange — refusing on a non-empty target is what makes a second run harmless, with no
        // import state to track anywhere
        var sourceRules = new InMemoryRuleStore();
        await sourceRules.AppendAsync([Row("a", 1)], default);

        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var targetRules = new EfRuleStore(fixture.Factory);
        await targetRules.AppendAsync([Row("existing", 1)], default);

        // Act
        var result = await StoreImport.CopyAsync(
            sourceRules, targetRules,
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert — nothing copied, nothing thrown
        result.Imported.ShouldBeFalse();
        result.RuleVersions.ShouldBe(0);
        (await targetRules.HistoryAsync("a", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_report_nothing_imported_when_the_source_is_empty()
    {
        // Arrange
        await using var fixture = await SqliteStoreFixture.CreateAsync();

        // Act
        var result = await StoreImport.CopyAsync(
            new InMemoryRuleStore(), new EfRuleStore(fixture.Factory),
            new InMemoryPropositionStore(), new EfPropositionStore(fixture.Factory), default);

        // Assert
        result.Imported.ShouldBeTrue();
        result.RuleVersions.ShouldBe(0);
        result.Propositions.ShouldBe(0);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~StoreImportTests"
```

Expected: FAIL to compile with `CS0103: The name 'StoreImport' does not exist in the current context`.

- [ ] **Step 3: Implement the importer**

Create `src/Motiv.Serialization.EntityFrameworkCore/StoreImport.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>What a <see cref="StoreImport.CopyAsync"/> did.</summary>
/// <param name="Imported">
/// False when a target already held data and nothing was copied. Not an error: it is what makes
/// running the import on every startup harmless.
/// </param>
/// <param name="RuleVersions">How many version rows were replayed.</param>
/// <param name="Propositions">How many proposition rows were copied.</param>
public sealed record StoreImportResult(bool Imported, int RuleVersions, int Propositions);

/// <summary>
/// A one-way copy from one pair of stores into another — the migration path off the file-backed
/// stores and onto a database.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately written against <see cref="IRuleStore"/> and <see cref="IPropositionStore"/> alone,
/// with no knowledge of either end. The rule side replays the <em>whole</em> version log rather than
/// the head, because a head-only copy would restamp every rule as authored at import time and
/// destroy the audit trail an approval gate depends on.
/// </para>
/// <para>
/// Refuses a non-empty target rather than throwing or merging. A merge could not preserve version
/// numbers, and refusing means a second run is a no-op — so no import state has to be recorded
/// anywhere to keep this idempotent.
/// </para>
/// </remarks>
public static class StoreImport
{
    /// <summary>Copies both stores, or neither.</summary>
    public static async Task<StoreImportResult> CopyAsync(
        IRuleStore sourceRules,
        IRuleStore targetRules,
        IPropositionStore sourcePropositions,
        IPropositionStore targetPropositions,
        CancellationToken cancellationToken)
    {
        var existingRules = await targetRules.LoadAsync(cancellationToken);
        var existingPropositions = await targetPropositions.LoadAsync(cancellationToken);

        if (existingRules.Count > 0 || existingPropositions.Count > 0)
            return new StoreImportResult(false, 0, 0);

        var ruleVersions = 0;
        foreach (var head in await sourceRules.LoadAsync(cancellationToken))
        {
            var history = await sourceRules.HistoryAsync(head.Name, cancellationToken);
            if (history.Count == 0)
                continue;

            // One append per name, carrying that name's whole log: the batch is all-or-nothing, so
            // a name either arrives complete or not at all.
            var result = await targetRules.AppendAsync(history, cancellationToken);
            if (result.IsConflict)
            {
                throw new InvalidOperationException(
                    $"Import of rule '{result.Name}' conflicted at version {result.CurrentVersion}. " +
                    "The target was empty when the import began, so something else is writing to it.");
            }

            ruleVersions += history.Count;
        }

        var propositions = await sourcePropositions.LoadAsync(cancellationToken);
        if (propositions.Count > 0)
            await targetPropositions.WriteAsync(new PropositionBatch(propositions, []), cancellationToken);

        return new StoreImportResult(true, ruleVersions, propositions.Count);
    }
}
```

- [ ] **Step 4: Run the tests**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~StoreImportTests"
```

Expected: all 5 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore/StoreImport.cs src/Motiv.Serialization.EntityFrameworkCore.Tests/StoreImportTests.cs
git commit -m "feat(ef-store): add a store-to-store importer that carries rule history"
```

---

### Task 8: Factory overloads and the DI extension

The only change outside the new package. `AddRuleStore` and `AddPropositions` take a pre-built instance, but an EF store needs `IDbContextFactory` from the container.

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs:57-79` and `:136-149`
- Create: `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreServiceCollectionExtensions.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/StoreFactoryOverloadTests.cs`

**Interfaces:**
- Consumes: `MotivStoreDbContext` (Task 2).
- Produces: `MotivRulesBuilder.AddPropositions(Func<IServiceProvider, IPropositionStore>)`, `MotivRulesBuilder.AddRuleStore(Func<IServiceProvider, IRuleStore>, bool failFastOnQuarantine = true)`, and `IServiceCollection.AddMotivEntityFrameworkStore(Action<DbContextOptionsBuilder>)`.

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Serialization.AspNetCore.Tests/StoreFactoryOverloadTests.cs`. Follow the existing `RuleStoreWiringTests` for how a test app is built — open that file and reuse its `TestApp.Create` helper rather than inventing a new host.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.AspNetCore.Tests;

public class StoreFactoryOverloadTests
{
    [Fact]
    public void Should_resolve_a_rule_store_built_from_the_container()
    {
        // Arrange — an EF store needs IDbContextFactory from DI, so it cannot be built before
        // the container exists
        var marker = new InMemoryRuleStore();

        // Act
        using var app = TestApp.Create(builder => builder.AddRuleStore(provider =>
        {
            provider.ShouldNotBeNull();
            return marker;
        }));

        // Assert
        app.Services.GetRequiredService<IRuleStore>().ShouldBeSameAs(marker);
    }

    [Fact]
    public void Should_resolve_a_proposition_store_built_from_the_container()
    {
        // Arrange
        var marker = new InMemoryPropositionStore();

        // Act
        using var app = TestApp.Create(builder => builder.AddPropositions(_ => marker));

        // Assert
        app.Services.GetRequiredService<IPropositionStore>().ShouldBeSameAs(marker);
    }

    [Fact]
    public void Should_still_refuse_a_second_call_through_the_factory_overload()
    {
        // Arrange — the called-twice guard must not be bypassable by picking the other overload
        var act = () => TestApp.Create(builder =>
        {
            builder.AddRuleStore(new InMemoryRuleStore());
            builder.AddRuleStore(_ => new InMemoryRuleStore());
        });

        // Act & Assert
        act.ShouldThrow<InvalidOperationException>();
    }
}
```

If `TestApp.Create` returns something not `IDisposable`, drop the `using` and match whatever `RuleStoreWiringTests` does — that file is the authority on this project's host helper.

- [ ] **Step 2: Run to verify it fails**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj --filter "FullyQualifiedName~StoreFactoryOverloadTests"
```

Expected: FAIL to compile — no overload of `AddRuleStore` takes a lambda.

- [ ] **Step 3: Refactor `AddPropositions` to a factory core**

In `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`, replace the existing `AddPropositions` method with three members. The body of the private core is the existing body, with one line changed: `Services.AddSingleton<IPropositionStore>(store ?? new InMemoryPropositionStore());` becomes `Services.AddSingleton<IPropositionStore>(storeFactory);`.

```csharp
    /// <summary>
    /// Enables runtime-authored propositions, backed by the given store (in-memory when omitted).
    /// The <see cref="PropositionSet"/> shares the <see cref="RuleSet"/>'s coordinator, so a
    /// proposition edit and a rule update can never interleave.
    /// </summary>
    /// <param name="store">Where authored propositions persist, or null for in-memory.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">Propositions are already enabled. DI is
    /// last-wins, so a second call would silently discard the first store rather than layering
    /// onto it — an argument quietly ignored is worse than a refusal.</exception>
    public MotivRulesBuilder AddPropositions(IPropositionStore? store = null) =>
        AddPropositionsCore(_ => store ?? new InMemoryPropositionStore());

    /// <summary>
    /// Enables runtime-authored propositions, backed by a store built from the container. For a
    /// store with dependencies of its own — a database context factory, for instance — which
    /// therefore cannot be constructed before the container exists.
    /// </summary>
    /// <param name="storeFactory">Builds the store once the container is available.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">Propositions are already enabled.</exception>
    public MotivRulesBuilder AddPropositions(Func<IServiceProvider, IPropositionStore> storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        return AddPropositionsCore(storeFactory);
    }

    private MotivRulesBuilder AddPropositionsCore(Func<IServiceProvider, IPropositionStore> storeFactory)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(PropositionSet)))
            throw new InvalidOperationException(
                $"{nameof(AddPropositions)} has already been called. Call it once — a second call " +
                "would silently replace the first store, as DI registration is last-wins.");

        Services.AddSingleton<IPropositionStore>(storeFactory);
        Services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<MotivRulesOptions>();
            var propositions = new PropositionSet(
                provider.GetRequiredService<BindingScope>(),
                provider.GetRequiredService<IPropositionStore>(),
                options.SerializerOptions);

            foreach (var register in options.PropositionModelRegistrations)
                register(propositions);

            propositions.Load();
            return propositions;
        });
        return this;
    }
```

- [ ] **Step 4: Refactor `AddRuleStore` the same way**

```csharp
    /// <inheritdoc cref="AddRuleStoreCore" />
    /// <param name="store">Where published rules persist, or null for in-memory.</param>
    /// <param name="failFastOnQuarantine">
    /// True to refuse to boot when a stored document no longer binds; false to boot anyway and read
    /// the quarantine from the catalog.
    /// </param>
    public MotivRulesBuilder AddRuleStore(IRuleStore? store = null, bool failFastOnQuarantine = true) =>
        AddRuleStoreCore(_ => store ?? new InMemoryRuleStore(), failFastOnQuarantine);

    /// <summary>
    /// Points the live rules at a store built from the container. For a store with dependencies of
    /// its own, which therefore cannot be constructed before the container exists.
    /// </summary>
    /// <param name="storeFactory">Builds the store once the container is available.</param>
    /// <param name="failFastOnQuarantine">As the instance overload.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    /// <exception cref="InvalidOperationException">A rule store is already registered.</exception>
    public MotivRulesBuilder AddRuleStore(
        Func<IServiceProvider, IRuleStore> storeFactory, bool failFastOnQuarantine = true)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        return AddRuleStoreCore(storeFactory, failFastOnQuarantine);
    }

    /// <summary>
    /// Points the live rules at a store so a hot-swapped rule survives a restart instead of
    /// reverting to its compiled default.
    /// </summary>
    private MotivRulesBuilder AddRuleStoreCore(
        Func<IServiceProvider, IRuleStore> storeFactory, bool failFastOnQuarantine)
    {
        if (Services.Any(descriptor => descriptor.ServiceType == typeof(RuleStoreOptions)))
            throw new InvalidOperationException(
                $"{nameof(AddRuleStore)} has already been called. Call it once — a second call " +
                "would silently replace the first store, as DI registration is last-wins.");

        // Registered under the interface explicitly, as AddPropositions does for its own store: the
        // RuleSet factory resolves IRuleStore, and leaving the service type to type inference makes
        // that dependence on the parameter's declared type invisible.
        Services.AddSingleton<IRuleStore>(storeFactory);
        Services.AddSingleton(new RuleStoreOptions(failFastOnQuarantine));
        return this;
    }
```

Keep the existing XML doc comments on the public instance overloads — copy them across rather than dropping them.

- [ ] **Step 5: Check no call site becomes ambiguous**

```bash
grep -rn "AddPropositions(null\|AddRuleStore(null" --include="*.cs" src/
```

Expected: no output. A bare `null` literal would now be ambiguous between the two overloads; every current call site passes a typed instance or nothing, so none is affected.

- [ ] **Step 6: Create the DI extension in the EF package**

Create `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreServiceCollectionExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>Registers the EF Core authoring store's context factory.</summary>
public static class MotivStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IDbContextFactory{TContext}"/> for <see cref="MotivStoreDbContext"/>.
    /// A factory rather than a scoped context because the stores are singletons and
    /// <see cref="DbContext"/> is not thread-safe — and because a context per operation is what
    /// keeps the rule and proposition stores out of one another's transactions.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <param name="configure">Selects and configures the provider, e.g. <c>options.UseSqlite(...)</c>.</param>
    /// <returns>The container, to allow chained registration.</returns>
    public static IServiceCollection AddMotivEntityFrameworkStore(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddDbContextFactory<MotivStoreDbContext>(configure);
        return services;
    }
}
```

- [ ] **Step 7: Run the AspNetCore tests**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.AspNetCore.Tests/Motiv.Serialization.AspNetCore.Tests.csproj
```

Expected: all PASS, including the pre-existing `RuleStoreWiringTests` — the refactor must not change existing behaviour.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.EntityFrameworkCore/MotivStoreServiceCollectionExtensions.cs src/Motiv.Serialization.AspNetCore.Tests/StoreFactoryOverloadTests.cs
git commit -m "feat(aspnetcore): add container-built store overloads for AddRuleStore and AddPropositions"
```

---

### Task 9: Wire the sample onto the EF store

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs:182-195` and `:210`
- Modify: `src/examples/Motiv.RulesEngine.Sample.Tests/CheckoutEndpointTests.cs:119-122`
- Modify: `src/examples/Motiv.RulesEngine.Sample.Tests/GrantSourceTests.cs:25-26`

**Interfaces:**
- Consumes: `AddMotivEntityFrameworkStore`, the factory overloads (Task 8); `EfRuleStore` (Task 3); `EfPropositionStore` (Task 4); `StoreImport.CopyAsync` (Task 7).
- Produces: the configuration keys `Motiv:Store:ConnectionString` and `Motiv:Store:ImportFromJson`.

- [ ] **Step 1: Reference the EF package from the sample**

In `src/examples/Motiv.RulesEngine.Sample/Motiv.RulesEngine.Sample.csproj`, beside the existing `Motiv.Serialization.AspNetCore` reference:

```xml
        <ProjectReference Include="..\..\Motiv.Serialization.EntityFrameworkCore\Motiv.Serialization.EntityFrameworkCore.csproj" />
```

- [ ] **Step 2: Replace the store wiring in `Program.cs`**

Replace the `var rulesPath = ...` declaration and the `.AddPropositions(...)` / `.AddRuleStore(...)` lines (around lines 182-195). Keep `propositionsPath` and `rulesPath` — the importer still reads them.

```csharp
// Seam: rule and proposition persistence. Both stores are now one SQLite database rather than two
// JSON files: the (Name, Version) primary key is enforced by the database, so two replicas racing a
// publish really do produce one 200 and one 409 rather than both reading a stale file and both
// believing they won. failFastOnQuarantine keeps its default (true): under an approval gate, booting
// quietly into a quarantined rule's compiled default — behaviour nobody approved — is worse than a
// demo that refuses to boot until a stale row is repaired.
var storeConnectionString = builder.Configuration["Motiv:Store:ConnectionString"]
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "motiv-store.db")}";

builder.Services.AddMotivEntityFrameworkStore(store => store.UseSqlite(storeConnectionString));

builder.Services.AddMotivRules(registry, options)
    .AddPropositions(provider => new EfPropositionStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddGovernance(new JsonFileGateStore(gatePath))
    .AddRuleStore(provider => new EfRuleStore(
        provider.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()))
    .AddRule<CanCheckoutRule>()
    .AddRule<FraudScreeningRule>()
    .AddRule<LoyaltyDiscountRule>()
    // Seam: multi-instance convergence. Each store operation opens its own context, so two
    // processes over one database behave like two replicas; AddRefresh polls for another replica's
    // publish and rebuilds this one, so docker compose up actually converges instead of
    // demonstrating a feature that does nothing.
    .AddRefresh();
```

Add these usings at the top of `Program.cs`, beside the existing ones:

```csharp
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization.EntityFrameworkCore;
```

- [ ] **Step 3: Bootstrap the schema and run the import**

Immediately after `var app = builder.Build();` (line 210) — **before** any `app.Map*` call, because resolving `PropositionSet` calls `Load()` and a missing table would throw:

```csharp
// The schema, created in place for the zero-config path: `docker compose up` needs no migration
// step and no database server. A production host applies adopter-owned migrations instead —
// EnsureCreated deliberately does not write a migrations-history table, which is the same split
// ASP.NET Core Identity draws.
await using (var bootstrap = app.Services.GetRequiredService<IDbContextFactory<MotivStoreDbContext>>()
                 .CreateDbContext())
{
    await bootstrap.Database.EnsureCreatedAsync();
}

// One-way migration off the JSON stores. Opt-in, and a no-op once the database holds anything, so
// leaving the flag on is harmless.
if (builder.Configuration.GetValue("Motiv:Store:ImportFromJson", false))
{
    var imported = await StoreImport.CopyAsync(
        new JsonFileRuleStore(rulesPath),
        app.Services.GetRequiredService<IRuleStore>(),
        new JsonFilePropositionStore(propositionsPath),
        app.Services.GetRequiredService<IPropositionStore>(),
        CancellationToken.None);

    app.Logger.LogInformation(
        "Store import: imported={Imported} ruleVersions={RuleVersions} propositions={Propositions}",
        imported.Imported, imported.RuleVersions, imported.Propositions);
}
```

If `Program.cs` is not already an async top-level program, the `await`s here make it one — that is fine and requires no other change.

- [ ] **Step 4: Translate the tests' isolation**

In `src/examples/Motiv.RulesEngine.Sample.Tests/CheckoutEndpointTests.cs`, replace the `IsolatedRules` helper:

```csharp
    // Points the store at a fresh temp database rather than the sample's real motiv-store.db, which
    // every WebApplicationFactory<Program> in this assembly (and `dotnet run` itself) shares on disk
    // — this class reads a rule's version and expects it to still be 1, an assumption a shared
    // database cannot make once anything else in the suite (or a prior run) has published to it.
    private static WebApplicationFactory<Program> IsolatedRules(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.UseSetting(
            "Motiv:Store:ConnectionString",
            $"Data Source={Path.Combine(Path.GetTempPath(), $"motiv-{Guid.NewGuid():N}.db")}"));
```

In `src/examples/Motiv.RulesEngine.Sample.Tests/GrantSourceTests.cs`, replace the equivalent `UseSetting("Rules:Path", ...)` call with the same `Motiv:Store:ConnectionString` setting, keeping the surrounding comment's meaning but updating "rules.json" to "the store".

- [ ] **Step 5: Search for any other store-path override**

```bash
grep -rn "Rules:Path\|Propositions:Path" --include="*.cs" --include="*.json" src/ ui/ | grep -v node_modules
```

Any remaining hit in a test must be translated the same way. Hits in `Program.cs` are expected — the importer still reads those paths.

- [ ] **Step 6: Run the sample tests**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/examples/Motiv.RulesEngine.Sample.Tests/Motiv.RulesEngine.Sample.Tests.csproj
```

Expected: all PASS. A failure mentioning "no such table" means the bootstrap block is running after something already resolved a store — move it earlier.

- [ ] **Step 7: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample src/examples/Motiv.RulesEngine.Sample.Tests
git commit -m "feat(sample): back the sample's rules and propositions with the EF Core store"
```

---

### Task 10: The bundle's verification obligations

Spec 2 §7 lists behaviours the bundle must demonstrate. The conformance suite covers most; these are the ones that need a real database or a second replica.

**Files:**
- Create: `src/Motiv.Serialization.EntityFrameworkCore.Tests/CrossReplicaTests.cs`

**Interfaces:**
- Consumes: `SqliteStoreFixture` (Task 2); `EfRuleStore` (Task 3).
- Produces: nothing.

- [ ] **Step 1: Write the tests**

Create `src/Motiv.Serialization.EntityFrameworkCore.Tests/CrossReplicaTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.EntityFrameworkCore.Tests;

/// <summary>
/// The bundle's cross-process obligations: two stores over one database are two replicas.
/// </summary>
/// <remarks>
/// Deliberately sequential rather than thread-racing. The lost update ticket 21 describes is a
/// <em>stale</em> replica computing the same next version, not a nanosecond-level tie — and a
/// thread-racing test against SQLite would trade a real assertion for a flaky one, on a CI that
/// also runs Windows.
/// </remarks>
public class CrossReplicaTests
{
    private static readonly DateTimeOffset Epoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", Epoch, null, null, "test");

    [Fact]
    public async Task Should_let_exactly_one_of_two_replicas_take_a_version()
    {
        // Arrange — both replicas hold v1 and both compute next = 2
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var replicaA = new EfRuleStore(fixture.Factory);
        var replicaB = new EfRuleStore(fixture.Factory);
        await replicaA.AppendAsync([Row("a", 1)], default);

        // Act
        var first = await replicaA.AppendAsync([Row("a", 2, """{"winner":true}""")], default);
        var second = await replicaB.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert — one published, one rejected, and the log says so
        first.IsConflict.ShouldBeFalse();
        second.IsConflict.ShouldBeTrue();
        second.CurrentVersion.ShouldBe(2);

        var history = await replicaB.HistoryAsync("a", default);
        history.Select(row => row.Version).ShouldBe([1, 2]);
        history[1].DocumentJson.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_report_the_current_version_to_a_stale_writer()
    {
        // Arrange — a replica that has not refreshed since v3 was published
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfRuleStore(fixture.Factory);
        await store.AppendAsync([Row("a", 1)], default);
        await store.AppendAsync([Row("a", 2)], default);
        await store.AppendAsync([Row("a", 3)], default);

        // Act — it still believes the head is v1, so it offers v2
        var result = await store.AppendAsync([Row("a", 2)], default);

        // Assert — the rejection carries where the store actually is, so the caller can rebase
        result.IsConflict.ShouldBeTrue();
        result.CurrentVersion.ShouldBe(3);
    }

    [Fact]
    public async Task Should_leave_nothing_live_when_a_persist_fails()
    {
        // Arrange — Author is NOT NULL, so this batch fails at the database rather than at the
        // conflict check: the path that must rethrow rather than report a version conflict
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var store = new EfRuleStore(fixture.Factory);
        await store.AppendAsync([Row("a", 1)], default);
        var generationBefore = await store.GetGenerationAsync(default);

        var illegal = new StoredRuleVersion("b", 1, "{}", null!, Epoch, null, null, null);

        // Act
        var act = async () => await store.AppendAsync([illegal], default);

        // Assert — it throws rather than returning a conflict, and nothing landed
        await act.ShouldThrowAsync<DbUpdateException>();
        (await store.GetGenerationAsync(default)).ShouldBe(generationBefore);
        store.Load().ShouldHaveSingleItem().Name.ShouldBe("a");
    }

    [Fact]
    public async Task Should_move_one_generation_without_moving_the_other()
    {
        // Arrange — the two stores are never written in the same transaction, so their generations
        // are independent; a shared counter would make every proposition write rebuild every rule
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var rules = new EfRuleStore(fixture.Factory);
        var propositions = new EfPropositionStore(fixture.Factory);
        var propositionGenerationBefore = await propositions.GetGenerationAsync(default);

        // Act
        await rules.AppendAsync([Row("a", 1)], default);

        // Assert
        (await rules.GetGenerationAsync(default)).ShouldBeGreaterThan(0);
        (await propositions.GetGenerationAsync(default)).ShouldBe(propositionGenerationBefore);
    }
}
```

- [ ] **Step 2: Run them**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests/Motiv.Serialization.EntityFrameworkCore.Tests.csproj --filter "FullyQualifiedName~CrossReplicaTests"
```

Expected: 4 PASS. If `Should_leave_nothing_live_when_a_persist_fails` reports a conflict instead of throwing, the catch block is treating every `DbUpdateException` as a conflict — it must re-read and rethrow when no row of its own is present.

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore.Tests/CrossReplicaTests.cs
git commit -m "test(ef-store): cover the bundle's cross-replica and failed-persist obligations"
```

---

### Task 11: Documentation, full verification, and review

**Files:**
- Create: `docs/live-rules/entity-framework-store.md`
- Modify: `docs/toc.yml`, `docs/Overview.md`, `README.md`

**Interfaces:**
- Consumes: everything.
- Produces: nothing.

- [ ] **Step 1: Read the existing docs structure**

```bash
sed -n '1,60p' docs/toc.yml && ls docs/live-rules docs/multi-instance
```

Follow whatever shape those directories already use — CLAUDE.md requires `docs/{feature}/index.md`, individual pages, a local `toc.yml`, plus entries in `docs/toc.yml` and `docs/Overview.md`.

- [ ] **Step 2: Write the store documentation**

Create `docs/live-rules/entity-framework-store.md` covering, in the house voice of the neighbouring pages:

- Installing the package and calling `AddMotivEntityFrameworkStore` with each of the three providers.
- The three tables and what each column means, including that `DocumentJson` null means "on the compiled default at this version".
- **Migrations:** dev uses `EnsureCreated`; production derives `MotivStoreDbContext` and owns its migrations, the ASP.NET Identity pattern. `EnsureCreated` and migrations do not mix, deliberately.
- **Backup and restore:** the authoring database is one backup unit. A restore must never move a generation *backward* while replicas are live — it is the fencing token behind monotonic reads, and a replica that has seen generation 9 will ignore a store that claims 4.
- **The single-writer rule:** the generation counter is bumped by the application. Any out-of-band writer that inserts rows without bumping it leaves replicas silently skewed, so grant the application the only write credentials and keep DBA access read-only outside migrations.
- **Importing from the JSON stores:** `Motiv:Store:ImportFromJson`, one-way, refused once the target holds anything.

- [ ] **Step 3: Add the navigation entries**

Add the page to `docs/live-rules/toc.yml` (or the local toc the directory uses), to `docs/toc.yml`, and a line to `docs/Overview.md`. Add a short example under Core Features in `README.md`.

- [ ] **Step 4: Build the whole solution**

```bash
dotnet build Motiv.slnx
```

Expected: success across every TFM including `net472`, which never runs locally but is built in CI. This is the step that catches a `net472` slip in the source-linked conformance files.

- [ ] **Step 5: Run the full solution test suite**

```bash
DOTNET_ROOT=$HOME/.dotnet dotnet test Motiv.slnx
```

Expected: all PASS. CLAUDE.md is explicit that the example projects assert on justification strings and break on changes elsewhere — do not skip this in favour of the projects you touched.

- [ ] **Step 6: Run the UI end-to-end suite**

```bash
cd ui && pnpm e2e
```

Never invoke `playwright test` directly — the sample serves a prebuilt `wwwroot` that goes stale silently, and `pnpm e2e` is what rebuilds it. If the run reuses port 5100 from another checkout, check the served asset hash before believing a failure.

- [ ] **Step 7: Mandatory code-simplifier review**

CLAUDE.md requires this and it is not optional. Spawn a `code-simplifier` agent over the changed code, focusing on duplication between `EfRuleStore` and `EfPropositionStore` (the generation read appears in both), long methods in `AppendAsync`, and the translation layer in `Rows.cs`. Apply what it finds and re-run the affected tests.

- [ ] **Step 8: Commit**

```bash
git add docs README.md
git commit -m "docs(ef-store): document the EF Core authoring store, migrations and backup"
```

---

## Self-review notes

**Spec coverage.** Every section of the design doc maps to a task: package and schema → 2; asymmetric-turned-uniform generation → 2 (schema) and 10 (independence test); conflict detection → 3, verified in 10; sync `Load` head projection → 3, covered by conformance; proposition upsert → 4; conformance suite → 1, 3, 4, 5; provider verification → 6; importer → 7; factory overloads → 8; sample wiring and compose parity → 9; the five bundle obligations → conformance (all-or-nothing, quarantine unchanged) plus 10 (racing writers, stale base, failed persist) plus 7 (importer round-trip); docs → 11.

**Two design-doc corrections are folded in.** The `src/testing` linking precedent does not exist and is established here instead. Quarantine needs no new test — it is SDK-side and untouched by this slice, so the obligation is discharged by the existing `RuleStoreWiringTests` coverage rather than by a new EF test; Task 8 step 7 re-runs it.

**Known deferrals**, each stated in the design doc's out-of-scope section: no proposition version log, no public testing package, no Testcontainers, no shipped migrations, no `Motiv.Studio` rename.
