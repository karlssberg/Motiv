# Higher-Order Rule-Document Support — Design

**Date:** 2026-07-19
**Status:** Approved (pending spec review)
**Scope:** `Motiv.Serialization` (parser + binder + registry), the TS wire
contract in `ui/packages/rules-core/src/contracts.ts`, and the sample host.

## Context

The rule-document grammar already models five higher-order node kinds
(`asAllSatisfied`, `asAnySatisfied`, `asNSatisfied`, `asAtLeastNSatisfied`,
`asAtMostNSatisfied`) with an `n` count and a `path` collection selector, but the
backend does **not** support them:

- **Parser** — [`RuleDocumentParser.cs:115`](../../../src/Motiv.Serialization/RuleDocumentParser.cs)
  lists the higher-order keys only to emit *"part of the rule format but is not
  yet supported by this loader"* (`InvalidNode`).
- **Binder** — [`RuleBinder.cs`](../../../src/Motiv.Serialization/RuleBinder.cs)
  has no case for them; `BindComposition` handles only the binary operators.

This design adds first-class **parse + evaluate** support for higher-order nodes
in the synchronous explanation loader (`RuleSerializer.Deserialize<TModel>` →
`RuleBinder`), which is the path the `/validate` and `/evaluate` endpoints use.

This is the first of three backend grammar sub-projects
(higher-order → parameters → expressions); the demo reference-implementation UI
(parked spec `2026-07-19-rules-engine-demo-reference-implementation-design.md`)
surfaces each feature only after it is evaluable.

## Native API this binds to

Confirmed against `src/Motiv`:

- Higher-order builders hang off `Spec.Build(childSpec)`:
  `AsAllSatisfied()`, `AsAnySatisfied()`, `AsNSatisfied(int n)`,
  `AsAtLeastNSatisfied(int n)`, `AsAtMostNSatisfied(int n)`. `n` is a literal
  `int`. The chain terminates in `.WhenTrue(...).WhenFalse(...).Create(...)`,
  producing a spec over **`IEnumerable<TElement>`**
  (`PolicyBase<IEnumerable<TElement>, string>` : `SpecBase<IEnumerable<TElement>, string>`).
- Model projection is `ChangeModelTo<TParent>(Func<TParent, TChild> selector)`
  ([`SpecBase.cs:311`](../../../src/Motiv/SpecBase.cs)), mapping **parent → child**.
  There is no inline collection-selector overload on the higher-order builders;
  `ChangeModelTo` is the only projection API.

The canonical binding target:
```csharp
Spec.Build(orderSpec)                       // SpecBase<Order, string>
    .AsAtLeastNSatisfied(3)
    .WhenTrue(synth).WhenFalse(synth).Create(synth)   // SpecBase<IEnumerable<Order>, string>
    .ChangeModelTo<Customer>(c => c.Orders);          // SpecBase<Customer, string>
```

## Design Decision (settled during brainstorming)

**Path resolution = host-registered collection selectors** (not reflection
navigation). Rationale: keeps the serialization layer reflection-free (it has
**zero** direct reflection today), is AOT/trim-friendly, is secure (only
whitelisted projections are reachable from an untrusted document — no arbitrary
model-graph traversal), and mirrors the existing `AddModel`/`Register` idiom. The
cost — one registration call per navigable collection — is small and explicit.

## Host API — collection registration

A parallel registration on `SpecRegistry`, alongside `Register(spec)`:

```csharp
public SpecRegistry RegisterCollection<TParent, TElement>(
    string path,
    Func<TParent, IEnumerable<TElement>> selector,
    string? description = null);
```

- Keyed by `(typeof(TParent), path)` using an ordinal comparer, in a dictionary
  separate from the spec entries. Duplicate `(parent, path)` throws, matching
  `Register`'s duplicate-name behavior. Empty/whitespace `path` throws.
- Each entry stores a `CollectionBinding<TParent>` (see below), boxed as `object`
  and retrieved from `BindNode<TParent>` with a **compile-time-safe cast** — no
  reflection, no `MakeGenericMethod`.
- Living on `SpecRegistry` means `RuleBinder`/`RuleSerializer` already have access
  (they hold the registry); **no `Bind` signature change** and no new type
  threaded through `MapMotivRules`.

Sample host wiring (example-only):
```csharp
registry.RegisterCollection<Customer, Order>("orders", c => c.Orders);
```

### `CollectionBinding<TParent>`

An internal type that captures `TElement` and the selector at registration time
and exposes a single typed closure the binder calls:

```csharp
internal abstract class CollectionBinding<TParent>
{
    public abstract Type ElementType { get; }
    public abstract SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors);
}

internal sealed class CollectionBinding<TParent, TElement> : CollectionBinding<TParent>
{
    private readonly Func<TParent, IEnumerable<TElement>> _selector;
    // ... ctor captures the selector ...
    public override Type ElementType => typeof(TElement);
    public override SpecBase<TParent, string>? BindHigherOrder(
        RuleNode node, SpecRegistry registry, List<RuleError> errors)
    {
        // 1. bind child subtree in element space:
        var child = RuleBinder.BindElement<TElement>(node.Children[0], registry, errors);
        if (child is null) return null;
        // 2. apply the higher-order operation with a synthesized default statement:
        var higher = HigherOrder.Build(child, node.Operator, node.N);   // SpecBase<IEnumerable<TElement>, string>
        // 3. project onto the parent:
        return higher.ChangeModelTo<TParent>(_selector);                // SpecBase<TParent, string>
    }
}
```

