# AndAlso Policy Preservation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `AndAlso` policy-preserving across the sync, async, expression-tree and result surfaces, mirroring the existing `OrElse` policy family.

**Architecture:** A strict mirror. Every new file has an `OrElse` counterpart to copy structure from. Only two things invert: which operand becomes irrelevant under short-circuit (`OrElse` skips the right when the left is satisfied; `AndAlso` skips it when the left is unsatisfied), and the description operator (`"&&"` / `Operator.AndAlso`). `Value` stays `(Right ?? Left).Value` in both, which for `AndAlso` reads as *first failure wins, else the final success*.

**Tech Stack:** C# / .NET (net8.0, net9.0, net10.0, net472), xUnit, Shouldly, AutoFixture.

## Global Constraints

- **Test invocation requires the user-local .NET root.** Every `dotnet` command must be prefixed with `export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"`. Without it, net8.0/net9.0 testhosts abort with "You must install or update .NET".
- **net472 cannot execute on this machine** (vstest needs mono). It compiles only; CI covers it. `dotnet test` therefore exits 1 even when everything passes — **judge success by the per-assembly `Failed: 0` lines, never the exit code.** Use `-f net10.0` for filtered runs.
- **TDD applies normally.** This is new behaviour, unlike the preceding branch. Write the failing test, run it, confirm it fails *for the right reason*, implement minimally, confirm green.
- **Mirror, do not abstract.** CLAUDE.md: "Avoid over-DRYing — the codebase intentionally has some duplication between proposition types. Explicit code is preferred over complex abstractions with branching logic." Do not extract a shared short-circuit base for `OrElse`/`AndAlso`.
- **Assert collections with Shouldly collection expressions**: `result.Values.ShouldBe(["a", "b"])`.
- **The full solution suite is the gate**, not just `Motiv.Tests` — the example projects (`src/examples/Motiv.Poker.Tests`, `Motiv.ECommerce.Tests`, `Motiv.SmartHome.Tests`) assert on justification strings and are the net for the predicate hazard below.
- **Internal types are constructed directly by tests** via `[InternalsVisibleTo]`. When changing an internal constructor signature, search all call sites first.

**Source spec:** `docs/superpowers/specs/2026-08-02-andalso-policy-preservation-design.md`

---

## The Predicate Hazard

Three separate layers decide whether operands collapse into one operation heading in `Justification` output. The `OrElse` family populates all three with its policy types; the `AndAlso` family populates none. **Missing any one produces no build error and no isolated unit-test failure** — it silently stops a mixed spec/policy conjunction chain from collapsing, visible only in multi-level `Justification` strings.

| layer | file(s) | today | must become |
|---|---|---|---|
| sync + expression spec description | `AndAlsoSpec.cs`, `ExpressionAndAlsoSpec.cs` | 4-way | 6-way (add `AndAlsoPolicy`, `ExpressionAndAlsoPolicy`) |
| async spec description | `AsyncAndAlsoSpec.cs` | 6-way | 9-way (add `AsyncAndAlsoPolicy`, `AndAlsoPolicy`, `ExpressionAndAlsoPolicy`) |
| result description `IsSameFamily` | `AndAlsoBooleanResultDescription.cs` | 2-way | 3-way (add `AndAlsoPolicyResult`) |

Completion check, runnable at any point:

```bash
grep -c "BinarySpecDescription" src/Motiv/OrElse/*.cs | wc -l    # 6 carriers
grep -c "BinarySpecDescription" src/Motiv/AndAlso/*.cs | wc -l   # 3 today, must end at 6
```

The result classes do **not** carry `BinarySpecDescription` — they use `AndAlsoBooleanResultDescription`. So `AndAlsoPolicyResult.cs` is not one of the six.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Motiv/AndAlso/AndAlsoPolicyResult.cs` (create) | Result of a short-circuited conjunction of two policy results. Owns the `Value` selection rule. |
| `src/Motiv/AndAlso/AndAlsoPolicy.cs` (create) | Sync composition of two policies. |
| `src/Motiv/AndAlso/AsyncAndAlsoPolicy.cs` (create) | Async composition; the right operand's I/O never starts when the left is unsatisfied. |
| `src/Motiv/AndAlso/ExpressionAndAlsoPolicy.cs` (create) | Expression-tree composition; also rebuilds the combined `Expression`. |
| `src/Motiv/AndAlso/AndAlsoBooleanResultDescription.cs` (modify) | Widen `IsSameFamily`. |
| `src/Motiv/AndAlso/{AndAlso,AsyncAndAlso,ExpressionAndAlso}Spec.cs` (modify) | Widen collapsible predicates. |
| `src/Motiv/PolicyResultBase.cs` (modify) | `AndAlso` overload; `<remarks>` on `Value`. |
| `src/Motiv/PolicyBase.cs` (modify) | Two `AndAlso` overloads. |
| `src/Motiv/AsyncPolicyBase.cs` (modify) | Two `AndAlso` overloads. |
| `src/Motiv/ExpressionPolicyBase.cs` (modify) | Policy-preserving `AndAlso` plus declaring-type-precedence redeclarations. |
| `src/Motiv/{Policy,PolicyResult}Extensions.cs` (modify) | `AndAlsoTogether`. |
| `src/Motiv.Tests/AndAlsoPolicyTests.cs` (create) | The new behaviour's tests. |

---

### Task 1: Pin today's `policy.AndAlso(policy)` output

This is the regression net for the overload-resolution hazard. `policy.AndAlso(policy)` currently binds to `SpecBase.AndAlso` and produces an `AndAlsoSpec`/`AndAlsoBooleanResult`. After Task 3 it produces an `AndAlsoPolicy`/`AndAlsoPolicyResult`. If those render differently, every existing consumer who combined two policies sees changed output. **This test must survive every later task unmodified.**

**Files:**
- Create: `src/Motiv.Tests/AndAlsoPolicyTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `AndAlsoPolicyTests`, the file every later task adds to.

- [ ] **Step 1: Write the characterisation test**

Create `src/Motiv.Tests/AndAlsoPolicyTests.cs`:

