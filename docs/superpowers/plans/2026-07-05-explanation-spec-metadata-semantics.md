# Explanation-Spec Metadata Semantics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Named explanation specs (`.WhenTrue(string)`/`.WhenFalse(string)` + `Create("name")`) become observably identical to metadata specs — Reason/Assertions/Justification report `"name == true/false"` and the strings surface only via `Values` — while unnamed specs (parameterless `Create()`) keep strings as explanations, gain an eager `trueBecause` whitespace guard, and fall back to `Description.ToReason()` for degenerate strings.

**Architecture:** Factory-level rerouting. Three semantic roles become three class shapes per family: (1) shared named/metadata classes lose their `string`/`IEnumerable<string>` special-case arms and resolve explanation text as `Description.ToReason(satisfied)` unconditionally; (2) each family gains dedicated unnamed-explanation classes (string-with-fallback rule) used only by parameterless `Create()`; (3) minimal factories that currently share classes with explanation factories are rerouted to dedicated pass-through copies that preserve today's underlying-metadata pass-through. The `HasExplicitStatement` flag loses all readers and is deleted.

**Tech Stack:** C# / .NET multi-target (`net8.0;net9.0;netstandard2.0;net10.0` lib, `net8.0;net9.0;net472;net10.0` tests), xUnit + Shouldly + AutoFixture, Converj source generator (fluent API), solution file `Motiv.slnx`.

**Spec:** `docs/superpowers/specs/2026-07-05-explanation-spec-metadata-semantics-design.md`

## Global Constraints

- Lib code must compile for `netstandard2.0`: no ranges (`[..^n]`), `System.Index`/`System.Range`, or default interface methods. Collection expressions (`[]`, `[x]`) are fine (compile-time feature).
- The Converj generator reads factory struct constructor parameters and `[FluentMethod]`/`[FluentTarget]`/`[MultipleFluentMethods]` attributes. **Never change those.** `Create()` method bodies are opaque to the generator — all rerouting happens there.
- Preserve laziness: assertion/reason resolution stays inside `Lazy<T>`/deferred delegates exactly as today. Never move a lazy call to eager evaluation.
- All new/edited proposition classes are `internal`. Before changing any constructor signature, grep both production and test code for direct construction (`new <ClassName>`).
- Tests use xUnit `[Theory]`/`[InlineAutoData]` (custom `AutoDataAttribute` in test root), Shouldly (`act.ShouldBe(...)`, `act.ShouldBeEquivalentTo(...)`), and `// Arrange` `// Act` `// Assert` comments. Match this style.
- Fast inner-loop test command: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`. Add `--filter "FullyQualifiedName~<TestClassName>"` to scope to one class.
- Commit after every green task. Never commit red.
- Breaking-change release: this lands on a v8.0.0 branch; do not add `[Obsolete]` shims.

## The two transformation rules (referenced by every task)

**Named rule** (applies to every class reachable only via `Create(string statement)` or metadata factories): explanation text = `Description.ToReason(satisfied)`, unconditionally. Metadata/Values unchanged.

**Unnamed rule** (applies to classes reachable only via parameterless `Create()`): explanation text = the resolved because-string; if null/empty/whitespace, fall back to `Description.ToReason(satisfied)` (ExpressionTree family falls back to underlying assertions instead, matching that family's existing metadata fallback). For multi/yield: keep only non-whitespace strings; if none remain, use the fallback.

---

### Task 1: Fallback helper `AssertionFallbackExtensions`

**Files:**
- Create: `src/Motiv/Shared/AssertionFallbackExtensions.cs`
- Test: `src/Motiv.Tests/AssertionFallbackExtensionsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces (used by every later task):
  - `internal static string ElseFallback(this string? assertion, Func<string> fallback)`
  - `internal static IEnumerable<string> ElseFallback(this IEnumerable<string>? assertions, Func<string> fallback)`
  - `internal static IEnumerable<string> ElseFallback(this IEnumerable<string>? assertions, Func<IEnumerable<string>> fallback)`

- [ ] **Step 1: Write the failing test**

```csharp
namespace Motiv.Tests;

public class AssertionFallbackExtensionsTests
{
    [Theory]
    [InlineAutoData("meaningful", "meaningful")]
    [InlineAutoData("", "fallback")]
    [InlineAutoData("   ", "fallback")]
    [InlineAutoData(null, "fallback")]
    public void Should_use_single_assertion_unless_degenerate(string? assertion, string expected)
    {
        // Act
        var act = assertion.ElseFallback(() => "fallback");

        // Assert
        act.ShouldBe(expected);
    }

    [Fact]
    public void Should_filter_degenerate_entries_from_multiple_assertions()
    {
        // Arrange
        IEnumerable<string> assertions = ["good", "", "   ", "also good"];

        // Act
        var act = assertions.ElseFallback(() => "fallback");

        // Assert
        act.ShouldBe(["good", "also good"]);
    }

    [Fact]
    public void Should_fall_back_when_all_multiple_assertions_are_degenerate()
    {
        // Arrange
        IEnumerable<string> assertions = ["", "   "];

        // Act
        var act = assertions.ElseFallback(() => "fallback");

        // Assert
        act.ShouldBe(["fallback"]);
    }

    [Fact]
    public void Should_fall_back_to_collection_when_all_multiple_assertions_are_degenerate()
    {
        // Arrange
        IEnumerable<string> assertions = [" "];

        // Act
        var act = assertions.ElseFallback(() => (IEnumerable<string>)["a", "b"]);

        // Assert
        act.ShouldBe(["a", "b"]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~AssertionFallbackExtensionsTests"`
Expected: FAIL — compile error, `ElseFallback` not defined.

- [ ] **Step 3: Write the implementation**

```csharp
namespace Motiv.Shared;

internal static class AssertionFallbackExtensions
{
    internal static string ElseFallback(this string? assertion, Func<string> fallback) =>
        string.IsNullOrWhiteSpace(assertion) ? fallback() : assertion!;

    internal static IEnumerable<string> ElseFallback(this IEnumerable<string>? assertions, Func<string> fallback)
    {
        var meaningful = FilterMeaningful(assertions);
        return meaningful.Length > 0 ? meaningful : [fallback()];
    }

    internal static IEnumerable<string> ElseFallback(this IEnumerable<string>? assertions, Func<IEnumerable<string>> fallback)
    {
        var meaningful = FilterMeaningful(assertions);
        return meaningful.Length > 0 ? meaningful : fallback();
    }

    private static string[] FilterMeaningful(IEnumerable<string>? assertions) =>
        assertions?.Where(assertion => !string.IsNullOrWhiteSpace(assertion)).ToArray() ?? [];
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~AssertionFallbackExtensionsTests"`
Expected: PASS (all 7 cases).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv/Shared/AssertionFallbackExtensions.cs src/Motiv.Tests/AssertionFallbackExtensionsTests.cs
git commit -m "feat: add assertion fallback helpers for degenerate explanation strings"
```

---

### Task 2: BooleanPredicate family (`Spec.Build(Func<TModel,bool>)`)

**Files:**
- Create: `src/Motiv/BooleanPredicateProposition/ExplanationProposition.cs`
- Create: `src/Motiv/BooleanPredicateProposition/MultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/BooleanPredicateProposition/Proposition.cs:40-45`
- Modify: `src/Motiv/BooleanPredicateProposition/MultiValueProposition.cs:49-53`
- Modify: `src/Motiv/BooleanPredicateProposition/PropositionBuilders/ExplanationWithNamePropositionFactory.cs`
- Modify: `src/Motiv/BooleanPredicateProposition/PropositionBuilders/MultiAssertionExplanationWithNamePropositionFactory.cs`
- Test: `src/Motiv.Tests/BooleanPredicateNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `internal sealed class ExplanationProposition<TModel>(Func<TModel,bool> predicate, Func<TModel,string> trueBecause, Func<TModel,string> falseBecause, ISpecDescription specDescription) : PolicyBase<TModel,string>` and `internal sealed class MultiAssertionExplanationProposition<TModel>(Func<TModel,bool> predicate, Func<TModel,IEnumerable<string>> whenTrue, Func<TModel,IEnumerable<string>> whenFalse, ISpecDescription specDescription) : SpecBase<TModel,string>`.

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Motiv.Tests;

