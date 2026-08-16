# Spec 2B — Multi-Instance Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A live replica can rebuild its whole world from the store, knows cheaply when it needs to, swaps it in atomically, and lets clients detect that they were routed to a replica serving an older world.

**Architecture:** `BindingScope` stops owning five separately-mutated structures and owns one immutable `ScopeGeneration` instead, swapped by a single `Volatile.Write`. Every publish and every refresh builds a successor generation off to the side through a builder and swaps it in; rules read their state out of the generation by slot index rather than holding it themselves. A pin (`DecisionSnapshot`) lets a caller hold one generation across several evaluations, and the AspNetCore package pins per request. `RefreshAsync` reads both stores unlocked, rebuilds, and swaps only if no publish landed meanwhile.

**Tech Stack:** C# (net10.0/net9.0/net8.0/net472/netstandard2.0), xUnit + Shouldly, ASP.NET Core minimal APIs, TypeScript (`@motiv-rules/core`, vitest), Playwright e2e.

**Design:** [`docs/superpowers/specs/2026-08-15-spec-2b-multi-instance-refresh-design.md`](../specs/2026-08-15-spec-2b-multi-instance-refresh-design.md)
**Source ticket:** [#120](https://github.com/karlssberg/Motiv/issues/120) · **Predecessor:** [#125](https://github.com/karlssberg/Motiv/pull/125)

## Global Constraints

- **Run .NET tests with the user-local runtime root.** Prefix every `dotnet` invocation with
  `export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"`. Without it, net8.0/net9.0
  testhosts abort with "You must install or update .NET".
- **Before any commit that touches test or library code, run `dotnet build Motiv.slnx` with NO `-f` flag.**
  CI runs a bare `dotnet test` on `windows-latest`, which builds and runs **net472**. Local runs are
  filtered to `-f net10.0` and hide net472 breakage. This bit plan 2A four tasks deep.
- **APIs banned anywhere that reaches net472/netstandard2.0 — including test projects:**
  `DateTimeOffset.UnixEpoch` (hand-build the epoch), ranges `[..^n]`, `System.Index`/`System.Range`,
  `IAsyncEnumerable`, default interface methods, `ValueTask<T>`, `TimeProvider`, `Random.Shared`,
  `ArgumentNullException.ThrowIfNull`, `Chunk`, `DistinctBy`. Collection expressions (`[]`, `[.. x]`)
  are fine on every target.
- **`src/Motiv/` must not change.** `git diff --stat main -- src/Motiv/` must be empty at the end.
  `Motiv` v8.0.0 is published; nothing in this plan belongs to it.
- **TDD is mandatory** (CLAUDE.md): failing test → confirm it fails for the right reason → minimal
  implementation → confirm it passes → commit.
- **A `code-simplifier` review is mandatory** after implementation, per CLAUDE.md. It is Task 18.
- **Test latches take timeouts.** A latch with no timeout hangs CI instead of going red — 2A shipped
  one and disclosed it. Every `ManualResetEventSlim.Wait` / `Task.Wait` in a new test passes a
  timeout and asserts the wait succeeded.
- **UI commands run from `ui/`**: `pnpm --filter @motiv-rules/core test`, and e2e is `pnpm e2e`
  (never a bare `playwright test` — the sample serves a prebuilt `wwwroot` that goes stale silently).
- **Naming is locked by this plan.** Later tasks reference these exact names: `StoreGeneration`,
  `ScopeGeneration`, `ScopeGenerationBuilder`, `AuthoredProposition`, `RuleSlot`, `DecisionSnapshot`,
  `RefreshReport`, `RefreshOutcome`, `RefreshFailure`, `MotivRefreshOptions`,
  `MotivRefreshService`, `MotivGenerationFilter`, `MotivRefreshHealthCheck`.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `src/Motiv.Serialization/StoreGeneration.cs` | The `(Rules, Propositions)` scalar pair and its comparison. |
| `src/Motiv.Serialization/Propositions/ScopeGeneration.cs` | One immutable world: overlay, graph, participants, authored map, rule slots. |
| `src/Motiv.Serialization/Propositions/ScopeGenerationBuilder.cs` | The only way to produce a successor generation. |
| `src/Motiv.Serialization/Propositions/AuthoredProposition.cs` | The immutable authored proposition, moved out of `PropositionSet`. |
| `src/Motiv.Serialization/Propositions/DecisionSnapshot.cs` | The public pin: one generation held across several evaluations. |
| `src/Motiv.Serialization/Rules/RuleSlot.cs` | One rule's per-generation state: bound state + quarantine. |
| `src/Motiv.Serialization/Rules/RefreshReport.cs` | `RefreshOutcome`, `RefreshFailure`, `RefreshReport`. |
| `src/Motiv.Serialization.AspNetCore/MotivRefreshOptions.cs` | Poller interval and enablement. |
| `src/Motiv.Serialization.AspNetCore/MotivRefreshService.cs` | The opt-in `BackgroundService` poller. |
| `src/Motiv.Serialization.AspNetCore/MotivGenerationFilter.cs` | Per-request pin, and the response header. |
| `src/Motiv.Serialization.AspNetCore/MotivRefreshHealthCheck.cs` | Last refresh outcome + current generation. |

**Modified**

| File | Change |
|---|---|
| `src/Motiv.Serialization/Propositions/IPropositionStore.cs` | Add `LoadAsync`, `GetGenerationAsync`; implement on `InMemoryPropositionStore`. |
| `src/Motiv.Serialization/Propositions/BindingScope.cs` | Own `_current`; expose `Current`, `Mutate`, `TrySwap`; `Source` reads the current overlay. |
| `src/Motiv.Serialization/Propositions/PropositionOverlay.cs` | Unchanged in shape; it stops being mutated live and is only ever written through a builder. Update its `<remarks>` to say so — the doc comment currently describes a contract the code did not keep. |
| `src/Motiv.Serialization/Propositions/DependencyGraph.cs` | Copy constructor. |
| `src/Motiv.Serialization/Propositions/IRebindable.cs` | `IRebindCommit.Commit()` → `ApplyTo(ScopeGenerationBuilder)`. |
| `src/Motiv.Serialization/Propositions/PropositionSet.cs` | Commit paths write through the builder; `_authored` moves into the generation; `RefreshAsync`. |
| `src/Motiv.Serialization/Rules/RuleBase.cs` | Slot + scope; quarantine moves into the generation. |
| `src/Motiv.Serialization/Rules/Rule.cs`, `PolicyRule.cs`, `AsyncRule.cs`, `AsyncPolicyRule.cs` | Read state from the generation by slot; publications write into the builder. |
| `src/Motiv.Serialization/Rules/RuleSet.cs` | `Add`/`CommitCore`/`Track` write through the builder; `RefreshAsync`. |
| `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs` | `AddRefresh`, health-check registration. |
| `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` | Add `GenerationHeader`; add the pin/header endpoint filter to the mapped group. |
| `src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs` | Implement the two new members. |
| `src/examples/Motiv.RulesEngine.Sample/Program.cs` | Opt into the poller. |
| `ui/packages/rules-core/src/client.ts` | Track the generation header; report backwards routing. |

---

## Phase 1 — Store symmetry

### Task 1: `IPropositionStore` gains the async read pair

`IRuleStore` already declares `LoadAsync` and `GetGenerationAsync` and documents them as forward
surface awaiting this plan. The proposition store has neither, so a refresh could read only half the
world.

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/IPropositionStore.cs`
- Modify: `src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs` (create)
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreTests.cs`

**Interfaces:**
- Produces: `IPropositionStore.LoadAsync(CancellationToken) : Task<IReadOnlyList<StoredProposition>>`,
  `IPropositionStore.GetGenerationAsync(CancellationToken) : Task<long>`.

- [ ] **Step 1: Write the failing tests**

Create `src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class InMemoryPropositionStoreTests
{
    private static StoredProposition Row(string name, int version = 1) =>
        new(name, "customer", """{"spec":"a"}""", version, null);

    [Fact]
    public async Task Should_read_the_same_rows_asynchronously_as_synchronously()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(PropositionBatch.Save(Row("a")), default);

        // Act
        var asynchronous = await store.LoadAsync(default);

        // Assert
        asynchronous.Select(row => row.Name).ShouldBe(store.Load().Select(row => row.Name));
    }

    [Fact]
    public async Task Should_move_the_generation_when_a_write_lands()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        var before = await store.GetGenerationAsync(default);

        // Act
        await store.WriteAsync(PropositionBatch.Save(Row("a")), default);

        // Assert
        (await store.GetGenerationAsync(default)).ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Should_leave_the_generation_still_when_a_batch_changes_nothing()
    {
        // Arrange
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(PropositionBatch.Save(Row("a")), default);
        var before = await store.GetGenerationAsync(default);

        // Act — an empty batch is not a write
        await store.WriteAsync(new PropositionBatch([], []), default);

        // Assert — a poller that rebuilt on this would rebuild forever
        (await store.GetGenerationAsync(default)).ShouldBe(before);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryPropositionStoreTests"
```

Expected: compile error — `IPropositionStore` does not contain `LoadAsync` / `GetGenerationAsync`.

- [ ] **Step 3: Add the members to the interface**

In `IPropositionStore.cs`, after `Load()`:

```csharp
    /// <summary>
    /// Every persisted proposition, read on a refresh. Separate from <see cref="Load"/> rather than
    /// replacing it because the two run at different times under different constraints: startup
    /// cannot await, a refresh can.
    /// </summary>
    Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// A monotonically increasing number that moves whenever a write lands, so a replica can tell
    /// whether it is behind without re-reading anything.
    /// </summary>
    /// <remarks>
    /// <strong>Must be a scalar read.</strong> An implementation that answers this by loading the
    /// store defeats the point — every replica polls it on a timer. It must never move backwards
    /// while replicas are live: it is half of the fencing token behind monotonic-read consistency.
    /// </remarks>
    Task<long> GetGenerationAsync(CancellationToken cancellationToken);
```

- [ ] **Step 4: Implement them on `InMemoryPropositionStore`**

Add a `private long _generation;` field, then:

```csharp
    /// <inheritdoc />
    public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Load());

    /// <inheritdoc />
    public Task<long> GetGenerationAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(_generation);
    }
```

and inside `WriteAsync`, after the two loops and still inside `lock (_gate)`:

```csharp
            // An empty batch is not a write. A generation that moved anyway would make every
            // replica rebuild its whole world for nothing, on a timer.
            if (batch.Saves.Count > 0 || batch.Deletes.Count > 0)
                _generation++;
```

- [ ] **Step 5: Implement them on `JsonFilePropositionStore`**

`LoadAsync` returns `Task.FromResult(Load())`, as `JsonFileRuleStore` does.

`GetGenerationAsync` **must not** copy its rule-store twin. `JsonFileRuleStore` answers with
`ReadAll().Count`, which is correct there because that store is an append-only version log: the count
is monotonic and moves on every write. This store replaces rows in place — `WriteAsync` drops every
superseded name and re-appends the saves — so editing an existing proposition's document leaves the
count identical, and a polling replica would never observe the single most common authoring
operation.

Derive it from the file's last-write time instead: `File.GetLastWriteTimeUtc(path).Ticks`, or `0`
when the file does not exist, read under the same `_gate` as the other members. Record the asymmetry
in the class remarks — the next person to "make the two stores consistent" needs to find the reason
here — along with the honest caveat that mtime is sample-grade: it inherits the filesystem's
timestamp resolution and is not immune to a clock moving backwards. Plan 2C's EF Core store is the
real answer; this one exists so two processes behave like two replicas.

- [ ] **Step 6: Add the sample-store test**

Append to `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreTests.cs` the two
tests that file's rule-store twin already has (`JsonFileRuleStoreTests` lines 60–80): a generation
that moves after a write, and one that survives being read through a second store instance over the
same path.

- [ ] **Step 7: Run the tests, then the full build**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryPropositionStoreTests" && dotnet build Motiv.slnx
```

Expected: PASS, and `dotnet build` reports 0 warnings / 0 errors across every TFM.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization/Propositions/IPropositionStore.cs src/Motiv.Serialization.Tests/Propositions/InMemoryPropositionStoreTests.cs src/examples/Motiv.RulesEngine.Sample/JsonFilePropositionStore.cs src/examples/Motiv.RulesEngine.Sample.Tests/JsonFilePropositionStoreTests.cs
git commit -m "feat(serialization): give IPropositionStore the async read pair IRuleStore already has"
```

---

## Phase 2 — The generation (behaviour-preserving)

Phase 2 changes no observable behaviour. The existing suite (5,604 tests) is the oracle: if anything
goes red that is not a compile error from a signature this plan changes, stop and fix it before
continuing. Do not add behaviour here — refresh does not exist until Phase 5.

### Task 2: `StoreGeneration`

**Files:**
- Create: `src/Motiv.Serialization/StoreGeneration.cs`
- Test: `src/Motiv.Serialization.Tests/StoreGenerationTests.cs`

**Interfaces:**
- Produces: `StoreGeneration(long Rules, long Propositions)` with `static StoreGeneration Zero`,
  `bool MovedFrom(StoreGeneration other)`, `bool IsBehind(StoreGeneration other)`, `string ToToken()`,
  `static bool TryParseToken(string?, out StoreGeneration)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests;

public class StoreGenerationTests
{
    [Fact]
    public void Should_report_movement_when_either_component_moves()
    {
        // Arrange
        var origin = new StoreGeneration(1, 1);

        // Act & Assert — either store is enough to make a replica stale
        new StoreGeneration(2, 1).MovedFrom(origin).ShouldBeTrue();
        new StoreGeneration(1, 2).MovedFrom(origin).ShouldBeTrue();
        new StoreGeneration(1, 1).MovedFrom(origin).ShouldBeFalse();
    }

    [Fact]
    public void Should_report_being_behind_when_any_component_is_lower()
    {
        // Arrange — deliberately mixed: newer rules, older propositions
        var observed = new StoreGeneration(5, 2);
        var highest = new StoreGeneration(4, 3);

        // Act & Assert — the two sequences are independent, so "behind" is component-wise.
        // There is no total order to appeal to and inventing one would be a fiction.
        observed.IsBehind(highest).ShouldBeTrue();
        highest.IsBehind(observed).ShouldBeTrue();
        observed.IsBehind(observed).ShouldBeFalse();
    }

    [Fact]
    public void Should_round_trip_through_its_wire_token()
    {
        // Arrange
        var generation = new StoreGeneration(12, 7);

        // Act
        var parsed = StoreGeneration.TryParseToken(generation.ToToken(), out var result);

        // Assert
        parsed.ShouldBeTrue();
        result.ShouldBe(generation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("r1")]
    [InlineData("r1.pX")]
    public void Should_refuse_a_token_it_did_not_write(string? token)
    {
        // Act & Assert — a header is caller-supplied text, never trusted input
        StoreGeneration.TryParseToken(token, out _).ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~StoreGenerationTests"
```

Expected: compile error — `StoreGeneration` does not exist.

- [ ] **Step 3: Write the implementation**

```csharp
using System.Globalization;

namespace Motiv.Serialization;

/// <summary>
/// Where both stores stand, as one value: a scalar per store, each moving whenever a write lands
/// there. Polled to decide whether a replica needs to rebuild, and stamped on responses so a client
/// can tell it was routed to a replica serving an older world.
/// </summary>
/// <remarks>
/// A pair rather than one number because the two stores are <em>never written in the same
/// transaction</em> — there is no shared sequence to derive. Comparison is therefore component-wise
/// and deliberately not a total order: "am I behind" is answerable, "which of these two is newer"
/// is not, and inventing an answer would be a fiction a caller could act on.
/// </remarks>
/// <param name="Rules">Where the rule store stands.</param>
/// <param name="Propositions">Where the proposition store stands.</param>
public readonly record struct StoreGeneration(long Rules, long Propositions)
{
    /// <summary>Before anything has been read or written.</summary>
    public static StoreGeneration Zero => default;

    /// <summary>Whether either component differs from <paramref name="other"/> — the poll's question.</summary>
    public bool MovedFrom(StoreGeneration other) => this != other;

    /// <summary>
    /// Whether any component is lower than <paramref name="other"/>'s — the client's question, and
    /// the reason this is not an ordering: both directions can be true at once.
    /// </summary>
    public bool IsBehind(StoreGeneration other) =>
        Rules < other.Rules || Propositions < other.Propositions;

    /// <summary>The wire form, as carried by the response header.</summary>
    public string ToToken() =>
        string.Format(CultureInfo.InvariantCulture, "r{0}.p{1}", Rules, Propositions);

    /// <summary>Reads a token written by <see cref="ToToken"/>. Anything else is refused.</summary>
    public static bool TryParseToken(string? token, out StoreGeneration generation)
    {
        generation = Zero;

        if (string.IsNullOrEmpty(token) || token![0] != 'r')
            return false;

        var separator = token.IndexOf(".p", StringComparison.Ordinal);
        if (separator < 1)
            return false;

        var rules = token.Substring(1, separator - 1);
        var propositions = token.Substring(separator + 2);

        if (!long.TryParse(rules, NumberStyles.None, CultureInfo.InvariantCulture, out var ruleValue)
            || !long.TryParse(propositions, NumberStyles.None, CultureInfo.InvariantCulture, out var propositionValue))
        {
            return false;
        }

        generation = new StoreGeneration(ruleValue, propositionValue);
        return true;
    }
}
```

`Substring` rather than range indexing, and `NumberStyles.None` rather than a plain `long.TryParse`
overload that varies by target — both because this file compiles for netstandard2.0 and net472.

- [ ] **Step 4: Run it to verify it passes**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~StoreGenerationTests"
```

Expected: PASS (9 tests, counting the theory cases).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/StoreGeneration.cs src/Motiv.Serialization.Tests/StoreGenerationTests.cs
git commit -m "feat(serialization): add StoreGeneration, the two-store fencing token"
```

### Task 3: `DependencyGraph` copy constructor

The builder needs to fork the graph so a rebuild can rewrite edges without touching the live one.

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/DependencyGraph.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/DependencyGraphTests.cs` (append; create if absent)

**Interfaces:**
- Consumes: nothing.
- Produces: `internal DependencyGraph(DependencyGraph copyFrom)`.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Should_fork_without_aliasing_the_original()
    {
        // Arrange
        var original = new DependencyGraph();
        original.Set(NodeId.Proposition("child"), ["parent"]);

        // Act — a copy is edited; the original must not see it
        var copy = new DependencyGraph(original);
        copy.Set(NodeId.Proposition("other"), ["parent"]);
        copy.Remove(NodeId.Proposition("child"));

        // Assert — both indexes fork, not just the outgoing one
        original.Referrers("parent").Select(node => node.Name).ShouldBe(["child"]);
        copy.Referrers("parent").Select(node => node.Name).ShouldBe(["other"]);
    }
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DependencyGraphTests"
```

Expected: compile error — no such constructor.

- [ ] **Step 3: Write the implementation**

Add to `DependencyGraph`, above `Set`:

```csharp
    public DependencyGraph()
    {
    }

    /// <summary>
    /// Forks a graph so a prospective world can rewrite edges while the live one keeps serving.
    /// Both indexes are copied, and the reverse index's sets are copied rather than aliased — a
    /// shared <see cref="HashSet{T}"/> would let an edit to the fork appear in the live graph, which
    /// is precisely the half-applied state a generation exists to make unrepresentable.
    /// </summary>
    public DependencyGraph(DependencyGraph copyFrom)
    {
        foreach (var entry in copyFrom._outgoing)
            _outgoing[entry.Key] = entry.Value;

        foreach (var entry in copyFrom._incoming)
            _incoming[entry.Key] = [.. entry.Value];
    }
```

The `_outgoing` values are already `string[]` copies made by `Set`, so aliasing them is safe;
`_incoming`'s sets are mutated in place by `Set`/`Detach`, so they are not.

- [ ] **Step 4: Run it to verify it passes**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DependencyGraphTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/Propositions/DependencyGraph.cs src/Motiv.Serialization.Tests/Propositions/DependencyGraphTests.cs
git commit -m "feat(serialization): let DependencyGraph fork for a prospective world"
```

### Task 4: `ScopeGeneration`, its builder, and `BindingScope._current`

The structural heart of the plan. After this task `BindingScope` owns one immutable world and every
mutation goes through a builder — but the commit paths still call it one mutation at a time, so
atomicity per publish arrives in Tasks 6 and 7.

**Files:**
- Create: `src/Motiv.Serialization/Rules/RuleSlot.cs`
- Create: `src/Motiv.Serialization/Propositions/ScopeGeneration.cs`
- Create: `src/Motiv.Serialization/Propositions/ScopeGenerationBuilder.cs`
- Modify: `src/Motiv.Serialization/Propositions/BindingScope.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/ScopeGenerationTests.cs`

**Interfaces:**
- Consumes: `StoreGeneration` (Task 2), `new DependencyGraph(DependencyGraph)` (Task 3).
- Produces:
  - `internal sealed class RuleSlot` with `object State`, `IReadOnlyList<RuleError> Quarantine`,
    `RuleSlot WithState(object)`, `RuleSlot WithQuarantine(IReadOnlyList<RuleError>)`.
  - `internal sealed class ScopeGeneration` with `StoreGeneration Sequence`, `PropositionOverlay Overlay`,
    `DependencyGraph Graph`, `IReadOnlyDictionary<NodeId, IRebindable> Participants`,
    `IReadOnlyDictionary<string, AuthoredProposition> Authored`, `RuleSlot?[] RuleSlots`, `ISpecSource Source`.
  - `internal sealed class ScopeGenerationBuilder` with `SetOverlayEntry`, `RemoveOverlayEntry`,
    `SetAuthored`, `RemoveAuthored`, `FindAuthored`, `Enrol`, `Withdraw`, `SetRuleState`,
    `SetRuleQuarantine`, `EnsureRuleSlots`, `SetSequence`, `Graph`, `Source`, `Build()`.
  - `BindingScope.Current`, `BindingScope.Active`, `BindingScope.WriteStamp`,
    `BindingScope.Mutate(Action<ScopeGenerationBuilder>)`, `BindingScope.TrySwap(ScopeGeneration, long)`,
    `BindingScope.Pin()`.

Note: `AuthoredProposition` is created in Task 5. Until then, type the two authored members against
`PropositionSet.Authored` and change them in Task 5 — a one-line edit that keeps this task compiling
on its own.

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Serialization.Tests/Propositions/ScopeGenerationTests.cs`:

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class ScopeGenerationTests
{
    [Fact]
    public void Should_publish_a_mutation_as_one_new_generation()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var before = scope.Current;

        // Act
        scope.Mutate(builder => builder.SetSequence(new StoreGeneration(1, 0)));

        // Assert — the old generation is untouched, not edited
        scope.Current.ShouldNotBeSameAs(before);
        before.Sequence.ShouldBe(StoreGeneration.Zero);
        scope.Current.Sequence.ShouldBe(new StoreGeneration(1, 0));
    }

    [Fact]
    public void Should_move_the_write_stamp_on_every_mutation()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var before = scope.WriteStamp;

        // Act
        scope.Mutate(builder => builder.SetSequence(new StoreGeneration(1, 0)));

        // Assert — this is what a refresh compares against to know a publish beat it
        scope.WriteStamp.ShouldNotBe(before);
    }

    [Fact]
    public void Should_refuse_a_swap_whose_write_stamp_is_stale()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var stamp = scope.WriteStamp;
        var successor = scope.Current;

        // Act — a publish lands after the successor was built
        scope.Mutate(builder => builder.SetSequence(new StoreGeneration(9, 9)));
        var swapped = scope.TrySwap(successor, stamp);

        // Assert — the rebuild is discarded, and the publish survives
        swapped.ShouldBeFalse();
        scope.Current.Sequence.ShouldBe(new StoreGeneration(9, 9));
    }

    [Fact]
    public void Should_accept_a_swap_whose_write_stamp_still_holds()
    {
        // Arrange
        var scope = new BindingScope(new SpecRegistry());
        var stamp = scope.WriteStamp;
        var builder = new ScopeGenerationBuilder(scope.Registry, scope.Current);
        builder.SetSequence(new StoreGeneration(4, 4));

        // Act
        var swapped = scope.TrySwap(builder.Build(), stamp);

        // Assert
        swapped.ShouldBeTrue();
        scope.Current.Sequence.ShouldBe(new StoreGeneration(4, 4));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~ScopeGenerationTests"
```

Expected: compile errors — `Mutate`, `Current`, `WriteStamp`, `TrySwap` and `ScopeGenerationBuilder`
do not exist.

- [ ] **Step 3: Write `RuleSlot`**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One rule's place in a generation: the bound state it evaluates through, and why its stored
/// document could not be applied. Both live here rather than on the rule so that a whole world moves
/// with a single reference write — a rule that held either itself would be a second write, and two
/// writes are a straddle.
/// </summary>
/// <remarks>
/// <c>State</c> is typed <see cref="object"/> because the state type closes over the rule's own
/// generic arguments; the rule casts it back on the way out, which is a castclass on a path that
/// already dereferences two fields.
/// </remarks>
internal sealed class RuleSlot(object state, IReadOnlyList<RuleError> quarantine)
{
    public object State { get; } = state;

    public IReadOnlyList<RuleError> Quarantine { get; } = quarantine;

    /// <summary>
    /// The slot after a successful publish. Quarantine is dropped rather than carried: a quarantine
    /// says "running a compiled default in place of a stored document that would not bind", and a
    /// successful publish is exactly what stops that being true.
    /// </summary>
    public RuleSlot WithState(object state) => new(state, []);

    /// <summary>The slot after a stored document failed to bind: the binding is kept, the reason recorded.</summary>
    public RuleSlot WithQuarantine(IReadOnlyList<RuleError> quarantine) => new(State, quarantine);
}
```

- [ ] **Step 4: Write `ScopeGeneration`**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One coherent world: every authored proposition, every rule's binding, the graph relating them,
/// and where both stores stood when it was built. Immutable once constructed and replaced wholesale
/// — never edited — so a reader holding one holds a set that really was published together.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a straddle unrepresentable. Before it, a publish wrote the overlay, the graph,
/// the participant table and each rule's own state separately, so a reader between two of those
/// writes could observe a combination no publish ever produced. For a product whose promise is
/// explainability, an internally inconsistent justification is the one failure that cannot be
/// tolerated.
/// </para>
/// <para>
/// Holding a generation is not by itself enough: a caller evaluating two rules performs two reads
/// and can still see two worlds. <see cref="DecisionSnapshot"/> is the other half.
/// </para>
/// </remarks>
internal sealed class ScopeGeneration
{
    public ScopeGeneration(
        SpecRegistry registry,
        StoreGeneration sequence,
        PropositionOverlay overlay,
        DependencyGraph graph,
        IReadOnlyDictionary<NodeId, IRebindable> participants,
        IReadOnlyDictionary<string, AuthoredProposition> authored,
        RuleSlot?[] ruleSlots)
    {
        Sequence = sequence;
        Overlay = overlay;
        Graph = graph;
        Participants = participants;
        Authored = authored;
        RuleSlots = ruleSlots;
        Source = new LayeredSpecSource(overlay, registry);
    }

    /// <summary>Where both stores stood when this world was built.</summary>
    public StoreGeneration Sequence { get; }

    /// <summary>The authored layer as it resolves in this world.</summary>
    public PropositionOverlay Overlay { get; }

    /// <summary>Who references whom in this world.</summary>
    public DependencyGraph Graph { get; }

    /// <summary>Every node that must be rebound when a proposition it references is republished.</summary>
    public IReadOnlyDictionary<NodeId, IRebindable> Participants { get; }

    /// <summary>Every authored proposition, by name.</summary>
    public IReadOnlyDictionary<string, AuthoredProposition> Authored { get; }

    /// <summary>
    /// Every rule's state, indexed by the slot assigned at registration. Null only for a slot whose
    /// rule is mid-registration inside <see cref="RuleSet.Add"/>.
    /// </summary>
    public RuleSlot?[] RuleSlots { get; }

    /// <summary>Resolution in this world: authored first, then compiled.</summary>
    public ISpecSource Source { get; }
}
```

- [ ] **Step 5: Write `ScopeGenerationBuilder`**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The only way to produce a <see cref="ScopeGeneration"/>. A builder forks the world it starts
/// from, is written into freely, and yields one successor — so a caller cannot publish half a change,
/// and "everything fallible runs before anything mutates" has somewhere to put the not-yet-mutated
/// state.
/// </summary>
/// <remarks>
/// Not thread-safe, and not meant to be: a builder is created, written and built inside one holder of
/// <see cref="BindingScope"/>'s inner monitor, or off to the side by a refresh that owns it alone.
/// </remarks>
internal sealed class ScopeGenerationBuilder
{
    private readonly SpecRegistry _registry;
    private readonly PropositionOverlay _overlay;
    private readonly Dictionary<NodeId, IRebindable> _participants;
    private readonly Dictionary<string, AuthoredProposition> _authored;
    private RuleSlot?[] _ruleSlots;
    private StoreGeneration _sequence;

    /// <summary>Forks an existing world — the publish path, which changes a little and keeps the rest.</summary>
    public ScopeGenerationBuilder(SpecRegistry registry, ScopeGeneration from)
    {
        _registry = registry;
        _overlay = new PropositionOverlay(from.Overlay);
        Graph = new DependencyGraph(from.Graph);
        _participants = new Dictionary<NodeId, IRebindable>(from.Participants);
        _authored = new Dictionary<string, AuthoredProposition>(from.Authored, StringComparer.Ordinal);
        _ruleSlots = [.. from.RuleSlots];
        _sequence = from.Sequence;
    }

    /// <summary>
    /// Starts from nothing but the rules' shape — the refresh path, which rebuilds the authored world
    /// from the store rather than amending it. Slots are carried over in count only: a refresh rebinds
    /// every rule, and a slot index is stable for a rule's lifetime.
    /// </summary>
    public ScopeGenerationBuilder(SpecRegistry registry, int ruleCount)
    {
        _registry = registry;
        _overlay = new PropositionOverlay();
        Graph = new DependencyGraph();
        _participants = [];
        _authored = new Dictionary<string, AuthoredProposition>(StringComparer.Ordinal);
        _ruleSlots = new RuleSlot?[ruleCount];
        _sequence = StoreGeneration.Zero;
    }

    /// <summary>The prospective graph, written directly by the callers that track edges.</summary>
    public DependencyGraph Graph { get; }

    /// <summary>Resolution as this prospective world would resolve — what a rebind binds against.</summary>
    public ISpecSource Source => new LayeredSpecSource(_overlay, _registry);

    public void SetOverlayEntry(SpecRegistryEntry entry) => _overlay.Set(entry);

    public void RemoveOverlayEntry(string name) => _overlay.Remove(name);

    public void SetAuthored(AuthoredProposition authored) => _authored[authored.Name] = authored;

    public void RemoveAuthored(string name) => _authored.Remove(name);

    public AuthoredProposition? FindAuthored(string name) =>
        _authored.TryGetValue(name, out var authored) ? authored : null;

    public void Enrol(IRebindable participant) => _participants[participant.Node] = participant;

    public void Withdraw(NodeId node) => _participants.Remove(node);

    /// <summary>Grows the slot array so <paramref name="count"/> rules fit. Never shrinks: slots are permanent.</summary>
    public void EnsureRuleSlots(int count)
    {
        if (_ruleSlots.Length >= count)
            return;

        var grown = new RuleSlot?[count];
        Array.Copy(_ruleSlots, grown, _ruleSlots.Length);
        _ruleSlots = grown;
    }

    /// <summary>Publishes a rule's binding, clearing any quarantine — see <see cref="RuleSlot.WithState"/>.</summary>
    public void SetRuleState(int slot, object state)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithState(state)
            : new RuleSlot(state, []);
    }

    /// <summary>Records why a stored document could not be applied, keeping the binding in place.</summary>
    public void SetRuleQuarantine(int slot, IReadOnlyList<RuleError> quarantine)
    {
        EnsureRuleSlots(slot + 1);
        _ruleSlots[slot] = _ruleSlots[slot] is { } existing
            ? existing.WithQuarantine(quarantine)
            : throw new InvalidOperationException(
                $"Rule slot {slot} has no state, so there is nothing to quarantine.");
    }

    public void SetSequence(StoreGeneration sequence) => _sequence = sequence;

    public ScopeGeneration Build() =>
        new(_registry, _sequence, _overlay, Graph, _participants, _authored, _ruleSlots);
}
```

- [ ] **Step 6: Rework `BindingScope` to own the generation**

In `BindingScope.cs`:

1. Delete the `Overlay` and `Graph` properties and the `_participants` field. Keep `Source` as a
   property, but assign it the façade below rather than a `LayeredSpecSource`.
2. Add, after the `_outer` field:

```csharp
    private readonly AsyncLocal<ScopeGeneration?> _pinned = new();
    private ScopeGeneration _current;
    private long _writes;
```

3. In the constructor, replace the `Overlay`/`Source` assignments with:

```csharp
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _current = new ScopeGenerationBuilder(Registry, ruleCount: 0).Build();
        Source = new ScopeSource(this);
```

4. Add these members:

```csharp
    /// <summary>The live world. One volatile read; never edited in place.</summary>
    public ScopeGeneration Current => Volatile.Read(ref _current);

    /// <summary>
    /// The world this call resolves against: the pinned one when a <see cref="DecisionSnapshot"/> is
    /// open on this async flow, otherwise the live one. Every evaluation goes through here, which is
    /// what makes a pinned decision see one world rather than one world per rule.
    /// </summary>
    public ScopeGeneration Active => _pinned.Value ?? Current;

    /// <summary>
    /// How many times the world has been replaced. A refresh records this before building and refuses
    /// to swap if it has moved — the world's own compare-and-set, and the reason a slow store need not
    /// hold the write gate.
    /// </summary>
    public long WriteStamp => Volatile.Read(ref _writes);

    /// <summary>
    /// Builds a successor from the live world and swaps it in as one write. Assumes the inner monitor
    /// is held.
    /// </summary>
    public void Mutate(Action<ScopeGenerationBuilder> mutate)
    {
        var builder = new ScopeGenerationBuilder(Registry, Current);
        mutate(builder);
        Publish(builder.Build());
    }

    /// <summary>
    /// Swaps in a successor built elsewhere, unless the world moved since
    /// <paramref name="expectedWriteStamp"/> was taken. Assumes the inner monitor is held.
    /// </summary>
    /// <returns>Whether the successor went live.</returns>
    public bool TrySwap(ScopeGeneration successor, long expectedWriteStamp)
    {
        if (Volatile.Read(ref _writes) != expectedWriteStamp)
            return false;

        Publish(successor);
        return true;
    }

    /// <summary>Pins the live world for the current async flow. A nested pin reuses the outer one.</summary>
    public IDisposable Pin()
    {
        if (_pinned.Value is not null)
            return NestedPin.Instance;

        _pinned.Value = Current;
        return new OuterPin(this);
    }

    private void Publish(ScopeGeneration successor)
    {
        // The stamp moves first: a reader that sees the new world must never also see a stamp that
        // would let a stale rebuild overwrite it.
        Volatile.Write(ref _writes, _writes + 1);
        Volatile.Write(ref _current, successor);
    }

    private sealed class OuterPin(BindingScope scope) : IDisposable
    {
        public void Dispose() => scope._pinned.Value = null;
    }

    private sealed class NestedPin : IDisposable
    {
        public static NestedPin Instance { get; } = new();

        // The outer pin owns the lifetime; releasing here would end the decision early.
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A stable façade over an unstable world: <see cref="RuleSerializer"/> is built once and must
    /// keep resolving through whatever generation is current — or pinned — at the moment of the call.
    /// </summary>
    private sealed class ScopeSource(BindingScope scope) : ISpecSource
    {
        public SpecRegistryEntry? Find(string name) => scope.Active.Source.Find(name);

        public CollectionBinding<TParent>? FindCollection<TParent>(string path) =>
            scope.Registry.FindCollection<TParent>(path);
    }
```

5. Route `Enrol`/`Withdraw` through the builder:

```csharp
    /// <summary>Registers a node as rebindable. Replaces any participant already under that id.</summary>
    public void Enrol(IRebindable participant) =>
        Locked(() => Mutate(builder => builder.Enrol(participant)));

    /// <summary>Unregisters a node, so it is no longer rebound.</summary>
    public void Withdraw(NodeId node) =>
        Locked(() => Mutate(builder => builder.Withdraw(node)));
```

6. Change `PrepareClosure`'s prospective parameter from `PropositionOverlay` to
   `ScopeGenerationBuilder`, reading the graph and participants from `Current`:

```csharp
    public IReadOnlyList<BrokenDependent> PrepareClosure(
        string propositionName, ScopeGenerationBuilder prospective, List<IRebindCommit> commits,
        HashSet<NodeId> excluding)
    {
        var broken = new List<BrokenDependent>();

        foreach (var node in Current.Graph.DependentClosure(propositionName))
        {
            if (excluding.Contains(node))
                continue;

            // A graph edge can outlive its participant while a node is being torn down.
            if (!Current.Participants.TryGetValue(node, out var participant))
                continue;

            var errors = new List<RuleError>();
            var commit = participant.PrepareRebind(prospective.Source, errors);

            if (commit is null)
            {
                broken.Add(new BrokenDependent(node.Name, node.KindLabel, errors));
                // Keep going: reporting only the first break would make a wide failure take many
                // round trips to diagnose.
                continue;
            }

            commit.ApplyTo(prospective);
            commits.Add(commit);
        }

        return broken;
    }
```

**Keep the existing `<remarks>` block on this method verbatim.** The exclusion-set reasoning it
records is still exactly right and is the only place that reasoning is written down.

7. Replace `CommitClosure` with a builder-taking form:

```csharp
    /// <summary>
    /// Applies commits prepared earlier by <see cref="PrepareClosure"/> into the successor being
    /// built, so the whole closure goes live in the one swap that publishes it.
    /// </summary>
    public static void CommitClosure(IReadOnlyList<IRebindCommit> commits, ScopeGenerationBuilder builder)
    {
        foreach (var commit in commits)
            commit.ApplyTo(builder);
    }
```

- [ ] **Step 7: Change `IRebindCommit` to write into a builder**

In `IRebindable.cs`, replace `SpecRegistryEntry? OverlayEntry { get; }` and `void Commit();` with:

```csharp
    /// <summary>
    /// Publishes the prepared binding into the world being built. Must not fail. Replaces the older
    /// pair of an overlay entry plus a <c>Commit()</c> that mutated live state: a commit now has one
    /// destination, and it is the world nobody is reading yet.
    /// </summary>
    void ApplyTo(ScopeGenerationBuilder builder);
```

Give `NoRebindCommit.ApplyTo` an empty body, keeping its existing summary.

- [ ] **Step 8: Fix every call site the compiler names**

`Rule<,>.RebindCommit`, `Rule<,>.Publication`, `PropositionSet.Authored.RebindCommit`,
`PropositionSet.CommitPublish` / `CommitWithdrawCore`, `RuleSet.CommitCore` / `Track`, and
`ChangeRequestSet`. In this task make the **minimal** change that compiles: each site wraps its work
in `Scope.Mutate(builder => …)`. Tasks 6 and 7 collapse those into one `Mutate` per publish.

- [ ] **Step 9: Run the whole serialization suite**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS — 747 tests, unchanged. This task changes no behaviour, so a failure here is a real
regression in the refactor, never a test that needs updating.

- [ ] **Step 10: Run the full solution and the all-TFM build**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0 && dotnet build Motiv.slnx
```

Expected: 5,604 tests PASS; build 0 warnings / 0 errors.

- [ ] **Step 11: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "refactor(serialization): give BindingScope one immutable generation, swapped as a whole"
```

### Task 5: `AuthoredProposition` — moved out, and immutable

`Authored` is nested in `PropositionSet` and mutable: `Bound`, `Quarantine` and `References` are all
settable, and `RebindCommit.Commit()` writes to them. A generation cannot be immutable while the
objects inside it are not, so a rebind must produce a replacement instead.

**Files:**
- Create: `src/Motiv.Serialization/Propositions/AuthoredProposition.cs`
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs` (delete the nested `Authored`; make
  `ResolveModel` and `_options` reachable)
- Modify: `src/Motiv.Serialization/Governance/ChangeRequestSet.cs` (the one external reference, at the
  `PropositionSet.RowFor(publish.Edit.Authored!)` call site — a type name change only)
- Test: `src/Motiv.Serialization.Tests/Propositions/AuthoredPropositionTests.cs`

**Interfaces:**
- Consumes: `ScopeGenerationBuilder` (Task 4).
- Produces: `internal sealed class AuthoredProposition : IRebindable` with constructor
  `(PropositionSet owner, string name, string modelTypeId, string documentJson, int version,
  string? description, SpecRegistryEntry? bound, IReadOnlyList<RuleError> quarantine,
  IReadOnlyList<string> references)`, read-only properties of the same names, and
  `AuthoredProposition WithBinding(SpecRegistryEntry bound)`,
  `AuthoredProposition WithQuarantine(IReadOnlyList<RuleError> quarantine)`.
- `PropositionSet.ResolveModel` and `PropositionSet.Options` become `internal`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class AuthoredPropositionTests
{
    [Fact]
    public void Should_produce_a_replacement_rather_than_mutate_when_rebound()
    {
        // Arrange
        var registry = new SpecRegistry();
        var propositions = new PropositionSet(registry, new InMemoryPropositionStore());
        var original = new AuthoredProposition(
            propositions, "customer.is-adult", "customer", """{"spec":"a"}""", 3, null,
            bound: null, quarantine: [], references: ["a"]);

        // Act
        var repaired = original.WithQuarantine([new RuleError("$", RuleErrorCode.InvalidNode, "broken")]);

        // Assert — the generation that holds the original must not see the change
        original.Quarantine.ShouldBeEmpty();
        repaired.Quarantine.Count.ShouldBe(1);
        repaired.Version.ShouldBe(original.Version);
        repaired.References.ShouldBe(original.References);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~AuthoredPropositionTests"
```

Expected: compile error — `AuthoredProposition` does not exist.

- [ ] **Step 3: Write the implementation**

Move the nested class into its own file, converting every setter to a constructor parameter and
turning `RebindCommit` into a builder writer:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One authored proposition's state within a generation, and its participation in the rebind
/// transaction. Immutable: a rebind produces a replacement rather than editing this one, because the
/// generation holding it is published to lock-free readers and must not change underneath them.
/// </summary>
internal sealed class AuthoredProposition(
    PropositionSet owner,
    string name,
    string modelTypeId,
    string documentJson,
    int version,
    string? description,
    SpecRegistryEntry? bound,
    IReadOnlyList<RuleError> quarantine,
    IReadOnlyList<string> references)
    : IRebindable
{
    public NodeId Node { get; } = NodeId.Proposition(name);
    public string Name { get; } = name;
    public string ModelTypeId { get; } = modelTypeId;
    public string DocumentJson { get; } = documentJson;
    public int Version { get; } = version;
    public string? Description { get; } = description;

    /// <summary>The current binding, or null while quarantined.</summary>
    public SpecRegistryEntry? Bound { get; } = bound;

    /// <summary>Why this proposition is excluded from the effective set, or empty.</summary>
    public IReadOnlyList<RuleError> Quarantine { get; } = quarantine;

    /// <summary>The names this proposition's document resolves.</summary>
    public IReadOnlyList<string> References { get; } = references;

    /// <summary>
    /// The same proposition, rebound. The version is deliberately carried across: the document did
    /// not change, only what it resolves to, so bumping it would spuriously conflict with an editor's
    /// open draft. Quarantine is dropped — binding again is what resolves one.
    /// </summary>
    public AuthoredProposition WithBinding(SpecRegistryEntry rebound) =>
        new(owner, Name, ModelTypeId, DocumentJson, Version, Description, rebound, [], References);

    /// <summary>The same proposition, excluded from the effective set with the reasons why.</summary>
    public AuthoredProposition WithQuarantine(IReadOnlyList<RuleError> quarantine) =>
        new(owner, Name, ModelTypeId, DocumentJson, Version, Description, Bound, quarantine, References);

    public IRebindCommit? PrepareRebind(ISpecSource prospective, List<RuleError> errors)
    {
        var model = owner.ResolveModel(ModelTypeId, errors);
        if (model is null)
            return null;

        var document = new RuleDocumentParser(owner.Options).Parse(DocumentJson, errors);
        if (document is null || errors.Count > 0)
            return null;

        var isAsync = PropositionSet.BindsAsync(prospective, References);
        var entry = model.Bind(prospective, Name, Description, document, isAsync, errors);
        return entry is null ? null : new RebindCommit(this, entry);
    }

    private sealed class RebindCommit(AuthoredProposition authored, SpecRegistryEntry entry) : IRebindCommit
    {
        public void ApplyTo(ScopeGenerationBuilder builder)
        {
            builder.SetAuthored(authored.WithBinding(entry));
            builder.SetOverlayEntry(entry);
        }
    }
}
```

Make `PropositionSet.ResolveModel` and `PropositionSet.BindsAsync` `internal`, and add an
`internal RuleSerializerOptions Options => _options;` property. Delete the nested `Authored` class,
and rename every `Authored` reference in `PropositionSet` and `ChangeRequestSet` to
`AuthoredProposition` — including `WritePrepare`'s property type, whose property name stays
`Authored` so `ChangeRequestSet.cs:1309` is untouched.

- [ ] **Step 4: Run the tests**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS — 747 + 1 tests. Still no behaviour change.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "refactor(serialization): make the authored proposition immutable and give it its own file"
```

### Task 6: One swap per proposition publish

Task 4 left `PropositionSet` calling `Mutate` several times per publish, which is several swaps and
therefore several observable worlds. Collapse each publish to one.

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs`
- Test: `src/Motiv.Serialization.Tests/Propositions/PropositionPublishAtomicityTests.cs`

**Interfaces:**
- Consumes: `BindingScope.Mutate` (Task 4), `AuthoredProposition` (Task 5).
- Produces: `internal static int PropositionSet.CommitPublishCore(WritePrepare, ScopeGenerationBuilder)`,
  `internal static void PropositionSet.CommitWithdrawCore(WritePrepare, ScopeGenerationBuilder)`,
  `internal static void PropositionSet.CommitPublish(AuthoredProposition, ScopeGenerationBuilder)`.
  All three become `static`: once the authored proposition carries its own binding and edges, a
  commit needs nothing from the set instance, and `ChangeRequestSet` calls them as
  `PropositionSet.CommitPublishCore(edit, builder)`.
  Every one takes the builder its caller owns, so a governed envelope can pass one builder through
  all of its members and publish them in a single swap.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionPublishAtomicityTests
{
    [Fact]
    public async Task Should_publish_a_proposition_and_its_dependents_in_one_generation()
    {
        // Arrange — base, plus a proposition that references it, so a publish rebinds two nodes
        var registry = new SpecRegistry();
        registry.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var propositions = new PropositionSet(registry, new InMemoryPropositionStore());
        propositions.AddModel<int>("number");
        propositions.Load();

        await propositions.CreateAsync("base", "number", """{"spec":"positive"}""", null);
        await propositions.CreateAsync("derived", "number", """{"spec":"base"}""", null);

        var before = propositions.Scope.WriteStamp;

        // Act — republishing base rebinds derived as well
        var result = await propositions.UpdateAsync("base", """{"not":{"spec":"positive"}}""", 1);

        // Assert — one publish is one swap, however many nodes it rebound. Two swaps would let a
        // reader in between observe a combination that was never published.
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        (propositions.Scope.WriteStamp - before).ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~PropositionPublishAtomicityTests"
```

Expected: FAIL — the stamp moves by more than 1, because Task 4 left several `Mutate` calls per
publish.

- [ ] **Step 3: Thread the builder through the commit path**

1. `CommitPublish` takes the builder and writes all four facts into it:

```csharp
    /// <summary>
    /// The infallible half of a publish, for a caller that has already persisted the document. Folds
    /// the authored proposition into the successor's overlay, graph and participant table — all into
    /// one builder, so all four facts go live in the single swap that publishes it.
    /// </summary>
    internal static void CommitPublish(AuthoredProposition authored, ScopeGenerationBuilder builder)
    {
        builder.SetAuthored(authored);
        builder.SetOverlayEntry(authored.Bound!);
        builder.Graph.Set(authored.Node, authored.References);
        builder.Enrol(authored);
    }
```

2. `CommitPublishCore` and `CommitWithdrawCore` take the builder and pass it on:

```csharp
    internal static int CommitPublishCore(WritePrepare prepared, ScopeGenerationBuilder builder)
    {
        var authored = prepared.Authored!;
        CommitPublish(authored, builder);
        BindingScope.CommitClosure(prepared.Commits!, builder);
        return authored.Version;
    }

    internal static void CommitWithdrawCore(WritePrepare prepared, ScopeGenerationBuilder builder)
    {
        BindingScope.CommitClosure(prepared.Commits!, builder);

        var current = prepared.Authored!;
        builder.RemoveAuthored(current.Name);
        builder.RemoveOverlayEntry(current.Name);
        builder.Graph.Remove(current.Node);
        builder.Withdraw(current.Node);
    }
```

3. `PersistAndCommitCoreAsync`'s final line becomes one `Mutate`:

```csharp
        return Scope.Locked(() =>
        {
            var version = 0;
            Scope.Mutate(builder => version = CommitPublishCore(prepared, builder));
            return success(version);
        });
```

4. `WithdrawCoreAsync`'s commit becomes the same shape, calling `CommitWithdrawCore`.
5. Every `Prepare…` method that built `new PropositionOverlay(Scope.Overlay)` now builds
   `new ScopeGenerationBuilder(Scope.Registry, Scope.Current)` and passes it where the overlay went;
   `Scope.PrepareClosure` already takes a builder after Task 4.
6. Replace `_authored` lookups with `Scope.Current.Authored` (reads) and builder writes (writes).
   Delete the `_authored` field. `Propositions`, `Find` and `DocumentJsonOf` read
   `Scope.Active.Authored`; `Dependents` reads `Scope.Active.Graph`.

- [ ] **Step 4: Update `ChangeRequestSet`'s envelope commit**

The governed apply phase is the `rules.Scope.Locked(() => { … })` block at
`ChangeRequestSet.cs:1150`, whose body today calls `propositions!.CommitPublishCore(edit)`,
`propositions!.CommitWithdrawCore(edit)` and `rules.CommitCore(name, publication).Version` in three
loops — three-plus swaps for one envelope. Wrap the existing body in a single `Mutate` and pass the
builder to each call, leaving the loops and the `versions` dictionary exactly as they are:

```csharp
            return rules.Scope.Locked(() =>
            {
                rules.Scope.Mutate(builder =>
                {
                    // …the existing loop bodies, each call taking `builder`:
                    //   versions[name] = PropositionSet.CommitPublishCore(edit, builder);
                    //   PropositionSet.CommitWithdrawCore(edit, builder);
                    //   versions[name] = rules.CommitCore(name, publication, builder).Version;
                });

                // …the existing return, unchanged
            });
```

An envelope means "these edits publish together", and one swap is what makes that true for a reader
as well as for the store. `RuleSet.CommitCore`'s builder parameter arrives in Task 7, so if the
compiler demands it, land this step in the same commit as that task.

- [ ] **Step 5: Run the tests**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0
```

Expected: PASS, including the new atomicity test.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "refactor(serialization): publish a proposition and its closure in one generation swap"
```

### Task 7: One swap per rule publish

**Files:**
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs`
- Test: `src/Motiv.Serialization.Tests/Rules/RulePublishAtomicityTests.cs`

**Interfaces:**
- Consumes: `BindingScope.Mutate`, `ScopeGenerationBuilder` (Task 4).
- Produces: `RuleSet.CommitCore(string name, IRulePublication publication, ScopeGenerationBuilder builder) : RuleUpdateResult`,
  `RuleSet.Track(RuleBase rule, ScopeGenerationBuilder builder) : void`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RulePublishAtomicityTests
{
    private sealed class NumberRule() : Rule<int, string>("number", Spec.Build((int n) => n > 0).Create("positive"));

    [Fact]
    public async Task Should_publish_a_rule_update_in_one_generation()
    {
        // Arrange
        var registry = new SpecRegistry();
        registry.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var rules = new RuleSet(registry);
        rules.Add(new NumberRule());
        var before = rules.Scope.WriteStamp;

        // Act
        var result = await rules.UpdateAsync("number", """{"not":{"spec":"positive"}}""", 1);

        // Assert — the publication and the graph re-tracking are one world, not two
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        (rules.Scope.WriteStamp - before).ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RulePublishAtomicityTests"
```

Expected: FAIL — the stamp moves by 2 or more (publication, then `Track`).

- [ ] **Step 3: Thread the builder through**

```csharp
    /// <summary>
    /// Commits a prepared publication and re-tracks the rule's graph edges into the same successor,
    /// so the binding and the edges that describe it go live together. Has no failure outcome —
    /// everything a caller can get wrong was decided by the prepare. Assumes
    /// <see cref="BindingScope"/>'s inner monitor is held.
    /// </summary>
    internal RuleUpdateResult CommitCore(
        string name, IRulePublication publication, ScopeGenerationBuilder builder)
    {
        var rule = Find(name)
            ?? throw new InvalidOperationException(
                $"Rule '{name}' is no longer registered, so its prepared publication cannot be committed.");

        // Writing the state also clears the rule's quarantine — see RuleSlot.WithState. A quarantine
        // says "running a compiled default in place of a stored document that would not bind", and a
        // successful publish is exactly what stops that being true.
        publication.ApplyTo(builder);

        // Track reads the rule's *published* document, so it must run after ApplyTo, not before.
        Track(rule, builder);

        return RuleUpdateResult.Updated(publication.Version);
    }

    private void Track(RuleBase rule, ScopeGenerationBuilder builder)
    {
        var node = NodeId.Rule(rule.Name);
        var references = ReferencesOf(rule.DocumentJsonIn(builder));

        if (references.Count == 0)
        {
            builder.Graph.Remove(node);
            builder.Withdraw(node);
            return;
        }

        builder.Graph.Set(node, references);
        builder.Enrol(new RuleParticipant(rule, _options));
    }
```

`IRulePublication` gains `void ApplyTo(ScopeGenerationBuilder builder)` in place of `Commit()`, and
`RuleBase` gains `internal abstract string? DocumentJsonIn(ScopeGenerationBuilder builder);` so
`Track` can read the document the builder is about to publish rather than the live one. Both arrive
fully in Task 8; add just those two members here.

`PersistAndCommitCoreAsync`'s final line becomes:

```csharp
        return Scope.Locked(() =>
        {
            var result = default(RuleUpdateResult);
            Scope.Mutate(builder => result = CommitCore(name, publication, builder));
            return result;
        });
```

`Add` becomes one `Mutate` too: assign the slot, bind the default, write the state and track, all in
one builder — Task 8 fills in the slot assignment.

- [ ] **Step 4: Run the tests**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0 && dotnet build Motiv.slnx
```

Expected: 5,604 + the new tests PASS; build clean on every TFM.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "refactor(serialization): publish a rule update in one generation swap"
```

## Phase 3 — Rules read through the generation

### Task 8: Rule slots

The last per-rule mutable field goes. After this task a rule owns no state at all: it is a name, a
default, a slot number, and the scope that holds worlds.

**Files:**
- Modify: `src/Motiv.Serialization/Rules/RuleBase.cs`
- Modify: `src/Motiv.Serialization/Rules/Rule.cs`, `PolicyRule.cs`, `AsyncRule.cs`, `AsyncPolicyRule.cs`
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs` (`Add`, `Load`, `Apply`)
- Modify: `src/Motiv.Serialization/Propositions/ScopeGenerationBuilder.cs` (add `FindRuleState`)
- Test: `src/Motiv.Serialization.Tests/Rules/RuleSlotTests.cs`

**Interfaces:**
- Consumes: `RuleSlot`, `ScopeGenerationBuilder` (Task 4).
- Produces on `RuleBase`: `internal int Slot { get; }`, `internal void Occupy(BindingScope scope, int slot)`,
  `internal BindingScope Scope { get; }`, `internal IReadOnlyList<RuleError> Quarantine { get; }`,
  `internal abstract object BindDefaultState(RuleSerializer serializer)`,
  `internal abstract object WithVersion(object state, int version)`,
  `internal abstract string? DocumentJsonIn(ScopeGenerationBuilder builder)`.
  `Attach` and `RestoreVersion` are deleted.
- Produces on `IRulePublication`: `void ApplyTo(ScopeGenerationBuilder builder)` replacing `Commit()`.
- Produces on `ScopeGenerationBuilder`: `object? FindRuleState(int slot)`.

**The rule that decides which world a member reads:** *evaluation is pinned, administration is live.*
`Evaluate` reads `Scope.Active` so a pinned decision sees one world. Everything else — `Version`,
`DocumentJson`, `VersionedDocument`, and every `Prepare…` — reads `Scope.Current`, because an
administrative read or a publish must see the truth, not the older world a request happens to be
pinned to.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleSlotTests
{
    private sealed class FirstRule() : Rule<int, string>("first", Spec.Build((int n) => n > 0).Create("positive"));
    private sealed class SecondRule() : Rule<int, string>("second", Spec.Build((int n) => n > 1).Create("big"));

    [Fact]
    public void Should_give_each_rule_a_distinct_stable_slot()
    {
        // Arrange
        var rules = new RuleSet(new SpecRegistry());
        var first = new FirstRule();
        var second = new SecondRule();

        // Act
        rules.Add(first);
        rules.Add(second);

        // Assert — a slot is permanent, so a later Add must not renumber an earlier rule
        first.Slot.ShouldBe(0);
        second.Slot.ShouldBe(1);
        rules.Scope.Current.RuleSlots.Length.ShouldBe(2);
    }

    [Fact]
    public void Should_refuse_to_evaluate_a_rule_that_was_never_added()
    {
        // Arrange
        var rule = new FirstRule();

        // Act & Assert — the message is load-bearing: it is what a developer sees on the mistake
        var exception = Should.Throw<InvalidOperationException>(() => rule.Evaluate(1));
        exception.Message.ShouldContain("has not been bound");
    }

    [Fact]
    public async Task Should_evaluate_through_the_generation_rather_than_the_rule()
    {
        // Arrange
        var registry = new SpecRegistry();
        registry.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var rules = new RuleSet(registry);
        var rule = new FirstRule();
        rules.Add(rule);

        // Act
        await rules.UpdateAsync("first", """{"not":{"spec":"positive"}}""", 1);

        // Assert — the rule holds no state of its own; the swap alone changed what it evaluates
        rule.Evaluate(1).Satisfied.ShouldBeFalse();
        rule.Evaluate(-1).Satisfied.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RuleSlotTests"
```

Expected: compile error — `Slot` does not exist.

- [ ] **Step 3: Rework `RuleBase`**

Delete the `_quarantine` field, the `Quarantine` setter, `Attach` and `RestoreVersion`. Add:

```csharp
    private BindingScope? _scope;

    /// <summary>Where this rule's state lives in every generation. Assigned once, by <see cref="RuleSet.Add"/>.</summary>
    internal int Slot { get; private set; } = -1;

    /// <summary>
    /// The scope holding this rule's worlds. Throws the same message the old unbound-state check gave,
    /// because it is the message a developer sees when they evaluate a rule they never registered.
    /// </summary>
    internal BindingScope Scope =>
        _scope ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    /// <summary>Claims a permanent slot in the scope's generations. Called exactly once, by <see cref="RuleSet.Add"/>.</summary>
    internal void Occupy(BindingScope scope, int slot)
    {
        if (_scope is not null)
            throw new InvalidOperationException($"Rule '{Name}' has already been added to a RuleSet.");

        _scope = scope;
        Slot = slot;
    }

    /// <summary>
    /// Why <see cref="RuleSet.Load"/> could not apply this rule's stored document, or empty. Read out
    /// of the generation rather than held here, so it moves with the binding it describes: a
    /// quarantine that lagged its own binding would report a rule broken after the publish that
    /// repaired it.
    /// </summary>
    internal IReadOnlyList<RuleError> Quarantine =>
        Slot >= 0 && Scope.Current.RuleSlots[Slot] is { } slot ? slot.Quarantine : [];

    /// <summary>Binds the default and produces the state version 1 will publish.</summary>
    internal abstract object BindDefaultState(RuleSerializer serializer);

    /// <summary>
    /// The same state at a different version number — used only by <see cref="RuleSet.Load"/>, to
    /// restore the number the store holds after a stored document has been bound through the ordinary
    /// publish path. Renumbering anywhere else would break the optimistic-concurrency contract.
    /// </summary>
    internal abstract object WithVersion(object state, int version);

    /// <summary>
    /// The document this rule will carry once <paramref name="builder"/> is published. Read from the
    /// builder rather than the live world because <see cref="RuleSet"/> re-tracks graph edges from the
    /// document a publish is *about to* make live.
    /// </summary>
    internal abstract string? DocumentJsonIn(ScopeGenerationBuilder builder);
```

- [ ] **Step 4: Rework `Rule<TModel, TMetadata>`**

Delete `private State? _state`. Replace `Snapshot`, `Attach`, `RestoreVersion`, `Publication` and
`RebindCommit` with:

```csharp
    /// <inheritdoc />
    public override int Version => Live().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Live().DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    /// <remarks>
    /// Reads the <em>pinned</em> world when a <see cref="DecisionSnapshot"/> is open, so several rules
    /// evaluated inside one decision resolve against one published set rather than one each.
    /// </remarks>
    public BooleanResultBase<TMetadata> Evaluate(TModel model) => StateIn(Scope.Active).Spec.Evaluate(model);

    /// <summary>The live state — what an administrative read or a publish must see, pinned or not.</summary>
    private protected State Live() => StateIn(Scope.Current);

    private protected State StateIn(ScopeGeneration generation) =>
        generation.RuleSlots[Slot]?.State as State
        ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    internal sealed override object BindDefaultState(RuleSerializer serializer) => BindDefault(serializer);

    internal sealed override object WithVersion(object state, int version)
    {
        var current = (State)state;
        return new State(current.DocumentJson, version, current.Spec);
    }

    internal sealed override string? DocumentJsonIn(ScopeGenerationBuilder builder) =>
        builder.FindRuleState(Slot) is State state ? state.DocumentJson : null;

    /// <summary>A prepared rebind of this rule, published by writing its state into the successor.</summary>
    private sealed class RebindCommit(Rule<TModel, TMetadata> rule, State replacement) : IRebindCommit
    {
        public void ApplyTo(ScopeGenerationBuilder builder) => builder.SetRuleState(rule.Slot, replacement);
    }

    /// <summary>
    /// A prepared rule change, published by writing its state into the successor generation. No
    /// compare-and-swap: the outer gate serialises whole operations, so no second writer is in flight,
    /// and a CAS could never see a different process anyway. Enforcement lives in the store's
    /// <c>(Name, Version)</c> primary key, which can.
    /// </summary>
    private sealed class Publication(Rule<TModel, TMetadata> rule, State replacement) : IRulePublication
    {
        public int Version => replacement.Version;

        public string? DocumentJson => replacement.DocumentJson;

        public void ApplyTo(ScopeGenerationBuilder builder) => builder.SetRuleState(rule.Slot, replacement);
    }
```

Change every `Snapshot()` call inside `PrepareUpdate`, `PrepareRevert`, `PrepareRebind` and
`VersionedDocument` to `Live()`. Repeat this whole step for `PolicyRule`, `AsyncRule` and
`AsyncPolicyRule` — they each carry their own `State` type and their own copies of these members, and
this codebase keeps that duplication deliberately (CLAUDE.md: "avoid over-DRYing").

- [ ] **Step 5: Add `FindRuleState` to the builder**

```csharp
    /// <summary>The state a slot will carry once this builder is published, or null when it has none.</summary>
    public object? FindRuleState(int slot) =>
        slot >= 0 && slot < _ruleSlots.Length ? _ruleSlots[slot]?.State : null;
```

- [ ] **Step 6: Rework `RuleSet.Add`, `Load` and `Apply`**

`Add` claims a slot and binds inside one `Mutate`:

```csharp
        return Scope.Locked(() =>
        {
            if (_rules.ContainsKey(rule.Name))
                throw new ArgumentException($"A rule is already registered under the name '{rule.Name}'.", nameof(rule));

            var slot = _rules.Count;
            rule.Occupy(Scope, slot);

            object state;
            try
            {
                state = rule.BindDefaultState(_serializer);
            }
            catch (RuleSerializationException ex)
            {
                // Name the failing rule — a startup failure over many rules is otherwise anonymous.
                throw new RuleSerializationException($"Rule '{rule.Name}': {ex.Message}", ex.Errors);
            }

            _rules[rule.Name] = rule;
            Scope.Mutate(builder =>
            {
                builder.SetRuleState(slot, state);
                Track(rule, builder);
            });

            return this;
        });
```

A failed bind throws before `_rules` is written, so a rule whose default does not bind leaves no slot
behind — but `Occupy` has already run, which is why `Occupy` is idempotent-hostile rather than
silent: re-adding the same instance must still fail loudly.

`Load`'s per-head work moves inside one `Mutate` so the whole stored world lands as one swap, and
`Apply` takes the builder:

```csharp
    private IReadOnlyList<RuleError>? Apply(RuleBase rule, StoredRule head, ScopeGenerationBuilder builder)
    {
        var prepared = head.DocumentJson is null
            ? PrepareRevertCore(head.Name, expectedVersion: rule.Version)
            : PrepareUpdateCore(head.Name, head.DocumentJson, expectedVersion: rule.Version);

        if (prepared.Publication is { } publication)
            CommitCore(head.Name, publication, builder);

        // Either way the store's version is authoritative: a restart must not renumber history.
        // Written before the quarantine below, because SetRuleState clears quarantine and this is a
        // state write.
        if (builder.FindRuleState(rule.Slot) is { } state)
            builder.SetRuleState(rule.Slot, rule.WithVersion(state, head.Version));

        if (prepared.Publication is null)
        {
            // The rule stays on its compiled default — a rule must be able to evaluate — but says so
            // rather than reverting silently.
            builder.SetRuleQuarantine(rule.Slot, prepared.Errors);
            return prepared.Errors;
        }

        return null;
    }
```

- [ ] **Step 7: Run the full solution and the all-TFM build**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0 && dotnet build Motiv.slnx
```

Expected: everything PASS. Phase 2 and 3 are complete and no behaviour has changed.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "refactor(serialization): move rule state out of the rule and into the generation"
```

## Phase 4 — The pin

### Task 9: `DecisionSnapshot`

Ticket 20's shared reference fixes N writes; this fixes N reads. Without it, two rules evaluated in
one decision take two independent reads and can still straddle a swap.

**Files:**
- Create: `src/Motiv.Serialization/Propositions/DecisionSnapshot.cs`
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs`, `src/Motiv.Serialization/Propositions/PropositionSet.cs`
  (add `PinSnapshot()`)
- Test: `src/Motiv.Serialization.Tests/Propositions/DecisionSnapshotTests.cs`

**Interfaces:**
- Consumes: `BindingScope.Pin()`, `BindingScope.Active` (Task 4).
- Produces: `public sealed class DecisionSnapshot : IDisposable` with `StoreGeneration Generation`;
  `RuleSet.PinSnapshot() : DecisionSnapshot`; `PropositionSet.PinSnapshot() : DecisionSnapshot`.

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class DecisionSnapshotTests
{
    private sealed class LeftRule() : Rule<int, string>("left", Spec.Build((int n) => n > 0).Create("positive"));
    private sealed class RightRule() : Rule<int, string>("right", Spec.Build((int n) => n > 0).Create("positive"));

    private static RuleSet TwoRules()
    {
        var registry = new SpecRegistry();
        registry.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var rules = new RuleSet(registry);
        rules.Add(new LeftRule());
        rules.Add(new RightRule());
        return rules;
    }

    [Fact]
    public async Task Should_hold_one_world_across_several_evaluations()
    {
        // Arrange
        var rules = TwoRules();
        var left = (LeftRule)rules.Find("left")!;
        var right = (RightRule)rules.Find("right")!;

        // Act — a publish lands between the two evaluations of a single pinned decision
        using var snapshot = rules.PinSnapshot();
        var before = left.Evaluate(1).Satisfied;
        await rules.UpdateAsync("right", """{"not":{"spec":"positive"}}""", 1);
        var after = right.Evaluate(1).Satisfied;

        // Assert — the decision sees the world it opened with, not a mix of two.
        // This is the whole point: a mix is a combination that was never published.
        before.ShouldBeTrue();
        after.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_see_the_new_world_once_the_pin_is_released()
    {
        // Arrange
        var rules = TwoRules();
        var right = (RightRule)rules.Find("right")!;
        using (rules.PinSnapshot())
        {
            await rules.UpdateAsync("right", """{"not":{"spec":"positive"}}""", 1);
        }

        // Act & Assert — a pin is a decision, not a subscription
        right.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_let_the_outer_pin_own_the_lifetime_when_pins_nest()
    {
        // Arrange
        var rules = TwoRules();
        var right = (RightRule)rules.Find("right")!;

        // Act
        using var outer = rules.PinSnapshot();
        using (rules.PinSnapshot())
        {
            await rules.UpdateAsync("right", """{"not":{"spec":"positive"}}""", 1);
        }

        // Assert — disposing the inner pin must not end the decision the outer one opened
        right.Evaluate(1).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_report_the_generation_it_pinned()
    {
        // Arrange
        var rules = TwoRules();

        // Act
        using var snapshot = rules.PinSnapshot();

        // Assert — what the response header stamps
        snapshot.Generation.ShouldBe(rules.Scope.Current.Sequence);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DecisionSnapshotTests"
```

Expected: compile error — `PinSnapshot` does not exist.

- [ ] **Step 3: Write `DecisionSnapshot`**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// One decision's world, held still. Every rule evaluated while this is open resolves against the
/// generation it pinned, so a decision spanning several rules sees a set that really was published
/// together.
/// </summary>
/// <remarks>
/// <para>
/// A single shared generation makes a publish one write instead of many — but a caller evaluating two
/// rules still performs two reads, and a swap can land between them. The result, one rule from the
/// new world and one from the old, is a combination that never existed anywhere: not staleness, which
/// is explicable ("you got yesterday's policy"), but incoherence, which is not. This closes that gap.
/// </para>
/// <para>
/// The pin follows the async flow, so it survives <c>await</c>. Nesting is safe: an inner pin reuses
/// the outer one and disposing it does not end the decision.
/// </para>
/// </remarks>
public sealed class DecisionSnapshot : IDisposable
{
    private readonly IDisposable _pin;

    internal DecisionSnapshot(BindingScope scope)
    {
        _pin = scope.Pin();
        Generation = scope.Active.Sequence;
    }

    /// <summary>Where both stores stood in the pinned world — what a response stamps as its fencing token.</summary>
    public StoreGeneration Generation { get; }

    /// <summary>Releases the pin, unless an outer pin owns the decision.</summary>
    public void Dispose() => _pin.Dispose();
}
```

- [ ] **Step 4: Expose it from both sets**

On `RuleSet` and on `PropositionSet`:

```csharp
    /// <summary>
    /// Pins the current world for the duration of a decision, so several evaluations resolve against
    /// one published set. Dispose to release. Hosts using <c>MapMotivRules</c> get one per request
    /// automatically and need not call this.
    /// </summary>
    public DecisionSnapshot PinSnapshot() => new(Scope);
```

- [ ] **Step 5: Run the tests**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DecisionSnapshotTests"
```

Expected: PASS — 4 tests.

- [ ] **Step 6: Prove the test can fail**

Temporarily change `Rule<,>.Evaluate` to read `Scope.Current` instead of `Scope.Active`, re-run, and
confirm `Should_hold_one_world_across_several_evaluations` goes **red**. Revert the change. A pinning
test that passes without the pin proves nothing — 2A's disclosures are explicit that a deliberate
break must reproduce.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat(serialization): add DecisionSnapshot, so one decision sees one world"
```

## Phase 5 — Refresh

### Task 10: `RefreshAsync`

**Files:**
- Modify: `src/Motiv.Serialization/Propositions/BindingScope.cs` (`Join`, `RefreshAsync`)
- Modify: `src/Motiv.Serialization/Propositions/PropositionSet.cs` (`RebuildInto`, store reads, `RefreshAsync`)
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs` (`RebuildInto`, store reads, `RefreshAsync`)
- Test: `src/Motiv.Serialization.Tests/Rules/RefreshTests.cs`

**Interfaces:**
- Consumes: `RefreshReport` (this task), `BindingScope.TrySwap`/`WriteStamp` (Task 4),
  `IPropositionStore.LoadAsync`/`GetGenerationAsync` (Task 1).
- Produces: `RuleSet.RefreshAsync(CancellationToken) : Task<RefreshReport>`,
  `PropositionSet.RefreshAsync(CancellationToken) : Task<RefreshReport>` — both routing to
  `BindingScope.RefreshAsync`, which rebuilds whatever sets have joined the scope.

- [ ] **Step 1: Write the failing tests**

```csharp
using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RefreshTests
{
    private sealed class NumberRule() : Rule<int, string>("number", Spec.Build((int n) => n > 0).Create("positive"));

    /// <summary>Two independent replicas over one store — the shape a second pod has.</summary>
    private static (RuleSet Rules, NumberRule Rule) Replica(IRuleStore store)
    {
        var registry = new SpecRegistry();
        registry.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var rules = new RuleSet(registry, store);
        var rule = new NumberRule();
        rules.Add(rule);
        rules.Load();
        return (rules, rule);
    }

    [Fact]
    public async Task Should_converge_a_second_replica_on_the_first_replicas_publish()
    {
        // Arrange — one store, two replicas, as two pods behind a load balancer
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        var (b, ruleB) = Replica(store);

        // Act
        await a.UpdateAsync("number", """{"not":{"spec":"positive"}}""", 1);
        var report = await b.RefreshAsync(default);

        // Assert — B was serving yesterday's policy and now is not
        report.Outcome.ShouldBe(RefreshOutcome.Applied);
        b.FindEntry("number")!.Version.ShouldBe(2);
        ruleB.Evaluate(1).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_do_nothing_when_the_store_has_not_moved()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        var stamp = a.Scope.WriteStamp;

        // Act
        var report = await a.RefreshAsync(default);

        // Assert — the cheap path: no rebuild, no swap, no allocation of a world
        report.Outcome.ShouldBe(RefreshOutcome.Unchanged);
        a.Scope.WriteStamp.ShouldBe(stamp);
    }

    [Fact]
    public async Task Should_keep_serving_when_a_stored_document_would_regress_a_live_rule()
    {
        // Arrange — B's build does not have the spec A's new document references
        var store = new InMemoryRuleStore();
        var (a, _) = Replica(store);
        await a.UpdateAsync("number", """{"not":{"spec":"positive"}}""", 1);

        var registryWithoutPositive = new SpecRegistry();
        registryWithoutPositive.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var b = new RuleSet(registryWithoutPositive, store);
        var ruleB = new NumberRule();
        b.Add(ruleB);
        b.Load();

        // A publishes something referencing a spec only its own build has
        await a.UpdateAsync("number", """{"spec":"only-in-the-new-build"}""", 2);

        // Act
        var report = await b.RefreshAsync(default);

        // Assert — B keeps the approved behaviour it was serving rather than dropping to the
        // compiled default, and says why
        report.Outcome.ShouldBe(RefreshOutcome.Aborted);
        report.Regressions.ShouldNotBeEmpty();
        report.Regressions[0].Name.ShouldBe("number");
        ruleB.Evaluate(1).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_discard_a_rebuild_that_a_publish_beat_to_the_swap()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var (a, ruleA) = Replica(store);
        await a.UpdateAsync("number", """{"not":{"spec":"positive"}}""", 1);

        // Act — a local publish lands while a refresh is mid-flight, simulated by moving the world
        // between the stamp being taken and the swap being attempted
        var stamp = a.Scope.WriteStamp;
        var successor = a.Scope.Current;
        await a.UpdateAsync("number", """{"spec":"positive"}""", 2);
        var swapped = a.Scope.Locked(() => a.Scope.TrySwap(successor, stamp));

        // Assert — the publish survives; the stale rebuild does not overwrite it
        swapped.ShouldBeFalse();
        ruleA.Evaluate(1).Satisfied.ShouldBeTrue();
        a.FindEntry("number")!.Version.ShouldBe(3);
    }

    [Fact]
    public async Task Should_read_only_the_generation_when_nothing_has_moved()
    {
        // Arrange
        var store = new CountingRuleStore(new InMemoryRuleStore());
        var (a, _) = Replica(store);
        store.Loads = 0;

        // Act
        await a.RefreshAsync(default);

        // Assert — a poll that loaded the store would defeat the entire point: every replica does
        // this on a timer
        store.Loads.ShouldBe(0);
        store.GenerationReads.ShouldBe(1);
    }

    private sealed class CountingRuleStore(IRuleStore inner) : IRuleStore
    {
        public int Loads { get; set; }
        public int GenerationReads { get; private set; }

        public IReadOnlyList<StoredRule> Load() => inner.Load();

        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct)
        {
            Loads++;
            return inner.LoadAsync(ct);
        }

        public Task<long> GetGenerationAsync(CancellationToken ct)
        {
            GenerationReads++;
            return inner.GetGenerationAsync(ct);
        }

        public Task<RuleAppendResult> AppendAsync(IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            inner.AppendAsync(versions, ct);

        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            inner.HistoryAsync(name, ct);
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RefreshTests"
```

Expected: compile error — `RefreshAsync` does not exist.

**A clarification this task locks in.** The design says a refresh that cannot bind a row aborts
entirely. Read literally that stalls a replica forever on a row that never bound in the first place —
a hand-edited document quarantined at startup would abort every subsequent refresh, and the replica
would never converge on anything. What abort exists to protect is a *live, correct, approved* binding
from regressing to a compiled default. So the rule is sharper than "anything fails":

> A refresh aborts when applying it would quarantine something that is **not quarantined today**.
> A row that is already quarantined carries its quarantine into the new world and blocks nothing.

`Regressions` are the blocking kind. `Quarantined` are the carried kind, reported so an operator can
still see them.

- [ ] **Step 3: Write the report types**

Create `src/Motiv.Serialization/Rules/RefreshReport.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>What a refresh did.</summary>
public enum RefreshOutcome
{
    /// <summary>Neither store had moved, so nothing was rebuilt. The common case, every tick.</summary>
    Unchanged,

    /// <summary>A new world was built and swapped in.</summary>
    Applied,

    /// <summary>
    /// The rebuild would have regressed a live binding to its compiled default, so it was discarded
    /// and the current world kept.
    /// </summary>
    Aborted,

    /// <summary>A publish landed while the rebuild was being built, so it was discarded. Retry.</summary>
    Contended
}

/// <summary>One node a refresh could not bind, and why.</summary>
/// <param name="Name">The rule or proposition name.</param>
/// <param name="Kind">"rule" or "proposition", matching <c>NodeId.KindLabel</c>.</param>
/// <param name="Errors">Why it would not bind.</param>
public sealed record RefreshFailure(string Name, string Kind, IReadOnlyList<RuleError> Errors);

/// <summary>
/// The outcome of a <see cref="RuleSet.RefreshAsync"/>: what happened, where the stores stood, and
/// anything that would not bind.
/// </summary>
/// <remarks>
/// <see cref="Regressions"/> and <see cref="Quarantined"/> are different in kind, not in degree. A
/// regression is a document that binds in the world being served and would not bind in the world
/// being built — applying it would drop a live, approved rule back to compiled behaviour nobody
/// approved, so the refresh refuses. Something already quarantined has no live binding to protect, so
/// it is carried forward and reported without blocking convergence.
/// </remarks>
public sealed class RefreshReport
{
    private RefreshReport(
        RefreshOutcome outcome, StoreGeneration generation,
        IReadOnlyList<RefreshFailure> regressions, IReadOnlyList<RefreshFailure> quarantined)
    {
        Outcome = outcome;
        Generation = generation;
        Regressions = regressions;
        Quarantined = quarantined;
    }

    /// <summary>What happened.</summary>
    public RefreshOutcome Outcome { get; }

    /// <summary>Where both stores stood in the world now being served.</summary>
    public StoreGeneration Generation { get; }

    /// <summary>What would have regressed, and therefore why an <see cref="RefreshOutcome.Aborted"/> refresh aborted.</summary>
    public IReadOnlyList<RefreshFailure> Regressions { get; }

    /// <summary>What was carried forward still quarantined. Never blocks a refresh.</summary>
    public IReadOnlyList<RefreshFailure> Quarantined { get; }

    /// <summary>Whether this replica converged, or is knowingly serving an older world.</summary>
    public bool IsConverged => Outcome is RefreshOutcome.Applied or RefreshOutcome.Unchanged;

    public static RefreshReport Unchanged(StoreGeneration generation) =>
        new(RefreshOutcome.Unchanged, generation, [], []);

    public static RefreshReport Applied(StoreGeneration generation, IReadOnlyList<RefreshFailure> quarantined) =>
        new(RefreshOutcome.Applied, generation, [], quarantined);

    public static RefreshReport Aborted(StoreGeneration generation, IReadOnlyList<RefreshFailure> regressions) =>
        new(RefreshOutcome.Aborted, generation, regressions, []);

    public static RefreshReport Contended(StoreGeneration generation) =>
        new(RefreshOutcome.Contended, generation, [], []);
}
```

- [ ] **Step 4: Let the sets join the scope**

Both sets already receive the scope in their constructors. Add to each constructor body
`scope.Join(this);`, and to `BindingScope`:

```csharp
    private PropositionSet? _propositions;
    private RuleSet? _rules;

    /// <summary>Records the proposition set that rebuilds this scope's authored half on a refresh.</summary>
    public void Join(PropositionSet propositions) => _propositions = propositions;

    /// <summary>Records the rule set that rebuilds this scope's rule half on a refresh.</summary>
    public void Join(RuleSet rules) => _rules = rules;
```

- [ ] **Step 5: Write `BindingScope.RefreshAsync`**

```csharp
    /// <summary>
    /// Rebuilds the whole world from both stores and swaps it in, if either store has moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately does <em>not</em> take the outer gate. Holding it across the store read would let
    /// a slow store block every publish, which is the hazard the async write contract exists to avoid.
    /// Instead the rebuild runs unlocked against a builder nobody else can see, and the swap validates
    /// under the monitor that <see cref="WriteStamp"/> has not moved — a compare-and-set on the world,
    /// mirroring how the store's <c>(Name, Version)</c> primary key guards a row.
    /// </para>
    /// <para>
    /// Two concurrent refreshes are safe and uninteresting: both build, one swaps, the other is told
    /// it was contended and retries.
    /// </para>
    /// </remarks>
    public async Task<RefreshReport> RefreshAsync(CancellationToken cancellationToken)
    {
        // Three attempts, then leave it to the next tick. A refresh that loses the swap has been
        // overtaken by a publish, which is a world at least as new as the one it was building.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var sequence = new StoreGeneration(
                _rules is null ? 0 : await _rules.StoreGenerationAsync(cancellationToken).ConfigureAwait(false),
                _propositions is null ? 0 : await _propositions.StoreGenerationAsync(cancellationToken).ConfigureAwait(false));

            // Snapshot() reads the stamp and the world in the only safe order — see its own doc.
            // Reading them separately here, in the wrong order, would hand this rebuild a stale
            // world with a fresh stamp, so its swap would succeed and silently overwrite a publish.
            var (stamp, current) = Snapshot();
            if (!sequence.MovedFrom(current.Sequence))
                return RefreshReport.Unchanged(current.Sequence);

            var builder = new ScopeGenerationBuilder(Registry, current.RuleSlots.Length);
            var regressions = new List<RefreshFailure>();
            var quarantined = new List<RefreshFailure>();

            // Propositions first: a rule document may reference an authored proposition, so the
            // authored layer has to be in the builder before any rule binds against it.
            if (_propositions is not null)
                await _propositions.RebuildIntoAsync(builder, current, regressions, quarantined, cancellationToken)
                    .ConfigureAwait(false);

            if (_rules is not null)
                await _rules.RebuildIntoAsync(builder, current, regressions, quarantined, cancellationToken)
                    .ConfigureAwait(false);

            if (regressions.Count > 0)
                return RefreshReport.Aborted(current.Sequence, regressions);

            builder.SetSequence(sequence);

            if (Locked(() => TrySwap(builder.Build(), stamp)))
                return RefreshReport.Applied(sequence, quarantined);
        }

        return RefreshReport.Contended(Current.Sequence);
    }
```

- [ ] **Step 6: Write the two `RebuildIntoAsync` halves**

On `PropositionSet`:

```csharp
    /// <summary>Where the proposition store stands. One scalar; polled on a timer.</summary>
    internal Task<long> StoreGenerationAsync(CancellationToken cancellationToken) =>
        _store.GetGenerationAsync(cancellationToken);

    /// <summary>
    /// Rebuilds the authored layer into <paramref name="builder"/> from the store. Mirrors
    /// <see cref="Load"/> step for step — read rows, order by dependency, quarantine cycles, bind —
    /// with one difference: a row that will not bind is a <em>regression</em> when the world being
    /// served has it bound, and merely carried when it does not.
    /// </summary>
    internal async Task RebuildIntoAsync(
        ScopeGenerationBuilder builder, ScopeGeneration current,
        List<RefreshFailure> regressions, List<RefreshFailure> quarantined,
        CancellationToken cancellationToken)
    {
        var rows = await _store.LoadAsync(cancellationToken).ConfigureAwait(false) ?? [];
        // Everything below is CPU over a builder nobody else can see, so no lock is held or needed.
        var candidates = CandidatesFrom(rows);
        var cycles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var ordered = OrderByDependency(candidates, cycles);

        foreach (var pair in cycles)
        {
            candidates[pair.Key].Errors.Add(new RuleError("$", RuleErrorCode.CycleDetected,
                $"the stored proposition '{pair.Key}' cannot be bound: {string.Join(" → ", pair.Value)} " +
                "forms a reference cycle"));
        }

        foreach (var name in ordered)
            RebuildOne(candidates[name], builder, current, regressions, quarantined);
    }
```

`CandidatesFrom(IReadOnlyList<StoredProposition> rows)` is `ReadCandidates`'s body with the store read
lifted out as a parameter — refactor `ReadCandidates` into `CandidatesFrom(_store.Load())` so `Load`
and the rebuild share one copy and cannot drift.

`RebuildOne` is `LoadOne` writing to the builder, with the regression check:

```csharp
    private void RebuildOne(
        LoadCandidate candidate, ScopeGenerationBuilder builder, ScopeGeneration current,
        List<RefreshFailure> regressions, List<RefreshFailure> quarantined)
    {
        var stored = candidate.Stored;
        var errors = new List<RuleError>(candidate.Errors);

        AuthoredProposition? bound = null;
        if (errors.Count == 0)
        {
            var attempt = new AuthoredProposition(
                this, stored.Name, stored.ModelType, stored.DocumentJson, stored.Version,
                stored.Description, bound: null, quarantine: [], references: candidate.References);

            if (attempt.PrepareRebind(builder.Source, errors) is { } commit)
            {
                commit.ApplyTo(builder);
                builder.Graph.Set(attempt.Node, attempt.References);
                builder.Enrol(builder.FindAuthored(stored.Name)!);
                bound = builder.FindAuthored(stored.Name);
            }
        }

        if (bound is not null)
            return;

        var failure = new RefreshFailure(stored.Name, "proposition", errors);

        // Was this bound in the world being served? Then applying the rebuild would take a working,
        // approved proposition away, and every dependent with it. Refuse.
        if (current.Authored.TryGetValue(stored.Name, out var live) && live.Bound is not null)
        {
            regressions.Add(failure);
            return;
        }

        var carried = new AuthoredProposition(
            this, stored.Name, stored.ModelType, stored.DocumentJson, stored.Version,
            stored.Description, bound: null, quarantine: errors, references: candidate.References);
        builder.SetAuthored(carried);
        quarantined.Add(failure);
    }
```

On `RuleSet`, the mirror:

```csharp
    /// <summary>Where the rule store stands. One scalar; polled on a timer.</summary>
    internal Task<long> StoreGenerationAsync(CancellationToken cancellationToken) =>
        _store.GetGenerationAsync(cancellationToken);

    /// <summary>
    /// Rebuilds every rule into <paramref name="builder"/>: compiled default first, then the stored
    /// head over it, which is the order <see cref="Add"/> and <see cref="Load"/> already establish
    /// between them. A head that will not bind is a <em>regression</em> when the rule is bound and
    /// unquarantined in the world being served, and merely carried when it is not.
    /// </summary>
    internal async Task RebuildIntoAsync(
        ScopeGenerationBuilder builder, ScopeGeneration current,
        List<RefreshFailure> regressions, List<RefreshFailure> quarantined,
        CancellationToken cancellationToken)
    {
        var heads = await _store.LoadAsync(cancellationToken).ConfigureAwait(false) ?? [];

        // A serializer over the *prospective* world: rule documents resolve authored propositions,
        // and the builder already holds the authored layer this refresh is rebuilding.
        var serializer = new RuleSerializer(builder.Source, _options);

        // Bind every default first, so a rule with no stored head is complete and a rule with one has
        // something to be applied over.
        foreach (var rule in _rules.Values)
        {
            builder.SetRuleState(rule.Slot, rule.BindDefaultState(serializer));
            TrackFromDefault(rule, builder);
        }

        foreach (var head in heads)
        {
            // A row with no usable name has nowhere to be recorded, and history outlives the code
            // that produced it — an orphan is not a fault. Both are skipped, as Load skips them.
            if (head?.Name is null || Find(head.Name) is not { } rule)
                continue;

            var prepared = head.DocumentJson is null
                ? rule.PrepareRevert(serializer, expectedVersion: 1)
                : rule.PrepareUpdate(serializer, head.DocumentJson, expectedVersion: 1);

            if (prepared.Publication is { } publication)
            {
                publication.ApplyTo(builder);
                Track(rule, builder);
                if (builder.FindRuleState(rule.Slot) is { } state)
                    builder.SetRuleState(rule.Slot, rule.WithVersion(state, head.Version));
                continue;
            }

            var failure = new RefreshFailure(head.Name, "rule", prepared.Errors);

            // Bound and healthy in the world being served? Then applying this rebuild would drop a
            // live, approved rule back to compiled behaviour nobody approved. Refuse the whole thing.
            var live = current.RuleSlots[rule.Slot];
            if (live is not null && live.Quarantine.Count == 0 && rule.DocumentJson is not null)
            {
                regressions.Add(failure);
                continue;
            }

            if (builder.FindRuleState(rule.Slot) is { } fallback)
                builder.SetRuleState(rule.Slot, rule.WithVersion(fallback, head.Version));
            builder.SetRuleQuarantine(rule.Slot, prepared.Errors);
            quarantined.Add(failure);
        }
    }

    /// <summary>
    /// <see cref="Track"/> for a rule that has just been rebound to its compiled default: the
    /// references come from the default document rather than from the builder, because the builder's
    /// state for this slot is the default and reading it back would be the same answer by a longer
    /// route.
    /// </summary>
    private void TrackFromDefault(RuleBase rule, ScopeGenerationBuilder builder)
    {
        var node = NodeId.Rule(rule.Name);
        var references = ReferencesOf(rule.Default.DocumentJson);

        if (references.Count == 0)
        {
            builder.Graph.Remove(node);
            builder.Withdraw(node);
            return;
        }

        builder.Graph.Set(node, references);
        builder.Enrol(new RuleParticipant(rule, _options));
    }
```

`expectedVersion: 1` is correct and not a bug: the builder's state for this slot is the freshly bound
default, which is version 1 by construction. The store's version is written afterwards by
`WithVersion`, exactly as `Load` does — a restart, or a refresh, must not renumber history.

- [ ] **Step 7: Expose `RefreshAsync` on both sets**

```csharp
    /// <summary>
    /// Rebuilds this replica's world from the stores, if either has moved since it was last built.
    /// </summary>
    /// <remarks>
    /// The whole world is rebuilt, not a part of it: a row that binds on one pass and quarantines on
    /// the next has already written its overlay entry and graph edges, and the quarantine path clears
    /// neither — which is why <see cref="Load"/> refuses to run twice and this exists instead. A scope
    /// shared with a <see cref="PropositionSet"/> rebuilds both halves whichever set you call.
    /// </remarks>
    public Task<RefreshReport> RefreshAsync(CancellationToken cancellationToken = default) =>
        Scope.RefreshAsync(cancellationToken);
```

Add the same to `PropositionSet`, with `RuleSet` and `PropositionSet` swapped in the prose.

- [ ] **Step 8: Run the tests**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~RefreshTests"
```

Expected: PASS — 5 tests.

- [ ] **Step 9: Run the full solution and the all-TFM build**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0 && dotnet build Motiv.slnx
```

- [ ] **Step 10: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat(serialization): add RefreshAsync, so a live replica can converge on another's write"
```

## Phase 6 — Hosting

### Task 11: The opt-in poller

`Motiv.Serialization` is a plain library and cannot own a lifecycle. The hosting package already
registers singletons and maps endpoints, so the timer lives here — and adopters get convergence by
configuration rather than by each writing the same loop.

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/MotivRefreshOptions.cs`
- Create: `src/Motiv.Serialization.AspNetCore/MotivRefreshService.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/MotivRefreshServiceTests.cs`

**Interfaces:**
- Consumes: `RuleSet.RefreshAsync` (Task 10).
- Produces: `public sealed class MotivRefreshOptions { TimeSpan Interval { get; set; } }` defaulting to
  5 seconds; `internal sealed class MotivRefreshService : BackgroundService`;
  `MotivRulesBuilder.AddRefresh(TimeSpan? interval = null) : MotivRulesBuilder`;
  `MotivRefreshService.LastReport : RefreshReport?` (read by Task 13's health check).

- [ ] **Step 1: Write the failing test**

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Motiv.Serialization;

namespace Motiv.Serialization.AspNetCore.Tests;

public class MotivRefreshServiceTests
{
    private sealed class NumberRule() : Rule<int, string>("number", Spec.Build((int n) => n > 0).Create("positive"));

    private static RuleSet Replica(IRuleStore store)
    {
        var registry = new SpecRegistry();
        registry.Add(Spec.Build((int n) => n > 0).Create("positive"), "positive");
        var rules = new RuleSet(registry, store);
        rules.Add(new NumberRule());
        rules.Load();
        return rules;
    }

    [Fact]
    public async Task Should_converge_a_replica_without_anyone_calling_refresh()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);
        var service = new MotivRefreshService(
            b, new MotivRefreshOptions { Interval = TimeSpan.FromMilliseconds(20) },
            NullLogger<MotivRefreshService>.Instance);

        await a.UpdateAsync("number", """{"not":{"spec":"positive"}}""", 1);

        // Act
        await service.StartAsync(default);
        try
        {
            // Poll for the outcome rather than sleeping a fixed time: a fixed sleep is either flaky
            // or slow, and this loop is bounded so a hang goes red rather than hanging CI.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
            while (b.FindEntry("number")!.Version == 1 && DateTimeOffset.UtcNow < deadline)
                await Task.Delay(10);
        }
        finally
        {
            await service.StopAsync(default);
        }

        // Assert
        b.FindEntry("number")!.Version.ShouldBe(2);
        service.LastReport!.Outcome.ShouldBe(RefreshOutcome.Applied);
    }

    [Fact]
    public async Task Should_survive_a_store_that_throws_and_keep_polling()
    {
        // Arrange — the store is fine at startup and fails afterwards, which is the realistic
        // outage: the database was reachable when the pod booted and is not any more
        var store = new FailAfterStartupRuleStore();
        var rules = Replica(store);
        store.Failing = true;

        var service = new MotivRefreshService(
            rules, new MotivRefreshOptions { Interval = TimeSpan.FromMilliseconds(10) },
            NullLogger<MotivRefreshService>.Instance);

        // Act — several ticks against a store that throws
        await service.StartAsync(default);
        await Task.Delay(100);
        await service.StopAsync(default);

        // Assert — the loop absorbed every failure. Taking the host down over an unreachable store
        // would trade a stale replica for no replica.
        service.ExecuteTask!.IsFaulted.ShouldBeFalse();
        rules.FindEntry("number")!.Version.ShouldBe(1);
    }

    private sealed class FailAfterStartupRuleStore : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();

        public bool Failing { get; set; }

        public IReadOnlyList<StoredRule> Load() => _inner.Load();

        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) =>
            Failing ? throw new InvalidOperationException("store down") : _inner.LoadAsync(ct);

        public Task<long> GetGenerationAsync(CancellationToken ct) =>
            Failing ? throw new InvalidOperationException("store down") : _inner.GetGenerationAsync(ct);

        public Task<RuleAppendResult> AppendAsync(IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            Failing ? throw new InvalidOperationException("store down") : _inner.AppendAsync(versions, ct);

        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            Failing ? throw new InvalidOperationException("store down") : _inner.HistoryAsync(name, ct);
    }
}
```

`BackgroundService.ExecuteTask` is the public property that exposes whether the loop faulted, which is
the assertion that actually distinguishes "absorbed the failure" from "died quietly".

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~MotivRefreshServiceTests"
```

