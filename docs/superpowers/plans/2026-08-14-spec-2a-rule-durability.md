# Spec 2A — Rule Durability: the store seam, the version log, and the async write contract

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rules survive a restart — behind an `IRuleStore` seam symmetrical with `IPropositionStore`, backed by an append-only version log whose `(Name, Version)` primary key *is* the compare-and-set, on an authoring write path that is async and cancellable.

**Architecture:** Two layered phases. Phase 1 introduces the seam and the log while the write path is still synchronous: the store records, `IRuleStore`, a bind/publish split in `RuleBase` so everything fallible runs before anything mutates, head-as-projection loading with quarantine, and rollback-appends. Phase 2 makes the authoring write path async via two-tier exclusion — an outer `SemaphoreSlim` on `BindingScope` for whole operations, the existing inner `Monitor` untouched for data-structure mutation — and ripples that through `PropositionSet`, `ChangeRequestSet` and the endpoints. Phase 3 discharges the spec's verification obligations.

**Tech Stack:** C# / .NET (`Motiv.Serialization` multi-TFM incl. `netstandard2.0`; `Motiv.Serialization.AspNetCore` net10.0), xUnit + Shouldly, ASP.NET Core minimal APIs.

**Source spec:** `.scratch/enterprise-grade-product/specs/2-durability-and-data.md` on branch `wayfinder/enterprise-grade-product` (tickets 02, 09, 10, 21 in `.scratch/enterprise-grade-product/issues/`). Map: [#100](https://github.com/karlssberg/Motiv/issues/100). Glossary: `CONTEXT.md`.

---

## Scope

Bundle spec 2 has a six-step build sequence. This plan covers **steps 1, 2, 3 and 6**. Steps 4 and 5
are separate plans because each is an independent subsystem that ships on its own:

| Spec build step | Plan |
|---|---|
| 1. `IRuleStore` + records + quarantine (02) | **this plan**, Phase 1 |
| 2. Async two-tier exclusion + `SaveAsync` path (09) | **this plan**, Phase 2 |
| 3. Version log + head projection + rollback (10) | **this plan**, Phase 1 |
| 6. PK-as-CAS; delete the in-memory CAS (21) | **this plan**, Phase 1 |
| 4. Generation + `RefreshAsync` + poller (20) | **plan 2B** — multi-instance refresh |
| 5. EF reference store + migrations + importer (16) | **plan 2C** — `Motiv.Serialization.EntityFrameworkCore` |

`GetGenerationAsync` **is** defined on `IRuleStore` in this plan even though nothing polls it yet.
Adding a member to a published-shaped interface later would break every implementer; the two
implementations here satisfy it in three lines each. Plan 2B builds the poller on top.

Steps 4 and 6 were resequenced relative to the spec (6 before 4) because the PK-as-CAS *is* the
version log's primary key — separating them would mean writing the log, shipping it with the
in-memory CAS still in place, then removing the CAS in a later pass over the same five files.

---

## Global Constraints

- **Every `dotnet` command** must be prefixed with `export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"` — net8/net9 testhosts abort otherwise. Use `-f net10.0` for filtered runs; **net472 never runs on this Mac**.
- There is **no `timeout` command on macOS** — never wrap a test run in it. It silently runs nothing and greps clean.
- `Motiv.Serialization` targets `net8.0;net9.0;netstandard2.0;net10.0`. **No C# 8+ features needing runtime support**: no ranges (`[..^n]`), no `System.Index`/`System.Range`, no `IAsyncEnumerable`, no default interface methods. `SemaphoreSlim.WaitAsync(CancellationToken)` and `Task<T>` are available on all four. Use `Task<T>`, never `ValueTask<T>` (needs `System.Threading.Tasks.Extensions` on netstandard2.0).
- Always `.ConfigureAwait(false)` on every `await` inside `Motiv.Serialization` — it is a library.
- **TDD strictly**: failing test → confirm it fails for the right reason → minimal code → confirm it passes → commit. Never write implementation before its test.
- Test naming follows the repo: `public class {Subject}Tests`, `[Fact] public void Should_snake_case_phrase()` (`async Task` when the subject is async), `// Arrange` / `// Act` / `// Assert` comments, **Shouldly** assertions.
- Run the **full solution suite** — including `src/examples/Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`, `Motiv.RulesEngine.Sample.Tests` — before calling any phase complete. Example projects assert on justification strings and break silently otherwise.
- `Motiv.Serialization.AspNetCore` and `Motiv.Serialization` have **never been published** (ticket 06). Every breaking change here costs nothing. **No compatibility shims, no `[Obsolete]` bridges** — delete the old signature.
- Published `Motiv` (v8.0.0) must **not** be touched. `git diff --stat` must show zero files changed under `src/Motiv/` at the end.
- Per project convention (`CLAUDE.md`), after each phase's tests pass, spawn a `code-simplifier` agent over the changed files and apply its findings before moving on.

---

## Design Decisions Locked by This Plan

The spec fixes the architecture. These are the mechanism-level decisions it left open, decided once
here so no task re-derives them.

1. **The store takes batches, not single rows.** `IRuleStore.AppendAsync` accepts
   `IReadOnlyList<StoredRuleVersion>`. This is forced by spec §4's *"everything fallible runs before
   anything mutates"*: `ChangeRequestSet.ApplyValidated` publishes a whole envelope and **throws** on
   any non-`Updated` outcome, because everything was validated first. A per-row store call inside
   that loop would reintroduce a failure point after mutation had begun. A single-rule write is the
   one-element case.

2. **`IPropositionStore` gains a matching batch write** — `WriteAsync(PropositionBatch, ct)` carrying
   saves and deletes — replacing `Save`/`Delete`, for the same reason. Ticket 16's EF store wants
   head + version-append + generation-bump in one transaction anyway.

3. **Head is a projection, not a row, at the *contract* level too.** `IRuleStore.Load()` returns
   `IReadOnlyList<StoredRule>` — `(Name, Version, DocumentJson?)` — and every implementation derives
   it from `max(Version)` over the log. `StoredRule` is never appended, only read. Divergence is
   unrepresentable (spec §4).

4. **Quarantine keeps the rule evaluable but never silent.** A stored document that no longer binds
   leaves the rule bound to its **compiled default** in memory — a rule must be able to evaluate, and
   there is nothing else to bind — but the stored version is preserved, the errors are recorded on
   `RuleSetEntry.Quarantine`, and `RuleSet.Load` returns a `RuleLoadReport`. Ticket 02 rejected a
   *silent* fall-back, not a reported one; fail-fast is the host's policy, so
   `RuleLoadReport.ThrowIfQuarantined()` is provided and the AspNetCore DI wiring **calls it by
   default** (consistent with spec 1's fail-closed discipline), with an opt-out on
   `MotivRulesBuilder.AddRuleStore`.

5. **Provenance is one parameter, not five.** `RuleChangeProvenance(Author, ChangeNote, ApprovalRef,
   BuildId)`. `BuildId` defaults to `BuildIdentity.Current`, read once from the entry assembly's
   `AssemblyInformationalVersionAttribute` — ticket 02's *"a code-defined rule tracks code with no
   version bump, which is unfixable, so the decision log must pin the build"*.

6. **The bind/publish split lives in `Rule<,>` and `AsyncRule<,>` only.** `PolicyRule<,>` and
   `AsyncPolicyRule<,>` override only `RequirePolicy`, and `TryUpdate`/`TryRevert` are already
   `sealed override` in the two base classes — so there are exactly **two** implementations to
   change, not four.

7. **Readers stay synchronous.** The outer `SemaphoreSlim` guards *writes* only. `RuleSet.Rules`,
   `Find`, `PropositionSet.Propositions` etc. keep the inner `Monitor`. Making reads async would buy
   nothing and would make `RuleSetEntry` lookups awkward at every call site.

8. **The outer gate is acquired only at public entry points** (spec §2). `SemaphoreSlim` is not
   reentrant, so an inner call to a public method self-deadlocks. Every `…Core` method keeps its
   "assumes the caller holds the gate" contract, and the public/`Core` split that already exists for
   governance is exactly the seam this needs.

9. **`RuleSet` without a store is still legal.** The default is `InMemoryRuleStore`, so existing
   hosts that never asked for durability behave as they do today, and every existing test that
   constructs a bare `RuleSet` keeps compiling.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Motiv.Serialization/Rules/StoredRule.cs` | `StoredRule` head projection + `StoredRuleVersion` log row |
| `src/Motiv.Serialization/Rules/RuleChangeProvenance.cs` | Who/when/why/which-build, + `BuildIdentity.Current` |
| `src/Motiv.Serialization/Rules/IRuleStore.cs` | The seam, `RuleAppendResult`, `InMemoryRuleStore` |
| `src/Motiv.Serialization/Rules/RulePreparation.cs` | `IRulePublication`, `RulePrepareResult` — the bind/publish split |
| `src/Motiv.Serialization/Rules/RuleBase.cs` | `PrepareUpdate`/`PrepareRevert` replace `TryUpdate`/`TryRevert` |
| `src/Motiv.Serialization/Rules/Rule.cs` | Split implementation; **`Interlocked.CompareExchange` deleted** |
| `src/Motiv.Serialization/Rules/AsyncRule.cs` | Same, for the async flavour |
| `src/Motiv.Serialization/Rules/RuleSet.cs` | Takes a store; `Load`, `UpdateAsync`, `RevertAsync`, `…Core` |
| `src/Motiv.Serialization/Rules/RuleLoadReport.cs` | Quarantine report + `ThrowIfQuarantined()` |
| `src/Motiv.Serialization/Rules/RuleSetEntry.cs` | Gains `Quarantine` |
| `src/Motiv.Serialization/Propositions/BindingScope.cs` | Outer `SemaphoreSlim`; `LockedAsync` |
| `src/Motiv.Serialization/Propositions/IPropositionStore.cs` | `WriteAsync(PropositionBatch)` replaces `Save`/`Delete` |
| `src/Motiv.Serialization/Propositions/PropositionSet.cs` | Async write path; persist-before-mutate |
| `src/Motiv.Serialization/Governance/ChangeRequestSet.cs` | Async publish; validate-all → persist-all → apply-all |
| `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` | Handlers await the write path |
| `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs` | `AddRuleStore`; load-and-report at startup |
| `src/examples/Motiv.RulesEngine.Sample/JsonFileRuleStore.cs` | File-backed reference implementation |

---

# Phase 1 — The seam and the log

The write path stays synchronous throughout this phase. Every test written here keeps passing
unchanged through Phase 2 except for `await`.

---

### Task 1: The store records and provenance

**Files:**
- Create: `src/Motiv.Serialization/Rules/StoredRule.cs`
- Create: `src/Motiv.Serialization/Rules/RuleChangeProvenance.cs`
- Test: `src/Motiv.Serialization.Tests/Rules/RuleChangeProvenanceTests.cs`

**Interfaces:**
- Produces: `StoredRule(string Name, int Version, string? DocumentJson)`;
  `StoredRuleVersion(string Name, int Version, string? DocumentJson, string Author, DateTimeOffset TimestampUtc, string? ChangeNote, string? ApprovalRef, string? BuildId)`;
  `RuleChangeProvenance(string Author, string? ChangeNote, string? ApprovalRef, string? BuildId)` with
  `RuleChangeProvenance.System`; `BuildIdentity.Current`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleChangeProvenanceTests
{
    [Fact]
    public void Should_default_the_build_id_to_the_current_build()
    {
        // Act
        var provenance = new RuleChangeProvenance("alice").WithDefaults();

        // Assert — a code-defined rule tracks code with no version bump, so the row must pin the build
        provenance.BuildId.ShouldBe(BuildIdentity.Current);
        provenance.BuildId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Should_keep_an_explicit_build_id()
    {
        // Act
        var provenance = new RuleChangeProvenance("alice", BuildId: "deadbeef").WithDefaults();

        // Assert
        provenance.BuildId.ShouldBe("deadbeef");
    }

    [Fact]
    public void Should_name_the_system_as_author_when_no_principal_is_involved()
    {
        // Assert — a rebind or a startup load is not a person's edit
        RuleChangeProvenance.System.Author.ShouldBe("system");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleChangeProvenanceTests"
```
Expected: FAIL — `RuleChangeProvenance` and `BuildIdentity` do not exist (CS0246).

- [ ] **Step 3: Write the records**

`src/Motiv.Serialization/Rules/StoredRule.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A rule's current state as the store reports it — the <em>head</em>. Never appended: every store
/// derives this by projection from the highest <see cref="StoredRuleVersion.Version"/> in the log, so
/// head and history cannot drift apart.
/// </summary>
/// <param name="Name">The rule name, matching a <see cref="RuleBase.Name"/> registered in the set.</param>
/// <param name="Version">The highest version recorded for the name.</param>
/// <param name="DocumentJson">
/// The document at that version, or null meaning "on the compiled default at this version". Null is a
/// meaningful state and must never collapse to an absent row — a revert records that the rule went
/// back to code, which an absent row could not distinguish from never having been authored.
/// </param>
public sealed record StoredRule(string Name, int Version, string? DocumentJson);

/// <summary>
/// One immutable row of the append-only version log: what was published, by whom, when, and why.
/// The primary key is <c>(Name, Version)</c>, which is also the cross-process compare-and-set — two
/// replicas both computing "next = 6" race on the insert and the key lets exactly one win.
/// </summary>
/// <param name="Name">The rule name.</param>
/// <param name="Version">This row's version. Immutable: the number names this row forever.</param>
/// <param name="DocumentJson">The document published, or null for "reverted to the compiled default".</param>
/// <param name="Author">Who published it.</param>
/// <param name="TimestampUtc">When it was published.</param>
/// <param name="ChangeNote">An optional human-supplied reason.</param>
/// <param name="ApprovalRef">The change request this publish discharged, when governed.</param>
/// <param name="BuildId">
/// The build that was live at publish time. A compiled default cannot be fingerprinted — delegates
/// have no stable hash — so the build id is the only anchor identifying what a null document meant.
/// </param>
public sealed record StoredRuleVersion(
    string Name,
    int Version,
    string? DocumentJson,
    string Author,
    DateTimeOffset TimestampUtc,
    string? ChangeNote,
    string? ApprovalRef,
    string? BuildId);
```

`src/Motiv.Serialization/Rules/RuleChangeProvenance.cs`:

```csharp
using System.Reflection;

namespace Motiv.Serialization;

/// <summary>
/// The who/why of a publish, supplied by the caller and written into the version log. Carried as one
/// parameter rather than four so that adding an anchor later does not re-break every write signature.
/// </summary>
/// <param name="Author">Who is publishing. Required — an unattributed row is not an audit record.</param>
/// <param name="ChangeNote">An optional human-supplied reason.</param>
/// <param name="ApprovalRef">The change request this discharges, when the publish was governed.</param>
/// <param name="BuildId">
/// The build to pin, or null to take <see cref="BuildIdentity.Current"/> at write time.
/// </param>
public sealed record RuleChangeProvenance(
    string Author,
    string? ChangeNote = null,
    string? ApprovalRef = null,
    string? BuildId = null)
{
    /// <summary>
    /// The attribution for a publish no principal asked for — a startup load, or a rebind triggered by
    /// someone else's proposition edit. Distinguishable in the log from a person's edit, which is the
    /// point: "who changed this?" must not answer with the last human to touch something adjacent.
    /// </summary>
    public static RuleChangeProvenance System { get; } = new("system");

    /// <summary>Fills in anything the caller left to the library. Called once, at write time.</summary>
    public RuleChangeProvenance WithDefaults() =>
        BuildId is null ? this with { BuildId = BuildIdentity.Current } : this;
}

/// <summary>Identifies the running build, so a version row can pin behaviour that is not in a document.</summary>
public static class BuildIdentity
{
    /// <summary>
    /// The entry assembly's informational version, falling back to its plain version and then to
    /// <c>"unknown"</c>. Read once — it cannot change within a process, and a host that wants
    /// something more precise (a commit sha) passes <see cref="RuleChangeProvenance.BuildId"/> itself.
    /// </summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
            return "unknown";

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informational)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informational!;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleChangeProvenanceTests"
```
Expected: PASS — 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Rules/StoredRule.cs src/Motiv.Serialization/Rules/RuleChangeProvenance.cs src/Motiv.Serialization.Tests/Rules/RuleChangeProvenanceTests.cs && git commit -m "feat(serialization): add the stored-rule records and publish provenance"
```

---

### Task 2: `IRuleStore` and the in-memory implementation

**Files:**
- Create: `src/Motiv.Serialization/Rules/IRuleStore.cs`
- Test: `src/Motiv.Serialization.Tests/Rules/InMemoryRuleStoreTests.cs`

**Interfaces:**
- Consumes: `StoredRule`, `StoredRuleVersion` (Task 1).
- Produces: `IRuleStore` with `Load()`, `LoadAsync(ct)`, `GetGenerationAsync(ct)`,
  `AppendAsync(IReadOnlyList<StoredRuleVersion>, ct)`, `HistoryAsync(string, ct)`;
  `RuleAppendResult` with `RuleAppendResult.Appended` and `RuleAppendResult.Conflict(name, currentVersion)`;
  `InMemoryRuleStore`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class InMemoryRuleStoreTests
{
    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", DateTimeOffset.UnixEpoch, null, null, "test");

    [Fact]
    public async Task Should_project_the_head_from_the_highest_version()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 2, """{"v":2}""")], default);
        await store.AppendAsync([Row("a", 3, """{"v":3}""")], default);

        // Act
        var heads = store.Load();

        // Assert — head is a projection, never a stored duplicate, so it cannot diverge
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(3);
        heads[0].DocumentJson.ShouldBe("""{"v":3}""");
    }

    [Fact]
    public async Task Should_keep_a_null_document_as_a_head_rather_than_an_absent_row()
    {
        // Arrange — a revert records that the rule went back to the compiled default
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 1, """{"v":1}""")], default);
        await store.AppendAsync([Row("a", 2, documentJson: null)], default);

        // Act
        var heads = store.Load();

        // Assert
        heads.ShouldHaveSingleItem();
        heads[0].Version.ShouldBe(2);
        heads[0].DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_reject_a_duplicate_name_and_version_as_a_conflict()
    {
        // Arrange — this is the cross-process compare-and-set: two replicas both computing next = 2
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 1)], default);
        await store.AppendAsync([Row("a", 2, """{"winner":true}""")], default);

        // Act
        var result = await store.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert
        result.IsConflict.ShouldBeTrue();
        result.Name.ShouldBe("a");
        result.CurrentVersion.ShouldBe(2);
        store.Load()[0].DocumentJson.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public async Task Should_append_a_whole_batch_or_none_of_it()
    {
        // Arrange — an envelope's rows must not land half-way; the second row conflicts
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("b", 1)], default);

        // Act
        var result = await store.AppendAsync([Row("a", 1), Row("b", 1)], default);

        // Assert — 'a' must not have landed
        result.IsConflict.ShouldBeTrue();
        result.Name.ShouldBe("b");
        store.Load().ShouldHaveSingleItem();
        store.Load()[0].Name.ShouldBe("b");
    }

    [Fact]
    public async Task Should_move_the_generation_forward_on_every_successful_append()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var before = await store.GetGenerationAsync(default);

        // Act
        await store.AppendAsync([Row("a", 1)], default);
        var after = await store.GetGenerationAsync(default);

        // Assert
        after.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_not_move_the_generation_on_a_rejected_append()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 1)], default);
        var before = await store.GetGenerationAsync(default);

        // Act
        await store.AppendAsync([Row("a", 1)], default);

        // Assert — a rejected write changed nothing, so replicas must not be told to rebuild
        (await store.GetGenerationAsync(default)).ShouldBe(before);
    }

    [Fact]
    public async Task Should_return_the_whole_history_of_a_name_in_version_order()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([Row("a", 2)], default);
        await store.AppendAsync([Row("a", 1)], default);

        // Act
        var history = await store.HistoryAsync("a", default);

        // Assert — kept forever, in order, so "what did v1 say?" is always answerable
        history.Select(row => row.Version).ShouldBe([1, 2]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryRuleStoreTests"
```
Expected: FAIL — `IRuleStore`/`InMemoryRuleStore` do not exist (CS0246).

- [ ] **Step 3: Write the seam**

`src/Motiv.Serialization/Rules/IRuleStore.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Where published rules are kept between restarts — the rule-side twin of
/// <see cref="IPropositionStore"/>. The two are symmetrical and are <em>never written in the same
/// transaction</em>: they coordinate independently, and no operation spans both.
/// </summary>
/// <remarks>
/// <para>
/// A store is a dumb sink for <em>semantic</em> legality — it validates no document and enforces no
/// rule-level invariant; <see cref="RuleSet"/> decides all of that before anything reaches here. It is
/// not, however, dumb about <em>structure</em>: the <c>(Name, Version)</c> primary key is load-bearing.
/// It is the compare-and-set that makes a lost update impossible across processes, and
/// <see cref="AppendAsync"/> reporting a conflict is how a stale writer finds out.
/// </para>
/// <para>
/// The log is append-only and kept forever. A rollback does not rewrite history — restoring v5 appends
/// v9 carrying v5's document, which also records <em>that a rollback happened</em>.
/// </para>
/// </remarks>
public interface IRuleStore
{
    /// <summary>
    /// Every rule's head, read once at startup. Synchronous because startup is: the DI factory wall
    /// cannot await, and paying for an async path there would buy nothing.
    /// </summary>
    IReadOnlyList<StoredRule> Load();

    /// <summary>
    /// Every rule's head, read on a refresh. Separate from <see cref="Load"/> rather than replacing it
    /// because the two run at different times under different constraints.
    /// </summary>
    Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A monotonically increasing number that moves whenever a write lands, so a replica can tell
    /// whether it is behind without re-reading anything.
    /// </summary>
    /// <remarks>
    /// <strong>Must be a scalar read.</strong> An implementation that answers this by loading the
    /// store defeats the entire point — it is polled on a timer by every replica. It must also never
    /// move backwards while replicas are live, including across a restore: it is the fencing token
    /// behind monotonic-read consistency.
    /// </remarks>
    Task<long> GetGenerationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Appends version rows — all of them, or none. A row whose <c>(Name, Version)</c> already exists
    /// is refused, and the whole batch with it.
    /// </summary>
    /// <remarks>
    /// The batch is not a convenience. A governed publish validates a whole envelope, then persists it,
    /// then mutates memory; a per-row call would put a failure point after mutation had begun and break
    /// "a failed persist leaves nothing live".
    /// </remarks>
    Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken);

    /// <summary>Every recorded version of one rule, oldest first. Empty when the name is unknown.</summary>
    Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of an <see cref="IRuleStore.AppendAsync"/>. A conflict is an expected outcome — a
/// second writer arriving with the same version — so it is a value, not an exception.
/// </summary>
public sealed class RuleAppendResult
{
    private RuleAppendResult(bool isConflict, string? name, int currentVersion)
    {
        IsConflict = isConflict;
        Name = name;
        CurrentVersion = currentVersion;
    }

    /// <summary>Whether the batch was refused because a row's version was already taken.</summary>
    public bool IsConflict { get; }

    /// <summary>The rule whose version was taken, or null when nothing conflicted.</summary>
    public string? Name { get; }

    /// <summary>The version that name is actually at, or 0 when nothing conflicted.</summary>
    public int CurrentVersion { get; }

    /// <summary>Every row landed.</summary>
    public static RuleAppendResult Appended { get; } = new(false, null, 0);

    /// <summary>Nothing landed: <paramref name="name"/> is already at <paramref name="currentVersion"/>.</summary>
    public static RuleAppendResult Conflict(string name, int currentVersion) =>
        new(true, name, currentVersion);
}

/// <summary>The default store: rules live for the lifetime of the process, as they always have.</summary>
/// <remarks>
/// Real, not a stub — it implements the same primary key, so the conflict path this store produces is
/// the one a database store produces, and a test written against it holds against Postgres.
/// </remarks>
public sealed class InMemoryRuleStore : IRuleStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<StoredRuleVersion>> _log = new(StringComparer.Ordinal);
    private long _generation;

    /// <inheritdoc />
    public IReadOnlyList<StoredRule> Load()
    {
        lock (_gate)
            return [.. _log.Values.Select(Head)];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(_generation);
    }

    /// <inheritdoc />
    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            // Check every row before writing any of them: the batch is all-or-nothing, and there is
            // no rollback here — refusing up front is what makes that true.
            foreach (var version in versions)
            {
                if (_log.TryGetValue(version.Name, out var existing)
                    && existing.Any(row => row.Version == version.Version))
                {
                    return Task.FromResult(
                        RuleAppendResult.Conflict(version.Name, existing.Max(row => row.Version)));
                }
            }

            foreach (var version in versions)
            {
                if (!_log.TryGetValue(version.Name, out var rows))
                    _log[version.Name] = rows = [];
                rows.Add(version);
            }

            if (versions.Count > 0)
                _generation++;

            return Task.FromResult(RuleAppendResult.Appended);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredRuleVersion>>(
                _log.TryGetValue(name, out var rows)
                    ? [.. rows.OrderBy(row => row.Version)]
                    : []);
        }
    }

    /// <summary>The head projection: the highest version's row, reduced to what a load needs.</summary>
    private static StoredRule Head(List<StoredRuleVersion> rows)
    {
        var head = rows[0];
        foreach (var row in rows)
        {
            if (row.Version > head.Version)
                head = row;
        }

        return new StoredRule(head.Name, head.Version, head.DocumentJson);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryRuleStoreTests"
```
Expected: PASS — 7 tests.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Rules/IRuleStore.cs src/Motiv.Serialization.Tests/Rules/InMemoryRuleStoreTests.cs && git commit -m "feat(serialization): add IRuleStore, with (Name,Version) as the compare-and-set"
```

---

### Task 3: Split binding from publishing, and delete the in-memory CAS

The current `TryUpdate` binds *and* publishes in one call, so there is nowhere to put a store write
between "this document is good" and "this document is live". This task opens that gap and closes
ticket 21 in the same edit — the `Interlocked.CompareExchange` becomes unreachable once publishing is
a separate, already-decided step, and it is blind across processes anyway.

**Files:**
- Create: `src/Motiv.Serialization/Rules/RulePreparation.cs`
- Modify: `src/Motiv.Serialization/Rules/RuleBase.cs:50-54`
- Modify: `src/Motiv.Serialization/Rules/Rule.cs:76-115,180-192`
- Modify: `src/Motiv.Serialization/Rules/AsyncRule.cs:81-120,~185-197`
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs:176-181,250-259`
- Test: `src/Motiv.Serialization.Tests/Rules/RulePreparationTests.cs`

**Interfaces:**
- Produces: `internal interface IRulePublication { int Version { get; } string? DocumentJson { get; } void Commit(); }`;
  `internal sealed class RulePrepareResult` with `Prepared(IRulePublication)`, `VersionConflict(int)`,
  `Invalid(IReadOnlyList<RuleError>)`, `NotFound()`, and `ToUpdateResult()`.
- Consumed by: Task 4 (`RuleSet.Load`), Task 7 (`RuleSet.UpdateAsync`), Task 9 (`ChangeRequestSet`).

- [ ] **Step 1: Write the failing test**

`Motiv.Serialization.Tests` already has `[InternalsVisibleTo]`, so internals are reachable.

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RulePreparationTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    private static (RuleSet Set, SampleRule Rule) Bound()
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new SampleRule();
        return (new RuleSet(registry).Add(rule), rule);
    }

    [Fact]
    public void Should_not_move_the_live_rule_when_only_prepared()
    {
        // Arrange
        var (set, rule) = Bound();

        // Act — prepare, but never commit
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 1);

        // Assert — the whole point of the split: nothing is live until Commit runs
        prepared.Publication.ShouldNotBeNull();
        prepared.Publication!.Version.ShouldBe(2);
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public void Should_publish_only_once_committed()
    {
        // Arrange
        var (set, rule) = Bound();
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 1);

        // Act
        prepared.Publication!.Commit();

        // Assert
        rule.Version.ShouldBe(2);
        rule.DocumentJson.ShouldBe(Document);
    }

    [Fact]
    public void Should_refuse_a_stale_expected_version_before_binding()
    {
        // Arrange
        var (set, _) = Bound();

        // Act
        var prepared = set.PrepareUpdateCore("sample", Document, expectedVersion: 99);

        // Assert
        prepared.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        prepared.Version.ShouldBe(1);
        prepared.Publication.ShouldBeNull();
    }

    [Fact]
    public void Should_report_an_unbindable_document_as_invalid()
    {
        // Arrange
        var (set, _) = Bound();

        // Act
        var prepared = set.PrepareUpdateCore(
            "sample", """{ "rule": { "spec": "customer.does-not-exist" } }""", expectedVersion: 1);

        // Assert
        prepared.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        prepared.Errors.ShouldNotBeEmpty();
        prepared.Publication.ShouldBeNull();
    }

    [Fact]
    public void Should_prepare_a_revert_carrying_the_defaults_document()
    {
        // Arrange
        var (set, rule) = Bound();
        set.PrepareUpdateCore("sample", Document, expectedVersion: 1).Publication!.Commit();

        // Act
        var prepared = set.PrepareRevertCore("sample", expectedVersion: 2);

        // Assert — a compiled default publishes a null document, and the version still moves forward
        prepared.Publication.ShouldNotBeNull();
        prepared.Publication!.Version.ShouldBe(3);
        prepared.Publication.DocumentJson.ShouldBeNull();
        rule.Version.ShouldBe(2);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RulePreparationTests"
```
Expected: FAIL — `PrepareUpdateCore`/`PrepareRevertCore` do not exist (CS1061).

- [ ] **Step 3: Create the preparation types**

`src/Motiv.Serialization/Rules/RulePreparation.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A rule change that has bound successfully and is waiting to go live. Mirrors
/// <see cref="IRebindCommit"/>: preparing everything before committing anything is what makes a
/// publish all-or-nothing, and here it is also what makes room for the store write — the one step
/// between "this binds" and "this is live" that can still fail.
/// </summary>
internal interface IRulePublication
{
    /// <summary>The version this publication will carry once committed.</summary>
    int Version { get; }

    /// <summary>The document it will carry, or null for a return to the compiled default.</summary>
    string? DocumentJson { get; }

    /// <summary>Makes the change live. Cannot fail, and must be called under the scope lock.</summary>
    void Commit();
}

/// <summary>
/// The outcome of preparing a rule change: a publication ready to commit, or the reason there is none.
/// The same four outcomes <see cref="RuleUpdateResult"/> reports, one stage earlier.
/// </summary>
internal sealed class RulePrepareResult
{
    private RulePrepareResult(
        RuleUpdateOutcome outcome, int version, IReadOnlyList<RuleError> errors, IRulePublication? publication)
    {
        Outcome = outcome;
        Version = version;
        Errors = errors;
        Publication = publication;
    }

    /// <summary>The outcome kind. <see cref="RuleUpdateOutcome.Updated"/> means "prepared", not "live".</summary>
    public RuleUpdateOutcome Outcome { get; }

    /// <summary>The prepared version, or the current version on a conflict; otherwise 0.</summary>
    public int Version { get; }

    /// <summary>The binding errors on <see cref="RuleUpdateOutcome.Invalid"/>; otherwise empty.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    /// <summary>The publication to commit, or null on any outcome but a successful prepare.</summary>
    public IRulePublication? Publication { get; }

    public static RulePrepareResult Prepared(IRulePublication publication) =>
        new(RuleUpdateOutcome.Updated, publication.Version, [], publication);

    public static RulePrepareResult VersionConflict(int currentVersion) =>
        new(RuleUpdateOutcome.VersionConflict, currentVersion, [], null);

    public static RulePrepareResult Invalid(IReadOnlyList<RuleError> errors) =>
        new(RuleUpdateOutcome.Invalid, 0, errors, null);

    public static RulePrepareResult NotFound() =>
        new(RuleUpdateOutcome.NotFound, 0, [], null);

    /// <summary>
    /// The caller-facing result for a prepare that did not produce a publication. Calling this on a
    /// successful prepare would report a publish that has not happened, so it refuses.
    /// </summary>
    public RuleUpdateResult ToFailureResult() =>
        Outcome switch
        {
            RuleUpdateOutcome.VersionConflict => RuleUpdateResult.VersionConflict(Version),
            RuleUpdateOutcome.Invalid => RuleUpdateResult.Invalid(Errors),
            RuleUpdateOutcome.NotFound => RuleUpdateResult.NotFound(),
            _ => throw new InvalidOperationException(
                "A prepared publication has not been committed yet, so there is no result to report. " +
                "Commit it and report RuleUpdateResult.Updated, or call this only on a failed prepare.")
        };
}
```

- [ ] **Step 4: Replace `TryUpdate`/`TryRevert` on `RuleBase`**

In `src/Motiv.Serialization/Rules/RuleBase.cs`, replace lines 50–54:

```csharp
    /// <summary>
    /// Validates and binds the document against <paramref name="expectedVersion"/>, returning a
    /// publication that is not yet live. Binding is the fallible half; committing is not — the split
    /// is what lets the store write sit between them.
    /// </summary>
    internal abstract RulePrepareResult PrepareUpdate(
        RuleSerializer serializer, string documentJson, int expectedVersion);

    /// <summary>
    /// Binds the default against <paramref name="expectedVersion"/>, returning a publication that
    /// moves the version <em>forward</em> — a revert is a new version, never a return to an old one.
    /// </summary>
    internal abstract RulePrepareResult PrepareRevert(RuleSerializer serializer, int expectedVersion);
```

- [ ] **Step 5: Implement the split in `Rule<TModel, TMetadata>`**

In `src/Motiv.Serialization/Rules/Rule.cs`, replace `TryUpdate` and `TryRevert` (lines 76–115) with:

```csharp
    internal sealed override RulePrepareResult PrepareUpdate(
        RuleSerializer serializer, string documentJson, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RulePrepareResult.VersionConflict(current.Version);

        SpecBase<TModel, TMetadata> spec;
        try
        {
            spec = Bind(serializer, documentJson);
        }
        catch (RuleSerializationException ex)
        {
            return RulePrepareResult.Invalid(ex.Errors);
        }

        if (RequirePolicy(spec) is { } policyError)
            return RulePrepareResult.Invalid([policyError]);

        return RulePrepareResult.Prepared(
            new Publication(this, new State(documentJson, current.Version + 1, spec)));
    }

    internal sealed override RulePrepareResult PrepareRevert(RuleSerializer serializer, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RulePrepareResult.VersionConflict(current.Version);

        State @default;
        try
        {
            @default = BindDefault(serializer);
        }
        catch (RuleSerializationException ex)
        {
            return RulePrepareResult.Invalid(ex.Errors);
        }

        return RulePrepareResult.Prepared(
            new Publication(this, new State(@default.DocumentJson, current.Version + 1, @default.Spec)));
    }
```

Then replace the `Publish` method (lines 180–192) with the publication class — this is where the
`Interlocked.CompareExchange` is **deleted**:

```csharp
    /// <summary>
    /// A prepared rule change, published by swapping the state snapshot. A plain
    /// <see cref="Volatile.Write{T}"/>, not a compare-and-swap: the outer gate on
    /// <see cref="BindingScope"/> serialises whole operations, so no second writer can be in flight,
    /// and a CAS was never able to see a <em>different process</em> anyway. Enforcement lives in the
    /// store's <c>(Name, Version)</c> primary key, which can.
    /// </summary>
    private sealed class Publication(Rule<TModel, TMetadata> rule, State replacement) : IRulePublication
    {
        public int Version => replacement.Version;

        public string? DocumentJson => replacement.DocumentJson;

        public void Commit() => Volatile.Write(ref rule._state, replacement);
    }
```

- [ ] **Step 6: Implement the same split in `AsyncRule<TModel, TMetadata>`**

`src/Motiv.Serialization/Rules/AsyncRule.cs` is structurally identical; apply the same three edits,
substituting `AsyncSpecBase<TModel, TMetadata>` for `SpecBase<TModel, TMetadata>` and
`AsyncRule<TModel, TMetadata>` for `Rule<TModel, TMetadata>`. Delete its
`Interlocked.CompareExchange` too. Verify with:

```bash
grep -rn "Interlocked.CompareExchange" src/Motiv.Serialization/
```
Expected: no output.

- [ ] **Step 7: Expose the prepare seam on `RuleSet`**

In `src/Motiv.Serialization/Rules/RuleSet.cs`, replace `UpdateCore`/`RevertCore` (lines 176–181) and
`MutateCore` (lines 250–259) with:

```csharp
    /// <summary>
    /// Prepares an update without publishing it, for a caller already holding the scope lock. The
    /// caller persists the prepared version, then commits — see <see cref="CommitCore"/>. The split
    /// exists so that everything fallible runs before anything mutates.
    /// </summary>
    internal RulePrepareResult PrepareUpdateCore(string name, string documentJson, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.PrepareUpdate(_serializer, documentJson, expectedVersion)
            : RulePrepareResult.NotFound();

    /// <summary>Prepares a revert without publishing it. See <see cref="PrepareUpdateCore"/>.</summary>
    internal RulePrepareResult PrepareRevertCore(string name, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.PrepareRevert(_serializer, expectedVersion)
            : RulePrepareResult.NotFound();

    /// <summary>
    /// Commits a prepared publication and re-tracks the rule's graph edges. Cannot fail. Assumes the
    /// scope lock is held.
    /// </summary>
    internal RuleUpdateResult CommitCore(string name, IRulePublication publication)
    {
        publication.Commit();

        // Track reads the rule's *current* document, so it must run after the commit, not before.
        if (Find(name) is { } rule)
            Track(rule);

        return RuleUpdateResult.Updated(publication.Version);
    }
```

- [ ] **Step 8: Make the solution compile**

`ChangeRequestSet` still calls `UpdateCore`/`RevertCore`. Bridge it for now with two private helpers
in `ChangeRequestSet` so the whole solution builds and every existing test still passes — Task 9
replaces them with the real persist-in-between path.

Add to `src/Motiv.Serialization/Governance/ChangeRequestSet.cs`:

```csharp
    /// <summary>
    /// Prepare-then-commit with nothing in between — the behaviour <c>UpdateCore</c> had before the
    /// split. Task 9 of the durability plan replaces these with a persist step between the two halves;
    /// until then they keep the governance path byte-identical.
    /// </summary>
    private static RuleUpdateResult UpdateCore(RuleSet rules, string name, string documentJson, int baseVersion)
    {
        var prepared = rules.PrepareUpdateCore(name, documentJson, baseVersion);
        return prepared.Publication is { } publication
            ? rules.CommitCore(name, publication)
            : prepared.ToFailureResult();
    }

    private static RuleUpdateResult RevertCore(RuleSet rules, string name, int baseVersion)
    {
        var prepared = rules.PrepareRevertCore(name, baseVersion);
        return prepared.Publication is { } publication
            ? rules.CommitCore(name, publication)
            : prepared.ToFailureResult();
    }
```

and rewrite the four call sites (`ChangeRequestSet.cs:437`, `:439`, `:810`, `:811`) to use them, e.g.
`_rules.UpdateCore(change.Name, change.DocumentJson!, change.BaseVersion)` becomes
`UpdateCore(_rules, change.Name, change.DocumentJson!, change.BaseVersion)`.

Also update `RuleSet.Update`/`Revert` (lines 151–163) to route through the same shape:

```csharp
    public RuleUpdateResult Update(string name, string documentJson, int expectedVersion)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));

        return Scope.Locked(() =>
        {
            var prepared = PrepareUpdateCore(name, documentJson, expectedVersion);
            return prepared.Publication is { } publication
                ? CommitCore(name, publication)
                : prepared.ToFailureResult();
        });
    }

    public RuleUpdateResult Revert(string name, int expectedVersion) =>
        Scope.Locked(() =>
        {
            var prepared = PrepareRevertCore(name, expectedVersion);
            return prepared.Publication is { } publication
                ? CommitCore(name, publication)
                : prepared.ToFailureResult();
        });