public class BooleanPredicateNamedExplanationSemanticsTests
{
    [Theory]
    [InlineAutoData(true, "is even == true")]
    [InlineAutoData(false, "is even == false")]
    public void Named_explanation_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Named_explanation_spec_keeps_strings_as_metadata(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Values.ShouldBe([expected]);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Unnamed_explanation_spec_keeps_strings_as_assertions(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create();

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
    }

    [Fact]
    public void Unnamed_explanation_spec_falls_back_to_reason_for_degenerate_delegate_output()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalse(_ => "   ")
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["yes == false"]);
    }

    [Fact]
    public void Unnamed_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build((bool b) => b)
            .WhenTrue("   ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Named_create_accepts_whitespace_because_strings()
    {
        // Act
        var act = () => Spec
            .Build((bool b) => b)
            .WhenTrue("   ")
            .WhenFalse("")
            .Create("is even");

        // Assert
        act.ShouldNotThrow();
    }

    [Theory]
    [InlineAutoData(true, "is even == true")]
    [InlineAutoData(false, "is even == false")]
    public void Named_multi_assertion_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalseYield(_ => ["no", "definitely not"])
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_multi_assertion_spec_keeps_yielded_strings_as_assertions()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue("yes")
            .WhenFalseYield(_ => ["no", "definitely not"])
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["no", "definitely not"]);
    }

    [Theory]
    [InlineAutoData(true, "is even == true")]
    [InlineAutoData(false, "is even == false")]
    public void Named_delegate_explanation_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => b)
            .WhenTrue(_ => "yes")
            .WhenFalse(_ => "no")
            .Create("is even");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~BooleanPredicateNamedExplanationSemanticsTests"`
Expected: FAIL — named tests report the strings instead of the suffix; guard test reports no exception.

- [ ] **Step 3: Create `ExplanationProposition<TModel>`**

New file `src/Motiv/BooleanPredicateProposition/ExplanationProposition.cs` — copy of `Proposition.cs` specialized to `string` with the unnamed rule:

```csharp
using System.Threading;
using Motiv.Shared;

namespace Motiv.BooleanPredicateProposition;

/// <summary>
/// An explanation proposition whose statement is derived from the WhenTrue assertion. The because-strings
/// double as the explanation; degenerate (null/empty/whitespace) strings fall back to the statement-derived reason.
/// </summary>
internal sealed class ExplanationProposition<TModel>(
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause,
    ISpecDescription specDescription)
    : PolicyBase<TModel, string>
{
    public override IEnumerable<SpecBase> Underlying => [];

    public override ISpecDescription Description => specDescription;

    public override bool Matches(TModel model) => predicate(model);

    protected override PolicyResultBase<string> EvaluatePolicy(TModel model)
    {
        var isSatisfied = predicate(model);

        var becauseResolver =
            isSatisfied switch
            {
                true => trueBecause,
                false => falseBecause
            };

        var because = new Lazy<string>(() => becauseResolver(model), LazyThreadSafetyMode.None);

        var assertion = new Lazy<string>(() =>
            because.Value.ElseFallback(() => Description.ToReason(isSatisfied)), LazyThreadSafetyMode.None);

        return new PropositionPolicyResult<string>(
            isSatisfied,
            () => because.Value,
            () => new MetadataNode<string>(because.Value),
            () => new Explanation(assertion.Value),
            () => new PropositionResultDescription(assertion.Value, Description.Statement));
    }
}
```

- [ ] **Step 4: Create `MultiAssertionExplanationProposition<TModel>`**

New file `src/Motiv/BooleanPredicateProposition/MultiAssertionExplanationProposition.cs` — copy `MultiValueProposition.cs`, rename the class to `MultiAssertionExplanationProposition<TModel>`, fix `TMetadata` to `string` throughout (`SpecBase<TModel, string>`, `Func<TModel, IEnumerable<string>>` for both whenTrue/whenFalse, `MetadataNode<string>`, result type `string`), and replace its Explanation special-case (the `metadataNode.Value.Metadata switch { IEnumerable<string> because => because, _ => ... }` expression at what is currently `MultiValueProposition.cs:49-53`) with:

```csharp
() => new Explanation(
    metadataNode.Value.Metadata.ElseFallback(() => Description.ToReason(isSatisfied))),
```

Keep everything else in the copied file identical (including the description path that already calls `Description.ToReason`).

- [ ] **Step 5: Apply the named rule to the shared classes**

In `src/Motiv/BooleanPredicateProposition/Proposition.cs`, replace lines 40-45:

```csharp
var assertion = new Lazy<string> (() =>
    metadata.Value switch
    {
        string because => because,
        _ => Description.ToReason(isSatisfied)
    }, LazyThreadSafetyMode.None);
```

with:

```csharp
var assertion = new Lazy<string>(() => Description.ToReason(isSatisfied), LazyThreadSafetyMode.None);
```

In `src/Motiv/BooleanPredicateProposition/MultiValueProposition.cs`, replace the Explanation factory (lines 49-53):

```csharp
() => new Explanation(metadataNode.Value.Metadata switch
{
    IEnumerable<string> because => because,
    _ => Description.ToReason(isSatisfied).ToEnumerable()
}),
```

with:

```csharp
() => new Explanation(Description.ToReason(isSatisfied).ToEnumerable()),
```

- [ ] **Step 6: Reroute the factories and add the guard**

In `ExplanationWithNamePropositionFactory.cs`, replace the `Create()` body:

```csharp
public PolicyBase<TModel, string> Create()
{
    predicate.ThrowIfNull(nameof(predicate));
    trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
    return new ExplanationProposition<TModel>(
        predicate,
        trueBecause.ToFunc<TModel, string>(),
        falseBecause,
        new SpecDescription(trueBecause));
}
```

`Create(string statement)` is unchanged (still constructs `Proposition<TModel, string>`).

In `MultiAssertionExplanationWithNamePropositionFactory.cs`, replace the `Create()` body so it constructs the new multi class and guards:

```csharp
public SpecBase<TModel, string> Create()
{
    predicate.ThrowIfNull(nameof(predicate));
    trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
    return new MultiAssertionExplanationProposition<TModel>(
        predicate,
        trueBecause.ToEnumerable().ToFunc<TModel, IEnumerable<string>>(),
        falseBecause,
        new SpecDescription(trueBecause));
}
```

`Create(string statement)` unchanged.

- [ ] **Step 7: Run the new tests**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~BooleanPredicateNamedExplanationSemanticsTests"`
Expected: PASS.

- [ ] **Step 8: Run the full test project and repair this family's existing expectations**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: failures in tests that assert because-strings from **named** BooleanPredicate explanation specs (and compositions of them). Update each failing expectation per the named rule: expected assertion/reason strings become `"{statement} == true"` / `"{statement} == false"`; expectations on `Values`/metadata stay the strings. Do not touch failures from other families' test files if any appear — they belong to later tasks; if a failure traces to a class not modified in this task, stop and investigate before proceeding. Re-run until green.

- [ ] **Step 9: Commit**

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named BooleanPredicate explanation specs adopt metadata semantics"
```

---

### Task 3: BooleanResultPredicate family — Spec sub-family

**Files:**
- Create: `src/Motiv/BooleanResultPredicateProposition/MinimalBooleanResultPredicateProposition.cs`
- Create: `src/Motiv/BooleanResultPredicateProposition/BooleanResultPredicateMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/BooleanResultPredicateProposition/BooleanResultPredicateProposition.cs:47-59`
- Modify: `src/Motiv/BooleanResultPredicateProposition/BooleanResultPredicateMultiValueProposition.cs:53-58`
- Modify: `src/Motiv/BooleanResultPredicateProposition/BooleanResultPredicateWithSingleAssertionProposition.cs`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PropositionBuilders/Spec/MinimalBooleanResultPredicatePropositionFactory.cs:24`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PropositionBuilders/Spec/NamedPropositionFactory.cs`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PropositionBuilders/Spec/NamedMultiAssertionPropositionFactory.cs`
- Test: `src/Motiv.Tests/BooleanResultPredicateNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `internal sealed class MinimalBooleanResultPredicateProposition<TModel, TMetadata>` (exact ctor of today's `BooleanResultPredicateMultiValueProposition<TModel, TMetadata, TMetadata>`) and `internal sealed class BooleanResultPredicateMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>` (ctor `(Func<TModel, BooleanResultBase<TUnderlyingMetadata>> predicate, Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>> whenTrue, ... whenFalse, ISpecDescription specDescription)`).

- [ ] **Step 1: Write the failing tests**

```csharp
namespace Motiv.Tests;

