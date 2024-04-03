# Motiv
### Turn your if-statements into _why_ statements

Motiv is a .NET library that supercharges your boolean logic.

At its core, it allows you to package your boolean expressions into strongly typed propositions which can then be 
conveniently re-used, re-combined and then queried to determine if they are satisfied by a given model.

You also get, for free, a concise reason for why a proposition was satisfied or not, which is especially useful for 
debugging and/or logging purposes.

You can optionally provide custom objects (referred to here as _metadata_) to propositions so that when models are 
evaluated, only those propositions that actually influenced the final boolean result have their metadata yielded.

The metadata can be any type, but is normally a human-readable `string` that explains why a proposition was 
satisfied, or not (referred to here as an _assertion_), but it could equally be a POCO object that contains 
explanations in different languages, or even a stateful object to drive behavior elsewhere in the application.

#### What can I use the metadata for?
* **User feedback** - You require an application to provide detailed and accurate feedback to the user about why a 
  certain decisions were made. 
* **Debugging** - Quickly understand why a certain condition was met, or not. This can be especially useful in complex 
  logical expressions where it might not be immediately clear which part of the expression was responsible for the final 
  result.
* **Multi-language Support** - The metadata doesn't have to be a string. It can be any type, which means you could use
  it to support multi-lingual explanations.
* **Rules Engine** - The metadata can be used to conditionally select stateful objects, which can be used to implement a 
  rules engine. This can be useful in scenarios where you need to apply different rules to different objects based on 
  their state.
* **Validation** - The metadata can be used to provide human-readable explanations of why a certain validation rule was 
  not met. This can be useful in scenarios where you need to provide feedback to the user about why a certain input 
  was not valid.
* **Parsing CLI arguments** - The command line arguments array can be interrogated and mapped to state 
  objects (metadata) to help conditionally drive behavior in the application.

## Usage

The following example is a basic demonstration of how to use Motiv.
It shows how to create a basic proposition and then use it to determine if a number (3 in this case) is negative or not.

### Basic proposition
A basic proposition can be created using the `Spec` class. This class provides a fluent API for creating a 
logical proposition

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .Create("is negative");

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "is negative"
isNegative.Assertions; // ["is negative"]
```

When negated, a basic proposition will return a reason prefixed with a `!` character.
This is useful for debugging purposes.

```csharp
var isNegative = isNegativeSpec.IsSatisfiedBy(3);

isNegative.Satisfied; // false
isNegative.Reason; // "!is negative"
isNegative.Assertions; // ["!is negative"]
```

You can also use the `WhenTrue()` and `WhenFalse()` methods to provide a more human-readable description for when the 
outcome is either `true` or `false`.
These values will be used in the `Reason` and `Assertions` properties of the result.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create();

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "the number is negative"
isNegative.Assertions; // ["the number is negative"]
```

If for whatever reason it is not convenient to use the strings supplied to the `WhenTrue()` and `WhenFalse()` as a 
`Reason`, you can instead provide a propositional statement to the `Create()` method.
This will then be used in the `Reason` property.  This can be useful if the text supplied to the `WhenTrue()` and 
`WhenFalse()` is particularly verbose, or doesn't make sense as a `Reason`.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create("is negative");

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "is negative"
isNegative.Assertions; // ["the number is negative"]
```

You are also not limited to strings.
You can equally supply any POCO object and it will be yielded when appropriate.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue(new MyClass { Message = "the number is negative" })
        .WhenFalse(new MyClass { Message = "the number is not negative" })
        .Create("is negative")

var isNegative = isNegativeSpec.IsSatisfiedBy(-3);

isNegative.Satisfied; // true
isNegative.Reason; // "is negative"
isNegative.Assertions; // ["is negative"]
isNegative.Metadata; // [{ Message = "the number is negative" }]
````

### Combining propositions
The real power of Motiv comes from combining propositions to form new ones.
The library will take care of collating the underlying causes and filter out irrelevant and inconsequential 
assertions and metadata from the final result.
propositions can be combined using the `&`,`|` and `^` operators as well as the supplemental `.OrElse()` and
`.AndAlso()` methods.

```csharp
var isNegativeSpec =
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse(n => n == 0 
            ? "the number is zero"
            : "the number is positive")
        .Create("is negative");