```

- [ ] **Step 9: Run the full serialization suite to verify nothing regressed**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests src/Motiv.Serialization.AspNetCore.Tests -f net10.0
```
Expected: PASS — the 5 new `RulePreparationTests` plus every pre-existing test (682 serialization, 128 AspNetCore at time of writing). **A single regression here means the split changed behaviour and must be fixed before continuing.**

- [ ] **Step 10: Commit**

```bash
git add -A src/Motiv.Serialization src/Motiv.Serialization.Tests && git commit -m "refactor(serialization): split rule binding from publishing, and drop the in-memory CAS"
```

---

### Task 4: `RuleSet` takes a store, loads heads, and quarantines what will not bind

**Files:**
- Create: `src/Motiv.Serialization/Rules/RuleLoadReport.cs`
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs:14-71,126-132`
- Modify: `src/Motiv.Serialization/Rules/RuleSetEntry.cs`
- Test: `src/Motiv.Serialization.Tests/Rules/RuleSetLoadTests.cs`

**Interfaces:**
- Consumes: `IRuleStore`, `StoredRule` (Task 2); `PrepareUpdateCore`, `CommitCore` (Task 3).
- Produces: `RuleSet(SpecRegistry, IRuleStore?, RuleSerializerOptions?)`,
  `RuleSet(PropositionSet, IRuleStore?, RuleSerializerOptions?)`, `RuleSet.Load() → RuleLoadReport`,
  `RuleLoadReport.Quarantined`, `RuleLoadReport.ThrowIfQuarantined()`,
  `RuleSetEntry.Quarantine`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleSetLoadTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";
    private const string Unbindable = """{ "rule": { "spec": "customer.was-renamed-away" } }""";

    private static StoredRuleVersion Row(int version, string? documentJson) =>
        new("sample", version, documentJson, "alice", DateTimeOffset.UnixEpoch, null, null, "test");

    private static async Task<(RuleSet Set, SampleRule Rule, RuleLoadReport Report)> Loaded(
        params StoredRuleVersion[] rows)
    {
        var store = new InMemoryRuleStore();
        foreach (var row in rows)
            (await store.AppendAsync([row], default)).IsConflict.ShouldBeFalse();

        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new SampleRule();
        var set = new RuleSet(registry, store).Add(rule);
        return (set, rule, set.Load());
    }

    [Fact]
    public async Task Should_apply_a_stored_document_over_the_compiled_default()
    {
        // Act
        var (_, rule, report) = await Loaded(Row(2, Document));

        // Assert — a stored document always beats the compiled default
        rule.DocumentJson.ShouldBe(Document);
        rule.Version.ShouldBe(2);
        report.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_restore_the_stored_version_so_the_next_save_does_not_conflict()
    {
        // Act
        var (set, _, _) = await Loaded(Row(7, Document));

        // Assert
        set.FindEntry("sample")!.Version.ShouldBe(7);
    }

    [Fact]
    public async Task Should_apply_a_null_document_as_a_recorded_revert()
    {
        // Arrange — v2 authored, v3 reverted to code: the head is null, at version 3
        // Act
        var (set, rule, report) = await Loaded(Row(2, Document), Row(3, null));

        // Assert — back on the compiled default, but the version records that it happened
        rule.DocumentJson.ShouldBeNull();
        set.FindEntry("sample")!.Version.ShouldBe(3);
        report.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_quarantine_a_stored_document_that_no_longer_binds()
    {
        // Act — the spec the document references was renamed away by a redeploy
        var (set, _, report) = await Loaded(Row(2, Unbindable));

        // Assert — reported, never silent
        report.Quarantined.ShouldHaveSingleItem();
        report.Quarantined[0].Name.ShouldBe("sample");
        report.Quarantined[0].Errors.ShouldNotBeEmpty();
        set.FindEntry("sample")!.Quarantine.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_keep_a_quarantined_rule_evaluable_on_its_compiled_default()
    {
        // Act
        var (_, rule, _) = await Loaded(Row(2, Unbindable));

        // Assert — a rule must be able to evaluate; there is nothing else to bind
        Should.NotThrow(() => rule.Evaluate(new Customer { IsActive = true }));
    }

    [Fact]
    public async Task Should_preserve_a_quarantined_rules_stored_version_for_repair()
    {
        // Act
        var (set, _, _) = await Loaded(Row(5, Unbindable));

        // Assert — an editor repairing it must send baseVersion 5, not 1
        set.FindEntry("sample")!.Version.ShouldBe(5);
    }

    [Fact]
    public async Task Should_throw_on_demand_so_a_host_can_fail_fast()
    {
        // Arrange
        var (_, _, report) = await Loaded(Row(2, Unbindable));

        // Act / Assert — fail-fast is the host's policy, and the SDK supplies only the mechanism
        var exception = Should.Throw<RuleSerializationException>(() => report.ThrowIfQuarantined());
        exception.Message.ShouldContain("sample");
    }

    [Fact]
    public async Task Should_not_throw_when_nothing_was_quarantined()
    {
        // Arrange
        var (_, _, report) = await Loaded(Row(2, Document));

        // Act / Assert
        Should.NotThrow(() => report.ThrowIfQuarantined());
    }

    [Fact]
    public void Should_refuse_a_second_load()
    {
        // Arrange
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, new InMemoryRuleStore()).Add(new SampleRule());
        set.Load();

        // Act / Assert — Load reads the store once, at startup; a refresh is a whole rebuild
        Should.Throw<InvalidOperationException>(() => set.Load());
    }

    [Fact]
    public async Task Should_ignore_a_stored_rule_no_longer_registered_in_code()
    {
        // Arrange — a rule was deleted from the host, but its rows remain
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "retired", 1, Document, "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());

        // Act
        var report = set.Load();

        // Assert — an orphan row is not a quarantine: nothing is wrong with the document, the code
        // simply no longer declares the rule. The row is kept for history.
        report.Quarantined.ShouldBeEmpty();
        report.Orphaned.ShouldBe(["retired"]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSetLoadTests"
```
Expected: FAIL — no `RuleSet(SpecRegistry, IRuleStore)` overload and no `RuleLoadReport` (CS1729/CS0246).