public class BooleanResultPredicateNamedExplanationSemanticsTests
{
    private static SpecBase<bool, string> Underlying => Spec
        .Build((bool b) => b)
        .Create("underlying");

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Named_explanation_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([model ? "yes" : "no"]);
    }

    [Theory]
    [InlineAutoData(true, "yes")]
    [InlineAutoData(false, "no")]
    public void Unnamed_explanation_spec_keeps_strings_as_assertions(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalse("no")
            .Create();

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_explanation_spec_falls_back_for_degenerate_false_because()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalse((_, _) => "   ")
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["yes == false"]);
    }

    [Fact]
    public void Unnamed_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue(" ")
            .WhenFalse("no")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineAutoData(true, "is accepted == true")]
    [InlineAutoData(false, "is accepted == false")]
    public void Named_multi_assertion_spec_asserts_statement_suffix(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_multi_assertion_spec_keeps_yielded_strings()
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .WhenTrue("yes")
            .WhenFalseYield((_, _) => ["no", "not at all"])
            .Create();

        // Act
        var result = spec.Evaluate(false);

        // Assert
        result.Assertions.ShouldBe(["no", "not at all"]);
    }

    [Theory]
    [InlineAutoData(true, "underlying == true")]
    [InlineAutoData(false, "underlying == false")]
    public void Minimal_spec_still_passes_underlying_assertions_through(bool model, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((bool b) => Underlying.Evaluate(b))
            .Create("is accepted");

        // Act
        var result = spec.Evaluate(model);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }
}
```

Note: the minimal test pins **current** behavior (write it first, run it against unmodified code to capture the exact strings — adjust the expected values to whatever the current output is before making changes, per the capture-first convention).

- [ ] **Step 2: Run tests, capture minimal baseline, verify the rest fail**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~BooleanResultPredicateNamedExplanationSemanticsTests"`
Expected: minimal pass-through test PASSES (baseline pin, fix expectations if the actual strings differ); named/guard tests FAIL.

- [ ] **Step 3: Create the pass-through minimal class and reroute the minimal factory**

Copy `BooleanResultPredicateMultiValueProposition.cs` to `MinimalBooleanResultPredicateProposition.cs`. Rename the class to `MinimalBooleanResultPredicateProposition<TModel, TMetadata>` and collapse its two metadata type parameters into one (`TMetadata` for both value and underlying: base `SpecBase<TModel, TMetadata>`, predicate `Func<TModel, BooleanResultBase<TMetadata>>`, whenTrue/whenFalse `Func<TModel, BooleanResultBase<TMetadata>, IEnumerable<TMetadata>>`). Keep the existing assertion special-case exactly as-is (that is the point of this class):

```csharp
var assertions = new Lazy<string[]>(() =>
    metadata.Value switch
    {
        IEnumerable<string> assertion => assertion.ToArray(),
        _ => [Description.ToReason(booleanResult.Satisfied)]
    }, LazyThreadSafetyMode.None);
```

Then in `MinimalBooleanResultPredicatePropositionFactory.cs:24` change the construction to:

```csharp
return new MinimalBooleanResultPredicateProposition<TModel, TMetadata>(
    predicate,
    (_, result) => result.Values,
    (_, result) => result.Values,
    new SpecDescription(statement) { HasExplicitStatement = true });
```

- [ ] **Step 4: Apply the named rule to the shared classes**

In `BooleanResultPredicateProposition.cs`, replace both lazies (lines 47-59) with a single resolver used in both places it was consumed:

```csharp
var assertion = new Lazy<string>(() =>
    Description.ToReason(booleanResult.Satisfied), LazyThreadSafetyMode.None);
```

Delete the `reason` lazy and pass `assertion.Value` where `reason.Value` was used (the `BooleanResultDescriptionWithUnderlying` argument).

In `BooleanResultPredicateMultiValueProposition.cs`, replace the assertion lazy (lines 53-58) with:

```csharp
var assertions = new Lazy<string[]>(() =>
    [Description.ToReason(booleanResult.Satisfied)], LazyThreadSafetyMode.None);
```

- [ ] **Step 5: Apply the unnamed rule to the single-assertion class**

