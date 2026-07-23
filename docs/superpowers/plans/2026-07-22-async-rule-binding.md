# Async Rule-Document Binding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Load externalized rule documents into `AsyncSpecBase` specs — `RuleSerializer.DeserializeAsyncSpec<TModel>[, TMetadata]` and `ValidateAsyncSpec` — closing the gap the `AsyncSpecInSyncLoad` error message already promises.

**Architecture:** New `AsyncRuleBinder` (explanation) and `AsyncMetadataRuleBinder<TMetadata>` classes that **mirror** `RuleBinder` / `MetadataRuleBinder` (deliberate duplication per the project's avoid-over-DRYing convention). Sync registry leaves are lifted with `.ToAsyncSpec()`; async leaves are used directly; compositions use `AsyncSpecBase` operators; decorations use the Plan-1 async decorator builders. Higher-order subtrees bind fully sync then lift (core has no async quantifiers); an async leaf inside one is the new `AsyncSpecInHigherOrder` error.

**Tech Stack:** C# in `src/Motiv.Serialization` (+ tests in `src/Motiv.Serialization.Tests`), xUnit + Shouldly.

**Prerequisite:** Plan 1 (`2026-07-22-async-decorator-propositions.md`) is merged — `Spec.Build(asyncSpec).Create(name)` / `.WhenTrue(...).WhenFalse(...).Create()` exist.

**Test command:**

```bash
DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~Async" -v minimal
```

---

## Context for the implementing engineer

- **Mirror templates:** `src/Motiv.Serialization/RuleBinder.cs` (152 lines) and `src/Motiv.Serialization/MetadataRuleBinder.cs` (237 lines). Read both fully before starting — the async binders follow them method-for-method.
- **Key APIs:**
  - `SpecRegistryEntry` (`src/Motiv.Serialization/SpecRegistryEntry.cs`): `{ Name, ModelType, MetadataType, IsAsync, Spec (object), Description }`. Async entries hold an `AsyncSpecBase<TModel, TMetadata>` in `Spec`.
  - `SpecBase<TModel, TMetadata>.ToAsyncSpec()` → `AsyncSpecBase<TModel, TMetadata>` (lifts sync into async, justification-preserving via `SyncSpecAsyncAdapter`).
  - `AsyncSpecBase<TModel>` (metadata-erased) exposes `And/AndAlso/Or/OrElse/XOr/Not` returning `AsyncSpecBase<TModel, string>` and accepting sync or async operands (see `src/Motiv/AsyncSpecBase.cs:66-170`).
  - Explanation lift for a non-string async entry: check `src/Motiv/AsyncSpecBase.cs` for the public conversion (`ToAsyncExplanationSpec()` is used internally at line 47 — if it is not public, the metadata-erased `AsyncSpecBase<TModel>` overloads make explicit conversion unnecessary for composition, and for a bare leaf you can use the Plan-1 minimal decorator or the same adapter the sync path uses via `ToExplanationSpec` on the sync side; resolve when compiling and mirror whatever `RuleBinder.BindSpecLeaf` does: `entry.MetadataType == typeof(string) ? cast : convert`).
  - `RuleDocument` / `RuleNode` / `RuleOperator` (`.IsHigherOrder()`), `RuleError`, `RuleErrorCode` — all unchanged by this plan except one new enum member.
  - `RuleSerializer.Prepare` / `PrepareForValidation` / `ThrowIfInvalid` (private helpers in `src/Motiv.Serialization/RuleSerializer.cs:170-201`) — reuse them for the async entry points.
- **Test style:** see `src/Motiv.Serialization.Tests` — plain xUnit + Shouldly, JSON documents as raw string literals. Registry fixtures build specs inline.

### File map

| File | Responsibility |
|---|---|
| Modify `src/Motiv.Serialization/RuleErrorCode.cs` | add `AsyncSpecInHigherOrder` |
| Create `src/Motiv.Serialization/AsyncRuleBinder.cs` | explanation (string) async binding |
| Create `src/Motiv.Serialization/AsyncMetadataRuleBinder.cs` | typed-metadata async binding |
| Modify `src/Motiv.Serialization/RuleSerializer.cs` | `DeserializeAsyncSpec` / `ValidateAsyncSpec` entry points |
| Test `src/Motiv.Serialization.Tests/AsyncRuleBinderTests.cs` | leaves, compositions, decorations, higher-order, errors |
| Test `src/Motiv.Serialization.Tests/AsyncMetadataRuleBinderTests.cs` | metadata loads |
| Test `src/Motiv.Serialization.Tests/AsyncRuleSerializerTests.cs` | entry-point behavior, parameters, validate |

---

### Task 1: `AsyncSpecInHigherOrder` error code

**Files:**
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs:43` (append after `UnknownCollection`)

- [ ] **Step 1: Append the enum member** (no test — enum addition; the behavior test lands in Task 3)

```csharp
    /// <summary>A higher-order node references a collection path that is not registered.</summary>
    UnknownCollection,

    /// <summary>An async spec was referenced inside a higher-order subtree, which must be fully synchronous.</summary>
    AsyncSpecInHigherOrder
```

- [ ] **Step 2: Build**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet build src/Motiv.Serialization -v minimal`
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Serialization/RuleErrorCode.cs
git commit -m "feat: AsyncSpecInHigherOrder rule error code"
```

---

### Task 2: `AsyncRuleBinder` — leaves and compositions

**Files:**
- Create: `src/Motiv.Serialization.Tests/AsyncRuleBinderTests.cs`
- Create: `src/Motiv.Serialization/AsyncRuleBinder.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs` (minimal `DeserializeAsyncSpec<TModel>(string)` so tests can drive the binder through the public API)

- [ ] **Step 1: Write the failing tests**

```csharp
using Motiv;

namespace Motiv.Serialization.Tests;

public class AsyncRuleBinderTests
{
    private sealed record Customer(bool IsActive, int Age);

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("is-active",
            Spec.Build((Customer c) => c.IsActive)
                .WhenTrue("customer is active").WhenFalse("customer is inactive").Create())
        .Register("is-adult",
            Spec.Build((Customer c) => c.Age >= 18)
                .WhenTrue("customer is an adult").WhenFalse("customer is a minor").Create())
        .Register("passes-credit-check",
            Spec.BuildAsync((Customer _) => new ValueTask<bool>(true))
                .WhenTrue("passes credit check").WhenFalse("fails credit check").Create());

    [Fact]
    public async Task Should_bind_an_async_leaf()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "passes-credit-check" } }""");
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["passes credit check"]);
    }

    [Fact]
    public async Task Should_bind_a_sync_leaf_by_lifting_it()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "is-active" } }""");
        var result = await spec.EvaluateAsync(new Customer(false, 30));

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Assertions.ShouldBe(["customer is inactive"]);
    }

    [Fact]
    public async Task Should_bind_mixed_sync_async_compositions_with_sync_parity()
    {
        // Arrange
        var registry = Registry();
        var serializer = new RuleSerializer(registry);
        var syncDocument = """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "is-adult" } ] } }""";
        var mixedDocument = """{ "rule": { "and": [ { "spec": "is-active" }, { "spec": "passes-credit-check" } ] } }""";
        var model = new Customer(true, 30);

        // Act
        var syncResult = serializer.Deserialize<Customer>(syncDocument).Evaluate(model);
        var mixedResult = await serializer.DeserializeAsyncSpec<Customer>(mixedDocument).EvaluateAsync(model);

        // Assert — same de-noising and formatting discipline as the sync loader
        mixedResult.Satisfied.ShouldBeTrue();
        mixedResult.Assertions.ShouldBe(["customer is active", "passes credit check"]);
        syncResult.Assertions.ShouldBe(["customer is active", "customer is an adult"]);
    }

    [Fact]
    public async Task Should_short_circuit_or_else_across_the_async_boundary()
    {
        // Arrange — orElse with a satisfied sync left must not invoke the async right
        var calls = 0;
        var registry = new SpecRegistry()
            .Register("always-true",
                Spec.Build((Customer _) => true).WhenTrue("t").WhenFalse("f").Create())
            .Register("expensive",
                Spec.BuildAsync((Customer _) => { calls++; return new ValueTask<bool>(true); })
                    .WhenTrue("t2").WhenFalse("f2").Create());
        var serializer = new RuleSerializer(registry);
        var document = """{ "rule": { "orElse": [ { "spec": "always-true" }, { "spec": "expensive" } ] } }""";

        // Act
        var result = await serializer.DeserializeAsyncSpec<Customer>(document).EvaluateAsync(new Customer(true, 1));

        // Assert
        result.Satisfied.ShouldBeTrue();
        calls.ShouldBe(0);
    }

    [Fact]
    public async Task Should_bind_not_nodes()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>(
            """{ "rule": { "not": { "spec": "passes-credit-check" } } }""");
        var result = await spec.EvaluateAsync(new Customer(true, 30));

        // Assert
        result.Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_report_unknown_specs_with_the_same_error_as_the_sync_binder()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer>("""{ "rule": { "spec": "missing" } }""");

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_report_model_type_mismatches()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act — loading a Customer-spec document for int
        var act = () => serializer.DeserializeAsyncSpec<int>("""{ "rule": { "spec": "is-active" } }""");

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.ModelTypeMismatch);
    }
}
```

Check the exact JSON grammar against existing sync tests in `src/Motiv.Serialization.Tests` (e.g. how `orElse` and `not` are spelled in `rule.v1.json` / existing test documents) and adjust the raw strings to match before running.

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncRuleBinder" -v minimal`
Expected: compile error — `DeserializeAsyncSpec` does not exist.

