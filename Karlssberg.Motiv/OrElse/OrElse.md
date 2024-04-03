# Conditional OR

A conditional OR (i.e., short-circuited) operation can be performed on two specifications using the
`left.OrElse(right)` method.
It is serialized as `left || right`—incidentally the `||` C# operator cannot be overriden, so we have to resort to 
using method invocation to perform the operation.
This will produce a new specification instance that is the logical OR of the two specifications.
When evaluated, the right specification will only be evaluated if the left specification is not satisfied.

For example:

```csharp
record Product(string Name, decimal Price, Size Size);

var expensiveProductSpec = Spec
    .Build<Product>(p => p.Price > 1000)
    .WhenTrue("product is expensive")
    .WhenFalse("product is not expensive")
    .Create();

var isProductSizeSmallSpec = Spec
    .Build<Product>(p => p.Size == Size.Small)
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

```csharp
var isProductAtRiskOfTheftSpec = 
    Spec.Build(expensiveProductSpec | isProductSizeSmallSpec)
        .WhenTrue("the product is at risk of theft")
        .WhenFalse("the product is at low risk of theft")
        .Create();
```

You can also use the `|` operator on the `BooleanResult<T>`s that are returned from the `IsSatisfiedBy` method. This is
so that you can still aggregate the results of specifications that interrogate different models.

```csharp
record Store(decimal ShopLiftingRatePercentage);
var store = new Store(5);

var isAtRiskLocationSpec = Spec
    .Build<Store>(store => store.ShopLiftingRatePercentage > 3)
    .WhenTrue("the store has high incidents of shop lifting")
    .WhenFalse("the store has low incidents of shop lifting")
    .Create();

var isAtRiskLocation = isAtRiskLocationSpec.IsSatisfiedBy(store);
var isProductAtRiskOfTheft = isProductAtRiskOfTheftSpec.IsSatisfiedBy(store);

var isExtaSecurityNeeded = isProductAtRiskOfTheft | isAtRiskLocation;

isExtaSecurityNeeded.Satisfied; // true
isExtaSecurityNeeded.Reason; // "the product is at risk of theft | the store has high incidents of shop lifting"
isExtaSecurityNeeded.Assertions; // ["the product is at risk of theft", "the store has high incidents of shop lifting"]
```