In `BooleanResultPredicateWithSingleAssertionProposition.cs`, wrap the resolved assertion with the fallback. Find the assertion lazy that switches on `Satisfied` between `trueBecause` and the `whenFalse` delegate, and apply `.ElseFallback(() => Description.ToReason(<satisfied>))` to its result. Example shape (adjust identifiers to the file's actual names):

```csharp
var assertion = new Lazy<string>(() =>
    (booleanResult.Satisfied switch
    {
        true => trueBecause,
        false => whenFalse(model, booleanResult)
    }).ElseFallback(() => Description.ToReason(booleanResult.Satisfied)), LazyThreadSafetyMode.None);
```

Add `using Motiv.Shared;` if missing.

- [ ] **Step 6: Create the unnamed multi class and reroute factories**

Copy `BooleanResultPredicateMultiValueProposition.cs` (its pre-Task state — use the new `MinimalBooleanResultPredicateProposition.cs` as the copy source since it preserves the arm) to `BooleanResultPredicateMultiAssertionExplanationProposition.cs`. Rename class to `BooleanResultPredicateMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>`, fix the value metadata type to `string` (base `SpecBase<TModel, string>`, whenTrue/whenFalse `Func<TModel, BooleanResultBase<TUnderlyingMetadata>, IEnumerable<string>>`), and set the assertion lazy to the unnamed rule:

```csharp
var assertions = new Lazy<string[]>(() =>
    metadata.Value
        .ElseFallback(() => Description.ToReason(booleanResult.Satisfied))
        .ToArray(), LazyThreadSafetyMode.None);
```

Factory edits:
- `NamedPropositionFactory.cs` `Create()`: add `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));` as the first statement. Constructed class unchanged (`BooleanResultPredicateWithSingleAssertionProposition`).
- `NamedMultiAssertionPropositionFactory.cs` `Create()`: add the same guard and construct `BooleanResultPredicateMultiAssertionExplanationProposition<TModel, TMetadata>` with identical arguments as today. `Create(string)` unchanged (shared multi class, now named-rule).

- [ ] **Step 7: Run the new tests**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~BooleanResultPredicateNamedExplanationSemanticsTests"`
Expected: PASS, including the minimal pass-through pin.

- [ ] **Step 8: Full project run, repair this family's expectations, commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0` — update failing named-explanation expectations per the named rule; verify minimal-spec tests did NOT change behavior. Then:

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named BooleanResultPredicate (spec) explanation specs adopt metadata semantics"
```

---

### Task 4: BooleanResultPredicate family — Policy sub-family (includes stray-guard deletion)

**Files:**
- Create: `src/Motiv/BooleanResultPredicateProposition/MinimalPolicyResultPredicateProposition.cs`
- Create: `src/Motiv/BooleanResultPredicateProposition/PolicyResultPredicateMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PolicyResultPredicateProposition.cs:47-59`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PolicyResultPredicateMultiValueProposition.cs:53-59`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PolicyResultPredicateWithSingleAssertionProposition.cs`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PropositionBuilders/Policy/MinimalPolicyResultPredicatePropositionFactory.cs:24`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PropositionBuilders/Policy/ExplanationFromPolicyResultWithNamePropositionFactory.cs`
- Modify: `src/Motiv/BooleanResultPredicateProposition/PropositionBuilders/Policy/NamedMultiAssertionPolicyPropositionFactory.cs`
- Test: `src/Motiv.Tests/PolicyResultPredicateNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `MinimalPolicyResultPredicateProposition<TModel, TMetadata>` (single-value pass-through, ctor of today's `PolicyResultPredicateProposition<TModel, TMetadata, TMetadata>`) and `PolicyResultPredicateMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>`.

- [ ] **Step 1: Write the failing tests**

Mirror Task 3's test file with a policy-result underlying (`Spec.Build((bool b) => b).Create("underlying")` is a policy; use `Underlying.Evaluate(b)` where the builder binds the PolicyResult overloads — i.e. `Spec.Build((bool b) => UnderlyingPolicy.Evaluate(b))` with `PolicyBase<bool, string> UnderlyingPolicy => Spec.Build((bool b) => b).Create("underlying");`). Test class name `PolicyResultPredicateNamedExplanationSemanticsTests`. Cover: named single suffix + Values, unnamed single strings, unnamed degenerate fallback, unnamed guard throws, **named `Create(string)` accepts whitespace `trueBecause` (pins the stray-guard deletion)**:

```csharp
[Fact]
public void Named_create_accepts_whitespace_true_because()
{
    // Act
    var act = () => Spec
        .Build((bool b) => UnderlyingPolicy.Evaluate(b))
        .WhenTrue(" ")
        .WhenFalse("no")
        .Create("is accepted");

    // Assert
    act.ShouldNotThrow();
}
```

plus named/unnamed multi-assertion cases and the minimal pass-through pin (capture-first, as in Task 3).

- [ ] **Step 2: Run tests to verify expected failures**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~PolicyResultPredicateNamedExplanationSemanticsTests"`
Expected: named-semantics tests FAIL; `Named_create_accepts_whitespace_true_because` FAILS (currently throws — the stray guard); minimal pin passes.

- [ ] **Step 3: Create the pass-through minimal class and reroute**

Copy `PolicyResultPredicateProposition.cs` to `MinimalPolicyResultPredicateProposition.cs`; rename class to `MinimalPolicyResultPredicateProposition<TModel, TMetadata>`, collapse to one metadata type parameter, keep its current two lazies exactly as-is (assertion string-arm intact; the `reason` lazy's `!Description.HasExplicitStatement` guard means minimal reasons stay `ToReason` — unchanged behavior). Reroute `MinimalPolicyResultPredicatePropositionFactory.cs:24` to construct it (same args as today).

- [ ] **Step 4: Apply named/unnamed rules and reroute factories**

Exactly parallel to Task 3 steps 4-6, on the Policy twins:
- `PolicyResultPredicateProposition.cs`: collapse the two lazies to the single `Description.ToReason(policyResult.Satisfied)` resolver.
- `PolicyResultPredicateMultiValueProposition.cs`: assertion lazy becomes `[Description.ToReason(policyResult.Satisfied)]` (delete both the `ICollection<string>` and `IEnumerable<string>` arms).
- `PolicyResultPredicateWithSingleAssertionProposition.cs`: apply `.ElseFallback(() => Description.ToReason(...))` to the resolved because-string.
- `PolicyResultPredicateMultiAssertionExplanationProposition.cs`: new class copied from the pre-change multi class, value metadata fixed to `string`, unnamed rule via `ElseFallback` (mirror Task 3 Step 6 code).
- `ExplanationFromPolicyResultWithNamePropositionFactory.cs`: in `Create()` add `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));`; in `Create(string)` **delete line 43** (`trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));`).
- `NamedMultiAssertionPolicyPropositionFactory.cs`: `Create()` adds the guard and constructs the new multi explanation class (same args); `Create(string)` unchanged.

- [ ] **Step 5: Run new tests, then full project, repair, commit**

Run scoped filter, then `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`; repair per the named rule.

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named PolicyResultPredicate explanation specs adopt metadata semantics; drop stray trueBecause guard"
```

---

### Task 5: Decorator family (`Spec.Build(spec)` / `Spec.Build(policy)`)

**Files:**
- Create: `src/Motiv/DecoratorProposition/SpecDecoratorMultiAssertionExplanationProposition.cs`
- Create: `src/Motiv/DecoratorProposition/PolicyDecoratorMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/DecoratorProposition/SpecDecoratorProposition.cs:35-40`
- Modify: `src/Motiv/DecoratorProposition/PolicyDecoratorProposition.cs:35-40`
- Modify: `src/Motiv/DecoratorProposition/SpecDecoratorMultiMetadataProposition.cs:33-38`
- Modify: `src/Motiv/DecoratorProposition/PolicyDecoratorMultiMetadataProposition.cs:33-38`
- Modify: `src/Motiv/DecoratorProposition/SpecDecoratorWithSingleTrueAssertionProposition.cs` (ctor + fallback)
- Modify: `src/Motiv/DecoratorProposition/PolicyDecoratorWithSingleTrueAssertionProposition.cs` (ctor + fallback)
- Modify: `src/Motiv/DecoratorProposition/PropositionBuilders/Spec/NamedPolicyPropositionFactory.cs`
- Modify: `src/Motiv/DecoratorProposition/PropositionBuilders/Policy/NamedSpecPropositionFactory.cs`
- Modify: `src/Motiv/DecoratorProposition/PropositionBuilders/Spec/MultiAssertionSpecExplanationWithNamePropositionFactory.cs`
- Modify: `src/Motiv/DecoratorProposition/PropositionBuilders/Policy/MultiAssertionPolicyExplanationWithNamePropositionFactory.cs`
- Test: `src/Motiv.Tests/DecoratorNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `SpecDecoratorMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>`, `PolicyDecoratorMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>`. Changes `SpecDecoratorWithSingleTrueAssertionProposition`/`PolicyDecoratorWithSingleTrueAssertionProposition` ctor: `string? propositionalStatement = null` param is **removed**, replaced by required `ISpecDescription description` (grep `new SpecDecoratorWithSingleTrueAssertionProposition` / `new PolicyDecoratorWithSingleTrueAssertionProposition` across src before editing; expected call sites: the two Named*PropositionFactory files only).

- [ ] **Step 1: Write the failing tests**

Same shape as Task 2's test file, but built over an underlying spec/policy. Cover, for BOTH `Spec.Build(spec)` (spec decorator) and `Spec.Build(policy)` (policy decorator — build over a `PolicyBase`):
named single suffix + Values retained, unnamed single strings, unnamed degenerate `WhenFalse((_, _) => "")` fallback to `"{trueBecause} == false"`, unnamed guard throws on whitespace `WhenTrue`, named multi suffix, unnamed multi yielded strings. Test class `DecoratorNamedExplanationSemanticsTests`, underlying:

```csharp
private static SpecBase<bool, string> UnderlyingSpec => Spec.Build((bool b) => b).Create("underlying");
private static PolicyBase<bool, string> UnderlyingPolicy => Spec.Build((bool b) => b).Create("underlying");
```

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~DecoratorNamedExplanationSemanticsTests"`
Expected: named tests FAIL (strings reported), guard tests FAIL.

- [ ] **Step 3: Apply named rule to the four shared classes**

- `SpecDecoratorProposition.cs` and `PolicyDecoratorProposition.cs`: replace the assertion lazy (lines 35-40 in each):

```csharp
var assertion = new Lazy<string>(() =>
    Description.ToReason(booleanResult.Satisfied), LazyThreadSafetyMode.None);
```

- `SpecDecoratorMultiMetadataProposition.cs` and `PolicyDecoratorMultiMetadataProposition.cs`: replace the assertions lazy (lines 33-38 in each):

```csharp
var assertions = new Lazy<string[]>(() =>
    [Description.ToReason(booleanResult.Satisfied)], LazyThreadSafetyMode.None);
```

(Use the actual local result variable name in each file.)

- [ ] **Step 4: Convert the WithSingleTrueAssertion classes to unnamed-only**

In `SpecDecoratorWithSingleTrueAssertionProposition.cs`:
1. Change the ctor parameter `string? propositionalStatement = null` to `ISpecDescription description`, delete the internal `SpecDescription` construction from `propositionalStatement ?? trueBecause`, and use the passed `description` as `Description`.
2. Apply `.ElseFallback(() => Description.ToReason(<satisfied>))` to the resolved assertion (same pattern as Task 3 Step 5).

Mirror both edits in `PolicyDecoratorWithSingleTrueAssertionProposition.cs`.

- [ ] **Step 5: Reroute the four factories**

`NamedPolicyPropositionFactory.cs` (spec decorator, raw `_trueBecause`):

```csharp
public PolicyBase<TModel, TMetadata> Create()
{
    _trueBecause.ThrowIfNullOrWhitespace(nameof(_trueBecause));
    return new SpecDecoratorWithSingleTrueAssertionProposition<TModel, TMetadata>(
        _spec,
        _trueBecause,
        _falseBecause,
        new SpecDescription(_trueBecause, _spec.Description));
}

public PolicyBase<TModel, TMetadata> Create(string statement)
{
    return new SpecDecoratorProposition<TModel, string, TMetadata>(
        _spec,
        _trueBecause.ToFunc<TModel, BooleanResultBase<TMetadata>, string>(),
        _falseBecause,
        new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement)), _spec.Description) { HasExplicitStatement = true });
}
```

Note the return type of `Create(string)`: `SpecDecoratorProposition` derives from `PolicyBase`, so the signature is unchanged. If the generic argument order of `SpecDecoratorProposition<TModel, TReplacementMetadata, TUnderlyingMetadata>` differs from shown, match the class declaration.

`NamedSpecPropositionFactory.cs` (policy decorator): same shape, constructing `PolicyDecoratorWithSingleTrueAssertionProposition<TModel, TMetadata>` in `Create()` (with `new SpecDescription(trueBecause, policy.Description)`) and `PolicyDecoratorProposition<TModel, string, TMetadata>` in `Create(string)`.

`MultiAssertionSpecExplanationWithNamePropositionFactory.cs` `Create()`: add `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));`, construct `SpecDecoratorMultiAssertionExplanationProposition<TModel, TMetadata>` with today's args. `Create(string)` unchanged.

`MultiAssertionPolicyExplanationWithNamePropositionFactory.cs`: same with `PolicyDecoratorMultiAssertionExplanationProposition<TModel, TMetadata>`.

- [ ] **Step 6: Create the two unnamed multi classes**

Copy `SpecDecoratorMultiMetadataProposition.cs` (pre-change content — restore the arm in the copy) to `SpecDecoratorMultiAssertionExplanationProposition.cs`; rename, fix value metadata to `string`, assertion lazy:

```csharp
var assertions = new Lazy<string[]>(() =>
    metadata.Value
        .ElseFallback(() => Description.ToReason(booleanResult.Satisfied))
        .ToArray(), LazyThreadSafetyMode.None);
```

Mirror for `PolicyDecoratorMultiAssertionExplanationProposition.cs`.

- [ ] **Step 7: Run scoped tests, then full project, repair, commit**

Scoped filter first, then `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`; repair named-decorator expectations per the named rule (this will touch many composition tests — `Spec.Build(spec).WhenTrue("...").Create("name")` is a very common pattern).

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named decorator explanation specs adopt metadata semantics"
```

---

### Task 6: HigherOrder — BooleanPredicate sub-family

**Files:**
- Create: `src/Motiv/HigherOrderProposition/BooleanPredicate/HigherOrderFromBooleanPredicateExplanationProposition.cs`
- Create: `src/Motiv/HigherOrderProposition/BooleanPredicate/HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/HigherOrderProposition/BooleanPredicate/HigherOrderFromBooleanPredicateProposition.cs:37-49`
- Modify: `src/Motiv/HigherOrderProposition/BooleanPredicate/HigherOrderFromBooleanPredicateMultiMetadataProposition.cs:36-41`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/BooleanPredicate/ExplanationFromBooleanPredicateWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/BooleanPredicate/MultiAssertionExplanationWithNameHigherOrderPropositionFactory.cs`
- Test: `src/Motiv.Tests/HigherOrderProposition/HigherOrderNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `HigherOrderFromBooleanPredicateExplanationProposition<TModel>` and `HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition<TModel>` — ctors identical to their shared counterparts with metadata fixed to `string` / `IEnumerable<string>`.

- [ ] **Step 1: Write the failing tests** (this file also hosts Tasks 7-8 test additions)

```csharp
namespace Motiv.Tests.HigherOrderProposition;

public class HigherOrderNamedExplanationSemanticsTests
{
    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all even == true")]
    [InlineAutoData(new[] { 1, 4 }, "all even == false")]
    public void Named_higher_order_explanation_spec_asserts_statement_suffix(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create("all even");

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
        result.Reason.ShouldBe(expected);
        result.Values.ShouldBe([models.All(n => n % 2 == 0) ? "all are even" : "some are odd"]);
    }

    [Theory]
    [InlineAutoData(new[] { 2, 4 }, "all are even")]
    [InlineAutoData(new[] { 1, 4 }, "some are odd")]
    public void Unnamed_higher_order_explanation_spec_keeps_strings(int[] models, string expected)
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse("some are odd")
            .Create();

        // Act
        var result = spec.Evaluate(models);

        // Assert
        result.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Unnamed_higher_order_create_throws_on_whitespace_true_because()
    {
        // Act
        var act = () => Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("  ")
            .WhenFalse("some are odd")
            .Create();

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Unnamed_higher_order_falls_back_for_degenerate_delegate_output()
    {
        // Arrange
        var spec = Spec
            .Build((int n) => n % 2 == 0)
            .AsAllSatisfied()
            .WhenTrue("all are even")
            .WhenFalse(_ => "")
            .Create();

        // Act
        var result = spec.Evaluate([1]);

        // Assert
        result.Assertions.ShouldBe(["all are even == false"]);
    }
}
```

Add the multi (`WhenFalseYield`) named/unnamed pair analogous to Task 2.

- [ ] **Step 2: Run to verify failures**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~HigherOrderNamedExplanationSemanticsTests"`
Expected: named + guard + fallback tests FAIL; unnamed string test passes (current behavior).

- [ ] **Step 3: Create the two unnamed classes**

Copy `HigherOrderFromBooleanPredicateProposition.cs` to `HigherOrderFromBooleanPredicateExplanationProposition.cs`; rename class, fix `TMetadata` to `string`, and replace BOTH lazies (assertion lines 37-42, reason lines 44-49) with one:

```csharp
var assertion = new Lazy<string>(() =>
    metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);
```

Use `assertion.Value` for both the `Explanation` and the `BooleanResultDescription` arguments.

Copy `HigherOrderFromBooleanPredicateMultiMetadataProposition.cs` to `HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition.cs`; rename, fix metadata to `string`, replace the assertion resolution (lines 36-41) with:

```csharp
var lazyAssertion = new Lazy<IEnumerable<string>>(() =>
    metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);
```

- [ ] **Step 4: Apply named rule to the two shared classes**

`HigherOrderFromBooleanPredicateProposition.cs`: replace both lazies (37-49) with:

```csharp
var assertion = new Lazy<string>(() =>
    specDescription.ToReason(isSatisfied), LazyThreadSafetyMode.None);
```

and use `assertion.Value` in both result arguments (delete `reason`). Note: the minimal higher-order factory passes `_ => statement.AsSatisfied()` funcs whose output text equals `ToReason` output, so minimal behavior is unchanged — the existing minimal higher-order tests pin this.

`HigherOrderFromBooleanPredicateMultiMetadataProposition.cs`: replace the resolution (36-41) with:

```csharp
var lazyAssertion = new Lazy<IEnumerable<string>>(() =>
    specDescription.ToReason(isSatisfied).ToEnumerable(), LazyThreadSafetyMode.None);
```

- [ ] **Step 5: Reroute the two factories**

`ExplanationFromBooleanPredicateWithNameHigherOrderPropositionFactory.cs` `Create()`: add `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));` after the predicate guard and construct `HigherOrderFromBooleanPredicateExplanationProposition<TModel>` (same args, drop the generic `string` argument since the class fixes it). `Create(string)` unchanged.

`MultiAssertionExplanationWithNameHigherOrderPropositionFactory.cs` `Create()`: add `resultResolver.ThrowIfNull(nameof(resultResolver));` (parity with its `Create(string)`) and `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));`, construct `HigherOrderFromBooleanPredicateMultiAssertionExplanationProposition<TModel>`. `Create(string)` unchanged.

- [ ] **Step 6: Run scoped, then full, repair, commit**

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named higher-order boolean-predicate explanation specs adopt metadata semantics"
```

---

### Task 7: HigherOrder — BooleanResult + Spec sub-families

**Files:**
- Create: `src/Motiv/HigherOrderProposition/BooleanResultPredicate/HigherOrderFromBooleanResultExplanationProposition.cs`
- Create: `src/Motiv/HigherOrderProposition/BooleanResultPredicate/HigherOrderFromBooleanResultMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/HigherOrderProposition/BooleanResultPredicate/HigherOrderFromBooleanResultProposition.cs:39-49`
- Modify: `src/Motiv/HigherOrderProposition/BooleanResultPredicate/HigherOrderFromBooleanResultMultiMetadataProposition.cs:40-44`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/BooleanResultPredicate/ExplanationFromBooleanResultWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/BooleanResultPredicate/MultiAssertionExplanationFromBooleanResultWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/Spec/ExplanationFromSpecWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/Spec/MultiAssertionFromSpecWithNameHigherOrderPropositionFactory.cs`
- Test: extend `src/Motiv.Tests/HigherOrderProposition/HigherOrderNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `HigherOrderFromBooleanResultExplanationProposition<TModel, TUnderlyingMetadata>`, `HigherOrderFromBooleanResultMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>` — ctors identical to shared counterparts with value metadata fixed to `string`.

- [ ] **Step 1: Add failing tests** — same named/unnamed/guard/fallback quartet as Task 6 but building over an underlying spec: `Spec.Build(UnderlyingSpec).AsAllSatisfied().WhenTrue("...").WhenFalse("...").Create("all even")` where `UnderlyingSpec => Spec.Build((int n) => n % 2 == 0).Create("is even")`. Named suffix, Values retained, unnamed strings, unnamed guard throw, unnamed degenerate fallback, multi variants.

- [ ] **Step 2: Run to verify failures** (scoped filter as before).

- [ ] **Step 3: Create the two unnamed classes**

Copy `HigherOrderFromBooleanResultProposition.cs` → `HigherOrderFromBooleanResultExplanationProposition.cs`: rename, fix value metadata to `string`, replace the assertion lazy (39-44) and reason ternary (46-49) with a single:

```csharp
var assertion = new Lazy<string>(() =>
    metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied)), LazyThreadSafetyMode.None);
```

used for both Explanation and description arguments.

Copy `HigherOrderFromBooleanResultMultiMetadataProposition.cs` → `HigherOrderFromBooleanResultMultiAssertionExplanationProposition.cs`: rename, fix to `string`, replace the inline `IEnumerable<string> reasons => reasons / _ => ToReason(...).ToEnumerable()` switch (40-44) with:

```csharp
metadata.Value.ElseFallback(() => specDescription.ToReason(isSatisfied))
```

- [ ] **Step 4: Apply named rule to the two shared classes**

`HigherOrderFromBooleanResultProposition.cs`: assertion lazy (39-44) becomes `specDescription.ToReason(isSatisfied)`; delete the reason ternary (46-49) and use `assertion.Value` everywhere `reason.Value` was used.

`HigherOrderFromBooleanResultMultiMetadataProposition.cs`: the inline switch (40-44) becomes `specDescription.ToReason(isSatisfied).ToEnumerable()`.

(`MinimalHigherOrderFromBooleanResultProposition` is a separate class — do NOT touch it.)

- [ ] **Step 5: Reroute the four factories**

Each `Create()` gains `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));` and constructs the matching new unnamed class with identical arguments:
- `ExplanationFromBooleanResultWithNameHigherOrderPropositionFactory` → `HigherOrderFromBooleanResultExplanationProposition<TModel, TMetadata>`
- `MultiAssertionExplanationFromBooleanResultWithNameHigherOrderPropositionFactory` → `HigherOrderFromBooleanResultMultiAssertionExplanationProposition<TModel, TMetadata>`
- `ExplanationFromSpecWithNameHigherOrderPropositionFactory` → `HigherOrderFromBooleanResultExplanationProposition<TModel, TMetadata>` (keeps `spec.Evaluate` + `new SpecDescription(trueBecause, spec.Description)`)
- `MultiAssertionFromSpecWithNameHigherOrderPropositionFactory` → `HigherOrderFromBooleanResultMultiAssertionExplanationProposition<TModel, TMetadata>`

All `Create(string)` bodies unchanged.

- [ ] **Step 6: Run scoped, then full, repair, commit**

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named higher-order boolean-result explanation specs adopt metadata semantics"
```