- [ ] **Step 3: Implement `AsyncRuleBinder` (leaves, Not, compositions, decorations, root naming)**

`src/Motiv.Serialization/AsyncRuleBinder.cs` — mirrors `RuleBinder.cs`; higher-order handled in Task 3 (leave a `BindHigherOrder` that mirrors the structure but is implemented there):

```csharp
namespace Motiv.Serialization;

internal static class AsyncRuleBinder
{
    public static AsyncSpecBase<TModel, string>? Bind<TModel>(
        RuleDocument document,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var root = BindNode<TModel>(document.Root!, registry, errors);
        if (root is null)
            return null;

        return document.Name is null ? root : Spec.Build(root).Create(document.Name);
    }

    public static AsyncSpecBase<TModel, string>? BindNode<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var spec = BindOperator<TModel>(node, registry, errors);

        // Reported independently of leaf/composition success, mirroring RuleBinder.
        var hasObjectPayloadError = ReportObjectPayloadError(node, errors);

        if (spec is null || hasObjectPayloadError)
            return null;

        return Decorate(node, spec);
    }

    private static AsyncSpecBase<TModel, string>? BindOperator<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors) =>
        node.Operator switch
        {
            RuleOperator.Spec => BindSpecLeaf<TModel>(node, registry, errors),
            RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
            RuleOperator.Not => BindNode<TModel>(node.Children[0], registry, errors)?.Not(),
            _ when node.Operator.IsHigherOrder() => BindHigherOrder<TModel>(node, registry, errors),
            _ => BindComposition<TModel>(node, registry, errors)
        };

    private static bool ReportObjectPayloadError(RuleNode node, List<RuleError> errors)
    {
        if (!node.HasObjectPayloads)
            return false;

        errors.Add(new RuleError(node.Path, RuleErrorCode.MetadataTypeMismatch,
            "object 'whenTrue'/'whenFalse' payloads cannot be bound with explanation (string) semantics; " +
            "use a metadata load (DeserializeAsyncSpec<TModel, TMetadata>)"));
        return true;
    }

    private static AsyncSpecBase<TModel, string>? BindSpecLeaf<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var entry = registry.Find(node.SpecName!);
        if (entry is null)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.UnknownSpec,
                $"no spec is registered under the name '{node.SpecName}'"));
            return null;
        }

        if (entry.IsAsync)
        {
            if (entry.Spec is not AsyncSpecBase<TModel> asyncSpec)
            {
                errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                    $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                    $"loaded for model type '{typeof(TModel).Name}'"));
                return null;
            }

            return ToExplanation(asyncSpec, entry);
        }

        if (entry.Spec is not SpecBase<TModel> spec)
        {
            errors.Add(new RuleError(node.Path, RuleErrorCode.ModelTypeMismatch,
                $"'{node.SpecName}' has model type '{entry.ModelType.Name}' but the document is being " +
                $"loaded for model type '{typeof(TModel).Name}'"));
            return null;
        }

        var explanation = entry.MetadataType == typeof(string)
            ? (SpecBase<TModel, string>)spec
            : spec.ToExplanationSpec();
        return explanation.ToAsyncSpec();
    }

    private static AsyncSpecBase<TModel, string> ToExplanation<TModel>(
        AsyncSpecBase<TModel> asyncSpec, SpecRegistryEntry entry) =>
        entry.MetadataType == typeof(string)
            ? (AsyncSpecBase<TModel, string>)asyncSpec
            : asyncSpec.ToAsyncExplanationSpec();   // public abstract on AsyncSpecBase<TModel> (src/Motiv/AsyncSpecBase.cs:56)

    private static AsyncSpecBase<TModel, string>? BindExpressionLeaf<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes require the Motiv.Serialization.Expressions package"));
        return null;
    }

    private static AsyncSpecBase<TModel, string>? Decorate<TModel>(
        RuleNode node,
        AsyncSpecBase<TModel, string> spec)
    {
        if (node.WhenTrueText is not null)
        {
            var builder = Spec.Build(spec).WhenTrue(node.WhenTrueText).WhenFalse(node.WhenFalseText!);
            return node.Name is null ? builder.Create() : builder.Create(node.Name);
        }

        return node.Name is null ? spec : Spec.Build(spec).Create(node.Name);
    }

    private static AsyncSpecBase<TModel, string>? BindComposition<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        var children = node.Children
            .Select(child => BindNode<TModel>(child, registry, errors))
            .ToArray();

        if (children.Any(child => child is null))
            return null;

        return children.Aggregate((left, right) => node.Operator switch
        {
            RuleOperator.And => left!.And(right!),
            RuleOperator.Or => left!.Or(right!),
            RuleOperator.XOr => left!.XOr(right!),
            RuleOperator.AndAlso => left!.AndAlso(right!),
            _ => left!.OrElse(right!)
        });
    }

    private static AsyncSpecBase<TModel, string>? BindHigherOrder<TModel>(
        RuleNode node, SpecRegistry registry, List<RuleError> errors) =>
        throw new NotImplementedException("Task 3");
}
```