Expected: compile error — `MotivRefreshService` does not exist.

- [ ] **Step 3: Write the options**

```csharp
namespace Motiv.Serialization.AspNetCore;

/// <summary>How often this replica checks whether another one has published.</summary>
/// <remarks>
/// The interval bounds how long two replicas can disagree, and is also the window in which a
/// cross-process write can be lost — see ticket 21's note that the version primary key closes the
/// lost-update hole, not the visibility one. Shorter is fresher and costs one scalar read per replica
/// per tick.
/// </remarks>
public sealed class MotivRefreshOptions
{
    /// <summary>How long to wait between polls. Defaults to five seconds.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
}
```

- [ ] **Step 4: Write the service**

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Polls the stores' generation and rebuilds this replica when it moves. Opt-in: a single-replica
/// host does not need it, and starting a timer nobody asked for is not a default worth having.
/// </summary>
/// <remarks>
/// The loop never throws. A store outage, a cancelled rebuild, or a rebuild that lost its swap are
/// all ordinary outcomes of a background poller, and taking the host down over any of them would
/// trade a stale replica for no replica.
/// </remarks>
internal sealed class MotivRefreshService(
    RuleSet rules, MotivRefreshOptions options, ILogger<MotivRefreshService> logger)
    : BackgroundService
{
    /// <summary>The most recent outcome, for the health check to report. Null until the first tick.</summary>
    public RefreshReport? LastReport { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Interval, stoppingToken).ConfigureAwait(false);
                var report = await rules.RefreshAsync(stoppingToken).ConfigureAwait(false);
                LastReport = report;

                if (report.Outcome == RefreshOutcome.Applied)
                {
                    logger.LogInformation(
                        "Motiv rebuilt on generation {Generation}; {Quarantined} stored document(s) carried quarantined.",
                        report.Generation.ToToken(), report.Quarantined.Count);
                }
                else if (report.Outcome == RefreshOutcome.Aborted)
                {
                    // Loud, and at Error: this replica is knowingly serving an older world, and will
                    // keep doing so until the store or this build changes.
                    logger.LogError(
                        "Motiv refresh aborted: {Count} stored document(s) would regress a live binding, " +
                        "so generation {Generation} is still being served. First: {Name}.",
                        report.Regressions.Count, report.Generation.ToToken(), report.Regressions[0].Name);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Motiv refresh failed; keeping the current world and retrying.");
            }
        }
    }
}
```

- [ ] **Step 5: Register it**

In `MotivRulesServiceCollectionExtensions`, on `MotivRulesBuilder`:

```csharp
    /// <summary>
    /// Polls the stores and rebuilds this replica when another one publishes. Opt-in, because a
    /// single-replica host does not need it.
    /// </summary>
    /// <param name="interval">How often to poll, or null for the five-second default.</param>
    public MotivRulesBuilder AddRefresh(TimeSpan? interval = null)
    {
        var options = new MotivRefreshOptions();
        if (interval is { } value)
            options.Interval = value;

        Services.AddSingleton(options);
        Services.AddSingleton<MotivRefreshService>();
        Services.AddHostedService(provider => provider.GetRequiredService<MotivRefreshService>());
        return this;
    }
