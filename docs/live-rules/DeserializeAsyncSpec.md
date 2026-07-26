---
title: DeserializeAsyncSpec()
---

`DeserializeAsyncSpec()` loads a rule document into the asynchronous hierarchy
(`AsyncSpecBase<TModel, TMetadata>`), resolving spec references against a registry that may contain
both synchronous and asynchronous entries. `ValidateAsyncSpec()` is its non-throwing counterpart,
accumulating every error instead of throwing on the first. [`AsyncRule` and
`AsyncPolicyRule`](Rules.md) bind their documents through these entry points.

```csharp
// Explanation loads (TMetadata = string)
AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json);
AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json, object? parameters);
AsyncSpecBase<TModel, string> DeserializeAsyncSpec<TModel>(string json, IReadOnlyDictionary<string, object?>? parameters);

// Typed-metadata loads
AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json);
AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json, object? parameters);
AsyncSpecBase<TModel, TMetadata> DeserializeAsyncSpec<TModel, TMetadata>(string json, IReadOnlyDictionary<string, object?>? parameters);

// Validation (non-throwing)
IReadOnlyList<RuleError> ValidateAsyncSpec<TModel>(string json);
IReadOnlyList<RuleError> ValidateAsyncSpec<TModel, TMetadata>(string json);
```

## Basic Example

```csharp
var registry = new SpecRegistry()
    .Register("is-adult",
        Spec.Build((Customer c) => c.Age >= 18).Create("is adult"))
    .Register("passes-credit-check",
        Spec.BuildAsync((Customer c, CancellationToken ct) => CheckCreditAsync(c, ct))
            .Create("passes credit check"));

var serializer = new RuleSerializer(registry);

var spec = serializer.DeserializeAsyncSpec<Customer>("""
    { "rule": { "and": [ { "spec": "is-adult" }, { "spec": "passes-credit-check" } ] } }
    """);

var result = await spec.EvaluateAsync(customer, cancellationToken);
```

## The Sync/Async Boundary

An asynchronous load composes a mixed registry under three rules:

- **Sync leaves are lifted.** A reference to a synchronous registry entry is lifted into the async
  hierarchy, exactly as [`ToAsyncSpec()`](../async/ToAsyncSpec.md) lifts a spec in code &mdash;
  evaluation remains fully synchronous internally, with only the outer `ValueTask` wrapping added.
- **Async leaves are used directly.** A reference to an asynchronous registry entry composes as-is,
  with cancellation threading through from `EvaluateAsync()`.
- **Higher-order subtrees bind synchronously, then lift.** A quantifier node (`asAllSatisfied`,
  `asAnySatisfied`, `asAtLeastNSatisfied`, …) binds its entire subtree with the *synchronous* binder
  and lifts the composed result. An async spec referenced anywhere inside a higher-order subtree is
  rejected with the `AsyncSpecInHigherOrder` error &mdash; quantifiers evaluate their operand across
  a collection synchronously, so the subtree must be fully synchronous.

The reverse direction is stricter: a *synchronous* load (`Deserialize()`/`Validate()`) that
references an async registry entry anywhere fails with `AsyncSpecInSyncLoad` &mdash; asynchrony
never silently enters a synchronous rule.

## Remarks

- **Same document format.** Async loads read the same rule-document JSON as synchronous loads; only
  the binding differs. The same document can bind synchronously or asynchronously, provided its
  references satisfy the boundary rules above.
- **Typed metadata mirrors the sync surface.** `DeserializeAsyncSpec<TModel, TMetadata>()` resolves
  object payloads and registry entries against `TMetadata`, with the same errors
  (`MetadataTypeMismatch`, `MixedWhenTrueFalseKinds`) as the synchronous
  `Deserialize<TModel, TMetadata>()`. `TMetadata = string` forwards to the explanation load.
- **Validation takes no parameter values.** `ValidateAsyncSpec()` stands required parameters in with
  type-shaped placeholders, so parameter-supply errors are only reported by
  `DeserializeAsyncSpec()`.
- **Policy enforcement happens at the rule.** These entry points return specs; a
  `PolicyRule`/`AsyncPolicyRule` additionally requires the bound root to be a policy and rejects the
  document with `PolicyRequired` otherwise.

## Next Steps

- See [Rule Classes](Rules.md) for the async rules that bind through these entry points.
- Read about [Asynchronous Propositions](../async/index.md) &mdash; the hierarchy async loads compose into.
- See [`ToAsyncSpec()`](../async/ToAsyncSpec.md), the in-code equivalent of sync-leaf lifting.