---

### Task 8: HigherOrder — PolicyResult + Policy sub-families

**Files:**
- Create: `src/Motiv/HigherOrderProposition/PolicyResultPredicate/HigherOrderFromPolicyResultExplanationProposition.cs`
- Create: `src/Motiv/HigherOrderProposition/PolicyResultPredicate/HigherOrderFromPolicyResultMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/HigherOrderProposition/PolicyResultPredicate/HigherOrderFromPolicyResultMetadataProposition.cs:38-50`
- Modify: `src/Motiv/HigherOrderProposition/PolicyResultPredicate/HigherOrderFromPolicyResultMultiMetadataProposition.cs:40-44`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/Policy/ExplanationFromPolicyWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/Policy/MultiAssertionFromPolicyWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/PolicyResultPredicateWithName/ExplanationFromPolicyResultWithNameHigherOrderPropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/PolicyResultPredicateWithName/MultiAssertionExplanationFromPolicyResultWithNameHigherOrderPropositionFactory.cs`
- Test: extend `src/Motiv.Tests/HigherOrderProposition/HigherOrderNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1).
- Produces: `HigherOrderFromPolicyResultExplanationProposition<TModel, TUnderlyingMetadata>`, `HigherOrderFromPolicyResultMultiAssertionExplanationProposition<TModel, TUnderlyingMetadata>`.

