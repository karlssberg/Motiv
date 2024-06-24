---
title: OrElse()
category: operators
---
# OrElse()

### [Propositions](xref:Motiv.SpecBase`2)

You can perform a conditional OR (i.e., short-circuited) operation on two <xref:Motiv.SpecBase`2> in only one way:

* `left.OrElse(right)`

This is due to quirks regarding the overloading of the `||` operator, only the `OrElse()` method is
available for use with propositions.

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
var isAtRiskShelfItem = isAtRiskShelfItemSpec.IsSatisfiedBy(product);

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

### [Boolean Results](xref:Motiv.BooleanResultBase`1)

You can perform a conditional OR operation on two <xref:Motiv.BooleanResultBase`1> in two ways:

* `left || right`
* `left.OrElse(right)`

This allows you to combine into a single result the evaluations of different model types (by different propositions).

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

var isExtraSecurityNeeded = isProductAtRiskOfTheft || isAtRiskLocation;

isExtraSecurityNeeded.Satisfied; // true
isExtraSecurityNeeded.Reason; // "the product is at risk of theft | the store has high incidents of shop lifting"
isExtraSecurityNeeded.Assertions; // ["the product is at risk of theft", "the store has high incidents of shop lifting"]
```