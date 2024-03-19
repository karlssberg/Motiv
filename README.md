# Motiv

### Turn your if-statements into _why did it do that_ statements

Motiv is a .NET library lets you decompose your logical expressions into _logical propositions_.  By propositions we 
mean a logical statement that can be either `true` or `false`, such as _sun is shining_ or _is even number_. However,
this library goes a step further by allowing you to associate metadata with the proposition for when it is either 
`true` or `false` and have it returned when resolved.  The power from this library comes from the ease with which 
you can combine and reuse these propositions, and without compromising on visibility of the logic at either 
design-time or run-time.


#### What can I use the metadata for?
It's primary purpose is to provide human readable strings that explain exactly why a particular logical expression 
resolved to a either `true` or `false` .  This can be useful in a number of scenarios, such as:
* __User feedback__ - You require your application to provide detailed and accurate feedback to the user about why a 
  certain decisions were  made. 
* __Debugging__ - Quickly understand why a certain condition was met, or not. This can be especially useful in complex 
  logical expressions where it might not be immediately clear which part of the expression was responsible for the final 
  result.
* __Multi-language Support__ - The metadata doesn't have to be a string. It can be any type, which means you could use
  it to support multi-lingual explanations.
* Rules Engine - The metadata can be used to conditionally select stateful objects, which can be used to implement a 
  rules engine. This can be useful in scenarios where you need to apply different rules to different objects based on 
  their state.
* Validation - The metadata can be used to provide human-readable explanations of why a certain validation rule was 
  not met. This can be useful in scenarios where you need to provide feedback to the user about why a certain input 
  was not valid.

### Usage

The following example is a basic demonstration of how to use Motiv. It shows how to create a basic specification and
then use it to determine if a number (3 in this case) is negative or not.

#### Basic specification
A basic specification can be created using the `Spec` class. This class provides a fluent API for creating a 
logical proposition

```csharp
var isNegativeSpec = Spec
        .Build<int>(n => n < 0)
        .Create("is negative");

var isNegative = isNegativeSpec.IsSatisfiedBy(3);

isNegative.Satisfied; // returns false
isNegative.Reason; // returns "!is negative"
```

you can also use the `WhenTrue` and `WhenFalse` methods to provide a more human-readable description for when the outcome is either `true` or `false`. 

```csharp
var isNegativeSpec = Spec
        .Build<int>(n => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse("the number is not negative")
        .Create();

var isNegative = isNegativeSpec.IsSatisfiedBy(3);

isNegative.Satisfied; // returns false
isNegative.Reason; // returns "the number is not negative"
```

You are also not limited to string.  You can equally supply any POCO object and it will be yielded when appropriate.
```csharp
var isNegativeSpec = Spec
        .Build<int>(n => n < 0)
        .WhenTrue(new MyClass { Message = "the number is negative" })
        .WhenFalse(new MyClass { Message = "the number is not negative" }
        .Create("is negative")

var isNegative = isNegativeSpec.IsSatisfiedBy(3);

isNegative.Satisfied; // returns false
isNegative.Reason; // returns instance of Myclass with the Message property set to"the number is not negative"
````

#### Combining specifications
```csharp
var isNegativeSpec = Spec
        .Build<int>(n => n < 0)
        .WhenTrue("the number is negative")
        .WhenFalse(n => n switch 
        {
                0 => "the number is zero"
                _ => "the number is positive"
        })
        .Create();

var isEvenSpec = Spec
        .Build<int>(n => n % 2 == 0)
        .WhenTrue(n => n switch 
        {
                0 => "the number is zero",
                _ => "the number is even"
        })
        .WhenFalse("the number is odd")
        .Create(); 

var isPositiveAndOddSpec = !isNegativeSpec & !isEvenSpec;

var isPositiveAndOdd = isPositiveAndOddSpec.IsSatisfiedBy(3);

