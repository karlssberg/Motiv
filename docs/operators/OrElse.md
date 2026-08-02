---
title: OrElse()
category: operators
---
# OrElse()

### [Propositions](xref:Motiv.SpecBase`2)

You can perform a conditional OR (i.e., short-circuited) operation on two <xref:Motiv.SpecBase`2> in only one way:

* `left.OrElse(right)`

Only the `OrElse()` method is available for propositions because C# cannot overload `||` directly.
The expression `x || y` compiles to `T.true(x) ? x : T.|(x, y)`, and the selected `operator |` must
take *and* return exactly `T` — so a short-circuiting operator on propositions would have to be built
out of the eager `|`, which always evaluates both operands. `x || y` also short-circuits by returning
`x` itself rather than a composed node, so it could not produce the `OrElse` node that appears in a
justification tree.

The conditional OR will produce a new proposition that represents the logical OR of the two input propositions.
When evaluating the resulting proposition, the right operand will only be evaluated if the left is unsatisfied.

For example:

```csharp
record Product(string Name, decimal Price, Size Size);

var expensiveProductSpec = Spec
    .Build((Product p) => p.Price > 1000)
    .WhenTrue("product is expensive")
    .WhenFalse("product is not expensive")
    .Create();

var isProductSizeSmallSpec = Spec
    .Build((Product p) => p.Size == Size.Small)
    .WhenTrue("product is easily stolen")
    .WhenFalse("product is not easily stolen")
    .Create();

var isAtRiskShelfItemSpec = expensiveProductSpec.OrElse(isProductSizeSmallSpec);

var product = new Product("Laptop", 1500, true);
var isAtRiskShelfItem = isAtRiskShelfItemSpec.Evaluate(product);

isAtRiskShelfItem.Satisfied; // true
isAtRiskShelfItem.Reason; // "product is expensive | product is easily stolen"
isAtRiskShelfItem.Assertions; // ["product is expensive", "product is easily stolen"]
```

If you want to give it a true or false reasons, you can do so by wrapping it in a new specification.

For example:

```csharp
var isProductAtRiskOfTheftSpec = 
    Spec.Build(expensiveProductSpec | isProductSizeSmallSpec)
        .WhenTrue("the product is at risk of theft")
        .WhenFalse("the product is at low risk of theft")
        .Create();
```

### [Policies](xref:Motiv.PolicyBase`2)

`OrElse()` is the one composition that preserves a policy: `policy.OrElse(policy)` returns a
<xref:Motiv.PolicyBase`2>, so an `OrElse` chain behaves like `??` — it yields a single `Value` even
when nothing matched.

When the chain is satisfied, `Value` is the first operand that matched. When nothing matched, every
operand is a genuine cause and `Value` is the **last-evaluated** operand's — the fallback:

```csharp
var left = Spec
    .Build((object _) => false)
    .WhenTrue("left-true")
    .WhenFalse("left-false")
    .Create("left");

var right = Spec
    .Build((object _) => false)
    .WhenTrue("right-true")
    .WhenFalse("right-false")
    .Create("right");

var result = left.OrElse(right).Evaluate(new object());

result.Satisfied; // false
result.Value;     // "right-false"                    <- the selection: last evaluated
result.Values;    // ["left-false", "right-false"]    <- every contributing cause
```

`Value` is therefore a *selection*, not a guarantee that only one cause exists. Use `Values` to reach
everything it was selected from. `Values` flattens a nested chain — three policies combined with
`OrElseTogether()` yield three values, not the root node's two — reports only causal values, and
works for any metadata type.

Note that `Causes` and `Underlying` describe the **binary composition shape** rather than the
flattened causal set, so a three-policy chain reports two operands at its root.

### [Boolean Results](xref:Motiv.BooleanResultBase`1)

You can perform a conditional OR operation on two <xref:Motiv.BooleanResultBase`1> in two ways:

* `left || right`
* `left.OrElse(right)`

These are not equivalent. `left || right` returns the left result unwrapped when it is satisfied, so it
produces either the bare left result or an eager `OrBooleanResult` (whose `Reason` uses `|`); `.OrElse()`
always produces an `OrElseBooleanResult` (whose `Reason` uses `||`). Prefer `.OrElse()` when the shape of
the justification tree or the policy-ness of the result matters.

This allows you to combine into a single result the evaluations of different model types (by different propositions).

```csharp
record Store(decimal ShopLiftingRatePercentage);
var store = new Store(5);

var isAtRiskLocationSpec = Spec
    .Build((Store store) => store.ShopLiftingRatePercentage > 3)
    .WhenTrue("the store has high incidents of shop lifting")
    .WhenFalse("the store has low incidents of shop lifting")
    .Create();

var isAtRiskLocation = isAtRiskLocationSpec.Evaluate(store);
var isProductAtRiskOfTheft = isProductAtRiskOfTheftSpec.Evaluate(store);

var isExtraSecurityNeeded = isProductAtRiskOfTheft || isAtRiskLocation;

isExtraSecurityNeeded.Satisfied; // true
isExtraSecurityNeeded.Reason; // "the product is at risk of theft | the store has high incidents of shop lifting"
isExtraSecurityNeeded.Assertions; // ["the product is at risk of theft", "the store has high incidents of shop lifting"]
```