- [ ] **Step 4: Add the minimal serializer entry point** (full overload set lands in Task 5)

In `src/Motiv.Serialization/RuleSerializer.cs`, after the sync `Deserialize<TModel>` group:

```csharp
    /// <summary>
    /// Loads a rule document into an asynchronous explanation spec, resolving spec references
    /// against the registry. Sync references are lifted; async references are used directly.
    /// Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = Prepare(json, null, errors);
        ThrowIfInvalid(errors);

        var spec = AsyncRuleBinder.Bind<TModel>(document!, _registry, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }
```

- [ ] **Step 5: Run the tests**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncRuleBinder" -v minimal`
Expected: PASS (higher-order tests come in Task 3).

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests/AsyncRuleBinderTests.cs
git commit -m "feat: async rule binding for leaves, compositions, and decorations"
```

---

### Task 3: Higher-order subtrees in async loads

**Files:**
- Modify: `src/Motiv.Serialization.Tests/AsyncRuleBinderTests.cs` (append tests)
- Modify: `src/Motiv.Serialization/AsyncRuleBinder.cs` (replace `BindHigherOrder` stub)

- [ ] **Step 1: Write the failing tests** (append to `AsyncRuleBinderTests`; reuse the higher-order JSON shape from the existing sync higher-order tests — check `src/Motiv.Serialization.Tests` for the exact `asAllSatisfied`/`path` grammar)

```csharp
    private sealed record Order(decimal Total);
    private sealed record Account(IReadOnlyList<Order> Orders);

    private static SpecRegistry HigherOrderRegistry() => new SpecRegistry()
        .Register("is-large-order",
            Spec.Build((Order o) => o.Total >= 100m)
                .WhenTrue("order is large").WhenFalse("order is small").Create())
        .Register("async-order-check",
            Spec.BuildAsync((Order _) => new ValueTask<bool>(true))
                .WhenTrue("checked").WhenFalse("unchecked").Create())
        .RegisterCollection<Account, Order>("orders", a => a.Orders);

    [Fact]
    public async Task Should_bind_a_fully_sync_higher_order_subtree_by_lifting_it()
    {
        // Arrange
        var serializer = new RuleSerializer(HigherOrderRegistry());
        var document = """
            { "rule": { "asAllSatisfied": { "spec": "is-large-order" }, "path": "orders", "name": "all orders large" } }
            """;

        // Act
        var spec = serializer.DeserializeAsyncSpec<Account>(document);
        var result = await spec.EvaluateAsync(new Account([new Order(150m), new Order(200m)]));

        // Assert
        result.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_async_specs_inside_higher_order_subtrees()
    {
        // Arrange
        var serializer = new RuleSerializer(HigherOrderRegistry());
        var document = """
            { "rule": { "asAllSatisfied": { "spec": "async-order-check" }, "path": "orders", "name": "all checked" } }
            """;

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Account>(document);

        // Assert
        var errors = act.ShouldThrow<RuleSerializationException>().Errors;
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AsyncSpecInHigherOrder);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncRuleBinder" -v minimal`
Expected: FAIL — `NotImplementedException` from the stub.

- [ ] **Step 3: Implement `BindHigherOrder`** — bind the whole node through the sync binder, remap async-leaf errors, lift:

```csharp
    private static AsyncSpecBase<TModel, string>? BindHigherOrder<TModel>(
        RuleNode node, SpecRegistry registry, List<RuleError> errors)
    {
        // Core has no async quantifiers: the whole higher-order subtree binds synchronously
        // and lifts. An async leaf inside it is therefore a distinct, actionable error.
        var errorCountBefore = errors.Count;
        var spec = RuleBinder.BindNode<TModel>(node, registry, errors);

        for (var i = errorCountBefore; i < errors.Count; i++)
        {
            if (errors[i].Code == RuleErrorCode.AsyncSpecInSyncLoad)
                errors[i] = new RuleError(errors[i].Path, RuleErrorCode.AsyncSpecInHigherOrder,
                    "async specs cannot be used inside a higher-order subtree; " +
                    "higher-order propositions evaluate synchronously");
        }

        return spec?.ToAsyncSpec();
    }
```

Note `errors[i] = ...` requires `List<RuleError>` index assignment — it is a `List`, so this works. Note also that `Decorate` must not run twice: `RuleBinder.BindNode` already applies the node's decorations, so in `BindNode` (async) route higher-order nodes around the async `Decorate` call — restructure `BindOperator`/`BindNode` so the higher-order branch returns directly from `BindNode`:

```csharp
    public static AsyncSpecBase<TModel, string>? BindNode<TModel>(
        RuleNode node,
        SpecRegistry registry,
        List<RuleError> errors)
    {
        // Higher-order subtrees bind (and decorate) entirely through the sync binder, then lift.
        if (node.Operator.IsHigherOrder())
            return BindHigherOrder<TModel>(node, registry, errors);

        var spec = BindOperator<TModel>(node, registry, errors);
        var hasObjectPayloadError = ReportObjectPayloadError(node, errors);

        if (spec is null || hasObjectPayloadError)
            return null;

        return Decorate(node, spec);
    }
```

(and remove the higher-order arm from `BindOperator`).

- [ ] **Step 4: Run the tests**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncRuleBinder" -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests/AsyncRuleBinderTests.cs
git commit -m "feat: higher-order subtrees in async loads bind sync and lift"
```

---

### Task 4: `AsyncMetadataRuleBinder<TMetadata>`

**Files:**
- Create: `src/Motiv.Serialization.Tests/AsyncMetadataRuleBinderTests.cs`
- Create: `src/Motiv.Serialization/AsyncMetadataRuleBinder.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs` (minimal `DeserializeAsyncSpec<TModel, TMetadata>(string)`)

- [ ] **Step 1: Write the failing tests**

```csharp
using Motiv;

namespace Motiv.Serialization.Tests;

public class AsyncMetadataRuleBinderTests
{
    private sealed record Customer(bool IsActive);
    private sealed record Verdict(string Code);

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("is-active",
            Spec.Build((Customer c) => c.IsActive)
                .WhenTrue("active").WhenFalse("inactive").Create())
        .Register("credit-check",
            Spec.BuildAsync((Customer _) => new ValueTask<bool>(true))
                .WhenTrue("passes").WhenFalse("fails").Create());

    [Fact]
    public async Task Should_bind_object_payloads_over_an_async_subtree()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var document = """
            {
              "rule": {
                "and": [ { "spec": "is-active" }, { "spec": "credit-check" } ],
                "name": "approved",
                "whenTrue": { "code": "OK" },
                "whenFalse": { "code": "DENIED" }
              }
            }
            """;

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, Verdict>(document);
        var result = await spec.EvaluateAsync(new Customer(true));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Values.ShouldBe([new Verdict("OK")]);
        result.Assertions.ShouldBe(["approved == true"]);
    }

    [Fact]
    public async Task Should_route_string_metadata_loads_to_the_explanation_path()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer, string>("""{ "rule": { "spec": "credit-check" } }""");
        var result = await spec.EvaluateAsync(new Customer(true));

        // Assert
        result.Assertions.ShouldBe(["passes"]);
    }

    [Fact]
    public void Should_report_string_payloads_in_metadata_loads()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());
        var document = """
            { "rule": { "spec": "credit-check", "whenTrue": "yes", "whenFalse": "no" } }
            """;

        // Act
        var act = () => serializer.DeserializeAsyncSpec<Customer, Verdict>(document);

        // Assert
        act.ShouldThrow<RuleSerializationException>()
            .Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.MetadataTypeMismatch);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncMetadataRuleBinder" -v minimal`
