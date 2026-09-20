# Proposition Version Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every `IPropositionStore` an append-only version log with tombstones and provenance, so a decision record's pinned proposition version names a document that is still there.

**Architecture:** The `WriteAsync(PropositionBatch)` contract is kept — the 2026-09-09 CAS design said a log could implement it "by a PK insert with no caller change", and that is what happens. Each save becomes one `StoredPropositionVersion` row keyed `(Name, Version)`; each deletion becomes a tombstone row at `Version + 1` with a null document. `Load()` still returns live heads only. A new `HistoryAsync(name)` reads the log. Provenance travels on the batch and is stamped on every row. `PropositionSet` learns one new fact: a re-created name continues its version numbering past the tombstone, which it reads from the history at create time.

**Tech Stack:** C# / .NET 10 (`Motiv.Serialization` also targets net8, net9, netstandard2.0), EF Core (SQLite in tests), xUnit + Shouldly, the shared `PropositionStoreConformance` suite compiled into three test projects.

**Spec:** `docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md`, section 2 ("The proposition version log (#224, prerequisite)"). Slice 1 of 5.

## Global Constraints

- `Motiv.Serialization` targets `net8.0;net9.0;netstandard2.0;net10.0`. No C# features needing runtime support beyond netstandard2.0 in that project (no `Index`/`Range` types, no default interface methods). Collection expressions and records are fine (they already appear).
- Every `dotnet` invocation on this machine needs `env -u MallocStackLogging -u MallocNanoZone` in front and the Bash sandbox disabled. Grep test output for `error CS` as well as `Failed` — a compile failure can read as a clean run.
- Rows are kept forever. No retention or pruning in this slice.
- A deletion at version `v` writes a tombstone at `v + 1`. This is a contract every store honours and the conformance suite pins.
- `Load()` / `LoadAsync()` never return tombstones. A tombstoned name is absent from the heads and present in the history.
- The conflict predicate is `PropositionBatch.FindConflict`, called by every store. Stores do not re-implement it.
- The old `MotivProposition` table is replaced by `MotivPropositionVersion`. An existing database keeps its old table untouched and needs the new one created; `StoreSchema` reports it as missing rather than migrating.
- Plan and design doc land in the same commit as the implementation (bundle-spec convention). The design doc for this slice is `docs/superpowers/specs/2026-09-20-proposition-version-log-design.md` (Task 8).
- Run the full solution suite before calling the slice done, and then the mandatory `code-simplifier` pass.

## Review Focus

1. A save at a version below or equal to a tombstone must be refused, or a re-created name would reuse a version number the decision log may already have pinned. Pinned by `Should_refuse_recreating_a_deleted_name_at_or_below_its_tombstone` (Task 1).
2. Deleting a name that is already tombstoned must be a conflict, not a second tombstone. Pinned by `Should_refuse_deleting_a_name_already_deleted` (Task 1).
3. A refused batch must leave the history exactly as it was, or a conflict could leak a partial row into the log. Pinned by `Should_leave_history_untouched_when_a_batch_is_refused` (Task 1).
4. Two replicas re-creating the same withdrawn name compute the same next version; the store's primary key must let exactly one win. Pinned by `Should_refuse_the_second_replica_recreating_a_withdrawn_name` (Task 2, `CrossReplicaTests`).
5. A pre-log `propositions.json` (rows without provenance fields) must still load in Studio's JSON store. Pinned by `Should_read_a_file_written_before_provenance_existed` (Task 3).

---

