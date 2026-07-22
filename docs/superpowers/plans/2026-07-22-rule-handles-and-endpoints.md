# Rule Handles, RuleSet & Endpoints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Typed, hot-swappable rule handles (`Rule`/`PolicyRule`/`AsyncRule`/`AsyncPolicyRule`), a `RuleSet` with validate-bind-then-CAS updates, and `GET/PUT/DELETE /rules` endpoints plus DI glue — so an app executes named rules that the UI can replace at runtime without race conditions.

**Architecture:** Each handle owns one immutable state snapshot (`document JSON`, `version`, bound spec) read via `Volatile.Read` and replaced via `Interlocked.CompareExchange` — evaluators always see a coherent snapshot; writers get optimistic version conflicts. `RuleSet` performs validate → bind → CAS and returns discriminated results. `Motiv.Serialization` stays DI-free; `Motiv.Serialization.AspNetCore` adds `AddMotivRules`/`AddRule<T>` and the endpoints.

**Tech Stack:** C# — `src/Motiv.Serialization`, `src/Motiv.Serialization.AspNetCore` + their test projects (xUnit + Shouldly; AspNetCore tests use the in-memory `TestApp` pattern).

**Prerequisite:** Plan 2 (`2026-07-22-async-rule-binding.md`) — `DeserializeAsyncSpec` exists.

---

## Context for the implementing engineer

