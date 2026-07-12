# Design: Short-circuiting boolean-only fast path for higher-order `Matches`

**Date:** 2026-07-12
**Status:** Approved for planning
**Area:** `src/Motiv/HigherOrderProposition`

## Problem

`Matches(TModel model)` is Motiv's allocation-free, boolean-only evaluation path — consumers who only need the boolean outcome can call it and skip all assertion/metadata/result construction. Benchmarks confirm it is already optimal for scalar and composed propositions (zero allocation, effectively free):

| Family | `Evaluate().Satisfied` | `Matches` | `Matches` alloc |
|---|---|---|---|
| Minimal | 7.29 ns / 176 B | ~0 ns | **0 B** |
| Explanation | 4.71 ns / 160 B | ~0 ns | **0 B** |
| Metadata | 4.79 ns / 160 B | ~0 ns | **0 B** |
| MultiAssertion | 4.48 ns / 152 B | ~0 ns | **0 B** |
| Composed (`&`) | 18.52 ns / 480 B | 1.69 ns | **0 B** |
| **HigherOrder (`AsAllSatisfied`)** | 44.9 ns / 296 B | 36.7 ns / **104 B** | **104 B** |

*(net8.0, BenchmarkDotNet ShortRun, AMD Ryzen 9 9950X.)*

**Higher-order is the sole outlier.** Every higher-order `Matches` routes through `EvaluateModels(...).IsSatisfied`, which:

1. Materializes a per-model result array via `HigherOrderResults.Materialize` (the 104 B — a single array of value-type entries), then
2. Passes it to an opaque `Func<IEnumerable<…>, bool>` higher-order predicate.

Consequences on the boolean-only path:

- **Allocation** — the per-model array is always allocated, even though the boolean outcome does not require it.
- **No short-circuit** — `AsAllSatisfied` over `[1, -2, 3, …]` visits all models and allocates, when it could stop at the first `false` (`-2`, the 2nd element) with zero allocation.

## Goal

Give the built-in higher-order operations a short-circuiting, minimal-allocation boolean-only path on `Matches`, across **all four** higher-order families:

- **BooleanPredicate** — underlying `Func<TModel,bool>`. Target: **104 B → 0 B**.
- **BooleanResultPredicate** — underlying `Func<TModel, BooleanResultBase<TMetadata>>`.
- **PolicyResultPredicate** — underlying `Func<TModel, PolicyResultBase<TMetadata>>`.
- **ExpressionTree** — underlying compiled expression producing `BooleanResult<TModel,string>`.

For the three result-based families, per-model result allocation is inherent (the underlying source *returns* a result), so they cannot reach zero allocation. But they still gain: the per-model wrapper **array is eliminated** and evaluation **short-circuits** (underlying results stop being produced once the outcome is decided).

Non-goals: no change to the full `Evaluate`/`EvaluatePolicy` path (assertions, causes, values, ordering all unchanged); no change to custom higher-order predicates (`As(customPredicate)`), which retain today's exact behavior.

## Key insight: a unifying boolean projection

Every family's `Matches` needs the same thing — a per-model `bool` derived from the raw source:

| Family | Per-model boolean projection |
|---|---|
| BooleanPredicate | `predicate(model)` |
| BooleanResultPredicate | `resultResolver(model).Satisfied` |
| PolicyResultPredicate | `resultResolver(model).Satisfied` |
| ExpressionTree | `_predicate.Execute(model).Satisfied` |

And every built-in operation's satisfaction is a **pure function of the multiset of those per-model booleans** (All / Any / None / AtLeast(n) / AtMost(n) / Exactly(n)). This is what makes the fast path provably equivalent to the current path and lets a single primitive serve all families.

## Design

### 1. `HigherOrderShortCircuit` — the short-circuit primitive

A new internal readonly struct describing a built-in operation as data, plus a generic, allocation-free evaluator:

```csharp
internal enum HigherOrderOp { All, Any, None, AtLeast, AtMost, Exactly }

internal readonly struct HigherOrderShortCircuit(HigherOrderOp op, int n)
{
    // Cached, non-capturing descriptors for the parameterless ops.
    internal static HigherOrderShortCircuit All  { get; } = new(HigherOrderOp.All, 0);
    internal static HigherOrderShortCircuit Any  { get; } = new(HigherOrderOp.Any, 0);
    internal static HigherOrderShortCircuit None { get; } = new(HigherOrderOp.None, 0);
    internal static HigherOrderShortCircuit AtLeast(int n) => new(HigherOrderOp.AtLeast, n);
    internal static HigherOrderShortCircuit AtMost(int n)  => new(HigherOrderOp.AtMost, n);
    internal static HigherOrderShortCircuit Exactly(int n) => new(HigherOrderOp.Exactly, n);

    // Generic over the projection *state* so call sites pass a static (non-capturing)
    // lambda — no per-evaluation closure allocation, mirroring HigherOrderResults.Materialize.
    internal bool Evaluate<TModel, TState>(
        IEnumerable<TModel> models,
        TState state,
        Func<TModel, TState, bool> project);
}
```

`Evaluate` iterates with the same source-type fast-paths as `HigherOrderResults.Materialize` (`TModel[]` indexed loop, then `IReadOnlyList<TModel>`, then enumerator fallback) and applies per-op early exit:

| Op | Rule | Early exit | Empty input |
|---|---|---|---|
| All | every projected `true` | first `false` → `false` | `true` |
| Any | some projected `true` | first `true` → `true` | `false` |
| None | no projected `true` | first `true` → `false` | `true` |
| AtLeast(n) | count `≥ n` | count reaches `n` → `true` | `0 ≥ n` |
| AtMost(n) | count `≤ n` | count exceeds `n` → `false` | `true` (`0 ≤ n`) |
| Exactly(n) | count `== n` | count exceeds `n` → `false` | `0 == n` |