### Task 1: The version record, the batch's provenance, and the in-memory log

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/StoredProposition.cs`
- Modify: `src/Motiv.Serialization/Propositions/IPropositionStore.cs`
- Modify: `src/testing/StoreConformance/PropositionStoreConformance.cs`
- Test project: `src/Motiv.Serialization.Tests` (runs `InMemoryPropositionStoreTests : PropositionStoreConformance`)

**Interfaces:**
- Consumes: `RuleChangeProvenance` (`src/Motiv.Serialization/Rules/RuleChangeProvenance.cs`), whose `WithDefaults()` fills `BuildId`.
- Produces:
  - `public sealed record StoredPropositionVersion(string Name, int Version, string? ModelType, string? DocumentJson, string? Description, string Author, DateTimeOffset TimestampUtc, string? ChangeNote, string? ApprovalRef, string? BuildId)` with `bool IsTombstone`, and static factories `Saved(StoredProposition, RuleChangeProvenance, DateTimeOffset)` and `Tombstone(PropositionDeletion, string? modelType, RuleChangeProvenance, DateTimeOffset)`, plus `static PropositionPosition? PositionOf(IEnumerable<StoredPropositionVersion>)` and `static StoredProposition? HeadOf(IEnumerable<StoredPropositionVersion>)`.
  - `public sealed record PropositionPosition(int Version, bool Live)`.
  - `PropositionBatch` gains a third positional parameter `RuleChangeProvenance Provenance`, a two-argument constructor overload defaulting it to `RuleChangeProvenance.System`, and `Save(proposition, provenance = null)` / `Delete(name, version, provenance = null)`.
  - `PropositionBatch.FindConflict(Func<string, PropositionPosition?> position)` replaces `FindConflict(Func<string, int>)`.
  - `IPropositionStore.HistoryAsync(string name, CancellationToken cancellationToken)` returning `Task<IReadOnlyList<StoredPropositionVersion>>`, version order.

- [ ] **Step 1: Add the conformance tests**

Append these to `src/testing/StoreConformance/PropositionStoreConformance.cs`, inside the class. They compile against types that do not exist yet.

```csharp
    /// <summary>A named author, so a row's provenance is distinguishable from the default.</summary>
    protected static RuleChangeProvenance By(string author, string? note = null, string? approvalRef = null) =>
        new(author, note, approvalRef, BuildId: "build-1");

    [Fact]
    public async Task Should_return_the_whole_history_of_a_name_in_version_order()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);

        // Act
        var history = await Store.HistoryAsync("a", default);

        // Assert — kept forever, in order, so "what did v1 say?" is always answerable
        history.Select(row => row.Version).ShouldBe([1, 2]);
        history.ShouldAllBe(row => row.DocumentJson != null && row.ModelType == "customer");
    }

    [Fact]
    public async Task Should_return_an_empty_history_for_a_name_it_has_never_held()
    {
        // Act & Assert
        (await Store.HistoryAsync("never", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_record_a_deletion_as_a_tombstone_one_past_the_deleted_version()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act
        var written = await Store.WriteAsync(PropositionBatch.Delete("a", 1), default);

        // Assert — gone from the heads, kept in the log as a row that says "withdrawn here"
        written.IsConflict.ShouldBeFalse();
        Store.Load().ShouldBeEmpty();
        var history = await Store.HistoryAsync("a", default);
        history.Select(row => row.Version).ShouldBe([1, 2]);
        history[1].IsTombstone.ShouldBeTrue();
        history[1].DocumentJson.ShouldBeNull();
        history[1].ModelType.ShouldBe("customer");
    }

    [Fact]
    public async Task Should_refuse_recreating_a_deleted_name_at_or_below_its_tombstone()
    {
        // Arrange — the decision log may already pin "a" v1 and v2; neither number may be reused
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        await Store.WriteAsync(PropositionBatch.Delete("a", 1), default);

        // Act
        var atOne = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        var atTwo = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 2)), default);
        var atThree = await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 3)), default);

        // Assert
        atOne.IsConflict.ShouldBeTrue();
        atOne.CurrentVersion.ShouldBe(2);
        atTwo.IsConflict.ShouldBeTrue();
        atTwo.CurrentVersion.ShouldBe(2);
        atThree.IsConflict.ShouldBeFalse();
        Store.Load().ShouldHaveSingleItem().Version.ShouldBe(3);
        (await Store.HistoryAsync("a", default)).Select(row => row.Version).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Should_refuse_deleting_a_name_already_deleted()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);
        await Store.WriteAsync(PropositionBatch.Delete("a", 1), default);

        // Act — a deletion names a live position; the tombstone is not one
        var again = await Store.WriteAsync(PropositionBatch.Delete("a", 2), default);

        // Assert
        again.IsConflict.ShouldBeTrue();
        again.CurrentVersion.ShouldBe(2);
        (await Store.HistoryAsync("a", default)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_stamp_the_batch_provenance_on_every_row_it_writes()
    {
        // Arrange
        var save = new PropositionBatch([Stored("a", version: 1)], [], By("alice", "why", "cr-1"));

        // Act
        await Store.WriteAsync(save, default);
        await Store.WriteAsync(PropositionBatch.Delete("a", 1, By("bob")), default);

        // Assert
        var history = await Store.HistoryAsync("a", default);
        history[0].Author.ShouldBe("alice");
        history[0].ChangeNote.ShouldBe("why");
        history[0].ApprovalRef.ShouldBe("cr-1");
        history[0].BuildId.ShouldBe("build-1");
        history[1].Author.ShouldBe("bob");
    }

    [Fact]
    public async Task Should_default_provenance_to_system_with_the_running_build()
    {
        // Act — the two-argument shapes callers already use
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Assert
        var row = (await Store.HistoryAsync("a", default)).ShouldHaveSingleItem();
        row.Author.ShouldBe("system");
        row.BuildId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_stamp_a_timestamp_on_every_row()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Act
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Assert
        var row = (await Store.HistoryAsync("a", default)).ShouldHaveSingleItem();
        row.TimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
        row.TimestampUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Should_leave_history_untouched_when_a_batch_is_refused()
    {
        // Arrange
        await Store.WriteAsync(PropositionBatch.Save(Stored("a", version: 1)), default);

        // Act — "b" is fine, "a" conflicts; the batch is all-or-nothing
        var refused = await Store.WriteAsync(
            new PropositionBatch([Stored("b", version: 1), Stored("a", version: 1)], []), default);

        // Assert
        refused.IsConflict.ShouldBeTrue();
        (await Store.HistoryAsync("a", default)).Count.ShouldBe(1);
        (await Store.HistoryAsync("b", default)).ShouldBeEmpty();
    }
```

- [ ] **Step 2: Run the in-memory conformance suite to see it fail to compile**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryPropositionStoreTests" 2>&1 | grep -E "error CS|Passed!|Failed!" | head
```

Expected: `error CS` lines naming `HistoryAsync`, `IsTombstone`, `RuleChangeProvenance` in the batch.

- [ ] **Step 3: Add `StoredPropositionVersion` and `PropositionPosition`**

Replace the contents of `src/Motiv.Serialization/Propositions/StoredProposition.cs` with:

```csharp
namespace Motiv.Serialization;

/// <summary>The head of an authored proposition as a store reports it: the live row, never a tombstone.</summary>
public sealed record StoredProposition(
    string Name, string ModelType, string DocumentJson, int Version, string? Description);

/// <summary>Where a store stands for one name: its highest version, and whether that row is live.</summary>
/// <param name="Version">The highest version the log holds for the name, tombstones included.</param>
/// <param name="Live">False when the highest row is a tombstone.</param>
public sealed record PropositionPosition(int Version, bool Live);

/// <summary>
/// One row of a proposition's version log. A save writes a row with a document; a deletion writes a
/// <em>tombstone</em> — a row one past the deleted version with no document — so the log records that
/// the name was withdrawn, by whom, and when, and a later re-creation continues the numbering rather
/// than reusing a version the decision log may already pin.
/// </summary>
/// <param name="ModelType">The model type of the saved document, carried onto the tombstone from the row it retires.</param>
/// <param name="DocumentJson">The document, or null for a tombstone.</param>
public sealed record StoredPropositionVersion(
    string Name,
    int Version,
    string? ModelType,
    string? DocumentJson,
    string? Description,
    string Author,
    DateTimeOffset TimestampUtc,
    string? ChangeNote,
    string? ApprovalRef,
    string? BuildId)
{
    /// <summary>True when this row records a withdrawal rather than a document.</summary>
    public bool IsTombstone => DocumentJson is null;

    /// <summary>The row a save writes.</summary>
    public static StoredPropositionVersion Saved(
        StoredProposition proposition, RuleChangeProvenance provenance, DateTimeOffset timestampUtc)
    {
        var stamped = provenance.WithDefaults();
        return new StoredPropositionVersion(
            proposition.Name, proposition.Version, proposition.ModelType, proposition.DocumentJson,
            proposition.Description, stamped.Author, timestampUtc,
            stamped.ChangeNote, stamped.ApprovalRef, stamped.BuildId);
    }

    /// <summary>The row a deletion writes: one past the deleted version, with no document.</summary>
    public static StoredPropositionVersion Tombstone(
        PropositionDeletion deletion, string? modelType, RuleChangeProvenance provenance,
        DateTimeOffset timestampUtc)
    {
        var stamped = provenance.WithDefaults();
        return new StoredPropositionVersion(
            deletion.Name, deletion.Version + 1, modelType, DocumentJson: null, Description: null,
            stamped.Author, timestampUtc, stamped.ChangeNote, stamped.ApprovalRef, stamped.BuildId);
    }

    /// <summary>The position of a name given its rows, or null when there are none.</summary>
    public static PropositionPosition? PositionOf(IEnumerable<StoredPropositionVersion> rows)
    {
        StoredPropositionVersion? highest = null;
        foreach (var row in rows)
        {
            if (highest is null || row.Version > highest.Version)
                highest = row;
        }

        return highest is null ? null : new PropositionPosition(highest.Version, !highest.IsTombstone);
    }

    /// <summary>The live head given a name's rows, or null when there are none or the head is a tombstone.</summary>
    public static StoredProposition? HeadOf(IEnumerable<StoredPropositionVersion> rows)
    {
        StoredPropositionVersion? highest = null;
        foreach (var row in rows)
        {
            if (highest is null || row.Version > highest.Version)
                highest = row;
        }

        return highest is null || highest.IsTombstone
            ? null
            : new StoredProposition(highest.Name, highest.ModelType!, highest.DocumentJson!, highest.Version, highest.Description);
    }
}
```

- [ ] **Step 4: Give the batch its provenance and the new conflict predicate; add `HistoryAsync`**

In `src/Motiv.Serialization/Propositions/IPropositionStore.cs`, replace the `PropositionBatch` record with:

```csharp
/// <summary>
/// One store round trip: everything a single publish changes, applied all at once or not at all, and
/// the provenance every row it writes is stamped with.
/// </summary>
public sealed record PropositionBatch(
    IReadOnlyList<StoredProposition> Saves,
    IReadOnlyList<PropositionDeletion> Deletes,
    RuleChangeProvenance Provenance)
{
    /// <summary>A batch attributed to the system — what an import or a test writes.</summary>
    public PropositionBatch(IReadOnlyList<StoredProposition> saves, IReadOnlyList<PropositionDeletion> deletes)
        : this(saves, deletes, RuleChangeProvenance.System)
    {
    }

    public static PropositionBatch Save(StoredProposition proposition, RuleChangeProvenance? provenance = null) =>
        new([proposition], [], provenance ?? RuleChangeProvenance.System);

    public static PropositionBatch Delete(string name, int version, RuleChangeProvenance? provenance = null) =>
        new([], [new PropositionDeletion(name, version)], provenance ?? RuleChangeProvenance.System);

    /// <summary>
    /// The one conflict predicate every store applies, against the position it holds for each name.
    /// A save must land strictly past the highest version the log holds — tombstones included, so a
    /// re-created name never reuses a number. A deletion must name the live head exactly: an absent
    /// name, a tombstoned one, or a stale version is a writer acting on a row someone else already
    /// moved.
    /// </summary>
    /// <param name="position">The store's position for a name, or null when it has never held it.</param>
    public PropositionWriteResult? FindConflict(Func<string, PropositionPosition?> position)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var save in Saves)
        {
            var current = position(save.Name);
            if (!claimed.Add(save.Name) || (current is not null && save.Version <= current.Version))
                return PropositionWriteResult.Conflict(save.Name, current?.Version ?? 0);
        }

        foreach (var deletion in Deletes)
        {
            var current = position(deletion.Name);
            if (!claimed.Add(deletion.Name)
                || deletion.Version < 1
                || current is not { Live: true }
                || deletion.Version != current.Version)
                return PropositionWriteResult.Conflict(deletion.Name, current?.Version ?? 0);
        }

        return null;
    }
}
```

Add to the `IPropositionStore` interface, after `WriteAsync`:

```csharp
    /// <summary>
    /// Every version the log holds for <paramref name="name"/>, in version order, tombstones included.
    /// Empty for a name the store has never held. Kept forever: this is what a decision record's pinned
    /// proposition version resolves against.
    /// </summary>
    Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(string name, CancellationToken cancellationToken);
```

- [ ] **Step 5: Make the in-memory store a log**

Replace the `InMemoryPropositionStore` class in the same file with:

```csharp
public sealed class InMemoryPropositionStore : IPropositionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<StoredPropositionVersion>> _log = new(StringComparer.Ordinal);
    private long _generation;

    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
        {
            var heads = new List<StoredProposition>();
            foreach (var rows in _log.Values)
            {
                if (StoredPropositionVersion.HeadOf(rows) is { } head)
                    heads.Add(head);
            }

            return heads;
        }
    }

    public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(_generation);
    }

    public Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (batch.FindConflict(Position) is { } conflict)
                return Task.FromResult(conflict);

            var now = DateTimeOffset.UtcNow;

            foreach (var proposition in batch.Saves)
                Rows(proposition.Name).Add(StoredPropositionVersion.Saved(proposition, batch.Provenance, now));

            foreach (var deletion in batch.Deletes)
            {
                var rows = Rows(deletion.Name);
                var modelType = StoredPropositionVersion.HeadOf(rows)?.ModelType;
                rows.Add(StoredPropositionVersion.Tombstone(deletion, modelType, batch.Provenance, now));
            }

            // An empty batch is not a write. A generation that moved anyway would make every
            // replica rebuild its whole world for nothing, on a timer.
            if (batch.Saves.Count > 0 || batch.Deletes.Count > 0)
                _generation++;

            return Task.FromResult(PropositionWriteResult.Written);
        }
    }

    public Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredPropositionVersion>>(
                _log.TryGetValue(name, out var rows)
                    ? [.. rows.OrderBy(row => row.Version)]
                    : []);
        }
    }

    private PropositionPosition? Position(string name) =>
        _log.TryGetValue(name, out var rows) ? StoredPropositionVersion.PositionOf(rows) : null;

    private List<StoredPropositionVersion> Rows(string name)
    {
        if (!_log.TryGetValue(name, out var rows))
            _log[name] = rows = [];
        return rows;
    }
}
```

- [ ] **Step 6: Fix the other in-project callers of the old shapes**

`Motiv.Serialization` has no other `FindConflict` callers, but `PropositionSetLoadTests.RawStore` and any other test double implementing `IPropositionStore` in `src/Motiv.Serialization.Tests` now lack `HistoryAsync`. Find them:

```bash
grep -rln --exclude-dir=obj --exclude-dir=bin ": IPropositionStore" src/Motiv.Serialization.Tests
```

Add to each:

```csharp
        public Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(string name, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoredPropositionVersion>>([]);
```

- [ ] **Step 7: Run the in-memory conformance suite and the rest of `Motiv.Serialization.Tests`**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -20
```

Expected: `Passed!` for the project, no `error CS`. (The EF and Studio test projects do not compile yet; that is Tasks 2 and 3.)

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization/Propositions/StoredProposition.cs src/Motiv.Serialization/Propositions/IPropositionStore.cs src/testing/StoreConformance/PropositionStoreConformance.cs src/Motiv.Serialization.Tests
git commit -m "Proposition store — a version log with tombstones and provenance, in memory first

Keeps the WriteAsync contract and adds HistoryAsync. A deletion writes a
tombstone one past the deleted version; a re-created name continues past it.

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: The EF store as a log

**Files:**
- Modify: `src/Motiv.Serialization.EntityFrameworkCore/Rows.cs`
- Modify: `src/Motiv.Serialization.EntityFrameworkCore/MotivStoreDbContext.cs`
- Modify: `src/Motiv.Serialization.EntityFrameworkCore/EfPropositionStore.cs` (full rewrite)
- Modify: `src/Motiv.Serialization.EntityFrameworkCore/StoreImport.cs` (compile fix only, see Step 6)
- Modify: `src/Motiv.Serialization.EntityFrameworkCore.Tests/SchemaTests.cs`
- Modify: `src/Motiv.Serialization.EntityFrameworkCore.Tests/ProviderSchemaTests.cs`
- Modify: `src/Motiv.Serialization.EntityFrameworkCore.Tests/CrossReplicaTests.cs`
- Test project: `src/Motiv.Serialization.EntityFrameworkCore.Tests`

**Interfaces:**
- Consumes: `StoredPropositionVersion`, `PropositionPosition`, `PropositionBatch.FindConflict(Func<string, PropositionPosition?>)`, `GenerationTracking.BumpAsync/ReadAsync` (`GenerationTracking.cs`, scope `PropositionsScope`).
- Produces: `PropositionVersionRow` mapped to table `MotivPropositionVersion` keyed `(Name, Version)`; `DbSet<PropositionVersionRow> MotivStoreDbContext.PropositionVersions`; `internal static IQueryable<StoredProposition> EfPropositionStore.HeadQuery(MotivStoreDbContext)`. `DbSet Propositions` and `PropositionRow` are removed.

- [ ] **Step 1: Write the schema tests**

In `src/Motiv.Serialization.EntityFrameworkCore.Tests/SchemaTests.cs`, change the first test's assertions to name the tables exactly and add a key test:

```csharp
        script.ShouldContain("MotivRuleVersion");
        script.ShouldContain("MotivPropositionVersion");
        script.ShouldContain("MotivStoreGeneration");
        script.ShouldNotContain("\"MotivProposition\"");
```

```csharp
    [Fact]
    public async Task Should_key_the_proposition_log_on_name_and_version()
    {
        // Arrange — the same cross-process compare-and-set the rule log has
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        await using var context = fixture.Factory.CreateDbContext();

        // Act
        var key = context.Model.FindEntityType(typeof(PropositionVersionRow))!.FindPrimaryKey()!;

        // Assert
        key.Properties.Select(property => property.Name).ShouldBe(["Name", "Version"]);
    }
```

In `ProviderSchemaTests.cs`, change `script.ShouldContain("MotivProposition", ...)` to `"MotivPropositionVersion"` and add:

```csharp
    [Theory]
    [MemberData(nameof(Providers))]
    public void Should_translate_the_proposition_head_projection_for_every_provider(
        string provider, Action<DbContextOptionsBuilder> configure)
    {
        // Arrange — heads are computed by the database, never by materialising the log
        var builder = new DbContextOptionsBuilder<MotivStoreDbContext>();
        configure(builder);
        using var context = new MotivStoreDbContext(builder.Options);

        // Act
        var sql = EfPropositionStore.HeadQuery(context).ToQueryString();

        // Assert — superseded rows and tombstones are excluded in SQL
        sql.ShouldContain("NOT EXISTS", customMessage: provider);
        sql.ShouldContain("MotivPropositionVersion", customMessage: provider);
        sql.ShouldContain("IS NOT NULL", customMessage: provider);
    }
```

In `CrossReplicaTests.cs`, add (follow the file's existing pattern of two `EfPropositionStore` instances over one `SqliteStoreFixture.Factory`):

```csharp
    [Fact]
    public async Task Should_refuse_the_second_replica_recreating_a_withdrawn_name()
    {
        // Arrange — both replicas read the same tombstone and compute next = 3
        await using var fixture = await SqliteStoreFixture.CreateAsync();
        var first = new EfPropositionStore(fixture.Factory);
        var second = new EfPropositionStore(fixture.Factory);
        var row = new StoredProposition("a", "customer", "{}", 1, null);
        await first.WriteAsync(PropositionBatch.Save(row), default);
        await first.WriteAsync(PropositionBatch.Delete("a", 1), default);

        // Act
        var winner = await first.WriteAsync(PropositionBatch.Save(row with { Version = 3 }), default);
        var loser = await second.WriteAsync(PropositionBatch.Save(row with { Version = 3 }), default);

        // Assert — the primary key lets exactly one win
        winner.IsConflict.ShouldBeFalse();
        loser.IsConflict.ShouldBeTrue();
        loser.CurrentVersion.ShouldBe(3);
        (await first.HistoryAsync("a", default)).Select(v => v.Version).ShouldBe([1, 2, 3]);
    }
```

- [ ] **Step 2: Run the EF tests to see them fail to compile**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests 2>&1 | grep -E "error CS" | head
```

Expected: `error CS` on `PropositionVersionRow`, `HistoryAsync`, `FindConflict`.

- [ ] **Step 3: Replace the row and the mapping**

In `Rows.cs`, replace the `PropositionRow` class with:

```csharp
public class PropositionVersionRow
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? ModelType { get; set; }
    public string? DocumentJson { get; set; }
    public string? Description { get; set; }
    public string Author { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string? ChangeNote { get; set; }
    public string? ApprovalRef { get; set; }
    public string? BuildId { get; set; }
}
```

Replace the two `PropositionRow` mapping methods in `RowMapping` with:

```csharp
    public static StoredPropositionVersion ToRecord(this PropositionVersionRow row) =>
        new(row.Name, row.Version, row.ModelType, row.DocumentJson, row.Description,
            row.Author, row.TimestampUtc, row.ChangeNote, row.ApprovalRef, row.BuildId);

    public static PropositionVersionRow ToRow(this StoredPropositionVersion version) =>
        new()
        {
            Name = version.Name,
            Version = version.Version,
            ModelType = version.ModelType,
            DocumentJson = version.DocumentJson,
            Description = version.Description,
            Author = version.Author,
            TimestampUtc = version.TimestampUtc,
            ChangeNote = version.ChangeNote,
            ApprovalRef = version.ApprovalRef,
            BuildId = version.BuildId,
        };
```

In `MotivStoreDbContext.cs`, replace `public DbSet<PropositionRow> Propositions => Set<PropositionRow>();` with `public DbSet<PropositionVersionRow> PropositionVersions => Set<PropositionVersionRow>();`, and replace the `PropositionRow` entity block with:

```csharp
        modelBuilder.Entity<PropositionVersionRow>(entity =>
        {
            entity.ToTable("MotivPropositionVersion");
            // The composite key is the cross-process compare-and-set, exactly as for rules: two
            // replicas racing the same version race on the insert, and the key lets exactly one win.
            entity.HasKey(row => new { row.Name, row.Version });
            entity.Property(row => row.Name).IsRequired();
            entity.Property(row => row.Author).IsRequired();
            entity.Property(row => row.DocumentJson);
        });
```

- [ ] **Step 4: Rewrite `EfPropositionStore`**

Replace the file's contents with:

```csharp
using Microsoft.EntityFrameworkCore;
using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The proposition store over an append-only version log, the twin of <see cref="EfRuleStore"/>.
/// A save inserts a row; a deletion inserts a tombstone; the <c>(Name, Version)</c> primary key is
/// the compare-and-set, so the read-then-catch shape below is sound without a concurrency token.
/// </summary>
public sealed class EfPropositionStore(IDbContextFactory<MotivStoreDbContext> contextFactory)
    : IPropositionStore
{
    public IReadOnlyList<StoredProposition> Load()
    {
        using var context = contextFactory.CreateDbContext();
        return HeadQuery(context).ToList();
    }

    public async Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await HeadQuery(context).ToListAsync(cancellationToken);
    }

    public async Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await GenerationTracking.ReadAsync(
            context, GenerationTracking.PropositionsScope, cancellationToken);
    }

    public async Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        // An empty batch is not a write — see EfRuleStore.AppendAsync for why that matters.
        if (batch.Saves.Count == 0 && batch.Deletes.Count == 0)
            return PropositionWriteResult.Written;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var positions = await PositionsNamedByAsync(context, batch, cancellationToken);
        if (batch.FindConflict(PositionIn(positions)) is { } conflict)
            return conflict;

        var now = DateTimeOffset.UtcNow;

        foreach (var save in batch.Saves)
            context.PropositionVersions.Add(StoredPropositionVersion.Saved(save, batch.Provenance, now).ToRow());

        foreach (var deletion in batch.Deletes)
        {
            positions.TryGetValue(deletion.Name, out var retired);
            context.PropositionVersions.Add(
                StoredPropositionVersion.Tombstone(deletion, retired?.ModelType, batch.Provenance, now).ToRow());
        }

        await GenerationTracking.BumpAsync(
            context, GenerationTracking.PropositionsScope, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PropositionWriteResult.Written;
        }
        catch (DbUpdateException)
        {
            // Another replica committed between the read above and this insert. Roll back and ask
            // the store what happened: a position that has moved past ours means we lost the race;
            // anything else — a full disk, a dropped connection — is not a version conflict and must
            // not be reported as one.
            await transaction.RollbackAsync(cancellationToken);

            await using var fresh = await contextFactory.CreateDbContextAsync(cancellationToken);
            var raced = batch.FindConflict(
                PositionIn(await PositionsNamedByAsync(fresh, batch, cancellationToken)));
            if (raced is not null)
                return raced;

            throw;
        }
    }

    public async Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.PropositionVersions.AsNoTracking()
            .Where(row => row.Name == name)
            .OrderBy(row => row.Version)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => row.ToRecord())];
    }

    /// <summary>
    /// The live heads: the highest-versioned row of each name, when that row carries a document.
    /// Computed by the database — superseded rows and tombstones never leave it.
    /// </summary>
    internal static IQueryable<StoredProposition> HeadQuery(MotivStoreDbContext context) =>
        context.PropositionVersions.AsNoTracking()
            .Where(row => row.DocumentJson != null
                && !context.PropositionVersions
                    .Any(other => other.Name == row.Name && other.Version > row.Version))
            .Select(row => new StoredProposition(
                row.Name, row.ModelType!, row.DocumentJson!, row.Version, row.Description));

    /// <summary>The highest row of every name the batch speaks for — enough to place and to tombstone.</summary>
    private static async Task<Dictionary<string, HighestRow>> PositionsNamedByAsync(
        MotivStoreDbContext context, PropositionBatch batch, CancellationToken cancellationToken)
    {
        var names = batch.Saves.Select(save => save.Name)
            .Concat(batch.Deletes.Select(deletion => deletion.Name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var rows = await context.PropositionVersions.AsNoTracking()
            .Where(row => names.Contains(row.Name))
            .Select(row => new { row.Name, row.Version, row.ModelType, Live = row.DocumentJson != null })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var highest = group.OrderByDescending(row => row.Version).First();
                    return new HighestRow(new PropositionPosition(highest.Version, highest.Live), highest.ModelType);
                },
                StringComparer.Ordinal);
    }

    private static Func<string, PropositionPosition?> PositionIn(Dictionary<string, HighestRow> positions) =>
        name => positions.TryGetValue(name, out var row) ? row.Position : null;

    private sealed record HighestRow(PropositionPosition Position, string? ModelType);
}
```

- [ ] **Step 5: Fix `StoreImport` and any EF test that referenced the old row**

`StoreImport.CopyAsync` constructs `new PropositionBatch(propositions, [])` — this still compiles via the two-argument constructor and writes rows attributed to `system`; leave it. Search the EF test project for `PropositionRow`, `context.Propositions`, and `FindConflict(`:

```bash
grep -rn --exclude-dir=obj --exclude-dir=bin "PropositionRow\b\|\.Propositions\b\|FindConflict(" src/Motiv.Serialization.EntityFrameworkCore.Tests src/Motiv.Serialization.EntityFrameworkCore
```

Update each hit to `PropositionVersionRow` / `PropositionVersions`. `EfPropositionStoreWriteFailureTests` exercises the `DbUpdateException` path with an interceptor; its assertions on "concurrency token" behaviour, if any, become assertions on the PK violation path — read the file and adjust the arrange to insert a competing row at the same `(Name, Version)` rather than editing a head in place.

- [ ] **Step 6: Run the EF test project**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.EntityFrameworkCore.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -20
```

Expected: `Passed!`, including every `PropositionStoreConformance` test from Task 1 and the three new tests.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization.EntityFrameworkCore src/Motiv.Serialization.EntityFrameworkCore.Tests
git commit -m "EF proposition store — MotivPropositionVersion replaces the in-place head table

The (Name, Version) key is the compare-and-set; heads and tombstones are
projected in SQL, as EfRuleStore already does.

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Studio's JSON file store and schema guard

**Files:**
- Modify: `src/Motiv.Studio/JsonFilePropositionStore.cs`
- Modify: `src/Motiv.Studio/StoreSchema.cs`
- Modify: `src/Motiv.Studio.Tests/JsonFilePropositionStoreTests.cs`
- Modify: `src/Motiv.Studio.Tests/StoreSchemaTests.cs`
- Test project: `src/Motiv.Studio.Tests` (runs `JsonFilePropositionStoreConformanceTests`)

**Interfaces:**
- Consumes: `StoredPropositionVersion`, `PropositionBatch.Provenance`, `PropositionBatch.FindConflict(Func<string, PropositionPosition?>)`, `MotivStoreDbContext.PropositionVersions`.
- Produces: the JSON file becomes a list of `StoredPropositionVersion` rows. A file of pre-log `StoredProposition` rows (no `author`, no `timestampUtc`) still reads.

- [ ] **Step 1: Write the failing tests**

In `JsonFilePropositionStoreTests.cs`, add (the class already has a temp `path` field and a `Store()` helper; use them):

```csharp
    [Fact]
    public async Task Should_read_a_file_written_before_provenance_existed()
    {
        // Arrange — the shape Studio's seed file and any pre-log deployment wrote
        File.WriteAllText(path, """
            [
              { "name": "customer.eligible", "modelType": "customer", "documentJson": "{}", "version": 2, "description": null }
            ]
            """);

        // Act
        var store = new JsonFilePropositionStore(path);
        var heads = store.Load();
        var history = await store.HistoryAsync("customer.eligible", default);

        // Assert — the head is live at its version; the one row is attributed to the system
        heads.ShouldHaveSingleItem().Version.ShouldBe(2);
        history.ShouldHaveSingleItem().Author.ShouldBe("system");
    }

    [Fact]
    public async Task Should_keep_the_whole_log_across_instances()
    {
        // Arrange
        var first = new JsonFilePropositionStore(path);
        await first.WriteAsync(PropositionBatch.Save(new StoredProposition("a", "customer", "{}", 1, null)), default);
        await first.WriteAsync(PropositionBatch.Delete("a", 1), default);

        // Act
        var second = new JsonFilePropositionStore(path);

        // Assert
        second.Load().ShouldBeEmpty();
        (await second.HistoryAsync("a", default)).Select(row => row.Version).ShouldBe([1, 2]);
    }
```

In `StoreSchemaTests.cs`, replace every `check.Propositions.CountAsync()` with `check.PropositionVersions.CountAsync()`.

- [ ] **Step 2: Run the Studio tests to see them fail**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Studio.Tests 2>&1 | grep -E "error CS" | head
```

Expected: `error CS` on `HistoryAsync`, `FindConflict`, `Propositions`.

- [ ] **Step 3: Make the JSON store a log**

In `JsonFilePropositionStore.cs`, replace `Load`, `WriteAsync`, `ReadAll` and `Write` (keep `GetGenerationAsync`, `CurrentGeneration`, `EnsureGenerationMovedPast` and the class remarks) with:

```csharp
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
        {
            return [.. ReadAll()
                .GroupBy(row => row.Name, StringComparer.Ordinal)
                .Select(StoredPropositionVersion.HeadOf)
                .Where(head => head is not null)
                .Select(head => head!)];
        }
    }

    public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    public Task<PropositionWriteResult> WriteAsync(
        PropositionBatch batch, CancellationToken cancellationToken)
    {
        if (batch.Saves.Count == 0 && batch.Deletes.Count == 0)
            return Task.FromResult(PropositionWriteResult.Written);

        lock (_gate)
        {
            // Read once and decide against that reading: the conflict check and the rewrite must see
            // the same file, or a batch could be cleared against one state and written over another.
            var log = ReadAll();
            var byName = log.ToLookup(row => row.Name, StringComparer.Ordinal);

            var conflict = batch.FindConflict(
                name => StoredPropositionVersion.PositionOf(byName[name]));
            if (conflict is not null)
                return Task.FromResult(conflict);

            var now = DateTimeOffset.UtcNow;
            foreach (var save in batch.Saves)
                log.Add(StoredPropositionVersion.Saved(save, batch.Provenance, now));
            foreach (var deletion in batch.Deletes)
            {
                var modelType = StoredPropositionVersion.HeadOf(byName[deletion.Name])?.ModelType;
                log.Add(StoredPropositionVersion.Tombstone(deletion, modelType, batch.Provenance, now));
            }

            var previousGeneration = CurrentGeneration();
            WriteAllAtomically(JsonSerializer.Serialize(log, Json));
            EnsureGenerationMovedPast(previousGeneration);
        }

        return Task.FromResult(PropositionWriteResult.Written);
    }

    public Task<IReadOnlyList<StoredPropositionVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredPropositionVersion>>(
                [.. ReadAll().Where(row => row.Name == name).OrderBy(row => row.Version)]);
        }
    }

    /// <summary>
    /// The log, tolerating the pre-log file shape: a row with no <c>author</c> was written when the
    /// file held heads rather than versions, and is read as a system-authored version row.
    /// </summary>
    private List<StoredPropositionVersion> ReadAll()
    {
        if (!File.Exists(path))
            return [];

        try
        {
            var rows = JsonSerializer.Deserialize<List<StoredPropositionVersion>>(File.ReadAllText(path), Json) ?? [];
            return [.. rows
                .Where(row => row?.Name is not null)
                .Select(row => string.IsNullOrEmpty(row.Author)
                    ? row with { Author = RuleChangeProvenance.System.Author }
                    : row)];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                $"[JsonFilePropositionStore] Could not read '{path}': {exception.Message}\n" +
                "  Continuing with no stored propositions. The next save will OVERWRITE this file — " +
                "copy it aside now if you intend to repair it.");
            return [];
        }
    }

    private void WriteAllAtomically(string contents)
    {
        var tempPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch (Exception e) when (e is not OutOfMemoryException) { }
            throw;
        }
    }
```

Keep the existing comment block on `ReadAll` about swallowing filesystem failures; it still applies. `StoredPropositionVersion` is a positional record, so `System.Text.Json` deserialises it through its constructor; a missing `author` arrives as null and is normalised above.

- [ ] **Step 4: Update the schema guard**

In `src/Motiv.Studio/StoreSchema.cs`, change the probe for `"MotivProposition"` to:

```csharp
            ("MotivPropositionVersion", () => context.PropositionVersions.AsNoTracking().AnyAsync(cancellationToken)),
```

Extend the incomplete-schema message so a database from before the log is diagnosable. Replace the final `throw new InvalidOperationException(...)` message with:

```csharp
        throw new InvalidOperationException(
            $"The Motiv store schema is incomplete: {string.Join(", ", missing)} could not be read, " +
            "and creating the schema reported no error. EnsureCreated treats a database that already " +
            "holds any table as already created, so this is what a store pointed at somebody else's " +
            "database looks like — or a Motiv database from before the proposition version log, " +
            "which still holds MotivProposition and lacks MotivPropositionVersion. Point it at its " +
            "own database, create the missing table with migrations, or delete a development store " +
            "and let the JSON import re-seed it.");
```

- [ ] **Step 5: Run the Studio test project**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Studio.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -20
```

Expected: `Passed!`. If `Should_treat_a_malformed_file_as_empty_rather_than_throwing` or its "cannot read" sibling fail, the `ReadAll` catch block was not kept; restore it.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Studio src/Motiv.Studio.Tests
git commit -m "Studio — the JSON proposition store is a log; the schema guard probes the version table

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: `PropositionSet` — provenance on every write, numbering past the tombstone

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs`
- Modify: `src/Motiv.Serialization.Tests/Propositions/PropositionSetCreateTests.cs`
- Test project: `src/Motiv.Serialization.Tests`

**Interfaces:**
- Consumes: `IPropositionStore.HistoryAsync`, `PropositionBatch.Save(row, provenance)`, `PropositionBatch.Delete(name, version, provenance)`.
- Produces:
  - `CreateAsync(string name, string modelTypeId, string documentJson, string? description, RuleChangeProvenance? provenance = null, CancellationToken cancellationToken = default)`
  - `UpdateAsync(string name, string documentJson, int expectedVersion, RuleChangeProvenance? provenance = null, CancellationToken cancellationToken = default)`
  - `WithdrawAsync(string name, int expectedVersion, RuleChangeProvenance? provenance = null, CancellationToken cancellationToken = default)`
  - The `*CoreAsync` internals gain a non-optional `RuleChangeProvenance provenance` parameter before the token.
  - `internal Task<int> NextVersionAsync(string name, CancellationToken cancellationToken)` — 1 for a name the store has never held, else the log's highest version plus one.
  - `internal WritePrepare PrepareCreateCore(string name, string modelTypeId, string documentJson, string? description, int version, ScopeGenerationBuilder prospective, HashSet<NodeId> excluding)` — `version` is new, fifth.
  - `internal Task<PropositionWriteResult> WriteBatchCoreAsync(PropositionBatch batch, CancellationToken cancellationToken)` is unchanged; the batch now carries provenance.

- [ ] **Step 1: Write the failing tests**

Add to `PropositionSetCreateTests`:

```csharp
    [Fact]
    public async Task Should_continue_version_numbering_when_recreating_a_withdrawn_name()
    {
        // Arrange — v1 created, withdrawn (tombstone v2); the decision log may pin both numbers
        var (set, _, store) = NewSet();
        const string document = """{ "rule": { "spec": "customer.is-active" } }""";
        (await set.CreateAsync("customer.a", "customer", document, null)).Version.ShouldBe(1);
        (await set.WithdrawAsync("customer.a", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Removed);

        // Act
        var recreated = await set.CreateAsync("customer.a", "customer", document, null);

        // Assert
        recreated.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        recreated.Version.ShouldBe(3);
        (await store.HistoryAsync("customer.a", default)).Select(row => row.Version).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Should_record_the_caller_as_the_author_of_every_version()
    {
        // Arrange
        var (set, _, store) = NewSet();
        const string document = """{ "rule": { "spec": "customer.is-active" } }""";

        // Act
        await set.CreateAsync("customer.a", "customer", document, null, new RuleChangeProvenance("alice", "first"));
        await set.UpdateAsync("customer.a", document, 1, new RuleChangeProvenance("bob", "second"));
        await set.WithdrawAsync("customer.a", 2, new RuleChangeProvenance("carol"));

        // Assert
        var history = await store.HistoryAsync("customer.a", default);
        history.Select(row => row.Author).ShouldBe(["alice", "bob", "carol"]);
        history.Select(row => row.ChangeNote).ShouldBe(["first", "second", null]);
        history[2].IsTombstone.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_attribute_a_write_with_no_provenance_to_the_system()
    {
        // Arrange
        var (set, _, store) = NewSet();
        const string document = """{ "rule": { "spec": "customer.is-active" } }""";

        // Act
        await set.CreateAsync("customer.a", "customer", document, null);

        // Assert
        (await store.HistoryAsync("customer.a", default)).ShouldHaveSingleItem().Author.ShouldBe("system");
    }
```

- [ ] **Step 2: Run them to see them fail**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetCreateTests" 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head
```

Expected: `error CS` — `RuleChangeProvenance` is not an argument `CreateAsync` accepts. (`PropositionUpdateResult.Version` exists; if the compiler names it, read `PropositionUpdateResult.cs` and use the property the `Created(int)` factory sets.)

- [ ] **Step 3: Thread provenance and the next version through `PropositionSet`**

Apply these edits in `PropositionSet.cs`:

1. `CreateAsync` and `CreateCoreAsync`:

```csharp
    public Task<PropositionUpdateResult> CreateAsync(
        string name, string modelTypeId, string documentJson, string? description,
        RuleChangeProvenance? provenance = null, CancellationToken cancellationToken = default) =>
        Scope.LockedAsync(
            () => CreateCoreAsync(
                name, modelTypeId, documentJson, description,
                provenance ?? RuleChangeProvenance.System, cancellationToken),
            cancellationToken);

    internal async Task<PropositionUpdateResult> CreateCoreAsync(
        string name, string modelTypeId, string documentJson, string? description,
        RuleChangeProvenance provenance, CancellationToken cancellationToken)
    {
        // Read before the prepare, which runs under the monitor and cannot await. A withdrawn name
        // resumes past its tombstone rather than at 1; a concurrent re-creation on another replica
        // is caught by the store's key, exactly as any other version race is.
        var version = await NextVersionAsync(name, cancellationToken).ConfigureAwait(false);

        return await PersistAndCommitCoreAsync(
                () => PrepareCreateCascade(name, modelTypeId, documentJson, description, version),
                PropositionUpdateResult.Created,
                provenance,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The version a creation of <paramref name="name"/> claims: 1 for a name the store has never
    /// held, otherwise one past the log's highest row — a tombstone included, so a re-created name
    /// never reuses a version a decision record may already pin.
    /// </summary>
    internal async Task<int> NextVersionAsync(string name, CancellationToken cancellationToken)
    {
        var history = await _store.HistoryAsync(name, cancellationToken).ConfigureAwait(false);
        var highest = 0;
        foreach (var row in history)
        {
            if (row.Version > highest)
                highest = row.Version;
        }

        return highest + 1;
    }
```

2. `PrepareCreateCascade` and `PrepareCreateCore` take `int version` after `description` and pass it through; in `PrepareCreateCore` replace `version: 1` with `version`.

3. `UpdateAsync` / `UpdateCoreAsync` gain `RuleChangeProvenance? provenance = null` (public) and `RuleChangeProvenance provenance` (internal) before the token, and the internal one passes `provenance` to `PersistAndCommitCoreAsync`.

4. `PersistAndCommitCoreAsync` gains `RuleChangeProvenance provenance` before the token, and its write becomes `TimedWriteAsync(SaveBatchFor(prepared.Authored!, provenance), cancellationToken)`. `SaveBatchFor` becomes:

```csharp
    private static PropositionBatch SaveBatchFor(AuthoredProposition authored, RuleChangeProvenance provenance) =>
        PropositionBatch.Save(RowFor(authored), provenance);
```

5. `WithdrawAsync` / `WithdrawCoreAsync` gain the same parameters, and the delete becomes `PropositionBatch.Delete(name, prepared.Authored!.Version, provenance)`.

- [ ] **Step 4: Plumb the new parameters through `ChangeRequestSet` so the project compiles**

The signature changes break four call sites in `src/Motiv.Serialization/Governance/ChangeRequestSet.cs`. Confirm with:

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet build src/Motiv.Serialization -f net10.0 2>&1 | grep "error CS" | sort -u | head -20
```

1. In the direct-write switch (around line 652), pass the existing `provenance` local into the three proposition cores:

```csharp
                DirectWriteOperation.PropositionCreate => OfProposition(await _propositions!.CreateCoreAsync(
                    change.Name, change.ModelTypeId!, change.DocumentJson!, change.Description, provenance, cancellationToken)
                    .ConfigureAwait(false)),
                DirectWriteOperation.PropositionUpdate => OfProposition(await _propositions!.UpdateCoreAsync(
                    change.Name, change.DocumentJson!, change.BaseVersion, provenance, cancellationToken).ConfigureAwait(false)),
                _ => OfProposition(await _propositions!.WithdrawCoreAsync(
                    change.Name, change.BaseVersion, provenance, cancellationToken).ConfigureAwait(false))
```

2. In the apply path (around line 1140), compute next versions before the locked `Prepare`:

```csharp
            var nextVersions = await NextVersionsAsync(propositions, change, cancellationToken).ConfigureAwait(false);
            var prepared = rules.Scope.Locked(() => Prepare(rules, propositions, change, nextVersions));
```

with, as a private static in the same nested class:

```csharp
        /// <summary>
        /// The version each proposition this envelope may create would claim — read before the
        /// locked prepare, which cannot await. Only creations consult it; a name that turns out to be
        /// live at prepare time goes through the update path and ignores its entry.
        /// </summary>
        private static async Task<IReadOnlyDictionary<string, int>> NextVersionsAsync(
            PropositionSet? propositions, ChangeRequest change, CancellationToken cancellationToken)
        {
            var next = new Dictionary<string, int>(StringComparer.Ordinal);
            if (propositions is null)
                return next;

            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: false))
            {
                var name = proposed.Target.Name;
                if (!next.ContainsKey(name))
                    next[name] = await propositions.NextVersionAsync(name, cancellationToken).ConfigureAwait(false);
            }

            return next;
        }
```

3. `Prepare` gains `IReadOnlyDictionary<string, int> nextVersions` as its last parameter, and the create call becomes:

```csharp
                    : propositions.PrepareCreateCore(
                        name, proposed.ModelTypeId!, proposed.ProposedDocumentJson!, proposed.Description,
                        nextVersions[name], prospective, envelopeNodes);
```

If the pre-publish `Validate` pass also calls `PrepareCreateCore`, it binds only to check and discards the result; pass `version: 1` there with a comment that validation never persists. Check with:

```bash
grep -n "PrepareCreateCore(" src/Motiv.Serialization/Governance/ChangeRequestSet.cs
```

4. The envelope's batch still compiles through the two-argument `PropositionBatch` constructor and is therefore attributed to `system` for now; Task 5 makes it carry the change request.

- [ ] **Step 5: Run the `PropositionSet` tests**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~Propositions" 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -20
```

Expected: `Passed!`.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "PropositionSet — provenance on every write, and a re-created name numbers past its tombstone

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: Governance — the envelope and the direct write stamp their author

**Files:**
- Modify: `src/Motiv.Serialization/Governance/ChangeRequestSet.cs`
- Modify: `src/Motiv.Serialization.Tests/Governance/ChangeRequestSetTests.cs`
- Test project: `src/Motiv.Serialization.Tests`

**Interfaces:**
- Consumes: the Task 4 plumbing already in `ChangeRequestSet` (`NextVersionsAsync`, `Prepare(..., nextVersions)`, provenance on the direct-write cores), and `PropositionBatch(Saves, Deletes, Provenance)`.
- Produces: `EnvelopePrepare.PropositionWrites(RuleChangeProvenance provenance)` (a method now, replacing the property).

- [ ] **Step 1: Write the failing tests**

In `ChangeRequestSetTests.cs`, extend the `Host` record and `NewHost()` so the store is reachable: add `InMemoryPropositionStore Store` as the last component of `Host`, and in `NewHost()` bind `var store = new InMemoryPropositionStore();` before the `PropositionSet` is constructed with it, passing `store` into the `Host`. Then add:

```csharp
    [Fact]
    public async Task Should_record_the_change_request_as_the_provenance_of_a_published_proposition()
    {
        // Arrange — one envelope: a new proposition; publish it through the gate
        var host = NewHost();
        var created = host.Changes.Create("alice", "a note",
        [
            new(ChangeTargetKind.Proposition, "customer.eligible", EligibleIsAdult,
                BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
        ]);

        // Act
        var published = await host.Changes.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert — the row names the author, the note and the request it discharged
        published.Outcome.ShouldBe(ChangeRequestOutcome.Published);
        var row = (await host.Store.HistoryAsync("customer.eligible", default)).ShouldHaveSingleItem();
        row.Author.ShouldBe("alice");
        row.ChangeNote.ShouldBe("a note");
        row.ApprovalRef.ShouldBe(created.Change!.Id.ToString());
    }

    [Fact]
    public async Task Should_number_a_governed_recreation_past_the_tombstone()
    {
        // Arrange — created and withdrawn directly, then re-created through an envelope
        var host = NewHost();
        await host.Propositions.CreateAsync("customer.eligible", "customer", EligibleIsAdult, null);
        await host.Propositions.WithdrawAsync("customer.eligible", 1);
        var created = host.Changes.Create("alice", null,
        [
            new(ChangeTargetKind.Proposition, "customer.eligible", EligibleIsAdult,
                BaseVersion: 0, RollbackOfVersion: null, ModelTypeId: "customer"),
        ]);

        // Act
        var published = await host.Changes.PublishAsync(created.Change!.Id, breakGlassActive: false);

        // Assert
        published.Outcome.ShouldBe(ChangeRequestOutcome.Published);
        (await host.Store.HistoryAsync("customer.eligible", default)).Select(row => row.Version).ShouldBe([1, 2, 3]);
    }
```

If `ChangeRequestOutcome.Published` is not the success member's name, read `ChangeRequestResult.cs` and use the one the existing happy-path publish test asserts.

- [ ] **Step 2: Run them to see them fail**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~ChangeRequestSetTests" 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head
```

Expected: the provenance test fails on `Author` (`"system"` rather than `"alice"`); the numbering test fails with a version conflict or `[1, 2]`.

- [ ] **Step 3: Make the envelope's proposition batch carry the change request**

Hoist the provenance above the rule half in the persist phase (around line 1158) so both halves share it, and make `PropositionWrites` a method:

```csharp
            var provenance = new RuleChangeProvenance(
                change.Author, change.ChangeNote, ApprovalRef: change.Id.ToString());

            if (prepared.Rules.Count > 0)
            {
                var appended = await rules
                    .AppendCoreAsync(prepared.Rules, provenance, cancellationToken)
                    .ConfigureAwait(false);
                // ... unchanged
            }

            if (prepared.PropositionWrites(provenance) is { } batch)
```

```csharp
            public PropositionBatch? PropositionWrites(RuleChangeProvenance provenance) =>
                PropositionPublishes.Count == 0 && PropositionWithdrawals.Count == 0
                    ? null
                    : new PropositionBatch(
                        [.. PropositionPublishes.Select(publish => PropositionSet.RowFor(publish.Edit.Authored!))],
                        [.. PropositionWithdrawals.Select(
                            withdrawal => PropositionSet.DeletionFor(withdrawal.Edit.Authored!))],
                        provenance);
```

- [ ] **Step 4: Run the governance tests**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~Governance" 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -20
```

Expected: `Passed!`.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Governance src/Motiv.Serialization.Tests/Governance
git commit -m "Governance — a published envelope stamps its author and request on proposition versions

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Endpoints — the principal is the author, and a change note travels

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/Provenance.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (use the shared helper)
- Modify: `src/Motiv.Serialization.AspNetCore/MotivPropositionEndpoints.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/PropositionsContracts.cs`
- Modify: `src/Motiv.Serialization.AspNetCore.Tests/PropositionEndpointTests.cs`
- Modify: `docs/propositions/AspNetCore.md`
- Test project: `src/Motiv.Serialization.AspNetCore.Tests`

**Interfaces:**
- Consumes: `PrincipalIdentity.Subject(ClaimsPrincipal)` (already used by `MotivRulesEndpoints.ProvenanceOf`), the `PropositionSet` overloads from Task 4.
- Produces: `internal static class Provenance { public static RuleChangeProvenance Of(HttpContext http, string? changeNote = null); }`; `PropositionCreateRequest` and `PropositionPutRequest` gain `string? ChangeNote = null` as their last parameter.

- [ ] **Step 1: Write the failing test**

In `PropositionEndpointTests.cs`, add (the file's `StartAsync` wires `AddPropositions()` with an in-memory store; resolve it from `app.Services.GetRequiredService<IPropositionStore>()`):

```csharp
    [Fact]
    public async Task Should_record_the_caller_and_change_note_on_every_version()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        const string document = """{ "rule": { "spec": "customer.is-active" } }""";

        // Act
        var created = await client.PostAsJsonAsync("/api/rules/propositions", new
        {
            name = "customer.a", modelType = "customer",
            document = JsonDocument.Parse(document).RootElement, description = (string?)null,
            changeNote = "first",
        });
        var updated = await client.PutAsJsonAsync("/api/rules/propositions/customer.a", new
        {
            document = JsonDocument.Parse(document).RootElement, baseVersion = 1, changeNote = "second",
        });
        var deleted = await client.DeleteAsync("/api/rules/propositions/customer.a?baseVersion=2");

        // Assert
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var history = await app.Services.GetRequiredService<IPropositionStore>().HistoryAsync("customer.a", default);
        history.Select(row => row.ChangeNote).ShouldBe(["first", "second", null]);
        history.Select(row => row.Author).Distinct().ShouldHaveSingleItem().ShouldNotBe("system");
    }
```

If the test auth helper (`AddTestAuth`) issues a principal whose subject the rules tests assert on by name, assert that exact name instead of `ShouldNotBe("system")` — grep `Author.ShouldBe(` in `RuleEndpointTests.cs` for it.

- [ ] **Step 2: Run it to see it fail**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.AspNetCore.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head
```

Expected: fails on `ChangeNote` being null for every row (the request field is ignored) or on `Author` being `"system"`.

- [ ] **Step 3: Share the provenance helper and use it in the proposition endpoints**

Create `src/Motiv.Serialization.AspNetCore/Provenance.cs`:

```csharp
namespace Motiv.Serialization.AspNetCore;

/// <summary>Who a direct write is attributed to: the request's principal, and the note it carried.</summary>
internal static class Provenance
{
    public static RuleChangeProvenance Of(HttpContext http, string? changeNote = null) =>
        new(PrincipalIdentity.Subject(http.User), changeNote);
}
```

In `MotivRulesEndpoints.cs`, delete the private `ProvenanceOf` and replace its two uses with `Provenance.Of(http, request.ChangeNote)` and `Provenance.Of(http)`.

In `PropositionsContracts.cs`:

```csharp
public sealed record PropositionCreateRequest(
    string Name, string ModelType, JsonElement Document, string? Description, string? ChangeNote = null);

public sealed record PropositionPutRequest(JsonElement Document, int BaseVersion, string? ChangeNote = null);
```

(Keep the existing parameter order of `PropositionCreateRequest`; only append `ChangeNote`.)

In `MotivPropositionEndpoints.cs`, the three ungoverned calls become:

```csharp
                    await propositions.CreateAsync(
                        request.Name, request.ModelType, documentJson, request.Description,
                        Provenance.Of(http, request.ChangeNote), http.RequestAborted),
```

```csharp
                    await propositions.UpdateAsync(
                        name, documentJson, request.BaseVersion, Provenance.Of(http, request.ChangeNote), http.RequestAborted),
```

```csharp
                ? ToResult(await propositions.WithdrawAsync(name, baseVersion, Provenance.Of(http), http.RequestAborted), name, json)
```

The governed branches already carry the note through `GovernedPropositionWrite`'s transient change if that method takes one; if its signature has no change-note parameter, add `string? changeNote = null` as its last parameter, pass `request.ChangeNote` from the create and put endpoints, and hand it to the transient `ChangeRequest` the way `GovernedRuleWrite` does (grep `GovernedRuleWrite(` for the shape).

- [ ] **Step 4: Run the AspNetCore tests**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.AspNetCore.Tests 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -20
```

Expected: `Passed!`.

- [ ] **Step 5: Document the request field**

In `docs/propositions/AspNetCore.md`, wherever the `POST /propositions` and `PUT /propositions/{name}` bodies are listed, add the optional `changeNote` field with one sentence: it is recorded on the version row alongside the caller's identity.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests docs/propositions/AspNetCore.md
git commit -m "Proposition endpoints — the principal is the author, and a changeNote travels to the version row

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Docs and the domain glossary

**Files:**
- Modify: `docs/propositions/IPropositionStore.md`
- Modify: `CONTEXT.md`

- [ ] **Step 1: Update the interface listing and the asymmetry table**

In `docs/propositions/IPropositionStore.md`, update the code block at the top to the new shapes (`PropositionBatch` with `Provenance`, `HistoryAsync`, `StoredPropositionVersion`, `PropositionPosition`), then replace the "The Asymmetry with `IRuleStore`" section with:

```markdown
## The Symmetry with `IRuleStore`

Both stores are append-only version logs keyed on `(Name, Version)`, and that key is the
cross-process compare-and-set for both:

| | `IRuleStore` | `IPropositionStore` |
|---|---|---|
| History | An append-only version log, kept forever | The same &mdash; `HistoryAsync(name)` |
| A withdrawal | A null document records a revert to the compiled default | A **tombstone** &mdash; a row one past the deleted version with no document |
| A re-created name | Continues the numbering | Continues the numbering past the tombstone |
| Rollback | `RestoreAsync` re-publishes a recorded version | None &mdash; the log exists for replay, not rollback |
| Provenance | On every row | On every row, from the batch |

The proposition log exists because the [decision log](../decision-log/index.md) pins the version of
every proposition a rule resolved through. Without it, that number named a document nobody kept.
`Load()` still returns live heads only; a tombstoned name is absent from the heads and present in
the history.

An existing database from before the log holds `MotivProposition` and lacks
`MotivPropositionVersion`. The EF store does not migrate it: create the table (the model's create
script names it), or delete a development store and let the JSON import re-seed it. Rows in the
old table are not read.
```

(If `docs/decision-log/index.md` does not exist, link to the 3B design doc path instead.)

- [ ] **Step 2: Update the glossary**

In `CONTEXT.md`, under Durability, change the **Version log** entry's first sentence to cover both artefacts, and add a term:

```markdown
**Version log**:
The append-only, immutable record of every rule and proposition publish — one row per
`(Name, Version)`, kept forever and never rewritten. The primary key is also the cross-process
compare-and-set: two replicas racing the same version race on the insert, and the key lets exactly
one win.
_Avoid_: History table, audit log (the log *is* the audit trail, not a copy of one), changelog

**Tombstone**:
The version-log row a proposition withdrawal writes — one past the withdrawn version, with no
document — so the log records who withdrew the name and when, and a later re-creation continues the
numbering rather than reusing a version the decision log may pin.
_Avoid_: Deleted row, soft delete, marker
```

- [ ] **Step 3: Commit**

```bash
git add docs/propositions/IPropositionStore.md CONTEXT.md
git commit -m "Docs — the proposition store is a version log; tombstone enters the glossary

Refs #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: Full-solution verification, the design doc, and the simplifier pass

**Files:**
- Create: `docs/superpowers/specs/2026-09-20-proposition-version-log-design.md`
- This plan: `docs/superpowers/plans/2026-09-20-proposition-version-log.md`

- [ ] **Step 1: Build the whole solution on every target, then run every test project**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet build 2>&1 | grep -E "error|Warn.*CS|Build succeeded" | head
```

Expected: `Build succeeded`, no `error`. Then:

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test 2>&1 | grep -E "error CS|Passed!|Failed!|Failed " | head -40
```

Expected: every project `Passed!`, including `src/examples/*.Tests`. If a net8/net9 run reports a missing runtime, use the `~/.dotnet/dotnet` muxer for those frameworks as the memory notes describe, and report which frameworks ran.

- [ ] **Step 2: Write the slice's design doc**

Create `docs/superpowers/specs/2026-09-20-proposition-version-log-design.md`:

```markdown
# Proposition version log — Design

**Date:** 2026-09-20
**Status:** Implemented
**Parent:** `2026-09-20-decision-reproduction-mcp-design.md`, section 2. Slice 1 of 5. Resolves #224.

## Problem

The decision log pins the version of every proposition a rule resolved through, but the
proposition store replaced rows in place, so that number named a document nobody kept. The
2026-09-09 CAS design closed the concurrency half of the rule/proposition asymmetry and
deliberately left history open, asking whether replay was an obligation. The reproduction design
answers yes.

## Decisions

1. **The `WriteAsync(PropositionBatch)` contract is kept.** The CAS design said a log could
   implement it "by a PK insert with no caller change". `HistoryAsync(name)` is the only addition.
2. **A deletion writes a tombstone at `version + 1`.** `Load()` excludes it; the history keeps it.
   Re-creation continues past it, so a version number is never reused for a name.
3. **Provenance travels on the batch.** Every row carries author, note, approval reference and
   build id. Direct writes are attributed to the principal; governed writes to the change request.
4. **The store stamps the timestamp.** A rule row is stamped by `RuleSet`; a proposition row by
   the store. One place rather than two, since two callers build proposition batches.
5. **`PropositionSet` reads the history once per creation** to learn the next version. Under the
   monitor it cannot await, so the read precedes the locked prepare; a racing replica is refused
   by the key.
6. **`MotivPropositionVersion` replaces `MotivProposition`.** No migration: the schema guard
   names the missing table and the docs say what to do.
7. **Import carries heads, not history.** The JSON store predates the log; imported rows are
   attributed to the system.
8. **Rows are kept forever.**

## Rejected

- A separate head table beside the log: two writes per publish, and a head that can drift from
  the log it is meant to summarise. Rules derive the head in SQL; propositions now do too.
- Tombstones in `Load()` as null-document heads: a null document is already the malformed-row
  case `PropositionSet` quarantines, and overloading it would put withdrawn names in the catalog
  as broken.
- Restarting a re-created name at version 1: the decision log may pin the old v1.
```

Also amend the parent spec's section 2 (`docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md`), which sketched `AppendAsync(versions)` on `IPropositionStore`. Replace that paragraph with:

```markdown
`IPropositionStore` keeps its `WriteAsync(PropositionBatch)` contract — the 2026-09-09 CAS design
said a log could implement it by a PK insert with no caller change — and gains `HistoryAsync(name)`
returning `StoredPropositionVersion(Name, Version, ModelType, DocumentJson, Description, Author,
TimestampUtc, ChangeNote, ApprovalRef, BuildId)` in version order. A save inserts a row keyed
`(Name, Version)`; a deletion inserts a tombstone at `Version + 1` with a null document, so a
re-created name continues its numbering. Provenance travels on the batch. Rows are kept forever.
The in-memory, EF and Studio JSON stores implement it; the head projection stays derived from the
log. See `2026-09-20-proposition-version-log-design.md`.
```

- [ ] **Step 3: Spawn the mandatory `code-simplifier` review over the branch's diff**

Use the Agent tool with `subagent_type: code-simplifier:code-simplifier`, pointing it at `git diff main...HEAD -- src/` and asking for duplication between the three store implementations, long methods, and naming. Apply what it finds; re-run the affected test projects.

- [ ] **Step 4: Commit the docs, then open the PR**

```bash
git add docs/superpowers/specs/2026-09-20-proposition-version-log-design.md docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md docs/superpowers/plans/2026-09-20-proposition-version-log.md
git commit -m "Design and plan — proposition version log (slice 1 of decision reproduction)

Closes #224

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

Then push and open a PR against `main` whose description states: the contract kept, the tombstone rule, the provenance sources, the replaced table and what an existing database must do, and which target frameworks the full suite ran on.
