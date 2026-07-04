# Queryable Expression Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose composed `Expression<Func<TModel, bool>>` from expression-backed propositions so they can be used with query providers (EF Core `IQueryable`), per the approved spec at `docs/superpowers/specs/2026-07-04-queryable-expression-composition-design.md`.

**Architecture:** Two new public abstract types — `ExpressionSpecBase<TModel, TMetadata>` (: `SpecBase`) and `ExpressionPolicyBase<TModel, TMetadata>` (: `PolicyBase`), unified by `IExpressionSpec<TModel>` — with eight internal expression-aware composites that mirror existing composites' evaluation byte-for-byte and lazily recombine child expressions. All bool-predicate `Spec.From` paths produce the new types via decorator-wrapped existing leaf propositions. `ExpressionTreeTransformer` nodes also become expression-backed.

**Tech Stack:** C# (.NET multi-target `net8.0;net9.0;netstandard2.0;net10.0`), xUnit + Shouldly, Converj fluent source generator, EF Core + SQLite (example test project only).

## Global Constraints

- Work on branch `feature/queryable-expression-composition` (already created).
- TDD strictly: failing test → verify failure → minimal code → verify pass → commit. Never write implementation before its test.
- The library targets `netstandard2.0` — **no covariant return types**, no default interface members, no C# 8+ runtime-dependent features in `src/Motiv`.
- `SpecBase` declares `public string Expression => Description.Detailed;`. In any class deriving from `SpecBase`, a bare `Expression.And(...)` resolves to that string property and fails to compile. **In such files add `using Expr = System.Linq.Expressions.Expression;` and use `Expr.*` for static Expression factory calls.** Generic type usages like `Expression<Func<TModel, bool>>` are unaffected (different arity) and stay as-is.
- Evaluation output parity is a hard requirement: `Reason`, `Justification`, and `Assertions` from expression-backed composition must be byte-identical to the equivalent ordinary-spec composition.
- Tests live in `src/Motiv.Tests` (xUnit, global usings `Xunit` + `Shouldly`, Arrange/Act/Assert comments, `ShouldBe`/`ShouldBeSameAs` style). Internal types are constructible from tests via `InternalsVisibleTo`.
- Dev-loop test command (fast, single TFM): `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~<TestClassName>"`. Expected: relevant tests pass.
- Full suite (run at every task's final verification step and before completion): `dotnet test src/Motiv.Tests/Motiv.Tests.csproj`. Example projects (Poker/ECommerce/SmartHome) must also pass when justification output could be affected: `dotnet test Motiv.slnx`.
- XML doc headers on all public methods/types, with `<param>` and `<returns>`.
- Commit after each green test cycle. Commit messages end with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- Do NOT rename, remove, or change the signatures of any existing public member. All changes are additive or return-type-narrowing to a subtype (allowed pre-v8 release).

## File Structure

New production files (all in `src/Motiv`):

| File | Responsibility |
|---|---|
| `IExpressionSpec.cs` | `IExpressionSpec<TModel>` — the `ToExpression()` contract |
| `ExpressionSpecBase.cs` | Abstract expression-backed spec; closed composition surface |
| `ExpressionPolicyBase.cs` | Abstract expression-backed policy; policy-preserving `Not`/`OrElse` |
| `ExpressionTreeProposition/ExpressionSpecDecorator.cs` | Wraps any spec + expression → `ExpressionSpecBase` |
| `ExpressionTreeProposition/ExpressionPolicyDecorator.cs` | Wraps any policy + expression → `ExpressionPolicyBase` |
| `ExpressionTreeProposition/ParameterReplacementVisitor.cs` | Rebinds a `ParameterExpression` in an expression body |
| `ExpressionTreeProposition/ExpressionComposer.cs` | Combines/negates child expressions into one lambda |
| `And/ExpressionAndSpec.cs`, `AndAlso/ExpressionAndAlsoSpec.cs`, `Or/ExpressionOrSpec.cs`, `OrElse/ExpressionOrElseSpec.cs`, `XOr/ExpressionXOrSpec.cs` | Binary expression composites (mirror ordinary counterparts) |
| `Not/ExpressionNotSpec.cs`, `Not/ExpressionNotPolicy.cs`, `OrElse/ExpressionOrElsePolicy.cs` | Negation + policy-preserving composites |
| `QueryableExtensions.cs` | `IQueryable<TModel>.Where(IExpressionSpec<TModel>)` |
| `ExpressionTreeProposition/PropositionBuilders/Boolean*Factory.cs` (×9) | Bool-specific `Spec.From` factory twins |
| `HigherOrderProposition/PropositionBuilders/**/Boolean*Factory.cs` (×8) | Bool twins for higher-order `From` chains |

Modified production files: the five ordinary binary composites + `OrElsePolicy` (collapse predicates only), `ExpressionTreeExtensions.cs`, `ExpressionTreeProposition/ExpressionTreeTransformer.cs`.

New test files in `src/Motiv.Tests`: `ExpressionSpecDecoratorTests.cs`, `ExpressionPolicyDecoratorTests.cs`, `ExpressionComposerTests.cs`, `ExpressionSpecCompositionTests.cs`, `ExpressionSpecNegationTests.cs`, `ExpressionSpecMixedMetadataTests.cs`, `ExpressionSpecBuilderTests.cs`, `ExpressionTreeToSpecExpressionTests.cs`, `QueryableExtensionsTests.cs`. New example project: `src/examples/Motiv.EntityFramework.Tests`.

---

### Task 1: Core abstractions — `IExpressionSpec<TModel>`, `ExpressionSpecBase<TModel, TMetadata>`, `ExpressionSpecDecorator`

**Files:**
- Create: `src/Motiv/IExpressionSpec.cs`
- Create: `src/Motiv/ExpressionSpecBase.cs`
- Create: `src/Motiv/ExpressionTreeProposition/ExpressionSpecDecorator.cs`
- Test: `src/Motiv.Tests/ExpressionSpecDecoratorTests.cs`

**Interfaces:**
- Consumes: `SpecBase<TModel, TMetadata>` (existing, `src/Motiv/SpecBase.cs`), `ISpecDescription`.
- Produces: `IExpressionSpec<TModel>` with `Expression<Func<TModel, bool>> ToExpression();` `ExpressionSpecBase<TModel, TMetadata> : SpecBase<TModel, TMetadata>, IExpressionSpec<TModel>` with `internal` ctor and `public abstract Expression<Func<TModel, bool>> ToExpression()`. `internal sealed class ExpressionSpecDecorator<TModel, TMetadata>(SpecBase<TModel, TMetadata> underlyingSpec, Expression<Func<TModel, bool>> expression) : ExpressionSpecBase<TModel, TMetadata>`. Later tasks add composition members to `ExpressionSpecBase`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionSpecDecoratorTests.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionSpecDecoratorTests
{
    [Fact]
    public void Should_return_the_same_expression_instance_that_was_supplied()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Act
        var act = sut.ToExpression();

        // Assert
        act.ShouldBeSameAs(expression);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(4, true)]
    public void Should_forward_evaluation_to_the_underlying_spec(int model, bool expected)
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Satisfied.ShouldBe(expected);
        act.Reason.ShouldBe(inner.Evaluate(model).Reason);
        act.Assertions.ShouldBe(inner.Evaluate(model).Assertions);
    }

    [Fact]
    public void Should_forward_description_and_underlying_to_the_underlying_spec()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Act & Assert
        sut.Description.ShouldBeSameAs(inner.Description);
        sut.Underlying.ShouldBe(inner.Underlying);
        sut.Matches(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_be_assignable_to_both_spec_base_and_expression_spec()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3).Create("is greater than three");

        // Act
        var sut = new ExpressionSpecDecorator<int, string>(inner, expression);

        // Assert
        sut.ShouldBeAssignableTo<SpecBase<int, string>>();
        sut.ShouldBeAssignableTo<IExpressionSpec<int>>();
        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecDecoratorTests"`
Expected: FAIL to compile — `IExpressionSpec`, `ExpressionSpecBase`, `ExpressionSpecDecorator` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Motiv/IExpressionSpec.cs
using System.Linq.Expressions;

namespace Motiv;

/// <summary>
/// Represents a proposition that retains a recoverable predicate expression tree, suitable for use
/// with query providers (e.g. <see cref="IQueryable{T}"/> translation).
/// </summary>
/// <typeparam name="TModel">The model type that the proposition evaluates against.</typeparam>
public interface IExpressionSpec<TModel>
{
    /// <summary>Gets the predicate expression tree that this proposition represents.</summary>
    /// <returns>The predicate lambda expression describing this proposition.</returns>
    Expression<Func<TModel, bool>> ToExpression();
}
```

```csharp
// src/Motiv/ExpressionSpecBase.cs
using System.Linq.Expressions;

namespace Motiv;

/// <summary>
/// The base class for propositions that retain a recoverable predicate expression tree. Composing
/// instances of this type (or <see cref="ExpressionPolicyBase{TModel,TMetadata}"/>) with the logical
/// operators yields propositions that are themselves expression-backed, so the composed expression
/// can be recovered via <see cref="ToExpression"/> and used with query providers.
/// </summary>
/// <typeparam name="TModel">The model type that the proposition evaluates against.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
public abstract class ExpressionSpecBase<TModel, TMetadata> : SpecBase<TModel, TMetadata>, IExpressionSpec<TModel>
{
    /// <summary>Prevents external instantiation of the <see cref="ExpressionSpecBase{TModel,TMetadata}"/> class.</summary>
    internal ExpressionSpecBase()
    {
    }

    /// <summary>Gets the predicate expression tree that this proposition represents.</summary>
    /// <returns>The predicate lambda expression describing this proposition.</returns>
    public abstract Expression<Func<TModel, bool>> ToExpression();
}
```

```csharp
// src/Motiv/ExpressionTreeProposition/ExpressionSpecDecorator.cs
using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionSpecDecorator<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> underlyingSpec,
    Expression<Func<TModel, bool>> expression)
    : ExpressionSpecBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingSpec.Underlying;

    public override ISpecDescription Description => underlyingSpec.Description;

    public override Expression<Func<TModel, bool>> ToExpression() => expression;

    public override bool Matches(TModel model) => underlyingSpec.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        underlyingSpec.Evaluate(model);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecDecoratorTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Verify no regressions and commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: all tests pass.

```bash
git add src/Motiv/IExpressionSpec.cs src/Motiv/ExpressionSpecBase.cs src/Motiv/ExpressionTreeProposition/ExpressionSpecDecorator.cs src/Motiv.Tests/ExpressionSpecDecoratorTests.cs
git commit -m "feat: add IExpressionSpec, ExpressionSpecBase and ExpressionSpecDecorator"
```

---

### Task 2: `ExpressionPolicyBase<TModel, TMetadata>` and `ExpressionPolicyDecorator`

**Files:**
- Create: `src/Motiv/ExpressionPolicyBase.cs`
- Create: `src/Motiv/ExpressionTreeProposition/ExpressionPolicyDecorator.cs`
- Test: `src/Motiv.Tests/ExpressionPolicyDecoratorTests.cs`