```

Registering the singleton separately and resolving it for the hosted service is deliberate: Task 13's
health check needs the same instance, and `AddHostedService<T>()` would give it a second one.

- [ ] **Step 6: Run the tests, then commit**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 && dotnet build Motiv.slnx
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "feat(aspnetcore): add the opt-in refresh poller"
```

### Task 12: Per-request pin and the generation header

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/MotivGenerationFilter.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (register it from `MapMotivRules`)
- Test: `src/Motiv.Serialization.AspNetCore.Tests/MotivGenerationHeaderTests.cs`

**Interfaces:**
- Consumes: `RuleSet.PinSnapshot()` (Task 9), `StoreGeneration.ToToken()` (Task 2).
- Produces: `internal sealed class MotivGenerationFilter` and the public constant
  `public const string GenerationHeader = "Motiv-Generation";` on `MotivRulesEndpoints`.

- [ ] **Step 1: Write the failing test**

Follow the existing `MotivRulesEndpointsTests` fixture pattern in this project (a `WebApplicationFactory`
or minimal host that calls `MapMotivRules`), and assert:

```csharp
    [Fact]
    public async Task Should_stamp_the_generation_on_every_response()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        var response = await client.GetAsync("/api/rules/rules");

        // Assert — the fencing token a client compares against what it has already seen
        response.Headers.TryGetValues(MotivRulesEndpoints.GenerationHeader, out var values).ShouldBeTrue();
        StoreGeneration.TryParseToken(values!.Single(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_move_the_stamped_generation_after_a_publish()
    {
        // Arrange
        using var client = CreateClient();
        var before = await ReadGeneration(client);

        // Act
        await client.PutAsJsonAsync("/api/rules/rules/number", new { document = new { spec = "positive" }, baseVersion = 1 });
        var after = await ReadGeneration(client);

        // Assert
        after.MovedFrom(before).ShouldBeTrue();
    }
```