- [ ] **Step 1: Add failing tests** — quartet over an underlying policy: `Spec.Build(UnderlyingPolicy).AsAllSatisfied()...` with `PolicyBase<int, string> UnderlyingPolicy => Spec.Build((int n) => n % 2 == 0).Create("is even");`.

- [ ] **Step 2: Run to verify failures.**

- [ ] **Step 3-5: Mirror Task 7 exactly on the PolicyResult twins.**

- New classes copied from `HigherOrderFromPolicyResultMetadataProposition.cs` (collapse the assertion lazy at 38-43 and the guarded reason at 45-50 into one `ElseFallback` lazy) and `HigherOrderFromPolicyResultMultiMetadataProposition.cs` (inline switch 40-44 → `ElseFallback`).
- Shared classes: `HigherOrderFromPolicyResultMetadataProposition` collapses to `specDescription.ToReason(isSatisfied)`; multi twin's switch → `ToReason(...).ToEnumerable()`. (`MinimalHigherOrderFromPolicyResultProposition` untouched.)
- Four factories: `Create()` gains the `trueBecause` guard + constructs the new class; `Create(string)` unchanged.

- [ ] **Step 6: Run scoped, then full, repair, commit**

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named higher-order policy-result explanation specs adopt metadata semantics"
```

---

### Task 9: ExpressionTree family (`Spec.From(expression)`)

**Family fallback note:** this family's metadata behavior surfaces **underlying decomposed assertions** (`result.Assertions`), not `ToReason`. Named explanation specs here therefore align to `ExpressionTreeMetadataProposition` behavior (assertions = underlying clause assertions; Reason = `ToReason`), and the unnamed degenerate fallback is `result.Assertions`.

**Files:**
- Create: `src/Motiv/ExpressionTreeProposition/MinimalExpressionTreeProposition.cs`
- Create: `src/Motiv/ExpressionTreeProposition/ExpressionTreeMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/ExpressionTreeMultiMetadataProposition.cs:47-51`
- Modify: `src/Motiv/ExpressionTreeProposition/ExpressionTreeExplanationProposition.cs:35-38`
- Modify: `src/Motiv/ExpressionTreeProposition/ExpressionTreeWithSingleTrueAssertionProposition.cs:31-41`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/MinimalExpressionTreePropositionFactory.cs:22`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanMinimalExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/ExplanationWithNameExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanExplanationWithNameExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/ExplanationExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanExplanationExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/MultiAssertionExplanationWithNameExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanMultiAssertionExplanationWithNameExpressionTreePropositionFactory.cs`
- Test: `src/Motiv.Tests/ExpressionTreeNamedExplanationSemanticsTests.cs`

**Interfaces:**
- Consumes: `ElseFallback` (Task 1, including the `Func<IEnumerable<string>>` overload).
- Produces: `MinimalExpressionTreeProposition<TModel, TPredicateResult>` (pass-through copy) and `ExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>`.

- [ ] **Step 1: Write the failing tests**

Capture-first convention applies heavily here (expression-derived statements and underlying assertions are hard to predict — run each test once against unmodified code where it pins existing behavior, and once expected-red for new behavior):

- Named single: `Spec.From((int n) => n > 0).WhenTrue("pos").WhenFalse("neg").Create("is positive")` → `Assertions` = underlying clause assertions (capture exact strings), `Reason` = `"is positive == true"` / `"== false"`, `Values` = `["pos"]`/`["neg"]`.
- Unnamed single: same without name → `Assertions`/`Reason` keep `"pos"`/`"neg"`.
- Unnamed degenerate: `.WhenFalse((_, _) => " ")` → falls back to underlying assertions (capture).
- Unnamed guard: `ExplanationWithName...Create()` derives its statement from the expression, NOT from `trueBecause` — **no guard is added here**; instead assert whitespace `WhenTrue` + `Create()` does NOT throw and falls back at evaluation.
- Named multi + unnamed multi (`WhenFalseYield`).
- Minimal pin: `Spec.From((int n) => n > 0).Create("is positive")` — capture current `Assertions` (underlying pass-through) and assert unchanged.

- [ ] **Step 2: Run, capture baselines, verify new-behavior tests fail.**

- [ ] **Step 3: Create pass-through minimal class, reroute the two minimal factories**

Copy `ExpressionTreeMultiMetadataProposition.cs` → `MinimalExpressionTreeProposition.cs`; rename to `MinimalExpressionTreeProposition<TModel, TPredicateResult>`, fix value metadata to `string`, keep the `IEnumerable<string> because => because / _ => result.Assertions` special-case verbatim. Reroute `MinimalExpressionTreePropositionFactory.cs:22` and the `BooleanMinimalExpressionTreePropositionFactory` (keep its `ExpressionSpecDecorator` wrapper) to construct it with today's args.

- [ ] **Step 4: Apply named rule to the shared multi class**

`ExpressionTreeMultiMetadataProposition.cs`: replace the Explanation special-case (47-51) so it always uses `result.Assertions` (delete the `IEnumerable<string>` arm; keep the existing `_` arm expression as the only path).

- [ ] **Step 5: Convert the two single explanation classes to unnamed-only**

`ExpressionTreeExplanationProposition.cs`: delete the reason ternary (35-38: `description.HasExplicitStatement ? description.ToReason(...) : assertion.Value`) and use `assertion.Value` directly; wrap the assertion resolution with `.ElseFallback(() => <the existing underlying-assertions expression used by this family — result.Assertions serialized via string.Join(", ", ...) if a single string is required; match how ExpressionTreeMetadataProposition renders its non-string fallback>)`. If the fallback shape is ambiguous in the file, prefer `description.ToReason(satisfied)` for the single-string fallback and note it in the commit message.

`ExpressionTreeWithSingleTrueAssertionProposition.cs`: same treatment (delete ternary at 38-41; `ElseFallback` on the assertion at 31-36).

- [ ] **Step 6: Reroute the six named factories**

- `ExplanationWithNameExpressionTreePropositionFactory.cs` `Create(string)`: construct `ExpressionTreeMetadataProposition<TModel, string, TPredicateResult>(expression, trueBecause.ToFunc<TModel, BooleanResultBase<string>, string>(), falseBecause, new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true })`. `Create()` unchanged (still `ExpressionTreeWithSingleTrueAssertionProposition` with `ExpressionTreeDescription`; **no trueBecause guard** — statement comes from the expression).
- `BooleanExplanationWithNameExpressionTreePropositionFactory.cs` `Create(string)`: same but `<TModel, string, bool>` and keep the `ExpressionPolicyDecorator` wrapper.
- `ExplanationExpressionTreePropositionFactory.cs` and `BooleanExplanationExpressionTreePropositionFactory.cs` `Create(string)`: construct `ExpressionTreeMetadataProposition` (`trueBecause` is already a `Func` — pass directly), keeping wrappers. `Create()` unchanged.
- `MultiAssertionExplanationWithNameExpressionTreePropositionFactory.cs` `Create()`: add `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));` (this one's statement IS `trueBecause` via `SpecDescription(trueBecause)`) and construct `ExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>`. `Create(string)` unchanged (shared multi class, now named rule).
- `BooleanMultiAssertionExplanationWithNameExpressionTreePropositionFactory.cs`: same with `ExpressionSpecDecorator` wrapper.

- [ ] **Step 7: Create the unnamed multi class**

Copy the pass-through `MinimalExpressionTreeProposition.cs` → `ExpressionTreeMultiAssertionExplanationProposition.cs`; rename; replace the special-case with the unnamed rule using the collection fallback:

```csharp
() => new Explanation(
    metadata.Value.ElseFallback(() => result.Assertions)),
