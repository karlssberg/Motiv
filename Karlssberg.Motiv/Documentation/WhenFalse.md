# WhenFalse()

## False assertion
### `WhenFalse(string assertion)`
```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")
    .WhenFalse("is odd")
    .Create();
```
This overload generates an assertion statement when the proposition is not satisfied.
When the proposition is not satisfied, the metadata returned by the factory function will be used to populate the 
`Reason`, `Assertions` and `Metadata` properties of the result.

## False metadata
### `WhenFalse(TMetadata metadata)`
```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(new MyMetadata("is odd"))
    .Create("is even");
```
This overload generates a metadata value when the proposition is not satisfied.
When the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata` 
property of the result.

## False assertion derived from model
### `WhenFalse(Func<TModel, string> factory)`
```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue("is even")
    .WhenFalse(n => $"{n} is odd")
    .Create();
```
This overload generates an assertion statement based on the model when the proposition is not satisfied.
When the proposition is not satisfied, the metadata returned by the factory function will be used to populate the 
`Reason`, `Assertions` and `Metadata` properties of the result.

## False metadata derived from model
### `WhenFalse(Func<TModel, TMetadata> factory)`
```csharp
Spec.Build((int n) => n % 2 == 0)
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse(n => new MyMetadata($"{n} is odd"))
    .Create("is even");
```
This overload generates a metadata value based on the model when the proposition is not satisfied.
When the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata` 
property of the result. 

## False assertion derived from model/result
### `WhenFalse(Func<TModel, BooleanResultBase<TMetadata>, string> factory)`
```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue("is even")
    .WhenFalse((n, result) => result.Assertions.Serialize())
    .Create();
```
This overload generates an assertion statement based on the model and the result of the underlying proposition.
When the proposition is not satisfied, the metadata returned by the factory function will be used to populate the 
`Reason`, `Assertions` and `Metadata` properties of the result. 

## False assertion derived from model/result
### `WhenFalse(Func<TModel, BooleanResultBase<TMetadata>, TMetadata> factory)`
```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse((n, result) => new MyMetadata($"{n} {result.Reason}"))
    .Create("is even");
```
This overload generates a metadata value based on the model and the result of the underlying proposition.
When the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata` 
property of the result.

## False assertion collection derived from model/result
### `WhenFalse(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<string>> factory)`
```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue("is even")
    .WhenFalse((n, result) => result.Assertions)
    .Create();
```
This overload generates multiple assertion statements based on the model and the result of the underlying proposition.
When the proposition is not satisfied, the metadata returned by the factory function will be used to populate the 
`Reason`, `Assertions` and `Metadata` properties of the result.

## False metadata collection derived from model/result
### `WhenFalse(Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>> factory)`
```csharp
Spec.Build(new IsEvenProposition())
    .WhenTrue(new MyMetadata("is even"))
    .WhenFalse((n, result) => result.Assertions.Select(assertion => new MyMetadata($"{n} {assertion}")))
    .Create("is even");
```
This overload generates multiple metadata values based on the model and the result of the underlying proposition.
When the proposition is not satisfied, the metadata returned by the factory function will populate the `Metadata` 
property of the result.

<div style="display: flex; justify-content: space-between;">
  <a href="./WhenTrue.md">Back - When True</a>
  <a href="./Create.md">Next - Create</a>
</div>