Add the `ReadGeneration` helper alongside, reading the header off a `GET /api/rules/rules` response
and parsing it with `StoreGeneration.TryParseToken`.

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~MotivGenerationHeaderTests"
```

Expected: FAIL — no such header.

- [ ] **Step 3: Write the filter**

An endpoint filter, not middleware: `MapMotivRules` already builds a route group
(`var group = endpoints.MapGroup(basePath);` at `MotivRulesEndpoints.cs:59`), so a filter on that
group covers exactly the Motiv routes and nothing else. Middleware would have to be installed by the
host in the right pipeline position, which is a second thing an adopter can get wrong.

```csharp
using Microsoft.AspNetCore.Http;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Pins one world for the duration of a request and stamps which one it was on the response.
/// </summary>
/// <remarks>
/// <para>
/// The pin is what makes a request coherent: a handler evaluating two rules would otherwise take two
/// independent reads and could see a combination that was never published. A request is the natural
/// unit — it is one decision from the caller's point of view.
/// </para>
/// <para>
/// The header is the same fact facing outward. A client that has seen <c>r7.p3</c> and then receives
/// <c>r5.p3</c> has been routed to a replica serving an older world. It cannot fix that on its own,
/// but it can know — which is the whole difference between eventual consistency and silent
/// divergence.
/// </para>
/// </remarks>
internal sealed class MotivGenerationFilter(Func<DecisionSnapshot> pin) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var snapshot = pin();

        // Stamped through OnStarting so it lands on whatever the endpoint returns — including the
        // error paths, where knowing which world refused you is worth as much as knowing which world
        // served you.
        var response = context.HttpContext.Response;
        var token = snapshot.Generation.ToToken();
        response.OnStarting(static state =>
        {
            var carried = ((HttpResponse Response, string Token))state;
            carried.Response.Headers[MotivRulesEndpoints.GenerationHeader] = carried.Token;
            return Task.CompletedTask;
        }, (response, token));

        return await next(context).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Register it from `MapMotivRules`**