- [ ] **Step 3: Create the load report**

`src/Motiv.Serialization/Rules/RuleLoadReport.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>One stored rule that was read but not applied, and why.</summary>
/// <param name="Name">The rule name.</param>
/// <param name="Version">The version the store holds, preserved so a repair can address it.</param>
/// <param name="Errors">Why the stored document did not bind.</param>
public sealed record QuarantinedRule(string Name, int Version, IReadOnlyList<RuleError> Errors);

/// <summary>
/// What <see cref="RuleSet.Load"/> found. Quarantine is deliberately not fatal here — a persisted
/// document failing to bind is an operational reality (a redeploy renames a C# spec a stored rule
/// referenced), and refusing to boot would turn a stale row into an outage.
/// </summary>
/// <remarks>
/// It is equally deliberately not <em>silent</em>. Ticket 02 rejected falling back to the compiled
/// default because a quiet revert to unapproved behaviour is indefensible under an approval gate. A
/// quarantined rule therefore stays on its default — a rule must be able to evaluate, and there is
/// nothing else to bind — but says so here, on <see cref="RuleSetEntry.Quarantine"/>, and through
/// <see cref="ThrowIfQuarantined"/> for a host whose policy is to stop.
/// </remarks>
public sealed class RuleLoadReport
{
    internal RuleLoadReport(IReadOnlyList<QuarantinedRule> quarantined, IReadOnlyList<string> orphaned)
    {
        Quarantined = quarantined;
        Orphaned = orphaned;
    }

    /// <summary>Stored rules that were read but did not bind. Empty on a clean load.</summary>
    public IReadOnlyList<QuarantinedRule> Quarantined { get; }

    /// <summary>
    /// Stored names no rule is registered under. Not a fault: the code no longer declares the rule.
    /// The rows are kept — history outlives the code that produced it — and simply not applied.
    /// </summary>
    public IReadOnlyList<string> Orphaned { get; }

    /// <summary>Whether anything was quarantined.</summary>
    public bool HasQuarantine => Quarantined.Count > 0;

    /// <summary>
    /// Stops startup when any stored rule failed to bind. The fail-fast half of the policy the SDK
    /// leaves to the host: call it to refuse a boot on stale rows, or read
    /// <see cref="Quarantined"/> and decide something else.
    /// </summary>
    /// <exception cref="RuleSerializationException">At least one stored rule was quarantined.</exception>
    public void ThrowIfQuarantined()
    {
        if (!HasQuarantine)
            return;

        var errors = Quarantined.SelectMany(rule => rule.Errors).ToArray();
        var names = string.Join(", ", Quarantined.Select(rule => $"'{rule.Name}' (v{rule.Version})"));

        throw new RuleSerializationException(
            $"{Quarantined.Count} stored rule(s) could not be bound and are quarantined: {names}. " +
            "They are running on their compiled defaults, which is not what was published — repair " +
            "or revert them, or drop ThrowIfQuarantined() to boot anyway.",
            errors);
    }
}
```