Boundary cases to get right (and test): `n = 0` for each counting op, and `n = count`.

### 2. Thread an optional descriptor through the operation structs

Add an optional `HigherOrderShortCircuit? ShortCircuit { get; }` to each of the three operation structs:

- `HigherOrderSpecBooleanPredicateOperation<TModel>`
- `HigherOrderSpecPredicateOperation<TModel, TUnderlyingMetadata>` (also used by ExpressionTree)
- `HigherOrderPolicyPredicateOperation<TModel, TUnderlyingMetadata>`

The six built-in `As*` methods in each of the three method classes set it (`HigherOrderShortCircuit.All`, `HigherOrderShortCircuit.AtLeast(n)`, …). The two generic `As(customPredicate)` overloads leave it `null`. The `higherOrderPredicate` and `causeSelector` members are unchanged — the descriptor is purely additive.

### 3. Each higher-order proposition consumes it in `Matches`

Every higher-order proposition gains a constructor parameter `HigherOrderShortCircuit? shortCircuit` and updates only `Matches`:

```csharp
// BooleanPredicate family
public override bool Matches(IEnumerable<TModel> models) =>
    shortCircuit is { } sc
        ? sc.Evaluate(models, predicate, static (m, p) => p(m))
        : EvaluateModels(models).IsSatisfied;

// Result families (BooleanResult / PolicyResult)
public override bool Matches(IEnumerable<TModel> models) =>
    shortCircuit is { } sc
        ? sc.Evaluate(models, resultResolver, static (m, r) => r(m).Satisfied)
        : EvaluateModels(models).IsSatisfied;

// ExpressionTree family
public override bool Matches(IEnumerable<TModel> models) =>
    shortCircuit is { } sc
        ? sc.Evaluate(models, _predicate, static (m, p) => p.Execute(m).Satisfied)
        : EvaluateModels(models).IsSatisfied;
```

`EvaluateModels`, `EvaluateSpec`/`EvaluatePolicy`, and everything on the full path are untouched. When `shortCircuit` is `null` (custom predicate), behavior is byte-for-byte today's.

### 4. Factories pass the descriptor through

Every factory that constructs a higher-order proposition already forwards `operation.HigherOrderPredicate` and `operation.CauseSelector`; it additionally forwards `operation.ShortCircuit`.

## Behavioral change (accepted)

With short-circuit, `Matches` invokes the underlying source (predicate / result resolver / expression) **fewer times** than today once the outcome is decided. For a pure predicate this is unobservable. For a side-effecting one, invocation count drops — identical in spirit to the short-circuit semantics Motiv already ships for `AndAlso`/`OrElse`. `Matches` makes no exhaustive-invocation guarantee. This change was explicitly approved.

The full `Evaluate` path retains exhaustive evaluation (it must, to build every underlying result and cause), so anyone relying on complete evaluation continues to use `Evaluate`.

## Correctness strategy (TDD)

The governing invariant: for every built-in operation, every family, and every input,

```
spec.Matches(models) == spec.Evaluate(models).Satisfied
```

Tests are written **first** and must fail before implementation. Coverage per operation × family:

- Empty collection
- All satisfied
- All unsatisfied
- Mixed
- Boundaries around `n`: `n = 0`, `n = 1`, `n = count - 1`, `n = count`, `n > count`

A side-effect test asserts short-circuit actually occurs (e.g. a counting predicate confirms `AsAllSatisfied.Matches` stops at the first `false`, and `AsAnySatisfied.Matches` stops at the first `true`).

## Delivery phases

Each phase is TDD → implement → run affected tests → benchmark/verify → `code-simplifier` review.

1. **BooleanPredicate family** — the measured, zero-alloc case. Proves the `HigherOrderShortCircuit` primitive and the operation/proposition/factory wiring end-to-end. Benchmark gate: `HigherOrder_Matches` **104 B → 0 B**.
2. **BooleanResultPredicate + PolicyResultPredicate families** — result-based; array eliminated + short-circuit. Add benchmarks confirming reduced allocation vs. `Evaluate().Satisfied`.
3. **ExpressionTree family** — reuses the `BooleanResult` operation/primitive; array eliminated + short-circuit.

## Blast radius

Large but highly uniform:

- **New:** 1 `HigherOrderShortCircuit` type (+ `HigherOrderOp` enum).
- **Operation structs:** 3, each gains one optional property.
- **Built-in `As*` methods:** 18 (6 ops × 3 method classes) set the descriptor.
- **Propositions:** ~19 across the four families — 1 ctor param + a 2-line `Matches` change each.
- **Factories:** ~30 forward one additional argument.
- **Tests / benchmarks:** equivalence + short-circuit suites; expand `EvaluationBenchmarks` for the result families.

## Verification

- Full solution test suite green (including example projects, which assert on justification strings — unaffected here, but run per project convention).
- `EvaluationBenchmarks` re-run confirming `HigherOrder_Matches` reaches 0 B and result-family `Matches` benchmarks show reduced allocation and latency vs. the full path.
- `code-simplifier` review after each phase.

## Alternatives considered

- **Polymorphic higher-order operation object** replacing the two raw delegates with one type that knows both the result-based predicate and the boolean short-circuit. Cleaner OO, but a far larger refactor across all families and against the codebase's "explicit over clever, avoid over-DRYing" convention. Rejected.
- **Reflect/identify the built-in delegate at `Matches` time** (recognize `AllTrue`/`AnyTrue`/… delegates). Fragile and unreliable. Rejected.
- **Scope to BooleanPredicate only.** Considered; reaches zero-alloc for the common case with minimal blast radius, but leaves the result families paying for the wrapper array and forgoing short-circuit. Rejected in favor of full coverage.