var isEvenSpec =
    Spec.Build((int n) => n % 2 == 0)
        .WhenTrue("the number is even")
        .WhenFalse("the number is odd")
        .Create("is even"); 

var isPositiveAndOddSpec = !isNegativeSpec & !isEvenSpec;

var isPositiveAndOdd = isPositiveAndOddSpec.IsSatisfiedBy(3);

isPositiveAndOdd.IsSatisfied; // returns true
isPositiveAndOdd.Reason; // "!is negative & !is even"
isPositiveAndOdd.Assertions; // ["the number is positive", "the number is odd"]
```

When combining propositions to form new ones, only the propositions that helped determine the final result 
will be included in the `Assertions` property and `Reason` property.

```csharp
var isPositiveAndOdd = isPositiveAndOddSpec.IsSatisfiedBy(-3);

isPositiveAndOdd.IsSatisfied; // returns false
isPositiveAndOdd.Reason; // "is negative"
isPositiveAndOdd.Assertions; // ["the number is negative"]
```

### Encapsulation and Re-use

#### Redefining propositions
Sometimes an existing propositions do not produce the desired assertions or metadata.
In this case, you will need to wrap the existing proposition in a new one.

```csharp
var underlying = 
    Spec.Build((int n) => n < 0)
        .WhenTrue(new MyClass { Message = "the number is negative" })
        .WhenFalse(new MyClass { Message = "the number is not negative" })
        .Create("is negative (metadata)");

var isNegative =
    Spec.Build(underlying)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create("is negative (explanation)");
