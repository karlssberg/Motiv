using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;

namespace Motiv.Tests.ExpressionTreeProposition;

public class MinimalExpressionTreePropositionBooleanResultTests
{
    private static readonly Expression<Func<int, bool>> Expression = n => n > 0;

    private static BooleanResultBase<string> Underlying(bool satisfied) =>
        Spec.Build((int n) => n > 0)
            .WhenTrue("n > 0")
            .WhenFalse("n <= 0")
            .Create()
            .Evaluate(satisfied ? 1 : -1);

    private static MinimalExpressionTreePropositionBooleanResult<int, bool> Create(
        bool satisfied,
        Func<int, BooleanResultBase<string>, IEnumerable<string>> assertionsResolver) =>
        new(satisfied,
            satisfied ? 1 : -1,
            Underlying(satisfied),
            assertionsResolver,
            Expression,
            new SpecDescription("is positive"));

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Satisfied_ShouldMirrorConstructorArgument(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom"]);

        result.Satisfied.ShouldBe(satisfied);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Underlying_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom"]);

        result.Underlying.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void UnderlyingWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom"]);

        result.UnderlyingWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Causes_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom"]);

        result.Causes.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void CausesWithValues_ShouldBeEmpty(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom"]);

        result.CausesWithValues.ShouldBeEmpty();
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void MetadataTier_ShouldExposeResolvedAssertionsAsMetadata(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom assertion"]);

        result.MetadataTier.Metadata.ShouldBe(["custom assertion"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldUseResolvedStringsWhenResolverReturnsStrings(bool satisfied)
    {
        var result = Create(satisfied, (_, _) => ["custom assertion"]);

        result.Explanation.Assertions.ShouldBe(["custom assertion"]);
    }

    [Theory]
    [InlineAutoData(true)]
    [InlineAutoData(false)]
    public void Explanation_ShouldFallBackToUnderlyingAssertionsWhenResolverReturnsNull(bool satisfied)
    {
        var underlying = Underlying(satisfied);
        var result = new MinimalExpressionTreePropositionBooleanResult<int, bool>(
            satisfied,
            satisfied ? 1 : -1,
            underlying,
            (_, _) => null!,
            Expression,
            new SpecDescription("is positive"));

        result.Explanation.Assertions.ShouldBe(underlying.Assertions);
    }
}