```csharp
namespace Motiv.Tests;

public class AndAlsoPolicyTests
{
    private static PolicyBase<string, string> Gate(bool satisfied, string name) =>
        Spec.Build<string>(_ => satisfied)
            .WhenTrue($"{name}-true")
            .WhenFalse($"{name}-false")
            .Create(name);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Should_render_a_conjunction_of_two_policies_identically_before_and_after_policy_preservation(
        bool leftSatisfied,
        bool rightSatisfied)
    {
        // Arrange
        var composed = Gate(leftSatisfied, "left").AndAlso(Gate(rightSatisfied, "right"));

        // Act
        var result = composed.Evaluate("model");

        // Assert — these renderings are the public contract for every existing consumer who
        // combined two policies. Making AndAlso policy-preserving must not change any of them.
        result.Satisfied.ShouldBe(leftSatisfied && rightSatisfied);
        result.Reason.ShouldBe(ExpectedReason(leftSatisfied, rightSatisfied));
        result.Assertions.ShouldBe(ExpectedAssertions(leftSatisfied, rightSatisfied));
        result.Values.ShouldBe(ExpectedAssertions(leftSatisfied, rightSatisfied));
    }

    private static string ExpectedReason(bool left, bool right) =>
        throw new NotImplementedException("replaced in Step 2 with the observed value");

    private static string[] ExpectedAssertions(bool left, bool right) =>
        throw new NotImplementedException("replaced in Step 2 with the observed value");
}
```

- [ ] **Step 2: Run it and replace the expectations with observed output**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: FAIL with `NotImplementedException` on all four cases.

Now capture the real values. Temporarily replace the two helper bodies with a throw that prints what was actually produced:

```csharp
    private static string ExpectedReason(bool left, bool right) => "";
    private static string[] ExpectedAssertions(bool left, bool right) => [];
```

and add, as the first assertion in the test body:

```csharp
        throw new Exception(
            $"[{leftSatisfied},{rightSatisfied}] Satisfied={result.Satisfied} " +
            $"Reason=[{result.Reason}] " +
            $"Assertions=[{string.Join(" | ", result.Assertions)}] " +
            $"Values=[{string.Join(" | ", result.Values)}] " +
            $"Type={result.GetType().Name}");
```

Re-run. Record the four lines of output. Then **delete the throw** and write the two helpers as `switch` expressions returning exactly the observed values, e.g.:

```csharp
    private static string ExpectedReason(bool left, bool right) =>
        (left, right) switch
        {
            (true, true) => "<observed>",
            (true, false) => "<observed>",
            (false, true) => "<observed>",
            (false, false) => "<observed>"
        };
```

Do the same for `ExpectedAssertions`, returning the observed arrays.

This is characterisation, not invention: every value must come from the run, never from what you expect the library to do. Record the observed `Type=` values in your report — they should be `AndAlsoBooleanResult` now and `AndAlsoPolicyResult` after Task 3.

- [ ] **Step 3: Confirm green**

Re-run the Step 2 command. Expected: 4/4 PASS, no exceptions.

- [ ] **Step 4: Commit**

```bash
git add src/Motiv.Tests/AndAlsoPolicyTests.cs
git commit -m "test(andalso): pin the current rendering of a two-policy conjunction"
```

---

### Task 2: `AndAlsoPolicyResult` and the result-level `AndAlso`

**Files:**
- Create: `src/Motiv/AndAlso/AndAlsoPolicyResult.cs`
- Modify: `src/Motiv/AndAlso/AndAlsoBooleanResultDescription.cs`
- Modify: `src/Motiv/PolicyResultBase.cs`
- Modify: `src/Motiv.Tests/AndAlsoPolicyTests.cs`

**Interfaces:**
- Consumes: `AndAlsoPolicyTests` from Task 1.
- Produces: `internal sealed class AndAlsoPolicyResult<TMetadata>(PolicyResultBase<TMetadata> left, PolicyResultBase<TMetadata>? right = null)` — a `PolicyResultBase<TMetadata>` with `Value => (Right ?? Left).Value`. Every later task constructs it. Also `PolicyResultBase<TMetadata>.AndAlso(PolicyResultBase<TMetadata>) → PolicyResultBase<TMetadata>`.

- [ ] **Step 1: Write the failing test**

Append inside `AndAlsoPolicyTests`:

```csharp
    private static PolicyResultBase<string> Evaluated(bool satisfied, string name) =>
        Gate(satisfied, name).Evaluate("model");

    [Fact]
    public void Should_select_the_first_failure_when_combining_two_policy_results()
    {
        // Arrange — the left gate passes, so the right is the decisive one.
        var left = Evaluated(true, "left");
        var right = Evaluated(false, "right");

        // Act
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBeFalse();

        // Value is the last-evaluated operand: for a conjunction that means the gate that failed.
        result.Value.ShouldBe("right-false");

        // Only the failing gate is causal — a passing gate did not cause an unsatisfied conjunction.
        result.Values.ShouldBe(["right-false"]);
    }

    [Fact]
    public void Should_short_circuit_on_an_unsatisfied_left_policy_result()
    {
        // Arrange
        var left = Evaluated(false, "left");
        var right = Evaluated(true, "right");

        // Act — the left already decided the outcome, so the right is not part of the result.
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("left-false");
        result.Values.ShouldBe(["left-false"]);
        result.Underlying.Count().ShouldBe(1);
    }

    [Fact]
    public void Should_select_the_last_evaluated_value_when_every_policy_result_is_satisfied()
    {
        // Arrange
        var left = Evaluated(true, "left");
        var right = Evaluated(true, "right");

        // Act
        var result = left.AndAlso(right);

        // Assert
        result.Satisfied.ShouldBeTrue();

        // All gates passed, so no operand is decisive; Value takes the last evaluated, and
        // Values still reports every contributing cause.
        result.Value.ShouldBe("right-true");
        result.Values.ShouldBe(["left-true", "right-true"]);
    }
```

- [ ] **Step 2: Run to verify it fails for the right reason**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: **compile error** — `PolicyResultBase<string>` has no `AndAlso` returning something with a `.Value`. (It inherits `BooleanResultBase.AndAlso`, which returns `BooleanResultBase<TMetadata>` with no `Value` member.) That compile failure is the correct red state. Task 1's four tests will not run until it compiles; that is expected.

- [ ] **Step 3: Create `AndAlsoPolicyResult`**

Create `src/Motiv/AndAlso/AndAlsoPolicyResult.cs`:

