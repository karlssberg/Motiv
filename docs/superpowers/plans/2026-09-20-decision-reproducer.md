# Decision Reproducer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A logged decision becomes a `Reproduction`: the rule and every referenced proposition at the version that decided, the model as far as capture and an adopter-registered resolver allow, a re-evaluation under those documents, and a fidelity verdict naming any anchor that could not be honoured.

**Architecture:** A read side for the decision log (`IDecisionSource`, implemented by the in-memory and SQL sinks); a resolver registry on `DecisionLogOptions` mirroring the capture registry; a `DecisionReproducer` in `Motiv.Serialization` that fetches the pinned documents from the two version logs, binds the propositions into a transient overlay layered over the live registry, asks the rule itself to replay its stored document against that source (a new internal `RuleBase.ReplayAsync`, which never records), and compares the verdict with the logged one. Registered in DI by `AddDecisionSource` on the rules builder. No HTTP or MCP surface in this slice.

**Tech Stack:** C# / .NET 10 (`Motiv.Serialization` also targets net8, net9, netstandard2.0), `System.Text.Json` for model rehydration, xUnit + Shouldly, the shared `src/testing/StoreConformance` folder.

**Spec:** `docs/superpowers/specs/2026-09-20-decision-reproduction-mcp-design.md`, sections 4 (resolver seam) and 5 (the primitive). Slice 3 of 5. Slice 4 adds the printer and the `CSharp` field; slice 5 the endpoints, the MCP and the testing package.

## Global Constraints

- `Motiv.Serialization` targets `net8.0;net9.0;netstandard2.0;net10.0`: no `Index`/`Range`, no default interface methods, no `System.Text.Json` APIs newer than the version the project already references.
- Every `dotnet` call needs `env -u MallocStackLogging -u MallocNanoZone` and the sandbox disabled; grep for `error CS`. Decision-log test classes belong to `Diagnostics.RulesTelemetryTestCollection` (constructing a `DecisionLog` tightens process-wide explanation detail) — new test classes that build one must carry the same `[Collection]` attribute.
- Reading stays **off** `IDecisionSink` (docs/decision-log/durable.md: a forwarding sink has nothing to read back). `IDecisionSource` is a separate interface a sink may implement.
- A replay never records: `ReplayAsync` evaluates the bound spec directly, not through `Rule.Evaluate`.
- The reproducer never mutates the live `RuleSet`, `PropositionSet` or scope. The overlay it binds into is transient.
- `DecisionQuery` moves from `Motiv.Serialization.Sql` to `Motiv.Serialization` (namespace `Motiv.Serialization`). Callers update their `using`; docs say so.
- Branch: `claude/decision-reproducer`, stacked on `claude/persisted-scenarios`. Plan and design doc (`docs/superpowers/specs/2026-09-20-decision-reproducer-design.md`) land on the branch before the PR opens.

## Review Focus

1. A pinned proposition version that has been superseded must be bound at the pinned version, not the head, or the reproduction silently shows today's logic. Pinned by `Should_bind_a_referenced_proposition_at_its_pinned_version_not_the_head` (Task 3).
2. A rule whose stored document at the logged version is a revert (`DocumentJson == null`) evaluated the compiled default; the reproduction must bind the default, not fail. Pinned by `Should_replay_a_reverted_version_against_the_compiled_default` (Task 3).
3. A `Whole` capture read back from SQL is a `JsonElement`, not the model; it must rehydrate to the rule's model type through the host's JSON options. Pinned by `Should_rehydrate_a_whole_capture_from_json` (Task 3) using an in-memory sink that hands back a `JsonElement`.
4. A resolver that returns null (the subject was erased) must yield `ModelUnresolved`, not a null-reference crash in the evaluation. Pinned by `Should_report_an_erased_subject_as_unresolved` (Task 3).
5. Propositions that reference each other must bind in dependency order even when the pinned list is not ordered. Pinned by `Should_bind_pinned_propositions_that_reference_each_other` (Task 3).

---

### Task 1: `IDecisionSource` and the sinks that read back