```

(match local variable names in the copied file).

- [ ] **Step 8: Run scoped, then full, repair, commit**

Expression-tree tests are numerous (`ExpressionSpec*Tests.cs`, `ExpressionTreeProposition/` folder if present) — repair per the named rule and this family's fallback note.

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named expression-tree explanation specs adopt metadata semantics"
```

---

### Task 10: HigherOrder — ExpressionTree sub-family

**Files:**
- Create: `src/Motiv/HigherOrderProposition/ExpressionTree/HigherOrderFromExpressionTreeMultiAssertionExplanationProposition.cs`
- Modify: `src/Motiv/HigherOrderProposition/ExpressionTree/HigherOrderFromExpressionTreeExplanationProposition.cs:45-48`
- Modify: `src/Motiv/HigherOrderProposition/ExpressionTree/HigherOrderFromExpressionTreeMultiMetadataProposition.cs:46-50`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree/ExplanationWithNameHigherOrderExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree/BooleanExplanationWithNameHigherOrderExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree/MultiAssertionExplanationFromBooleanResultWithNameHigherOrderExpressionTreePropositionFactory.cs`
- Modify: `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree/BooleanMultiAssertionExplanationFromBooleanResultWithNameHigherOrderExpressionTreePropositionFactory.cs`
- Test: extend `src/Motiv.Tests/ExpressionTreeNamedExplanationSemanticsTests.cs` with higher-order cases

**Interfaces:**
- Consumes: `ElseFallback` (Task 1); `HigherOrderFromExpressionTreeMetadataProposition` (existing class, becomes the named single target).
- Produces: `HigherOrderFromExpressionTreeMultiAssertionExplanationProposition<TModel, TPredicateResult>`.

- [ ] **Step 1: Add failing tests** — named/unnamed/degenerate cases over `Spec.From((int n) => n > 0).AsAllSatisfied().WhenTrue("...").WhenFalse("...").Create("name")` / `.Create()`. Named single: `Reason` = suffix, `Assertions` = underlying assertions (capture), `Values` = strings. These factories' `Create()` uses `SpecDescription(trueBecause)` — so unnamed guard DOES apply here: assert whitespace `WhenTrue` + `Create()` throws.

- [ ] **Step 2: Run, capture, verify failures.**

- [ ] **Step 3: Class edits**

- `HigherOrderFromExpressionTreeExplanationProposition.cs` (becomes unnamed-only): delete the reason ternary (45-48), use `assertion.Value` directly, and wrap the assertion resolution (34-43) with `.ElseFallback(() => underlyingResults.GetAssertions())` — flatten to a single string with `string.Join(", ", ...)` only if the lazy is `Lazy<string>`; if so use `.ElseFallback(() => description.ToReason(isSatisfied))` instead for the single-string fallback (match Task 9 Step 5's choice).
- `HigherOrderFromExpressionTreeMultiMetadataProposition.cs`: delete the `IEnumerable<string> reasons => reasons` arm (46-50) so it always uses `underlyingResults.GetAssertions()`.
- New `HigherOrderFromExpressionTreeMultiAssertionExplanationProposition.cs`: copy of the multi class WITH the unnamed rule: `metadata.Value.ElseFallback(() => underlyingResults.GetAssertions())`.

- [ ] **Step 4: Factory edits**

- `ExplanationWithNameHigherOrderExpressionTreePropositionFactory.cs` and `BooleanExplanationWithNameHigherOrderExpressionTreePropositionFactory.cs`: `Create()` adds `trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));` (constructed class unchanged — now unnamed-only); `Create(string)` constructs `HigherOrderFromExpressionTreeMetadataProposition<TModel, string, TPredicateResult>` (or `<TModel, string, bool>`) with `TrueBecauseFunc`/`falseBecause` and the `HasExplicitStatement = true` description.
- The two MultiAssertion factories: `Create()` adds the guard and constructs the new multi explanation class; `Create(string)` unchanged (shared multi class, now named rule).

- [ ] **Step 5: Run scoped, then full, repair, commit**

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "feat!: named higher-order expression-tree explanation specs adopt metadata semantics"
```