```csharp
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.AndAlso;

internal sealed class AndAlsoPolicyResult<TMetadata>(
    PolicyResultBase<TMetadata> left,
    PolicyResultBase<TMetadata>? right = null)
    : PolicyResultBase<TMetadata>, IBinaryBooleanOperationResult<TMetadata>
{
    public override TMetadata Value => (Right ?? Left).Value;

    public override bool Satisfied { get; } = left.Satisfied && (right?.Satisfied ?? true);

    public override ResultDescriptionBase Description =>
        field ??= new AndAlsoBooleanResultDescription<TMetadata>(GetCauses());

    public override Explanation Explanation => field ??= new Explanation(GetCauses(), Underlying);

    public override MetadataNode<TMetadata> MetadataTier => field ??= CreateMetadataTier();

    public PolicyResultBase<TMetadata> Left { get; } = left;
    public PolicyResultBase<TMetadata>? Right { get; } = right;

    BooleanResultBase<TMetadata> IBinaryBooleanOperationResult<TMetadata>.Left { get; } = left;

    BooleanResultBase<TMetadata>? IBinaryBooleanOperationResult<TMetadata>.Right { get; } = right;

    public string Operation => Operator.AndAlso;
    public bool IsCollapsable => true;

    BooleanResultBase IBinaryBooleanOperationResult.Left => Left;

    BooleanResultBase? IBinaryBooleanOperationResult.Right => Right;

    public override IEnumerable<BooleanResultBase> Underlying => GetUnderlying();

    public override IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues => GetUnderlying();

    public override IEnumerable<BooleanResultBase> Causes => GetCauses();

    public override IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues => GetCauses();


    private IEnumerable<PolicyResultBase<TMetadata>> GetCauses()
    {
        if (Satisfied == Left.Satisfied)
            yield return Left;

        if (Right is not null && Satisfied == Right.Satisfied)
            yield return Right;
    }

    private IEnumerable<BooleanResultBase<TMetadata>> GetUnderlying()
    {
        yield return Left;

        if (Right is not null)
            yield return Right;
    }

    private MetadataNode<TMetadata> CreateMetadataTier() =>
        new(CausesWithValues.GetValues(), CausesWithValues);
}
```

Note `Satisfied`: `left.Satisfied && (right?.Satisfied ?? true)`. A null `Right` means the left was unsatisfied and short-circuited, so the `?? true` is inert — but it is the correct identity for conjunction and mirrors `OrElsePolicyResult`'s `?? false`.

- [ ] **Step 4: Widen the result-description family (predicate hazard, layer 3)**

In `src/Motiv/AndAlso/AndAlsoBooleanResultDescription.cs`, change:

```csharp
    protected override bool IsSameFamily(BooleanResultBase<TMetadata> result) =>
        result is AndBooleanResult<TMetadata> or AndAlsoBooleanResult<TMetadata>;
```

to:

```csharp
    protected override bool IsSameFamily(BooleanResultBase<TMetadata> result) =>
        result is AndBooleanResult<TMetadata> or AndAlsoPolicyResult<TMetadata>
            or AndAlsoBooleanResult<TMetadata>;
```

This mirrors `OrElseBooleanResultDescription.cs:15`, which lists `OrElsePolicyResult` in the same position.

- [ ] **Step 5: Add the result-level `AndAlso`**

In `src/Motiv/PolicyResultBase.cs`, add `using Motiv.AndAlso;` alongside the existing `using Motiv.OrElse;`, then add this method immediately **before** the existing `OrElse` method:

```csharp
    /// <summary>
    /// Performs a conditional AND operation between the current PolicyResultBase instance and another
    /// PolicyResultBase instance. The right operand does not contribute when the left operand is unsatisfied,
    /// since an unsatisfied left operand already determines the outcome.
    /// </summary>
    /// <param name="right">The other policy result instance to perform the AND operation with.</param>
    /// <returns>A new policy result instance representing the result of the AND operation.</returns>
    public PolicyResultBase<TMetadata> AndAlso(PolicyResultBase<TMetadata> right) => Satisfied
        ? new AndAlsoPolicyResult<TMetadata>(this, right)
        : new AndAlsoPolicyResult<TMetadata>(this);
```

Note the inversion against `OrElse` directly below it: `OrElse` drops the right operand when `Satisfied`, `AndAlso` drops it when not.

- [ ] **Step 6: Add the `<remarks>` on `Value`** (folded-in follow-up)

In the same file, replace:

```csharp
    /// <summary>The single metadata instance that is returned by the policy.</summary>
    public abstract TMetadata Value { get; }
```

with:

```csharp
    /// <summary>The single metadata instance that is returned by the policy.</summary>
    /// <remarks>
    /// For a short-circuiting composition (<see cref="OrElse" /> or <see cref="AndAlso" />) this value is a
    /// <em>selection</em> — the last-evaluated operand's — and not a guarantee that only one cause exists.
    /// When such a composition has more than one contributing cause, <see cref="BooleanResultBase{TMetadata}.Values" />
    /// reports all of them, so <c>Value</c> is not necessarily <c>Values.Single()</c>.
    /// </remarks>
    public abstract TMetadata Value { get; }
```

- [ ] **Step 7: Run to verify green**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: 7/7 PASS — Task 1's four plus these three. **If any of Task 1's four now fail, stop and report**: that means the result-description widening changed existing rendering, which is exactly the regression this plan exists to catch.

- [ ] **Step 8: Commit**

```bash
git add src/Motiv/AndAlso/AndAlsoPolicyResult.cs src/Motiv/AndAlso/AndAlsoBooleanResultDescription.cs src/Motiv/PolicyResultBase.cs src/Motiv.Tests/AndAlsoPolicyTests.cs
git commit -m "feat(andalso): preserve policies when combining two policy results"
```

---

### Task 3: `AndAlsoPolicy` and the proposition-level `AndAlso`

**Files:**
- Create: `src/Motiv/AndAlso/AndAlsoPolicy.cs`
- Modify: `src/Motiv/AndAlso/AndAlsoSpec.cs`
- Modify: `src/Motiv/PolicyBase.cs`
- Modify: `src/Motiv.Tests/AndAlsoPolicyTests.cs`

**Interfaces:**
- Consumes: `AndAlsoPolicyResult<TMetadata>` from Task 2.
- Produces: `internal sealed class AndAlsoPolicy<TModel, TMetadata>(PolicyBase<TModel, TMetadata> left, PolicyBase<TModel, TMetadata> right)`, and `PolicyBase<TModel, TMetadata>.AndAlso(PolicyBase<TModel, TMetadata>) → PolicyBase<TModel, TMetadata>`. Tasks 4 and 5 name `AndAlsoPolicy` in their collapsible predicates.

- [ ] **Step 1: Write the failing test**

Append inside `AndAlsoPolicyTests`:

