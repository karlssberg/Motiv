---
title: EvaluateAsync()
---

`EvaluateAsync()` asynchronously evaluates an asynchronous proposition against a model, returning the same
immutable result types produced by the synchronous `Evaluate()`. `MatchesAsync()` is the asynchronous
counterpart of `Matches()` &mdash; a boolean-only evaluation for callers that don't need the explanation.

```csharp
Task<BooleanResultBase<TMetadata>> EvaluateAsync(TModel model, CancellationToken cancellationToken = default);

Task<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default);
```

`AsyncPolicyBase<TModel, TMetadata>` narrows the return type of `EvaluateAsync()` to
`Task<PolicyResultBase<TMetadata>>`, mirroring how `PolicyBase<TModel, TMetadata>.Evaluate()` narrows to
`PolicyResultBase<TMetadata>` &mdash; a policy still guarantees exactly one assertion or metadata value per
evaluation.

## Remarks

- **Results are identical to synchronous evaluation.** `Satisfied`, `Reason`, `Assertions`, `Justification`,
  and `Values` are computed by the same de-noising logic that powers `Evaluate()` &mdash; asynchrony only
  changes how the underlying predicates are invoked, never what the result contains.
- **Cancellation threads through every operand.** A `CancellationToken` passed to `EvaluateAsync()` or
  `MatchesAsync()` reaches every async predicate in the composition that accepts one. Cancelling mid-evaluation
  stops issuing further predicate calls and surfaces a standard `OperationCanceledException`; predicate
  exceptions propagate exactly as they do out of the synchronous `Evaluate()`, with no additional wrapping.
- **`MatchesAsync()` is a boolean-only fast path.** It answers `Satisfied` without necessarily materializing
  the full explanation, mirroring the synchronous `Matches()`.
- **No sync-over-async bridge.** There is no blocking `Evaluate()` on an asynchronous specification &mdash;
  callers must `await` `EvaluateAsync()`/`MatchesAsync()` all the way up the call stack.

## Basic Example

```csharp
var isEven = Spec
    .BuildAsync((int n) => Task.FromResult(n % 2 == 0))
    .Create("is even");

var result = await isEven.EvaluateAsync(3);
result.Satisfied;  // false
result.Assertions; // ["is even == false"]

var matched = await isEven.MatchesAsync(3);
matched; // false
```

## Cancellation Across a Composition

```csharp
using var cts = new CancellationTokenSource();

AsyncPolicyBase<object, string> Leaf(string name, bool value) =>
    Spec.BuildAsync((object _, CancellationToken ct) => Task.FromResult(value))
        .Create(name);

var isEligible = Leaf("has stock", true).AndAlso(Leaf("is paid", true));

var result = await isEligible.EvaluateAsync(new object(), cts.Token);
result.Satisfied;  // true
result.Reason;     // "(has stock == true) && (is paid == true)"
result.Assertions; // ["has stock == true", "is paid == true"]
```

The same `cts.Token` is observed by both leaf predicates &mdash; cancelling `cts` before or during evaluation
propagates through the whole `AndAlso` composition.

## Next Steps

- Use [`BuildAsync()`](BuildAsync.md) to construct the propositions being evaluated.
- Use [`ToAsyncSpec()`](ToAsyncSpec.md) to bring synchronous propositions into a composition evaluated with
  `EvaluateAsync()`.
- See [Concurrent Operators](ConcurrentOperators.md) for opting individual operands into concurrent evaluation.