- [ ] **Step 4: Add `Quarantine` to `RuleSetEntry`**

Add the property to the record in `src/Motiv.Serialization/Rules/RuleSetEntry.cs`, defaulted so no
existing construction site breaks:

```csharp
    /// <summary>
    /// Why the stored document for this rule was not applied, or empty. Non-empty means the rule is
    /// running on its compiled default while the store holds something that would not bind.
    /// </summary>
    public IReadOnlyList<RuleError> Quarantine { get; init; } = [];
```

- [ ] **Step 5: Wire the store into `RuleSet`**

In `src/Motiv.Serialization/Rules/RuleSet.cs`, add the field and thread the store through both public
constructors and the internal one (defaulting to `InMemoryRuleStore` so every existing call site keeps
compiling — see locked decision 9):

```csharp
    private readonly IRuleStore _store;
    private readonly Dictionary<string, IReadOnlyList<RuleError>> _quarantine = new(StringComparer.Ordinal);
    private bool _loaded;
```

```csharp
    public RuleSet(SpecRegistry registry, IRuleStore? store = null, RuleSerializerOptions? options = null)
        : this(BindingScope.For(registry, ScopeClaim.Rules), store, options)
    {
    }

    public RuleSet(PropositionSet propositions, IRuleStore? store = null, RuleSerializerOptions? options = null)
        : this((propositions ?? throw new ArgumentNullException(nameof(propositions))).Scope, store, options)
    {
    }

    internal RuleSet(BindingScope scope, IRuleStore? store = null, RuleSerializerOptions? options = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _store = store ?? new InMemoryRuleStore();
        _options = options ?? new RuleSerializerOptions();
        _serializer = new RuleSerializer(scope.Source, _options);
    }
```

Add `Load`:

```csharp
    /// <summary>
    /// Reads every stored rule head and applies it over the compiled default. A document that no
    /// longer binds is <em>quarantined</em> rather than fatal: the rule keeps evaluating its compiled
    /// default, its stored version is preserved so a repair can address it, and the reason is reported
    /// on the returned <see cref="RuleLoadReport"/>.
    /// </summary>
    /// <remarks>
    /// Call once, after every <see cref="Add"/>, and after the paired <see cref="PropositionSet.Load"/>
    /// — a stored rule document may reference an authored proposition. Synchronous by design: startup
    /// is, and the DI factory wall cannot await.
    /// </remarks>
    /// <returns>What was applied, quarantined, and orphaned.</returns>
    /// <exception cref="InvalidOperationException">Load has already been called on this set.</exception>
    public RuleLoadReport Load() =>
        Scope.Locked(() =>
        {
            // Same precondition as PropositionSet.Load, for the same reason: a second pass over rows
            // that bound the first time and quarantine the second would leave the catalog reporting a
            // rule broken while the evaluator still resolved the stale binding. A refresh has to be a
            // whole rebuild, so refuse rather than half-do it.
            if (_loaded)
                throw new InvalidOperationException(
                    "Load has already been called on this RuleSet. It reads the store once, at " +
                    "startup; it is not a refresh.");

            // Set only once the store has been read: reading is the one step that can throw rather
            // than quarantine, and it mutates nothing, so an unreachable store leaves the set loadable.
            var heads = _store.Load() ?? [];
            _loaded = true;

            var quarantined = new List<QuarantinedRule>();
            var orphaned = new List<string>();

            foreach (var head in heads)
            {
                if (head?.Name is null)
                    continue;

                if (Find(head.Name) is null)
                {
                    // History outlives the code that produced it. Not a fault, and not a quarantine.
                    orphaned.Add(head.Name);
                    continue;
                }

                if (Apply(head) is { } errors)
                    quarantined.Add(new QuarantinedRule(head.Name, head.Version, errors));
            }

            return new RuleLoadReport(quarantined, orphaned);
        });

    /// <summary>
    /// Applies one stored head, returning the errors that quarantined it or null when it bound. A null
    /// document is a recorded revert, not an absent row: the rule stays on its default and only the
    /// version moves.
    /// </summary>
    private IReadOnlyList<RuleError>? Apply(StoredRule head)
    {
        var prepared = head.DocumentJson is null
            ? PrepareRevertCore(head.Name, expectedVersion: 1)
            : PrepareUpdateCore(head.Name, head.DocumentJson, expectedVersion: 1);

        if (prepared.Publication is not { } publication)
        {
            // Only Invalid can reach here: the rule was just bound by Add at version 1, so the
            // expected-version check cannot miss, and Find already ruled NotFound out.
            _quarantine[head.Name] = prepared.Errors;
            return prepared.Errors;
        }

        CommitCore(head.Name, publication);
        // The store's version is authoritative — a restart must not renumber history — so the freshly
        // committed v2 is overwritten with what was actually published.
        SetVersion(head.Name, head.Version);
        return null;
    }
```

`SetVersion` is the one piece of new plumbing: `Load` publishes through the ordinary prepare/commit
path (so a stored document binds exactly as a live edit would), which numbers the publication v2,
but the store's number is the real one. Add to `RuleBase`:

```csharp
    /// <summary>
    /// Overwrites the live version without touching the binding — used only by
    /// <see cref="RuleSet.Load"/>, to restore the number the store holds after a stored document has
    /// been bound through the ordinary publish path. Renumbering anywhere else would break the
    /// optimistic-concurrency contract.
    /// </summary>
    internal abstract void RestoreVersion(int version);
```

and in `Rule<TModel, TMetadata>` (identically in `AsyncRule<TModel, TMetadata>`):

```csharp
    internal sealed override void RestoreVersion(int version)
    {
        var current = Snapshot();
        Volatile.Write(ref _state, new State(current.DocumentJson, version, current.Spec));
    }
```

and in `RuleSet`:

```csharp
    private void SetVersion(string name, int version) => Find(name)?.RestoreVersion(version);
```

Finally, surface the quarantine on the entry — change `ToEntry` (line 126) from `static` to an
instance method and add:

```csharp
    private RuleSetEntry ToEntry(RuleBase rule)
    {
        var (version, documentJson) = rule.VersionedDocument();
        return new RuleSetEntry(
            rule.Name, rule.ModelType, rule.MetadataType, rule.IsAsync, rule.IsPolicy,
            version, rule.Description, documentJson)
        {
            Quarantine = _quarantine.TryGetValue(rule.Name, out var errors) ? errors : []
        };
    }
```

- [ ] **Step 6: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSetLoadTests"
```
Expected: PASS — 10 tests.

- [ ] **Step 7: Run the full suite**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0
```
Expected: PASS across all projects. The new optional `store` parameter sits between `registry` and
`options`, so any existing **positional** two-argument call `new RuleSet(registry, options)` now
fails to compile — fix those call sites by naming the argument: `new RuleSet(registry, options: options)`.

- [ ] **Step 8: Commit**

```bash
git add -A src/Motiv.Serialization src/Motiv.Serialization.Tests && git commit -m "feat(serialization): load rules from a store, quarantining documents that no longer bind"
```

---

### Task 5: Persist on publish — the version log, and rollback-appends

**Files:**
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs`
- Test: `src/Motiv.Serialization.Tests/Rules/RuleVersionLogTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: `RuleSet.Update(name, documentJson, expectedVersion, provenance)`,
  `RuleSet.Revert(name, expectedVersion, provenance)`,
  `RuleSet.HistoryAsync(name, ct)`, `RuleSet.RestoreAsync(name, targetVersion, expectedVersion, provenance, ct)`.