isPositiveAndOdd.IsSatisfied; // returns false
isPositiveAndOdd.Reason; // returns "the number is not negative & the number is odd"
isPositiveAndOdd.Assertions; // returns ["the number is not negative", "the number is odd"]
```

### Problem Statement

This library deals with vexing issues from working with logic. Such as...

- **Not knowing why your application did that** After releasing an application and getting feedback from users it can be
  difficult trying figure out the specific reasons _why_ an unexpected decision was arrived at, especially when there
  are numerous parameters involved. The more complex the overall logical expression, the more error-prone the solution
  is to supplement it with metadata/additional-functionality in order to answer this question.
- **Unreadable blob of Logic**  When faced with the _logical expression from hell_ it can be challenging to understand
  what bits of the logic played a pivotal role in producing the final result. Sure you can inspect the values but
  this is onerous, error-prone and slows you down.
- **Blackbox Logic** If you have gone down the laudable path of decomposing your logic into bite-sized chunks then you
  are faced with a new conundrum, which is comprehending what your logic is actually doing when revisiting it. Logic can
  be just as easily decomposed as easily as it can be composed, and this can lead to _gotchas_ in your logic that are
  hard to stumble upon. This exacerbates the first problem _Not knowing why your application did that_.

### Solution

Motiv addresses these challenges by extending the [Specification Pattern](https://en.wikipedia.
org/wiki/Specification_pattern) so it can embed metadata along with logical statements. By following the same rules
that govern traditional logical operators, the metadata is filtered and aggregated with metadata from adjacent
logcal statements to form a list of metadata representing the underlying causes. You can think of it as a library
that helps you supplement validation-like metadata to your regular/vanilla if-statements.

### Benefits

1. **Decomposing Logic**: In any non-trivial application there is a high chance that you will find a need to re-use
   logic in various places. This often means wrapping it in a function and moving it somewhere else. Motiv provides a
   framework for doing this and and the means to recombine them afterwards.
2. **Metadata association**: Associate metadata for both `true` and `false` outcomes. By default the metadata is a
   string - so that human readable explanations of the logic can be defined alongside the actual logical expression.
   However, this doesn't have to be a string and can in fact be any type, which means that it can be used to support
   multi-lingual explanations, or even be used to conditionally select stateful objects.
3. **Metadata accumulation**: With complex logical expressions different underlying logic may (or may not) be
   responsible producing the final result. This means that in order to be useful, the metadata needs to be selectively
   filtered so that only the metadata from logic that contributed to the final result is accumulated, or to be more
   technical: only the metadata from _determinative operands_ are accumulated. For instance, with an _or_ operation, if
   one of the operands produces a `false` result and the other a _true_ result then only the operand that returned
   a `true` result will have its metadata accumulated and the other operand's metadata will be ignored.
4. **Enhanced Debugging Experience**: This library has been designed to ease the developer experience around 
   important and/or complex Boolean logic.  Specifications, whether composed of other Specifications or not, override the `ToString()` method so that it provides
   a human readable representation of its the logic tree. Furthermore, the generated result also accumulates a
   human-readable list of assertions why the result was either `true` or `false`. This is primarily for debugging and
   troubleshooting purposes, but it could also be surfaced to users if so desired.
5. **Simplified Testing**: By extracting your logical expressions into separate classes you make it much easier to
   thoroughly test all the possible combinations that the parameters can be in. It also means the type from which the
   expressions were extracted now has potentially mock-able dependencies, which should make testing code-paths simpler.

### Tradeoffs
1. **Performance**: This library is designed to be as performant as possible, but it is still a layer of abstraction
   over the top of your logic. This means that there is a measurable performance cost to using it. However, 
   this cost is negligible in most cases and are eclipsed by the benefits it provides.
2. **Dependency**: This library is a dependency that you will have to manage,although it is as unobtrusive as possible 
   and should not interfere with your other dependencies.
3. **Learning Curve**: This library is a new concept and will nonetheless require some familiarization. That being said,
   it has been deliberately designed to be as intuitive and easy to use as possible - there is very little to learn, 
   but a lot that can be expressed.

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

**Branching Strategy**:

Main Branches

    main: This is the primary branch of the repository. It should always be stable and deployable. All development 
branches are created from main, and features are merged back into it once they are complete and tested.

    develop: This branch serves as an integration branch for features. Once a feature is complete, it is merged into 
develop. When develop is stable and ready for a release, its contents are merged into main.

Supporting Branches

    Feature Branches (feature/):
        Created from: develop
        Merged back into: develop
        Naming convention: feature/ followed by a descriptive name (e.g., feature/add-login)
        Purpose: Used for developing new features. Each feature should have its own branch.

    Release Branches (release/):
        Created from: develop
        Merged back into: main and develop
        Naming convention: release/ followed by the version number (e.g., release/v1.0.0)
        Purpose: Used for preparing a new production release. Allows for last-minute dotting of i's and crossing of t's.

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