```csharp
    [Fact]
    public void Should_preserve_the_policy_when_combining_two_propositions()
    {
        // Arrange
        var composed = Gate(true, "left").AndAlso(Gate(false, "right"));

        // Act
        var result = composed.Evaluate("model");

        // Assert — the static type is the point: AndAlso on two policies yields a policy,
        // so `.Value` is available without a cast.
        result.Value.ShouldBe("right-false");
        result.Values.ShouldBe(["right-false"]);
        result.Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_not_evaluate_the_right_proposition_when_the_left_is_unsatisfied()
    {
        // Arrange
        var rightEvaluations = 0;
        var left = Gate(false, "left");
        var right = Spec
            .Build<string>(_ => { rightEvaluations++; return true; })
            .WhenTrue("right-true")
            .WhenFalse("right-false")
            .Create("right");

        // Act
        var result = left.AndAlso(right).Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("left-false");
        rightEvaluations.ShouldBe(0);
    }
```

- [ ] **Step 2: Run to verify it fails for the right reason**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: **compile error** on `result.Value` — `left.AndAlso(right)` still binds to `SpecBase.AndAlso`, returning a `SpecBase` whose `Evaluate` gives a `BooleanResultBase` with no `Value`. That is the correct red state.

- [ ] **Step 3: Create `AndAlsoPolicy`**

Create `src/Motiv/AndAlso/AndAlsoPolicy.cs`:

```csharp
using Motiv.And;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.AndAlso;

internal sealed class AndAlsoPolicy<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> left,
    PolicyBase<TModel, TMetadata> right)
    : PolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or AndAlsoSpec<TModel, TMetadata> or ExpressionAndSpec<TModel, TMetadata>
                or ExpressionAndAlsoSpec<TModel, TMetadata> or ExpressionAndAlsoPolicy<TModel, TMetadata>);

    public string Operation => Operator.AndAlso;

    public bool IsCollapsable => true;

    public override bool Matches(TModel model) => left.Matches(model) && right.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var leftResult = left.EvaluatePolicyInternal(model);
        return leftResult.Satisfied switch
        {
            true => new AndAlsoPolicyResult<TMetadata>(leftResult, right.EvaluatePolicyInternal(model)),
            false => new AndAlsoPolicyResult<TMetadata>(leftResult)
        };
    }

    public PolicyBase<TModel, TMetadata> Left => left;

    public PolicyBase<TModel, TMetadata> Right => right;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Left => left;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
```

**The predicate above references `ExpressionAndAlsoPolicy`, which Task 5 creates — so as written this will not compile.** Do not create a placeholder type to satisfy it. Instead, omit the trailing `or ExpressionAndAlsoPolicy<TModel, TMetadata>` clause in this task; Task 5 Step 5 adds it back to every carrier at once. Record the omission in your report so Task 5's reviewer can confirm it was closed.

- [ ] **Step 4: Widen `AndAlsoSpec`'s predicate (hazard layer 1)**

In `src/Motiv/AndAlso/AndAlsoSpec.cs`, change:

```csharp
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);
```

to:

```csharp
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or AndAlsoSpec<TModel, TMetadata> or ExpressionAndSpec<TModel, TMetadata>
                or ExpressionAndAlsoSpec<TModel, TMetadata>);
```

(`ExpressionAndAlsoPolicy` is added to this predicate by Task 5.)

- [ ] **Step 5: Add `PolicyBase.AndAlso`**

In `src/Motiv/PolicyBase.cs`, add `using Motiv.AndAlso;` to the using block, then add this method immediately **before** the existing `OrElse(PolicyBase<TModel, TMetadata>)` method:

```csharp
    /// <summary>
    /// Creates a new policy that is equivalent to a conditional "AND" of the current policy and the other
    /// policy. The other policy is only evaluated if <c>this</c> policy is satisfied. In the event that a
    /// policy is unsatisfied, that policy's "WhenFalse" metadata is selected as the policy value; when every
    /// policy is satisfied, the last one's "WhenTrue" metadata is selected.
    /// </summary>
    /// <param name="other">The policy to evaluate in the event that <c>this</c> policy is satisfied</param>
    /// <returns>
    /// A new <see cref="PolicyBase{TModel,TMetadata}" /> that will perform the conditional "And" operation
    /// between <c>this</c> and <paramref name="other" /> when the policy is eventually evaluated.
    /// </returns>
    public PolicyBase<TModel, TMetadata> AndAlso(PolicyBase<TModel, TMetadata> other) =>
        new AndAlsoPolicy<TModel, TMetadata>(this, other);
```

- [ ] **Step 6: Run to verify green, including Task 1's pin**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: 9/9 PASS.

**This is the plan's most important checkpoint.** Task 1's four tests now exercise `AndAlsoPolicy` instead of `AndAlsoSpec`. If any of them fails, the rendering changed and this is a breaking change for existing consumers — **stop and report** with the before/after values. Do not amend Task 1's expectations to make them pass.

