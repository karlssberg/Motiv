---
title: Or()
category: operators
---
# Or()

### [Propositions](xref:Motiv.SpecBase`2)

You can perform a logical OR operation on two propositions in two ways:

* `left | right`
* `left.Or(right)`

Both approaches produce a new proposition that represents the logical OR of the two input propositions.
When evaluating the resulting proposition, both operands will be evaluated,
regardless of the outcome of the left operand.
In other words, the logical OR operation is not short-circuited.

For example:

```csharp
record Product(string Name, decimal Price, Size Size);

var expensiveProductSpec = Spec
    .Build((Product p) => p.Price > 1000)
    .WhenTrue("product is expensive")
    .WhenFalse("product is not expensive")\\
    .Create();

var isProductSizeSmallSpec = Spec
    .Build((Product p) => p.Size == Size.Small)
    .WhenTrue("product is easily stolen")
    .WhenFalse("product is not easily stolen")
    .Create();

var isAtRiskShelfItemSpec = expensiveProductSpec | isProductSizeSmallSpec;

var isAtRiskShelfItem = isAtRiskShelfItemSpec.IsSatisfiedBy(new Product("Laptop", 1500, Size.Small));

isAtRiskShelfItem.Satisfied; // true
isAtRiskShelfItem.Reason; // "product is expensive | product is easily stolen"
isAtRiskShelfItem.Assertions; // ["product is expensive", "product is easily stolen"]
```

If you want to give it a true or false reasons you can do so by wrapping it in a new specification.

For example:

```csharp
var isProductAtRiskOfTheftSpec = Spec
    .Build(expensiveProductSpec | isProductSizeSmallSpec)
    .WhenTrue("the product is at risk of theft")
    .WhenFalse("the product is at low risk of theft")
    .Create();
```

### [Boolean Results](xref:Motiv.BooleanResultBase`1)

You can perform a logical OR operation on two <xref:Motiv.BooleanResultBase`1> in two ways:

* `left | right`
* `left.Or(right)`

This is so that you can still aggregate the results of specifications that interrogate different models.

```csharp
record Store(decimal ShopLiftingRatePercentage);
var store = new Store(5);

var isAtRiskLocationSpec = Spec
    .Build((Store store) => store.ShopLiftingRatePercentage > 3)
    .WhenTrue("the store has high incidents of shop lifting")
    .WhenFalse("the store has low incidents of shop lifting")
    .Create();

var isAtRiskLocation = isAtRiskLocationSpec.IsSatisfiedBy(store);
var isProductAtRiskOfTheft = isProductAtRiskOfTheftSpec.IsSatisfiedBy(store);

var isExtraSecurityNeeded = isProductAtRiskOfTheft | isAtRiskLocation;

isExtraSecurityNeeded.Satisfied; // true
isExtraSecurityNeeded.Reason; // "the product is at risk of theft | the store has high incidents of shop lifting"
isExtraSecurityNeeded.Assertions; // ["the product is at risk of theft", "the store has high incidents of shop lifting"]
```