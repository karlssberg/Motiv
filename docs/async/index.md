---
title: Asynchronous Propositions
description: Documentation for asynchronous propositions in Motiv, which compose rules that depend on I/O (databases, APIs, feature flags) with the same explainable results as synchronous propositions.
---

Real-world rules frequently depend on I/O &mdash; database lookups, HTTP APIs, feature-flag services.
Asynchronous propositions let you build those rules with `Spec.BuildAsync()` and compose them with the
same logical operators as synchronous propositions, while preserving true short-circuiting of
asynchronous work: an operand behind `AndAlso`/`OrElse` is never awaited, let alone started, once the
outcome no longer depends on it.

## Async Specifications

Asynchronous propositions live in a parallel type hierarchy that mirrors the synchronous one:

```
AsyncSpecBase<TModel>
└── AsyncSpecBase<TModel, TMetadata>       — EvaluateAsync() / MatchesAsync()
    └── AsyncPolicyBase<TModel, TMetadata>
```

| Type                                          | Base type                          | Guarantees                                                              |
|------------------------------------------------|-------------------------------------|--------------------------------------------------------------------------|
| `AsyncSpecBase<TModel, TMetadata>`              | `SpecBase` (non-generic root)       | Evaluates asynchronously; may yield multiple assertions/metadata per evaluation |
| `AsyncPolicyBase<TModel, TMetadata>`            | `AsyncSpecBase<TModel, TMetadata>`  | Asynchronous *and* resolves to exactly one assertion/metadata value       |

Every `Spec.BuildAsync()` builder path (minimal, explanation, metadata, and their `Yield` variants) returns
one of these two types instead of the synchronous `SpecBase`/`PolicyBase`. **Results are unchanged** &mdash;
`EvaluateAsync()` returns the same immutable `BooleanResultBase<TMetadata>` (or `PolicyResultBase<TMetadata>`
for policies) that synchronous evaluation produces. Asynchrony only concerns the *production* of a result;
explanation, justification, and result composition need nothing new.

## Building and Evaluating

[`Spec.BuildAsync()`](BuildAsync.md) accepts an async predicate and supports the same downstream builder
surface as [`Spec.Build()`](../builder/Build.md) &mdash; `WhenTrue`, `WhenFalse`, `WhenTrueYield`,
`WhenFalseYield`, `Create()` / `Create("name")` &mdash; including the v8 named-metadata semantics documented
under [Custom Assertions](../../README.md#custom-assertions). [`EvaluateAsync()`](EvaluateAsync.md) and
`MatchesAsync()` mirror `Evaluate()` and `Matches()`, threading a `CancellationToken` through every async
predicate in the composition.

```csharp
var isEven = Spec
    .BuildAsync(async (int n) =>
    {
        await Task.Yield();
        return n % 2 == 0;
    })
    .Create("is even");

var result = await isEven.EvaluateAsync(3);
result.Satisfied;  // false
result.Assertions; // ["is even == false"]
```

## Sequential by Default

Binary operators (`And()`/`&`, `AndAlso()`, `Or()`/`|`, `OrElse()`, `XOr()`/`^`) evaluate their operands
sequentially, left then right, by default. This is deliberate: a predicate frequently captures a
non-thread-safe dependency (an EF Core `DbContext`, a per-request scoped service), and evaluating two
operands that share such a dependency concurrently would corrupt it. Sequential-by-default keeps async
composition safe without requiring the caller to reason about the internals of every operand.

When operands are known to be safe to run concurrently (independent HTTP calls, independent database
connections), [`AndConcurrently()`, `OrConcurrently()`, and `XOrConcurrently()`](ConcurrentOperators.md) opt
in to `Task.WhenAll`-based evaluation. The result is indistinguishable from the sequential form &mdash; same
`Reason`, `Assertions`, and `Justification` &mdash; only the evaluation strategy differs.

## Mixed Sync/Async Composition

Synchronous and asynchronous propositions compose freely in both directions. A synchronous operand is lifted
into the asynchronous hierarchy via [`ToAsyncSpec()`](ToAsyncSpec.md) (evaluation remains fully synchronous
internally; only the outer `ValueTask` wrapping is added, without allocating a `Task` per evaluation):

```csharp
var isAdult = Spec.Build((int age) => age >= 18).Create("is adult");
var hasCredit = Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create("has credit");

var canBuy = isAdult.AndAlso(hasCredit);   // AsyncSpecBase<int, string> — sync operand lifted automatically
var reversed = hasCredit.AndAlso(isAdult); // also legal — either order works
```

## Cancellation

A `CancellationToken` passed to `EvaluateAsync()`/`MatchesAsync()` threads through every operand in the
composition, reaching every async predicate that accepts a `CancellationToken` parameter. Cancellation
surfaces as a standard `OperationCanceledException`; predicate exceptions propagate out of `EvaluateAsync()`
exactly as they do out of the synchronous `Evaluate()`, with no additional wrapping.

## No Sync-Over-Async Bridge

There is deliberately no `.ToSyncSpec()` and no blocking `Evaluate()` on an asynchronous specification.
Once a proposition enters the asynchronous hierarchy &mdash; whether built directly with `Spec.BuildAsync()`
or composed with an async operand &mdash; it can only be evaluated with `EvaluateAsync()`/`MatchesAsync()`.
This avoids the deadlocks and thread-pool starvation that blocking on async code is prone to; callers must
`await` all the way up.

## Available Methods

| Method                                    | Description                                                                                       |
|---------------------------------------------|-----------------------------------------------------------------------------------------------------|
| [BuildAsync()](BuildAsync.md)               | Initiates asynchronous proposition construction from an async predicate.                          |
| [EvaluateAsync()](EvaluateAsync.md)         | Asynchronously evaluates a proposition, returning the same result types as synchronous evaluation. |
| [ToAsyncSpec()](ToAsyncSpec.md)             | Lifts a synchronous specification into the asynchronous hierarchy so it can compose with async operands. |
| [Concurrent Operators](ConcurrentOperators.md) | `AndConcurrently()`, `OrConcurrently()`, `XOrConcurrently()` — opt-in `Task.WhenAll` evaluation with results identical to the sequential form. |

## Next Steps

- Read about [`Spec.Build()`](../builder/Build.md), the synchronous entry point that `Spec.BuildAsync()` mirrors.
- Explore the [Logical Operators](../operators/index.md) used to compose propositions.
- See [`BuildAsync()`](BuildAsync.md), [`EvaluateAsync()`](EvaluateAsync.md), [`ToAsyncSpec()`](ToAsyncSpec.md),
  and [Concurrent Operators](ConcurrentOperators.md) for the methods this feature adds.