- [ ] **Step 7: Run the full solution suite**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test
```

Expected: every assembly reports `Failed: 0`. The example projects assert on justification strings and are the real net here. Ignore the exit code (net472/mono).

- [ ] **Step 8: Commit**

```bash
git add src/Motiv/AndAlso/AndAlsoPolicy.cs src/Motiv/AndAlso/AndAlsoSpec.cs src/Motiv/PolicyBase.cs src/Motiv.Tests/AndAlsoPolicyTests.cs
git commit -m "feat(andalso): preserve policies when combining two propositions"
```

---

### Task 4: `AsyncAndAlsoPolicy` and the async surface

**Files:**
- Create: `src/Motiv/AndAlso/AsyncAndAlsoPolicy.cs`
- Modify: `src/Motiv/AndAlso/AsyncAndAlsoSpec.cs`
- Modify: `src/Motiv/AsyncPolicyBase.cs`
- Modify: `src/Motiv/PolicyBase.cs`
- Modify: `src/Motiv.Tests/AndAlsoPolicyTests.cs`

**Interfaces:**
- Consumes: `AndAlsoPolicyResult<TMetadata>` (Task 2), `AndAlsoPolicy<TModel, TMetadata>` (Task 3).
- Produces: `AsyncPolicyBase<TModel, TMetadata>.AndAlso(AsyncPolicyBase<TModel, TMetadata>)`, `AsyncPolicyBase<TModel, TMetadata>.AndAlso(PolicyBase<TModel, TMetadata>)`, and `PolicyBase<TModel, TMetadata>.AndAlso(AsyncPolicyBase<TModel, TMetadata>) → AsyncPolicyBase<TModel, TMetadata>`.

- [ ] **Step 1: Write the failing test**

Append inside `AndAlsoPolicyTests`:

```csharp
    private static AsyncPolicyBase<string, string> AsyncGate(bool satisfied, string name) =>
        Spec.Build<string>(_ => new ValueTask<bool>(satisfied))
            .WhenTrue($"{name}-true")
            .WhenFalse($"{name}-false")
            .Create(name);

    [Fact]
    public async Task Should_preserve_the_policy_when_combining_two_async_propositions()
    {
        // Arrange
        var composed = AsyncGate(true, "left").AndAlso(AsyncGate(false, "right"));

        // Act
        var result = await composed.EvaluateAsync("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("right-false");
        result.Values.ShouldBe(["right-false"]);
    }

    [Fact]
    public async Task Should_never_start_the_right_async_operand_when_the_left_is_unsatisfied()
    {
        // Arrange — the whole point of async short-circuiting: the right operand's I/O never begins.
        var rightStarted = false;
        var left = AsyncGate(false, "left");
        var right = Spec
            .Build<string>(_ => { rightStarted = true; return new ValueTask<bool>(true); })
            .WhenTrue("right-true")
            .WhenFalse("right-false")
            .Create("right");

        // Act
        var result = await left.AndAlso(right).EvaluateAsync("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("left-false");
        rightStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_lift_a_sync_policy_into_an_async_conjunction()
    {
        // Arrange
        var composed = AsyncGate(true, "left").AndAlso(Gate(false, "right"));

        // Act
        var result = await composed.EvaluateAsync("model");

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("right-false");
    }
```

Before running, confirm the async builder entry point actually has this shape — check an existing async test such as `src/Motiv.Tests/AsyncOrSpecTests.cs` for how async propositions are constructed, and adapt `AsyncGate` to match. Do not guess the builder signature.

- [ ] **Step 2: Run to verify it fails for the right reason**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: compile error on `result.Value` for the async cases — `AsyncPolicyBase.AndAlso` does not exist, so the call binds to `AsyncSpecBase.AndAlso` and yields a `BooleanResultBase`.

- [ ] **Step 3: Create `AsyncAndAlsoPolicy`**

Create `src/Motiv/AndAlso/AsyncAndAlsoPolicy.cs`, mirroring `src/Motiv/OrElse/AsyncOrElsePolicy.cs`:

```csharp
using Motiv.And;
using Motiv.Shared;
using Motiv.Traversal;

namespace Motiv.AndAlso;

/// <summary>
/// An asynchronous policy that represents the conditional AND of two asynchronous policies, preserving the
/// policy guarantee. The right operand is only evaluated if the left operand resolves to <c>true</c> — for
/// asynchronous policies this means the right operand's work (including any I/O) is never started when the
/// left operand is unsatisfied.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
internal sealed class AsyncAndAlsoPolicy<TModel, TMetadata>(
    AsyncPolicyBase<TModel, TMetadata> left,
    AsyncPolicyBase<TModel, TMetadata> right)
    : AsyncPolicyBase<TModel, TMetadata>,
        IAsyncBinaryOperationSpec<TModel, TMetadata>
{
    private readonly SpecBase[] _underlying = [left, right];

    /// <inheritdoc />
    public override IEnumerable<SpecBase> Underlying => _underlying;

    /// <inheritdoc />
    public override ISpecDescription Description => field ??=
        new AsyncBinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AsyncAndAlsoPolicy<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);

    /// <inheritdoc />
    public string Operation => Operator.AndAlso;

    /// <inheritdoc />
    public bool IsCollapsable => true;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Left => left;

    /// <inheritdoc />
    public AsyncSpecBase<TModel, TMetadata> Right => right;

    SpecBase IAsyncBinaryOperationSpec.Right => Right;

    SpecBase IAsyncBinaryOperationSpec.Left => Left;

    /// <inheritdoc />
    public override async ValueTask<bool> MatchesAsync(TModel model, CancellationToken cancellationToken = default) =>
        await left.MatchesAsync(model, cancellationToken).ConfigureAwait(false)
        && await right.MatchesAsync(model, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask<PolicyResultBase<TMetadata>> EvaluatePolicyAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        var leftResult = await left.EvaluatePolicyAsyncInternal(model, cancellationToken).ConfigureAwait(false);
        return leftResult.Satisfied switch
        {
            true => new AndAlsoPolicyResult<TMetadata>(
                leftResult,
                await right.EvaluatePolicyAsyncInternal(model, cancellationToken).ConfigureAwait(false)),
            false => new AndAlsoPolicyResult<TMetadata>(leftResult)
        };
    }
}
```

Task 5 adds `or ExpressionAndAlsoPolicy<TModel, TMetadata>` to this predicate.

- [ ] **Step 4: Widen `AsyncAndAlsoSpec`'s predicate (hazard layer 2)**

In `src/Motiv/AndAlso/AsyncAndAlsoSpec.cs`, change:

```csharp
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);
```

to:

```csharp
            operand => operand is AsyncAndSpec<TModel, TMetadata> or AsyncAndAlsoSpec<TModel, TMetadata>
                or AsyncAndAlsoPolicy<TModel, TMetadata>
                or AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);
```

- [ ] **Step 5: Add the async members**

In `src/Motiv/AsyncPolicyBase.cs`, add `using Motiv.AndAlso;`, then add immediately **before** the existing `OrElse(AsyncPolicyBase<TModel, TMetadata>)` method:

```csharp
    /// <summary>
    /// Creates a new asynchronous policy that is equivalent to a conditional "AND" of the current policy and
    /// the other policy, preserving the single-value policy guarantee. The other policy's work is only
    /// started if <c>this</c> policy is satisfied.
    /// </summary>
    /// <param name="other">The asynchronous policy to evaluate in the event that <c>this</c> policy is satisfied</param>
    /// <returns>
    /// A new <see cref="AsyncPolicyBase{TModel,TMetadata}" /> that will perform the conditional "And"
    /// operation between <c>this</c> and <paramref name="other" /> when the policy is eventually evaluated.
    /// </returns>
    public AsyncPolicyBase<TModel, TMetadata> AndAlso(AsyncPolicyBase<TModel, TMetadata> other) =>
        new AsyncAndAlsoPolicy<TModel, TMetadata>(this, other);

    /// <inheritdoc cref="AndAlso(AsyncPolicyBase{TModel,TMetadata})" />
    public AsyncPolicyBase<TModel, TMetadata> AndAlso(PolicyBase<TModel, TMetadata> other) =>
        AndAlso(other.ToAsyncSpec());
