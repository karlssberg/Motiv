# Higher-Order Rule-Document Support — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the five higher-order rule-document node kinds parse and evaluate in the synchronous explanation loader, using host-registered collection selectors.

**Architecture:** The parser learns the higher-order keys plus their `n`/`path` siblings. `SpecRegistry` gains a parallel registration of typed collection selectors keyed by `(parentType, path)`. The binder, at a higher-order node, resolves the registered `CollectionBinding<TParent>`, binds the child subtree in element space, builds the native higher-order proposition (`Spec.Build(child).As*(...).Create(...)`), and projects it onto the parent with `ChangeModelTo<TParent>(selector)`. The existing `Decorate` tail applies `name`/`whenTrue`/`whenFalse` uniformly, so higher-order nodes are decorated like any other node. No reflection; every generic type argument is compile-time. The AspNet layer is unchanged — it already deserializes via the registry.

**Tech Stack:** C# / .NET 10, `Motiv.Serialization`; xUnit + Shouldly tests; TypeScript wire contract in `@motiv/rules-core`.

**Design spec:** `docs/superpowers/specs/2026-07-19-higher-order-serialization-design.md`

**Conventions to follow (from the existing code):**
- Tests: xUnit `[Fact]`/`[Theory]`/`[InlineData]`, Shouldly assertions (`ShouldBe`, `ShouldBeEmpty`, `ShouldHaveSingleItem`, `ShouldContain`, `Should.Throw<T>`), Arrange/Act/Assert comments, raw-string-literal JSON. Namespace `Motiv.Serialization.Tests`.
- `Motiv.Serialization.Tests` has `InternalsVisibleTo` (existing tests read `entry.Spec`, an `internal` member), so tests may use internal types (`RuleOperator`, `HigherOrder`, `CollectionBinding<>`, `FindCollection`).
- Run a single test: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~<ClassName>.<MethodName>"`.

**Synthesized statements (PIN — used by both the binder and every `expected` in tests):**

| Operator | whenTrue | whenFalse |
|---|---|---|
| `AsAllSatisfied` | `all satisfied` | `not all satisfied` |
| `AsAnySatisfied` | `any satisfied` | `none satisfied` |
| `AsNSatisfied` | `exactly {n} satisfied` | `not exactly {n} satisfied` |
| `AsAtLeastNSatisfied` | `at least {n} satisfied` | `fewer than {n} satisfied` |
| `AsAtMostNSatisfied` | `at most {n} satisfied` | `more than {n} satisfied` |

---

## Task 1: Parse higher-order nodes (grammar model + parser)

**Files:**
- Modify: `src/Motiv.Serialization/RuleOperator.cs`
- Modify: `src/Motiv.Serialization/RuleNode.cs`
- Modify: `src/Motiv.Serialization/RuleDocumentParser.cs`
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (temporary guard, replaced in Task 4)
- Test: `src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `RuleSerializerValidateTests`)

```csharp
[Theory]
[InlineData("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": "xs" } }""")]
[InlineData("""{ "rule": { "asAnySatisfied": { "spec": "a" }, "path": "xs" } }""")]
[InlineData("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": 2, "path": "xs" } }""")]
[InlineData("""{ "rule": { "asAtLeastNSatisfied": { "spec": "a" }, "n": 1, "path": "xs" } }""")]
[InlineData("""{ "rule": { "asAtMostNSatisfied": { "spec": "a" }, "n": 3, "path": "xs" } }""")]
[InlineData("""{ "rule": { "asAllSatisfied": { "and": [ { "spec": "a" }, { "spec": "b" } ] }, "path": "xs" } }""")]
public void Should_accept_valid_higher_order_nodes(string json) =>
    Validate(json).ShouldBeEmpty();

[Fact]
public void Should_reject_a_count_on_a_quantifier_that_takes_none()
{
    var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "n": 2, "path": "xs" } }""");
    var error = errors.ShouldHaveSingleItem();
    error.Code.ShouldBe(RuleErrorCode.InvalidNode);
    error.Path.ShouldBe("$.rule");
    error.Message.ShouldContain("'n'");
}

[Fact]
public void Should_reject_a_missing_count_on_an_n_quantifier()
{
    var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "path": "xs" } }""");
    var error = errors.ShouldHaveSingleItem();
    error.Code.ShouldBe(RuleErrorCode.InvalidNode);
    error.Message.ShouldContain("'n'");
}