- **Core evaluation API:** sync `SpecBase<TModel, TMetadata>.Evaluate(model)` → `BooleanResultBase<TMetadata>`; `PolicyBase.Evaluate` shadows returning `PolicyResultBase<TMetadata>`; async equivalents return `ValueTask<...>` and take an optional `CancellationToken` (`EvaluateAsync(model, ct)`).
- **Forwarding constraint (from the spec):** async handles forward the underlying spec's `ValueTask` directly — a snapshot read plus delegation, never an `async`/`await` wrapper method.
- **`RuleSerializer`:** `Deserialize<TModel, TMetadata>(json)`, `DeserializeAsyncSpec<TModel, TMetadata>(json)`, `Validate<...>` — throw `RuleSerializationException` (`.Errors`) on invalid documents.
- **Endpoint file patterns:** `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (route group + `Results.Json(..., json)`), `RulesContracts.cs` (sealed records + XML docs), `MotivRulesOptions.cs`. Tests: `src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs` spins up `WebApplication.CreateSlimBuilder` + `UseTestServer`.
- **DI package reference:** `Motiv.Serialization.AspNetCore.csproj` uses `FrameworkReference Microsoft.AspNetCore.App` (verify — if it references specific packages instead, `IServiceCollection` comes from `Microsoft.Extensions.DependencyInjection.Abstractions`).

### File map

| File | Responsibility |
|---|---|
| Modify `src/Motiv.Serialization/RuleErrorCode.cs` | add `PolicyRequired` |
| Create `src/Motiv.Serialization/Rules/RuleBase.cs` | non-generic identity + abstract update/revert seam |
| Create `src/Motiv.Serialization/Rules/RuleDefault.cs` | compiled-vs-document default union |
| Create `src/Motiv.Serialization/Rules/Rule.cs` | sync spec-flavoured handle |
| Create `src/Motiv.Serialization/Rules/PolicyRule.cs` | sync policy-flavoured handle |
| Create `src/Motiv.Serialization/Rules/AsyncRule.cs` | async spec-flavoured handle |
| Create `src/Motiv.Serialization/Rules/AsyncPolicyRule.cs` | async policy-flavoured handle |
| Create `src/Motiv.Serialization/Rules/RuleUpdateResult.cs` | Updated/VersionConflict/Invalid/NotFound |
| Create `src/Motiv.Serialization/Rules/RuleDocuments.cs` | embedded-resource JSON helper |
| Create `src/Motiv.Serialization/Rules/RuleSet.cs` | registration, enumeration, update/revert |
| Create `src/Motiv.Serialization/Rules/RuleSetEntry.cs` | enumeration record |
| Modify `src/Motiv.Serialization.AspNetCore/RulesContracts.cs` | rule endpoint contracts |
| Modify `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` | GET/PUT/DELETE rules endpoints |
| Create `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs` | `AddMotivRules`/`AddRule<T>` |
| Tests | `src/Motiv.Serialization.Tests/Rules/*.cs`, `src/Motiv.Serialization.AspNetCore.Tests/RuleEndpointTests.cs`, `.../RuleDiTests.cs` |

---

### Task 1: `PolicyRequired` error code + `RuleUpdateResult`

**Files:**
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs` (append after `AsyncSpecInHigherOrder`)
- Create: `src/Motiv.Serialization/Rules/RuleUpdateResult.cs`
- Test: `src/Motiv.Serialization.Tests/Rules/RuleUpdateResultTests.cs`

- [ ] **Step 1: Append enum member**

```csharp
    /// <summary>An async spec was referenced inside a higher-order subtree, which must be fully synchronous.</summary>
    AsyncSpecInHigherOrder,

    /// <summary>A policy rule was updated with a document whose bound root is not a policy.</summary>
    PolicyRequired
```

- [ ] **Step 2: Write the failing test**

```csharp
namespace Motiv.Serialization.Tests.Rules;

public class RuleUpdateResultTests
{
    [Fact]
    public void Should_expose_outcome_specific_data()
    {
        // Arrange & Act
        var updated = RuleUpdateResult.Updated(3);
        var conflict = RuleUpdateResult.VersionConflict(5);
        var invalid = RuleUpdateResult.Invalid([new RuleError("$.rule", RuleErrorCode.UnknownSpec, "x")]);
        var notFound = RuleUpdateResult.NotFound();

        // Assert
        updated.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        updated.Version.ShouldBe(3);
        conflict.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        conflict.Version.ShouldBe(5);
        invalid.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        invalid.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
        notFound.Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
    }
}
```

- [ ] **Step 3: Run to verify compile failure**, then implement:

`src/Motiv.Serialization/Rules/RuleUpdateResult.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>The outcome kind of a <see cref="RuleSet.Update"/> or <see cref="RuleSet.Revert"/> call.</summary>
public enum RuleUpdateOutcome
{
    /// <summary>The rule was replaced; <see cref="RuleUpdateResult.Version"/> is the new version.</summary>
    Updated,

    /// <summary>The expected version was stale; <see cref="RuleUpdateResult.Version"/> is the current version.</summary>
    VersionConflict,

    /// <summary>The document failed to bind; <see cref="RuleUpdateResult.Errors"/> holds the errors.</summary>
    Invalid,

    /// <summary>No rule is registered under the given name.</summary>
    NotFound
}

/// <summary>The result of attempting to replace or revert a rule. Expected outcomes are values, not exceptions.</summary>
public sealed class RuleUpdateResult
{
    private RuleUpdateResult(RuleUpdateOutcome outcome, int version, IReadOnlyList<RuleError> errors)
    {
        Outcome = outcome;
        Version = version;
        Errors = errors;
    }

    /// <summary>The outcome kind.</summary>
    public RuleUpdateOutcome Outcome { get; }

    /// <summary>The new version on <see cref="RuleUpdateOutcome.Updated"/>; the current version on <see cref="RuleUpdateOutcome.VersionConflict"/>; otherwise 0.</summary>
    public int Version { get; }

    /// <summary>The binding errors on <see cref="RuleUpdateOutcome.Invalid"/>; otherwise empty.</summary>
    public IReadOnlyList<RuleError> Errors { get; }

    /// <summary>The rule was replaced and now has the given version.</summary>
    public static RuleUpdateResult Updated(int newVersion) => new(RuleUpdateOutcome.Updated, newVersion, []);

    /// <summary>The caller's expected version was stale; the rule is at the given version.</summary>
    public static RuleUpdateResult VersionConflict(int currentVersion) => new(RuleUpdateOutcome.VersionConflict, currentVersion, []);

    /// <summary>The document failed structural or semantic binding.</summary>
    public static RuleUpdateResult Invalid(IReadOnlyList<RuleError> errors) => new(RuleUpdateOutcome.Invalid, 0, errors);

    /// <summary>No rule is registered under the requested name.</summary>
    public static RuleUpdateResult NotFound() => new(RuleUpdateOutcome.NotFound, 0, []);
}
```

- [ ] **Step 4: Run test** → PASS. **Step 5: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: RuleUpdateResult and PolicyRequired error code"
```

---

### Task 2: `RuleBase` + sync `Rule<TModel, TMetadata>` with snapshot/CAS state

**Files:**
- Create: `src/Motiv.Serialization.Tests/Rules/RuleTests.cs`
- Create: `src/Motiv.Serialization/Rules/RuleBase.cs`
- Create: `src/Motiv.Serialization/Rules/RuleDefault.cs`
- Create: `src/Motiv.Serialization/Rules/Rule.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Motiv;

namespace Motiv.Serialization.Tests.Rules;

public class RuleTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("is-active-rule", IsActive, "demo rule");

    private static SpecRegistry Registry() => new SpecRegistry().Register("is-active", IsActive);

    [Fact]
    public void Should_evaluate_the_compiled_default_at_version_1()
    {
        // Arrange
        var rule = new ActiveRule();
        new RuleSet(Registry()).Add(rule);

        // Act
        var result = rule.Evaluate(new Customer(true));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["active"]);
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldBeNull();     // compiled default has no document
    }

    [Fact]
    public void Should_expose_identity()
    {
        // Arrange & Act
        var rule = new ActiveRule();

        // Assert
        rule.Name.ShouldBe("is-active-rule");
        rule.Description.ShouldBe("demo rule");
        rule.ModelType.ShouldBe(typeof(Customer));
        rule.MetadataType.ShouldBe(typeof(string));
        rule.IsAsync.ShouldBeFalse();
    }

    [Fact]
    public void Should_throw_when_evaluated_before_being_added_to_a_rule_set()
    {
        // Arrange
        var rule = new ActiveRule();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => rule.Evaluate(new Customer(true)))
            .Message.ShouldContain("RuleSet");
    }

    [Fact]
    public void Should_swap_to_an_updated_document_and_bump_the_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var document = """{ "rule": { "not": { "spec": "is-active" } } }""";

        // Act
        var outcome = rules.Update("is-active-rule", document, expectedVersion: 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
        rule.DocumentJson.ShouldNotBeNull();
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();   // negated now
    }

    [Fact]
    public void Should_report_a_version_conflict_for_a_stale_expected_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act — second writer still believes version 1
        var outcome = rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
    }

    [Fact]
    public void Should_report_binding_errors_without_touching_the_live_rule()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act
        var outcome = rules.Update("is-active-rule", """{ "rule": { "spec": "missing" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
        rule.Version.ShouldBe(1);
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();    // untouched
    }

    [Fact]
    public void Should_revert_to_the_compiled_default_and_bump_the_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "not": { "spec": "is-active" } } }""", 1);

        // Act
        var outcome = rules.Revert("is-active-rule", expectedVersion: 2);

        // Assert — revert is an update: version moves forward, never back (no ABA)
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        outcome.Version.ShouldBe(3);
        rule.DocumentJson.ShouldBeNull();
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_never_yield_a_torn_state_under_concurrent_update_and_evaluation()
    {
        // Arrange — hammer evaluation while a writer swaps between two documents; every
        // result must be coherent (either fully old or fully new — assertions match outcome).
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var positive = """{ "rule": { "spec": "is-active" } }""";
        var negated = """{ "rule": { "not": { "spec": "is-active" } } }""";
        var model = new Customer(true);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var version = rule.Version;
                rules.Update("is-active-rule", version % 2 == 1 ? negated : positive, version);
            }
        });

        // Act & Assert
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var result = rule.Evaluate(model);
                if (result.Satisfied)
                    result.Assertions.ShouldBe(["active"]);
                else
                    result.Assertions.ShouldContain("active == true");   // adjust to actual negated assertion once observed
            }
        })).ToArray();

        await Task.WhenAll(readers.Append(writer));
    }
}
```

Before finalizing the concurrency test's negated-branch assertion, evaluate the negated document once in a scratch test and pin the actual assertion text (don't guess — CLAUDE.md: capture actual output first).

- [ ] **Step 2: Run to verify compile failure**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleTests" -v minimal`
Expected: compile errors — none of the types exist. (`RuleSet` is referenced here but implemented in Task 3; implement Task 2 and 3 together against this one test file, committing after both are green — the tests are written store-first because handles are only reachable through a `RuleSet`.)

- [ ] **Step 3: Implement the default union and the base + sync handle**

`src/Motiv.Serialization/Rules/RuleDefault.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A rule's default implementation: either a compiled spec (bound at construction, no document)
/// or a serialized rule document (bound later, when the rule is added to a <see cref="RuleSet"/>).
/// </summary>
internal sealed class RuleDefault
{
    private RuleDefault(object? compiledSpec, string? documentJson)
    {
        CompiledSpec = compiledSpec;
        DocumentJson = documentJson;
    }

    /// <summary>The compiled default spec/policy, or null for a document default.</summary>
    public object? CompiledSpec { get; }

    /// <summary>The document-default JSON, or null for a compiled default.</summary>
    public string? DocumentJson { get; }

    public static RuleDefault Compiled(object spec) => new(spec, null);

    public static RuleDefault Document(string json) => new(null, json);
}
```

`src/Motiv.Serialization/Rules/RuleBase.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The non-generic identity of a rule: a named, versioned, hot-swappable decision handle.
/// Concrete rules derive from <see cref="Rule{TModel,TMetadata}"/>, <see cref="PolicyRule{TModel,TMetadata}"/>,
/// <see cref="AsyncRule{TModel,TMetadata}"/>, or <see cref="AsyncPolicyRule{TModel,TMetadata}"/>.
/// </summary>
public abstract class RuleBase
{
    private protected RuleBase(string name, RuleDefault @default, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A rule name must not be empty or whitespace.", nameof(name));

        Name = name;
        Default = @default;
        Description = description;
    }

    /// <summary>The stable name the rule is addressed by (endpoints, RuleSet lookups).</summary>
    public string Name { get; }

    /// <summary>An optional human-readable description surfaced by the rules endpoints.</summary>
    public string? Description { get; }

    /// <summary>The model type the rule evaluates against.</summary>
    public abstract Type ModelType { get; }

    /// <summary>The metadata type the rule yields.</summary>
    public abstract Type MetadataType { get; }

    /// <summary>Whether the rule evaluates asynchronously.</summary>
    public abstract bool IsAsync { get; }

    /// <summary>Whether the rule is policy-flavoured (yields a single value).</summary>
    public abstract bool IsPolicy { get; }

    /// <summary>The current version, starting at 1 and incremented by every successful update or revert.</summary>
    public abstract int Version { get; }

    /// <summary>The current document JSON, or null while the rule is on a compiled default.</summary>
    public abstract string? DocumentJson { get; }

    internal RuleDefault Default { get; }

    /// <summary>Binds the default and publishes version 1. Called exactly once, by <see cref="RuleSet.Add"/>.</summary>
    internal abstract void Attach(RuleSerializer serializer);

    /// <summary>Validates and binds the document, then CAS-publishes it over <paramref name="expectedVersion"/>.</summary>
    internal abstract RuleUpdateResult TryUpdate(RuleSerializer serializer, string documentJson, int expectedVersion);

    /// <summary>CAS-publishes the default back over <paramref name="expectedVersion"/>, bumping the version.</summary>
    internal abstract RuleUpdateResult TryRevert(RuleSerializer serializer, int expectedVersion);
}
```

`src/Motiv.Serialization/Rules/Rule.cs`:

```csharp
using Motiv;

namespace Motiv.Serialization;

/// <summary>
/// A named, hot-swappable, spec-flavoured rule: evaluations read an immutable snapshot, so an
/// in-flight evaluation always completes against a coherent version even while the rule is being
/// replaced. Declare rules as sealed subclasses so the type itself is the identity:
/// <code>public sealed class CanCheckoutRule() : Rule&lt;Customer, string&gt;("can-checkout", SomeSpec);</code>
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class Rule<TModel, TMetadata> : RuleBase
{
    private protected sealed class State(string? documentJson, int version, SpecBase<TModel, TMetadata> spec)
    {
        public string? DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public SpecBase<TModel, TMetadata> Spec { get; } = spec;
    }

    private State? _state;

    /// <summary>Creates a rule whose default implementation is a compiled spec.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultSpec">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public Rule(string name, SpecBase<TModel, TMetadata> defaultSpec, string? description = null)
        : base(name, RuleDefault.Compiled(defaultSpec ?? throw new ArgumentNullException(nameof(defaultSpec))), description)
    {
    }

    /// <summary>Creates a rule whose default implementation is a serialized rule document, bound at <see cref="RuleSet.Add"/>.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON (e.g. from <see cref="RuleDocuments.Embedded"/>).</param>
    /// <param name="description">An optional human-readable description.</param>
    public Rule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, RuleDefault.Document((defaultDocument ?? throw new ArgumentNullException(nameof(defaultDocument))).Json), description)
    {
    }

    /// <inheritdoc />
    public override Type ModelType => typeof(TModel);

    /// <inheritdoc />
    public override Type MetadataType => typeof(TMetadata);

    /// <inheritdoc />
    public override bool IsAsync => false;

    /// <inheritdoc />
    public override bool IsPolicy => false;

    /// <inheritdoc />
    public override int Version => Snapshot().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Snapshot().DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    public BooleanResultBase<TMetadata> Evaluate(TModel model) => Snapshot().Spec.Evaluate(model);

    private protected State Snapshot() =>
        Volatile.Read(ref _state)
        ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    internal sealed override void Attach(RuleSerializer serializer)
    {
        if (Volatile.Read(ref _state) is not null)
            throw new InvalidOperationException($"Rule '{Name}' has already been added to a RuleSet.");

        Volatile.Write(ref _state, BindDefault(serializer));
    }

    internal sealed override RuleUpdateResult TryUpdate(RuleSerializer serializer, string documentJson, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RuleUpdateResult.VersionConflict(current.Version);

        SpecBase<TModel, TMetadata> spec;
        try
        {
            spec = Bind(serializer, documentJson);
        }
        catch (RuleSerializationException ex)
        {
            return RuleUpdateResult.Invalid(ex.Errors);
        }

        if (RequirePolicy(spec) is { } policyError)
            return RuleUpdateResult.Invalid([policyError]);

        return Publish(current, new State(documentJson, current.Version + 1, spec));
    }

    internal sealed override RuleUpdateResult TryRevert(RuleSerializer serializer, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RuleUpdateResult.VersionConflict(current.Version);

        var @default = BindDefault(serializer);
        return Publish(current, new State(@default.DocumentJson, current.Version + 1, @default.Spec));
    }

    private RuleUpdateResult Publish(State expected, State replacement)
    {
        var witnessed = Interlocked.CompareExchange(ref _state, replacement, expected);
        return ReferenceEquals(witnessed, expected)
            ? RuleUpdateResult.Updated(replacement.Version)
            : RuleUpdateResult.VersionConflict(witnessed!.Version);
    }

    private State BindDefault(RuleSerializer serializer) =>
        Default.CompiledSpec is not null
            ? new State(null, 1, (SpecBase<TModel, TMetadata>)Default.CompiledSpec)
            : new State(Default.DocumentJson, 1, Bind(serializer, Default.DocumentJson!));

    private protected virtual SpecBase<TModel, TMetadata> Bind(RuleSerializer serializer, string documentJson) =>
        serializer.Deserialize<TModel, TMetadata>(documentJson);

    /// <summary>Policy-flavoured subclasses override to reject non-policy documents; specs accept anything.</summary>
    private protected virtual RuleError? RequirePolicy(SpecBase<TModel, TMetadata> spec) => null;
}
```

Note: `BindDefault` inside `TryRevert` rebinds a document default — deterministic, and keeps `State` immutable. The `RuleDocumentSource` parameter on the second constructor is the Task 4 helper's return type — deliberately NOT named `RuleDocument`, because an internal `RuleDocument` parser model already exists in this namespace (`src/Motiv.Serialization/RuleDocument.cs`). Task 4 defines `RuleDocumentSource`; implement Tasks 2–3–4's types as one compile unit, then run the full test file.

- [ ] **Step 4: proceed to Task 3** (same compile unit).

---

### Task 3: `RuleSet`

**Files:**
- Create: `src/Motiv.Serialization/Rules/RuleSet.cs`
- Create: `src/Motiv.Serialization/Rules/RuleSetEntry.cs`

- [ ] **Step 1: Implement `RuleSetEntry`**

```csharp
namespace Motiv.Serialization;

/// <summary>A read-only listing of one rule in a <see cref="RuleSet"/>.</summary>
/// <param name="Name">The stable rule name.</param>
/// <param name="ModelType">The model type the rule evaluates against.</param>
/// <param name="MetadataType">The metadata type the rule yields.</param>
/// <param name="IsAsync">Whether the rule evaluates asynchronously.</param>
/// <param name="IsPolicy">Whether the rule yields a single value.</param>
/// <param name="Version">The current version.</param>
/// <param name="Description">The optional description.</param>
/// <param name="DocumentJson">The current document, or null while on a compiled default.</param>
public sealed record RuleSetEntry(
    string Name,
    Type ModelType,
    Type MetadataType,
    bool IsAsync,
    bool IsPolicy,
    int Version,
    string? Description,
    string? DocumentJson);
```

- [ ] **Step 2: Implement `RuleSet`**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// The set of live rules an application executes. Adding a rule binds its default immediately
/// (fail-fast at startup); <see cref="Update"/> and <see cref="Revert"/> validate and bind
/// first, then publish atomically — writers get optimistic version conflicts, evaluators
/// always see a coherent snapshot.
/// </summary>
/// <remarks>
/// Like <see cref="SpecRegistry"/>, registration (<see cref="Add"/>) is intended to finish at
/// startup before concurrent use; <see cref="Update"/>/<see cref="Revert"/>/lookups are safe
/// concurrently thereafter.
/// </remarks>
public sealed class RuleSet
{
    private readonly Dictionary<string, RuleBase> _rules = new(StringComparer.Ordinal);
    private readonly RuleSerializer _serializer;

    /// <summary>Creates a rule set whose documents bind against the given registry.</summary>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="options">Options forwarded to the underlying serializer, or null for defaults.</param>
    public RuleSet(SpecRegistry registry, RuleSerializerOptions? options = null)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        _serializer = new RuleSerializer(registry, options);
    }

    /// <summary>The number of registered rules.</summary>
    public int Count => _rules.Count;

    /// <summary>Read-only listings of every registered rule, reflecting live versions.</summary>
    public IReadOnlyCollection<RuleSetEntry> Rules =>
        _rules.Values
            .Select(rule => new RuleSetEntry(
                rule.Name, rule.ModelType, rule.MetadataType, rule.IsAsync, rule.IsPolicy,
                rule.Version, rule.Description, rule.DocumentJson))
            .ToArray();

    /// <summary>
    /// Registers a rule and binds its default immediately — an invalid default document throws
    /// here, at startup, rather than at first evaluation.
    /// </summary>
    /// <param name="rule">The rule to register.</param>
    /// <returns>This rule set, to allow chained registration.</returns>
    /// <exception cref="RuleSerializationException">The rule's default document does not bind.</exception>
    public RuleSet Add(RuleBase rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        if (_rules.ContainsKey(rule.Name))
            throw new ArgumentException($"A rule is already registered under the name '{rule.Name}'.", nameof(rule));

        rule.Attach(_serializer);
        _rules[rule.Name] = rule;
        return this;
    }

    /// <summary>Looks up a registered rule by name.</summary>
    /// <param name="name">The rule name.</param>
    /// <returns>The rule, or null when none is registered under the name.</returns>
    public RuleBase? Find(string name) => _rules.TryGetValue(name, out var rule) ? rule : null;

    /// <summary>
    /// Replaces a rule's implementation with a document: validate → bind → atomic publish.
    /// The live rule is untouched unless the document binds and the expected version holds.
    /// </summary>
    /// <param name="name">The rule name.</param>
    /// <param name="documentJson">The replacement rule document.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, invalid document, or not found.</returns>
    public RuleUpdateResult Update(string name, string documentJson, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.TryUpdate(_serializer, documentJson, expectedVersion)
            : RuleUpdateResult.NotFound();

    /// <summary>Reverts a rule to its default. The version moves forward, never back.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedVersion">The version the caller last observed.</param>
    /// <returns>The outcome: updated, version conflict, or not found.</returns>
    public RuleUpdateResult Revert(string name, int expectedVersion) =>
        Find(name) is { } rule
            ? rule.TryRevert(_serializer, expectedVersion)
            : RuleUpdateResult.NotFound();
}
```

- [ ] **Step 3: Run the Task-2 test file**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleTests" -v minimal`
Expected: PASS (fix the concurrency test's pinned assertion from actual output on first run).

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: hot-swappable Rule handle and RuleSet with validate-bind-CAS updates"
```

---

### Task 4: `RuleDocumentSource` + `RuleDocuments` embedded helper

**Files:**
- Create: `src/Motiv.Serialization.Tests/Rules/RuleDocumentsTests.cs`
- Create: `src/Motiv.Serialization/Rules/RuleDocuments.cs`
- Test resource: `src/Motiv.Serialization.Tests/Rules/test-rule.json` (embedded resource)

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Motiv.Serialization.Tests.Rules;

public class RuleDocumentsTests
{
    [Fact]
    public void Should_wrap_raw_json()
    {
        // Arrange & Act
        var source = RuleDocuments.FromJson("""{ "rule": { "spec": "x" } }""");

        // Assert
        source.Json.ShouldContain("\"spec\"");
    }

    [Fact]
    public void Should_read_an_embedded_resource_from_the_calling_assembly()
    {
        // Arrange & Act — resource name matching is by trailing segment, so the
        // project-relative path works without the assembly-name prefix
        var source = RuleDocuments.Embedded("test-rule.json");

        // Assert
        source.Json.ShouldContain("is-active");
    }

    [Fact]
    public void Should_throw_a_helpful_error_for_a_missing_resource()
    {
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => RuleDocuments.Embedded("nope.json"))
            .Message.ShouldContain("nope.json");
    }
}
```

Add the resource file `src/Motiv.Serialization.Tests/Rules/test-rule.json`:

```json
{ "rule": { "spec": "is-active" } }
```

and in `Motiv.Serialization.Tests.csproj`:

```xml
    <ItemGroup>
        <EmbeddedResource Include="Rules\test-rule.json" />
    </ItemGroup>
```

- [ ] **Step 2: Run to verify compile failure**, then implement `src/Motiv.Serialization/Rules/RuleDocuments.cs`:

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Motiv.Serialization;

/// <summary>A rule-document JSON payload used as a rule's default implementation.</summary>
/// <param name="Json">The rule-document JSON.</param>
public sealed record RuleDocumentSource(string Json);

/// <summary>Sources for rule-document defaults.</summary>
public static class RuleDocuments
{
    /// <summary>Wraps raw rule-document JSON.</summary>
    /// <param name="json">The rule-document JSON.</param>
    /// <returns>A document source for a rule constructor.</returns>
    public static RuleDocumentSource FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("A rule document must not be empty or whitespace.", nameof(json));
        return new RuleDocumentSource(json);
    }

    /// <summary>
    /// Reads a rule document embedded in the calling assembly. The name matches by trailing
    /// resource-name segment, so a project-relative file name (e.g. <c>"loyalty.json"</c> or
    /// <c>"Rules/loyalty.json"</c>) resolves without the assembly-name prefix.
    /// </summary>
    /// <param name="resourceName">The embedded resource name or trailing path segment.</param>
    /// <returns>A document source for a rule constructor.</returns>
    /// <exception cref="InvalidOperationException">No unique matching resource exists.</exception>
    public static RuleDocumentSource Embedded(string resourceName) =>
        Embedded(resourceName, Assembly.GetCallingAssembly());

    /// <summary>Reads a rule document embedded in the given assembly.</summary>
    /// <param name="resourceName">The embedded resource name or trailing path segment.</param>
    /// <param name="assembly">The assembly holding the resource.</param>
    /// <returns>A document source for a rule constructor.</returns>
    /// <exception cref="InvalidOperationException">No unique matching resource exists.</exception>
    public static RuleDocumentSource Embedded(string resourceName, Assembly assembly)
    {
        var suffix = resourceName.Replace('/', '.').Replace('\\', '.');
        var matches = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(suffix, StringComparison.Ordinal))
            .ToArray();

        if (matches.Length != 1)
            throw new InvalidOperationException(matches.Length == 0
                ? $"No embedded resource ending in '{resourceName}' was found in {assembly.GetName().Name}."
                : $"Multiple embedded resources end in '{resourceName}' in {assembly.GetName().Name}: {string.Join(", ", matches)}.");

        using var stream = assembly.GetManifestResourceStream(matches[0])!;
        using var reader = new StreamReader(stream);
        return new RuleDocumentSource(reader.ReadToEnd());
    }
}
```

**Caution:** `Assembly.GetCallingAssembly()` is unreliable under inlining — add `[MethodImpl(MethodImplOptions.NoInlining)]` to the single-argument `Embedded` overload.

Now finish the Task-2 leftover: the document-default constructor on `Rule<TModel, TMetadata>` takes `RuleDocumentSource`:

```csharp
    public Rule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, RuleDefault.Document((defaultDocument ?? throw new ArgumentNullException(nameof(defaultDocument))).Json), description)
    {
    }
```

Add a test to `RuleTests` proving a document default binds at `Add` and fails fast when invalid:

```csharp
    private sealed class DocumentRule() : Rule<Customer, string>(
        "doc-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "is-active" } }"""));

    private sealed class BrokenDocumentRule() : Rule<Customer, string>(
        "broken-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "missing" } }"""));

    [Fact]
    public void Should_bind_a_document_default_at_add_and_serve_its_document()
    {
        // Arrange
        var rule = new DocumentRule();

        // Act
        new RuleSet(Registry()).Add(rule);

        // Assert
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldNotBeNull();
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_fail_fast_at_add_for_an_invalid_document_default()
    {
        // Act & Assert
        Should.Throw<RuleSerializationException>(() => new RuleSet(Registry()).Add(new BrokenDocumentRule()));
    }
```

- [ ] **Step 3: Run all rule tests** → PASS. **Step 4: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: document-default rules via RuleDocuments embedded/raw JSON sources"
```

---

### Task 5: `PolicyRule`, `AsyncRule`, `AsyncPolicyRule`

**Files:**
- Create: `src/Motiv.Serialization.Tests/Rules/RuleFlavourTests.cs`
- Create: `src/Motiv.Serialization/Rules/PolicyRule.cs`
- Create: `src/Motiv.Serialization/Rules/AsyncRule.cs`
- Create: `src/Motiv.Serialization/Rules/AsyncPolicyRule.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Motiv;

namespace Motiv.Serialization.Tests.Rules;

public class RuleFlavourTests
{
    private sealed record Customer(bool IsActive);
    private sealed record Verdict(string Code);

    private static PolicyBase<Customer, string> IsActivePolicy { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static AsyncSpecBase<Customer, string> CreditCheck { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.IsActive))
            .WhenTrue("passes").WhenFalse("fails").Create();

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("is-active", IsActivePolicy)
        .Register("credit-check", CreditCheck);

    private sealed class ActivePolicyRule() : PolicyRule<Customer, string>("active-policy", IsActivePolicy);
    private sealed class CreditRule() : AsyncRule<Customer, string>("credit", CreditCheck);

    [Fact]
    public void Should_shadow_evaluate_with_the_policy_result()
    {
        // Arrange
        var rule = new ActivePolicyRule();
        new RuleSet(Registry()).Add(rule);

        // Act
        PolicyResultBase<string> result = rule.Evaluate(new Customer(true));

        // Assert
        result.Value.ShouldBe("active");
        rule.IsPolicy.ShouldBeTrue();
        rule.ShouldBeAssignableTo<Rule<Customer, string>>();   // usable as its spec-flavoured base
    }

    [Fact]
    public void Should_yield_typed_metadata_from_a_policy_rule()
    {
        // Arrange — the typed-metadata DX: TMetadata inline as a generic argument, no casts
        var rule = new VerdictRule();
        new RuleSet(Registry()).Add(rule);

        // Act
        Verdict verdict = rule.Evaluate(new Customer(true)).Value;

        // Assert
        verdict.ShouldBe(new Verdict("OK"));
        rule.MetadataType.ShouldBe(typeof(Verdict));
    }

    private sealed class VerdictRule() : PolicyRule<Customer, Verdict>(
        "verdict",
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue(new Verdict("OK")).WhenFalse(new Verdict("DENIED")).Create("is approved"));

    [Fact]
    public void Should_reject_document_updates_that_do_not_bind_to_a_policy()
    {
        // Arrange — a bare 'and' composition binds to a spec, not a policy
        var rule = new ActivePolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var nonPolicy = """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-active" } ] } }""";

        // Act
        var outcome = rules.Update("active-policy", nonPolicy, 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.PolicyRequired);
        rule.Version.ShouldBe(1);
    }

    [Fact]
    public void Should_accept_document_updates_that_bind_to_a_policy()
    {
        // Arrange — a leaf referencing a registered policy stays a policy at runtime
        var rule = new ActivePolicyRule();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act
        var outcome = rules.Update("active-policy", """{ "rule": { "spec": "is-active" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        rule.Evaluate(new Customer(false)).Value.ShouldBe("inactive");
    }

    [Fact]
    public async Task Should_evaluate_async_rules_and_swap_them_atomically()
    {
        // Arrange
        var rule = new CreditRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rule.IsAsync.ShouldBeTrue();

        // Act — default, then a swapped mixed document
        var before = await rule.EvaluateAsync(new Customer(true));
        var outcome = rules.Update("credit",
            """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ] } }""", 1);
        var after = await rule.EvaluateAsync(new Customer(true));

        // Assert
        before.Satisfied.ShouldBeTrue();
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        after.Satisfied.ShouldBeTrue();
        after.Assertions.ShouldBe(["active", "passes"]);
    }

    [Fact]
    public void Should_reject_sync_documents_with_async_leaves_on_sync_rules()
    {
        // Arrange — a sync Rule must not accept a document referencing an async spec
        var rule = new RuleFlavourSyncProbe();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act
        var outcome = rules.Update("sync-probe", """{ "rule": { "spec": "credit-check" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
    }

    private sealed class RuleFlavourSyncProbe() : Rule<Customer, string>("sync-probe", IsActivePolicy);
}
```

- [ ] **Step 2: Run to verify compile failure**, then implement the three flavours.

`src/Motiv.Serialization/Rules/PolicyRule.cs`:

```csharp
using Motiv;

namespace Motiv.Serialization;

/// <summary>
/// A policy-flavoured rule: guarantees a single-value outcome. Derives from
/// <see cref="Rule{TModel,TMetadata}"/> and shadows <see cref="Evaluate"/> with the policy
/// result, exactly as <see cref="PolicyBase{TModel,TMetadata}"/> shadows
/// <see cref="SpecBase{TModel,TMetadata}"/>. Document updates must bind to a policy
/// (<see cref="RuleErrorCode.PolicyRequired"/> otherwise).
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class PolicyRule<TModel, TMetadata> : Rule<TModel, TMetadata>
{
    /// <summary>Creates a policy rule whose default implementation is a compiled policy.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultPolicy">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public PolicyRule(string name, PolicyBase<TModel, TMetadata> defaultPolicy, string? description = null)
        : base(name, defaultPolicy, description)
    {
    }

    /// <summary>Creates a policy rule whose default implementation is a serialized rule document.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON.</param>
    /// <param name="description">An optional human-readable description.</param>
    public PolicyRule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, defaultDocument, description)
    {
    }

    /// <inheritdoc />
    public override bool IsPolicy => true;

    /// <summary>Evaluates the current rule implementation, yielding the policy's single value.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <returns>The single-value policy result of the current implementation.</returns>
    public new PolicyResultBase<TMetadata> Evaluate(TModel model) =>
        ((PolicyBase<TModel, TMetadata>)Snapshot().Spec).Evaluate(model);

    private protected override RuleError? RequirePolicy(SpecBase<TModel, TMetadata> spec) =>
        spec is PolicyBase<TModel, TMetadata>
            ? null
            : new RuleError("$.rule", RuleErrorCode.PolicyRequired,
                $"rule '{Name}' is a policy rule, but the document binds to a spec; " +
                "give the root a whenTrue/whenFalse decoration or reference a policy");
}
```

Note this requires `Snapshot()` (Task 2) to be `private protected` on `Rule` — it is. A document-default `PolicyRule` must also be policy-checked at `Attach`: move the `RequirePolicy` check into `BindDefault`'s document path too. In `Rule<TModel, TMetadata>.BindDefault`, after binding a document default:

```csharp
    private State BindDefault(RuleSerializer serializer)
    {
        if (Default.CompiledSpec is not null)
            return new State(null, 1, (SpecBase<TModel, TMetadata>)Default.CompiledSpec);

        var spec = Bind(serializer, Default.DocumentJson!);
        if (RequirePolicy(spec) is { } policyError)
            throw new RuleSerializationException([policyError]);
        return new State(Default.DocumentJson, 1, spec);
    }
```

(Check `RuleSerializationException`'s constructor signature in `src/Motiv.Serialization/RuleSerializationException.cs` and match it.)

`src/Motiv.Serialization/Rules/AsyncRule.cs` — a parallel hierarchy over `AsyncSpecBase` (it does not derive from the sync `Rule`; the snapshot's spec type differs):

```csharp
using Motiv;

namespace Motiv.Serialization;

/// <summary>
/// A named, hot-swappable, async spec-flavoured rule. Evaluations forward the underlying
/// spec's <see cref="ValueTask{TResult}"/> directly off an immutable snapshot — synchronously
/// completing evaluations stay allocation-free, and in-flight evaluations always complete
/// against a coherent version.
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class AsyncRule<TModel, TMetadata> : RuleBase
{
    private protected sealed class State(string? documentJson, int version, AsyncSpecBase<TModel, TMetadata> spec)
    {
        public string? DocumentJson { get; } = documentJson;
        public int Version { get; } = version;
        public AsyncSpecBase<TModel, TMetadata> Spec { get; } = spec;
    }

    private State? _state;

    /// <summary>Creates an async rule whose default implementation is a compiled async spec.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultSpec">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncRule(string name, AsyncSpecBase<TModel, TMetadata> defaultSpec, string? description = null)
        : base(name, RuleDefault.Compiled(defaultSpec ?? throw new ArgumentNullException(nameof(defaultSpec))), description)
    {
    }

    /// <summary>Creates an async rule whose default implementation is a serialized rule document.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON.</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncRule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, RuleDefault.Document(defaultDocument.Json), description)
    {
    }

    /// <inheritdoc />
    public override Type ModelType => typeof(TModel);

    /// <inheritdoc />
    public override Type MetadataType => typeof(TMetadata);

    /// <inheritdoc />
    public override bool IsAsync => true;

    /// <inheritdoc />
    public override bool IsPolicy => false;

    /// <inheritdoc />
    public override int Version => Snapshot().Version;

    /// <inheritdoc />
    public override string? DocumentJson => Snapshot().DocumentJson;

    /// <summary>Evaluates the current rule implementation against the model.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>The rich boolean result of the current implementation.</returns>
    public ValueTask<BooleanResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
        Snapshot().Spec.EvaluateAsync(model, cancellationToken);

    private protected State Snapshot() =>
        Volatile.Read(ref _state)
        ?? throw new InvalidOperationException(
            $"Rule '{Name}' has not been bound; add it to a RuleSet before evaluating.");

    internal sealed override void Attach(RuleSerializer serializer)
    {
        if (Volatile.Read(ref _state) is not null)
            throw new InvalidOperationException($"Rule '{Name}' has already been added to a RuleSet.");

        Volatile.Write(ref _state, BindDefault(serializer));
    }

    internal sealed override RuleUpdateResult TryUpdate(RuleSerializer serializer, string documentJson, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RuleUpdateResult.VersionConflict(current.Version);

        AsyncSpecBase<TModel, TMetadata> spec;
        try
        {
            spec = serializer.DeserializeAsyncSpec<TModel, TMetadata>(documentJson);
        }
        catch (RuleSerializationException ex)
        {
            return RuleUpdateResult.Invalid(ex.Errors);
        }

        if (RequirePolicy(spec) is { } policyError)
            return RuleUpdateResult.Invalid([policyError]);

        return Publish(current, new State(documentJson, current.Version + 1, spec));
    }

    internal sealed override RuleUpdateResult TryRevert(RuleSerializer serializer, int expectedVersion)
    {
        var current = Snapshot();
        if (current.Version != expectedVersion)
            return RuleUpdateResult.VersionConflict(current.Version);

        var @default = BindDefault(serializer);
        return Publish(current, new State(@default.DocumentJson, current.Version + 1, @default.Spec));
    }

    private RuleUpdateResult Publish(State expected, State replacement)
    {
        var witnessed = Interlocked.CompareExchange(ref _state, replacement, expected);
        return ReferenceEquals(witnessed, expected)
            ? RuleUpdateResult.Updated(replacement.Version)
            : RuleUpdateResult.VersionConflict(witnessed!.Version);
    }

    private State BindDefault(RuleSerializer serializer)
    {
        if (Default.CompiledSpec is not null)
            return new State(null, 1, (AsyncSpecBase<TModel, TMetadata>)Default.CompiledSpec);

        var spec = serializer.DeserializeAsyncSpec<TModel, TMetadata>(Default.DocumentJson!);
        if (RequirePolicy(spec) is { } policyError)
            throw new RuleSerializationException([policyError]);
        return new State(Default.DocumentJson, 1, spec);
    }

    /// <summary>Policy-flavoured subclasses override to reject non-policy documents.</summary>
    private protected virtual RuleError? RequirePolicy(AsyncSpecBase<TModel, TMetadata> spec) => null;
}
```

(The sync/async duplication is deliberate — same avoid-over-DRYing convention as the binders. Check `RuleSerializationException`'s constructor and adjust `[policyError]` construction to match.)

`src/Motiv.Serialization/Rules/AsyncPolicyRule.cs`:

```csharp
using Motiv;

namespace Motiv.Serialization;

/// <summary>
/// An async policy-flavoured rule: guarantees a single-value outcome, forwarding the
/// underlying policy's <see cref="ValueTask{TResult}"/> directly off an immutable snapshot.
/// Document updates must bind to an async policy (<see cref="RuleErrorCode.PolicyRequired"/> otherwise).
/// </summary>
/// <typeparam name="TModel">The model type the rule evaluates against.</typeparam>
/// <typeparam name="TMetadata">The metadata type the rule yields.</typeparam>
public class AsyncPolicyRule<TModel, TMetadata> : AsyncRule<TModel, TMetadata>
{
    /// <summary>Creates an async policy rule whose default implementation is a compiled async policy.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultPolicy">The compiled default implementation.</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncPolicyRule(string name, AsyncPolicyBase<TModel, TMetadata> defaultPolicy, string? description = null)
        : base(name, defaultPolicy, description)
    {
    }

    /// <summary>Creates an async policy rule whose default implementation is a serialized rule document.</summary>
    /// <param name="name">The stable name the rule is addressed by.</param>
    /// <param name="defaultDocument">The default rule-document JSON.</param>
    /// <param name="description">An optional human-readable description.</param>
    public AsyncPolicyRule(string name, RuleDocumentSource defaultDocument, string? description = null)
        : base(name, defaultDocument, description)
    {
    }

    /// <inheritdoc />
    public override bool IsPolicy => true;

    /// <summary>Evaluates the current rule implementation, yielding the policy's single value.</summary>
    /// <param name="model">The model to evaluate.</param>
    /// <param name="cancellationToken">A token that can cancel the evaluation.</param>
    /// <returns>The single-value policy result of the current implementation.</returns>
    public new ValueTask<PolicyResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default) =>
        ((AsyncPolicyBase<TModel, TMetadata>)Snapshot().Spec).EvaluateAsync(model, cancellationToken);

    private protected override RuleError? RequirePolicy(AsyncSpecBase<TModel, TMetadata> spec) =>
        spec is AsyncPolicyBase<TModel, TMetadata>
            ? null
            : new RuleError("$.rule", RuleErrorCode.PolicyRequired,
                $"rule '{Name}' is a policy rule, but the document binds to a spec; " +
                "give the root a whenTrue/whenFalse decoration or reference a policy");
}
```

- [ ] **Step 3: Run the flavour tests** → PASS.

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleFlavour" -v minimal`

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: PolicyRule, AsyncRule, and AsyncPolicyRule flavours"
```

---

### Task 6: Rule endpoints — `GET /rules`, `GET /rules/{name}`, `PUT /rules/{name}`, `DELETE /rules/{name}`

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore.Tests/RuleEndpointTests.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/RulesContracts.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`
- Modify: `src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class RuleEndpointTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive, "the demo rule");

    private static (SpecRegistry, MotivRulesOptions, RuleSet) Fixture()
    {
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var rules = new RuleSet(registry).Add(new ActiveRule());
        return (registry, options, rules);
    }

    [Fact]
    public async Task Should_list_rules()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert
        var entry = body[0];
        entry.GetProperty("name").GetString().ShouldBe("active-rule");
        entry.GetProperty("modelType").GetString().ShouldBe("customer");
        entry.GetProperty("metadataType").GetString().ShouldBe("String");
        entry.GetProperty("isAsync").GetBoolean().ShouldBeFalse();
        entry.GetProperty("isPolicy").GetBoolean().ShouldBeFalse();
        entry.GetProperty("version").GetInt32().ShouldBe(1);
        entry.GetProperty("description").GetString().ShouldBe("the demo rule");
    }

    [Fact]
    public async Task Should_get_a_rule_with_a_null_document_while_on_a_compiled_default()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule");

        // Assert
        body.GetProperty("version").GetInt32().ShouldBe(1);
        body.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_put_a_document_and_serve_it_back()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "not": { "spec": "is-active" } } }""").RootElement;

        // Act
        var put = await client.PutAsJsonAsync("/api/rules/rules/active-rule",
            new { document, baseVersion = 1 });

        // Assert
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await put.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32().ShouldBe(2);

        var get = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule");
        get.GetProperty("version").GetInt32().ShouldBe(2);
        get.GetProperty("document").GetProperty("rule").GetProperty("not").GetProperty("spec")
            .GetString().ShouldBe("is-active");
    }

    [Fact]
    public async Task Should_409_with_the_current_version_on_a_stale_base_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "is-active" } }""").RootElement;
        await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Act — a second writer still on version 1
        var conflict = await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Assert
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentVersion").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Should_400_with_structured_errors_for_an_invalid_document()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "spec": "missing" } }""").RootElement;

        // Act
        var response = await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("UnknownSpec");
    }

    [Fact]
    public async Task Should_404_for_unknown_rules()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();

        // Act & Assert
        (await client.GetAsync("/api/rules/rules/nope")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_revert_via_delete_and_bump_the_version()
    {
        // Arrange
        var (registry, options, rules) = Fixture();
        await using var app = await TestApp.StartAsync(registry, options, rules);
        var client = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "rule": { "not": { "spec": "is-active" } } }""").RootElement;
        await client.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Act
        var response = await client.DeleteAsync("/api/rules/rules/active-rule?baseVersion=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var get = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/active-rule");
        get.GetProperty("version").GetInt32().ShouldBe(3);
        get.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
```

Add a `TestApp.StartAsync(registry, options, rules)` overload:

```csharp
    public static async Task<WebApplication> StartAsync(SpecRegistry registry, MotivRulesOptions options, RuleSet rules)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapMotivRules("/api/rules", registry, options, rules);
        await app.StartAsync();
        return app;
    }
```

- [ ] **Step 2: Run to verify compile failure**, then implement.

Contracts (append to `RulesContracts.cs`):

```csharp
/// <summary>A listing of one live rule.</summary>
/// <param name="Name">The stable rule name.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the rule evaluates asynchronously.</param>
/// <param name="IsPolicy">Whether the rule yields a single value.</param>
/// <param name="Version">The current version.</param>
/// <param name="Description">An optional human-readable description.</param>
public sealed record RuleListEntry(
    string Name, string ModelType, string MetadataType, bool IsAsync, bool IsPolicy, int Version, string? Description);

/// <summary>One rule's current document and version.</summary>
/// <param name="Document">The current rule document, or null while on a compiled (code-defined) default.</param>
/// <param name="Version">The current version; pass it back as <c>baseVersion</c> when updating.</param>
public sealed record RuleGetResponse(JsonElement? Document, int Version);

/// <summary>A request to replace a rule's implementation.</summary>
/// <param name="Document">The replacement rule document.</param>
/// <param name="BaseVersion">The version the caller last observed; a stale value yields 409.</param>
public sealed record RulePutRequest(JsonElement Document, int BaseVersion);

/// <summary>A successful update or revert.</summary>
/// <param name="Version">The rule's new version.</param>
public sealed record RulePutResponse(int Version);

/// <summary>A rejected update: the caller's base version was stale.</summary>
/// <param name="CurrentVersion">The version the rule is actually at; reload before retrying.</param>
public sealed record RuleConflictResponse(int CurrentVersion);
```

Endpoints — change `MapMotivRules` signature to take an optional rule set and mount the group when present (keep the existing catalog/validate/evaluate code untouched above it):

```csharp
    public static IEndpointRouteBuilder MapMotivRules(
        this IEndpointRouteBuilder endpoints,
        string basePath,
        SpecRegistry registry,
        MotivRulesOptions options,
        RuleSet? rules = null)
    {
        // ... existing body unchanged ...

        if (rules is not null)
            MapRuleEndpoints(group, rules, options, json);

        return endpoints;
    }

    private static void MapRuleEndpoints(RouteGroupBuilder group, RuleSet rules, MotivRulesOptions options, JsonSerializerOptions json)
    {
        group.MapGet("/rules", () =>
            Results.Json(rules.Rules
                .Select(rule => new RuleListEntry(
                    rule.Name,
                    options.ResolveModelId(rule.ModelType),
                    rule.MetadataType.Name,
                    rule.IsAsync,
                    rule.IsPolicy,
                    rule.Version,
                    rule.Description))
                .ToArray(), json));

        group.MapGet("/rules/{name}", (string name) =>
        {
            if (rules.Find(name) is not { } rule)
                return Results.Json(new ErrorResponse($"Unknown rule '{name}'."), json, statusCode: 404);

            // Read the snapshot fields via one entry to keep document+version coherent.
            var entry = rules.Rules.First(r => r.Name == name);
            var document = entry.DocumentJson is null
                ? (JsonElement?)null
                : JsonDocument.Parse(entry.DocumentJson).RootElement.Clone();
            return Results.Json(new RuleGetResponse(document, entry.Version), json);
        });

        group.MapPut("/rules/{name}", (string name, RulePutRequest request) =>
            ToResult(rules.Update(name, request.Document.GetRawText(), request.BaseVersion), name, json));

        group.MapDelete("/rules/{name}", (string name, int baseVersion) =>
            ToResult(rules.Revert(name, baseVersion), name, json));
    }

    private static IResult ToResult(RuleUpdateResult outcome, string name, JsonSerializerOptions json) =>
        outcome.Outcome switch
        {
            RuleUpdateOutcome.Updated => Results.Json(new RulePutResponse(outcome.Version), json),
            RuleUpdateOutcome.VersionConflict => Results.Json(new RuleConflictResponse(outcome.Version), json, statusCode: 409),
            RuleUpdateOutcome.Invalid => Results.Json(new ValidationResponse(outcome.Errors), json, statusCode: 400),
            _ => Results.Json(new ErrorResponse($"Unknown rule '{name}'."), json, statusCode: 404)
        };
```

**Coherence caveat:** `RuleGetResponse` document+version must come from one snapshot. `rules.Rules` materializes each entry from the live rule — a swap between reading `DocumentJson` and `Version` inside `RuleSetEntry` construction would tear. Fix at the source: in Task 2/5, back `RuleSetEntry` materialization with a single snapshot read — add to `RuleBase` an `internal abstract (int Version, string? DocumentJson) VersionedDocument();` implemented in each handle as `var s = Snapshot(); return (s.Version, s.DocumentJson);`, and use it in `RuleSet.Rules`. Do this now if not already done, with a covering unit test in `RuleTests` asserting `VersionedDocument()` fields always agree under the Task-2 concurrency hammer.

- [ ] **Step 3: Run the endpoint tests** → PASS.

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.AspNetCore.Tests -v minimal`

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests src/Motiv.Serialization.Tests
git commit -m "feat: GET/PUT/DELETE rule endpoints with optimistic concurrency"
```

---

### Task 7: DI — `AddMotivRules` / `AddRule<T>` / DI-resolving `MapMotivRules`

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore.Tests/RuleDiTests.cs`
- Create: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

public class RuleDiTests
{
    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive);

    [Fact]
    public async Task Should_register_rules_as_singletons_and_mount_endpoints_from_di()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMotivRules(registry, options).AddRule<ActiveRule>();
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        await using var _ = app;

        // Act — the injected handle and the endpoint see the same live rule
        var handle = app.Services.GetRequiredService<ActiveRule>();
        var body = await app.GetTestClient().GetFromJsonAsync<JsonElement>("/api/rules/rules");

        // Assert
        handle.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
        body[0].GetProperty("name").GetString().ShouldBe("active-rule");
    }

    [Fact]
    public async Task Should_reflect_endpoint_updates_in_the_injected_handle()
    {
        // Arrange
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMotivRules(registry, options).AddRule<ActiveRule>();
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        await using var _ = app;
        var document = JsonDocument.Parse("""{ "rule": { "not": { "spec": "is-active" } } }""").RootElement;

        // Act — hot-swap over HTTP, observe via the injected handle
        var put = await app.GetTestClient()
            .PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 });

        // Assert
        put.EnsureSuccessStatusCode();
        app.Services.GetRequiredService<ActiveRule>()
            .Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run to verify compile failure**, then implement `MotivRulesServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A builder for enrolling rules after <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/>.</summary>
public sealed class MotivRulesBuilder
{
    internal MotivRulesBuilder(IServiceCollection services) => Services = services;

    /// <summary>The underlying service collection, for advanced scenarios.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers a rule as a singleton under its concrete type and enrolls it in the
    /// <see cref="RuleSet"/>. Inject the concrete type wherever the rule is executed.
    /// </summary>
    /// <typeparam name="TRule">The sealed rule class (parameterless constructor).</typeparam>
    /// <returns>This builder, to allow chained registration.</returns>
    public MotivRulesBuilder AddRule<TRule>() where TRule : RuleBase, new()
    {
        Services.AddSingleton<TRule>();
        Services.AddSingleton<RuleBase>(provider => provider.GetRequiredService<TRule>());
        return this;
    }

    /// <summary>Registers an existing rule instance and enrolls it in the <see cref="RuleSet"/>.</summary>
    /// <typeparam name="TRule">The rule's concrete type.</typeparam>
    /// <param name="rule">The rule instance.</param>
    /// <returns>This builder, to allow chained registration.</returns>
    public MotivRulesBuilder AddRule<TRule>(TRule rule) where TRule : RuleBase
    {
        Services.AddSingleton(rule);
        Services.AddSingleton<RuleBase>(rule);
        return this;
    }
}

/// <summary>DI registration for the Motiv rules endpoints and live rules.</summary>
public static class MotivRulesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the registry, options, and a <see cref="RuleSet"/> singleton built from every
    /// rule enrolled via <see cref="MotivRulesBuilder.AddRule{TRule}()"/>. The RuleSet binds all
    /// rule defaults when first resolved — <c>MapMotivRules(basePath)</c> resolves it eagerly so
    /// invalid defaults fail at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="registry">The registry rule documents resolve spec references against.</param>
    /// <param name="options">The endpoint options, including evaluable model registrations.</param>
    /// <returns>A builder for enrolling rules.</returns>
    public static MotivRulesBuilder AddMotivRules(
        this IServiceCollection services,
        SpecRegistry registry,
        MotivRulesOptions options)
    {
        services.AddSingleton(registry);
        services.AddSingleton(options);
        services.AddSingleton(provider =>
        {
            var rules = new RuleSet(registry, options.SerializerOptions);
            foreach (var rule in provider.GetServices<RuleBase>())
                rules.Add(rule);
            return rules;
        });
        return new MotivRulesBuilder(services);
    }
}
```

And the DI-resolving map overload in `MotivRulesEndpoints.cs`:

```csharp
    /// <summary>
    /// Maps the rules endpoints using the registry, options, and <see cref="RuleSet"/> registered
    /// via <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/>. Resolves the
    /// RuleSet eagerly so an invalid rule default fails here, at startup.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="basePath">The base path to mount under, e.g. <c>/api/rules</c>.</param>
    /// <returns>The endpoint route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMotivRules(this IEndpointRouteBuilder endpoints, string basePath)
    {
        var services = endpoints.ServiceProvider;
        return endpoints.MapMotivRules(
            basePath,
            services.GetRequiredService<SpecRegistry>(),
            services.GetRequiredService<MotivRulesOptions>(),
            services.GetRequiredService<RuleSet>());
    }
```

(`IEndpointRouteBuilder.ServiceProvider` exists on the interface; add `using Microsoft.Extensions.DependencyInjection;`.)

- [ ] **Step 3: Run the DI tests, then both serialization test projects** → PASS.

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests src/Motiv.Serialization.AspNetCore.Tests -v minimal`

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "feat: AddMotivRules/AddRule DI registration with eager RuleSet binding"
```

---

### Task 8: Full-solution verification + review

- [ ] **Step 1:** `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test *.sln -v minimal` → all PASS.
- [ ] **Step 2:** Spawn the mandatory `code-simplifier` agent over the new `Rules/` folder and endpoint changes; apply accepted improvements (sync/async handle duplication is intentional); re-run affected tests.
- [ ] **Step 3:** Commit review fixes if any.