```

Then in `src/Motiv/PolicyBase.cs`, add immediately **before** the existing `OrElse(AsyncPolicyBase<TModel, TMetadata>)` method:

```csharp
    /// <summary>
    /// Creates a new asynchronous policy that is equivalent to a conditional "AND" of the current policy and
    /// the asynchronous other policy, preserving the single-value policy guarantee. This policy is lifted
    /// into the asynchronous hierarchy via <see cref="ToAsyncSpec" />.
    /// </summary>
    /// <param name="other">The asynchronous policy to evaluate in the event that <c>this</c> policy is satisfied</param>
    /// <returns>
    /// A new <see cref="AsyncPolicyBase{TModel,TMetadata}" /> that will perform the conditional "And"
    /// operation between <c>this</c> and <paramref name="other" /> when the policy is eventually evaluated.
    /// </returns>
    public AsyncPolicyBase<TModel, TMetadata> AndAlso(AsyncPolicyBase<TModel, TMetadata> other) =>
        ToAsyncSpec().AndAlso(other);
```

- [ ] **Step 6: Run to verify green**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: 12/12 PASS, with Task 1's four still green.

- [ ] **Step 7: Commit**

```bash
git add src/Motiv/AndAlso/AsyncAndAlsoPolicy.cs src/Motiv/AndAlso/AsyncAndAlsoSpec.cs src/Motiv/AsyncPolicyBase.cs src/Motiv/PolicyBase.cs src/Motiv.Tests/AndAlsoPolicyTests.cs
git commit -m "feat(andalso): preserve policies across the async surface"
```

---

### Task 5: `ExpressionAndAlsoPolicy` and the expression-tree surface

The fiddliest task. `ExpressionPolicyBase` carries `new` redeclarations whose only purpose is to give inherited overloads equal declaring-type precedence — without them, C# stops overload resolution at the most-derived declaring type and silently picks the wrong one. `ExpressionPolicyBase.cs:218` documents this for `OrElse`.

**Files:**
- Create: `src/Motiv/AndAlso/ExpressionAndAlsoPolicy.cs`
- Modify: `src/Motiv/AndAlso/ExpressionAndAlsoSpec.cs`
- Modify: `src/Motiv/AndAlso/AndAlsoPolicy.cs`, `AsyncAndAlsoPolicy.cs`, `AndAlsoSpec.cs`, `AsyncAndAlsoSpec.cs` (close the deferred predicate entries)
- Modify: `src/Motiv/ExpressionPolicyBase.cs`
- Modify: `src/Motiv.Tests/AndAlsoPolicyTests.cs`

**Interfaces:**
- Consumes: `AndAlsoPolicyResult` (Task 2), `AndAlsoPolicy` (Task 3), `AsyncAndAlsoPolicy` (Task 4).
- Produces: `ExpressionAndAlsoPolicy<TModel, TMetadata>` and `ExpressionPolicyBase<TModel, TMetadata>.AndAlso(ExpressionPolicyBase<TModel, TMetadata>) → ExpressionPolicyBase<TModel, TMetadata>`.

- [ ] **Step 1: Read the `OrElse` region you are mirroring**

Read `src/Motiv/ExpressionPolicyBase.cs` lines 200-265 — the complete `OrElse` overload set — and lines 95-135, the current `AndAlso` set. Write down the difference. The `OrElse` set has a policy-preserving overload and a `new PolicyBase AndAlso(PolicyBase)`-style redeclaration that the `AndAlso` set lacks. Your job is to make the `AndAlso` set structurally identical to the `OrElse` set, substituting `AndAlso`/`&&`/`Expr.AndAlso` throughout.

Do not invent signatures. If the `OrElse` set contains an overload with no `AndAlso` counterpart and no obvious mapping, stop and report NEEDS_CONTEXT rather than guessing.

- [ ] **Step 2: Write the failing test**

Append inside `AndAlsoPolicyTests`:

```csharp
    private static ExpressionPolicyBase<int, string> ExprGate(int threshold, string name) =>
        Spec.From((int n) => n > threshold)
            .WhenTrue($"{name}-true")
            .WhenFalse($"{name}-false")
            .Create(name);

    [Fact]
    public void Should_preserve_the_policy_when_combining_two_expression_propositions()
    {
        // Arrange — 5 > 0 is satisfied, 5 > 10 is not, so the right gate is decisive.
        var composed = ExprGate(0, "above-zero").AndAlso(ExprGate(10, "above-ten"));

        // Act
        var result = composed.Evaluate(5);

        // Assert
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("above-ten-false");
    }

    [Fact]
    public void Should_preserve_the_policy_when_mixing_expression_and_plain_propositions()
    {
        // Arrange
        var composed = ExprGate(0, "above-zero").AndAlso(
            Spec.Build<int>(_ => false).WhenTrue("plain-true").WhenFalse("plain-false").Create("plain"));

        // Act
        var result = composed.Evaluate(5);

        // Assert — degrading from ExpressionPolicyBase to PolicyBase is fine; degrading to a
        // spec is not, so `.Value` must still be reachable.
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("plain-false");
    }
```

- [ ] **Step 3: Run to verify it fails for the right reason**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: compile error on `result.Value` for the expression cases.

- [ ] **Step 4: Create `ExpressionAndAlsoPolicy`**

Create `src/Motiv/AndAlso/ExpressionAndAlsoPolicy.cs`, mirroring `src/Motiv/OrElse/ExpressionOrElsePolicy.cs`:

```csharp
using System.Linq.Expressions;
using Motiv.And;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.AndAlso;

internal sealed class ExpressionAndAlsoPolicy<TModel, TMetadata>(
    ExpressionPolicyBase<TModel, TMetadata> left,
    ExpressionPolicyBase<TModel, TMetadata> right)
    : ExpressionPolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(left, right, Expr.AndAlso));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description => field ??=
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoPolicy<TModel, TMetadata>
                or AndAlsoSpec<TModel, TMetadata> or ExpressionAndSpec<TModel, TMetadata>
                or ExpressionAndAlsoSpec<TModel, TMetadata> or ExpressionAndAlsoPolicy<TModel, TMetadata>);

    public string Operation => Operator.AndAlso;

    public bool IsCollapsable => true;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) && right.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var leftResult = left.EvaluatePolicyInternal(model);
        return leftResult.Satisfied switch
        {
            true => new AndAlsoPolicyResult<TMetadata>(leftResult, right.EvaluatePolicyInternal(model)),
            false => new AndAlsoPolicyResult<TMetadata>(leftResult)
        };
    }

    public PolicyBase<TModel, TMetadata> Left => left;

    public PolicyBase<TModel, TMetadata> Right => right;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Left => left;

    SpecBase<TModel, TMetadata> IBinaryOperationSpec<TModel, TMetadata>.Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