[Fact]
public void Should_reject_a_missing_path()
{
    var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" } } }""");
    var error = errors.ShouldHaveSingleItem();
    error.Code.ShouldBe(RuleErrorCode.InvalidNode);
    error.Message.ShouldContain("'path'");
}

[Fact]
public void Should_reject_an_empty_path()
{
    var errors = Validate("""{ "rule": { "asAllSatisfied": { "spec": "a" }, "path": " " } }""");
    errors.ShouldContain(e => e.Code == RuleErrorCode.InvalidNode && e.Path == "$.rule.path");
}

[Fact]
public void Should_reject_a_parameter_count_until_parameters_are_supported()
{
    var errors = Validate("""{ "rule": { "asNSatisfied": { "spec": "a" }, "n": "@minLarge", "path": "xs" } }""");
    var error = errors.ShouldHaveSingleItem();
    error.Code.ShouldBe(RuleErrorCode.InvalidNode);
    error.Message.ShouldContain("parameter counts are not yet supported");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleSerializerValidateTests"`
Expected: the new tests FAIL — currently every higher-order key yields an `InvalidNode` "not yet supported by this loader", so `Should_accept_valid_higher_order_nodes` reports errors instead of none.

- [ ] **Step 3: Add the operator members**

In `src/Motiv.Serialization/RuleOperator.cs`, extend the enum:

```csharp
internal enum RuleOperator
{
    Spec,
    Expression,
    And,
    Or,
    XOr,
    AndAlso,
    OrElse,
    Not,
    AsAllSatisfied,
    AsAnySatisfied,
    AsNSatisfied,
    AsAtLeastNSatisfied,
    AsAtMostNSatisfied
}
```

- [ ] **Step 4: Add the node fields**

In `src/Motiv.Serialization/RuleNode.cs`, add two properties (do **not** reuse `Path`, which is the node's location):

```csharp
public int? N { get; set; }

public string? CollectionPath { get; set; }
```

- [ ] **Step 5: Teach the parser the higher-order keys, `n`, and `path`**

In `src/Motiv.Serialization/RuleDocumentParser.cs`:

(a) Delete the now-obsolete `HigherOrderProperties` array (lines 7–10) — its keys are handled explicitly below.

(b) In `ParseNode`, capture `n`/`path` and recognise the higher-order operators. Replace the property loop's operator/default handling so the switch reads:

```csharp
var operators = new List<JsonProperty>();
JsonElement? whenTrue = null;
JsonElement? whenFalse = null;
JsonElement? nElement = null;
string? collectionPath = null;
var hasPath = false;
string? name = null;

foreach (var property in element.EnumerateObject())
{
    switch (property.Name)
    {
        case "spec" or "expression" or "not" or "and" or "or" or "xor" or "andAlso" or "orElse"
            or "asAllSatisfied" or "asAnySatisfied" or "asNSatisfied"
            or "asAtLeastNSatisfied" or "asAtMostNSatisfied":
            operators.Add(property);
            break;
        case "whenTrue":
            whenTrue = property.Value;
            break;
        case "whenFalse":
            whenFalse = property.Value;
            break;
        case "n":
            nElement = property.Value;
            break;
        case "path":
            hasPath = true;
            collectionPath = ReadNonEmptyString(property.Value, $"{path}.path", errors);
            break;
        case "name":
            name = ReadNonEmptyString(property.Value, $"{path}.name", errors);
            break;
        default:
            errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                $"unknown property '{property.Name}'"));
            break;
    }
}
```

(c) In `ParseOperator`, add the five higher-order cases (single child, like `not`) before the `default`:

```csharp
case "asAllSatisfied":
case "asAnySatisfied":
case "asNSatisfied":
case "asAtLeastNSatisfied":
case "asAtMostNSatisfied":
{
    var child = ParseNode(property.Value, $"{path}.{property.Name}", depth + 1, errors);
    if (child is null)
        return null;
    var @operator = property.Name switch
    {
        "asAllSatisfied" => RuleOperator.AsAllSatisfied,
        "asAnySatisfied" => RuleOperator.AsAnySatisfied,
        "asNSatisfied" => RuleOperator.AsNSatisfied,
        "asAtLeastNSatisfied" => RuleOperator.AsAtLeastNSatisfied,
        _ => RuleOperator.AsAtMostNSatisfied
    };
    var node = new RuleNode(@operator, path);
    node.Children.Add(child);
    return node;
}
```

(d) After the `var node = ParseOperator(...)` call in `ParseNode` (before `node.Name = name;`), apply and validate the higher-order siblings. Add this helper and call it:

```csharp
// in ParseNode, right after: var node = ParseOperator(operators[0], path, depth, errors);
ParsePayloads(node, whenTrue, whenFalse, path, errors);
if (node is null)
    return null;

ApplyHigherOrder(node, nElement, collectionPath, hasPath, path, errors);

node.Name = name;
return node;
```

```csharp
private static readonly RuleOperator[] NQuantifiers =
    [RuleOperator.AsNSatisfied, RuleOperator.AsAtLeastNSatisfied, RuleOperator.AsAtMostNSatisfied];

private static readonly RuleOperator[] HigherOrderOperators =
    [RuleOperator.AsAllSatisfied, RuleOperator.AsAnySatisfied, .. NQuantifiers];

private static void ApplyHigherOrder(
    RuleNode node, JsonElement? nElement, string? collectionPath, bool hasPath,
    string path, List<RuleError> errors)
{
    var isHigherOrder = Array.IndexOf(HigherOrderOperators, node.Operator) >= 0;

    if (!isHigherOrder)
    {
        if (nElement is not null)
            errors.Add(new RuleError($"{path}.n", RuleErrorCode.InvalidNode, "unknown property 'n'"));
        if (hasPath)
            errors.Add(new RuleError($"{path}.path", RuleErrorCode.InvalidNode, "unknown property 'path'"));
        return;
    }

    if (!hasPath)
        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
            "higher-order nodes require a 'path' to the collection"));
    else
        node.CollectionPath = collectionPath;

    var takesCount = Array.IndexOf(NQuantifiers, node.Operator) >= 0;

    if (nElement is { ValueKind: JsonValueKind.String })
    {
        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
            "parameter counts are not yet supported; use an integer 'n'"));
    }
    else if (nElement is { } e)
    {
        if (!takesCount)
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                $"'n' is not valid for '{node.Operator}'"));
        else if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n))
            node.N = n;
        else
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode, "'n' must be an integer"));
    }
    else if (takesCount)
    {
        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
            $"'{node.Operator}' requires an integer 'n'"));
    }
}
```

Note: `collectionPath` is only non-null when `ReadNonEmptyString` accepted it; an empty/whitespace `path` already produced a `$.rule.path` error inside that call, and `node.CollectionPath` is left null (bind will not be reached because Validate/Deserialize sees the error).

- [ ] **Step 6: Add a temporary binder guard so higher-order nodes fail loudly (replaced in Task 4)**

In `src/Motiv.Serialization/RuleBinder.cs`, add an arm to the `BindNode` switch so a higher-order node cannot fall through to `BindComposition` (which would silently mis-bind a single child):

```csharp
var spec = node.Operator switch
{
    RuleOperator.Spec => BindSpecLeaf<TModel>(node, registry, errors),
    RuleOperator.Expression => BindExpressionLeaf<TModel>(node, errors),
    RuleOperator.Not => BindNode<TModel>(node.Children[0], registry, errors)?.Not(),
    RuleOperator.AsAllSatisfied or RuleOperator.AsAnySatisfied or RuleOperator.AsNSatisfied
        or RuleOperator.AsAtLeastNSatisfied or RuleOperator.AsAtMostNSatisfied
        => BindHigherOrderNotYetImplemented<TModel>(node, errors),
    _ => BindComposition<TModel>(node, registry, errors)
};
```

```csharp
// TEMPORARY — replaced by real higher-order binding in Task 4.
private static SpecBase<TModel, string>? BindHigherOrderNotYetImplemented<TModel>(
    RuleNode node, List<RuleError> errors)
{
    errors.Add(new RuleError(node.Path, RuleErrorCode.InvalidNode,
        "higher-order binding is not yet implemented"));
    return null;
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleSerializerValidateTests"`
Expected: PASS (all new and existing validate tests green).

- [ ] **Step 8: Commit**

```bash
git add src/Motiv.Serialization/RuleOperator.cs src/Motiv.Serialization/RuleNode.cs \
        src/Motiv.Serialization/RuleDocumentParser.cs src/Motiv.Serialization/RuleBinder.cs \
        src/Motiv.Serialization.Tests/RuleSerializerValidateTests.cs
git commit -m "feat(rules-serialization): parse higher-order nodes, n and path"
```

---

## Task 2: `HigherOrder.Build` — construct the native higher-order proposition

**Files:**
- Create: `src/Motiv.Serialization/HigherOrder.cs`
- Test: `src/Motiv.Serialization.Tests/HigherOrderBuildTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using Motiv.Serialization;
using static Motiv.Serialization.Tests.SpecAssertions;

namespace Motiv.Serialization.Tests;

public class HigherOrderBuildTests
{
    private static SpecBase<int, string> IsEven { get; } =
        Spec.Build((int i) => i % 2 == 0).WhenTrue("even").WhenFalse("odd").Create();

    private static readonly IEnumerable<int> AllEven = [2, 4, 6];
    private static readonly IEnumerable<int> SomeEven = [1, 2, 3];
    private static readonly IEnumerable<int> NoneEven = [1, 3, 5];

    [Fact]
    public void Should_build_all_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAllSatisfied, null);
        var expected = Spec.Build(IsEven).AsAllSatisfied()
            .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_any_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAnySatisfied, null);
        var expected = Spec.Build(IsEven).AsAnySatisfied()
            .WhenTrue("any satisfied").WhenFalse("none satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_exactly_n_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsNSatisfied, 2);
        var expected = Spec.Build(IsEven).AsNSatisfied(2)
            .WhenTrue("exactly 2 satisfied").WhenFalse("not exactly 2 satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_at_least_n_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAtLeastNSatisfied, 2);
        var expected = Spec.Build(IsEven).AsAtLeastNSatisfied(2)
            .WhenTrue("at least 2 satisfied").WhenFalse("fewer than 2 satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }

    [Fact]
    public void Should_build_at_most_n_satisfied_like_the_fluent_equivalent()
    {
        var built = HigherOrder.Build(IsEven, RuleOperator.AsAtMostNSatisfied, 1);
        var expected = Spec.Build(IsEven).AsAtMostNSatisfied(1)
            .WhenTrue("at most 1 satisfied").WhenFalse("more than 1 satisfied").Create();
        ShouldBehaveIdentically(built, expected, AllEven, SomeEven, NoneEven);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~HigherOrderBuildTests"`
Expected: FAIL to compile — `HigherOrder` does not exist.

- [ ] **Step 3: Implement `HigherOrder.Build`**

Create `src/Motiv.Serialization/HigherOrder.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Builds a native higher-order proposition over <c>IEnumerable&lt;TElement&gt;</c> from a bound
/// element spec, using synthesized default statements. Document decoration (name/whenTrue/whenFalse)
/// is applied afterwards by the binder's <c>Decorate</c> tail, uniformly with every other node.
/// </summary>
internal static class HigherOrder
{
    public static SpecBase<IEnumerable<TElement>, string> Build<TElement>(
        SpecBase<TElement, string> child, RuleOperator @operator, int? n) =>
        @operator switch
        {
            RuleOperator.AsAllSatisfied => Spec.Build(child).AsAllSatisfied()
                .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create(),
            RuleOperator.AsAnySatisfied => Spec.Build(child).AsAnySatisfied()
                .WhenTrue("any satisfied").WhenFalse("none satisfied").Create(),
            RuleOperator.AsNSatisfied => Spec.Build(child).AsNSatisfied(n!.Value)
                .WhenTrue($"exactly {n.Value} satisfied").WhenFalse($"not exactly {n.Value} satisfied").Create(),
            RuleOperator.AsAtLeastNSatisfied => Spec.Build(child).AsAtLeastNSatisfied(n!.Value)
                .WhenTrue($"at least {n.Value} satisfied").WhenFalse($"fewer than {n.Value} satisfied").Create(),
            RuleOperator.AsAtMostNSatisfied => Spec.Build(child).AsAtMostNSatisfied(n!.Value)
                .WhenTrue($"at most {n.Value} satisfied").WhenFalse($"more than {n.Value} satisfied").Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, "not a higher-order operator")
        };
}
```

Note: `Create()` is called **without a name** on purpose. Per the Motiv naming
rule, an unnamed explanation proposition keeps the `whenTrue`/`whenFalse` strings
as the assertions; a named `Create("all satisfied")` would instead make the
assertion `"all satisfied == true"` and demote the string to metadata. The binder
and every test `expected` must use the identical unnamed form. If the no-arg
`.Create()` overload is unavailable on the higher-order WithName factory, the
correct fix is to keep it unnamed via the available overload — do **not** switch
to a named `Create`, and apply the same change to the test `expected` values.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~HigherOrderBuildTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization/HigherOrder.cs src/Motiv.Serialization.Tests/HigherOrderBuildTests.cs
git commit -m "feat(rules-serialization): HigherOrder.Build over IEnumerable<TElement>"
```

---

## Task 3: Collection registration (`SpecRegistry` + `CollectionBinding` + `BindElement`)

**Files:**
- Create: `src/Motiv.Serialization/CollectionBinding.cs`
- Modify: `src/Motiv.Serialization/SpecRegistry.cs`
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (expose `BindElement`)
- Test: `src/Motiv.Serialization.Tests/SpecRegistryTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `SpecRegistryTests`)

```csharp
private sealed record Order(decimal Total);
private sealed record Cart(IReadOnlyList<Order> Orders);

[Fact]
public void Should_find_a_registered_collection_and_report_its_element_type()
{
    var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);

    var binding = registry.FindCollection<Cart>("orders");

    binding.ShouldNotBeNull();
    binding.ElementType.ShouldBe(typeof(Order));
}

[Fact]
public void Should_return_null_for_an_unregistered_collection_path()
{
    var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);
    registry.FindCollection<Cart>("items").ShouldBeNull();
}

[Fact]
public void Should_scope_collections_by_parent_type()
{
    var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);
    registry.FindCollection<Order>("orders").ShouldBeNull();
}

[Fact]
public void Should_reject_a_duplicate_collection_registration()
{
    var registry = new SpecRegistry().RegisterCollection<Cart, Order>("orders", c => c.Orders);
    Should.Throw<ArgumentException>(() => registry.RegisterCollection<Cart, Order>("orders", c => c.Orders));
}

[Fact]
public void Should_reject_an_empty_collection_path()
{
    Should.Throw<ArgumentException>(() =>
        new SpecRegistry().RegisterCollection<Cart, Order>(" ", c => c.Orders));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~SpecRegistryTests"`
Expected: FAIL to compile — `RegisterCollection` / `FindCollection` / `ElementType` do not exist.

- [ ] **Step 3: Create `CollectionBinding`**

Create `src/Motiv.Serialization/CollectionBinding.cs`:

```csharp
namespace Motiv.Serialization;

/// <summary>
/// A host-registered projection from a parent model to one of its collections, able to bind a
/// higher-order rule node that operates over that collection. Keeps the element type a compile-time
/// generic argument (captured at registration), so binding needs no reflection.
/// </summary>
internal abstract class CollectionBinding<TParent>
{
    public abstract Type ElementType { get; }

    public abstract SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors);
}

internal sealed class CollectionBinding<TParent, TElement>(Func<TParent, IEnumerable<TElement>> selector)
    : CollectionBinding<TParent>
{
    public override Type ElementType => typeof(TElement);

    public override SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors)
    {
        var child = RuleBinder.BindElement<TElement>(node.Children[0], registry, errors);
        if (child is null)
            return null;

        var higherOrder = HigherOrder.Build(child, node.Operator, node.N);
        return higherOrder.ChangeModelTo<TParent>(selector);
    }
}
```

- [ ] **Step 4: Add registration + lookup to `SpecRegistry`**

In `src/Motiv.Serialization/SpecRegistry.cs`, add a field beside `_entries` and two members:

```csharp
private readonly Dictionary<(Type Parent, string Path), object> _collections = new();
```

```csharp
/// <summary>
/// Registers a projection from <typeparamref name="TParent"/> to a collection of
/// <typeparamref name="TElement"/>, referenced by higher-order rule nodes via their <c>path</c>.
/// </summary>
public SpecRegistry RegisterCollection<TParent, TElement>(
    string path, Func<TParent, IEnumerable<TElement>> selector)
{
    if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentException("A collection path must not be empty or whitespace.", nameof(path));
    if (selector is null)
        throw new ArgumentNullException(nameof(selector));

    var key = (typeof(TParent), path);
    if (_collections.ContainsKey(key))
        throw new ArgumentException(
            $"A collection is already registered at path '{path}' for model '{typeof(TParent).Name}'.", nameof(path));

    _collections[key] = new CollectionBinding<TParent, TElement>(selector);
    return this;
}

/// <summary>Resolves the collection registered for <typeparamref name="TParent"/> at a path, or null.</summary>
internal CollectionBinding<TParent>? FindCollection<TParent>(string path) =>
    _collections.TryGetValue((typeof(TParent), path), out var binding)
        ? (CollectionBinding<TParent>)binding
        : null;
```

(Deviation from the spec: the optional `description` parameter is dropped as YAGNI — collections are not yet surfaced in a catalog.)

- [ ] **Step 5: Expose `BindElement` on `RuleBinder`**

In `src/Motiv.Serialization/RuleBinder.cs`, add an internal entry that binds a subtree in element-model space (a thin wrapper over the existing generic recursion):

```csharp
/// <summary>Binds a rule subtree against an element model type (used by higher-order collection binding).</summary>
internal static SpecBase<TElement, string>? BindElement<TElement>(
    RuleNode node, SpecRegistry registry, List<RuleError> errors) =>
    BindNode<TElement>(node, registry, errors);
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~SpecRegistryTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv.Serialization/CollectionBinding.cs src/Motiv.Serialization/SpecRegistry.cs \
        src/Motiv.Serialization/RuleBinder.cs src/Motiv.Serialization.Tests/SpecRegistryTests.cs
git commit -m "feat(rules-serialization): register collection selectors on SpecRegistry"
```

---

## Task 4: Bind higher-order nodes end-to-end (`UnknownCollection` + binder case)

**Files:**
- Modify: `src/Motiv.Serialization/RuleErrorCode.cs`
- Modify: `src/Motiv.Serialization/RuleBinder.cs` (replace the temporary guard)
- Test: `src/Motiv.Serialization.Tests/RuleSerializerDeserializeTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `RuleSerializerDeserializeTests`)

Add these members and tests to the class:

```csharp
private sealed record Order(decimal Total);
private sealed record Customer(IReadOnlyList<Order> Orders);

private static SpecBase<Order, string> IsLargeOrder { get; } =
    Spec.Build((Order o) => o.Total >= 100m).WhenTrue("large order").WhenFalse("small order").Create();

private static RuleSerializer CustomerSerializer() =>
    new(new SpecRegistry()
        .Register("is-large-order", IsLargeOrder)
        .RegisterCollection<Customer, Order>("orders", c => c.Orders));

private static Customer TwoLarge => new([new Order(150m), new Order(200m), new Order(50m)]);
private static Customer OneLarge => new([new Order(150m), new Order(50m)]);

[Fact]
public void Should_load_at_least_n_satisfied_like_the_fluent_equivalent()
{
    const string json =
        """{ "rule": { "asAtLeastNSatisfied": { "spec": "is-large-order" }, "n": 2, "path": "orders" } }""";
    var expected = Spec.Build(IsLargeOrder).AsAtLeastNSatisfied(2)
        .WhenTrue("at least 2 satisfied").WhenFalse("fewer than 2 satisfied").Create()
        .ChangeModelTo<Customer>(c => c.Orders);

    var loaded = CustomerSerializer().Deserialize<Customer>(json);

    ShouldBehaveIdentically(loaded, expected, TwoLarge, OneLarge);
}

[Fact]
public void Should_load_all_satisfied_like_the_fluent_equivalent()
{
    const string json = """{ "rule": { "asAllSatisfied": { "spec": "is-large-order" }, "path": "orders" } }""";
    var expected = Spec.Build(IsLargeOrder).AsAllSatisfied()
        .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
        .ChangeModelTo<Customer>(c => c.Orders);

    var loaded = CustomerSerializer().Deserialize<Customer>(json);

    ShouldBehaveIdentically(loaded, expected, TwoLarge, OneLarge);
}

[Fact]
public void Should_decorate_a_higher_order_node_like_every_other_node()
{
    const string json =
        """{ "rule": { "asAllSatisfied": { "spec": "is-large-order" }, "path": "orders", "whenTrue": "all big", "whenFalse": "not all big" } }""";
    var inner = Spec.Build(IsLargeOrder).AsAllSatisfied()
        .WhenTrue("all satisfied").WhenFalse("not all satisfied").Create()
        .ChangeModelTo<Customer>(c => c.Orders);
    var expected = Spec.Build(inner).WhenTrue("all big").WhenFalse("not all big").Create();

    var loaded = CustomerSerializer().Deserialize<Customer>(json);

    ShouldBehaveIdentically(loaded, expected, TwoLarge, OneLarge);
}

[Fact]
public void Should_reject_an_unregistered_collection_path()
{
    const string json = """{ "rule": { "asAllSatisfied": { "spec": "is-large-order" }, "path": "items" } }""";

    var ex = Should.Throw<RuleSerializationException>(() => CustomerSerializer().Deserialize<Customer>(json));

    ex.Errors.ShouldContain(e => e.Code == RuleErrorCode.UnknownCollection && e.Path == "$.rule");
}

[Fact]
public void Should_reject_a_child_spec_whose_model_type_is_wrong()
{
    // is-positive is an int spec; using it as the per-Order element spec must fail with ModelTypeMismatch.
    var serializer = new RuleSerializer(new SpecRegistry()
        .Register("is-positive", IsPositive)
        .RegisterCollection<Customer, Order>("orders", c => c.Orders));
    const string json = """{ "rule": { "asAllSatisfied": { "spec": "is-positive" }, "path": "orders" } }""";

    var ex = Should.Throw<RuleSerializationException>(() => serializer.Deserialize<Customer>(json));

    ex.Errors.ShouldContain(e => e.Code == RuleErrorCode.ModelTypeMismatch);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleSerializerDeserializeTests"`
Expected: FAIL — `UnknownCollection` does not exist (compile error), and once added, the higher-order loads hit the temporary guard ("not yet implemented").

- [ ] **Step 3: Add the `UnknownCollection` error code**

In `src/Motiv.Serialization/RuleErrorCode.cs`, add a member (after `DocumentTooLarge`):

```csharp
    /// <summary>A higher-order node references a collection path that is not registered.</summary>
    UnknownCollection
```

- [ ] **Step 4: Replace the temporary guard with real binding**

In `src/Motiv.Serialization/RuleBinder.cs`, change the higher-order switch arm to call the real binder and delete `BindHigherOrderNotYetImplemented`:

```csharp
    RuleOperator.AsAllSatisfied or RuleOperator.AsAnySatisfied or RuleOperator.AsNSatisfied
        or RuleOperator.AsAtLeastNSatisfied or RuleOperator.AsAtMostNSatisfied
        => BindHigherOrder<TModel>(node, registry, errors),
```

```csharp
private static SpecBase<TModel, string>? BindHigherOrder<TModel>(
    RuleNode node, SpecRegistry registry, List<RuleError> errors)
{
    var binding = registry.FindCollection<TModel>(node.CollectionPath!);
    if (binding is null)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.UnknownCollection,
            $"no collection is registered at path '{node.CollectionPath}' for model '{typeof(TModel).Name}'"));
        return null;
    }

    return binding.BindHigherOrder(node, registry, errors);
}
```

The existing `Decorate(node, spec)` tail of `BindNode` then applies document `name`/`whenTrue`/`whenFalse` uniformly — no change needed there.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Serialization.Tests --filter "FullyQualifiedName~RuleSerializerDeserializeTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv.Serialization/RuleErrorCode.cs src/Motiv.Serialization/RuleBinder.cs \
        src/Motiv.Serialization.Tests/RuleSerializerDeserializeTests.cs
git commit -m "feat(rules-serialization): bind higher-order nodes via registered collections"
```

---

## Task 5: Mirror `UnknownCollection` into the TS wire contract

**Files:**
- Modify: `ui/packages/rules-core/src/contracts.ts`

- [ ] **Step 1: Add the code to the union**

In `ui/packages/rules-core/src/contracts.ts`, extend `RuleErrorCode`:

```typescript
export type RuleErrorCode =
  | 'InvalidNode' | 'UnknownSpec' | 'ModelTypeMismatch' | 'MetadataTypeMismatch'
  | 'MixedWhenTrueFalseKinds' | 'ExpressionsNotEnabled' | 'AsyncSpecInSyncLoad'
  | 'DocumentTooLarge' | 'UnknownCollection';
```

- [ ] **Step 2: Typecheck the package**

Run: `pnpm -C ui/packages/rules-core build`
Expected: PASS (tsup build succeeds; the union is a pure type change).

- [ ] **Step 3: Commit**

```bash
git add ui/packages/rules-core/src/contracts.ts
git commit -m "feat(rules-core): add UnknownCollection to the RuleErrorCode wire contract"
```

---

## Task 6: Endpoint integration test (evaluate a higher-order document)

**Files:**
- Test: `src/Motiv.Serialization.AspNetCore.Tests/EvaluateEndpointTests.cs`

No production change: `MapMotivRules` already deserializes via the registry, and the host registers the collection on that registry before mapping.

- [ ] **Step 1: Write the failing test** (append to `EvaluateEndpointTests`)

```csharp
private sealed record Item(decimal Price);
private sealed record Cart(IReadOnlyList<Item> Items);

private static async Task<WebApplication> StartCartAppAsync()
{
    var isPricey = Spec.Build((Item i) => i.Price >= 100m)
        .WhenTrue("pricey").WhenFalse("cheap").Create();
    var registry = new SpecRegistry()
        .Register("is-pricey", isPricey)
        .RegisterCollection<Cart, Item>("items", c => c.Items);
    var options = new MotivRulesOptions().AddModel<Cart>("cart");
    return await TestApp.StartAsync(registry, options);
}

[Fact]
public async Task Should_evaluate_a_higher_order_document_over_a_collection()
{
    // Arrange
    await using var app = await StartCartAppAsync();
    var client = app.GetTestClient();
    var request = new
    {
        modelType = "cart",
        document = JsonDocument.Parse(
            """{ "rule": { "asAtLeastNSatisfied": { "spec": "is-pricey" }, "n": 2, "path": "items" } }""").RootElement,
        model = JsonDocument.Parse("""{ "items": [ { "price": 150 }, { "price": 200 }, { "price": 20 } ] }""").RootElement
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/rules/evaluate", request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
}

[Fact]
public async Task Should_report_an_unknown_collection_path_as_a_validation_error()
{
    // Arrange
    await using var app = await StartCartAppAsync();
    var client = app.GetTestClient();
    var request = new
    {
        modelType = "cart",
        document = JsonDocument.Parse(
            """{ "rule": { "asAllSatisfied": { "spec": "is-pricey" }, "path": "widgets" } }""").RootElement
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/rules/validate", request);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<JsonElement>();
    body.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("UnknownCollection");
}
```

- [ ] **Step 2: Run the tests to verify they fail, then pass**

Run: `dotnet test src/Motiv.Serialization.AspNetCore.Tests --filter "FullyQualifiedName~EvaluateEndpointTests"`
Expected: PASS immediately — the production code already supports this end-to-end after Tasks 1–4; these tests lock in the endpoint behavior. (If the `/validate` shape differs, confirm the `errors` array shape against the existing `ValidateEndpointTests`.)

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore.Tests/EvaluateEndpointTests.cs
git commit -m "test(rules-aspnetcore): evaluate a higher-order document over a collection"
```

---

## Task 7: Full-solution verification + code-simplifier review

**Files:** none (verification + review)

- [ ] **Step 1: Run the entire .NET solution test suite**

Run: `dotnet test`
Expected: PASS across all projects — including the example projects (`Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`) that assert on justification strings. Higher-order is additive, so none should change; if any fail, investigate before proceeding (do not edit expectations to mask a real regression).

- [ ] **Step 2: Build the TS package**

Run: `pnpm -C ui/packages/rules-core build`
Expected: PASS.

- [ ] **Step 3: Code-simplifier review (mandatory per CLAUDE.md)**

Dispatch a `code-simplifier` agent over the changed files (`RuleDocumentParser.cs`, `RuleBinder.cs`, `SpecRegistry.cs`, `CollectionBinding.cs`, `HigherOrder.cs`). Focus: duplication in the parser's higher-order handling, the operator→statement switch, and any convoluted control flow. Apply worthwhile suggestions, then re-run:

Run: `dotnet test src/Motiv.Serialization.Tests`
Expected: PASS.

- [ ] **Step 4: Commit any review changes**

```bash
git add -A
git commit -m "refactor(rules-serialization): simplify higher-order binding per review"
```

---

## Self-Review notes (already reconciled)

- **Spec coverage:** parser (Task 1) ✔, registration API (Task 3) ✔, binder + `ChangeModelTo` + decoration reuse (Tasks 3–4) ✔, `UnknownCollection` in C# and TS (Tasks 4–5) ✔, `n` literal-only with `@param` deferred (Task 1) ✔, tests incl. full suite (Tasks 1–7) ✔, reflection-free (registration captures generics; no `MakeGenericMethod` anywhere) ✔.
- **Naming collision:** the collection path uses `RuleNode.CollectionPath` because `RuleNode.Path` already holds the node location.
- **Type consistency:** `HigherOrder.Build<TElement>` → `SpecBase<IEnumerable<TElement>,string>`; `CollectionBinding<TParent,TElement>.BindHigherOrder` → `SpecBase<TParent,string>` via `ChangeModelTo<TParent>(Func<TParent,IEnumerable<TElement>>)`; `RuleBinder.BindElement<TElement>`/`BindHigherOrder<TModel>`/`FindCollection<TParent>` names are used identically at every call site.
- **Deviation:** `RegisterCollection` drops the spec's optional `description` parameter (YAGNI; collections aren't cataloged yet).
