using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.Tests.ExpressionTreeProposition;

public class ExpressionTreeWithSingleTrueAssertionPropositionPolicyResultTests
{
    private static readonly Expression<Func<int, bool>> Expression = n => n > 0;

    private static BooleanResultBase<string> Underlying(bool satisfied) =>
        Spec.Build((int n) => n > 0)
            .WhenTrue("n > 0")
            .WhenFalse("n <= 0")
            .Create()
            .Evaluate(satisfied ? 1 : -1);

    private static ExpressionTreeWithSingleTrueAssertionPropositionPolicyResult<int, bool> Create(bool satisfied) =>
        new(satisfied,
            satisfied ? 1 : -1,
            Underlying(satisfied),
            "is positive",
            (_, _) => "is not positive",
            Expression,
            new SpecDescription("is positive"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorConstructorArgument(bool satisfied)
    {
        var result = Create(satisfied);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Fact]
    public void Value_ShouldReturnFixedTrueBecauseWhenSatisfied()
    {
        var result = Create(satisfied: true);

        result.Value.ShouldBe("is positive");
    }

    [Fact]
    public void Value_ShouldReturnResolvedFalseBecauseWhenNotSatisfied()
    {
        var result = Create(satisfied: false);

        result.Value.ShouldBe("is not positive");
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied);

        result.Underlying.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied);

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied);

        result.Causes.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied);

        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true, "is positive")]
    [InlineAutoData(false, "is not positive")]
    public void Explanation_ShouldUseResolvedBecauseString(bool satisfied, string expected)
    {
        var result = Create(satisfied);

        result.Explanation.Assertions.ShouldBe([expected]);
    }

    [Fact]
    public void Explanation_ShouldFallBackToStatementReasonWhenFalseBecauseIsWhitespace()
    {
        var result = new ExpressionTreeWithSingleTrueAssertionPropositionPolicyResult<int, bool>(
            satisfied: false,
            -1,
            Underlying(false),
            "is positive",
            (_, _) => "  ",
            Expression,
            new SpecDescription("is positive"));

        result.Explanation.Assertions.ShouldBe(["is positive == false"]);
    }
}