**Files:**
- Move: `src/Motiv.Serialization.Sql/DecisionQuery.cs` → `src/Motiv.Serialization/Decisions/DecisionQuery.cs` (namespace `Motiv.Serialization`)
- Create: `src/Motiv.Serialization/Decisions/IDecisionSource.cs`
- Modify: `src/Motiv.Serialization/Decisions/InMemoryDecisionSink.cs`
- Modify: `src/Motiv.Serialization.Sql/SqlDecisionSink.cs`, `DecisionStatements.cs`
- Modify: `src/Motiv.Studio/Program.cs` (the `using` for `DecisionQuery`, if it names the Sql namespace)
- Create: `src/testing/StoreConformance/DecisionSourceConformance.cs`
- Create: `src/Motiv.Serialization.Tests/Decisions/InMemoryDecisionSourceTests.cs`, `src/Motiv.Serialization.Sql.Tests/SqlDecisionSourceTests.cs`
- Modify: `src/Motiv.Serialization.Sql.Tests/Motiv.Serialization.Sql.Tests.csproj` if it does not already compile `..\testing\StoreConformance\*.cs` (check with `grep StoreConformance`)
- Modify: `docs/decision-log/durable.md` ("Reading It Back": the query type's namespace, and that the SQL sink is also an `IDecisionSource`)

**Interfaces:**
- Produces: `public interface IDecisionSource { Task<DecisionRecord?> FindAsync(Guid id, CancellationToken ct); Task<IReadOnlyList<DecisionRecord>> QueryAsync(DecisionQuery query, CancellationToken ct); }`. `SqlDecisionSink.ReadAsync` remains and `QueryAsync` delegates to it. `InMemoryDecisionSink` implements both by filtering `Records` (newest first, capped by `Limit`).

- [ ] **Step 1: Write the conformance suite**

`src/testing/StoreConformance/DecisionSourceConformance.cs`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>What it means to read the decision log back: by id, and by the existing query.</summary>
public abstract class DecisionSourceConformance : IAsyncLifetime
{
    protected IDecisionSink Sink { get; private set; } = null!;
    protected IDecisionSource Source { get; private set; } = null!;

    /// <summary>One object that is both the sink written to and the source read from.</summary>
    protected abstract Task<(IDecisionSink Sink, IDecisionSource Source)> CreateAsync();

    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    public async Task InitializeAsync() => (Sink, Source) = await CreateAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    protected static DecisionRecord Record(string rule = "can-checkout", bool satisfied = true, string? correlation = null, DateTimeOffset? at = null) =>
        new(Guid.NewGuid(), correlation ?? Guid.NewGuid().ToString("N"), at ?? DateTimeOffset.UtcNow, "alice",
            rule, RuleVersion: 3, BuildId: "build-1", [new PropositionVersion("customer.is-active", 2)],
            DecisionInput.Reference("cust-42"),
            new RuleEvaluationResult<object?>(satisfied, "r", ["r"], [], "r", new ExplanationNode(["r"], [])));

    [Fact]
    public async Task Should_find_a_written_record_by_id()
    {
        var record = Record();
        await Sink.WriteAsync([record], default);

        var found = await Source.FindAsync(record.Id, default);

        found.ShouldNotBeNull();
        found.RuleName.ShouldBe("can-checkout");
        found.RuleVersion.ShouldBe(3);
        found.ReferencedPropositionVersions.ShouldBe([new PropositionVersion("customer.is-active", 2)]);
        found.Input!.Kind.ShouldBe(DecisionInputKind.Reference);
        found.Input.Value.ShouldBe("cust-42");
    }

    [Fact]
    public async Task Should_answer_null_for_an_unknown_id()
    {
        (await Source.FindAsync(Guid.NewGuid(), default)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_query_by_rule_newest_first_and_capped()
    {
        var t = DateTimeOffset.UtcNow;
        await Sink.WriteAsync([Record("a", at: t.AddSeconds(-2)), Record("a", at: t.AddSeconds(-1)), Record("b", at: t)], default);

        var rows = await Source.QueryAsync(new DecisionQuery { RuleName = "a", Limit = 1 }, default);

        rows.ShouldHaveSingleItem().TimestampUtc.ShouldBe(t.AddSeconds(-1), TimeSpan.FromMilliseconds(1));
    }
}
```

If `ExplanationNode`'s constructor shape differs, read `RuleEvaluationResult.cs:53` and build it the way `DecisionRecordTests.AnOutcome()` does.

Subclasses: `InMemoryDecisionSourceTests` returns one `InMemoryDecisionSink` as both; `SqlDecisionSourceTests` builds a `SqlDecisionSink` the way the existing Sql tests do (read `src/Motiv.Serialization.Sql.Tests/*Fixture*.cs` for the SQLite connection factory and `EnsureSchemaAsync`).

- [ ] **Step 2: Run to see the failures**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~InMemoryDecisionSourceTests" 2>&1 | grep -E "error CS" | sed -E 's/.*(error CS[0-9]+: [^[]*).*/\1/' | sort -u | head -3
```

Expected: `IDecisionSource` and `DecisionQuery` not found.

- [ ] **Step 3: Move the query, add the interface, implement in memory**

`git mv src/Motiv.Serialization.Sql/DecisionQuery.cs src/Motiv.Serialization/Decisions/DecisionQuery.cs` and change its namespace to `Motiv.Serialization`. Create `IDecisionSource.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The decision log read back. Deliberately not on <see cref="IDecisionSink"/>: a sink that forwards
/// to a SIEM has nothing to read, and a query on the sink interface would make it lie. A sink that
/// keeps records implements this too, and a host registers it with <c>AddDecisionSource</c>.
/// </summary>
public interface IDecisionSource
{
    /// <summary>One record by its id, or null when the log holds none — retention may have purged it.</summary>
    Task<DecisionRecord?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A bounded page of records, newest first.</summary>
    Task<IReadOnlyList<DecisionRecord>> QueryAsync(DecisionQuery query, CancellationToken cancellationToken);
}
```

In `InMemoryDecisionSink`, add `IDecisionSource` to the class declaration and:

```csharp
    public Task<DecisionRecord?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Records.FirstOrDefault(record => record.Id == id));

    public Task<IReadOnlyList<DecisionRecord>> QueryAsync(DecisionQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<DecisionRecord> rows = Records;
        if (query.CorrelationId is { } correlation) rows = rows.Where(r => r.CorrelationId == correlation);
        if (query.RuleName is { } rule) rows = rows.Where(r => r.RuleName == rule);
        if (query.Satisfied is { } satisfied) rows = rows.Where(r => r.Outcome.Satisfied == satisfied);
        if (query.FromUtc is { } from) rows = rows.Where(r => r.TimestampUtc >= from);
        if (query.ToUtc is { } to) rows = rows.Where(r => r.TimestampUtc <= to);
        return Task.FromResult<IReadOnlyList<DecisionRecord>>(
            [.. rows.OrderByDescending(r => r.TimestampUtc).Take(query.Limit)]);
    }
```

- [ ] **Step 4: Implement in the SQL sink**

Add `IDecisionSource` to `SqlDecisionSink`. `QueryAsync` calls `ReadAsync`. `FindAsync` needs a by-id statement: add `SelectDecisionById` to `DecisionStatements` (the same column list as `SelectDecisions`, `WHERE <Id column> = @id`) and an `IdParameter = "@id"`, bind the `Guid` with `_dialect.ToParameter(id)` (check how the dialect handles guids on insert and mirror it), and read with `ReadAllAsync(command, _mapper.Read, ct)` taking `.FirstOrDefault()`. Update the `using` in Studio's `Program.cs` if it has `using Motiv.Serialization.Sql;` only for `DecisionQuery` (it also uses `SqlDecisionSink`, so it stays).

- [ ] **Step 5: Run the three test projects**

```bash
for p in src/Motiv.Serialization.Tests src/Motiv.Serialization.Sql.Tests src/Motiv.Studio.Tests; do env -u MallocStackLogging -u MallocNanoZone dotnet test $p 2>&1 | grep -E "error CS|Passed!|Failed!" | sed -E 's/ \[.*//'; done
```

Expected: `Passed!` on all three.

- [ ] **Step 6: Docs and commit**

In `docs/decision-log/durable.md` "Reading It Back": note `DecisionQuery` now lives in `Motiv.Serialization`, and add one sentence: "`SqlDecisionSink` also implements `IDecisionSource` — `FindAsync(id)` and `QueryAsync(query)` — which is what a reproduction reads through; see [replay](replay.md)."

```bash
git add -A src docs/decision-log/durable.md
git commit -m "Decision log — IDecisionSource: the log read back by id and by query, on the sinks that keep records

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: The model resolver seam

**Files:**
- Create: `src/Motiv.Serialization/Decisions/DecisionModelResolvers.cs`
- Modify: `src/Motiv.Serialization/Decisions/DecisionLogOptions.cs` (`public DecisionModelResolvers Resolve { get; } = new();`)
- Create: `src/Motiv.Serialization.Tests/Decisions/DecisionModelResolverTests.cs`

**Interfaces:**
- Produces: `public sealed class DecisionModelResolvers { public DecisionModelResolvers Reference<TModel>(Func<string, CancellationToken, Task<TModel?>> resolver); internal bool Covers(Type modelType); internal Task<object?> ResolveAsync(Type modelType, string key, CancellationToken ct); }` — `ResolveAsync` returns null when no resolver is registered for the type or the resolver returned null.

- [ ] **Step 1: Tests**

```csharp
[Collection(Diagnostics.RulesTelemetryTestCollection.Name)]
public class DecisionModelResolverTests
{
    private sealed record Customer(string Id);

    [Fact]
    public async Task Should_resolve_a_reference_through_the_registered_resolver()
    {
        var resolvers = new DecisionModelResolvers()
            .Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key)));

        (await resolvers.ResolveAsync(typeof(Customer), "cust-42", default)).ShouldBe(new Customer("cust-42"));
        resolvers.Covers(typeof(Customer)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_answer_null_when_nothing_is_registered_or_the_subject_is_gone()
    {
        var empty = new DecisionModelResolvers();
        (await empty.ResolveAsync(typeof(Customer), "cust-42", default)).ShouldBeNull();
        empty.Covers(typeof(Customer)).ShouldBeFalse();

        var erased = new DecisionModelResolvers().Reference<Customer>((_, _) => Task.FromResult<Customer?>(null));
        (await erased.ResolveAsync(typeof(Customer), "cust-42", default)).ShouldBeNull();
    }

    [Fact]
    public void Should_sit_beside_capture_on_the_log_options()
    {
        var options = new DecisionLogOptions();
        options.Resolve.Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key)));
        options.Resolve.Covers(typeof(Customer)).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run red, then implement**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// How a reference-only capture gets its model back for a reproduction: one resolver per model
/// type, registered at startup beside the capture posture. Nothing is registered by default — an
/// adopter who registers nothing gets scaffolds, never data — and null is the erasure case: the
/// subject is gone and replay is correctly impossible.
/// </summary>
public sealed class DecisionModelResolvers
{
    private readonly Dictionary<Type, Func<string, CancellationToken, Task<object?>>> _resolvers = new();

    public DecisionModelResolvers Reference<TModel>(Func<string, CancellationToken, Task<TModel?>> resolver)
    {
        if (resolver is null) throw new ArgumentNullException(nameof(resolver));
        _resolvers[typeof(TModel)] = async (key, ct) => await resolver(key, ct).ConfigureAwait(false);
        return this;
    }

    internal bool Covers(Type modelType) => _resolvers.ContainsKey(modelType);

    internal Task<object?> ResolveAsync(Type modelType, string key, CancellationToken cancellationToken) =>
        _resolvers.TryGetValue(modelType, out var resolver)
            ? resolver(key, cancellationToken)
            : Task.FromResult<object?>(null);
}
```

- [ ] **Step 3: Run green, commit**

```bash
git add src/Motiv.Serialization/Decisions src/Motiv.Serialization.Tests/Decisions
git commit -m "Decision log — a resolver seam beside the capture posture, for reference-only captures

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `Reproduction`, `ReplayAsync`, and `DecisionReproducer`

**Files:**
- Create: `src/Motiv.Serialization/Decisions/Reproduction.cs` (`Reproduction`, `ReproducedModel`, `ReproductionFidelity`, `FidelityReason`, `DecisionNotFoundException`)
- Modify: `src/Motiv.Serialization/Rules/RuleBase.cs`, `Rule.cs`, `AsyncRule.cs` (`internal abstract ValueTask<RuleEvaluationResult<object?>> ReplayAsync(RuleSerializer serializer, string? documentJson, object model, CancellationToken ct)`)
- Create: `src/Motiv.Serialization/Decisions/DecisionReproducer.cs`
- Create: `src/Motiv.Serialization.Tests/Decisions/DecisionReproducerTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum FidelityReason { RuleVersionMissing, BuildMismatch, PropositionVersionMissing, PropositionBindFailed, ModelRedacted, ModelUnresolved, OutcomeDiverged }
public sealed record FidelityNote(FidelityReason Reason, string Detail);
public sealed record ReproductionFidelity(IReadOnlyList<FidelityNote> Notes) { public bool IsExact => Notes.Count == 0; }
public enum ReproducedModelKind { Whole, Redacted, Resolved, Reference, Absent }
public sealed record ReproducedModel(ReproducedModelKind Kind, object? Value, string? Key);
public sealed record Reproduction(
    DecisionRecord Decision,
    StoredRuleVersion? Rule,
    IReadOnlyList<StoredPropositionVersion> Propositions,
    ReproducedModel Model,
    RuleEvaluationResult<object?>? Replayed,
    ReproductionFidelity Fidelity);
public sealed class DecisionNotFoundException(Guid id) : InvalidOperationException($"No decision '{id}' is in the log; retention may have purged it.");
public sealed class DecisionReproducer(IDecisionSource decisions, IRuleStore ruleStore, IPropositionStore propositionStore, RuleSet rules, PropositionSet? propositions, DecisionModelResolvers resolvers, JsonSerializerOptions modelJson)
{
    public Task<Reproduction> ReproduceAsync(Guid decisionId, CancellationToken cancellationToken);
}
```

- `RuleBase.ReplayAsync` binds `documentJson` (null → the compiled default) through `BindStoredState(serializer, documentJson, version: 0, errors)` and evaluates the bound spec **directly** (never `Evaluate`, so nothing is recorded), projecting with `ResultProjection.ProjectUntyped`. Bind errors throw `RuleSerializationException(errors)`.

- [ ] **Step 1: Write the tests**

Model the harness on `DecisionRecordingTests.AHostAsync`, extended with a `PropositionSet` sharing the `BindingScope` and stores the reproducer reads:

```csharp
[Collection(Diagnostics.RulesTelemetryTestCollection.Name)]
public class DecisionReproducerTests
{
    private sealed record Customer(string Id, bool IsActive, int Age);

    private static PolicyBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();
    private static PolicyBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckout() : Rule<Customer, string>("can-checkout", IsActive);

    private const string AuditedOverEligible =
        """{ "audited": true, "rule": { "spec": "customer.eligible" } }""";
    private const string EligibleIsActive = """{ "rule": { "spec": "customer.is-active" } }""";
    private const string EligibleIsAdult = """{ "rule": { "spec": "customer.is-adult" } }""";

    private sealed class Host : IAsyncDisposable
    {
        public required DecisionLog Log { get; init; }
        public required InMemoryDecisionSink Sink { get; init; }
        public required RuleSet Rules { get; init; }
        public required PropositionSet Propositions { get; init; }
        public required InMemoryRuleStore RuleStore { get; init; }
        public required InMemoryPropositionStore PropositionStore { get; init; }
        public required DecisionLogOptions Options { get; init; }
        public CanCheckout Rule => (CanCheckout)Rules.Find("can-checkout")!;

        public DecisionReproducer Reproducer() => new(
            Sink, RuleStore, PropositionStore, Rules, Propositions, Options.Resolve, JsonSerializerOptions.Web);

        public async Task<DecisionRecord> DecideAsync(Customer customer)
        {
            Rule.Evaluate(customer);
            await Log.FlushAsync();   // if DecisionLog has no flush, drain the way DecisionRecordingTests.DrainAsync does but keep the log open: read Sink.Records after a short poll
            return Sink.Records[^1];
        }

        public async ValueTask DisposeAsync() => await Log.DisposeAsync();
    }

    private static async Task<Host> AHostAsync(Action<DecisionLogOptions>? configure = null)
    {
        var sink = new InMemoryDecisionSink();
        var options = new DecisionLogOptions { Backpressure = DecisionBackpressure.Block };
        options.Capture.StoreWhole<Customer>();
        configure?.Invoke(options);
        var log = new DecisionLog(sink, options);

        var registry = new SpecRegistry().Register("customer.is-active", IsActive).Register("customer.is-adult", IsAdult);
        var scope = new BindingScope(registry);
        var propositionStore = new InMemoryPropositionStore();
        var propositions = new PropositionSet(scope, propositionStore).AddModel<Customer>("customer");
        var ruleStore = new InMemoryRuleStore();
        var rules = new RuleSet(scope, ruleStore, decisionLog: log).Add(new CanCheckout());
        // If RuleSet's constructor takes (scope, store, decisionLog) in a different order, read RuleSet.cs and adjust.

        (await propositions.CreateAsync("customer.eligible", "customer", EligibleIsActive, null)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await rules.UpdateAsync("can-checkout", AuditedOverEligible, 1, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        return new Host { Log = log, Sink = sink, Rules = rules, Propositions = propositions, RuleStore = ruleStore, PropositionStore = propositionStore, Options = options };
    }

    [Fact]
    public async Task Should_reproduce_an_exact_decision_with_the_model_and_the_same_verdict()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", IsActive: true, Age: 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue();
        reproduction.Rule!.Version.ShouldBe(2);
        reproduction.Propositions.Select(p => (p.Name, p.Version)).ShouldBe([("customer.eligible", 1)]);
        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Whole);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_bind_a_referenced_proposition_at_its_pinned_version_not_the_head()
    {
        // Arrange — decide under eligible v1 (is-active), then publish v2 (is-adult) for a minor who is active
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-7", IsActive: true, Age: 16));
        (await host.Propositions.UpdateAsync("customer.eligible", EligibleIsAdult, 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Act
        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        // Assert — v1 said yes (active); the head would say no (minor)
        reproduction.Propositions.ShouldHaveSingleItem().Version.ShouldBe(1);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.IsExact.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_report_a_missing_proposition_version_and_fall_back_to_the_head()
    {
        // Arrange — a record that pins a version the log never held (as retention or a pre-log store would leave)
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { ReferencedPropositionVersions = [new PropositionVersion("customer.eligible", 9)] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionVersionMissing && n.Detail.Contains("customer.eligible"));
        reproduction.Replayed.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_replay_a_reverted_version_against_the_compiled_default()
    {
        // Arrange — audited doc at v2, then a revert (v3, null document); decide at v2, reproduce at v2 and confirm v3 is a null-document row the reproducer can also bind
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        (await host.Rules.RevertAsync("can-checkout", 2, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var forged = decision with { RuleVersion = 3, ReferencedPropositionVersions = [] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule!.DocumentJson.ShouldBeNull();
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.Notes.ShouldNotContain(n => n.Reason == FidelityReason.RuleVersionMissing);
    }

    [Fact]
    public async Task Should_report_a_missing_rule_version_and_not_replay()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { RuleVersion = 42 };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule.ShouldBeNull();
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.RuleVersionMissing);
    }

    [Fact]
    public async Task Should_report_a_build_mismatch()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { BuildId = "some-other-build" };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.BuildMismatch && n.Detail.Contains("some-other-build"));
    }

    [Fact]
    public async Task Should_resolve_a_reference_only_capture_through_the_resolver()
    {
        await using var host = await AHostAsync(o =>
        {
            o.Capture.ReferenceOnly<Customer>(c => c.Id);
            o.Resolve.Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key, true, 30)));
        });
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Resolved);
        reproduction.Model.Key.ShouldBe("cust-42");
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.IsExact.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_report_an_erased_subject_as_unresolved()
    {
        await using var host = await AHostAsync(o =>
        {
            o.Capture.ReferenceOnly<Customer>(c => c.Id);
            o.Resolve.Reference<Customer>((_, _) => Task.FromResult<Customer?>(null));
        });
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Reference);
        reproduction.Model.Key.ShouldBe("cust-42");
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved);
    }

    [Fact]
    public async Task Should_report_a_reference_with_no_resolver_as_unresolved()
    {
        await using var host = await AHostAsync(o => o.Capture.ReferenceOnly<Customer>(c => c.Id));
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved && n.Detail.Contains("no resolver"));
    }

    [Fact]
    public async Task Should_report_a_diverged_outcome_loudly()
    {
        // The resolver hands back today's customer, who is no longer active: the replay flips
        await using var host = await AHostAsync(o =>
        {
            o.Capture.ReferenceOnly<Customer>(c => c.Id);
            o.Resolve.Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key, IsActive: false, 30)));
        });
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Replayed!.Satisfied.ShouldBeFalse();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.OutcomeDiverged);
    }

    [Fact]
    public async Task Should_mark_a_redacted_capture_and_still_replay_it()
    {
        await using var host = await AHostAsync(o => o.Capture.Redact<Customer>(c => new { c.Id, c.IsActive, c.Age }));
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Redacted);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelRedacted);
    }

    [Fact]
    public async Task Should_rehydrate_a_whole_capture_from_json()
    {
        // As the SQL sink hands it back: a JsonElement, not the model
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var element = JsonSerializer.SerializeToElement(new Customer("cust-42", true, 30), JsonSerializerOptions.Web);
        var fromSql = decision with { Input = DecisionInput.Whole(element) };
        await host.Sink.WriteAsync([fromSql], default);

        var reproduction = await host.Reproducer().ReproduceAsync(fromSql.Id, default);

        reproduction.Model.Value.ShouldBeOfType<Customer>();
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_bind_pinned_propositions_that_reference_each_other()
    {
        // eligible -> vip -> is-active; the pin lists them out of order
        await using var host = await AHostAsync();
        (await host.Propositions.CreateAsync("customer.vip", "customer", EligibleIsActive, null)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await host.Propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.vip" } }""", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        decision.ReferencedPropositionVersions.Select(p => p.Name).ShouldBe(["customer.eligible", "customer.vip"], ignoreOrder: true);

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue();
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_throw_for_an_unknown_decision()
    {
        await using var host = await AHostAsync();
        await Should.ThrowAsync<DecisionNotFoundException>(() => host.Reproducer().ReproduceAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Should_never_record_a_replay()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var before = host.Sink.Records.Count;

        await host.Reproducer().ReproduceAsync(decision.Id, default);
        await Task.Delay(50);

        host.Sink.Records.Count.ShouldBe(before);
    }
}
```

Two harness facts to confirm before running: the `RuleSet` constructor overload that takes a rule store and a decision log (grep `public RuleSet(` in `RuleSet.cs`), and how to wait for the in-memory sink to receive a record without disposing the log (`DecisionLogTests` will show whether a `FlushAsync` exists; if not, poll `Sink.Records` up to a second as `DecisionLogEndpointTests` does).

- [ ] **Step 2: Run red**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter "FullyQualifiedName~DecisionReproducerTests" 2>&1 | grep -E "error CS" | sed -E 's/.*(error CS[0-9]+: [^[]*).*/\1/' | sort -u | head -4
```

Expected: `DecisionReproducer`, `Reproduction`, `FidelityReason` not found.

- [ ] **Step 3: `Reproduction.cs`**

Write the records exactly as the Interfaces block shows, with XML docs on each: `RuleVersionMissing` (the rule log lacks the pinned version; nothing replays), `BuildMismatch` (compiled specs may differ), `PropositionVersionMissing` (bound the head instead), `PropositionBindFailed` (a pinned document no longer binds; the replay ran without it only if the rule still bound — otherwise `Replayed` is null), `ModelRedacted`, `ModelUnresolved`, `OutcomeDiverged`.

- [ ] **Step 4: `RuleBase.ReplayAsync`**

In `RuleBase`:

```csharp
    /// <summary>
    /// Binds <paramref name="documentJson"/> (null: the compiled default) against
    /// <paramref name="serializer"/>'s source and evaluates it once, for a reproduction. Evaluates
    /// the bound spec directly — never <c>Evaluate</c> — so nothing is recorded, pinned or traced.
    /// </summary>
    internal abstract ValueTask<RuleEvaluationResult<object?>> ReplayAsync(
        RuleSerializer serializer, string? documentJson, object model, CancellationToken cancellationToken);
```

In `Rule<TModel, TMetadata>`:

```csharp
    internal sealed override ValueTask<RuleEvaluationResult<object?>> ReplayAsync(
        RuleSerializer serializer, string? documentJson, object model, CancellationToken cancellationToken)
    {
        var errors = new List<RuleError>();
        if (BindStoredState(serializer, documentJson, version: 0, errors) is not State state)
            throw new RuleSerializationException(errors);
        return new(ResultProjection.ProjectUntyped(state.Spec.Evaluate((TModel)model)));
    }
```

and the async twin in `AsyncRule<TModel, TMetadata>` awaiting `state.Spec.EvaluateAsync((TModel)model, cancellationToken)` (read `AsyncRule.cs:92` for the exact evaluate signature and the `State` type's spec property). Check `RuleSerializationException`'s constructor shape.

- [ ] **Step 5: `DecisionReproducer`**

```csharp
using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>
/// Turns a logged decision into everything needed to run it again: the rule and every referenced
/// proposition at the version that decided, the model as far as capture and a resolver allow, a
/// replay under exactly those documents, and a fidelity verdict naming each anchor that slipped.
/// The live sets are read, never written; the overlay the propositions bind into is transient.
/// </summary>
public sealed class DecisionReproducer(
    IDecisionSource decisions,
    IRuleStore ruleStore,
    IPropositionStore propositionStore,
    RuleSet rules,
    PropositionSet? propositions,
    DecisionModelResolvers resolvers,
    JsonSerializerOptions modelJson)
{
    public async Task<Reproduction> ReproduceAsync(Guid decisionId, CancellationToken cancellationToken)
    {
        var decision = await decisions.FindAsync(decisionId, cancellationToken).ConfigureAwait(false)
            ?? throw new DecisionNotFoundException(decisionId);
        var notes = new List<FidelityNote>();

        if (decision.BuildId != BuildIdentity.Current)
            notes.Add(new(FidelityReason.BuildMismatch, $"decided on build '{decision.BuildId}', this host is '{BuildIdentity.Current}'"));

        var rule = rules.Find(decision.RuleName);
        var ruleVersion = (await ruleStore.HistoryAsync(decision.RuleName, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(row => row.Version == decision.RuleVersion);
        if (rule is null || ruleVersion is null)
            notes.Add(new(FidelityReason.RuleVersionMissing, $"'{decision.RuleName}' v{decision.RuleVersion} is not in the rule log"));

        var (pinned, source) = await BindPinnedPropositionsAsync(decision, notes, cancellationToken).ConfigureAwait(false);
        var model = await RehydrateAsync(decision, rule?.ModelType, notes, cancellationToken).ConfigureAwait(false);

        RuleEvaluationResult<object?>? replayed = null;
        if (rule is not null && ruleVersion is not null && model.Value is not null)
        {
            try
            {
                var serializer = new RuleSerializer(source, propositions?.Options ?? rules.Options);
                replayed = await rule.ReplayAsync(serializer, ruleVersion.DocumentJson, model.Value, cancellationToken).ConfigureAwait(false);
                if (replayed.Satisfied != decision.Outcome.Satisfied)
                    notes.Add(new(FidelityReason.OutcomeDiverged, $"logged {decision.Outcome.Satisfied}, replay {replayed.Satisfied}"));
            }
            catch (RuleSerializationException exception)
            {
                notes.Add(new(FidelityReason.PropositionBindFailed, string.Join("; ", exception.Errors.Select(e => e.Message))));
            }
        }

        return new Reproduction(decision, ruleVersion, pinned, model, replayed, new ReproductionFidelity(notes));
    }
```

`BindPinnedPropositionsAsync`: for each `PropositionVersion` in the record, `propositionStore.HistoryAsync(name)` → the row at that version; missing → note `PropositionVersionMissing` and take the live head's document via `propositions?.Find(name)` / `DocumentJsonOf(name)` when there is one. Collect `(name, modelTypeId, documentJson, description)`. Then bind in dependency order: an overlay (`new PropositionOverlay()`) layered as `new LayeredSpecSource(overlay, rules.Registry)`; loop: for every unbound document whose `DocumentReferences.From(parsed)` all resolve in the layered source (or are compiled names), bind through `propositions.ResolveModel(modelTypeId, errors)!.Bind(layered, name, description, document, isAsync: PropositionSet.BindsAsync(layered, references), errors)` and `overlay.Set(entry)`; stop when a pass binds nothing; anything left → `PropositionBindFailed` with its errors. Return the rows bound (as `StoredPropositionVersion`) and the layered source. With `propositions == null`, the pin must be empty; otherwise every entry is `PropositionBindFailed("no proposition set")`.

`RehydrateAsync`: `decision.Input` null → `Absent`, note `ModelUnresolved("nothing was captured")`. `Reference` → `resolvers.Covers(modelType)`? no → note `ModelUnresolved("no resolver registered for <type>")`, `Reference(key)`; else resolve → null → note `ModelUnresolved("resolver returned nothing for '<key>'")`, `Reference(key)`; else `Resolved(value, key)`. `Whole`/`Redacted` → value: if `value is JsonElement element` → `JsonSerializer.Deserialize(element, modelType, modelJson)`; else if `modelType.IsInstanceOfType(value)` → as is; else round-trip `JsonSerializer.SerializeToElement(value, modelJson)` then deserialize. A deserialization exception → note `ModelUnresolved(message)` and `Value = null`. `Redacted` adds `ModelRedacted("the capture was a projection; fields the rule reads may be absent")`. `modelType` null (rule missing) → skip rehydration, `Absent`.

`rules.Options` / `rules.Registry`: read `RuleSet.cs` for the names of the serializer options and registry it holds; add an `internal` accessor if there is none.

- [ ] **Step 6: Run green, then the whole Serialization project on net10**

```bash
env -u MallocStackLogging -u MallocNanoZone dotnet test src/Motiv.Serialization.Tests -f net10.0 2>&1 | grep -E "error CS|Passed!|Failed!|^\s+Failed " | sed -E 's/ \[.*//' | sort -u | head
```

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "Decision reproducer — a logged decision re-run under the documents that decided it, with a fidelity verdict

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: DI registration

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`
- Modify: `src/Motiv.Serialization.AspNetCore.Tests/RuleDiTests.cs` (or a new `DecisionSourceDiTests.cs`)
- Modify: `src/Motiv.Studio/Program.cs` (`.AddDecisionSource(provider => provider.GetRequiredService<SqlDecisionSink>())` and `log.Resolve.Reference<Customer>(...)` returning a demo customer by id from the seeded scenarios' models — read `ScenarioSeeds` for the four customers and resolve by `customerId`)

**Interfaces:**
- Produces: `MotivRulesBuilder.AddDecisionSource(IDecisionSource source)` / `AddDecisionSource(Func<IServiceProvider, IDecisionSource>)`: registers `IDecisionSource` and a `DecisionReproducer` singleton built from `IDecisionSource`, `IRuleStore`, `IPropositionStore` (both required — throw a clear `InvalidOperationException` at resolve time naming `AddRuleStore` / `AddPropositions` if absent), `RuleSet`, `PropositionSet?`, the `DecisionLog`'s `Options.Resolve` (expose `DecisionLog.Resolve` as a public property beside `Capture`), and `MotivRulesOptions.JsonSerializerOptions`.

- [ ] **Step 1: Test** — a host with `AddRuleStore().AddPropositions().AddDecisionLog(new InMemoryDecisionSink()).AddDecisionSource(sameSink)` resolves a `DecisionReproducer`; a host without `AddRuleStore` throws on resolve with a message containing `AddRuleStore`.
- [ ] **Step 2: Implement, run `Motiv.Serialization.AspNetCore.Tests` and `Motiv.Studio.Tests`, commit.**

---

### Task 5: Docs, design doc, full verification, simplifier, PR

- [ ] **Docs:** new `docs/decision-log/replay.md` (what a reproduction is, the three anchors it honours, the fidelity reasons table, the resolver seam with the `ReferenceOnly` + `Reference` pairing, "a replay never records", the `JsonElement` rehydration note); add it to `docs/decision-log/toc.yml` and link from `index.md`'s "What This Does Not Do Yet" (replay is now done; the endpoint and MCP are next); `CONTEXT.md` glossary gains **Reproduction** and **Fidelity**. Amend the parent spec's section 5 table to add `RuleVersionMissing` and `PropositionBindFailed` and to say `CSharp` arrives with slice 4.
- [ ] **Design doc:** `docs/superpowers/specs/2026-09-20-decision-reproducer-design.md` — decisions: read side off the sink interface; resolver beside capture; `ReplayAsync` on the rule; transient overlay; dependency-ordered binding; head fallback on a missing proposition version; rehydration rules; no HTTP yet.
- [ ] **Full verification:** solution build, every test project, net8/net9 for `Motiv.Serialization.Tests`.
- [ ] **Simplifier** over `git diff claude/persisted-scenarios...HEAD -- src/`; apply; re-run.
- [ ] **PR** against `claude/persisted-scenarios`, describing the primitive, the fidelity vocabulary, the namespace move of `DecisionQuery`, and what was and was not run.