Because both `TParent` and `TElement` are compile-time type arguments (fixed at
the `RegisterCollection<TParent,TElement>` call), every generic dispatch is
static.

## Grammar + parser changes

- `RuleOperator` gains five members: `AsAllSatisfied`, `AsAnySatisfied`,
  `AsNSatisfied`, `AsAtLeastNSatisfied`, `AsAtMostNSatisfied`.
- `RuleNode` gains `public int? N { get; set; }` and `public string? Path { get; set; }`.
- `RuleDocumentParser`:
  - Recognizes each higher-order key as an operator whose value is a **single**
    child rule node (parsed like `not`), and reads the sibling `n` and `path`
    properties from the same object.
  - `path` is **required** and must be a non-empty string.
  - `n` is **required** for `asNSatisfied` / `asAtLeastNSatisfied` /
    `asAtMostNSatisfied`, and **forbidden** for `asAllSatisfied` /
    `asAnySatisfied`. A string `n` (a future `@param`) → `InvalidNode`
    *"parameter counts are not yet supported"*.
  - All parser-level violations use `InvalidNode` and are caught by
    `RuleSerializer.Validate` (parser-only) as well as `Deserialize`.

## Binder changes

`RuleBinder.BindNode<TParent>` gains a higher-order case (before the
binary-composition default):

1. Look up `registry.FindCollection<TParent>(node.Path!)` →
   `CollectionBinding<TParent>?`. On miss, add the new `UnknownCollection` error
   (*"no collection is registered at path 'orders' for model 'Customer'"*) and
   return null.
2. Call `binding.BindHigherOrder(node, registry, errors)`, which binds the child
   subtree via a new internal `RuleBinder.BindElement<TElement>` entry
   (a thin generic wrapper over the existing `BindNode<TElement>` recursion, so
   element-level compositions of registered `TElement` specs work; model
   mismatches reuse `ModelTypeMismatch`, unknown specs reuse `UnknownSpec`).
3. The higher-order spec is created with a **synthesized default statement**
   derived from the kind (e.g. `"all satisfied"`, `"any satisfied"`,
   `"at least 3 satisfied"`, `"at most 2 satisfied"`, `"exactly 3 satisfied"`),
   then `ChangeModelTo<TParent>` projects it.
4. The result returns up to the **existing** `Decorate(node, spec)` tail of
   `BindNode`, so document `name` / `whenTrue` / `whenFalse` decorate a
   higher-order node **uniformly with every other node** — no special-casing.

`HigherOrder.Build(child, op, n)` is a small internal helper switching on the
operator to call the matching `As*` method with `n!` for the N-variants.

## Errors

- **New:** `UnknownCollection` — add to the C# `RuleErrorCode` enum
  ([`RuleErrorCode.cs`](../../../src/Motiv.Serialization/RuleErrorCode.cs)) **and**
  to the TS `RuleErrorCode` union in
  [`contracts.ts`](../../../ui/packages/rules-core/src/contracts.ts) to keep the
  wire contract in sync.
- **Reused:** `InvalidNode` (parser shape / `n` rules / missing `path`),
  `ModelTypeMismatch` (child element-spec model mismatch), `UnknownSpec` (child
  references an unregistered spec).

## Testing (TDD)

Write failing tests first, in this order.

- **Parser** (`Motiv.Serialization.Tests`):
  - each of the five keys parses into the right `RuleOperator` with `N`/`Path`;
  - `n` required for the three N-variants, forbidden for all/any;
  - missing or empty `path` → `InvalidNode`;
  - string `n` → `InvalidNode` "parameter counts are not yet supported";
  - a nested child subtree (e.g. `asAllSatisfied` over `and[...]`) parses.
- **Registry** (`Motiv.Serialization.Tests`):
  - `RegisterCollection` duplicate `(parent, path)` throws; empty path throws;
  - `FindCollection<TParent>(path)` returns the binding; unknown path → null.
- **Binder / round-trip** (`Motiv.Serialization.Tests`), against a test
  `Customer { IEnumerable<Order> Orders }` with an `Order`-level spec:
  - `asAtLeastNSatisfied(2)` over `orders` evaluates true/false correctly and the
    justification reflects the higher-order outcome;
  - `asAllSatisfied` and `asAnySatisfied` evaluate correctly;
  - unknown `path` → `UnknownCollection`;
  - child spec with the wrong model type → `ModelTypeMismatch`;
  - `name` and `whenTrue`/`whenFalse` on a higher-order node flow into the
    assertions (decoration parity with other nodes).
- **Whole solution suite** stays green — including the example projects
  (`Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`) that
  assert on justification strings.

## Out of Scope

- `@param` counts for `n` (parameters sub-project).
- Expression leaves (expressions sub-project).
- The async loader and the metadata-object payload loader for higher-order nodes.
- Reflection-based path navigation.
- The demo builder UI (parked demo spec; built after all three backend features).

## Success Criteria

1. A rule document using any of the five higher-order kinds over a registered
   collection **loads and evaluates** through `RuleSerializer.Deserialize<TModel>`
   and the `/evaluate` endpoint, with correct satisfaction and justification.
2. `path` resolves only to host-registered collections; an unregistered path
   yields a clear `UnknownCollection` error at validate and evaluate time.
3. Decoration (`name` / `whenTrue` / `whenFalse`) works on higher-order nodes
   identically to every other node.
4. The serialization layer remains reflection-free.
5. All new and existing tests pass across the full solution.