---

### Task 11: Delete `HasExplicitStatement`

**Files:**
- Modify: `src/Motiv/ISpecDescription.cs:26` (remove the property)
- Modify: all implementations (`src/Motiv/Shared/SpecDescription.cs`, `src/Motiv/Not/NotSpecDescription.cs`, `src/Motiv/Shared/BinarySpecDescription.cs`, `src/Motiv/ExpressionTreeProposition/ExpressionAsStatementDescription.cs`, `src/Motiv/ExpressionTreeProposition/ExpressionTreeDescription.cs`, `src/Motiv/Shared/ExpressionDescription.cs`)
- Modify: ~90 factory files removing `{ HasExplicitStatement = true }` initializers

- [ ] **Step 1: Verify zero readers remain**

Run: `grep -rn "HasExplicitStatement" src/Motiv --include="*.cs" | grep -v "PropositionBuilders" | grep -v "= true"`
Expected: only the interface declaration and the 6 property implementations. If ANY evaluation-class reader remains, STOP — a prior task missed a branch; fix that first.

- [ ] **Step 2: Remove writers mechanically**

Replace all `) { HasExplicitStatement = true }` object-initializer suffixes with `)` across `src/Motiv/**/PropositionBuilders/**` and any factory outside those folders. On Git Bash use word-boundary-safe sed (per project memory) or do it via IDE structural replace; verify with:

Run: `grep -rn "HasExplicitStatement" src/Motiv --include="*.cs"`
Expected after edits: zero matches (interface + impls removed too).

- [ ] **Step 3: Build + full test project**

Run: `dotnet build src/Motiv/Motiv.csproj && dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: PASS with no behavior change (the flag was decision-dead after Tasks 2-10).

- [ ] **Step 4: Commit**

```bash
git add -A src/Motiv src/Motiv.Tests
git commit -m "refactor: delete HasExplicitStatement flag now that class split encodes named semantics"
```

---

### Task 12: Whole-solution test repair (all TFMs + example projects)

**Files:**
- Modify: failing tests across `src/Motiv.Tests`, `src/examples/Motiv.Poker.Tests`, `src/examples/Motiv.ECommerce.Tests`, `src/examples/Motiv.SmartHome.Tests`, `src/Motiv.CodeFix.Tests`, `src/Motiv.Analyzer.Tests`

- [ ] **Step 1: Run the full solution suite**

Run: `dotnet test Motiv.slnx`
(If `dotnet test` rejects `.slnx`, run per-project: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj` then each example/CodeFix/Analyzer test project — all TFMs, no `-f` filter.)

- [ ] **Step 2: Repair failures using ONLY these transformation rules**

1. Named explanation spec expectation on `Assertions`/`Reason`/`Justification` → `"{statement} == true/false"` (ExpressionTree family: `Assertions` = underlying assertions, `Reason` = suffix).
2. Expectations on `Values`/metadata → unchanged (the strings).
3. If a test intended to verify explanation strings and the strings are now metadata, EITHER assert via `Values` OR drop the explicit name (use parameterless `Create()`) — choose whichever preserves the test's documented intent; prefer `Values` for integration-style tests.
4. CodeFix/Analyzer tests: only descriptions/doc-comments should be affected, if anything; generated code shape must NOT change. If a CodeFix test fails on generated output, STOP and investigate — that indicates an unintended API change.
5. net472 test quirks: records redeclare positional props as get-only (project memory) — relevant only if adding new test records.

- [ ] **Step 3: Verify green across all TFMs, commit**

Run: `dotnet test Motiv.slnx` (or the per-project set)
Expected: all ~13,267 tests PASS.

```bash
git add -A src
git commit -m "test: align solution test suite with named-explanation metadata semantics"
```

---

### Task 13: Documentation

**Files:**
- Modify: `CLAUDE.md` (root of repo)
- Modify: `docs/builder/WhenTrue.md`, `docs/builder/Create.md` (and `WhenFalse.md` if it shows named output)
- Modify: `README.md` (Core Features examples showing named explanation output)
- Modify: XML docs on the ~32 factories' `Create()`/`Create(string)` methods

- [ ] **Step 1: Rewrite CLAUDE.md sections**

In `C:\Dev\Motiv\CLAUDE.md`:
- **"The `== true` / `== false` Suffix Rule"** — replace the section body with: the suffix applies whenever an explicit name is supplied via `.Create("name")`, regardless of WhenTrue/WhenFalse payload types; WhenTrue/WhenFalse payloads (strings included) are metadata when a name exists; strings serve as explanation text only for parameterless `Create()` (where the WhenTrue string is also the statement, and is guarded non-whitespace); degenerate strings in the unnamed path fall back to `"statement == true/false"`.
- **"Three Proposition Types" → Explanation Proposition** — update the example output comment: with `Create()` assertions are the strings; add a named variant example showing `Assertions = ["is active == true"]`, `Values = ["user is active"]`.
- **"Assertions Property Rules"** — rule 1 becomes: "Explanation propositions: assertions come from WhenTrue/WhenFalse strings only when `Create()` is parameterless; with `Create(\"name\")` assertions are `\"{name} == true/false\"` and the strings appear in `Values`."

- [ ] **Step 2: Update docs/ and README examples**

Every example in `docs/builder/WhenTrue.md`, `docs/builder/Create.md`, `README.md` that shows a named explanation spec's `Assertions`/`Reason` output must show the suffix form; add a short "v8 breaking change" callout box in `Create.md` with the migration line: *"Named explanation specs now report `name == true/false`; to keep string assertions, use parameterless `Create()`, or read the strings from `Values`."* Grep `docs/` for `WhenTrue("` to find further affected pages.

- [ ] **Step 3: XML docs**

On each WithName factory: `Create()` summary gains `<exception cref="ArgumentException">Thrown when the WhenTrue assertion is null, empty or whitespace (it doubles as the propositional statement).</exception>` (only for the factories that actually guard — not the two expression-statement ones); `Create(string)` gains `<exception cref="ArgumentException">Thrown when <paramref name="statement"/> is null, empty or whitespace.</exception>` and a `<remarks>` note that WhenTrue/WhenFalse values are surfaced via `Values`, not `Assertions`.

- [ ] **Step 4: Build docs sanity + commit**

Run: `dotnet build src/Motiv/Motiv.csproj` (XML docs compile) — Expected: PASS, no new warnings.

```bash
git add CLAUDE.md README.md docs src/Motiv
git commit -m "docs: document named-explanation metadata semantics and Create() guards"
```

---

### Task 14: Final verification and mandatory simplification review

- [ ] **Step 1: Full suite, all TFMs**

Run: `dotnet test Motiv.slnx` (or per-project across net472/net8.0/net9.0/net10.0)
Expected: PASS.

- [ ] **Step 2: Spec conformance sweep**

Check each spec behavioral row against a REPL-style scratch test or the new test files: named single, named delegate, named yield, unnamed single, unnamed degenerate, guard throws, stray guard gone (`Create("name")` with whitespace `WhenTrue` does not throw), minimal pass-through unchanged.

- [ ] **Step 3: Mandatory code-simplifier review (CLAUDE.md requirement)**

Spawn a `code-simplifier` agent over the diff (`git diff main...HEAD`) focusing on: duplication among the 17 new classes (acceptable per the project's anti-over-DRY stance, but flag accidental divergence), dead code left by arm deletions (unused `ToEnumerable`/`using` imports, orphaned locals), naming consistency of the new classes. Apply accepted findings, re-run affected tests.

- [ ] **Step 4: Commit any review fixes**

```bash
git add -A
git commit -m "refactor: apply code-simplifier findings"
```

---

## Post-plan notes for the PR

- PR targets the v8.0.0 line (expression-trees, PR #57 lineage) — coordinate base branch with the release owner.
- PR body must include the migration note: *"Named explanation specs now report `name == true/false`; to keep string assertions, use parameterless `Create()`, or read the strings from `Values`."*
- Codecov patch gate: the new tests in Tasks 1-10 cover every new guard/fallback line; if the gate still fails, add targeted tests for the uncovered lines rather than suppressing.