Add the constant to `MotivRulesEndpoints`:

```csharp
    /// <summary>The response header carrying the world a response was served from.</summary>
    public const string GenerationHeader = "Motiv-Generation";
```

and immediately after the `group.AllowAnonymous()` / `group.RequireAuthorization()` branch at
`MotivRulesEndpoints.cs:66`, add:

```csharp
        // Either set pins the same scope when they share one; a registry-only mount has no scope to
        // pin and no generation to report, so it is left alone.
        Func<DecisionSnapshot>? pin =
            rules is not null ? rules.PinSnapshot
            : propositions is not null ? propositions.PinSnapshot
            : null;

        if (pin is not null)
            group.AddEndpointFilter(new MotivGenerationFilter(pin));
```

- [ ] **Step 5: Run the tests, then commit**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 && dotnet build Motiv.slnx
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "feat(aspnetcore): pin one world per request and stamp it on the response"
```

### Task 13: The health check

A stalled replica must be an operational fact, not a log-grep. This is what makes abort-on-regression
a tolerable policy rather than a silent one.

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/MotivRefreshHealthCheck.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/MotivRefreshHealthCheckTests.cs`

**Interfaces:**
- Consumes: `MotivRefreshService.LastReport` (Task 11).
- Produces: `internal sealed class MotivRefreshHealthCheck : IHealthCheck`, registered as
  `"motiv-refresh"` by `AddRefresh`.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public async Task Should_report_degraded_when_the_last_refresh_aborted()
    {
        // Arrange
        var check = new MotivRefreshHealthCheck(ServiceWithLastReport(
            RefreshReport.Aborted(new StoreGeneration(4, 1),
                [new RefreshFailure("number", "rule", [])])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — degraded, not unhealthy: the replica is serving correctly, just not the newest world
        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Description.ShouldContain("number");
        result.Data["generation"].ShouldBe("r4.p1");
    }

    [Fact]
    public async Task Should_report_healthy_when_the_replica_is_converged()
    {
        // Arrange
        var check = new MotivRefreshHealthCheck(ServiceWithLastReport(
            RefreshReport.Applied(new StoreGeneration(4, 1), [])));

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }
```

`ServiceWithLastReport` constructs a `MotivRefreshService` and drives one tick, or — simpler — the
health check takes an abstraction the test can satisfy directly. Prefer making
`MotivRefreshHealthCheck` depend on `MotivRefreshService` and giving `LastReport` an internal setter
used only by the service and the tests; the project already has `InternalsVisibleTo` for its test
assembly.

- [ ] **Step 2: Run it to verify it fails**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter "FullyQualifiedName~MotivRefreshHealthCheckTests"
```

Expected: compile error — `MotivRefreshHealthCheck` does not exist.

- [ ] **Step 3: Write it**

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Whether this replica is converging. Degraded — not unhealthy — when a refresh aborted: the
/// replica is serving a coherent, approved world correctly, it just is not the newest one, and
/// taking it out of rotation would turn a stale pod into a missing pod.
/// </summary>
internal sealed class MotivRefreshHealthCheck(MotivRefreshService service) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var report = service.LastReport;

        if (report is null)
            return Task.FromResult(HealthCheckResult.Healthy("Motiv has not polled yet."));

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["generation"] = report.Generation.ToToken(),
            ["outcome"] = report.Outcome.ToString()
        };

        if (report.Outcome != RefreshOutcome.Aborted)
            return Task.FromResult(HealthCheckResult.Healthy($"Motiv is on generation {report.Generation.ToToken()}.", data));

        var names = string.Join(", ", report.Regressions.Select(failure => failure.Name));
        return Task.FromResult(HealthCheckResult.Degraded(
            $"Motiv is stuck on generation {report.Generation.ToToken()}: {names} would regress a live binding.",
            data: data));
    }
}
```

- [ ] **Step 4: Register it in `AddRefresh`**

```csharp
        Services.AddHealthChecks().AddCheck<MotivRefreshHealthCheck>("motiv-refresh");
