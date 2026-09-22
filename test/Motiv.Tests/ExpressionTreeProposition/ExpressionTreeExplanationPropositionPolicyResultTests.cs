using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.Tests.ExpressionTreeProposition;

public class ExpressionTreeExplanationPropositionPolicyResultTests
{
    private static readonly Expression<Func<int, bool>> Expression = n => n > 0;

    private static BooleanResultBase<string> Underlying(bool satisfied) =>
        Spec.Build((int n) => n > 0)
            .WhenTrue("n > 0")
            .WhenFalse("n <= 0")
            .Create()
            .Evaluate(satisfied ? 1 : -1);

    private static ExpressionTreeExplanationPropositionPolicyResult<int, bool> Create(
        bool satisfied,
        Func<int, BooleanResultBase<string>, string> becauseResolver) =>
        new(satisfied,
            satisfied ? 1 : -1,
            Underlying(satisfied),
            becauseResolver,
            Expression,
            new SpecDescription("is positive"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorConstructorArgument(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because");

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Value_ShouldReturnResolvedBecauseString(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because value");

        result.Value.ShouldBe("because value");
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because");

        result.Underlying.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because");

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because");

        result.Causes.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because");

        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void MetadataTier_ShouldExposeResolvedValue(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because value");

        result.MetadataTier.Metadata.ShouldBe(["because value"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldUseResolvedBecauseString(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "because value");

        result.Explanation.Assertions.ShouldBe(["because value"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldFallBackToStatementReasonWhenBecauseIsWhitespace(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => "  ");

        result.Explanation.Assertions.ShouldBe([$"is positive == {(satisfied ? "true" : "false")}"]);
    }
}
