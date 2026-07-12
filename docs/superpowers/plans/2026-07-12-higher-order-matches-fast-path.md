# Higher-Order `Matches` Fast-Path Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the built-in higher-order operations (All/Any/None/AtLeast/AtMost/Exactly) a short-circuiting, minimal-allocation boolean-only path on `Matches`, across all four higher-order families.

**Architecture:** Introduce one allocation-free primitive, `HigherOrderShortCircuit`, that describes a built-in operation as data (op + `n`) and evaluates it over a per-model boolean projection with array/list fast-paths and per-op early exit. Thread it as an optional descriptor from the built-in `As*` builder methods → the three operation structs → each higher-order proposition, where `Matches` uses it (falling back to today's exact path when the descriptor is `null`, i.e. for custom predicates). The full `Evaluate`/`EvaluatePolicy` path is never touched.

**Tech Stack:** C# (multi-target net472/net8.0/net9.0/net10.0), xUnit, AutoFixture + NSubstitute, BenchmarkDotNet.

## Global Constraints

- Library `Motiv` multi-targets **net472; net8.0; net9.0; net10.0** — no C# features requiring a runtime unavailable on net472 in shipped code paths (the fast path here uses only `switch`, generics, and indexed loops — all fine).
- New helper types are **`internal`** (consumed via `[InternalsVisibleTo]` from `Motiv.Tests`).
- The governing correctness invariant, tested for every built-in op and family: **`spec.Matches(models) == spec.Evaluate(models).Satisfied`**.
- Short-circuit is an **accepted behavioral change**: `Matches` may invoke the underlying source fewer times than before. `Evaluate` remains exhaustive.
- Custom higher-order predicates (`As(customPredicate)`) must retain **byte-for-byte** current behavior (descriptor is `null` → existing `EvaluateModels` path).
- Follow existing caching/style conventions; do not over-DRY across families (explicit per-family projections are expected).
- Tests use **xUnit + Shouldly** (`value.ShouldBe(expected)`, `.ShouldBeTrue()`, `.ShouldBeFalse()`) — global usings are `Xunit` and `Shouldly`; do **not** introduce FluentAssertions. Prefer `[Theory]`/`[InlineData]` (serializable args only) for the boundary matrices.
- The test project is `src/Motiv.Tests/Motiv.Tests.csproj`. Higher-order-over-results is built by `Spec.Build(underlying)` where the **static type** of `underlying` selects the family: `SpecBase<T,string>` → BooleanResultPredicate family, `PolicyBase<T,string>` → PolicyResultPredicate family (see `src/Motiv.Tests/AsAllSatisfiedSpecTests.cs`).
- Run the **full solution** test suite (including `src/examples/Motiv.*.Tests`) before considering a phase complete.

**Per-family boolean projection** (used throughout — memorize these three):

| Family | Underlying ctor member | Projection expression | Static projection lambda |
|---|---|---|---|
| BooleanPredicate | `Func<TModel,bool> predicate` | `predicate(model)` | `static (m, p) => p(m)` with state `predicate` |
| BooleanResultPredicate | `Func<TModel, BooleanResultBase<T>> resultResolver` | `resultResolver(model).Satisfied` | `static (m, r) => r(m).Satisfied` with state `resultResolver` |
| PolicyResultPredicate | `Func<TModel, PolicyResultBase<T>> resultResolver` | `resultResolver(model).Satisfied` | `static (m, r) => r(m).Satisfied` with state `resultResolver` |
| ExpressionTree | field `ExpressionPredicate<TModel,TPredicateResult> _predicate` | `_predicate.Execute(model).Satisfied` | `static (m, p) => p.Execute(m).Satisfied` with state `_predicate` |

---

## Task 1: The `HigherOrderShortCircuit` primitive

**Files:**
- Create: `src/Motiv/HigherOrderProposition/HigherOrderShortCircuit.cs`
- Test: `src/Motiv.Tests/HigherOrderShortCircuitTests.cs`

**Interfaces:**
- Produces:
  - `internal enum HigherOrderOp { All, Any, None, AtLeast, AtMost, Exactly }`
  - `internal readonly struct HigherOrderShortCircuit` with:
    - static props `All`, `Any`, `None` (type `HigherOrderShortCircuit`)
    - static methods `AtLeast(int n)`, `AtMost(int n)`, `Exactly(int n)` → `HigherOrderShortCircuit`
    - `bool Evaluate<TModel, TState>(IEnumerable<TModel> source, TState state, Func<TModel, TState, bool> project)`

- [ ] **Step 1: Write the failing test**

Create `src/Motiv.Tests/HigherOrderShortCircuitTests.cs`:

```csharp
using Motiv.HigherOrderProposition;

namespace Motiv.Tests;

public class HigherOrderShortCircuitTests
{
    private sealed class Counter { public int Count; }

    // Counting projection: state is the Counter so the lambda passed to Evaluate stays static.
    private static bool Positive(int n, Counter c)
    {
        c.Count++;
        return n > 0;
    }

    private static HigherOrderShortCircuit Descriptor(string op, int n) =>
        op switch
        {
            "All" => HigherOrderShortCircuit.All,
            "Any" => HigherOrderShortCircuit.Any,
            "None" => HigherOrderShortCircuit.None,
            "AtLeast" => HigherOrderShortCircuit.AtLeast(n),
            "AtMost" => HigherOrderShortCircuit.AtMost(n),
            "Exactly" => HigherOrderShortCircuit.Exactly(n),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [InlineData("All", 0, new[] { 1, 2, 3 }, true)]
    [InlineData("All", 0, new[] { 1, -2, 3 }, false)]
    [InlineData("All", 0, new int[0], true)]
    [InlineData("Any", 0, new[] { -1, -2, 3 }, true)]
    [InlineData("Any", 0, new[] { -1, -2, -3 }, false)]
    [InlineData("Any", 0, new int[0], false)]
    [InlineData("None", 0, new[] { -1, -2 }, true)]
    [InlineData("None", 0, new[] { -1, 2 }, false)]
    [InlineData("None", 0, new int[0], true)]
    [InlineData("AtLeast", 2, new[] { 1, 2, -3 }, true)]
    [InlineData("AtLeast", 2, new[] { 1, -2, -3 }, false)]
    [InlineData("AtLeast", 0, new int[0], true)]
    [InlineData("AtMost", 1, new[] { 1, -2, -3 }, true)]
    [InlineData("AtMost", 1, new[] { 1, 2, -3 }, false)]
    [InlineData("AtMost", 0, new int[0], true)]
    [InlineData("Exactly", 2, new[] { 1, 2, -3 }, true)]
    [InlineData("Exactly", 2, new[] { 1, 2, 3 }, false)]
    [InlineData("Exactly", 0, new int[0], true)]
    public void Evaluate_over_array_matches_reference(string op, int n, int[] data, bool expected)
    {
        Descriptor(op, n).Evaluate(data, new Counter(), Positive).ShouldBe(expected);
    }

    [Theory]
    [InlineData("All", new[] { 1, -2, 3 }, false)]   // IReadOnlyList<T> fast path
    [InlineData("Any", new[] { -1, 2, -3 }, true)]
    public void Evaluate_over_list_matches_reference(string op, int[] data, bool expected)
    {
        Descriptor(op, 0).Evaluate(new List<int>(data), new Counter(), Positive).ShouldBe(expected);
    }

    [Theory]
    [InlineData("All", new[] { 1, -2, 3 }, false)]   // non-array, non-IReadOnlyList fallback
    [InlineData("Any", new[] { -1, 2, -3 }, true)]
    public void Evaluate_over_lazy_enumerable_matches_reference(string op, int[] data, bool expected)
    {
        IEnumerable<int> lazy = data.Where(static _ => true);
        Descriptor(op, 0).Evaluate(lazy, new Counter(), Positive).ShouldBe(expected);
    }

    [Fact]
    public void All_short_circuits_on_first_false()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.All.Evaluate(new[] { -1, 2, 3, 4 }, counter, Positive).ShouldBeFalse();
        counter.Count.ShouldBe(1); // stopped at the first element
    }

    [Fact]
    public void Any_short_circuits_on_first_true()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.Any.Evaluate(new[] { 1, -2, -3, -4 }, counter, Positive).ShouldBeTrue();
        counter.Count.ShouldBe(1);
    }

    [Fact]
    public void AtLeast_short_circuits_once_threshold_reached()
    {
        var counter = new Counter();
        HigherOrderShortCircuit.AtLeast(2).Evaluate(new[] { 1, 2, 3, 4, 5 }, counter, Positive).ShouldBeTrue();
        counter.Count.ShouldBe(2);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderShortCircuitTests"`
Expected: **FAIL — compile error** (`HigherOrderShortCircuit` / `HigherOrderOp` do not exist).

- [ ] **Step 3: Write the primitive**

Create `src/Motiv/HigherOrderProposition/HigherOrderShortCircuit.cs`:

```csharp
namespace Motiv.HigherOrderProposition;

internal enum HigherOrderOp
{
    All,
    Any,
    None,
    AtLeast,
    AtMost,
    Exactly
}

/// <summary>
/// Describes a built-in higher-order operation as data and evaluates it directly against a per-model boolean
/// projection with array/list fast-paths and per-operation short-circuit. This is the allocation-free,
/// boolean-only counterpart to the result-materializing higher-order predicate used by the full evaluation path.
/// </summary>
internal readonly struct HigherOrderShortCircuit(HigherOrderOp op, int n)
{
    internal static HigherOrderShortCircuit All { get; } = new(HigherOrderOp.All, 0);
    internal static HigherOrderShortCircuit Any { get; } = new(HigherOrderOp.Any, 0);
    internal static HigherOrderShortCircuit None { get; } = new(HigherOrderOp.None, 0);
    internal static HigherOrderShortCircuit AtLeast(int n) => new(HigherOrderOp.AtLeast, n);
    internal static HigherOrderShortCircuit AtMost(int n) => new(HigherOrderOp.AtMost, n);
    internal static HigherOrderShortCircuit Exactly(int n) => new(HigherOrderOp.Exactly, n);

    /// <summary>
    /// Evaluates the operation over <paramref name="source" />, deriving each model's boolean via
    /// <paramref name="project" /> (which receives the per-call <paramref name="state" /> so call sites can pass a
    /// non-capturing <c>static</c> lambda). Arrays and <see cref="IReadOnlyList{T}" /> sources iterate by index to
    /// avoid enumerator allocation; the operation's decision logic is shared across all iteration shapes.
    /// </summary>
    internal bool Evaluate<TModel, TState>(
        IEnumerable<TModel> source,
        TState state,
        Func<TModel, TState, bool> project)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        var trueCount = 0;

        switch (source)
        {
            case TModel[] array:
                for (var i = 0; i < array.Length; i++)
                    if (TryDecide(project(array[i], state), ref trueCount, out var decidedArray))
                        return decidedArray;
                break;

            case IReadOnlyList<TModel> list:
                var count = list.Count;
                for (var i = 0; i < count; i++)
                    if (TryDecide(project(list[i], state), ref trueCount, out var decidedList))
                        return decidedList;
                break;

            default:
                foreach (var item in source)
                    if (TryDecide(project(item, state), ref trueCount, out var decidedSeq))
                        return decidedSeq;
                break;
        }

        return FinalDecision(trueCount);
    }

    // Applies the operation's early-exit rule to one projected boolean. Returns true (with `decided` set) when the
    // overall outcome is already determined; otherwise returns false to continue iterating.
    private bool TryDecide(bool satisfied, ref int trueCount, out bool decided)
    {
        if (satisfied)
            trueCount++;

        switch (op)
        {
            case HigherOrderOp.All:
                decided = false;
                return !satisfied;         // first false → false
            case HigherOrderOp.Any:
                decided = true;
                return satisfied;          // first true → true
            case HigherOrderOp.None:
                decided = false;
                return satisfied;          // first true → false
            case HigherOrderOp.AtLeast:
                decided = true;
                return trueCount >= n;      // reached n → true
            case HigherOrderOp.AtMost:
                decided = false;
                return trueCount > n;       // exceeded n → false
            case HigherOrderOp.Exactly:
                decided = false;
                return trueCount > n;       // exceeded n → false
            default:
                decided = false;
                return false;
        }
    }

    // Outcome when iteration completed without an early decision.
    private bool FinalDecision(int trueCount) =>
        op switch
        {
            HigherOrderOp.All => true,             // no false was seen
            HigherOrderOp.Any => false,            // no true was seen
            HigherOrderOp.None => true,            // no true was seen
            HigherOrderOp.AtLeast => trueCount >= n,
            HigherOrderOp.AtMost => true,          // never exceeded n
            HigherOrderOp.Exactly => trueCount == n,
            _ => false
        };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderShortCircuitTests"`
Expected: **PASS** (all theory cases + the three short-circuit facts).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv/HigherOrderProposition/HigherOrderShortCircuit.cs src/Motiv.Tests/HigherOrderShortCircuitTests.cs
git commit -m "feat: add HigherOrderShortCircuit boolean-only primitive"
```

---

## Task 2: Carry the descriptor on the BooleanPredicate operation

**Files:**
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderSpecBooleanPredicateOperation.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderBooleanPredicateSpecMethods.cs`

**Interfaces:**
- Consumes: `HigherOrderShortCircuit` (Task 1).
- Produces: `HigherOrderSpecBooleanPredicateOperation<TModel>.ShortCircuit` → `HigherOrderShortCircuit?` (null for custom predicates, set for the six built-ins).

- [ ] **Step 1: Add the optional property to the operation struct**

Replace the body of `HigherOrderSpecBooleanPredicateOperation.cs` with (adds a second constructor + `ShortCircuit`; existing two-arg constructor now delegates with `null`):

```csharp
namespace Motiv.HigherOrderProposition.PropositionBuilders;

/// <summary>
/// A builder for creating higher-order propositions using a predicate function that evaluates a collection of model results.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
public readonly struct HigherOrderSpecBooleanPredicateOperation<TModel>
{
    internal HigherOrderSpecBooleanPredicateOperation(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector)
        : this(higherOrderPredicate, causeSelector, null)
    {
    }

    internal HigherOrderSpecBooleanPredicateOperation(
        Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
        Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector,
        HigherOrderShortCircuit? shortCircuit)
    {
        HigherOrderPredicate = higherOrderPredicate;
        CauseSelector = causeSelector;
        ShortCircuit = shortCircuit;
    }

    internal Func<IEnumerable<ModelResult<TModel>>, bool> HigherOrderPredicate { get; }

    internal Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> CauseSelector { get; }

    internal HigherOrderShortCircuit? ShortCircuit { get; }
}
```

> Note: the constructor was previously the primary-constructor form. Confirm no external caller uses the public constructor directly (it is `public readonly struct` but constructed only via the internal `As*` methods). If the `public` modifier on the struct requires a public constructor for source compatibility, keep the constructors `internal` as shown — the struct is only instantiated inside the assembly.

- [ ] **Step 2: Set the descriptor in the six built-in methods**

In `HigherOrderBooleanPredicateSpecMethods.cs`, add the third constructor argument to each built-in. Apply these exact edits:

`AsAllSatisfied` → third arg `HigherOrderShortCircuit.All`:
```csharp
return new HigherOrderSpecBooleanPredicateOperation<TModel>(
    modelResult => modelResult.AllTrue(),
    (isSatisfied, results) => isSatisfied ? results : results.WhereFalse(),
    HigherOrderShortCircuit.All);
```

`AsAnySatisfied` → `HigherOrderShortCircuit.Any`:
```csharp
return new HigherOrderSpecBooleanPredicateOperation<TModel>(
    modelResults => modelResults.AnyTrue(),
    (isSatisfied, results) => isSatisfied ? results.WhereTrue() : results,
    HigherOrderShortCircuit.Any);
```

`AsNoneSatisfied` → `HigherOrderShortCircuit.None`:
```csharp
return new HigherOrderSpecBooleanPredicateOperation<TModel>(
    modelResults => modelResults.AllFalse(),
    (isSatisfied, results) => isSatisfied ? results : results.WhereTrue(),
    HigherOrderShortCircuit.None);
```

`AsAtLeastNSatisfied` → `HigherOrderShortCircuit.AtLeast(n)`:
```csharp
return new HigherOrderSpecBooleanPredicateOperation<TModel>(
    HigherOrderPredicate,
    (_, results) => Causes.SatisfiedElseAll(results),
    HigherOrderShortCircuit.AtLeast(n));

bool HigherOrderPredicate(IEnumerable<ModelResult<TModel>> modelResults) =>
    modelResults.CountTrue() >= n;
```

`AsAtMostNSatisfied` → `HigherOrderShortCircuit.AtMost(n)`:
```csharp
return new HigherOrderSpecBooleanPredicateOperation<TModel>(
    HigherOrderPredicate,
    (_, results) => Causes.SatisfiedElseAll(results),
    HigherOrderShortCircuit.AtMost(n));

bool HigherOrderPredicate(IEnumerable<ModelResult<TModel>> modelResults) =>
    modelResults.CountTrue() <= n;
```

`AsNSatisfied` → `HigherOrderShortCircuit.Exactly(n)`:
```csharp
return new HigherOrderSpecBooleanPredicateOperation<TModel>(
    HigherOrderPredicate,
    (_, results) => Causes.SatisfiedElseAll(results),
    HigherOrderShortCircuit.Exactly(n));

bool HigherOrderPredicate(IEnumerable<ModelResult<TModel>> modelResults) =>
    modelResults.CountTrue() == n;
```

Leave both generic `As(...)` overloads **unchanged** (they use the two-arg constructor → `ShortCircuit` stays `null`).

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/Motiv/Motiv.csproj --framework net8.0`
Expected: **Build succeeded** (no call sites consume `ShortCircuit` yet, so behavior is unchanged).

- [ ] **Step 4: Commit**

```bash
git add src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderSpecBooleanPredicateOperation.cs src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderBooleanPredicateSpecMethods.cs
git commit -m "feat: carry short-circuit descriptor on BooleanPredicate higher-order operation"
```

---

## Task 3: BooleanPredicate propositions consume the descriptor in `Matches`

**Files (all under `src/Motiv/HigherOrderProposition/BooleanPredicate/`):**
- Modify: `HigherOrderFromBooleanPredicateProposition.cs`
- Modify: `HigherOrderFromBooleanPredicateExplanationProposition.cs`
- Modify: `HigherOrderFromBooleanPredicateMultiMetadataProposition.cs`
- Modify: `HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition.cs`
- Test: `src/Motiv.Tests/HigherOrderMatchesShortCircuitTests.cs`

**Interfaces:**
- Consumes: `HigherOrderShortCircuit` (Task 1); each proposition's existing `Func<TModel,bool> predicate` field.
- Produces: each BooleanPredicate proposition gains a trailing constructor parameter `HigherOrderShortCircuit? shortCircuit = null`.

- [ ] **Step 1: Write the failing short-circuit test**

Create `src/Motiv.Tests/HigherOrderMatchesShortCircuitTests.cs`:

```csharp
namespace Motiv.Tests;

public class HigherOrderMatchesShortCircuitTests
{
    [Fact]
    public void AllSatisfied_Matches_short_circuits_on_first_false()
    {
        var calls = 0;
        var spec = Spec
            .Build((int n) => { calls++; return n > 0; })
            .AsAllSatisfied()
            .Create("all positive");

        spec.Matches([-1, 2, 3, 4, 5]).ShouldBeFalse();
        calls.ShouldBe(1); // stopped at the first (false) model
    }

    [Fact]
    public void AnySatisfied_Matches_short_circuits_on_first_true()
    {
        var calls = 0;
        var spec = Spec
            .Build((int n) => { calls++; return n > 0; })
            .AsAnySatisfied()
            .Create("any positive");

        spec.Matches([1, -2, -3, -4]).ShouldBeTrue();
        calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, true)]
    [InlineData(new[] { 1, -2, 3 }, false)]
    [InlineData(new int[0], true)]
    public void AllSatisfied_Matches_equals_Evaluate_Satisfied(int[] models, bool expected)
    {
        var spec = Spec.Build((int n) => n > 0).AsAllSatisfied().Create("all positive");

        spec.Matches(models).ShouldBe(expected);
        spec.Matches(models).ShouldBe(spec.Evaluate(models).Satisfied);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderMatchesShortCircuitTests"`
Expected: **FAIL** — `AllSatisfied_Matches_short_circuits_on_first_false` asserts `calls == 1` but current code materializes all models, so `calls == 5`. (The equivalence theory already passes — it is a regression guard.)

- [ ] **Step 3: Update `HigherOrderFromBooleanPredicateProposition.cs`**

Add the trailing constructor parameter and rewrite `Matches`. The class uses a primary constructor; add `HigherOrderShortCircuit? shortCircuit = null` as the **last** parameter and change `Matches`:

```csharp
internal sealed class HigherOrderFromBooleanPredicateProposition<TModel, TMetadata>(
    Func<TModel,bool> predicate,
    Func<IEnumerable<ModelResult<TModel>>, bool> higherOrderPredicate,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenTrue,
    Func<HigherOrderBooleanEvaluation<TModel>, TMetadata> whenFalse,
    ISpecDescription specDescription,
    Func<bool, IEnumerable<ModelResult<TModel>>, IEnumerable<ModelResult<TModel>>> causeSelector,
    HigherOrderShortCircuit? shortCircuit = null)
    : PolicyBase<IEnumerable<TModel>, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(IEnumerable<TModel> models) =>
        shortCircuit is { } sc
            ? sc.Evaluate(models, predicate, static (m, p) => p(m))
            : EvaluateModels(models).IsSatisfied;

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(IEnumerable<TModel> models)
    {
        var (underlyingResults, isSatisfied) = EvaluateModels(models);

        return new HigherOrderFromBooleanPredicateMetadataPolicyResult<TModel, TMetadata>(
            isSatisfied,
            underlyingResults,
            whenTrue,
            whenFalse,
            specDescription,
            causeSelector);
    }

    private (ModelResult<TModel>[] Results, bool IsSatisfied) EvaluateModels(IEnumerable<TModel> models)
    {
        var results = HigherOrderResults.Materialize(models, predicate,
            static (model, p) => new ModelResult<TModel>(model, p(model)));
        return (results, higherOrderPredicate(results));
    }
}
```

> The `namespace Motiv.HigherOrderProposition.BooleanPredicate;` file already imports the containing namespace's siblings; `HigherOrderShortCircuit` lives in the parent `Motiv.HigherOrderProposition` namespace. Add `using Motiv.HigherOrderProposition;` only if the file's namespace does not already make it visible (it is a parent namespace, so no using is required).

- [ ] **Step 4: Apply the identical two-part edit to the other three BooleanPredicate propositions**

For each file below: (a) add `HigherOrderShortCircuit? shortCircuit = null` as the **last** primary-constructor parameter, and (b) replace the `Matches` body with the short-circuit form. The projection is identical in all four (`static (m, p) => p(m)` over the `predicate` field).

`HigherOrderFromBooleanPredicateExplanationProposition.cs` — new `Matches`:
```csharp
public override bool Matches(IEnumerable<TModel> models) =>
    shortCircuit is { } sc
        ? sc.Evaluate(models, predicate, static (m, p) => p(m))
        : EvaluateModels(models).IsSatisfied;
```
(constructor: append `, HigherOrderShortCircuit? shortCircuit = null` after the existing `causeSelector` parameter.)

`HigherOrderFromBooleanPredicateMultiMetadataProposition.cs` — same `Matches` body and same trailing parameter. (Note: this type is `SpecBase<...>` with `EvaluateSpec`, but `Matches` and the `predicate`/`shortCircuit` handling are identical.)

`HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition.cs` — same `Matches` body and same trailing parameter.

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build src/Motiv/Motiv.csproj --framework net8.0`
Expected: **Build succeeded.** (Factories still call the old argument list; the new parameter is optional, so they bind with `shortCircuit = null` — feature not yet active, but compiles.)

- [ ] **Step 6: Wire the factories to forward the descriptor**

In each of the seven BooleanPredicate factory files, append `higherOrderOperation.ShortCircuit` (or `_higherOrderOperation.ShortCircuit` where the operation is stored in a field — see `MultiAssertionExplanationHigherOrderPropositionFactory.cs`) as the **final argument** to every `new HigherOrderFromBooleanPredicate*Proposition(...)` call:

- `PropositionBuilders/BooleanPredicate/MinimalHigherOrderFromBooleanPredicatePropositionFactory.cs`
- `PropositionBuilders/BooleanPredicate/MetadataFromBooleanHigherOrderPropositionFactory.cs`
- `PropositionBuilders/BooleanPredicate/ExplanationFromBooleanPredicateWithNameHigherOrderPropositionFactory.cs`
- `PropositionBuilders/BooleanPredicate/MultiAssertionExplanationHigherOrderPropositionFactory.cs` (uses `_higherOrderOperation`)
- `PropositionBuilders/BooleanPredicate/MultiAssertionExplanationWithNameHigherOrderPropositionFactory.cs`
- `PropositionBuilders/BooleanPredicate/MultiMetadataFromBooleanHigherOrderPropositionFactory.cs`
- `PropositionBuilders/BooleanPredicate/MultiMetadataFromBooleanHigherOrderWithSingularWhenTruePropositionFactory.cs`

Example (`MinimalHigherOrderFromBooleanPredicatePropositionFactory.cs`), from:
```csharp
return new HigherOrderFromBooleanPredicateProposition<TModel, string>(
    predicate,
    higherOrderOperation.HigherOrderPredicate,
    _ => statement.AsSatisfied(),
    _ => statement.AsUnsatisfied(),
    new SpecDescription(statement),
    higherOrderOperation.CauseSelector);
```
to:
```csharp
return new HigherOrderFromBooleanPredicateProposition<TModel, string>(
    predicate,
    higherOrderOperation.HigherOrderPredicate,
    _ => statement.AsSatisfied(),
    _ => statement.AsUnsatisfied(),
    new SpecDescription(statement),
    higherOrderOperation.CauseSelector,
    higherOrderOperation.ShortCircuit);
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderMatchesShortCircuitTests"`
Expected: **PASS** — `calls == 1` in both short-circuit facts; equivalence theory green.

- [ ] **Step 8: Run the full Motiv.Tests suite for regressions**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0`
Expected: **PASS** — no regressions (higher-order full-evaluation behavior unchanged).

- [ ] **Step 9: Commit**

```bash
git add src/Motiv/HigherOrderProposition/BooleanPredicate/ src/Motiv/HigherOrderProposition/PropositionBuilders/BooleanPredicate/ src/Motiv.Tests/HigherOrderMatchesShortCircuitTests.cs
git commit -m "feat: short-circuit boolean-only Matches for BooleanPredicate higher-order ops"
```

---

## Task 4: Verify the BooleanPredicate allocation win (benchmark gate)

**Files:**
- Reference: `src/examples/Motiv.Benchmark/EvaluationBenchmarks.cs` (`HigherOrder_Matches` already exists)

- [ ] **Step 1: Run the higher-order benchmarks**

Run: `dotnet run -c Release --project src/examples/Motiv.Benchmark/Motiv.Benchmark.csproj --framework net8.0 -- --filter "*EvaluationBenchmarks.HigherOrder*"`
Expected: `HigherOrder_Matches` **Allocated == 0 B** (was 104 B) and Mean well below `HigherOrder_Satisfied` (short-circuit exits on the 2nd model of `[1, -2, …]`).

- [ ] **Step 2: Record the result in the plan checklist (no code change)**

If `HigherOrder_Matches` is not 0 B, STOP and investigate: the descriptor is not reaching the proposition (a factory was missed) or a projection captured state. Do not proceed to Task 5 until this gate passes.

---

## Task 5: Equivalence coverage for all six BooleanPredicate operations

**Files:**
- Test: `src/Motiv.Tests/HigherOrderMatchesEquivalenceTests.cs`

**Interfaces:**
- Consumes: the public `Spec.Build(...).AsXxx().Create(...)` builder surface.

- [ ] **Step 1: Write the equivalence matrix test**

Create `src/Motiv.Tests/HigherOrderMatchesEquivalenceTests.cs`:

```csharp
namespace Motiv.Tests;

public class HigherOrderMatchesEquivalenceTests
{
    private static readonly int[][] Datasets =
    [
        [], [1], [-1], [1, 2, 3], [-1, -2, -3], [1, -2, 3], [1, 2, -3, 4, -5]
    ];

    // Built inside the test (not passed via MemberData) so theory args stay serializable.
    private static SpecBase<IEnumerable<int>, string> Build(string op) =>
        op switch
        {
            "All" => Spec.Build((int n) => n > 0).AsAllSatisfied().Create("all"),
            "Any" => Spec.Build((int n) => n > 0).AsAnySatisfied().Create("any"),
            "None" => Spec.Build((int n) => n > 0).AsNoneSatisfied().Create("none"),
            "AtLeast0" => Spec.Build((int n) => n > 0).AsAtLeastNSatisfied(0).Create("al0"),
            "AtLeast2" => Spec.Build((int n) => n > 0).AsAtLeastNSatisfied(2).Create("al2"),
            "AtMost0" => Spec.Build((int n) => n > 0).AsAtMostNSatisfied(0).Create("am0"),
            "AtMost2" => Spec.Build((int n) => n > 0).AsAtMostNSatisfied(2).Create("am2"),
            "Exactly0" => Spec.Build((int n) => n > 0).AsNSatisfied(0).Create("n0"),
            "Exactly2" => Spec.Build((int n) => n > 0).AsNSatisfied(2).Create("n2"),
            _ => throw new ArgumentOutOfRangeException(nameof(op))
        };

    [Theory]
    [InlineData("All")]
    [InlineData("Any")]
    [InlineData("None")]
    [InlineData("AtLeast0")]
    [InlineData("AtLeast2")]
    [InlineData("AtMost0")]
    [InlineData("AtMost2")]
    [InlineData("Exactly0")]
    [InlineData("Exactly2")]
    public void Matches_equals_Evaluate_Satisfied_across_datasets(string op)
    {
        var spec = Build(op);

        foreach (var data in Datasets)
            spec.Matches(data).ShouldBe(spec.Evaluate(data).Satisfied, $"{op} on [{string.Join(",", data)}]");
    }
}
```

- [ ] **Step 2: Run to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderMatchesEquivalenceTests"`
Expected: **PASS** for all nine specs × seven datasets.

- [ ] **Step 3: Commit**

```bash
git add src/Motiv.Tests/HigherOrderMatchesEquivalenceTests.cs
git commit -m "test: equivalence matrix for BooleanPredicate higher-order Matches"
```

---

## Task 6: Phase 1 hardening — multi-framework tests + simplifier review

- [ ] **Step 1: Run the higher-order tests on every target framework**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~HigherOrder"`
Expected: **PASS** on net472, net8.0, net9.0, net10.0 (verifies the `switch`/generic projection compiles and behaves on net472).

- [ ] **Step 2: Run the full solution test suite**

Run: `dotnet test Motiv.slnx`
Expected: **PASS** — including `Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests` (justification strings unaffected; this confirms it).

- [ ] **Step 3: Spawn the mandatory `code-simplifier` review**

Dispatch a `code-simplifier` agent over the Phase 1 diff (`HigherOrderShortCircuit.cs`, the BooleanPredicate propositions, the operation struct, the built-in methods, the factories). Apply any accepted simplifications and re-run `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrder"`.

- [ ] **Step 4: Commit any simplifications**

```bash
git add -A
git commit -m "refactor: simplifier pass on BooleanPredicate higher-order fast path"
```

---

## Task 7: BooleanResultPredicate + PolicyResultPredicate families

These two families share one shape: an operation struct forwarding a result-based predicate, a `resultResolver` field on each proposition, and the projection `static (m, r) => r(m).Satisfied`.

**Files — operation structs & built-in methods:**
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderSpecPredicateOperation.cs` (BooleanResult family)
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderPolicyPredicateOperation.cs` (Policy family)
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderPredicateSpecMethods.cs` (6 built-ins)
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/HigherOrderPredicatePolicyMethods.cs` (6 built-ins)

**Files — propositions (add trailing `HigherOrderShortCircuit? shortCircuit = null` + short-circuit `Matches`):**
- `BooleanResultPredicate/MinimalHigherOrderFromBooleanResultProposition.cs`
- `BooleanResultPredicate/HigherOrderFromBooleanResultProposition.cs`
- `BooleanResultPredicate/HigherOrderFromBooleanResultExplanationProposition.cs`
- `BooleanResultPredicate/HigherOrderFromBooleanResultMultiMetadataProposition.cs`
- `BooleanResultPredicate/HigherOrderFromBooleanResultMultiAssertionExplanationProposition.cs`
- `PolicyResultPredicate/MinimalHigherOrderFromPolicyResultProposition.cs`
- `PolicyResultPredicate/HigherOrderFromPolicyResultMetadataProposition.cs`
- `PolicyResultPredicate/HigherOrderFromPolicyResultExplanationProposition.cs`
- `PolicyResultPredicate/HigherOrderFromPolicyResultMultiMetadataProposition.cs`
- `PolicyResultPredicate/HigherOrderFromPolicyResultMultiAssertionExplanationProposition.cs`

**Interfaces:**
- Consumes: `HigherOrderShortCircuit`; each proposition's `Func<TModel, BooleanResultBase<T>> resultResolver` / `Func<TModel, PolicyResultBase<T>> resultResolver`.
- Produces: `.ShortCircuit` on `HigherOrderSpecPredicateOperation<TModel,TUnderlyingMetadata>` and `HigherOrderPolicyPredicateOperation<TModel,TUnderlyingMetadata>`.

- [ ] **Step 1: Write the failing tests (short-circuit + equivalence, both families)**

Append to `src/Motiv.Tests/HigherOrderMatchesShortCircuitTests.cs`:

```csharp
[Fact]
public void SpecPredicate_AllSatisfied_Matches_short_circuits_on_first_false()
{
    var calls = 0;
    // Static type SpecBase<...> selects the BooleanResultPredicate family.
    SpecBase<int, string> underlying = Spec.Build((int n) => { calls++; return n > 0; }).Create("positive");
    var spec = Spec.Build(underlying).AsAllSatisfied().Create("all positive");

    spec.Matches([-1, 2, 3, 4]).ShouldBeFalse();
    calls.ShouldBe(1);
}

[Fact]
public void PolicyPredicate_AnySatisfied_Matches_short_circuits_on_first_true()
{
    var calls = 0;
    // Static type PolicyBase<...> selects the PolicyResultPredicate family.
    PolicyBase<int, string> underlying = Spec.Build((int n) => { calls++; return n > 0; }).Create("positive");
    var spec = Spec.Build(underlying).AsAnySatisfied().Create("any positive");

    spec.Matches([1, -2, -3]).ShouldBeTrue();
    calls.ShouldBe(1);
}
```

> The static type of `underlying` selects the family (per `AsAllSatisfiedSpecTests.cs`): `SpecBase<int,string>` → BooleanResultPredicate, `PolicyBase<int,string>` → PolicyResultPredicate. `Spec.Build((int n) => …).Create("positive")` returns a minimal policy (assignable to both). Each underlying evaluation invokes the counting predicate exactly once, so `calls` counts underlying evaluations. Confirm the test compiles and **fails** on `calls` before implementing.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderMatchesShortCircuitTests"`
Expected: **FAIL** — `calls` equals the collection length, not 1.

- [ ] **Step 3: Add `ShortCircuit` to both operation structs**

Apply the same two-constructor pattern from Task 2 Step 1 to `HigherOrderSpecPredicateOperation.cs` and `HigherOrderPolicyPredicateOperation.cs` (their result element types are `BooleanResult<TModel,TUnderlyingMetadata>` and `PolicyResult<TModel,TUnderlyingMetadata>` respectively — keep those; only add the `HigherOrderShortCircuit? ShortCircuit` property and the third constructor arg).

- [ ] **Step 4: Set descriptors in the 12 built-in methods**

In `HigherOrderPredicateSpecMethods.cs` and `HigherOrderPredicatePolicyMethods.cs`, add the third constructor argument to each of the six built-ins exactly as in Task 2 Step 2 (`HigherOrderShortCircuit.All`/`.Any`/`.None`/`.AtLeast(n)`/`.AtMost(n)`/`.Exactly(n)`). Leave both `As(...)` custom overloads untouched.

- [ ] **Step 5: Update the 10 propositions**

For each proposition file listed above: append `HigherOrderShortCircuit? shortCircuit = null` as the last primary-constructor parameter and set `Matches` to:
```csharp
public override bool Matches(IEnumerable<TModel> models) =>
    shortCircuit is { } sc
        ? sc.Evaluate(models, resultResolver, static (m, r) => r(m).Satisfied)
        : EvaluateModels(models).IsSatisfied;
```

- [ ] **Step 6: Forward `.ShortCircuit` in the result-family factories**

Append `<operationVar>.ShortCircuit` as the final argument to every `new HigherOrderFromBooleanResult*` / `new MinimalHigherOrderFromBooleanResult*` / `new HigherOrderFromPolicyResult*` / `new MinimalHigherOrderFromPolicyResult*` construction. First locate them:

Run: `grep -rln "new HigherOrderFromBooleanResult\|new MinimalHigherOrderFromBooleanResult\|new HigherOrderFromPolicyResult\|new MinimalHigherOrderFromPolicyResult" src/Motiv/HigherOrderProposition/PropositionBuilders`

For each hit, confirm the operation variable in scope (`higherOrderOperation` or a `_higherOrderOperation` field) and append its `.ShortCircuit`. Candidate directories: `PropositionBuilders/BooleanResultPredicate/`, `PropositionBuilders/Explanation/Spec/`, `PropositionBuilders/Explanation/Policy/`, `PropositionBuilders/Explanation/PolicyResultPredicate/`, `PropositionBuilders/Explanation/PolicyResultPredicateWithName/`, `PropositionBuilders/Metadata/Spec/`, `PropositionBuilders/Metadata/Policy/`, `PropositionBuilders/Metadata/PolicyResultPredicate/`.

> Any factory that does **not** have an operation in scope (e.g. a `True…`/degenerate builder that hard-codes the higher-order predicate) should pass `null` and be noted in the commit message, so coverage gaps are explicit rather than silent (per the "no silent caps" convention).

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrderMatches"`
Expected: **PASS** — both new short-circuit facts and the equivalence matrix.

- [ ] **Step 8: Extend the equivalence matrix to both families**

Add two `Specs()` members to `HigherOrderMatchesEquivalenceTests.cs` using the result/policy builders (one `AsAllSatisfied` each is sufficient given the shared primitive), asserting `Matches == Evaluate().Satisfied` across `Datasets`.

- [ ] **Step 9: Run full suite + benchmark + commit**

Run: `dotnet test Motiv.slnx`
Run: `dotnet run -c Release --project src/examples/Motiv.Benchmark/Motiv.Benchmark.csproj --framework net8.0 -- --filter "*EvaluationBenchmarks*"` (confirm result-family `Matches`, if benchmarked, shows reduced allocation vs `Evaluate().Satisfied`; the wrapper array is gone though per-model result allocation remains).
```bash
git add -A
git commit -m "feat: short-circuit boolean-only Matches for BooleanResult/PolicyResult higher-order ops"
```

---

## Task 8: ExpressionTree family

Identical pattern; the projection uses the `_predicate` field: `static (m, p) => p.Execute(m).Satisfied`. This family reuses the `HigherOrderSpecPredicateOperation` (BooleanResult) operation already updated in Task 7, so no new operation struct or built-in methods are required — only propositions and factories.

**Files — propositions (all under `src/Motiv/HigherOrderProposition/ExpressionTree/`):**
- `MinimalHigherOrderFromExpressionTreeProposition.cs`
- `HigherOrderFromExpressionTreeMetadataProposition.cs`
- `HigherOrderFromExpressionTreeExplanationProposition.cs`
- `HigherOrderFromExpressionTreeMultiMetadataProposition.cs`
- `HigherOrderFromExpressionTreeMultiAssertionExplanationProposition.cs`

- [ ] **Step 1: Confirm the ExpressionTree builders use `HigherOrderSpecPredicateOperation`**

Run: `grep -rln "HigherOrderSpecPredicateOperation\|HigherOrderPolicyPredicateOperation" src/Motiv/HigherOrderProposition/PropositionBuilders/Metadata/ExpressionTree src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree`
Expected: the ExpressionTree factories carry one of the already-updated operation structs. If a factory carries neither (hard-codes the predicate), it takes the `null` fallback — note it.

- [ ] **Step 2: Write the failing short-circuit test**

Append to `src/Motiv.Tests/HigherOrderMatchesShortCircuitTests.cs`:

```csharp
[Fact]
public void ExpressionTree_AllSatisfied_Matches_equals_Evaluate_Satisfied()
{
    var spec = Spec.From((int n) => n > 0).AsAllSatisfied().Create("all positive");

    foreach (var data in new int[][] { [], [1, 2, 3], [1, -2, 3] })
        spec.Matches(data).ShouldBe(spec.Evaluate(data).Satisfied);
}
```

> A `calls`-counting short-circuit assertion is not straightforward for expression trees (the predicate is a compiled `Expression`, not an instrumented lambda). Equivalence + the benchmark allocation drop are the gates for this family. Confirm this test compiles and passes only after Step 3.

- [ ] **Step 3: Update the five ExpressionTree propositions**

For each file: append `HigherOrderShortCircuit? shortCircuit = null` as the last primary-constructor parameter and set `Matches` to:
```csharp
public override bool Matches(IEnumerable<TModel> models) =>
    shortCircuit is { } sc
        ? sc.Evaluate(models, _predicate, static (m, p) => p.Execute(m).Satisfied)
        : EvaluateModels(models).IsSatisfied;
```

- [ ] **Step 4: Forward `.ShortCircuit` in the ExpressionTree factories**

Run: `grep -rln "new HigherOrderFromExpressionTree\|new MinimalHigherOrderFromExpressionTree" src/Motiv/HigherOrderProposition/PropositionBuilders`
For each hit, append the in-scope operation variable's `.ShortCircuit` as the final constructor argument (or `null` where no operation is in scope — note it).

- [ ] **Step 5: Run tests + full suite**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --framework net8.0 --filter "FullyQualifiedName~HigherOrder"`
Run: `dotnet test Motiv.slnx`
Expected: **PASS**.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: short-circuit boolean-only Matches for ExpressionTree higher-order ops"
```

---

## Task 9: Final verification, benchmarks, docs, and simplifier

- [ ] **Step 1: Full solution test suite, all frameworks**

Run: `dotnet test Motiv.slnx`
Expected: **PASS** (~13,267 baseline tests + the new higher-order tests).

- [ ] **Step 2: Benchmark sweep**

Run: `dotnet run -c Release --project src/examples/Motiv.Benchmark/Motiv.Benchmark.csproj --framework net8.0 -- --filter "*EvaluationBenchmarks*"`
Expected: `HigherOrder_Matches` at **0 B**; scalar `Matches` benchmarks unchanged at 0 B.

- [ ] **Step 3: Spawn the mandatory `code-simplifier` over the full feature diff**

Dispatch a `code-simplifier` agent across all touched files. A likely suggestion: if the four per-family `Matches` bodies plus projections feel duplicative, consider a single generic helper `bool MatchesVia<TState>(IEnumerable<TModel> models, HigherOrderShortCircuit? sc, TState state, Func<TModel,TState,bool> project, Func<bool> fallback)` — but weigh against the codebase's "avoid over-DRYing / explicit per family" convention; keep per-family projections explicit if the abstraction adds indirection without clear benefit. Apply accepted changes, re-run `dotnet test src/Motiv.Tests/Motiv.Tests.csproj --filter "FullyQualifiedName~HigherOrder"`.

- [ ] **Step 4: Documentation**

The boolean-only `Matches` API predates this change; this task only makes the higher-order path faster (no API surface change), so no new public docs page is required. Add a one-line note under the higher-order docs that `Matches` short-circuits the built-in All/Any/N operations. Files: `docs/` higher-order section (follow existing structure) and, if a performance/evaluation note exists, mention the zero-allocation higher-order `Matches`.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "docs: note short-circuiting higher-order Matches; final simplifier pass"
```

---

## Self-Review Notes (author checklist — completed)

- **Spec coverage:** primitive (Task 1) ✓; all four families (Tasks 3, 7, 8) ✓; equivalence invariant (Tasks 3, 5, 7, 8) ✓; short-circuit behavior (Tasks 3, 7) ✓; null fallback for custom predicates (unchanged `As` overloads, Tasks 2/7) ✓; benchmark gate 104 B → 0 B (Tasks 4, 9) ✓; phased delivery (Phase 1 = Tasks 1–6, Phase 2 = Task 7, Phase 3 = Task 8) ✓; multi-framework + example-project tests (Tasks 6, 9) ✓; simplifier review (Tasks 6, 9) ✓.
- **Type consistency:** `HigherOrderShortCircuit` / `HigherOrderOp`, `Evaluate<TModel,TState>(IEnumerable<TModel>, TState, Func<TModel,TState,bool>)`, `.ShortCircuit` property, and the trailing `HigherOrderShortCircuit? shortCircuit = null` parameter are used identically in every task.
- **Known verification points (flagged inline, not placeholders):** exact `Spec.Build(...)` overload for the BooleanResult/PolicyResult test lambdas (Task 7 Step 1); the full factory construction-site list for the result and ExpressionTree families is discovered via the exact `grep` commands given (Tasks 7 Step 6, 8 Step 4) rather than pre-enumerated, because the ~48 result/expression factory sites span orthogonally-named directories and each must be confirmed to carry an operation in scope; sites without an operation take the documented `null` fallback and must be noted in the commit.
