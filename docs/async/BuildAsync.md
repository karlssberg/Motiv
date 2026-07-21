---
title: BuildAsync()
---

`BuildAsync()` initiates the construction of an asynchronous proposition from an async predicate. It is the
asynchronous counterpart of [`Build()`](../builder/Build.md) &mdash; the downstream builder surface
(`WhenTrue`, `WhenFalse`, `WhenTrueYield`, `WhenFalseYield`, `Create()` / `Create("name")`) is semantically
identical, including the v8 named-metadata rules described under
[Custom Assertions](../../README.md#custom-assertions).

```csharp
BuildAsync<TModel>(Func<TModel, CancellationToken, ValueTask<bool>> predicate)

BuildAsync<TModel>(Func<TModel, ValueTask<bool>> predicate)
```

Two overloads are available: one whose predicate accepts a `CancellationToken`, and one that doesn't. Both
continue into the same fluent builder chain as [`Build()`](../builder/Build.md), terminating in an
`AsyncSpecBase<TModel, TMetadata>` (or `AsyncPolicyBase<TModel, TMetadata>` for the minimal/single-value
paths) once `Create()` is called.

## Remarks

- **Results are identical to the synchronous builder.** The same `WhenTrue`/`WhenFalse`/`Create` rules that
  govern minimal, explanation, and metadata propositions built with `Build()` apply unchanged &mdash; only the
  predicate and evaluation method are asynchronous.
- **The `CancellationToken` overload receives whatever token is passed to evaluation.** When the predicate
  accepts a `CancellationToken`, it receives the token passed to `EvaluateAsync()`/`MatchesAsync()` (or
  `CancellationToken.None` if none was supplied).
- **No sync-over-async bridge.** A proposition built with `BuildAsync()` only exposes `EvaluateAsync()` and
  `MatchesAsync()` &mdash; there is no synchronous `Evaluate()` to fall back to.

## Minimal Proposition Example

```csharp
var isEven = Spec
    .BuildAsync(async (int n) =>
    {
        await Task.Yield();
        return n % 2 == 0;
    })
    .Create("is even");

var result = await isEven.EvaluateAsync(2);
result.Satisfied;  // true
result.Reason;     // "is even == true"
result.Assertions; // ["is even == true"]
```

## Explanation Proposition Example

```csharp
var isActive = Spec
    .BuildAsync((bool b) => new ValueTask<bool>(b))
    .WhenTrue("user is active")
    .WhenFalse("user is not active")
    .Create();

var result = await isActive.EvaluateAsync(true);
result.Assertions; // ["user is active"]
```

Supplying an explicit name via `Create("name")` demotes the strings to metadata (`Values`), exactly as it
does for the synchronous builder:

```csharp
var isActive = Spec
    .BuildAsync((bool b) => new ValueTask<bool>(b))
    .WhenTrue("user is active")
    .WhenFalse("user is not active")
    .Create("is active");

var result = await isActive.EvaluateAsync(true);
result.Assertions; // ["is active == true"]
result.Values;     // ["user is active"]
```

## Cancellation Token Overload

```csharp
var spec = Spec
    .BuildAsync((int model, CancellationToken ct) =>
        externalService.CheckAsync(model, ct))
    .Create("passes external check");

using var cts = new CancellationTokenSource();
var result = await spec.EvaluateAsync(42, cts.Token);
// externalService.CheckAsync receives cts.Token
```

## Next Steps

- Use [`EvaluateAsync()`](EvaluateAsync.md) to run the built proposition and read cancellation-threading rules
  for composed specifications.
- Use [`ToAsyncSpec()`](ToAsyncSpec.md) to compose a `BuildAsync()` proposition with existing synchronous
  propositions.
- Read the [Asynchronous Propositions](index.md) overview for the full type hierarchy and design rationale.
