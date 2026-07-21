---
title: ToAsyncSpec()
---

`ToAsyncSpec()` lifts a synchronous specification into the asynchronous hierarchy so it can be composed with
asynchronous specifications and evaluated with `EvaluateAsync()`/`MatchesAsync()`.

```csharp
public AsyncSpecBase<TModel, TMetadata> ToAsyncSpec();
```

It is declared on `SpecBase<TModel, TMetadata>` and implemented on every synchronous proposition. In most
cases you don't need to call it directly &mdash; the composition methods and operators (`And()`, `AndAlso()`,
`Or()`, `OrElse()`, `XOr()`, `AndConcurrently()`, `OrConcurrently()`, `XOrConcurrently()`, `&`, `|`, `^`) all
accept a synchronous operand on either side and call `ToAsyncSpec()` internally. It is exposed for cases
where the lifted type is needed explicitly, e.g. assigning to an `AsyncSpecBase<TModel, TMetadata>` variable.

## Remarks

- **Evaluation remains fully synchronous internally.** `ToAsyncSpec()` wraps the existing synchronous
  evaluation in an already-completed `ValueTask` &mdash; it does not introduce any actual asynchrony, thread
  hops, I/O, or per-evaluation `Task` allocation. Results are identical to calling `Evaluate()` directly.
- **Policies stay policies.** `PolicyBase<TModel, TMetadata>.ToAsyncSpec()` returns an
  `AsyncPolicyBase<TModel, TMetadata>`, preserving the single-value guarantee &mdash; the same way `!policy`
  and `policy.OrElse(policy)` preserve policy-ness elsewhere in Motiv.
- **Mixed composition works in both directions.** `syncSpec.AndAlso(asyncSpec)` and
  `asyncSpec.AndAlso(syncSpec)` (and the `&`/`|`/`^` operator forms) both compile and produce an
  `AsyncSpecBase<TModel, TMetadata>` &mdash; the synchronous operand is lifted regardless of which side it's on.

## Explicit Example

```csharp
var isAdult = Spec.Build((int age) => age >= 18).Create("is adult");

AsyncSpecBase<int, string> asyncIsAdult = isAdult.ToAsyncSpec();

var result = await asyncIsAdult.EvaluateAsync(20);
result.Satisfied;  // true
result.Assertions; // ["is adult == true"]
```

## Mixed Sync/Async Composition, Both Directions

```csharp
var isAdult = Spec.Build((int age) => age >= 18).Create("is adult");
var hasCredit = Spec.BuildAsync((int _) => new ValueTask<bool>(true)).Create("has credit");

// sync.AndAlso(async) — the sync left operand is lifted automatically
var canBuy = isAdult.AndAlso(hasCredit);
(await canBuy.EvaluateAsync(20)).Satisfied; // true

// async.AndAlso(sync) — the sync right operand is lifted automatically
var canBuyReversed = hasCredit.AndAlso(isAdult);
(await canBuyReversed.EvaluateAsync(20)).Satisfied; // true
```

Both compositions produce the same result as an all-synchronous `isAdult.AndAlso(...)` would &mdash;
`Reason`, `Assertions`, and `Justification` are unaffected by which operand happened to originate from
`Spec.Build()` versus `Spec.BuildAsync()`.

## Next Steps

- Read the [Asynchronous Propositions](index.md) overview for the full async/sync type hierarchy.
- Use [`BuildAsync()`](BuildAsync.md) to construct native asynchronous propositions.
- See [Concurrent Operators](ConcurrentOperators.md) for opting a lifted synchronous operand into concurrent
  evaluation alongside an async one.