```

- [ ] **Step 5: Run the tests, then commit**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 && dotnet build Motiv.slnx
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "feat(aspnetcore): report a stalled replica as degraded rather than as a log line"
```

## Phase 7 — The client

### Task 14: Monotonic-read detection in `@motiv-rules/core`

**Files:**
- Modify: `ui/packages/rules-core/src/client.ts`
- Test: `ui/packages/rules-core/src/client.test.ts` (append; create if absent)

**Interfaces:**
- Consumes: the `Motiv-Generation` header (Task 12).
- Produces: `RulesApiClientOptions.onStaleGeneration?: (observed: StoreGeneration, highest: StoreGeneration) => void`;
  `RulesApiClient.generation: StoreGeneration | undefined`;
  `export interface StoreGeneration { rules: number; propositions: number }`;
  `export function parseGeneration(token: string | null): StoreGeneration | undefined`.

- [ ] **Step 1: Write the failing test**

```typescript
import { describe, expect, it, vi } from 'vitest';
import { RulesApiClient, parseGeneration } from './client.js';

const respond = (body: unknown, generation: string) =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json', 'motiv-generation': generation },
  });

describe('generation tracking', () => {
  it('parses the token the server stamps', () => {
    expect(parseGeneration('r7.p3')).toEqual({ rules: 7, propositions: 3 });
    expect(parseGeneration('nonsense')).toBeUndefined();
    expect(parseGeneration(null)).toBeUndefined();
  });

  it('remembers the highest generation it has seen', async () => {
    const fetch = vi.fn()
      .mockResolvedValueOnce(respond([], 'r7.p3'))
      .mockResolvedValueOnce(respond([], 'r8.p3'));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetch as never });

    await client.listRules();
    await client.listRules();

    expect(client.generation).toEqual({ rules: 8, propositions: 3 });
  });

  it('reports being routed backwards without throwing', async () => {
    const onStaleGeneration = vi.fn();
    const fetch = vi.fn()
      .mockResolvedValueOnce(respond([], 'r7.p3'))
      .mockResolvedValueOnce(respond([], 'r5.p3'));
    const client = new RulesApiClient({ baseUrl: '/api/rules', fetch: fetch as never, onStaleGeneration });

    await client.listRules();
    const second = await client.listRules();

    // Detection, not policy: the caller decides whether to retry, warn, or ignore
    expect(second).toEqual([]);
    expect(onStaleGeneration).toHaveBeenCalledWith({ rules: 5, propositions: 3 }, { rules: 7, propositions: 3 });
    expect(client.generation).toEqual({ rules: 7, propositions: 3 });
  });
});
```

