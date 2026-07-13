---
title: Concurrent Operators
---

`AndConcurrently()`, `OrConcurrently()`, and `XOrConcurrently()` are opt-in concurrent variants of
`And()`/`&`, `Or()`/`|`, and `XOr()`/`^` for asynchronous propositions. They evaluate both operands via
`Task.WhenAll` instead of sequentially.

```csharp
AsyncSpecBase<TModel, TMetadata> AndConcurrently(AsyncSpecBase<TModel, TMetadata> spec);
AsyncSpecBase<TModel, TMetadata> OrConcurrently(AsyncSpecBase<TModel, TMetadata> spec);
AsyncSpecBase<TModel, TMetadata> XOrConcurrently(AsyncSpecBase<TModel, TMetadata> spec);
```

Each is declared on `AsyncSpecBase<TModel, TMetadata>`, with an overload accepting a synchronous
`SpecBase<TModel, TMetadata>` operand (lifted via [`ToAsyncSpec()`](ToAsyncSpec.md) before evaluation).

## Remarks

- **Results are indistinguishable from the sequential form.** `Satisfied`, `Reason`, `Assertions`, and
  `Justification` are identical to what `And()`/`Or()`/`XOr()` would produce for the same operands &mdash;
  only the evaluation strategy differs. Concurrency and asynchrony are evaluation details, never semantic
  ones.
- **Only opt in when operands are safe to run concurrently.** The default sequential evaluation exists
  because a predicate frequently captures a non-thread-safe dependency &mdash; an EF Core `DbContext`, a
  per-request scoped service. Use the concurrent variants only when both operands' predicates are known to be
  safe to execute at the same time (e.g. independent HTTP calls, independent database connections).
- **Both operands always run** &mdash; there is no concurrent short-circuiting variant. `AndConcurrently()`
  starts both operands before either has a result, the same way `And()` always evaluates both operands
  sequentially; use `AndAlso()`/`OrElse()` (sequential) when short-circuiting is required, since skipping an
  operand entirely is incompatible with starting it concurrently.
- **Exceptions propagate as `Task.WhenAll` propagates them.**

## Example

```csharp
AsyncPolicyBase<object, string> Left() =>
    Spec.BuildAsync((object _) => Task.FromResult(true)).Create("left");
AsyncPolicyBase<object, string> Right() =>
    Spec.BuildAsync((object _) => Task.FromResult(true)).Create("right");

var sequential = Left() & Right();
var concurrent = Left().AndConcurrently(Right());

var sequentialResult = await sequential.EvaluateAsync(new object());
var concurrentResult = await concurrent.EvaluateAsync(new object());

sequentialResult.Reason;      // "(left == true) & (right == true)"
concurrentResult.Reason;      // "(left == true) & (right == true)" — identical to sequential
sequentialResult.Assertions;  // ["left == true", "right == true"]
concurrentResult.Assertions;  // ["left == true", "right == true"] — identical to sequential
```

## Next Steps

- Read the [Asynchronous Propositions](index.md) overview for why sequential evaluation is the default.
- Use [`BuildAsync()`](BuildAsync.md) to construct the operands being composed concurrently.
- Use [`ToAsyncSpec()`](ToAsyncSpec.md) to bring a synchronous operand into a concurrent composition.