Expected: compile error.

- [ ] **Step 3: Implement `AsyncMetadataRuleBinder<TMetadata>`** — mirror `MetadataRuleBinder<TMetadata>` with these substitutions (read the sync file side-by-side; keep its method order and comments style):

- Bind results are `AsyncSpecBase<TModel, TMetadata>`.
- `BindRemetadatized`: underlying binds via `AsyncRuleBinder.BindOperator<TModel>` (make that method `internal`-visible within the assembly — it already is, being in the same assembly); decorate via the Plan-1 async metadata builder: `Spec.Build(underlying).WhenTrue(whenTrue!).WhenFalse(whenFalse!).Create(node.Name!)`.
- `BindSpecLeaf`: async entries cast to `AsyncSpecBase<TModel, TMetadata>` (report `MetadataTypeMismatch` with the sync binder's message shape when the cast fails); sync entries cast to `SpecBase<TModel, TMetadata>` then `.ToAsyncSpec()`.
- `BindComposition`: aggregate with `AsyncSpecBase<TModel, TMetadata>` operators (`And/Or/XOr/AndAlso/OrElse` at `src/Motiv/AsyncSpecBase.cs:380+`).
- Higher-order: bind the whole node via the **sync** `MetadataRuleBinder<TMetadata>` (instantiate one with the same registry/options), remap `AsyncSpecInSyncLoad` → `AsyncSpecInHigherOrder` exactly as Task 3 did, then `.ToAsyncSpec()`.
- `DeserializePayload` copies verbatim (it is payload-shaped, not sync-shaped).

- [ ] **Step 4: Add the minimal typed entry point** in `RuleSerializer`:

```csharp
    /// <summary>
    /// Loads a rule document into an asynchronous typed-metadata spec, resolving spec references
    /// against the registry. Throws when the document is invalid.
    /// </summary>
    /// <typeparam name="TModel">The model type the document's spec references were registered for.</typeparam>
    /// <typeparam name="TMetadata">The metadata type object payloads deserialize to and registry entries must yield.</typeparam>
    /// <param name="json">The rule document to load.</param>
    /// <returns>The composed async spec.</returns>
    /// <exception cref="RuleSerializationException">The document is structurally or semantically invalid.</exception>
    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json)
    {
        if (typeof(TMetadata) == typeof(string))
            return (AsyncSpecBase<TModel, TMetadata>)(object)DeserializeAsyncSpec<TModel>(json);

        var errors = new List<RuleError>();
        var document = Prepare(json, null, errors);
        ThrowIfInvalid(errors);

        var spec = new AsyncMetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document!, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }
```

- [ ] **Step 5: Run the tests**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncMetadataRuleBinder" -v minimal`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests/AsyncMetadataRuleBinderTests.cs
git commit -m "feat: typed-metadata async rule binding"
```

---

### Task 5: Parameter overloads + `ValidateAsyncSpec`

**Files:**
- Create: `src/Motiv.Serialization.Tests/AsyncRuleSerializerTests.cs`
- Modify: `src/Motiv.Serialization/RuleSerializer.cs`

- [ ] **Step 1: Write the failing tests** (parameter grammar: copy a parameterized document from the existing sync parameter tests in `src/Motiv.Serialization.Tests` and adapt)

```csharp
using Motiv;

namespace Motiv.Serialization.Tests;

public class AsyncRuleSerializerTests
{
    private sealed record Customer(bool IsActive);

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("credit-check",
            Spec.BuildAsync((Customer _) => new ValueTask<bool>(true))
                .WhenTrue("passes").WhenFalse("fails").Create());

    [Fact]
    public void Should_accumulate_errors_in_validate_async_instead_of_throwing()
    {
        // Arrange
        var serializer = new RuleSerializer(Registry());

        // Act
        var errors = serializer.ValidateAsyncSpec<Customer>("""{ "rule": { "spec": "missing" } }""");

        // Assert
        errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public void Should_validate_async_documents_that_reference_async_specs_as_valid()
    {
        // Arrange — the same document a sync Validate<TModel> would reject
        var serializer = new RuleSerializer(Registry());
        var document = """{ "rule": { "spec": "credit-check" } }""";

        // Act & Assert
        serializer.Validate<Customer>(document)
            .ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.AsyncSpecInSyncLoad);
        serializer.ValidateAsyncSpec<Customer>(document).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_supply_parameters_to_async_loads()
    {
        // Arrange — adapt to the real parameter grammar from the sync parameter tests
        var serializer = new RuleSerializer(Registry());
        var document = """
            {
              "parameters": { "label": { "type": "string" } },
              "rule": { "spec": "credit-check", "name": "{label}" }
            }
            """;

        // Act
        var spec = serializer.DeserializeAsyncSpec<Customer>(document, new { label = "credit gate" });
        var result = await spec.EvaluateAsync(new Customer(true));

        // Assert
        result.Assertions.ShouldBe(["credit gate == true"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~AsyncRuleSerializer" -v minimal`
Expected: compile error.

- [ ] **Step 3: Implement the full overload surface** in `RuleSerializer.cs`, exactly mirroring the sync groups (XML docs mirroring the sync ones; `DeserializeAsyncSpec<TModel>` single-arg forwards to the dictionary overload — refactor the Task-2/Task-4 minimal versions into this shape):

```csharp
    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json) =>
        DeserializeAsyncSpec<TModel>(json, (IReadOnlyDictionary<string, object?>?)null);

    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json, object? parameters) =>
        DeserializeAsyncSpec<TModel>(json, RuleParameterResolver.ToDictionary(parameters));

    public AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = AsyncRuleBinder.Bind<TModel>(document!, _registry, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json) =>
        DeserializeAsyncSpec<TModel, TMetadata>(json, (IReadOnlyDictionary<string, object?>?)null);

    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json, object? parameters) =>
        DeserializeAsyncSpec<TModel, TMetadata>(json, RuleParameterResolver.ToDictionary(parameters));

    public AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(
        string json,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        if (typeof(TMetadata) == typeof(string))
            return (AsyncSpecBase<TModel, TMetadata>)(object)DeserializeAsyncSpec<TModel>(json, parameters);

        var errors = new List<RuleError>();
        var document = Prepare(json, parameters, errors);
        ThrowIfInvalid(errors);

        var spec = new AsyncMetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document!, errors);
        ThrowIfInvalid(errors);
        return spec!;
    }

    public IReadOnlyList<RuleError> ValidateAsyncSpec<TModel>(string json)
    {
        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            AsyncRuleBinder.Bind<TModel>(document, _registry, errors);
        return errors;
    }

    public IReadOnlyList<RuleError> ValidateAsyncSpec<TModel, TMetadata>(string json)
    {
        if (typeof(TMetadata) == typeof(string))
            return ValidateAsyncSpec<TModel>(json);

        var errors = new List<RuleError>();
        var document = PrepareForValidation(json, errors);
        if (document?.Root is not null && errors.Count == 0)
            new AsyncMetadataRuleBinder<TMetadata>(_registry, _options).Bind<TModel>(document, errors);
        return errors;
    }
```

(Add XML doc comments to every public method, mirroring the sync equivalents' wording with "asynchronous" adjectives.)

- [ ] **Step 4: Run the whole serialization test project**

Run: `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test src/Motiv.Serialization.Tests -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization src/Motiv.Serialization.Tests
git commit -m "feat: DeserializeAsyncSpec/ValidateAsyncSpec entry points with parameter overloads"
```

---

### Task 6: Full-solution verification + review

- [ ] **Step 1: Run the entire solution suite** (example projects assert justification text): `DOTNET_ROOT=$HOME/.dotnet PATH="$HOME/.dotnet:$PATH" dotnet test *.sln -v minimal` → all PASS.
- [ ] **Step 2: Spawn the mandatory `code-simplifier` agent** over `src/Motiv.Serialization` changes; apply accepted improvements; re-run affected tests. Watch for over-DRYing — mirrored binders are intentionally separate.
- [ ] **Step 3: Commit** `git commit -m "refactor: simplify async binders per review"` (if changes).