**Interfaces:**
- Consumes: `PolicyBase<TModel, TMetadata>` (`src/Motiv/PolicyBase.cs`), `IExpressionSpec<TModel>` (Task 1).
- Produces: `ExpressionPolicyBase<TModel, TMetadata> : PolicyBase<TModel, TMetadata>, IExpressionSpec<TModel>` with `internal` ctor and `public abstract Expression<Func<TModel, bool>> ToExpression()`. `internal sealed class ExpressionPolicyDecorator<TModel, TMetadata>(PolicyBase<TModel, TMetadata> underlyingPolicy, Expression<Func<TModel, bool>> expression) : ExpressionPolicyBase<TModel, TMetadata>`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionPolicyDecoratorTests.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionPolicyDecoratorTests
{
    [Fact]
    public void Should_return_the_same_expression_instance_that_was_supplied()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Act
        var act = sut.ToExpression();

        // Assert
        act.ShouldBeSameAs(expression);
    }

    [Theory]
    [InlineData(2, false, "is not greater than three")]
    [InlineData(4, true, "is greater than three")]
    public void Should_forward_policy_evaluation_to_the_underlying_policy(int model, bool expected, string expectedAssertion)
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Act
        var act = sut.Evaluate(model);

        // Assert
        act.Satisfied.ShouldBe(expected);
        act.Value.ShouldBe(expectedAssertion);
        act.Reason.ShouldBe(inner.Evaluate(model).Reason);
    }

    [Fact]
    public void Should_be_assignable_to_policy_base_and_expression_spec()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;
        var inner = Spec.Build((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();

        // Act
        var sut = new ExpressionPolicyDecorator<int, string>(inner, expression);

        // Assert
        sut.ShouldBeAssignableTo<PolicyBase<int, string>>();
        sut.ShouldBeAssignableTo<IExpressionSpec<int>>();
        sut.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionPolicyDecoratorTests"`
Expected: FAIL to compile — `ExpressionPolicyBase`, `ExpressionPolicyDecorator` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Motiv/ExpressionPolicyBase.cs
using System.Linq.Expressions;

namespace Motiv;

/// <summary>
/// The base class for policies that retain a recoverable predicate expression tree. A policy resolves
/// to a single assertion or metadata value per evaluation, and this variant additionally allows the
/// composed predicate expression to be recovered via <see cref="ToExpression"/> for use with query
/// providers.
/// </summary>
/// <typeparam name="TModel">The model type that the policy evaluates against.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the policy.</typeparam>
public abstract class ExpressionPolicyBase<TModel, TMetadata> : PolicyBase<TModel, TMetadata>, IExpressionSpec<TModel>
{
    /// <summary>Prevents external instantiation of the <see cref="ExpressionPolicyBase{TModel,TMetadata}"/> class.</summary>
    internal ExpressionPolicyBase()
    {
    }

    /// <summary>Gets the predicate expression tree that this policy represents.</summary>
    /// <returns>The predicate lambda expression describing this policy.</returns>
    public abstract Expression<Func<TModel, bool>> ToExpression();
}
```

```csharp
// src/Motiv/ExpressionTreeProposition/ExpressionPolicyDecorator.cs
using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ExpressionPolicyDecorator<TModel, TMetadata>(
    PolicyBase<TModel, TMetadata> underlyingPolicy,
    Expression<Func<TModel, bool>> expression)
    : ExpressionPolicyBase<TModel, TMetadata>
{
    public override IEnumerable<SpecBase> Underlying => underlyingPolicy.Underlying;

    public override ISpecDescription Description => underlyingPolicy.Description;

    public override Expression<Func<TModel, bool>> ToExpression() => expression;

    public override bool Matches(TModel model) => underlyingPolicy.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        underlyingPolicy.Evaluate(model);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionPolicyDecoratorTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Verify no regressions and commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: all tests pass.

```bash
git add src/Motiv/ExpressionPolicyBase.cs src/Motiv/ExpressionTreeProposition/ExpressionPolicyDecorator.cs src/Motiv.Tests/ExpressionPolicyDecoratorTests.cs
git commit -m "feat: add ExpressionPolicyBase and ExpressionPolicyDecorator"
```

---

### Task 3: `ParameterReplacementVisitor` and `ExpressionComposer`

**Files:**
- Create: `src/Motiv/ExpressionTreeProposition/ParameterReplacementVisitor.cs`
- Create: `src/Motiv/ExpressionTreeProposition/ExpressionComposer.cs`
- Test: `src/Motiv.Tests/ExpressionComposerTests.cs`

**Interfaces:**
- Consumes: `IExpressionSpec<TModel>` (Task 1), `ExpressionSpecDecorator` (Task 1, used as a convenient concrete `IExpressionSpec` in tests).
- Produces:
  - `internal static Expression ParameterReplacementVisitor.Replace(Expression body, ParameterExpression original, ParameterExpression replacement)`
  - `internal static Expression<Func<TModel, bool>> ExpressionComposer.Combine<TModel>(IExpressionSpec<TModel> left, IExpressionSpec<TModel> right, Func<Expression, Expression, BinaryExpression> combineOperands)`
  - `internal static Expression<Func<TModel, bool>> ExpressionComposer.Negate<TModel>(IExpressionSpec<TModel> operand)`

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionComposerTests.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionComposerTests
{
    private static ExpressionSpecDecorator<int, string> CreateExpressionSpec(
        Expression<Func<int, bool>> expression, string statement) =>
        new(Spec.Build(expression.Compile()).Create(statement), expression);

    [Theory]
    [InlineData(4, true)]   // even and > 3
    [InlineData(2, false)]  // even but not > 3
    [InlineData(5, false)]  // > 3 but odd
    public void Should_combine_two_expressions_with_a_shared_parameter(int model, bool expected)
    {
        // Arrange
        var left = CreateExpressionSpec(n => n % 2 == 0, "is even");
        var right = CreateExpressionSpec(n => n > 3, "is greater than three");

        // Act
        var act = ExpressionComposer.Combine(left, right, Expression.AndAlso);

        // Assert
        act.Body.NodeType.ShouldBe(ExpressionType.AndAlso);
        act.Parameters.Count.ShouldBe(1);
        act.Compile()(model).ShouldBe(expected);
    }

    [Fact]
    public void Should_rebind_the_right_operand_parameter_to_the_left_operand_parameter()
    {
        // Arrange
        var left = CreateExpressionSpec(a => a % 2 == 0, "is even");
        var right = CreateExpressionSpec(b => b > 3, "is greater than three");

        // Act
        var act = ExpressionComposer.Combine(left, right, Expression.And);

        // Assert — compilation fails if the right body still references its original parameter
        act.Compile()(4).ShouldBeTrue();
        act.Parameters[0].ShouldBeSameAs(left.ToExpression().Parameters[0]);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(3, true)]
    public void Should_negate_an_expression(int model, bool expected)
    {
        // Arrange
        var operand = CreateExpressionSpec(n => n % 2 == 0, "is even");

        // Act
        var act = ExpressionComposer.Negate(operand);

        // Assert
        act.Body.NodeType.ShouldBe(ExpressionType.Not);
        act.Compile()(model).ShouldBe(expected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionComposerTests"`
Expected: FAIL to compile — `ExpressionComposer`, `ParameterReplacementVisitor` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Motiv/ExpressionTreeProposition/ParameterReplacementVisitor.cs
using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal sealed class ParameterReplacementVisitor(
    ParameterExpression original,
    ParameterExpression replacement) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node) =>
        node == original ? replacement : base.VisitParameter(node);

    internal static Expression Replace(
        Expression body,
        ParameterExpression original,
        ParameterExpression replacement) =>
        new ParameterReplacementVisitor(original, replacement).Visit(body);
}
```

```csharp
// src/Motiv/ExpressionTreeProposition/ExpressionComposer.cs
using System.Linq.Expressions;

namespace Motiv.ExpressionTreeProposition;

internal static class ExpressionComposer
{
    internal static Expression<Func<TModel, bool>> Combine<TModel>(
        IExpressionSpec<TModel> left,
        IExpressionSpec<TModel> right,
        Func<Expression, Expression, BinaryExpression> combineOperands)
    {
        var leftExpression = left.ToExpression();
        var rightExpression = right.ToExpression();
        var parameter = leftExpression.Parameters[0];
        var rightBody = ParameterReplacementVisitor.Replace(
            rightExpression.Body,
            rightExpression.Parameters[0],
            parameter);

        return Expression.Lambda<Func<TModel, bool>>(
            combineOperands(leftExpression.Body, rightBody),
            parameter);
    }

    internal static Expression<Func<TModel, bool>> Negate<TModel>(IExpressionSpec<TModel> operand)
    {
        var operandExpression = operand.ToExpression();

        return Expression.Lambda<Func<TModel, bool>>(
            Expression.Not(operandExpression.Body),
            operandExpression.Parameters[0]);
    }
}
```

Note: `ExpressionComposer` and `ParameterReplacementVisitor` do not derive from `SpecBase`, so bare `Expression.*` factory calls are unambiguous here — no alias needed.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionComposerTests"`
Expected: PASS (6 test cases).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv/ExpressionTreeProposition/ParameterReplacementVisitor.cs src/Motiv/ExpressionTreeProposition/ExpressionComposer.cs src/Motiv.Tests/ExpressionComposerTests.cs
git commit -m "feat: add expression recombination primitives"
```

---
### Task 4: `ExpressionAndSpec` and closed `And` composition

**Files:**
- Create: `src/Motiv/And/ExpressionAndSpec.cs`
- Modify: `src/Motiv/ExpressionSpecBase.cs` (add `And` overloads + `&` operators)
- Modify: `src/Motiv/And/AndSpec.cs:20-21` (extend collapse predicate)
- Test: `src/Motiv.Tests/ExpressionSpecCompositionTests.cs`

**Interfaces:**
- Consumes: `ExpressionComposer.Combine` (Task 3), `BinarySpecDescription<TModel, TMetadata>` (`src/Motiv/Shared`), `Operator.And` (`src/Motiv/Traversal`), `IBinaryOperationSpec*` interfaces, `ExpressionPolicyBase` (Task 2).
- Produces: `internal sealed class ExpressionAndSpec<TModel, TMetadata>(SpecBase<TModel, TMetadata> left, SpecBase<TModel, TMetadata> right, IExpressionSpec<TModel> leftExpression, IExpressionSpec<TModel> rightExpression) : ExpressionSpecBase<TModel, TMetadata>, IBinaryOperationSpec<TModel, TMetadata>, IBinaryOperationSpec<TModel>, IBinaryOperationSpec`. On `ExpressionSpecBase`: `public ExpressionSpecBase<TModel, TMetadata> And(ExpressionSpecBase<TModel, TMetadata> spec)`, `public ExpressionSpecBase<TModel, TMetadata> And(ExpressionPolicyBase<TModel, TMetadata> spec)`, and `operator &` for the operand pairs (ESB,ESB), (ESB,EPB), (EPB,ESB). The 4-arg composite constructor pattern (evaluation operands + expression sources, same objects passed twice) is reused by Tasks 5–7.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionSpecCompositionTests.cs
using System.Linq.Expressions;

namespace Motiv.Tests;

public class ExpressionSpecCompositionTests
{
    private static readonly ExpressionSpecBase<int, string> IsEven =
        new Motiv.ExpressionTreeProposition.ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static readonly ExpressionSpecBase<int, string> IsPositive =
        new Motiv.ExpressionTreeProposition.ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n > 0).Create("is positive"),
            n => n > 0);

    [Fact]
    public void Should_produce_an_expression_spec_when_anding_two_expression_specs()
    {
        // Act
        var act = IsEven.And(IsPositive);

        // Assert
        act.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        act.ToExpression().Body.NodeType.ShouldBe(ExpressionType.And);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void Should_compile_an_and_expression_that_agrees_with_spec_evaluation(int model)
    {
        // Arrange
        var sut = IsEven.And(IsPositive);

        // Act
        var act = sut.ToExpression().Compile()(model);

        // Assert
        act.ShouldBe(sut.Matches(model));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void Should_produce_identical_explanations_to_ordinary_and_composition(int model)
    {
        // Arrange — upcasting forces the ordinary SpecBase overload to bind
        SpecBase<int, string> ordinaryLeft = IsEven;
        SpecBase<int, string> ordinaryRight = IsPositive;
        var ordinary = ordinaryLeft.And(ordinaryRight);
        var sut = IsEven.And(IsPositive);

        // Act
        var act = sut.Evaluate(model);
        var expected = ordinary.Evaluate(model);

        // Assert
        act.Reason.ShouldBe(expected.Reason);
        act.Assertions.ShouldBe(expected.Assertions);
        act.Justification.ShouldBe(expected.Justification);
    }

    [Fact]
    public void Should_memoize_the_composed_expression()
    {
        // Arrange
        var sut = IsEven.And(IsPositive);

        // Act & Assert
        sut.ToExpression().ShouldBeSameAs(sut.ToExpression());
    }

    [Fact]
    public void Should_degrade_to_an_ordinary_spec_when_anding_with_a_non_expression_spec()
    {
        // Arrange
        var ordinary = Spec.Build((int n) => n > 100).Create("is large");

        // Act
        var act = IsEven.And(ordinary);

        // Assert
        (act is IExpressionSpec<int>).ShouldBeFalse();
    }

    [Fact]
    public void Should_produce_an_expression_spec_when_using_the_amp_operator()
    {
        // Act
        var act = IsEven & IsPositive;

        // Assert
        act.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        act.ToExpression().Compile()(4).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecCompositionTests"`
Expected: FAIL — `IsEven.And(IsPositive)` binds to the base `SpecBase` overload, so `ShouldBeAssignableTo<ExpressionSpecBase<int, string>>()` fails (and `ToExpression` is not available on the result).

- [ ] **Step 3: Write minimal implementation**

Create the composite (mirror of `AndSpec` at `src/Motiv/And/AndSpec.cs`, plus the expression axis):

```csharp
// src/Motiv/And/ExpressionAndSpec.cs
using System.Linq.Expressions;
using Motiv.AndAlso;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.And;

internal sealed class ExpressionAndSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    IExpressionSpec<TModel> leftExpression,
    IExpressionSpec<TModel> rightExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.And));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&", Operator.And,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata>);

    public string Operation => Operator.And;

    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) & right.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var leftResult = left.Evaluate(model);
        var rightResult = right.Evaluate(model);

        return leftResult.And(rightResult);
    }
}
```

Note: Task 5 creates `ExpressionAndAlsoSpec`; when it exists, extend this collapse predicate with `or ExpressionAndAlsoSpec<TModel, TMetadata>`.

Add to `src/Motiv/ExpressionSpecBase.cs` (inside the class; add `using Motiv.And;` at the top):

```csharp
    /// <summary>
    /// Combines this proposition with another expression-backed proposition using the logical AND
    /// operator. The result is itself expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <returns>An expression-backed proposition representing the logical AND of the two propositions.</returns>
    public ExpressionSpecBase<TModel, TMetadata> And(ExpressionSpecBase<TModel, TMetadata> spec) =>
        new ExpressionAndSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <inheritdoc cref="And(ExpressionSpecBase{TModel, TMetadata})"/>
    public ExpressionSpecBase<TModel, TMetadata> And(ExpressionPolicyBase<TModel, TMetadata> spec) =>
        new ExpressionAndSpec<TModel, TMetadata>(this, spec, this, spec);

    /// <summary>Combines two expression-backed propositions using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>An expression-backed proposition representing the logical AND of the two propositions.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <inheritdoc cref="op_BitwiseAnd(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionSpecBase<TModel, TMetadata> left,
        ExpressionPolicyBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <inheritdoc cref="op_BitwiseAnd(ExpressionSpecBase{TModel, TMetadata}, ExpressionSpecBase{TModel, TMetadata})"/>
    public static ExpressionSpecBase<TModel, TMetadata> operator &(
        ExpressionPolicyBase<TModel, TMetadata> left,
        ExpressionSpecBase<TModel, TMetadata> right) =>
        new ExpressionAndSpec<TModel, TMetadata>(left, right, left, right);
```

Extend the collapse predicate in `src/Motiv/And/AndSpec.cs` so mixed ordinary/expression nesting serializes identically:

```csharp
    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&", Operator.And,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata>);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecCompositionTests"`
Expected: PASS.

- [ ] **Step 5: Verify no regressions and commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: all tests pass (the `AndSpec` predicate change is behavior-preserving for existing types).

```bash
git add src/Motiv/And/ExpressionAndSpec.cs src/Motiv/ExpressionSpecBase.cs src/Motiv/And/AndSpec.cs src/Motiv.Tests/ExpressionSpecCompositionTests.cs
git commit -m "feat: add ExpressionAndSpec and closed And composition for expression specs"
```

---

### Task 5: Remaining binary composites (`AndAlso`, `Or`, `OrElse`, `XOr`) and `ExpressionPolicyBase` binary methods

**Files:**
- Create: `src/Motiv/AndAlso/ExpressionAndAlsoSpec.cs`, `src/Motiv/Or/ExpressionOrSpec.cs`, `src/Motiv/OrElse/ExpressionOrElseSpec.cs`, `src/Motiv/XOr/ExpressionXOrSpec.cs`
- Modify: `src/Motiv/ExpressionSpecBase.cs`, `src/Motiv/ExpressionPolicyBase.cs` (composition methods + operators)
- Modify: `src/Motiv/And/ExpressionAndSpec.cs`, `src/Motiv/And/AndSpec.cs`, `src/Motiv/AndAlso/AndAlsoSpec.cs:20-21`, `src/Motiv/Or/OrSpec.cs:20-21`, `src/Motiv/OrElse/OrElseSpec.cs:20-21`, `src/Motiv/OrElse/OrElsePolicy.cs:20-21` (collapse predicates)
- Test: `src/Motiv.Tests/ExpressionSpecCompositionTests.cs` (extend)

**Interfaces:**
- Consumes: `ExpressionComposer.Combine`, `AndAlsoBooleanResult<TMetadata>` (`src/Motiv/AndAlso`), `OrElseBooleanResult<TMetadata>` (`src/Motiv/OrElse`), existing composites as structural templates.
- Produces: four internal composites with the same 4-arg constructor shape as `ExpressionAndSpec`. On both `ExpressionSpecBase` and `ExpressionPolicyBase`: `AndAlso`, `Or`, `OrElse`, `XOr` (two overloads each: `ExpressionSpecBase<TModel, TMetadata>` and `ExpressionPolicyBase<TModel, TMetadata>` parameters), returning `ExpressionSpecBase<TModel, TMetadata>`; operators `|` and `^` for the operand pairs (ESB,ESB), (ESB,EPB), (EPB,ESB), (EPB,EPB); `&` for (EPB,EPB). Note: `ExpressionPolicyBase.OrElse(ExpressionPolicyBase)` is NOT added here — Task 6 adds the policy-preserving variant.

- [ ] **Step 1: Write the failing tests** (append to `ExpressionSpecCompositionTests.cs`)

```csharp
    public static TheoryData<int> Models => new() { 4, 3, -2, -3, 0 };

    [Theory]
    [MemberData(nameof(Models))]
    public void Should_compose_and_also_with_expression_parity(int model)
    {
        // Arrange
        var sut = IsEven.AndAlso(IsPositive);
        SpecBase<int, string> l = IsEven; SpecBase<int, string> r = IsPositive;
        var ordinary = l.AndAlso(r);

        // Act & Assert
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.AndAlso);
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Should_compose_or_with_expression_parity(int model)
    {
        // Arrange
        var sut = IsEven.Or(IsPositive);
        SpecBase<int, string> l = IsEven; SpecBase<int, string> r = IsPositive;
        var ordinary = l.Or(r);

        // Act & Assert
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.Or);
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Should_compose_or_else_with_expression_parity(int model)
    {
        // Arrange
        var sut = IsEven.OrElse(IsPositive);
        SpecBase<int, string> l = IsEven; SpecBase<int, string> r = IsPositive;
        var ordinary = l.OrElse(r);

        // Act & Assert
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.OrElse);
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Should_compose_xor_with_expression_parity(int model)
    {
        // Arrange
        var sut = IsEven.XOr(IsPositive);
        SpecBase<int, string> l = IsEven; SpecBase<int, string> r = IsPositive;
        var ordinary = l.XOr(r);

        // Act & Assert
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.ExclusiveOr);
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    [Fact]
    public void Should_keep_operator_forms_closed_over_expression_specs()
    {
        // Act & Assert
        (IsEven | IsPositive).ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        (IsEven ^ IsPositive).ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
    }

    [Fact]
    public void Should_collapse_nested_and_reasons_identically_to_ordinary_specs()
    {
        // Arrange — three-way AND exercises the collapse predicate across families
        var sut = IsEven.And(IsPositive).And(IsEven);
        SpecBase<int, string> l = IsEven; SpecBase<int, string> r = IsPositive;
        var ordinary = l.And(r).And(l);

        // Act & Assert
        sut.Evaluate(4).Reason.ShouldBe(ordinary.Evaluate(4).Reason);
        sut.Evaluate(3).Justification.ShouldBe(ordinary.Evaluate(3).Justification);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecCompositionTests"`
Expected: FAIL — the new methods bind to base overloads; `ToExpression` unavailable / type assertions fail.

- [ ] **Step 3: Write the four composites**

Each mirrors its ordinary counterpart exactly (same description, same evaluation) plus the expression axis. Full code:

```csharp
// src/Motiv/AndAlso/ExpressionAndAlsoSpec.cs
using System.Linq.Expressions;
using Motiv.And;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.AndAlso;

internal sealed class ExpressionAndAlsoSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    IExpressionSpec<TModel> leftExpression,
    IExpressionSpec<TModel> rightExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.AndAlso));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "&&", Operator.AndAlso,
            operand => operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata>
                or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>);

    public string Operation => Operator.AndAlso;

    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) && right.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var leftResult = left.Evaluate(model);
        return leftResult.Satisfied switch
        {
            true => new AndAlsoBooleanResult<TMetadata>(
                leftResult,
                right.Evaluate(model)),
            false => new AndAlsoBooleanResult<TMetadata>(leftResult)
        };
    }
}
```

```csharp
// src/Motiv/Or/ExpressionOrSpec.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.OrElse;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.Or;

internal sealed class ExpressionOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    IExpressionSpec<TModel> leftExpression,
    IExpressionSpec<TModel> rightExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.Or));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "|", Operator.Or,
            operand => operand is OrSpec<TModel, TMetadata> or OrElseSpec<TModel, TMetadata>
                or OrElsePolicy<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
                or ExpressionOrElseSpec<TModel, TMetadata>);

    public string Operation => Operator.Or;

    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) | right.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var leftResult = left.Evaluate(model);
        var rightResult = right.Evaluate(model);

        return leftResult.Or(rightResult);
    }
}
```

```csharp
// src/Motiv/OrElse/ExpressionOrElseSpec.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Or;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.OrElse;

internal sealed class ExpressionOrElseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    IExpressionSpec<TModel> leftExpression,
    IExpressionSpec<TModel> rightExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.OrElse));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "||", Operator.OrElse,
            operand => operand is OrSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata>
                or OrElseSpec<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
                or ExpressionOrElseSpec<TModel, TMetadata>);

    public string Operation => Operator.OrElse;

    public bool IsCollapsable => true;

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) || right.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var leftResult = left.Evaluate(model);
        return leftResult.Satisfied switch
        {
            true => new OrElseBooleanResult<TMetadata>(leftResult),
            false => new OrElseBooleanResult<TMetadata>(
                leftResult,
                right.Evaluate(model))
        };
    }
}
```

```csharp
// src/Motiv/XOr/ExpressionXOrSpec.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.XOr;

internal sealed class ExpressionXOrSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right,
    IExpressionSpec<TModel> leftExpression,
    IExpressionSpec<TModel> rightExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(leftExpression, rightExpression, Expr.ExclusiveOr));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "^", Operator.XOr,
            operand => operand is XOrSpec<TModel, TMetadata> or ExpressionXOrSpec<TModel, TMetadata>);

    public string Operation => Operator.XOr;

    public bool IsCollapsable => false;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) ^ right.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model)
    {
        var leftResult = left.Evaluate(model);
        var rightResult = right.Evaluate(model);

        return leftResult.XOr(rightResult);
    }

    public SpecBase<TModel, TMetadata> Left => left;

    public SpecBase<TModel, TMetadata> Right => right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Right => Right;

    SpecBase<TModel> IBinaryOperationSpec<TModel>.Left => Left;

    SpecBase IBinaryOperationSpec.Right => Right;

    SpecBase IBinaryOperationSpec.Left => Left;
}
```

- [ ] **Step 4: Add composition members to the abstract bases**

To `src/Motiv/ExpressionSpecBase.cs` (add `using Motiv.AndAlso; using Motiv.Or; using Motiv.OrElse; using Motiv.XOr;`), following the exact pattern of the Task 4 `And` members, add for each of `AndAlso` (→ `ExpressionAndAlsoSpec`), `Or` (→ `ExpressionOrSpec`), `OrElse` (→ `ExpressionOrElseSpec`), `XOr` (→ `ExpressionXOrSpec`):
- `public ExpressionSpecBase<TModel, TMetadata> <Method>(ExpressionSpecBase<TModel, TMetadata> spec) => new Expression<Method>Spec<TModel, TMetadata>(this, spec, this, spec);`
- `public ExpressionSpecBase<TModel, TMetadata> <Method>(ExpressionPolicyBase<TModel, TMetadata> spec) => new Expression<Method>Spec<TModel, TMetadata>(this, spec, this, spec);`
- operators `|` (→ `Or`) and `^` (→ `XOr`) for operand pairs (ESB,ESB), (ESB,EPB), (EPB,ESB) — same three-signature shape as Task 4's `&`.
- XML doc headers on every member, mirroring the wording style of `SpecBase` (`src/Motiv/SpecBase.cs:207-251`) including the short-circuiting remarks for `AndAlso`/`OrElse`.

To `src/Motiv/ExpressionPolicyBase.cs` (same usings plus `using Motiv.And;`), add:
- `And`, `AndAlso`, `Or`, `XOr` — each with the two overloads above, returning `ExpressionSpecBase<TModel, TMetadata>` (constructed as `new Expression<Method>Spec<TModel, TMetadata>(this, spec, this, spec)`).
- `OrElse(ExpressionSpecBase<TModel, TMetadata> spec)` → `ExpressionOrElseSpec` (the policy-preserving `OrElse(ExpressionPolicyBase)` overload is Task 6).
- operators for (EPB,EPB): `&` → `new ExpressionAndSpec<TModel, TMetadata>(left, right, left, right)`, `|` → `ExpressionOrSpec`, `^` → `ExpressionXOrSpec`.

- [ ] **Step 5: Extend collapse predicates in existing composites**

Apply these exact predicate replacements (behavior-preserving for existing types):
- `src/Motiv/And/AndSpec.cs` and `src/Motiv/And/ExpressionAndSpec.cs`: predicate becomes `operand is AndSpec<TModel, TMetadata> or AndAlsoSpec<TModel, TMetadata> or ExpressionAndSpec<TModel, TMetadata> or ExpressionAndAlsoSpec<TModel, TMetadata>`
- `src/Motiv/AndAlso/AndAlsoSpec.cs`: same predicate as above.
- `src/Motiv/Or/OrSpec.cs`: `operand is OrSpec<TModel, TMetadata> or OrElseSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata> or ExpressionOrElseSpec<TModel, TMetadata>`
- `src/Motiv/OrElse/OrElseSpec.cs` and `src/Motiv/OrElse/OrElsePolicy.cs`: `operand is OrSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata> or OrElseSpec<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata> or ExpressionOrElseSpec<TModel, TMetadata>`
- `src/Motiv/XOr/XOrSpec.cs`: `operand is XOrSpec<TModel, TMetadata> or ExpressionXOrSpec<TModel, TMetadata>`

(Task 6 extends the `Or`-family predicates once more with `ExpressionOrElsePolicy`.)

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecCompositionTests"`
Expected: PASS.

- [ ] **Step 7: Verify no regressions and commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: all tests pass.

```bash
git add src/Motiv
git add src/Motiv.Tests/ExpressionSpecCompositionTests.cs
git commit -m "feat: add remaining binary expression composites and policy composition surface"
```

---
### Task 6: Negation and policy preservation — `ExpressionNotSpec`, `ExpressionNotPolicy`, `ExpressionOrElsePolicy`

**Files:**
- Create: `src/Motiv/Not/ExpressionNotSpec.cs`, `src/Motiv/Not/ExpressionNotPolicy.cs`, `src/Motiv/OrElse/ExpressionOrElsePolicy.cs`
- Modify: `src/Motiv/ExpressionSpecBase.cs` (`Not()` + `!`), `src/Motiv/ExpressionPolicyBase.cs` (`Not()`, `!`, `OrElse(ExpressionPolicyBase)`)
- Modify: `src/Motiv/Or/OrSpec.cs`, `src/Motiv/Or/ExpressionOrSpec.cs`, `src/Motiv/OrElse/OrElseSpec.cs`, `src/Motiv/OrElse/OrElsePolicy.cs`, `src/Motiv/OrElse/ExpressionOrElseSpec.cs` (add `ExpressionOrElsePolicy` to `Or`-family collapse predicates)
- Test: `src/Motiv.Tests/ExpressionSpecNegationTests.cs`

**Interfaces:**
- Consumes: `ExpressionComposer.Negate` (Task 3), `NotSpecDescription<TModel, TMetadata>` (`src/Motiv/Not/NotSpecDescription.cs`), `OrElsePolicyResult<TMetadata>` (`src/Motiv/OrElse`), `IUnaryOperationSpec*` interfaces.
- Produces: `ExpressionNotSpec<TModel, TMetadata>(SpecBase<TModel, TMetadata> operand, IExpressionSpec<TModel> operandExpression) : ExpressionSpecBase<TModel, TMetadata>`; `ExpressionNotPolicy<TModel, TMetadata>(ExpressionPolicyBase<TModel, TMetadata> operand) : ExpressionPolicyBase<TModel, TMetadata>`; `ExpressionOrElsePolicy<TModel, TMetadata>(ExpressionPolicyBase<TModel, TMetadata> left, ExpressionPolicyBase<TModel, TMetadata> right) : ExpressionPolicyBase<TModel, TMetadata>`. Base-class members: `ExpressionSpecBase.Not()` / `operator !` → `ExpressionSpecBase`; `ExpressionPolicyBase.Not()` / `operator !` → `ExpressionPolicyBase`; `ExpressionPolicyBase.OrElse(ExpressionPolicyBase<TModel, TMetadata>)` → `ExpressionPolicyBase<TModel, TMetadata>`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionSpecNegationTests.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionSpecNegationTests
{
    private static ExpressionSpecBase<int, string> IsEvenSpec() =>
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).Create("is even"),
            n => n % 2 == 0);

    private static ExpressionPolicyBase<int, string> IsEvenPolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n % 2 == 0).WhenTrue("even").WhenFalse("odd").Create(),
            n => n % 2 == 0);

    private static ExpressionPolicyBase<int, string> IsPositivePolicy() =>
        new ExpressionPolicyDecorator<int, string>(
            Spec.Build((int n) => n > 0).WhenTrue("positive").WhenFalse("not positive").Create(),
            n => n > 0);

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    public void Should_negate_an_expression_spec_and_stay_expression_backed(int model)
    {
        // Arrange
        var sut = IsEvenSpec().Not();
        SpecBase<int, string> ordinaryOperand = IsEvenSpec();
        var ordinary = ordinaryOperand.Not();

        // Act & Assert
        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.Not);
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Justification.ShouldBe(ordinary.Evaluate(model).Justification);
    }

    [Fact]
    public void Should_preserve_policy_ness_when_negating_an_expression_policy()
    {
        // Act
        var act = IsEvenPolicy().Not();

        // Assert
        act.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
        act.Evaluate(3).Value.ShouldBe("odd");
        act.ToExpression().Compile()(3).ShouldBeTrue();
    }

    [Fact]
    public void Should_preserve_policy_ness_when_bang_operator_is_used()
    {
        // Act
        var act = !IsEvenPolicy();

        // Assert
        act.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
    }

    [Theory]
    [InlineData(4, true)]   // even → left satisfied
    [InlineData(3, true)]   // odd but positive
    [InlineData(-3, false)] // odd and not positive
    public void Should_preserve_policy_ness_when_or_else_combines_two_expression_policies(int model, bool expected)
    {
        // Arrange
        var sut = IsEvenPolicy().OrElse(IsPositivePolicy());
        PolicyBase<int, string> l = IsEvenPolicy();
        PolicyBase<int, string> r = IsPositivePolicy();
        var ordinary = l.OrElse(r);

        // Act
        var act = sut.Evaluate(model);

        // Assert
        sut.ShouldBeAssignableTo<ExpressionPolicyBase<int, string>>();
        act.Satisfied.ShouldBe(expected);
        act.Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        act.Value.ShouldBe(ordinary.Evaluate(model).Value);
        sut.ToExpression().Body.NodeType.ShouldBe(ExpressionType.OrElse);
        sut.ToExpression().Compile()(model).ShouldBe(expected);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecNegationTests"`
Expected: FAIL — `Not()`/`OrElse` bind to base overloads returning ordinary types.

- [ ] **Step 3: Write the composites**

```csharp
// src/Motiv/Not/ExpressionNotSpec.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class ExpressionNotSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    IExpressionSpec<TModel> operandExpression)
    : ExpressionSpecBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Negate(operandExpression));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new NotSpecDescription<TModel, TMetadata>(operand);

    public string Operation => Operator.Not;

    public bool IsCollapsable => false;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => !operand.Matches(model);

    protected override BooleanResultBase<TMetadata> EvaluateSpec(TModel model) =>
        operand.Evaluate(model).Not();

    public SpecBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
```

```csharp
// src/Motiv/Not/ExpressionNotPolicy.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Traversal;

namespace Motiv.Not;

internal sealed class ExpressionNotPolicy<TModel, TMetadata>(
    ExpressionPolicyBase<TModel, TMetadata> operand)
    : ExpressionPolicyBase<TModel, TMetadata>,
        IUnaryOperationSpec<TModel, TMetadata>,
        IUnaryOperationSpec<TModel>,
        IUnaryOperationSpec
{
    private readonly SpecBase[] _underlying = [operand];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Negate(operand));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new NotSpecDescription<TModel, TMetadata>(operand);

    string IBooleanOperationSpec.Operation => Operator.Not;

    bool IBooleanOperationSpec.IsCollapsable => false;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => !operand.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model) =>
        operand.Evaluate(model).Not();

    public PolicyBase<TModel, TMetadata> Operand => operand;

    SpecBase<TModel, TMetadata> IUnaryOperationSpec<TModel, TMetadata>.Operand => operand;

    SpecBase<TModel> IUnaryOperationSpec<TModel>.Operand => operand;

    SpecBase IUnaryOperationSpec.Operand => operand;
}
```

```csharp
// src/Motiv/OrElse/ExpressionOrElsePolicy.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Or;
using Motiv.Shared;
using Motiv.Traversal;
using Expr = System.Linq.Expressions.Expression;

namespace Motiv.OrElse;

internal sealed class ExpressionOrElsePolicy<TModel, TMetadata>(
    ExpressionPolicyBase<TModel, TMetadata> left,
    ExpressionPolicyBase<TModel, TMetadata> right)
    : ExpressionPolicyBase<TModel, TMetadata>,
        IBinaryOperationSpec<TModel, TMetadata>,
        IBinaryOperationSpec<TModel>,
        IBinaryOperationSpec
{
    private readonly SpecBase[] _underlying = [left, right];

    private readonly Lazy<Expression<Func<TModel, bool>>> _expression = new(() =>
        ExpressionComposer.Combine(left, right, Expr.OrElse));

    public override IEnumerable<SpecBase> Underlying => _underlying;

    public override ISpecDescription Description =>
        new BinarySpecDescription<TModel, TMetadata>(left, right, "||", Operator.OrElse,
            operand => operand is OrSpec<TModel, TMetadata> or OrElsePolicy<TModel, TMetadata>
                or OrElseSpec<TModel, TMetadata> or ExpressionOrSpec<TModel, TMetadata>
                or ExpressionOrElseSpec<TModel, TMetadata> or ExpressionOrElsePolicy<TModel, TMetadata>);

    public string Operation => Operator.OrElse;

    public bool IsCollapsable => true;

    public override Expression<Func<TModel, bool>> ToExpression() => _expression.Value;

    public override bool Matches(TModel model) => left.Matches(model) || right.Matches(model);

    protected override PolicyResultBase<TMetadata> EvaluatePolicy(TModel model)
    {
        var leftResult = left.Evaluate(model);
        return leftResult.Satisfied switch
        {
            true => new OrElsePolicyResult<TMetadata>(leftResult),
            false => new OrElsePolicyResult<TMetadata>(leftResult, right.Evaluate(model))
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

- [ ] **Step 4: Add base-class members**

`src/Motiv/ExpressionSpecBase.cs` (add `using Motiv.Not;`):

```csharp
    /// <summary>Negates this proposition. The result is itself expression-backed.</summary>
    /// <returns>An expression-backed proposition representing the logical NOT of this proposition.</returns>
    public new ExpressionSpecBase<TModel, TMetadata> Not() =>
        new ExpressionNotSpec<TModel, TMetadata>(this, this);

    /// <summary>Negates an expression-backed proposition.</summary>
    /// <param name="spec">The proposition to negate.</param>
    /// <returns>An expression-backed proposition representing the logical NOT of the proposition.</returns>
    public static ExpressionSpecBase<TModel, TMetadata> operator !(
        ExpressionSpecBase<TModel, TMetadata> spec) =>
        spec.Not();
```

`src/Motiv/ExpressionPolicyBase.cs` (add `using Motiv.Not; using Motiv.OrElse;`):

```csharp
    /// <summary>Negates this policy. The result remains both a policy and expression-backed.</summary>
    /// <returns>An expression-backed policy representing the logical NOT of this policy.</returns>
    public new ExpressionPolicyBase<TModel, TMetadata> Not() =>
        new ExpressionNotPolicy<TModel, TMetadata>(this);

    /// <summary>Negates an expression-backed policy.</summary>
    /// <param name="policy">The policy to negate.</param>
    /// <returns>An expression-backed policy representing the logical NOT of the policy.</returns>
    public static ExpressionPolicyBase<TModel, TMetadata> operator !(
        ExpressionPolicyBase<TModel, TMetadata> policy) =>
        policy.Not();

    /// <summary>
    /// Creates a policy equivalent to a conditional "OR" of this policy and the alternative policy.
    /// The result remains both a policy and expression-backed.
    /// </summary>
    /// <param name="alternative">The policy to evaluate when this policy is unsatisfied.</param>
    /// <returns>An expression-backed policy representing the conditional OR of the two policies.</returns>
    public ExpressionPolicyBase<TModel, TMetadata> OrElse(ExpressionPolicyBase<TModel, TMetadata> alternative) =>
        new ExpressionOrElsePolicy<TModel, TMetadata>(this, alternative);
```

- [ ] **Step 5: Extend the `Or`-family collapse predicates**

Add `or ExpressionOrElsePolicy<TModel, TMetadata>` to the collapse predicates in: `src/Motiv/Or/OrSpec.cs`, `src/Motiv/Or/ExpressionOrSpec.cs`, `src/Motiv/OrElse/OrElseSpec.cs`, `src/Motiv/OrElse/OrElsePolicy.cs`, `src/Motiv/OrElse/ExpressionOrElseSpec.cs`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecNegationTests"`
Expected: PASS.

- [ ] **Step 7: Verify no regressions and commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: all tests pass.

```bash
git add src/Motiv src/Motiv.Tests/ExpressionSpecNegationTests.cs
git commit -m "feat: add expression-backed negation and policy-preserving composites"
```

---

### Task 7: Mixed-metadata closure (generic method overloads + `ToExplanationSpec` overrides)

**Files:**
- Modify: `src/Motiv/ExpressionSpecBase.cs`, `src/Motiv/ExpressionPolicyBase.cs`
- Test: `src/Motiv.Tests/ExpressionSpecMixedMetadataTests.cs`

**Interfaces:**
- Consumes: all composites (Tasks 4–6), `ExpressionSpecDecorator` (Task 1), base `ToExplanationSpec()` (`src/Motiv/SpecBase.cs:288-293`).
- Produces: on BOTH abstract classes, for each of `And`, `AndAlso`, `Or`, `OrElse`, `XOr`:
  `public ExpressionSpecBase<TModel, string> <Method><TSpec>(TSpec spec) where TSpec : SpecBase<TModel>, IExpressionSpec<TModel>`
  plus `public override SpecBase<TModel, string> ToExplanationSpec()` that preserves expression-ness. Cross-metadata **operator** forms are deliberately NOT closed (operators cannot be generic, and an `IExpressionSpec<TModel>`-typed operand would require an unsafe cast because external code can implement the interface) — operators degrade for mixed metadata; method forms are the documented route. This resolves spec risk #2 with the documented fallback.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionSpecMixedMetadataTests.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;

namespace Motiv.Tests;

public class ExpressionSpecMixedMetadataTests
{
    private static ExpressionPolicyBase<int, int> ErrorCodeSpec() =>
        new ExpressionPolicyDecorator<int, int>(
            Spec.Build((int n) => n % 2 == 0).WhenTrue(0).WhenFalse(422).Create("is even"),
            n => n % 2 == 0);

    private static ExpressionSpecBase<int, string> IsPositive() =>
        new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n > 0).Create("is positive"),
            n => n > 0);

    [Theory]
    [InlineData(4)]
    [InlineData(-3)]
    public void Should_stay_expression_backed_when_combining_specs_with_different_metadata(int model)
    {
        // Arrange
        var sut = IsPositive().And(ErrorCodeSpec());
        SpecBase<int> l = IsPositive();
        SpecBase<int> r = ErrorCodeSpec();
        var ordinary = l.And(r);

        // Act & Assert
        sut.ShouldBeAssignableTo<ExpressionSpecBase<int, string>>();
        sut.ToExpression().Compile()(model).ShouldBe(sut.Matches(model));
        sut.Evaluate(model).Reason.ShouldBe(ordinary.Evaluate(model).Reason);
        sut.Evaluate(model).Assertions.ShouldBe(ordinary.Evaluate(model).Assertions);
    }

    [Fact]
    public void Should_prefer_the_same_metadata_overload_when_metadata_types_match()
    {
        // Arrange
        var left = IsPositive();
        var right = new ExpressionSpecDecorator<int, string>(
            Spec.Build((int n) => n < 100).Create("is small"),
            n => n < 100);

        // Act — same metadata must NOT coerce to string via the generic overload
        ExpressionSpecBase<int, string> act = left.And(right);

        // Assert
        act.ToExpression().Body.NodeType.ShouldBe(ExpressionType.And);
    }

    [Fact]
    public void Should_preserve_expression_when_explicitly_coerced_to_explanation_spec()
    {
        // Act
        var act = ErrorCodeSpec().ToExplanationSpec();

        // Assert
        act.ShouldBeAssignableTo<IExpressionSpec<int>>();
        ((IExpressionSpec<int>)act).ToExpression().Compile()(4).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecMixedMetadataTests"`
Expected: FAIL — mixed-metadata `And` binds to `SpecBase<TModel>.And`, returning an ordinary spec.

- [ ] **Step 3: Write minimal implementation**

Add to `src/Motiv/ExpressionSpecBase.cs` (same members go into `ExpressionPolicyBase.cs`, changing only the containing class):

```csharp
    private SpecBase<TModel, string>? _explanationExpressionSpec;

    /// <summary>
    /// Converts this proposition to an explanation proposition (string metadata) while preserving the
    /// underlying predicate expression tree.
    /// </summary>
    /// <returns>An expression-backed explanation proposition.</returns>
    public override SpecBase<TModel, string> ToExplanationSpec() =>
        this as SpecBase<TModel, string>
            ?? (_explanationExpressionSpec ??=
                new ExpressionSpecDecorator<TModel, string>(base.ToExplanationSpec(), ToExpression()));

    /// <summary>
    /// Combines this proposition with an expression-backed proposition that has a different metadata
    /// type, using the logical AND operator. The operands are coerced to string metadata, and the
    /// result remains expression-backed.
    /// </summary>
    /// <param name="spec">The expression-backed proposition to combine with this proposition.</param>
    /// <typeparam name="TSpec">The type of the other proposition.</typeparam>
    /// <returns>An expression-backed explanation proposition representing the logical AND.</returns>
    public ExpressionSpecBase<TModel, string> And<TSpec>(TSpec spec)
        where TSpec : SpecBase<TModel>, IExpressionSpec<TModel> =>
        new ExpressionAndSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec);
```

…and the equivalent `AndAlso<TSpec>` (→ `ExpressionAndAlsoSpec`), `Or<TSpec>` (→ `ExpressionOrSpec`), `OrElse<TSpec>` (→ `ExpressionOrElseSpec`), `XOr<TSpec>` (→ `ExpressionXOrSpec`), each with the same body shape `new Expression<Method>Spec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec(), this, spec)` and analogous XML docs. Add `using Motiv.ExpressionTreeProposition;` to both files.

Note on parity: the ordinary untyped path is `new AndSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec())` (`src/Motiv/SpecBase.cs:88-89`). The generic overload feeds the composite the exact same evaluation operands — the overridden `ToExplanationSpec()` returns a decorator that forwards `Description`/`Underlying`/`Evaluate` to what the base method would have returned, so `Reason`/`Assertions` parity holds.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecMixedMetadataTests"`
Expected: PASS.

- [ ] **Step 5: Verify no regressions and commit**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0`
Expected: all tests pass. Also verify same-metadata composition tests from Tasks 4–6 still pass (overload-resolution guard).

```bash
git add src/Motiv/ExpressionSpecBase.cs src/Motiv/ExpressionPolicyBase.cs src/Motiv.Tests/ExpressionSpecMixedMetadataTests.cs
git commit -m "feat: keep mixed-metadata composition expression-backed via generic overloads"
```

---

### Task 8: Bool-specific `Spec.From` factory twins (all 17)

**Files:**
- Create: 9 files in `src/Motiv/ExpressionTreeProposition/PropositionBuilders/` — `BooleanMinimalExpressionTreePropositionFactory.cs`, `BooleanExplanationExpressionTreePropositionFactory.cs`, `BooleanExplanationWithNameExpressionTreePropositionFactory.cs`, `BooleanMetadataExpressionTreePropositionFactory.cs`, `BooleanMultiAssertionExplanationExpressionTreePropositionFactory.cs`, `BooleanMultiAssertionExplanationWithNameExpressionTreePropositionFactory.cs`, `BooleanMultiAssertionExplanationWithSingularWhenTrueExpressionTreePropositionFactory.cs`, `BooleanMultiMetadataPropositionExpressionTreeFactory.cs`, `BooleanMultiMetadataWithSingularWhenTruePropositionExpressionTreeFactory.cs`
- Create: 8 files mirroring `src/Motiv/HigherOrderProposition/PropositionBuilders/{Explanation,Metadata}/ExpressionTree/*.cs` with a `Boolean` prefix
- Test: `src/Motiv.Tests/ExpressionSpecBuilderTests.cs`

**Interfaces:**
- Consumes: existing leaf propositions instantiated with `TPredicateResult = bool` (`ExpressionTreeMultiMetadataProposition<TModel, TMetadata, bool>`, `ExpressionTreeExplanationProposition<TModel, bool>`, `ExpressionTreeMetadataProposition<TModel, TMetadata, bool>`, `ExpressionTreeWithSingleTrueAssertionProposition<TModel, bool>`), decorators (Tasks 1–2), `SpecDescription`, `ExpressionTreeDescription<TModel, bool>`, Converj attributes.
- Produces: `Spec.From(Expression<Func<TModel, bool>>)` overloads (source-generated from the new structs) whose `Create(...)` returns `ExpressionSpecBase<TModel, TMetadata>` / `ExpressionPolicyBase<TModel, TMetadata>`; higher-order chains (`AsAnySatisfied()` etc.) keep compiling for bool lambdas with unchanged (ordinary) return types.

**IMPORTANT — atomicity:** all 17 twins must land in ONE commit. A bool `From` overload that covers only some chains breaks the others for bool lambdas mid-task (including `ExpressionTreeTransformer`'s internal `Spec.From(predicate).AsAnySatisfied()` calls). Expect the build to be broken between Steps 3 and 5; do not commit until Step 7 is green.

**Spec risks #1 and #3 are resolved in this task:** Step 2's compile-behavior test verifies C# prefers the bool-specific `From` (tie-break on more-specific parameter types), and building verifies the Converj generator accepts sibling factory structs sharing the `"From"` fluent method name with a different signature. If the generator fails on the sibling signature, STOP and report — that invalidates the entry-point mechanism and needs a design conversation.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionSpecBuilderTests.cs
using System.Linq.Expressions;

namespace Motiv.Tests;

public class ExpressionSpecBuilderTests
{
    [Fact]
    public void Should_create_an_expression_spec_from_a_minimal_bool_expression_proposition()
    {
        // Act — static typing is the assertion: this must compile against ExpressionSpecBase
        ExpressionSpecBase<int, string> act = Spec.From((int n) => n > 3).Create("is greater than three");

        // Assert
        act.ToExpression().Compile()(4).ShouldBeTrue();
        act.Evaluate(4).Reason.ShouldBe("is greater than three == true");
    }

    [Fact]
    public void Should_return_the_original_lambda_from_a_from_built_proposition()
    {
        // Arrange
        Expression<Func<int, bool>> expression = n => n > 3;

        // Act
        var act = Spec.From(expression).Create("is greater than three");

        // Assert
        act.ToExpression().ShouldBeSameAs(expression);
    }

    [Fact]
    public void Should_create_an_expression_policy_from_an_explanation_proposition()
    {
        // Act
        ExpressionPolicyBase<int, string> act = Spec
            .From((int n) => n > 3)
            .WhenTrue("is greater than three")
            .WhenFalse("is not greater than three")
            .Create();

        // Assert
        act.Evaluate(2).Value.ShouldBe("is not greater than three");
        act.ToExpression().Compile()(2).ShouldBeFalse();
    }

    [Fact]
    public void Should_create_an_expression_policy_from_a_metadata_proposition()
    {
        // Act
        ExpressionPolicyBase<int, Guid> act = Spec
            .From((int n) => n > 3)
            .WhenTrue(Guid.Empty)
            .WhenFalse(Guid.NewGuid())
            .Create("is greater than three");

        // Assert
        act.Evaluate(4).Value.ShouldBe(Guid.Empty);
        act.ToExpression().Compile()(4).ShouldBeTrue();
    }

    [Fact]
    public void Should_create_an_expression_spec_from_a_multi_assertion_proposition()
    {
        // Act
        ExpressionSpecBase<int, string> act = Spec
            .From((int n) => n > 3)
            .WhenTrueYield((_, _) => ["big"])
            .WhenFalseYield((_, _) => ["small"])
            .Create("is greater than three");

        // Assert
        act.Evaluate(4).Assertions.ShouldBe(["big"]);
    }

    [Fact]
    public void Should_keep_boolean_result_lambdas_on_the_ordinary_hierarchy()
    {
        // Arrange
        var inner = Spec.Build((int n) => n > 3).Create("inner");

        // Act — lambda returns BooleanResultBase<string>, not bool
        var act = Spec.From((int n) => inner.Evaluate(n)).Create("wraps a result");

        // Assert
        act.ShouldBeAssignableTo<SpecBase<int, string>>();
        (act is IExpressionSpec<int>).ShouldBeFalse();
    }

    [Fact]
    public void Should_keep_higher_order_from_chains_compiling_for_bool_lambdas()
    {
        // Act
        var act = Spec
            .From((int n) => n > 3)
            .AsAnySatisfied()
            .Create("any are greater than three");

        // Assert
        act.Evaluate([1, 2, 4]).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_compose_two_from_built_propositions_and_recover_the_combined_expression()
    {
        // Arrange
        var adult = Spec.From((int age) => age >= 18).Create("is adult");
        var senior = Spec.From((int age) => age >= 65).Create("is senior");

        // Act
        var act = adult.And(!senior);

        // Assert
        act.ShouldBeAssignableTo<IExpressionSpec<int>>();
        act.ToExpression().Compile()(30).ShouldBeTrue();
        act.ToExpression().Compile()(70).ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecBuilderTests"`
Expected: FAIL to compile — `ExpressionSpecBase<int, string> act = Spec.From(...)` cannot convert from the current `SpecBase`/`PolicyBase` returns.

- [ ] **Step 3: Create the 9 core bool factory twins**

Three fully worked examples covering all shapes; the remaining six follow the table below. Every twin: same `[FluentTarget]`/`[FluentMethod]`/`[MultipleFluentMethods]` attributes as its source struct, `TPredicateResult` type parameter removed, `Expression<Func<TModel, bool>>` parameter, `Create` return type upgraded and body wrapped in the appropriate decorator. Copy the source struct's XML docs verbatim (adjusting `<typeparam>` list).

```csharp
// src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanMinimalExpressionTreePropositionFactory.cs
using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// Creates a minimal proposition from a boolean predicate expression tree. The resulting proposition
/// is expression-backed, so the predicate expression can be recovered for use with query providers.
/// </summary>
/// <param name="expression">The expression tree predicate to represent</param>
/// <typeparam name="TModel">The model type</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly partial struct BooleanMinimalExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed proposition for the model.</returns>
    public ExpressionSpecBase<TModel, string> Create(string statement) =>
        new ExpressionSpecDecorator<TModel, string>(
            new ExpressionTreeMultiMetadataProposition<TModel, string, bool>(
                expression,
                (_, result) => result.Values,
                (_, result) => result.Values,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);
}
```

```csharp
// src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanExplanationExpressionTreePropositionFactory.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating expression-backed propositions based on the supplied proposition and
/// explanation factories.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="trueBecause">The explanation to use when the expression evaluates to true.</param>
/// <param name="falseBecause">The explanation to use when the expression evaluates to false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanExplanationExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(WhenTrueLambdaOverloads))]Func<TModel, BooleanResultBase<string>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, string> falseBecause)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed policy for the model.</returns>
    public ExpressionPolicyBase<TModel, string> Create(string statement) =>
        new ExpressionPolicyDecorator<TModel, string>(
            new ExpressionTreeExplanationProposition<TModel, bool>(
                expression,
                trueBecause,
                falseBecause,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);

    /// <summary>
    /// Creates a proposition. The propositional statement is derived from the expression itself.
    /// </summary>
    /// <returns>An expression-backed policy for the model.</returns>
    public ExpressionPolicyBase<TModel, string> Create() =>
        new ExpressionPolicyDecorator<TModel, string>(
            new ExpressionTreeExplanationProposition<TModel, bool>(
                expression,
                trueBecause,
                falseBecause,
                new ExpressionTreeDescription<TModel, bool>(expression)),
            expression);
}
```

```csharp
// src/Motiv/ExpressionTreeProposition/PropositionBuilders/BooleanMetadataExpressionTreePropositionFactory.cs
using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Overloads;
using Converj.Attributes;
using Motiv.Shared;

namespace Motiv.ExpressionTreeProposition.PropositionBuilders;

/// <summary>
/// A factory for creating expression-backed propositions based on the supplied proposition and
/// metadata factories.
/// </summary>
/// <param name="expression">The expression to use for the specification.</param>
/// <param name="whenTrue">The metadata factory for the proposition when the predicate is true.</param>
/// <param name="whenFalse">The metadata factory for the proposition when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
/// <typeparam name="TMetadata">The type of the metadata associated with the proposition.</typeparam>
[FluentTarget(typeof(Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanMetadataExpressionTreePropositionFactory<TModel, TMetadata>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(WhenTrueOverloads))]Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse)
{
    /// <summary>Creates a proposition and names it with the propositional statement provided.</summary>
    /// <param name="statement">The proposition statement of what the proposition represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An expression-backed policy for the model.</returns>
    public ExpressionPolicyBase<TModel, TMetadata> Create(string statement) =>
        new ExpressionPolicyDecorator<TModel, TMetadata>(
            new ExpressionTreeMetadataProposition<TModel, TMetadata, bool>(
                expression,
                whenTrue,
                whenFalse,
                new SpecDescription(statement.ThrowIfNullOrWhitespace(nameof(statement))) { HasExplicitStatement = true }),
            expression);
}
```

Remaining six core twins — copy the source file, then apply exactly: rename struct with `Boolean` prefix; delete `TPredicateResult` from the type parameter list and its `<typeparam>` doc; change the `From` parameter type to `Expression<Func<TModel, bool>>`; substitute `bool` for every remaining `TPredicateResult` usage; wrap each `Create` body in the decorator from the table with `expression` as the second argument; change the return type accordingly:

| Source file (same directory) | Inner proposition (generic args) | Decorator | `Create` return type |
|---|---|---|---|
| `ExplanationWithNameExpressionTreePropositionFactory.cs` | `ExpressionTreeWithSingleTrueAssertionProposition<TModel, bool>` | `ExpressionPolicyDecorator<TModel, string>` | `ExpressionPolicyBase<TModel, string>` (both `Create` overloads; the parameterless one keeps `ExpressionTreeDescription<TModel, bool>`) |
| `MultiAssertionExplanationExpressionTreePropositionFactory.cs` | `ExpressionTreeMultiMetadataProposition<TModel, string, bool>` | `ExpressionSpecDecorator<TModel, string>` | `ExpressionSpecBase<TModel, string>` |
| `MultiAssertionExplanationWithNameExpressionTreePropositionFactory.cs` | `ExpressionTreeMultiMetadataProposition<TModel, string, bool>` | `ExpressionSpecDecorator<TModel, string>` | `ExpressionSpecBase<TModel, string>` (both `Create` overloads; keep the `TrueBecauseFunc` private property) |
| `MultiAssertionExplanationWithSingularWhenTrueExpressionTreePropositionFactory.cs` | `ExpressionTreeMultiMetadataProposition<TModel, string, bool>` | `ExpressionSpecDecorator<TModel, string>` | `ExpressionSpecBase<TModel, string>` |
| `MultiMetadataPropositionExpressionTreeFactory.cs` | `ExpressionTreeMultiMetadataProposition<TModel, TMetadata, bool>` | `ExpressionSpecDecorator<TModel, TMetadata>` | `ExpressionSpecBase<TModel, TMetadata>` |
| `MultiMetadataWithSingularWhenTruePropositionExpressionTreeFactory.cs` | `ExpressionTreeMultiMetadataProposition<TModel, TMetadata, bool>` | `ExpressionSpecDecorator<TModel, TMetadata>` | `ExpressionSpecBase<TModel, TMetadata>` |

- [ ] **Step 4: Create the 8 higher-order bool factory twins**

For each file in `src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree/` and `src/Motiv/HigherOrderProposition/PropositionBuilders/Metadata/ExpressionTree/` (the 8 files matching `[FluentMethod("From")]`): copy the file into the same directory with a `Boolean` name prefix, rename the struct with a `Boolean` prefix, delete the `TPredicateResult` type parameter (and its `<typeparam>` doc), change the `From` parameter to `Expression<Func<TModel, bool>>`, and substitute `bool` for every remaining `TPredicateResult` usage. **`Create` return types and bodies stay otherwise identical** — higher-order propositions remain ordinary (deferred queryability). Worked example of the transformation for `ExplanationHigherOrderExpressionTreePropositionFactory.cs`:

```csharp
// src/Motiv/HigherOrderProposition/PropositionBuilders/Explanation/ExpressionTree/BooleanExplanationHigherOrderExpressionTreePropositionFactory.cs
using System.Linq.Expressions;
using Converj.Attributes;
using Motiv.HigherOrderProposition.ExpressionTree;
using Motiv.Shared;

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;

/// <summary>
/// A factory for creating specifications based on a boolean predicate expression and explanations
/// for true and false conditions.
/// </summary>
/// <param name="expression">The expression to evaluate.</param>
/// <param name="higherOrderOperation">The higher-order predicate operation.</param>
/// <param name="trueBecause">The explanation for when the predicate is true.</param>
/// <param name="falseBecause">The explanation for when the predicate is false.</param>
/// <typeparam name="TModel">The type of the model.</typeparam>
[FluentTarget(typeof(Motiv.Spec), TerminalMethod = TerminalMethod.None)]
public readonly struct BooleanExplanationHigherOrderExpressionTreePropositionFactory<TModel>(
    [FluentMethod("From")]Expression<Func<TModel, bool>> expression,
    [MultipleFluentMethods(typeof(HigherOrderPredicateSpecMethods))]HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation,
    [FluentMethod("WhenTrue")]Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> trueBecause,
    [MultipleFluentMethods(typeof(WhenFalseOverloads))]Func<HigherOrderBooleanResultEvaluation<TModel, string>, string> falseBecause)
{
    /// <summary>
    /// Creates a specification with explanations for when the condition is true or false, and names
    /// it with the propositional statement provided.
    /// </summary>
    /// <param name="statement">The proposition statement of what the specification represents.</param>
    /// <remarks>It is best to use short phrases in natural-language, as if you were naming a boolean variable.</remarks>
    /// <returns>An instance of <see cref="SpecBase{TModel, TMetadata}" />.</returns>
    public PolicyBase<IEnumerable<TModel>, string> Create(string statement)
    {
        statement.ThrowIfNullOrWhitespace(nameof(statement));
        return new HigherOrderFromExpressionTreeExplanationProposition<TModel, bool>(
            expression,
            higherOrderOperation.HigherOrderPredicate,
            trueBecause,
            falseBecause,
            new SpecDescription(statement) { HasExplicitStatement = true },
            higherOrderOperation.CauseSelector);
    }
}
```

- [ ] **Step 5: Build and inspect generator output**

Run: `dotnet build src/Motiv/Motiv.csproj`
Expected: SUCCESS with new `Spec.From(Expression<Func<TModel, bool>>)` overloads generated. If the Converj generator errors on sibling `From` signatures, STOP and report (spec risk #3).

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionSpecBuilderTests"`
Expected: PASS — including the overload-resolution assertions (spec risk #1).

- [ ] **Step 7: Run the FULL suite across all TFMs and commit**

Run: `dotnet test Motiv.slnx`
Expected: all ~13,000+ tests pass — existing `Spec.From` tests now flow through the bool twins and must behave identically.

```bash
git add src/Motiv src/Motiv.Tests/ExpressionSpecBuilderTests.cs
git commit -m "feat: route bool-predicate Spec.From paths to expression-backed propositions"
```

---
### Task 9: Expression-backed decomposition — `ExpressionTreeTransformer` and `ToSpec()`

**Files:**
- Modify: `src/Motiv/ExpressionTreeProposition/ExpressionTreeTransformer.cs`
- Modify: `src/Motiv/ExpressionTreeExtensions.cs:18-19`
- Test: `src/Motiv.Tests/ExpressionTreeToSpecExpressionTests.cs`

**Interfaces:**
- Consumes: `ExpressionSpecDecorator` (Task 1), closed composition (Tasks 4–6).
- Produces: `public static ExpressionSpecBase<TModel, string> ToSpec<TModel>(this Expression<Func<TModel, bool>> expression)`; internal `ExpressionTreeTransformer<TModel>.Transform()` returns `ExpressionSpecBase<TModel, string>`; every decomposed node implements `IExpressionSpec<TModel>` and returns the fragment of the original lambda it explains.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/ExpressionTreeToSpecExpressionTests.cs
using System.Linq.Expressions;
using Motiv.Traversal;

namespace Motiv.Tests;

public class ExpressionTreeToSpecExpressionTests
{
    public record Customer(int Age, bool IsActive, List<int> OrderTotals);

    [Fact]
    public void Should_return_an_expression_spec_from_to_spec()
    {
        // Arrange
        Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;

        // Act — static typing is part of the assertion
        ExpressionSpecBase<Customer, string> act = expression.ToSpec();

        // Assert
        act.ToExpression().Compile()(new Customer(30, true, [])).ShouldBeTrue();
    }

    [Fact]
    public void Should_decompose_into_atoms_that_expose_their_source_fragments()
    {
        // Arrange
        Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;

        // Act
        var act = expression.ToSpec();

        // Assert — the root is a binary composite whose operands each carry their fragment
        var binary = act.ShouldBeAssignableTo<IBinaryOperationSpec<Customer, string>>();
        var leftFragment = binary!.Left.ShouldBeAssignableTo<IExpressionSpec<Customer>>()!.ToExpression();
        var rightFragment = binary.Right.ShouldBeAssignableTo<IExpressionSpec<Customer>>()!.ToExpression();
        leftFragment.Compile()(new Customer(17, true, [])).ShouldBeFalse();
        leftFragment.Compile()(new Customer(18, false, [])).ShouldBeTrue();
        rightFragment.Compile()(new Customer(17, true, [])).ShouldBeTrue();
    }

    [Fact]
    public void Should_keep_inline_any_decomposition_expression_backed()
    {
        // Arrange
        Expression<Func<Customer, bool>> expression = c => c.OrderTotals.Any(t => t > 100);

        // Act
        var act = expression.ToSpec();

        // Assert — explanation still flows through the higher-order decomposition
        act.ToExpression().Compile()(new Customer(30, true, [50, 150])).ShouldBeTrue();
        act.Evaluate(new Customer(30, true, [50])).Satisfied.ShouldBeFalse();
    }

    [Theory]
    [InlineData(17, true)]
    [InlineData(30, false)]
    public void Should_preserve_existing_explanations_after_transformer_change(int age, bool isMinor)
    {
        // Arrange — guard: assertions must be identical to the pre-change behaviour
        var sut = Spec.From((Customer c) => c.Age >= 18).Create("is adult");

        // Act
        var act = sut.Evaluate(new Customer(age, true, []));

        // Assert
        act.Satisfied.ShouldBe(!isMinor);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionTreeToSpecExpressionTests"`
Expected: FAIL to compile — `ToSpec()` returns `SpecBase<TModel, string>`, not `ExpressionSpecBase<TModel, string>`.

- [ ] **Step 3: Modify the transformer**

In `src/Motiv/ExpressionTreeProposition/ExpressionTreeTransformer.cs`:

1. Add a private helper (the transformer class does not derive `SpecBase`, so bare `Expression.*` is fine):

```csharp
    private static ExpressionSpecBase<TModel, string> WithFragment(
        SpecBase<TModel, string> spec,
        Expression expression,
        ParameterExpression parameter) =>
        new ExpressionSpecDecorator<TModel, string>(
            spec,
            Expression.Lambda<Func<TModel, bool>>(expression, parameter));
```

2. Change the return type from `SpecBase<TModel, string>` to `ExpressionSpecBase<TModel, string>` on: `Transform()`, `Transform(Expression, ParameterExpression)`, `TransformBinaryExpression`, `TransformMethodCallExpression` (and its local functions `Left`/`Right` inside `TransformBinaryExpression`), `TransformBooleanConditionalExpression`, `TransformComparisonExpression`, `TransformUnaryExpression`, `TransformQuasiProposition`, `CreateSpecForBoolean`, `CreateSpecForBooleanResult`.

3. Wrap leaf creations with `WithFragment`:
   - `TransformComparisonExpression` (`ExpressionTreeTransformer.cs:203-219`): wrap the built spec — `return WithFragment(spec, expression, parameter);` where `spec` is the existing `Spec.Build(predicate)...Create(...)` chain assigned to a local.
   - `CreateSpecForBoolean` (`:439-450`): `return WithFragment(spec, expression, parameter);`
   - `CreateSpecForBooleanResult` (`:452-460`): the wrapped fragment must be boolean — use the `Satisfied` member access already computed by the caller. Change the signature to `CreateSpecForBooleanResult(Expression expression, ParameterExpression parameter, Expression propositionalStatementExpression)` → wrap with `WithFragment(spec, propositionalStatementExpression, parameter)` (the `statement` argument built in `TransformQuasiProposition` at `:421-424` is `expression.Satisfied`, which is `bool`-typed).
   - `TransformBooleanConditionalExpression` (`:179-201`): assign the built spec to a local and `return WithFragment(spec, expression, parameter);`
4. Wrap the `Any`/`All` branches in `TransformMethodCallExpression` (`:78-108`): each of the six `TransformSpecExpression(...)`/`TransformPredicateExpression(...)` branch results gets wrapped: `WithFragment(TransformSpecExpression(expression, CreateAnySpec), expression, parameter)` etc. (`expression` here is the bool-typed `MethodCallExpression`). The signatures of `TransformSpecExpression`/`TransformPredicateExpression`/`CreateAnySpec`/`CreateAllSpec` and the reflection internals stay `SpecBase<TModel, string>`.
5. Binary logical branches (`:38-47`) need no wrapping — `Left(...)` / `Right(...)` now return `ExpressionSpecBase<TModel, string>`, so `.And(...)`, `.AndAlso(...)`, `.Or(...)`, `.OrElse(...)`, `.XOr(...)` bind to the closed overloads and return expression-backed composites. Likewise `ExpressionType.Not` (`:227`) binds to `ExpressionSpecBase.Not()`.
6. `Convert`/`ConvertChecked` (`:228`) recursion needs no change.

Change `src/Motiv/ExpressionTreeExtensions.cs`:

```csharp
    public static ExpressionSpecBase<TModel, string> ToSpec<TModel>(this Expression<Func<TModel, bool>> expression) =>
        new ExpressionTreeTransformer<TModel>(expression).Transform();
```

(add `using Motiv.ExpressionTreeProposition;` — already present — and keep the existing XML docs, updating `<returns>`).

`ExpressionPredicate` (`src/Motiv/ExpressionTreeProposition/ExpressionPredicate.cs:12`) uses `var` — no change needed.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ExpressionTreeToSpecExpressionTests"`
Expected: PASS.

- [ ] **Step 5: Run the FULL suite (explanation output is at stake) and commit**

Run: `dotnet test Motiv.slnx`
Expected: all tests pass, including `ExpressionTree*Tests` justification/reason assertions and the example projects. Any diff in assertion text here is a regression — fix the transformer wrapping, do not update the expected strings.

```bash
git add src/Motiv/ExpressionTreeProposition/ExpressionTreeTransformer.cs src/Motiv/ExpressionTreeExtensions.cs src/Motiv.Tests/ExpressionTreeToSpecExpressionTests.cs
git commit -m "feat: make decomposed expression tree nodes expression-backed"
```

---

### Task 10: `QueryableExtensions.Where`

**Files:**
- Create: `src/Motiv/QueryableExtensions.cs`
- Test: `src/Motiv.Tests/QueryableExtensionsTests.cs`

**Interfaces:**
- Consumes: `IExpressionSpec<TModel>` (Task 1).
- Produces: `public static IQueryable<TModel> Where<TModel>(this IQueryable<TModel> source, IExpressionSpec<TModel> spec)`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/Motiv.Tests/QueryableExtensionsTests.cs
namespace Motiv.Tests;

public class QueryableExtensionsTests
{
    [Fact]
    public void Should_filter_a_queryable_using_an_expression_spec()
    {
        // Arrange
        var isAdult = Spec.From((int age) => age >= 18).Create("is adult");
        var queryable = new[] { 12, 18, 30, 65 }.AsQueryable();

        // Act
        var act = queryable.Where(isAdult).ToArray();

        // Assert
        act.ShouldBe([18, 30, 65]);
    }

    [Fact]
    public void Should_filter_a_queryable_using_a_composed_expression_spec()
    {
        // Arrange
        var isAdult = Spec.From((int age) => age >= 18).Create("is adult");
        var isSenior = Spec.From((int age) => age >= 65).Create("is senior");
        var workingAge = isAdult.And(!isSenior);
        var queryable = new[] { 12, 18, 30, 65 }.AsQueryable();

        // Act
        var act = queryable.Where(workingAge).ToArray();

        // Assert
        act.ShouldBe([18, 30]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~QueryableExtensionsTests"`
Expected: FAIL to compile — no `Where(IExpressionSpec<TModel>)` overload (`Queryable.Where` cannot accept the spec).

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Motiv/QueryableExtensions.cs
namespace Motiv;

/// <summary>
/// Provides extension methods for using expression-backed propositions with
/// <see cref="IQueryable{T}"/> sources.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Filters a sequence of values using the proposition's underlying predicate expression tree.
    /// The expression is passed to the query provider verbatim, so providers such as EF Core can
    /// translate it (e.g. to SQL) rather than evaluating it client-side.
    /// </summary>
    /// <param name="source">The queryable source to filter.</param>
    /// <param name="spec">The expression-backed proposition to filter with.</param>
    /// <typeparam name="TModel">The element type of the queryable source.</typeparam>
    /// <returns>A queryable filtered by the proposition's predicate expression.</returns>
    public static IQueryable<TModel> Where<TModel>(
        this IQueryable<TModel> source,
        IExpressionSpec<TModel> spec) =>
        Queryable.Where(source, spec.ToExpression());
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Motiv.Tests/Motiv.Tests.csproj -f net10.0 --filter "FullyQualifiedName~QueryableExtensionsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Motiv/QueryableExtensions.cs src/Motiv.Tests/QueryableExtensionsTests.cs
git commit -m "feat: add IQueryable.Where extension for expression-backed propositions"
```

---

### Task 11: EF Core integration proof — `Motiv.EntityFramework.Tests` example project

**Files:**
- Create: `src/examples/Motiv.EntityFramework.Tests/Motiv.EntityFramework.Tests.csproj`
- Create: `src/examples/Motiv.EntityFramework.Tests/CustomerDbContext.cs`
- Create: `src/examples/Motiv.EntityFramework.Tests/QueryTranslationTests.cs`
- Modify: `Motiv.slnx` (via `dotnet sln add`)

**Interfaces:**
- Consumes: `Spec.From` expression-backed builders (Task 8), composition (Tasks 4–6), `QueryableExtensions.Where` (Task 10).
- Produces: end-to-end proof that a composed spec translates to server-side SQL. No production code.

- [ ] **Step 1: Scaffold the project**

Before writing the csproj, read `src/examples/Motiv.Poker.Tests/Motiv.Poker.Tests.csproj` and mirror its `TargetFramework`, xunit/Shouldly/Test SDK package versions and conventions exactly. Then:

```xml
<!-- src/examples/Motiv.EntityFramework.Tests/Motiv.EntityFramework.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
        <!-- copy versions from Motiv.Poker.Tests.csproj -->
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="..." />
        <PackageReference Include="xunit" Version="..." />
        <PackageReference Include="xunit.runner.visualstudio" Version="..." />
        <PackageReference Include="Shouldly" Version="..." />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\Motiv\Motiv.csproj" />
    </ItemGroup>
</Project>
```

Then add EF Core with the current package versions (do not guess versions — let NuGet resolve):

```bash
dotnet add src/examples/Motiv.EntityFramework.Tests package Microsoft.EntityFrameworkCore.Sqlite
dotnet sln Motiv.slnx add src/examples/Motiv.EntityFramework.Tests/Motiv.EntityFramework.Tests.csproj
```

- [ ] **Step 2: Write the DbContext and the failing test**

```csharp
// src/examples/Motiv.EntityFramework.Tests/CustomerDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace Motiv.EntityFramework.Tests;

public class Customer
{
    public int Id { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
}
```

```csharp
// src/examples/Motiv.EntityFramework.Tests/QueryTranslationTests.cs
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Shouldly;

namespace Motiv.EntityFramework.Tests;

public class QueryTranslationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly CustomerDbContext _context;

    public QueryTranslationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new CustomerDbContext(options);
        _context.Database.EnsureCreated();
        _context.Customers.AddRange(
            new Customer { Id = 1, Age = 17, IsActive = true },
            new Customer { Id = 2, Age = 30, IsActive = true },
            new Customer { Id = 3, Age = 45, IsActive = false },
            new Customer { Id = 4, Age = 70, IsActive = true });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Should_translate_a_composed_spec_to_a_server_side_where_clause()
    {
        // Arrange
        var isAdult = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
        var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");
        var eligible = isAdult & isActive;

        // Act
        var query = _context.Customers.Where(eligible);
        var sql = query.ToQueryString();
        var act = query.Select(c => c.Id).OrderBy(id => id).ToArray();

        // Assert — a WHERE clause in the generated SQL proves server-side translation
        sql.ShouldContain("WHERE");
        act.ShouldBe([2, 4]);
    }

    [Fact]
    public void Should_translate_a_negated_or_else_composition()
    {
        // Arrange
        var isSenior = Spec.From((Customer c) => c.Age >= 65).Create("is senior");
        var isInactive = Spec.From((Customer c) => !c.IsActive).Create("is inactive");
        var needsReview = isSenior.OrElse(isInactive);
        var fine = !needsReview;

        // Act
        var act = _context.Customers.Where(fine).Select(c => c.Id).OrderBy(id => id).ToArray();

        // Assert
        act.ShouldBe([1, 2]);
    }

    [Fact]
    public void Should_translate_expressions_recovered_from_decomposed_causes()
    {
        // Arrange — recover the atomic cause expression and re-query with it
        Expression<Func<Customer, bool>> expression = c => c.Age >= 18 & c.IsActive;
        var spec = expression.ToSpec();
        var binary = (Motiv.Traversal.IBinaryOperationSpec<Customer, string>)spec;
        var ageAtom = (IExpressionSpec<Customer>)binary.Left;

        // Act
        var act = _context.Customers.Where(ageAtom.ToExpression()).Select(c => c.Id).OrderBy(id => id).ToArray();

        // Assert
        act.ShouldBe([2, 3, 4]);
    }
}
```

(Add `using System.Linq.Expressions;` for the third test.)

- [ ] **Step 3: Run the tests**

Run: `dotnet test src/examples/Motiv.EntityFramework.Tests`
Expected: PASS — if EF throws a client-evaluation/translation exception, the recombined tree is malformed (most likely parameter rebinding); fix in `ExpressionComposer`, not by suppressing EF's behavior.

- [ ] **Step 4: Commit**

```bash
git add src/examples/Motiv.EntityFramework.Tests Motiv.slnx
git commit -m "test: prove EF Core server-side translation of composed specs"
```

---

### Task 12: Documentation

**Files:**
- Modify: `README.md` (brief example under Core Features)
- Create: `docs/expression-composition/index.md`, `docs/expression-composition/ToExpression.md`, `docs/expression-composition/Where.md`, `docs/expression-composition/toc.yml`
- Modify: `docs/toc.yml`, `docs/Overview.md`, `docs/builder/From.md`

**Interfaces:** none (documentation only). Follow the structure of an existing feature section (e.g. `docs/builder/`) for headings, tone, and toc wiring — read those files first.

- [ ] **Step 1: README** — add a short "Query provider integration" example under Core Features:

```csharp
var isAdult  = Spec.From((Customer c) => c.Age >= 18).Create("is adult");
var isActive = Spec.From((Customer c) => c.IsActive).Create("is active");

var eligible = isAdult & isActive;

// Translate to SQL via any IQueryable provider (e.g. EF Core)
var customers = dbContext.Customers.Where(eligible);

// Or take the raw expression anywhere expressions are accepted
Expression<Func<Customer, bool>> predicate = eligible.ToExpression();
```

- [ ] **Step 2: `docs/expression-composition/index.md`** — explain: expression-backed propositions (`Spec.From` with a bool predicate), what `ExpressionSpecBase`/`ExpressionPolicyBase`/`IExpressionSpec<TModel>` guarantee, closed composition, degradation rules (mixing with ordinary specs, `ChangeModelTo`, higher-order — with the Policy→Spec analogy), the mixed-metadata rule (method forms stay expression-backed via string coercion; operator forms require matching metadata, consistent with the documented `&&`/`||` limitation), and the faithful operator mapping table (`And`→`Expression.And`, `AndAlso`→`Expression.AndAlso`, `Or`→`Expression.Or`, `OrElse`→`Expression.OrElse`, `XOr`→`Expression.ExclusiveOr`, `Not`→`Expression.Not`). Note that explanations still come from decomposition while `ToExpression()` returns the original lambda.

- [ ] **Step 3: `ToExpression.md` and `Where.md`** — one page per method following the per-method page style of `docs/builder/From.md`: signature, remarks (leaves return the original lambda instance; composites recombine lazily and memoize; no simplification is applied), examples including recovering atomic cause expressions from a decomposed spec.

- [ ] **Step 4: Wire up navigation** — create `docs/expression-composition/toc.yml` listing the three pages; add the section to `docs/toc.yml` and a paragraph + link to `docs/Overview.md`; update `docs/builder/From.md` to state that bool-predicate `From` propositions are expression-backed and composable for query providers.

- [ ] **Step 5: Verify docs build if a docs pipeline exists** (check for a docfx config in `docs/`); otherwise proofread links, then commit:

```bash
git add README.md docs
git commit -m "docs: document queryable expression composition"
```

---

### Task 13: Final verification and mandated code-simplifier review

- [ ] **Step 1: Full solution test run**

Run: `dotnet test Motiv.slnx`
Expected: all projects green across all TFMs (Motiv.Tests, CodeFix/Analyzer/Generator tests, all example projects including the new EF project).

- [ ] **Step 2: Spec conformance check** — re-read `docs/superpowers/specs/2026-07-04-queryable-expression-composition-design.md` and verify each Testing bullet (1–8) has passing tests; verify no public API was renamed/removed.

- [ ] **Step 3: Spawn the `code-simplifier` agent** (mandatory per CLAUDE.md) over the full diff (`git diff main...HEAD`), focused on: duplication across the eight expression composites, decorator design, base-class overload surface, long methods in the transformer changes. Apply accepted improvements and re-run affected tests — but reject any change that breaks the byte-identical explanation guarantee or the overload-resolution behavior (the tests enforce both).

- [ ] **Step 4: Final commit and wrap-up**

```bash
git add -A
git commit -m "refactor: apply code-simplifier review findings"
```

Then run `dotnet test Motiv.slnx` one final time (expected: green) and use the superpowers:finishing-a-development-branch skill to decide integration (merge/PR).

---

## Plan Self-Review Notes

- **Spec coverage:** hierarchy (T1–2), recombination + memoization + faithful mapping (T3–6), policy preservation (T6), mixed metadata incl. documented operator fallback (T7), entry points + both spikes (T8), expression-backed decomposition + causal recovery (T9, T11 test 3), IQueryable layer (T10), EF proof (T11), docs (T12), degradations asserted in T4/T7/T8 tests. `ChangeModelTo` degradation needs no code change (base methods already return ordinary types) — covered by design, no task required.
- **Known judgment call:** spec risk #2 (cross-metadata operator closure) is resolved as the spec's documented fallback (method forms only) rather than spiked, because an `IExpressionSpec`-typed operator parameter would require an unsafe runtime cast — recorded in T7's interface notes and the docs task.
- **Type consistency:** composite constructor shape `(left, right, leftExpression, rightExpression)` is uniform across T4–T7; decorator names and `WithFragment` helper match between T1/T2/T8/T9.