```

- [ ] **Step 5: Close every deferred predicate entry**

Add `or ExpressionAndAlsoPolicy<TModel, TMetadata>` to the collapsible predicate in each of:
- `src/Motiv/AndAlso/AndAlsoPolicy.cs`
- `src/Motiv/AndAlso/AndAlsoSpec.cs`
- `src/Motiv/AndAlso/AsyncAndAlsoPolicy.cs`
- `src/Motiv/AndAlso/AsyncAndAlsoSpec.cs`
- `src/Motiv/AndAlso/ExpressionAndAlsoSpec.cs`

Then run the completion check and confirm the shape matches `OrElse`:

```bash
grep -l "BinarySpecDescription" src/Motiv/AndAlso/*.cs   # must list 6 files
grep -c "ExpressionAndAlsoPolicy" src/Motiv/AndAlso/*.cs # every one of the 6 must be >= 1
```

- [ ] **Step 6: Mirror the `ExpressionPolicyBase` overload set**

Apply the changes you identified in Step 1. At minimum this means changing the existing
`AndAlso(ExpressionPolicyBase<TModel, TMetadata>)` to return `ExpressionPolicyBase<TModel, TMetadata>`
via `new ExpressionAndAlsoPolicy<TModel, TMetadata>(this, other)`, and adding the
`new PolicyBase<TModel, TMetadata> AndAlso(PolicyBase<TModel, TMetadata>)` redeclaration that forwards
to `base.AndAlso`. Copy the XML doc comments from the `OrElse` equivalents, substituting AND for OR —
including the comments explaining *why* the redeclarations exist, since that reasoning is the only
record of a non-obvious constraint.

- [ ] **Step 7: Run to verify green**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: 14/14 PASS, Task 1's four still green.

- [ ] **Step 8: Run the full solution suite**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test
```

Expected: every assembly reports `Failed: 0`. This is the checkpoint that catches a missed predicate entry, since the example projects assert on justification strings.

- [ ] **Step 9: Commit**

```bash
git add src/Motiv/AndAlso/ src/Motiv/ExpressionPolicyBase.cs src/Motiv.Tests/AndAlsoPolicyTests.cs
git commit -m "feat(andalso): preserve policies across the expression-tree surface"
```

---

### Task 6: `AndAlsoTogether`

**Files:**
- Modify: `src/Motiv/PolicyExtensions.cs`
- Modify: `src/Motiv/PolicyResultExtensions.cs`
- Modify: `src/Motiv.Tests/AndAlsoPolicyTests.cs`

**Interfaces:**
- Consumes: `PolicyBase.AndAlso` (Task 3), `PolicyResultBase.AndAlso` (Task 2).
- Produces: `PolicyExtensions.AndAlsoTogether<TModel, TMetadata>(IEnumerable<PolicyBase<TModel, TMetadata>>) → PolicyBase<TModel, TMetadata>` and `PolicyResultExtensions.AndAlsoTogether<TMetadata>(IEnumerable<PolicyResultBase<TMetadata>>) → PolicyResultBase<TMetadata>`.

- [ ] **Step 1: Write the failing test**

Append inside `AndAlsoPolicyTests`:

```csharp
    [Fact]
    public void Should_select_the_first_failing_gate_in_a_chain()
    {
        // Arrange
        var policies = new[] { Gate(true, "a"), Gate(false, "b"), Gate(false, "c") };

        // Act
        var result = policies.AndAlsoTogether().Evaluate("model");

        // Assert — "b" fails first, so "c" is never evaluated and "b" is the value.
        result.Satisfied.ShouldBeFalse();
        result.Value.ShouldBe("b-false");
        result.Values.ShouldBe(["b-false"]);
    }

    [Fact]
    public void Should_flatten_every_cause_of_a_fully_satisfied_chain()
    {
        // Arrange
        var policies = new[] { Gate(true, "a"), Gate(true, "b"), Gate(true, "c") };

        // Act
        var result = policies.AndAlsoTogether().Evaluate("model");

        // Assert
        result.Satisfied.ShouldBeTrue();

        // No gate is decisive when all pass, so Value takes the last evaluated — but Values
        // flattens the left-nested tree and reports all three.
        result.Value.ShouldBe("c-true");
        result.Values.ShouldBe(["a-true", "b-true", "c-true"]);
    }

    [Fact]
    public void Should_combine_policy_results_with_AndAlsoTogether()
    {
        // Arrange
        var results = new[] { Evaluated(true, "a"), Evaluated(false, "b"), Evaluated(true, "c") };

        // Act
        var combined = results.AndAlsoTogether();

        // Assert
        combined.Satisfied.ShouldBeFalse();
        combined.Value.ShouldBe("b-false");
    }
```

- [ ] **Step 2: Run to verify it fails for the right reason**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: compile error — `AndAlsoTogether` is not defined for either collection type.

- [ ] **Step 3: Add `PolicyExtensions.AndAlsoTogether`**

In `src/Motiv/PolicyExtensions.cs`, add before the existing `OrElseTogether`:

```csharp
    /// <summary>
    /// Combines a collection of <see cref="PolicyBase{TModel,TMetadata}" /> whereby the enumeration of the
    /// policies halts at the first policy that is unsatisfied, whose false metadata is returned.  If every
    /// policy is satisfied, the last policy's true metadata is returned.  This is equivalent to combining the
    /// policies using the <see cref="PolicyBase{TModel,TMetadata}.AndAlso(Motiv.PolicyBase{TModel,TMetadata})" />
    /// method.
    /// </summary>
    /// <param name="propositions">The propositions to apply the conditional AND operator to.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TMetadata">The type of the metadata.</typeparam>
    /// <returns>A single policy that represents the conditional AND of all the input propositions.</returns>
    public static PolicyBase<TModel, TMetadata> AndAlsoTogether<TModel, TMetadata>(
        this IEnumerable<PolicyBase<TModel, TMetadata>> propositions) =>
        propositions.Aggregate((leftSpec, rightSpec) => leftSpec.AndAlso(rightSpec));
```

- [ ] **Step 4: Add `PolicyResultExtensions.AndAlsoTogether`**

Read `src/Motiv/PolicyResultExtensions.cs` and add an `AndAlsoTogether` mirroring its existing `OrElseTogether` exactly — same generic parameters, same null-guard behaviour, same XML-doc shape — substituting `AndAlso` for `OrElse`.

- [ ] **Step 5: Run to verify green**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Tests -f net10.0 --filter "FullyQualifiedName~AndAlsoPolicyTests"
```

Expected: 17/17 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Motiv/PolicyExtensions.cs src/Motiv/PolicyResultExtensions.cs src/Motiv.Tests/AndAlsoPolicyTests.cs
git commit -m "feat(andalso): add AndAlsoTogether for policies and policy results"
```

---

### Task 7: Documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify: `docs/operators/AndAlso.md`
- Modify: `docs/superpowers/plans/2026-08-02-policy-preservation-boundary.md`

**Interfaces:**
- Consumes: the behaviour built in Tasks 2-6, and the asserted values in `AndAlsoPolicyTests`.
- Produces: nothing.

- [ ] **Step 1: Update `CLAUDE.md`'s Policy Preservation section**

Find this text (added by the preceding branch):

```
`OrElse` and `Not` implement this today. **`AndAlso` does not yet** — `policy.AndAlso(policy)` still
returns a spec. That is an outstanding gap to be closed on its own branch by mirroring the `OrElse`
policy family (`OrElsePolicy`, `OrElsePolicyResult`, and the async and expression-tree variants),
not a judgement that conjunction is ineligible.
```

Replace it with:

```
`OrElse`, `AndAlso` and `Not` all implement this. `policy.AndAlso(policy)` returns a policy across the
sync, async, expression-tree and result surfaces, as `OrElse` does.
```

Then find the bullet list above it and confirm the `AndAlso` bullet already describes the implemented
semantics ("the first operand that failed, else the final success"). Leave it as-is if so.

Finally, update the three-line summary at the top of the section:

```
### Policy Preservation
- `!policy` returns a policy
- `policy.OrElse(policy)` returns a policy
- All other operations return a spec
```

to:

```
### Policy Preservation
- `!policy` returns a policy
- `policy.OrElse(policy)` and `policy.AndAlso(policy)` return a policy
- All other operations return a spec
```

- [ ] **Step 2: Add a Policies section to `docs/operators/AndAlso.md`**

Read `docs/operators/OrElse.md`'s `### [Policies](xref:Motiv.PolicyBase\`2)` section in full and write the
conjunction equivalent, inserted at the same relative position (after the propositions material, before
the Boolean Results section). It must cover:

- that `AndAlso()` preserves a policy, so a chain yields a single `Value`;
- that when a gate fails, `Value` is that gate's false metadata — first failure wins;
- that when every gate passes, `Value` is the last one's true metadata, and that this is a
  last-evaluated selection rather than a designated value;
- that `Values` reports every contributing cause, and `Causes`/`Underlying` describe the binary
  composition shape instead.

The `csharp` example must use values that match assertions committed in `src/Motiv.Tests/AndAlsoPolicyTests.cs`.
Read that file and copy the values; do not invent them. State in your report which test you drew from.

- [ ] **Step 3: Annotate the superseded plan-doc line** (folded-in follow-up)

`docs/superpowers/plans/2026-08-02-policy-preservation-boundary.md` still quotes, inside its Task 3
step text, the instruction `do not add a policy-preserving AndAlso`. That is now the opposite of what
shipped. Add a short, clearly marked note directly above the block containing it, recording that the
instruction was superseded, naming this branch, and pointing at
`docs/superpowers/specs/2026-08-02-andalso-policy-preservation-design.md`. Do not delete the original
text — it is a historical record of an executed plan.

- [ ] **Step 4: Verify markdown integrity and coherence**

Re-read every modified region. Confirm code fences are balanced and DocFX `` <xref:Motiv.PolicyBase`2> ``
backtick-arity syntax is intact. Confirm nothing anywhere still says `AndAlso` returns a spec:

```bash
grep -rn "AndAlso" CLAUDE.md docs/operators/ docs/live-rules/ | grep -i "spec\|not yet\|outstanding\|do not add"
```

Inspect every hit and confirm each is either correct or intentionally historical.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md docs/operators/AndAlso.md docs/superpowers/plans/2026-08-02-policy-preservation-boundary.md
git commit -m "docs(andalso): record AndAlso as policy-preserving"
```

---

### Task 8: Full-suite verification and review

**Files:** no edits expected. If this task requires edits, an earlier task was incomplete.

- [ ] **Step 1: Run the full solution suite**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test
```

Expected: every assembly reports `Failed: 0`. Ignore the exit code — net472 needs mono, which is absent
on this host, and CI covers that target. If an example project fails on a justification string, that is
almost certainly a missed collapsible-predicate entry — go back to the hazard table rather than editing
the assertion.

- [ ] **Step 2: Confirm the predicate hazard is fully closed**

```bash
grep -l "BinarySpecDescription" src/Motiv/AndAlso/*.cs
grep -l "BinarySpecDescription" src/Motiv/OrElse/*.cs
```

Both must list six files. Then confirm every `AndAlso` carrier names the policy types:

```bash
grep -c "AndAlsoPolicy" src/Motiv/AndAlso/*.cs
```

Every carrier must be ≥ 1. Also confirm `AndAlsoBooleanResultDescription.cs` names `AndAlsoPolicyResult`.

- [ ] **Step 3: Confirm the Task 1 pin was never amended**

```bash
git log -p --follow src/Motiv.Tests/AndAlsoPolicyTests.cs | grep -c "^-.*ExpectedReason\|^-.*ExpectedAssertions"
```

Expected: `0`. Any deletion of those helpers' contents after Task 1 means the regression pin was
weakened to make a later task pass — report it prominently.

- [ ] **Step 4: Run the mandatory code-simplifier review**

Per CLAUDE.md's "Post-Implementation Code Review" convention, spawn a `code-simplifier` agent over the
changed files. Tell it explicitly that the `OrElse`/`AndAlso` duplication is deliberate per CLAUDE.md's
"Avoid over-DRYing" guidance and must not be abstracted away; ask it to focus on naming, comment
quality, and any accidental divergence from the `OrElse` family's structure.

Apply any improvements, re-run the suite, and commit separately if anything changed.