```

#### Strongly typed proposition
You will likely want to encapsulate propositions for reuse across an application.
For this you typically have two options, which is to either return `Spec` instances from members of POCO 
objects, or to derive from the `Spec<TModel>` or `Spec<TModel, TMetadata>` class (the former being merely syntactic 
sugar for `Spec<TModel, string>`).
Using these classes will help you to maintain a separation of concerns and also raise the conspicuity of important 
logic within an application. 

```csharp
public class IsNegativeSpec : Spec<int>(
    Spec.Build((int n) => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create();

public class IsNegativeMultiLingualSpec : Spec<int, MyClass>(
    Spec.Build((int n) => n < 0)
        .WhenTrue(new MyClass { Spanish = "el número es negativo" })
        .WhenFalse(new MyClass { Spanish = "el número no es negativo" })
        .Create("is negative");
```

If you require (or prefer) your proposition to be expressed as multiple statements you can define them within a 
factory method.

```csharp
public class IsPositiveAndOddSpec : Spec<int>(() => 
    {
        var isNegativeSpec =
            Spec.Build((int n) => n < 0)
                .Create("is negative");+

        var isEvenSpec =
            Spec.Build((int n) => n % 2 == 0)
                .Create("is even"); 

        return !isNegativeSpec & !isEvenSpec;
    });
```

### Higher Order Logic
To perform logic over collections of models, higher order logical operations are required.
This library provides a `.As()` builder method that allows you to define your own higher order logical operations.
Some built-in higher order logical operations are provided for popular operations, but you can also add your own using
extension methods.

The current built-in higher order logical operations are:
- `AsAllSatisfied()`: Creates a proposition that yields a true boolean-result object if all the models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAnySatisfied()`: Creates a proposition that yields a true boolean-result object if any of the models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsNoneSatisfied()`: Creates a proposition that yields a true boolean-result object if none of the models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsNSatisfied()`: Creates a proposition that yields a true boolean-result object if exactly N models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAtLeastNSatisfied()`: Creates a proposition that yields a true boolean-result object if at least N models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.
- `AsAtMostNSatisfied()`: Creates a proposition that yields a true boolean-result object if at most N models in a 
  collection satisfy the proposition, otherwise a false boolean-result object is returned.

```csharp
Spec.Build((int n) => n < 0)
    .AsAllSatisfied()
    .WhenTrue("all are negative")
    .WhenFalse("some are not negative")
    .Create();
```

You can also use an existing proposition instead of a predicate to create a higher order logical operation.
This will give you access to each result and model pair, which can be used to customize the output to a 
particular use-case.

```csharp
Spec.Build(new IsNegativeSpec<int>())
    .AsAllSatisfied()
    .WhenTrue("all are negative")
    .WhenFalse(evaluation => evaluation.FalseModels.Select(n => $"{n} is not negative"))
    .Create();
```
When dynamically generating assertions/metadata, you are provided with an _evaluation_ object that contains 
pre-defined properties that can be used to customize the output (such as `TrueModels`, `FalseModels`, `TrueCount`, 
`FalseCount` etc.).
This is to facilitate pattern matching using switch expressions, which results in more readable inline conditional 
checks.

```csharp
var allAreNegativeSpec =
    Spec.Build(new IsNegativeSpec<int>())
        .AsAllSatisfied()
        .WhenTrue("all are negative")
        .WhenFalse(evaluation => evaluation switch
        {
            { FalseCount: <= 10 } => evaluation.FalseModels.Select(n => $"{n}  is not negative"),
            _ => $"{evaluation.FalseCount} of {evaluation.Count} are not negative".ToEnumerable()
        })
        .Create();
    
allAreNegativeSpec.IsSatisfiedBy([-2, -1, 0, 1, 2]).Assertions; // ["0 is not negative", "1 is not negative", "2 is not negative"]
```

## Tradeoffs
There are inevitably potential tradeoffs to consider when using this library.
1. **Performance**: This library is designed to be as performant as possible, but it is still a layer of abstraction
   over the top of your logic.
   This means that there is a measurable performance cost to using it.
   However, this cost is negligible in most cases and is generally eclipsed by the benefits it provides.
2. **Dependency**: This library is a dependency that you will have to manage.
   Once embedded in your codebase it will be challenging to remove. 
   However, this library does not itself depend on any third-party libraries, so it does not bring any unexpected 
   baggage with it. 
3. **Learning Curve**: For many, this library is a new approach and will nonetheless require a bit of familiarization.
   That being said, it has been deliberately designed to be as intuitive and easy to use as possible—there is 
   relatively little to learn.

## Getting Started with CLI

This section provides instructions on how to build and run the Motiv project using the .NET Core CLI, which is a
powerful and flexible way to work with .NET projects.

#### Prerequisites

- Ensure you have the [.NET SDK](https://dotnet.microsoft.com/download) installed on your machine.
- Clone the repository to your local machine.

#### Building the Project

1. **Open Terminal or Command Prompt**: Navigate to the directory where you cloned the Motiv repository.
2. **Navigate to the Project Directory**: If the solution file (`.sln`) is not in the root, navigate to the directory
   containing the solution file.
3. **Build the Solution**: Run the following command to build the solution:
   ```bash
   dotnet build
   ```

#### Running Tests

**Run Unit Tests** To execute tests within the solution run the following command:

```bash
dotnet test
```

## Contribution

Your contributions to Motiv are greatly appreciated:

### Branching Strategy

     main
        This is the primary branch of the repository. It should always be stable and deployable. 

     develop
        Merged into: main
        Purpose: This branch serves as an integration branch for features. Once a feature is complete, it is merged 
        into develop.  When develop is stable and ready for a release, its contents are merged into main.

     feature/
        Created from: develop
        Merged back into: develop
        Naming convention: feature/ followed by a descriptive name (e.g., feature/add-login)
        Purpose: Used for developing new features. Each feature should have its own branch.

Workflow Summary

    Start a new feature by creating a feature/ branch off develop.
    Once the feature is complete, create a pull request to merge it back into develop.
    Regularly merge develop into release branches for preparing releases.

Additional Notes

    Delete branches post-merge to keep the repository clean.
    Use pull requests for code review and ensure CI checks pass before merging.
    Regularly update branches with the latest changes from their parent branch to avoid large merge conflicts.

This strategy helps in maintaining a clean and manageable workflow, ensuring stability in the main branch, and
enabling continuous development and quick fixes as needed.

## License

MIT License

Copyright (c) 2023 karlssberg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