- [ ] **Step 2: Run it to verify it fails**

```bash
cd ui && pnpm --filter @motiv-rules/core test
```

Expected: FAIL — `parseGeneration` is not exported.

- [ ] **Step 3: Implement**

Add to `client.ts`:

```typescript
/** Where both stores stood in the world a response was served from. */
export interface StoreGeneration {
  rules: number;
  propositions: number;
}

/** Reads the `Motiv-Generation` header. Anything the server did not write is refused. */
export function parseGeneration(token: string | null | undefined): StoreGeneration | undefined {
  if (!token) return undefined;
  const match = /^r(\d+)\.p(\d+)$/.exec(token);
  return match ? { rules: Number(match[1]), propositions: Number(match[2]) } : undefined;
}
```

Extend `RulesApiClientOptions` with `onStaleGeneration?: (observed: StoreGeneration, highest: StoreGeneration) => void;`,
hold `#generation: StoreGeneration | undefined` and the callback as private fields, expose
`get generation()`, and call a new private `#trackGeneration(response: Response)` from `#read` and
from every method that returns a raw `Response`:

```typescript
  /**
   * Records the world a response came from, and reports a backwards move.
   *
   * Replicas converge eventually, so a client can be routed to one that has not caught up yet.
   * That is explicable staleness rather than incoherence — but only if the client can see it, so
   * the highest generation observed is kept and a lower one is surfaced rather than swallowed.
   */
  #trackGeneration(response: Response): void {
    const observed = parseGeneration(response.headers.get('motiv-generation'));
    if (!observed) return;

    const highest = this.#generation;
    if (!highest) {
      this.#generation = observed;
      return;
    }

    if (observed.rules < highest.rules || observed.propositions < highest.propositions) {
      this.#onStaleGeneration?.(observed, highest);
      return;
    }

    this.#generation = observed;
  }
```