Both write methods stay synchronous in this task and become async in Task 7 — the store call is made
through `.GetAwaiter().GetResult()` here and that bridge is **deleted** in Task 7. Doing it in this
order keeps the version-log tests independent of the async refactor, so a failure in either is
unambiguous.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleVersionLogTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string V2 = """{ "rule": { "spec": "customer.is-active" } }""";
    private const string V3 = """{ "rule": { "not": { "spec": "customer.is-active" } } }""";

    private static (RuleSet Set, InMemoryRuleStore Store) Fresh()
    {
        var store = new InMemoryRuleStore();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());
        set.Load();
        return (set, store);
    }

    [Fact]
    public async Task Should_append_a_version_row_carrying_who_and_why()
    {
        // Arrange
        var (set, store) = Fresh();

        // Act
        var result = set.Update("sample", V2, 1, new RuleChangeProvenance("alice", "tighten the check"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var history = await store.HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
        history[0].Version.ShouldBe(2);
        history[0].DocumentJson.ShouldBe(V2);
        history[0].Author.ShouldBe("alice");
        history[0].ChangeNote.ShouldBe("tighten the check");
        history[0].BuildId.ShouldBe(BuildIdentity.Current);
    }

    [Fact]
    public async Task Should_append_a_null_document_row_on_a_revert()
    {
        // Arrange
        var (set, store) = Fresh();
        set.Update("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Act
        set.Revert("sample", 2, new RuleChangeProvenance("bob"));

        // Assert — the version moves forward and the row records the return to code
        var history = await store.HistoryAsync("sample", default);
        history.Select(row => row.Version).ShouldBe([2, 3]);
        history[1].DocumentJson.ShouldBeNull();
        history[1].Author.ShouldBe("bob");
    }

    [Fact]
    public void Should_leave_nothing_live_when_the_store_refuses_the_append()
    {
        // Arrange — a second replica already took version 2
        var (set, store) = Fresh();
        store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{"other":"replica"}""", "carol",
            DateTimeOffset.UnixEpoch, null, null, "test")], default).GetAwaiter().GetResult();

        // Act
        var result = set.Update("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Assert — the PK is the compare-and-set; the loser is told the current version
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
        set.FindEntry("sample")!.Version.ShouldBe(1);
        set.FindEntry("sample")!.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public void Should_not_append_anything_when_the_document_does_not_bind()
    {
        // Arrange
        var (set, store) = Fresh();

        // Act
        var result = set.Update(
            "sample", """{ "rule": { "spec": "nope" } }""", 1, new RuleChangeProvenance("alice"));

        // Assert — everything fallible runs before anything mutates, in both directions
        result.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        store.Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_restore_an_old_version_by_appending_a_copy_of_it()
    {
        // Arrange
        var (set, store) = Fresh();
        set.Update("sample", V2, 1, new RuleChangeProvenance("alice"));
        set.Update("sample", V3, 2, new RuleChangeProvenance("alice"));

        // Act — roll back to v2
        var result = await set.RestoreAsync(
            "sample", targetVersion: 2, expectedVersion: 3, new RuleChangeProvenance("bob", "rollback"), default);

        // Assert — rollback appends: restoring v2 writes v4, which also records that it happened
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        result.Version.ShouldBe(4);
        set.FindEntry("sample")!.DocumentJson.ShouldBe(V2);

        var history = await store.HistoryAsync("sample", default);
        history.Select(row => row.Version).ShouldBe([2, 3, 4]);
        history[2].DocumentJson.ShouldBe(V2);
        history[2].ChangeNote.ShouldBe("rollback");
    }

    [Fact]
    public async Task Should_refuse_to_restore_a_version_that_was_never_recorded()
    {
        // Arrange
        var (set, _) = Fresh();
        set.Update("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Act
        var result = await set.RestoreAsync("sample", 99, 2, new RuleChangeProvenance("bob"), default);

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
    }

    [Fact]
    public async Task Should_survive_a_restart_with_the_document_and_version_intact()
    {
        // Arrange
        var (set, store) = Fresh();
        set.Update("sample", V2, 1, new RuleChangeProvenance("alice"));

        // Act — a fresh RuleSet over the same store is what a restart looks like
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var restarted = new RuleSet(registry, store).Add(new SampleRule());
        var report = restarted.Load();

        // Assert — the thing an enterprise governs now survives a restart
        report.Quarantined.ShouldBeEmpty();
        restarted.FindEntry("sample")!.DocumentJson.ShouldBe(V2);
        restarted.FindEntry("sample")!.Version.ShouldBe(2);
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleVersionLogTests"
```
Expected: FAIL — `Update` takes no provenance and `RestoreAsync` does not exist (CS1501/CS1061).

- [ ] **Step 3: Persist between prepare and commit**

In `src/Motiv.Serialization/Rules/RuleSet.cs`, replace the `Update`/`Revert` bodies written in Task 3
step 8:

```csharp
    /// <summary>
    /// Replaces a rule's implementation with a document: bind → persist → publish. The live rule is
    /// untouched unless the document binds, the expected version holds, <em>and</em> the store accepts
    /// the new version row.
    /// </summary>
    /// <remarks>
    /// The ordering is the whole guarantee. Binding and persisting are the two steps that can fail and
    /// both run before anything mutates, so a broken document or a version another replica already
    /// took leaves nothing live — there is no rollback here because none is needed.
    /// </remarks>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="provenance">Who is publishing, and why. Written into the version log.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public RuleUpdateResult Update(
        string name, string documentJson, int expectedVersion, RuleChangeProvenance provenance)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        return Scope.Locked(() =>
            PersistAndCommit(name, PrepareUpdateCore(name, documentJson, expectedVersion), provenance));
    }

    /// <summary>
    /// Reverts a rule to its default. The version moves forward, never back, and the log records that
    /// the rule went back to code.
    /// </summary>
    public RuleUpdateResult Revert(string name, int expectedVersion, RuleChangeProvenance provenance)
    {
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        return Scope.Locked(() =>
            PersistAndCommit(name, PrepareRevertCore(name, expectedVersion), provenance));
    }

    /// <summary>
    /// Restores the document a previous version carried by <em>appending</em> a copy of it. Restoring
    /// v5 writes v9 — history is never rewritten, and the new row is itself the record that a rollback
    /// happened.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="targetVersion">The version whose document to republish.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="provenance">Who is rolling back, and why.</param>
    /// <param name="cancellationToken">Cancels the history read.</param>
    /// <returns>
    /// The outcome. <see cref="RuleUpdateOutcome.NotFound"/> when the rule or the target version is
    /// unknown — a version that was never recorded cannot be restored.
    /// </returns>
    public async Task<RuleUpdateResult> RestoreAsync(
        string name, int targetVersion, int expectedVersion, RuleChangeProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        // Read history outside the lock: it is a store read that cannot affect anything live, and
        // holding an exclusion gate across it would serialise publishes behind an I/O round trip.
        var history = await _store.HistoryAsync(name, cancellationToken).ConfigureAwait(false);
        var target = history.FirstOrDefault(row => row.Version == targetVersion);
        if (target is null)
            return RuleUpdateResult.NotFound();

        return target.DocumentJson is null
            ? Revert(name, expectedVersion, provenance)
            : Update(name, target.DocumentJson, expectedVersion, provenance);
    }

    /// <summary>Every recorded version of one rule, oldest first.</summary>
    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken = default) =>
        _store.HistoryAsync(name, cancellationToken);

    /// <summary>
    /// The middle of bind → persist → publish. Assumes the scope lock is held.
    /// </summary>
    /// <remarks>
    /// The store call blocks here rather than awaiting: the write path is still synchronous at this
    /// point in the build sequence and this bridge is removed when it goes async.
    /// </remarks>
    private RuleUpdateResult PersistAndCommit(
        string name, RulePrepareResult prepared, RuleChangeProvenance provenance)
    {
        if (prepared.Publication is not { } publication)
            return prepared.ToFailureResult();

        var appended = _store
            .AppendAsync([RowFor(name, publication, provenance)], CancellationToken.None)
            .GetAwaiter().GetResult();

        if (appended.IsConflict)
            return RuleUpdateResult.VersionConflict(appended.CurrentVersion);

        // Nothing below can fail.
        _quarantine.Remove(name);
        return CommitCore(name, publication);
    }

    private static StoredRuleVersion RowFor(
        string name, IRulePublication publication, RuleChangeProvenance provenance)
    {
        var stamped = provenance.WithDefaults();
        return new StoredRuleVersion(
            name, publication.Version, publication.DocumentJson,
            stamped.Author, DateTimeOffset.UtcNow,
            stamped.ChangeNote, stamped.ApprovalRef, stamped.BuildId);
    }
```

Also update `Load`'s `Apply` (Task 4) to bypass persistence — a row that came *from* the store must
not be written back to it. Replace its `CommitCore(head.Name, publication);` call with a comment
making that explicit:

```csharp
        // Committed directly, not through PersistAndCommit: this document came from the store, so
        // appending it again would mint a duplicate version row and immediately conflict on the PK.
        CommitCore(head.Name, publication);
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleVersionLogTests"
```
Expected: PASS — 7 tests.

- [ ] **Step 5: Fix every caller of the old two-argument `Update`/`Revert`**

`ChangeRequestSet` and `MotivRulesEndpoints` call them. Pass `RuleChangeProvenance.System` at the
governance bridges added in Task 3 step 8 for now; Task 9 replaces those with the change request's
real author and approval reference.

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet build Motiv.slnx -f net10.0
```
Expected: clean build.

- [ ] **Step 6: Run the full solution suite**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0
```
Expected: PASS across all 10 projects.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(serialization): persist every rule publish to an append-only version log"
```

- [ ] **Step 8: Phase 1 code review**

Spawn a `code-simplifier` agent over the files changed in Tasks 1–5 (per `CLAUDE.md`). Apply its
findings, re-run `dotnet test Motiv.slnx -f net10.0`, and commit any changes separately.

---

# Phase 2 — The async write contract

Two-tier exclusion, then the ripple. Nothing in Phase 1's behaviour changes; only signatures do.

---

### Task 6: The outer `SemaphoreSlim` on `BindingScope`

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/BindingScope.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/BindingScopeExclusionTests.cs`

**Interfaces:**
- Produces: `BindingScope.LockedAsync<T>(Func<Task<T>>, CancellationToken)`,
  `BindingScope.LockedAsync(Func<Task>, CancellationToken)`. `Locked<T>`/`Locked` are unchanged.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class BindingScopeExclusionTests
{
    private static BindingScope Scope() => new(new SpecRegistry());

    [Fact]
    public async Task Should_serialise_whole_operations_across_awaits()
    {
        // Arrange — the inner Monitor cannot do this: it is released at the first await
        var scope = Scope();
        var observed = new List<string>();

        async Task Operation(string id)
        {
            await scope.LockedAsync(async () =>
            {
                observed.Add($"{id}-enter");
                await Task.Yield();
                await Task.Delay(20);
                observed.Add($"{id}-exit");
            }, default);
        }

        // Act
        await Task.WhenAll(Operation("a"), Operation("b"));

        // Assert — neither operation may interleave with the other
        observed.ShouldBeOneOf(
            ["a-enter", "a-exit", "b-enter", "b-exit"],
            ["b-enter", "b-exit", "a-enter", "a-exit"]);
    }

    [Fact]
    public async Task Should_cancel_a_waiter_rather_than_hang_behind_a_stuck_store()
    {
        // Arrange — this is why the write path is async: a hung store must be escapable
        var scope = Scope();
        var held = new TaskCompletionSource<bool>();
        var entered = new TaskCompletionSource<bool>();

        var holder = scope.LockedAsync(async () =>
        {
            entered.SetResult(true);
            await held.Task;
        }, default);

        await entered.Task;
        using var cancellation = new CancellationTokenSource();

        // Act
        var waiter = scope.LockedAsync(() => Task.CompletedTask, cancellation.Token);
        cancellation.Cancel();

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => await waiter);

        held.SetResult(true);
        await holder;
    }

    [Fact]
    public async Task Should_release_the_gate_when_the_operation_throws()
    {
        // Arrange
        var scope = Scope();

        // Act
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await scope.LockedAsync<int>(() => throw new InvalidOperationException("boom"), default));

        // Assert — a failed publish must not wedge every later one
        var reentered = await scope.LockedAsync(() => Task.FromResult(42), default);
        reentered.ShouldBe(42);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~BindingScopeExclusionTests"
```
Expected: FAIL — `LockedAsync` does not exist (CS1061).

- [ ] **Step 3: Add the outer tier**

In `src/Motiv.Serialization/Propositions/BindingScope.cs`, add the field and the two methods, and
extend the class remarks:

```csharp
    /// <summary>
    /// The outer tier: serialises whole publish operations await-safely. The inner
    /// <see cref="_gate"/> monitor is deliberately left in place rather than replaced — every
    /// <see cref="Enrol"/>/<see cref="Withdraw"/> site is reentrant, and a pure swap to a
    /// non-reentrant semaphore would self-deadlock at startup.
    /// </summary>
    /// <remarks>
    /// <strong>Acquired only at public entry points.</strong> <see cref="SemaphoreSlim"/> is not
    /// reentrant, so anything already inside must call a <c>…Core</c> method, never a public one.
    /// </remarks>
    private readonly SemaphoreSlim _outer = new(1, 1);
```

```csharp
    /// <summary>
    /// Runs an operation holding the outer write gate, so a whole publish — including its store
    /// round trip — serialises against every other publish.
    /// </summary>
    /// <remarks>
    /// The reason this exists is <em>cancellation</em>, not throughput: the critical section is
    /// mostly CPU, so awaiting frees a few milliseconds at most. What it buys is
    /// <see cref="SemaphoreSlim.WaitAsync(CancellationToken)"/> — an answer to a store that has
    /// stopped responding, which a monitor cannot give.
    /// </remarks>
    public async Task<T> LockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _outer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _outer.Release();
        }
    }

    /// <summary>The void companion to <see cref="LockedAsync{T}"/>.</summary>
    public async Task LockedAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await _outer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _outer.Release();
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~BindingScopeExclusionTests"
```
Expected: PASS — 3 tests.

- [ ] **Step 5: Commit**

```bash
git add -A src/Motiv.Serialization/Propositions/BindingScope.cs src/Motiv.Serialization.Tests/Propositions/BindingScopeExclusionTests.cs && git commit -m "feat(serialization): add the outer await-safe exclusion tier to BindingScope"
```

---

### Task 7: `RuleSet` write path goes async

**Files:**
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs`
- Modify: `src/Motiv.Serialization.Tests/Rules/RuleVersionLogTests.cs` (await the calls)
- Test: `src/Motiv.Serialization.Tests/Rules/RuleSetAsyncWriteTests.cs`

**Interfaces:**
- Produces: `RuleSet.UpdateAsync(name, documentJson, expectedVersion, provenance, ct)`,
  `RuleSet.RevertAsync(name, expectedVersion, provenance, ct)`,
  `internal RuleSet.PersistAndCommitCoreAsync(name, RulePrepareResult, provenance, ct)`.
  **`Update` and `Revert` are deleted** — no shims (ticket 06: never published).

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleSetAsyncWriteTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>A store that blocks inside AppendAsync until released — a hung database.</summary>
    private sealed class StallingRuleStore : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();
        public TaskCompletionSource<bool> Released { get; } = new();
        public TaskCompletionSource<bool> Entered { get; } = new();

        public IReadOnlyList<StoredRule> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);
        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            _inner.HistoryAsync(name, ct);

        public async Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct)
        {
            Entered.TrySetResult(true);
            await Released.Task;
            return await _inner.AppendAsync(versions, ct);
        }
    }

    private static (RuleSet Set, IRuleStore Store) Fresh(IRuleStore? store = null)
    {
        store ??= new InMemoryRuleStore();
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());
        set.Load();
        return (set, store);
    }

    [Fact]
    public async Task Should_publish_through_the_async_path()
    {
        // Arrange
        var (set, _) = Fresh();

        // Act
        var result = await set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Assert
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        set.FindEntry("sample")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_cancel_a_write_waiting_behind_a_stuck_store()
    {
        // Arrange — the cancellation this whole refactor exists for
        var store = new StallingRuleStore();
        var (set, _) = Fresh(store);

        var stuck = set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));
        await store.Entered.Task;

        using var cancellation = new CancellationTokenSource();

        // Act
        var queued = set.UpdateAsync(
            "sample", Document, 1, new RuleChangeProvenance("bob"), cancellation.Token);
        cancellation.Cancel();

        // Assert — the second writer escapes rather than hanging forever
        await Should.ThrowAsync<OperationCanceledException>(async () => await queued);

        store.Released.SetResult(true);
        (await stuck).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
    }

    [Fact]
    public async Task Should_serialise_two_concurrent_writers_into_one_winner()
    {
        // Arrange
        var (set, _) = Fresh();

        // Act — both hold baseVersion 1
        var results = await Task.WhenAll(
            set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice")),
            set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob")));

        // Assert — one publishes, one is told the current version
        results.Count(r => r.Outcome == RuleUpdateOutcome.Updated).ShouldBe(1);
        results.Count(r => r.Outcome == RuleUpdateOutcome.VersionConflict).ShouldBe(1);
        results.Single(r => r.Outcome == RuleUpdateOutcome.VersionConflict).Version.ShouldBe(2);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSetAsyncWriteTests"
```
Expected: FAIL — `UpdateAsync` does not exist (CS1061).

- [ ] **Step 3: Convert the write path**

In `src/Motiv.Serialization/Rules/RuleSet.cs`, replace `Update`, `Revert` and `PersistAndCommit`:

```csharp
    /// <summary>
    /// Replaces a rule's implementation with a document: bind → persist → publish, under the outer
    /// write gate. The live rule is untouched unless the document binds, the expected version holds,
    /// and the store accepts the new version row.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <param name="provenance">Who is publishing, and why. Written into the version log.</param>
    /// <param name="cancellationToken">Cancels while waiting for the gate or the store.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public Task<RuleUpdateResult> UpdateAsync(
        string name, string documentJson, int expectedVersion, RuleChangeProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        return Scope.LockedAsync(
            () => PersistAndCommitCoreAsync(
                name, PrepareUpdateCore(name, documentJson, expectedVersion), provenance, cancellationToken),
            cancellationToken);
    }

    /// <summary>Reverts a rule to its default. The version moves forward, never back.</summary>
    public Task<RuleUpdateResult> RevertAsync(
        string name, int expectedVersion, RuleChangeProvenance provenance,
        CancellationToken cancellationToken = default)
    {
        if (provenance is null) throw new ArgumentNullException(nameof(provenance));

        return Scope.LockedAsync(
            () => PersistAndCommitCoreAsync(
                name, PrepareRevertCore(name, expectedVersion), provenance, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// The middle of bind → persist → publish, for a caller already holding the outer gate. The store
    /// call is the last step that can fail; everything after it is a memory swap that cannot.
    /// </summary>
    internal async Task<RuleUpdateResult> PersistAndCommitCoreAsync(
        string name, RulePrepareResult prepared, RuleChangeProvenance provenance,
        CancellationToken cancellationToken)
    {
        if (prepared.Publication is not { } publication)
            return prepared.ToFailureResult();

        var appended = await _store
            .AppendAsync([RowFor(name, publication, provenance)], cancellationToken)
            .ConfigureAwait(false);

        if (appended.IsConflict)
            return RuleUpdateResult.VersionConflict(appended.CurrentVersion);

        _quarantine.Remove(name);
        return CommitCore(name, publication);
    }
```

Update `RestoreAsync` to call `UpdateAsync`/`RevertAsync` and thread its `cancellationToken`.

- [ ] **Step 4: Update `RuleVersionLogTests` to await**

Change every `set.Update(…)`/`set.Revert(…)` in `RuleVersionLogTests` to
`await set.UpdateAsync(…)`/`await set.RevertAsync(…)`, and change the affected `[Fact] public void`
signatures to `[Fact] public async Task`. The assertions are unchanged — that is the point of having
written them in Phase 1.

- [ ] **Step 5: Run both test classes to verify they pass**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSetAsyncWriteTests|FullyQualifiedName~RuleVersionLogTests"
```
Expected: PASS — 3 + 7 tests.

- [ ] **Step 6: Commit**

```bash
git add -A src/Motiv.Serialization/Rules src/Motiv.Serialization.Tests/Rules && git commit -m "feat(serialization): make the rule write path async and cancellable"
```

---

### Task 8: `IPropositionStore` and `PropositionSet` go async

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/IPropositionStore.cs`
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs:155-286,606-637`
- Modify: `src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/PropositionSetAsyncWriteTests.cs`

**Interfaces:**
- Produces: `PropositionBatch(IReadOnlyList<StoredProposition> Saves, IReadOnlyList<string> Deletes)`;
  `IPropositionStore.WriteAsync(PropositionBatch, ct)` **replacing** `Save`/`Delete` (`Load` stays);
  `PropositionSet.CreateAsync/UpdateAsync/WithdrawAsync(..., ct)` **replacing** `Create`/`Update`/`Withdraw`;
  `internal PropositionSet.PersistCoreAsync(PropositionBatch, ct)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetAsyncWriteTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>A store that refuses every write — the "persist failed" arm.</summary>
    private sealed class FailingPropositionStore : IPropositionStore
    {
        public IReadOnlyList<StoredProposition> Load() => [];

        public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken) =>
            throw new IOException("disk full");
    }

    private static PropositionSet Fresh(IPropositionStore? store = null)
    {
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store ?? new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        set.Load();
        return set;
    }

    [Fact]
    public async Task Should_create_through_the_async_path()
    {
        // Arrange
        var set = Fresh();

        // Act
        var result = await set.CreateAsync("customer.a", "customer", Document, null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        set.Find("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_leave_nothing_live_when_the_store_refuses_the_write()
    {
        // Arrange
        var set = Fresh(new FailingPropositionStore());

        // Act
        await Should.ThrowAsync<IOException>(async () =>
            await set.CreateAsync("customer.a", "customer", Document, null));

        // Assert — persist runs before any memory mutation, so a failure leaves nothing behind
        set.Find("customer.a").ShouldBeNull();
        set.DocumentJsonOf("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_withdraw_through_the_async_path()
    {
        // Arrange
        var set = Fresh();
        await set.CreateAsync("customer.a", "customer", Document, null);

        // Act
        var result = await set.WithdrawAsync("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_serialise_a_proposition_write_against_a_rule_write()
    {
        // Arrange — the two share one BindingScope, so they share one outer gate
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var propositions = new PropositionSet(scope, new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        propositions.Load();

        var rules = new RuleSet(scope, new InMemoryRuleStore());

        // Act — a rule write and a proposition write launched together must not interleave
        var results = await Task.WhenAll(
            propositions.CreateAsync("customer.a", "customer", Document, null)
                .ContinueWith(t => t.Result.Outcome.ToString()),
            propositions.CreateAsync("customer.b", "customer", Document, null)
                .ContinueWith(t => t.Result.Outcome.ToString()));

        // Assert
        results.ShouldAllBe(outcome => outcome == "Created");
        rules.Rules.ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetAsyncWriteTests"
```
Expected: FAIL — `WriteAsync`/`CreateAsync` do not exist (CS0535/CS1061).

- [ ] **Step 3: Convert `IPropositionStore`**

Replace the interface and the in-memory implementation in
`src/Motiv.Serialization/Propositions/IPropositionStore.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One store round trip: everything a single publish changes. Batched rather than per-row because a
/// governed envelope publishes several propositions at once and must not be able to land half-way —
/// a failure point after the first row had been written would break "a failed persist leaves nothing
/// live".
/// </summary>
/// <param name="Saves">Propositions to write, replacing any existing row of the same name.</param>
/// <param name="Deletes">Names to remove. Absent names are not an error.</param>
public sealed record PropositionBatch(
    IReadOnlyList<StoredProposition> Saves, IReadOnlyList<string> Deletes)
{
    /// <summary>A batch that writes one proposition and removes nothing.</summary>
    public static PropositionBatch Save(StoredProposition proposition) => new([proposition], []);

    /// <summary>A batch that removes one name and writes nothing.</summary>
    public static PropositionBatch Delete(string name) => new([], [name]);
}

/// <summary>
/// Where authored propositions are kept between restarts — the twin of <see cref="IRuleStore"/>. The
/// two are never written in the same transaction: each coordinates independently.
/// </summary>
/// <remarks>
/// A store is a dumb sink — it validates nothing and enforces no invariant. Legality is decided by
/// <see cref="PropositionSet"/> before anything reaches here.
/// </remarks>
public interface IPropositionStore
{
    /// <summary>Every persisted proposition, read once at startup. Synchronous because startup is.</summary>
    IReadOnlyList<StoredProposition> Load();

    /// <summary>
    /// Applies a batch — all of it, or none. Called under the publish gate with a cancellation token,
    /// so a store that stops responding can be escaped rather than waited on forever.
    /// </summary>
    Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken);
}

/// <summary>The default store: propositions live for the lifetime of the process, as rules do.</summary>
public sealed class InMemoryPropositionStore : IPropositionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StoredProposition> _propositions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<StoredProposition> Load()
    {
        lock (_gate)
            return [.. _propositions.Values];
    }

    /// <inheritdoc />
    public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var proposition in batch.Saves)
                _propositions[proposition.Name] = proposition;

            foreach (var name in batch.Deletes)
                _propositions.Remove(name);
        }

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Convert `PropositionSet`'s write path**

`Create`, `Update`, `Withdraw` become `CreateAsync`, `UpdateAsync`, `WithdrawAsync`, each
`Scope.LockedAsync(…, cancellationToken)`. `CreateCore`, `UpdateCore`, `WithdrawCore` stay
synchronous **prepare-only** methods and gain an async persist step, exactly as rules did.

The key structural change is in `Publish` (line 629) and `WithdrawCore`'s store call (line 275): both
become an awaited `WriteAsync` that runs *before* any in-memory mutation. Split `Publish`:

```csharp
    /// <summary>
    /// Publishes an authored proposition: store first, then overlay, graph and participant. The store
    /// runs first and is the only step that can fail — none of the in-memory mutations can — so a
    /// store failure leaves nothing live behind it, keeping "all of it, or none" true even though
    /// there is no explicit rollback.
    /// </summary>
    private async Task PublishAsync(Authored authored, CancellationToken cancellationToken)
    {
        await _store.WriteAsync(
            PropositionBatch.Save(new StoredProposition(
                authored.Name, authored.ModelTypeId, authored.DocumentJson,
                authored.Version, authored.Description)),
            cancellationToken).ConfigureAwait(false);

        CommitPublish(authored);
    }

    /// <summary>The infallible half of <see cref="PublishAsync"/>, for a caller that persisted already.</summary>
    internal void CommitPublish(Authored authored)
    {
        _authored[authored.Name] = authored;
        Scope.Overlay.Set(authored.Bound!);
        Scope.Graph.Set(authored.Node, authored.References);
        Scope.Enrol(authored);
    }
```

`Authored` must become `internal` rather than `private` for `ChangeRequestSet` to drive it in Task 9.

- [ ] **Step 5: Update `JsonFilePropositionStore`**

In `src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`, collapse `Save`/`Delete` into
one `WriteAsync` that applies both lists in a single rewrite — which is strictly better than before,
since an envelope now costs one file write rather than N:

```csharp
    /// <inheritdoc />
    public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var deleted = new HashSet<string>(batch.Deletes, StringComparer.Ordinal);
            var saved = batch.Saves.ToDictionary(p => p.Name, StringComparer.Ordinal);

            var propositions = ReadAll()
                .Where(existing => !deleted.Contains(existing.Name) && !saved.ContainsKey(existing.Name))
                .Concat(batch.Saves)
                .ToList();

            Write(propositions);
        }

        return Task.CompletedTask;
    }
```

- [ ] **Step 6: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionSetAsyncWriteTests"
```
Expected: PASS — 4 tests.

- [ ] **Step 7: Update every existing proposition test**

`PropositionSetCreateTests`, `PropositionSetLoadTests`, `PublicHostingTests` and the AspNetCore tests
call `Create`/`Update`/`Withdraw` and construct stores. Convert each to `await …Async`, and change
`[Fact] public void` to `[Fact] public async Task` where needed. The assertions do not change.

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0
```
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat(serialization): make the proposition write path async and batched"
```

---

### Task 9: The governed publish persists before it applies

**Files:**
- Modify: `src/Motiv.Serialization/Governance/ChangeRequestSet.cs:400-460,780-830`
- Test: `src/Motiv.Serialization.Tests/Governance/GovernedPublishOrderingTests.cs`

**Interfaces:**
- Consumes: `RuleSet.PrepareUpdateCore/PrepareRevertCore/CommitCore` (Task 3),
  `IRuleStore.AppendAsync` (Task 2), `PropositionSet.CommitPublish` (Task 8).
- Produces: `ChangeRequestSet.PublishAsync(..., ct)` and `ChangeRequestSet.DirectWriteAsync(..., ct)`
  **replacing** their synchronous forms. The envelope's author and id become the version rows'
  `Author` and `ApprovalRef`.

The existing `ApplyValidated` throws on any non-`Updated` outcome because everything was validated
first. Persistence must therefore move *ahead* of it, so the three-phase shape becomes
**validate all → persist all → apply all**.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Governance;

public class GovernedPublishOrderingTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ARule() : Rule<Customer, string>("a", IsActive);
    private sealed class BRule() : Rule<Customer, string>("b", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>Refuses the Nth append, so the envelope's persist phase fails part-way.</summary>
    private sealed class RefusingRuleStore(int refuseAfter) : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();
        private int _appends;

        public IReadOnlyList<StoredRule> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);
        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string n, CancellationToken ct) =>
            _inner.HistoryAsync(n, ct);

        public Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            ++_appends > refuseAfter
                ? Task.FromResult(RuleAppendResult.Conflict(versions[0].Name, 99))
                : _inner.AppendAsync(versions, ct);
    }

    [Fact]
    public async Task Should_write_one_batch_for_a_whole_envelope()
    {
        // Arrange — two rules in one change request must persist as one store call, not two
        var store = new RefusingRuleStore(refuseAfter: 1);
        var (governance, rules) = Harness(store);

        // Act
        var result = await governance.PublishAsync(
            Envelope(("a", Document), ("b", Document)), default);

        // Assert — one batch means one append, so refuseAfter: 1 lets it through
        result.Outcome.ShouldBe(ChangeRequestOutcome.Ok);
        rules.FindEntry("a")!.Version.ShouldBe(2);
        rules.FindEntry("b")!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_leave_the_whole_envelope_unpublished_when_the_persist_is_refused()
    {
        // Arrange — refuse the very first append
        var store = new RefusingRuleStore(refuseAfter: 0);
        var (governance, rules) = Harness(store);

        // Act
        var result = await governance.PublishAsync(
            Envelope(("a", Document), ("b", Document)), default);

        // Assert — nothing live, and no exception: a conflict is an expected outcome
        result.Outcome.ShouldBe(ChangeRequestOutcome.VersionConflict);
        rules.FindEntry("a")!.Version.ShouldBe(1);
        rules.FindEntry("b")!.Version.ShouldBe(1);
        rules.FindEntry("a")!.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_stamp_the_change_request_as_the_approval_reference()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (governance, _) = Harness(store);
        var envelope = Envelope(("a", Document));

        // Act
        await governance.PublishAsync(envelope, default);

        // Assert — the audit trail must connect the row to the request that authorised it
        var history = await store.HistoryAsync("a", default);
        history.ShouldHaveSingleItem();
        history[0].ApprovalRef.ShouldBe(envelope.Id.ToString());
        history[0].Author.ShouldBe(envelope.Author);
    }
}
```

> **Note for the implementer:** `Harness(...)` and `Envelope(...)` are helpers you write against the
> `ChangeRequestSet` construction already used in `src/Motiv.Serialization.Tests/Governance/` — copy
> the setup from `ApprovalGateTests` and the change-request builders from the existing governance
> tests rather than inventing new ones. The outcome enum member is `ChangeRequestOutcome.VersionConflict`
> (`ChangeRequestSet.cs:102`); results are built through the private `Failure(...)` helper
> (`ChangeRequestSet.cs:518`), which takes `conflictVersion` as a named optional argument. There is no
> `ChangeRequestResult.Conflict` factory — do not invent one.

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~GovernedPublishOrderingTests"
```
Expected: FAIL — `PublishAsync` does not exist.

- [ ] **Step 3: Restructure `ApplyValidated` into three phases**

Replace `ApplyValidated` with an async three-phase method. The rule half prepares every publication
first, appends them as one batch, then commits:

```csharp
        /// <summary>
        /// Validate all → persist all → apply all. The middle phase is why this is not just a loop:
        /// every rule row goes to the store as a single batch, so an envelope cannot land half-way,
        /// and the commit phase below is unable to fail.
        /// </summary>
        private static async Task<ChangeRequestResult> ApplyValidatedAsync(
            RuleSet rules, PropositionSet? propositions, ChangeRequest change,
            CancellationToken cancellationToken)
        {
            var versions = new Dictionary<string, int>(StringComparer.Ordinal);
            var provenance = new RuleChangeProvenance(
                change.Author, change.Reason, ApprovalRef: change.Id.ToString());

            // --- Prepare every rule change, publishing none of them.
            var prepared = new List<(string Name, IRulePublication Publication)>();
            foreach (var proposed in change.ProposedChanges)
            {
                if (proposed.Target.Kind != ChangeTargetKind.Rule)
                    continue;

                var name = proposed.Target.Name;
                var result = proposed.ProposedDocumentJson is null
                    ? rules.PrepareRevertCore(name, proposed.BaseVersion)
                    : rules.PrepareUpdateCore(name, proposed.ProposedDocumentJson, proposed.BaseVersion);

                if (result.Publication is not { } publication)
                {
                    // A conflict here is an expected outcome, not a bug: another replica moved the
                    // rule between this envelope's validation and its publish. Reported through the
                    // existing Failure(...) helper — there is no dedicated Conflict factory, and
                    // Mismatch(...) is the validation-phase path, not this one.
                    if (result.Outcome == RuleUpdateOutcome.VersionConflict)
                        return Failure(
                            ChangeRequestOutcome.VersionConflict, change, proposed.Target,
                            conflictVersion: result.Version);

                    throw Unexpected(proposed.Target, result.Outcome.ToString(),
                        string.Join("; ", result.Errors));
                }

                prepared.Add((name, publication));
            }

            // --- Persist the whole rule half as one batch. The last step that can fail.
            if (prepared.Count > 0)
            {
                var appended = await rules
                    .AppendCoreAsync(prepared, provenance, cancellationToken)
                    .ConfigureAwait(false);

                if (appended.IsConflict)
                    return Failure(
                        ChangeRequestOutcome.VersionConflict, change,
                        new ChangeTarget(ChangeTargetKind.Rule, appended.Name!),
                        conflictVersion: appended.CurrentVersion);
            }

            // --- Propositions, then rule commits, then withdrawals. Nothing below may fail.
            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: false))
            {
                var name = proposed.Target.Name;
                var state = propositions!.AuthoredStateCore(name);
                var result = state.Exists
                    ? await propositions.UpdateCoreAsync(
                        name, proposed.ProposedDocumentJson!, proposed.BaseVersion, cancellationToken)
                        .ConfigureAwait(false)
                    : await propositions.CreateCoreAsync(
                        name, proposed.ModelTypeId!, proposed.ProposedDocumentJson!, proposed.Description,
                        cancellationToken).ConfigureAwait(false);

                if (result.Outcome is not (PropositionUpdateOutcome.Created or PropositionUpdateOutcome.Updated))
                    throw Unexpected(proposed.Target, result.Outcome.ToString(), Detail(result));

                versions[name] = result.Version;
            }

            foreach (var (name, publication) in prepared)
                versions[name] = rules.CommitCore(name, publication).Version;

            foreach (var proposed in Ordered(change, ChangeTargetKind.Proposition, deletions: true))
            {
                var result = await propositions!
                    .WithdrawCoreAsync(proposed.Target.Name, proposed.BaseVersion, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Outcome != PropositionUpdateOutcome.Removed)
                    throw Unexpected(proposed.Target, result.Outcome.ToString(), Detail(result));

                versions[proposed.Target.Name] = 0;
            }

            return new ChangeRequestResult(ChangeRequestOutcome.Ok, change, null, [], null, null, versions);
        }
```

Add the batch helper to `RuleSet`:

```csharp
    /// <summary>
    /// Appends version rows for several prepared publications as one store call, so a governed
    /// envelope's rule half lands all-or-nothing. Assumes the outer gate is held; commits nothing.
    /// </summary>
    internal Task<RuleAppendResult> AppendCoreAsync(
        IReadOnlyList<(string Name, IRulePublication Publication)> prepared,
        RuleChangeProvenance provenance, CancellationToken cancellationToken) =>
        _store.AppendAsync(
            [.. prepared.Select(entry => RowFor(entry.Name, entry.Publication, provenance))],
            cancellationToken);
```

Apply the same prepare→persist→commit shape to `DirectWrite`, renamed `DirectWriteAsync`, and make
every enclosing method async up to the public surface, replacing `Scope.Locked` with
`Scope.LockedAsync`.

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~GovernedPublishOrderingTests"
```
Expected: PASS — 3 tests.

- [ ] **Step 5: Run the governance suite**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~Governance"
```
Expected: PASS — every pre-existing governance test, converted to `await`, still green. **The six
spec-1 invariants must all still hold** (no live-rule endpoint; the gate never governs itself; the
last-administer invariant; fail-closed dev identity/grants/break-glass; audit-stamped break-glass
publishes; no gate bypass on any write surface).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(governance): persist a whole envelope before applying any of it"
```

---

### Task 10: Endpoints and DI

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs:336-380`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/RuleStoreWiringTests.cs`

**Interfaces:**
- Produces: `MotivRulesBuilder.AddRuleStore(IRuleStore? store = null, bool failFastOnQuarantine = true)`.
  Handlers become `async` and pass a `RuleChangeProvenance` built from the caller's
  `ClaimsPrincipal` and `HttpContext.RequestAborted`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.AspNetCore.Tests;

public class RuleStoreWiringTests
{
    [Fact]
    public async Task Should_survive_a_restart_when_a_rule_store_is_registered()
    {
        // Arrange — one store, two app lifetimes
        var store = new InMemoryRuleStore();
        var document = """{ "rule": { "spec": "customer.is-active" } }""";

        await using (var first = TestApp.Create(builder => builder.AddRuleStore(store)))
        {
            var response = await first.Client.PutAsJsonAsync(
                "/api/rules/rules/sample", new { documentJson = document, baseVersion = 1 });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act
        await using var second = TestApp.Create(builder => builder.AddRuleStore(store));
        var reloaded = await second.Client.GetFromJsonAsync<JsonElement>("/api/rules/rules/sample");

        // Assert
        reloaded.GetProperty("version").GetInt32().ShouldBe(2);
        reloaded.GetProperty("documentJson").GetString().ShouldBe(document);
    }

    [Fact]
    public async Task Should_refuse_startup_when_a_stored_rule_no_longer_binds()
    {
        // Arrange — a redeploy renamed the spec the stored document referenced
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{ "rule": { "spec": "customer.was-renamed-away" } }""",
            "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        // Act / Assert — fail-fast is the default: a silent revert to unapproved behaviour is worse
        var exception = Should.Throw<RuleSerializationException>(() =>
            TestApp.Create(builder => builder.AddRuleStore(store)));
        exception.Message.ShouldContain("quarantined");
    }

    [Fact]
    public async Task Should_boot_with_the_quarantine_reported_when_fail_fast_is_off()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{ "rule": { "spec": "customer.was-renamed-away" } }""",
            "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        // Act
        await using var app = TestApp.Create(
            builder => builder.AddRuleStore(store, failFastOnQuarantine: false));
        var listed = await app.Client.GetFromJsonAsync<JsonElement>("/api/rules/rules/sample");

        // Assert — booted, but the catalog says so rather than pretending the default is what was published
        listed.GetProperty("quarantine").GetArrayLength().ShouldBeGreaterThan(0);
    }
}
```

> **Note for the implementer:** `TestApp` already exists at
> `src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs` (added by spec 1). Extend its factory to take
> a `MotivRulesBuilder` callback rather than writing a second harness, and reuse its existing
> registration of `customer.is-active` and the `sample` rule.

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~RuleStoreWiringTests"
```
Expected: FAIL — `AddRuleStore` does not exist.

- [ ] **Step 3: Add `AddRuleStore` and load at startup**

In `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`, add to
`MotivRulesBuilder`:

```csharp
    /// <summary>
    /// Registers where rules persist, and loads them when the <see cref="RuleSet"/> is first resolved.
    /// Without this, rules live for the lifetime of the process, as they always have.
    /// </summary>
    /// <param name="store">The store, or null for <see cref="InMemoryRuleStore"/>.</param>
    /// <param name="failFastOnQuarantine">
    /// Whether a stored document that no longer binds should stop startup. Defaults to <c>true</c>:
    /// a quarantined rule is running its compiled default, which is <em>not what was published</em>,
    /// and under an approval gate booting quietly into unapproved behaviour is the worse failure. Set
    /// false to boot anyway and read the quarantine from the catalog.
    /// </param>
    public MotivRulesBuilder AddRuleStore(IRuleStore? store = null, bool failFastOnQuarantine = true)
    {
        Services.AddSingleton(store ?? new InMemoryRuleStore());
        Services.AddSingleton(new RuleStoreOptions(failFastOnQuarantine));
        return this;
    }
```

and in the `RuleSet` factory (around line 158), after `Add`-ing every enrolled rule:

```csharp
            // Load after every rule is registered — a stored head can only apply to a rule that
            // exists — and after the PropositionSet has loaded, since a stored rule document may
            // reference an authored proposition.
            if (provider.GetService<IRuleStore>() is not null)
            {
                var report = rules.Load();
                if (provider.GetRequiredService<RuleStoreOptions>().FailFastOnQuarantine)
                    report.ThrowIfQuarantined();
            }
```

Thread `IRuleStore` into the `new RuleSet(...)` construction on the same line.

- [ ] **Step 4: Make the write handlers async**

In `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, convert the `MapPut("/rules/{name}")`
and `MapDelete("/rules/{name}")` handlers to `async` and pass provenance and cancellation:

```csharp
        group.MapPut("/rules/{name}", async (string name, RulePutRequest request, HttpContext http) =>
        {
            // The principal is the author of record. Spec 1 made every authoring endpoint
            // authenticated, so there is always one.
            var provenance = new RuleChangeProvenance(
                http.User.Identity?.Name ?? "unknown", request.ChangeNote);

            var result = await rules.UpdateAsync(
                name, request.DocumentJson, request.BaseVersion, provenance, http.RequestAborted);

            return ToResponse(result);
        });
```

with the matching change for `MapDelete` calling `RevertAsync`. Where governance is mounted, the
handler calls `DirectWriteAsync` instead, unchanged apart from `await` and the token.

Add `ChangeNote` to `RulePutRequest` as an optional `string?`.

- [ ] **Step 5: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0
```
Expected: PASS — 3 new tests plus every pre-existing AspNetCore test.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(aspnetcore): register a rule store and load it at startup"
```

---

### Task 11: The file-backed reference store

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/JsonFileRuleStore.cs`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFileRuleStoreTests.cs`

**Interfaces:**
- Consumes: `IRuleStore`, `StoredRule`, `StoredRuleVersion`, `RuleAppendResult` (Task 2).
- Produces: `JsonFileRuleStore(string path)`.

This is the sample's durability, not the product's. Plan 2C ships the real EF Core store; this exists
so `docker compose up` survives a restart with no database, and so `IRuleStore` has a second
implementation proving the seam is implementable off the in-memory shape.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Motiv.RulesEngine.Sample.Tests;

public class JsonFileRuleStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"motiv-rules-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static StoredRuleVersion Row(string name, int version, string? documentJson = "{}") =>
        new(name, version, documentJson, "alice", DateTimeOffset.UnixEpoch, null, null, "test");

    [Fact]
    public async Task Should_round_trip_the_log_through_the_file()
    {
        // Arrange
        var store = new JsonFileRuleStore(_path);
        await store.AppendAsync([Row("a", 1), Row("b", 1)], default);
        await store.AppendAsync([Row("a", 2, """{"v":2}""")], default);

        // Act — a fresh instance over the same file is what a restart looks like
        var heads = new JsonFileRuleStore(_path).Load();

        // Assert
        heads.Count.ShouldBe(2);
        heads.Single(h => h.Name == "a").Version.ShouldBe(2);
        heads.Single(h => h.Name == "a").DocumentJson.ShouldBe("""{"v":2}""");
    }

    [Fact]
    public async Task Should_enforce_the_primary_key_across_instances()
    {
        // Arrange — two "replicas" over one file, both at v1
        var first = new JsonFileRuleStore(_path);
        await first.AppendAsync([Row("a", 1)], default);

        var second = new JsonFileRuleStore(_path);

        // Act
        await first.AppendAsync([Row("a", 2, """{"winner":true}""")], default);
        var loser = await second.AppendAsync([Row("a", 2, """{"loser":true}""")], default);

        // Assert
        loser.IsConflict.ShouldBeTrue();
        loser.CurrentVersion.ShouldBe(2);
        first.Load().Single().DocumentJson.ShouldBe("""{"winner":true}""");
    }

    [Fact]
    public void Should_return_an_empty_log_when_the_file_does_not_exist()
    {
        // Act / Assert — a first boot is not an error
        new JsonFileRuleStore(_path).Load().ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_move_the_generation_forward_across_instances()
    {
        // Arrange
        var store = new JsonFileRuleStore(_path);
        await store.AppendAsync([Row("a", 1)], default);
        var generation = await store.GetGenerationAsync(default);

        // Act
        await new JsonFileRuleStore(_path).AppendAsync([Row("a", 2)], default);

        // Assert — the fencing token must be derived from the file, not from instance state
        (await new JsonFileRuleStore(_path).GetGenerationAsync(default))
            .ShouldBeGreaterThan(generation);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -f net10.0 --filter "FullyQualifiedName~JsonFileRuleStoreTests"
```
Expected: FAIL — `JsonFileRuleStore` does not exist.

- [ ] **Step 3: Write the store**

`src/examples/Motiv.RulesEngine.Sample/JsonFileRuleStore.cs`:

```csharp
using System.Text.Json;
using Motiv.Serialization;

/// <summary>
/// Seam: rule persistence, backed by a file holding the whole append-only version log. The twin of
/// <see cref="JsonFilePropositionStore"/> — swap it for a database and nothing else changes.
/// </summary>
/// <remarks>
/// <para>
/// Rereads the file on every operation rather than caching, so two processes over one file behave
/// like two replicas over one database: the <c>(Name, Version)</c> check below really is a
/// cross-process compare-and-set, which is what makes it a useful reference implementation rather
/// than a mock. It is not, however, atomic — two processes appending at exactly the same instant can
/// both read a stale file — so it is a sample store, not a production one. That is what plan 2C's
/// EF Core store is for, where the primary key is enforced by the database.
/// </para>
/// <para>
/// The generation is derived from the log's own size rather than held in a field, so it survives a
/// restart and moves for every process — a cached counter would reset to zero on boot and break the
/// fencing token.
/// </para>
/// </remarks>
public sealed class JsonFileRuleStore(string path) : IRuleStore
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();

    /// <inheritdoc />
    public IReadOnlyList<StoredRule> Load()
    {
        lock (_gate)
        {
            return [.. ReadAll()
                .GroupBy(row => row.Name, StringComparer.Ordinal)
                .Select(rows => rows.OrderByDescending(row => row.Version).First())
                .Select(head => new StoredRule(head.Name, head.Version, head.DocumentJson))];
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult((long)ReadAll().Count);
    }

    /// <inheritdoc />
    public Task<RuleAppendResult> AppendAsync(
        IReadOnlyList<StoredRuleVersion> versions, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var log = ReadAll();

            foreach (var version in versions)
            {
                var existing = log.Where(row => row.Name == version.Name).ToList();
                if (existing.Any(row => row.Version == version.Version))
                {
                    return Task.FromResult(
                        RuleAppendResult.Conflict(version.Name, existing.Max(row => row.Version)));
                }
            }

            log.AddRange(versions);
            File.WriteAllText(path, JsonSerializer.Serialize(log, Json));
            return Task.FromResult(RuleAppendResult.Appended);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
        string name, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StoredRuleVersion>>(
                [.. ReadAll().Where(row => row.Name == name).OrderBy(row => row.Version)]);
        }
    }

    private List<StoredRuleVersion> ReadAll()
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<StoredRuleVersion>>(File.ReadAllText(path), Json) ?? [];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Deliberately *not* the same answer JsonFilePropositionStore gives. An unreadable
            // proposition file costs the propositions; an unreadable rule log would silently revert
            // every rule to its compiled default and then overwrite the history that proved what was
            // published. Under an approval gate that is indefensible, so this refuses instead.
            throw new InvalidOperationException(
                $"The rule version log at '{path}' could not be read: {exception.Message}. " +
                "Refusing to continue — appending over it would destroy the published history. " +
                "Repair or move the file.", exception);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -f net10.0 --filter "FullyQualifiedName~JsonFileRuleStoreTests"
```
Expected: PASS — 4 tests.

- [ ] **Step 5: Wire it into the sample**

In `src/examples/Motiv.RulesEngine.Sample/Program.cs`, alongside the existing
`AddPropositions(new JsonFilePropositionStore(...))`, add:

```csharp
    .AddRuleStore(new JsonFileRuleStore(
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "rules.json")))
```

matching whatever path convention the proposition store already uses in that file.

- [ ] **Step 6: Verify the sample end-to-end**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0
```
Expected: PASS across all projects.

Then run the e2e suite — the demo edits rules through the very endpoints that just changed:
```bash
MOTIV_E2E_PORT=5107 pnpm -C ui/apps/demo e2e
```
Expected: 27 passed, 8 skipped (auth cases self-skip without the Keycloak stack, by prior design).
Use a port other than 5100 — this is a worktree, and another checkout may already hold it.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(sample): persist rules to a file-backed version log"
```

- [ ] **Step 8: Phase 2 code review**

Spawn a `code-simplifier` agent over the files changed in Tasks 6–11. Apply its findings, re-run
`dotnet test Motiv.slnx -f net10.0`, and commit separately.

---

# Phase 3 — Verification obligations

### Task 12: Discharge the spec's §7 obligations as named tests

Spec §7 lists five obligations. Three are already covered by tests written above; this task adds the
two that are not, in one file that names each obligation explicitly so a reviewer can check them off.
The remaining two obligations belong to later plans and are recorded as such.

**Files:**
- Create: `src/Motiv.Serialization.Tests/Rules/DurabilityObligationsTests.cs`
- Modify: `docs/live-rules/index.md` (or the nearest existing durability page)
- Test: as above

**Interfaces:**
- Consumes: everything from Tasks 1–11.

| Spec §7 obligation | Where discharged |
|---|---|
| A publish that validated then failed to persist leaves nothing live | `RuleVersionLogTests.Should_leave_nothing_live_when_the_store_refuses_the_append`, `PropositionSetAsyncWriteTests.Should_leave_nothing_live_when_the_store_refuses_the_write` |
| Two replicas racing a write: one 200, one 409; audit shows one published version + one rejected attempt | **Task 12, below** |
| A stale-base publish returns 409 with the current version | **Task 12, below** |
| Quarantine fires on load for a stored document that no longer binds | `RuleSetLoadTests.Should_quarantine_a_stored_document_that_no_longer_binds`, `RuleStoreWiringTests.Should_refuse_startup_when_a_stored_rule_no_longer_binds` |
| The propositions importer round-trips a `JsonFilePropositionStore` file into the EF store | **plan 2C** — there is no EF store yet |

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

/// <summary>
/// The verification obligations of bundle spec 2 §7 that no earlier test already covers. Named after
/// the obligations themselves so a reviewer can check them off against the spec.
/// </summary>
public class DurabilityObligationsTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>One store, two RuleSets — two replicas of the same application.</summary>
    private static RuleSet Replica(IRuleStore store)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());
        set.Load();
        return set;
    }

    [Fact]
    public async Task Two_replicas_racing_a_write_produce_one_publish_and_one_rejection()
    {
        // Arrange — separate RuleSets, one shared store: separate outer gates, one primary key
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);

        // Act — both hold baseVersion 1, and neither gate can see the other
        var results = await Task.WhenAll(
            a.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice")),
            b.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob")));

        // Assert — the lost update is impossible: the PK, not a lock, is what decides
        results.Count(r => r.Outcome == RuleUpdateOutcome.Updated).ShouldBe(1);
        results.Count(r => r.Outcome == RuleUpdateOutcome.VersionConflict).ShouldBe(1);

        // ...and the audit shows exactly one published version, not two
        var history = await store.HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
        history[0].Version.ShouldBe(2);
    }

    [Fact]
    public async Task The_losing_replica_does_not_publish_locally_either()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);

        await a.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Act — b is now stale and does not know it
        var result = await b.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob"));

        // Assert — the refusal must reach memory too, or b would run behaviour the log never recorded
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        b.FindEntry("sample")!.Version.ShouldBe(1);
        b.FindEntry("sample")!.DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task A_stale_base_publish_reports_the_current_version()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var set = Replica(store);
        await set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Act — an editor whose tab sat open through someone else's save
        var result = await set.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("bob"));

        // Assert — the refusal must carry the version to re-base onto, or the editor cannot recover
        result.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        result.Version.ShouldBe(2);
    }

    [Fact]
    public async Task The_two_stores_are_never_written_together()
    {
        // Arrange — a rule store that records when it is called, and a proposition store likewise
        var ruleWrites = new List<string>();
        var propositionWrites = new List<string>();

        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var propositions = new PropositionSet(
            scope, new RecordingPropositionStore(propositionWrites)).AddModel<Customer>("customer");
        propositions.Load();

        var rules = new RuleSet(scope, new RecordingRuleStore(ruleWrites)).Add(new SampleRule());
        rules.Load();

        // Act
        await propositions.CreateAsync("customer.a", "customer", Document, null);
        await rules.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice"));

        // Assert — each store saw only its own write; no operation spans both
        propositionWrites.ShouldBe(["customer.a"]);
        ruleWrites.ShouldBe(["sample"]);
    }
}
```

> **Note for the implementer:** write `RecordingRuleStore` and `RecordingPropositionStore` as small
> private classes in the same file, each delegating to the in-memory store and appending the written
> name to the supplied list.

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DurabilityObligationsTests"
```
Expected: FAIL — the recording stores do not exist yet (CS0246). Every other assertion should pass
once they do; **if any does not, the implementation is wrong, not the test.**

- [ ] **Step 3: Write the recording stores and make it pass**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DurabilityObligationsTests"
```
Expected: PASS — 4 tests.

- [ ] **Step 4: Document the seam**

Per `CLAUDE.md`, user-facing feature documentation goes in `README.md` and `docs/`, not `CLAUDE.md`.
Add a `docs/live-rules/durability.md` page covering: registering a store (`AddRuleStore`), what the
version log records, quarantine and the fail-fast switch, reading history, and rolling back with
`RestoreAsync`. Add it to `docs/live-rules/toc.yml` and `docs/toc.yml`, and add a short entry under
Core Features in `README.md`.

Also add to `CONTEXT.md`'s glossary the terms this plan introduces: **version log**, **head
projection**, **quarantine (rule)**, **generation**, **provenance**.

- [ ] **Step 5: Full verification**

Run, in order:
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0
```
```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet build Motiv.slnx
```
(the second builds every TFM including `netstandard2.0`, catching any C# 8+ feature that slipped in)
```bash
pnpm -C ui/packages/rules-core test && pnpm -C ui/apps/demo test && pnpm -C ui/apps/demo typecheck
```
```bash
MOTIV_E2E_PORT=5107 pnpm -C ui/apps/demo e2e
```

Then confirm published `Motiv` was not touched:
```bash
git diff --stat main -- src/Motiv/
```
Expected: no output.

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "test(serialization): discharge the durability spec's verification obligations"
```

---

## Self-Review

**Spec coverage** — every §2, §3, §4, §5 item in scope maps to a task:

| Spec item | Task |
|---|---|
| §2 `IRuleStore` beside `IPropositionStore`, two symmetrical stores never in one transaction | 2, 12 |
| §2 `StoredRule(Name, Version, DocumentJson?)`, null meaningful, never an absent row | 1, 2, 4 |
| §2 quarantine + fail-fast left to the host | 4, 10 |
| §2 two-tier exclusion, inner Monitor untouched | 6 |
| §2 async `SaveAsync`/`DeleteAsync`, sync `Load()`, rationale is cancellation | 6, 7, 8 |
| §2 outer gate at public entry points only | 6 (documented), 7, 8, 9 |
| §2 ordering bind → check dependents → persist → mutate → commit | 3, 5, 7, 9 |
| §2 append-only log, row shape, PK `(Name, Version)` | 1, 2, 5 |
| §2 version as identity *and* concurrency token | 2, 5 |
| §2 rollback appends | 5 (`RestoreAsync`) |
| §2 kept forever | 2 (no pruning path exists) |
| §2 provenance anchors: document + `BuildId` | 1, 5 |
| §2 remove the in-memory CAS; `VersionConflict` from the PK | 3, 5 |
| §4 head never diverges (projection) | 2 |
| §4 `GetGenerationAsync` is a scalar read | 2 (documented + enforced by the two implementations) |
| §7 obligations 1–4 | 5, 8, 4, 10, 12 |

**Deliberately out of scope**, and recorded as such above: `RefreshAsync` + the `IHostedService`
poller + the client-facing fencing token (spec §2 multi-instance → **plan 2B**); the EF Core store,
its three providers, migrations and the propositions importer (spec §3 → **plan 2C**); §7's importer
obligation, which has nothing to import into until 2C.

**Type consistency** — `RulePrepareResult.Publication` is `IRulePublication?` everywhere;
`RuleAppendResult.IsConflict`/`Name`/`CurrentVersion` are used identically in Tasks 2, 5, 7, 9 and
11; `RuleChangeProvenance` keeps the same four-parameter shape from Task 1 through Task 10;
`CommitCore(name, publication)` returns `RuleUpdateResult` in Tasks 3, 5, 7 and 9. `RuleSet.Load`
returns `RuleLoadReport` in Tasks 4, 5, 10 and 12.

**Known sequencing hazard** — Task 3 leaves `ChangeRequestSet` on a prepare-then-immediately-commit
bridge and Task 5 leaves `RuleSet` on a `.GetAwaiter().GetResult()` bridge. **Both are deleted in
Phase 2** (Tasks 9 and 7 respectively). If Phase 2 is deferred, neither bridge is safe to ship: the
first reintroduces the mid-envelope failure point the batch exists to prevent, and the second blocks
a thread-pool thread inside the publish gate.