- [ ] **Step 4: Run the tests and the typecheck**

```bash
cd ui && pnpm --filter @motiv-rules/core test && pnpm --filter @motiv-rules/core typecheck
```

Expected: PASS, 457 + 3 tests.

- [ ] **Step 5: Export the new surface**

Add `parseGeneration` and `StoreGeneration` to `ui/packages/rules-core/src/index.ts`. Ticket 06 warns
that the barrel is `export *` today and needs curating before publication; do not widen that problem
— add these two names explicitly if the barrel has been curated by the time this runs, and otherwise
leave the wildcard alone and note it.

- [ ] **Step 6: Commit**

```bash
git add ui/packages/rules-core
git commit -m "feat(rules-core): let the client detect that it was routed to an older replica"
```

## Phase 8 — The app, the docs, and verification

### Task 15: Sample wiring and e2e

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Test: `ui/apps/demo/e2e/` (append an assertion to an existing spec rather than adding a file)

- [ ] **Step 1: Opt the sample into the poller**

In `Program.cs`, on the Motiv rules builder chain, add `.AddRefresh()`. The sample's
`JsonFileRuleStore` and `JsonFilePropositionStore` reread their files per operation *"so two processes
behave like two replicas"*, so this makes `docker compose up` converge for real rather than
decoratively.

- [ ] **Step 2: Assert the header in e2e**

In the demo's existing e2e spec that already loads the rules list, add:

```typescript
  test('every rules response carries the generation it was served from', async ({ request }) => {
    const response = await request.get('/api/rules/rules');
    expect(response.headers()['motiv-generation']).toMatch(/^r\d+\.p\d+$/);
  });
```

- [ ] **Step 3: Run the e2e suite**

```bash
cd ui/apps/demo && pnpm e2e
```

Expected: 27 passed / 8 skipped, plus the new test. Never run `playwright test` directly — the sample
serves a prebuilt `wwwroot` and would test a stale bundle. If the run reuses port 5100 from another
checkout, stop that server first: in a worktree this silently tests the *other* checkout's build.

- [ ] **Step 4: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample ui/apps/demo
git commit -m "feat(sample): opt into the refresh poller, and assert the generation header end to end"
```

### Task 16: Documentation

**Files:**
- Modify: `README.md` (a short example under Core Features)
- Create: `docs/multi-instance/index.md`, `docs/multi-instance/refresh.md`, `docs/multi-instance/toc.yml`
- Modify: `docs/toc.yml`, `docs/Overview.md`

Per CLAUDE.md, user-facing feature documentation goes to `README.md` and `docs/`, never to CLAUDE.md.
Follow the existing `docs/{feature}/index.md` + method-page + `toc.yml` structure exactly as the
neighbouring feature folders do.

- [ ] **Step 1: Write the docs**

Cover, in this order: what a generation is and why it is a pair; `RefreshAsync` and why a refresh is a
whole rebuild rather than a re-read; `AddRefresh` and its interval; the pin, including that
`MapMotivRules` supplies one per request and an in-process caller opens their own with `PinSnapshot`;
the `Motiv-Generation` header and what a client should do with it; and the abort policy, stated
plainly — a replica that cannot bind a stored document keeps serving what it has, reports Degraded,
and does not converge until it is repaired or redeployed.

- [ ] **Step 2: Commit**

```bash
git add README.md docs
git commit -m "docs: document multi-instance refresh, the generation, and the pin"
```

### Task 17: Full verification

- [ ] **Step 1: Whole suite, every project**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0
```

Expected: every project green, including the example projects — `Motiv.Poker.Tests`,
`Motiv.ECommerce.Tests` and `Motiv.SmartHome.Tests` assert on justification strings and are the
canary for anything this plan disturbed in result formatting.

- [ ] **Step 2: All-TFM build**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet build Motiv.slnx
```

Expected: 0 warnings / 0 errors on net472, net8.0, net9.0, net10.0 and netstandard2.0.

- [ ] **Step 3: Confirm the published package is untouched**

```bash
git diff --stat main -- src/Motiv/
```

Expected: no output.

- [ ] **Step 4: UI suites**

```bash
cd ui && pnpm --filter @motiv-rules/core test && pnpm --filter @motiv-rules/core typecheck && pnpm --filter demo test
```

If the demo typecheck reports implicit-`any` errors at files you never touched, build both package
dists first (`pnpm --filter @motiv-rules/core build && pnpm --filter @motiv-rules/react build`) — in
a fresh worktree the dists are absent and the errors are spurious.

- [ ] **Step 5: e2e**

```bash
cd ui/apps/demo && pnpm e2e
```

### Task 18: Mandatory `code-simplifier` review

CLAUDE.md requires this after implementation and it is not optional.

- [ ] **Step 1: Dispatch a `code-simplifier` agent** over everything this plan changed, asking it to
  look for duplication (the four rule classes now share more shape than they did — check whether the
  duplication that remains is still the deliberate kind CLAUDE.md endorses, or has become accidental),
  convoluted design, long methods, and anti-patterns.
- [ ] **Step 2: Apply what it finds**, re-running the affected tests after each change.
- [ ] **Step 3: Re-run Task 17 in full.**
- [ ] **Step 4: Commit**

```bash
git commit -am "refactor: apply code-simplifier findings"
```

---

## Self-review notes

**Spec coverage.** Every locked decision in the design maps to a task: 1 → Task 1; 2 → Task 10;
3 → Task 4; 4 → Tasks 4, 6, 7; 5 → Task 8; 6 → Tasks 9, 12; 7 → Task 10; 8 → Tasks 4, 10;
9 → Task 2; 10 → Tasks 2, 12, 14; 11 → Task 11; 12 → Task 13 (health check only, no spans).
Verification obligations: two-replica convergence → Task 10; abort keeps the live world → Task 10;
scalar-read poll → Task 10; atomicity → Tasks 6, 7, 9; slot stability and pin nesting → Tasks 8, 9.

**One clarification the plan makes to the design**, recorded in Task 10: "a refresh that cannot bind
a row aborts entirely" is sharpened to "aborts when applying it would quarantine something that is
not quarantined today". The literal reading stalls a replica permanently on a row that never bound,
which converges on nothing and was not the intent.

**Known ordering constraint.** Task 6's step 4 and Task 7 both touch `ChangeRequestSet`'s apply
phase; if the compiler forces it, land them as one commit rather than splitting the envelope commit
across two.
